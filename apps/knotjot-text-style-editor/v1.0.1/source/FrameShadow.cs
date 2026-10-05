using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace KnotJotTextStyleEditor;

// EN: Preserve Chromium's CSS frame paint order in a frozen WPF bitmap without evaluating user CSS as script or loading remote content.
// ZH: 以冻结 WPF 位图保留 Chromium 的 CSS 外框绘制顺序，不将用户 CSS 当脚本执行，也不加载远程内容。
public static class FrameShadow
{
    // EN: Limit raster memory rather than shadow syntax; serialize ownership of the isolated browser and keep only one cached frame.
    // ZH: 限制栅格内存而非阴影语法；串行使用隔离浏览器，且仅缓存一帧。
    public const int MaximumPixels = 4_000_000;
    static readonly SemaphoreSlim Gate = new(1, 1);
    static readonly object LifetimeLock = new();
    static CancellationTokenSource lifetime = new();
    static Browser? browser;
    static string? cachedKey;
    static FrameImage? cachedImage;

    // EN: Carry the frame, its offset from the border box, and diagnostics without changing persisted style fields.
    // ZH: 携带外框、相对边框的偏移及诊断信息，不改变持久化样式字段。
    public sealed record FrameImage(BitmapSource Bitmap, double Left, double Top, double Width, double Height, bool InvalidShadow, bool Reduced, string ComputedShadow);

    // EN: Register cleanup once, terminating only the browser created with this renderer's unique profile.
    // ZH: 仅注册一次清理，只终止本渲染器通过唯一配置目录创建的浏览器。
    static FrameShadow() => AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();

    // EN: Retain a synchronous diagnostic entry point; the editor uses RenderAsync so typing does not wait for Chromium.
    // ZH: 保留同步诊断入口；编辑器使用 RenderAsync，因此输入无需等待 Chromium。
    public static bool Apply(Canvas surface, double width, double height, double radius, string? css, string background = "transparent", string border = "transparent", double borderWidth = 0, string color = "#000000", double fontSize = 14)
    {
        var result = RenderAsync(width, height, radius, css, background, border, borderWidth, color, fontSize).GetAwaiter().GetResult();
        Present(surface, width, height, result); return !result.InvalidShadow;
    }

    // EN: Position the complete frame below editable content; another card background must not cover transparent interiors or inset shadows.
    // ZH: 将完整外框放在可编辑内容下方；另一层卡片背景不可覆盖透明内部或内阴影。
    public static void Present(Canvas surface, double width, double height, FrameImage result)
    {
        surface.Children.Clear(); surface.Width = width; surface.Height = height;
        var image = new Image { Source = result.Bitmap, Width = result.Width, Height = result.Height, IsHitTestVisible = false };
        Canvas.SetLeft(image, result.Left); Canvas.SetTop(image, result.Top); surface.Children.Add(image);
    }

    // EN: Serialize CSS as data, bound startup/render time, and discard cancelled protocol sessions without publishing partial frames.
    // ZH: 将 CSS 序列化为数据，限制启动及渲染时间，并丢弃取消后的协议会话，避免发布不完整外框。
    public static async Task<FrameImage> RenderAsync(double width, double height, double radius, string? css, string background, string border, double borderWidth, string color, double fontSize, CancellationToken cancellationToken = default)
    {
        var key = JsonSerializer.Serialize(new { width, height, radius, shadow = string.IsNullOrWhiteSpace(css) ? "none" : css, background, border, borderWidth, color, fontSize });
        CancellationToken shutdownToken; lock (LifetimeLock) shutdownToken = lifetime.Token;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, shutdownToken); deadline.CancelAfter(TimeSpan.FromSeconds(20));
        await Gate.WaitAsync(deadline.Token).ConfigureAwait(false);
        try
        {
            if (cachedKey == key && cachedImage != null) return cachedImage;
            browser ??= await Browser.StartAsync(deadline.Token).ConfigureAwait(false);
            var result = await browser.RenderAsync(key, deadline.Token).ConfigureAwait(false);
            cachedKey = key; cachedImage = result; return result;
        }
        catch { browser?.Dispose(); browser = null; throw; }
        finally { Gate.Release(); }
    }

    // EN: Release the owned process/profile and cached bitmap when the application or isolated test run ends.
    // ZH: 在应用或隔离测试结束时释放自有进程、配置目录及缓存位图。
    public static void Shutdown()
    {
        lock (LifetimeLock)
        {
            lifetime.Cancel(); Gate.Wait();
            try { browser?.Dispose(); browser = null; cachedKey = null; cachedImage = null; lifetime.Dispose(); lifetime = new CancellationTokenSource(); }
            finally { Gate.Release(); }
        }
    }

    // EN: Speak only to the loopback endpoint of a new headless Edge process while retaining its normal browser sandbox.
    // ZH: 仅连接新无头 Edge 进程的回环端点，同时保留正常浏览器沙箱。
    sealed class Browser : IDisposable
    {
        readonly Process process;
        readonly FrameBrowserJob job;
        readonly string profile;
        readonly ClientWebSocket socket = new();
        int nextId;

        // EN: Retain exact process/profile ownership; never enumerate or terminate the user's browser processes.
        // ZH: 保留精确进程及配置目录所有权；绝不枚举或终止用户浏览器进程。
        Browser(Process process, string profile, FrameBrowserJob job) { this.process = process; this.profile = profile; this.job = job; }

        // EN: Find Edge through App Paths and standard machine/user locations; missing runtime does not prevent editing or saving.
        // ZH: 通过 App Paths 和标准系统、用户目录查找 Edge；缺失运行时不阻止编辑或保存。
        static string Executable()
        {
            var candidates = new List<string?>();
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
                using (var key = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe")) candidates.Add(key?.GetValue(null) as string);
            foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) })
                candidates.Add(Path.Combine(root, "Microsoft", "Edge", "Application", "msedge.exe"));
            return candidates.FirstOrDefault(path => path != null && File.Exists(path)) ?? throw new InvalidOperationException("Microsoft Edge is required for CSS frame preview. Install or repair Edge; editing and saving remain available.");
        }

        // EN: Start a unique profile with loopback debugging, wait at most twelve seconds, and block all page requests before assigning CSS.
        // ZH: 使用唯一配置和回环调试启动，最多等待十二秒，并在设置 CSS 前阻止所有页面请求。
        public static async Task<Browser> StartAsync(CancellationToken cancellationToken)
        {
            var executable = Executable();
            var profile = Path.Combine(Path.GetTempPath(), "KnotJotTextStyleEditor", "frame-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(profile);
            var arguments = new[] { "--headless=new", "--edge-skip-compat-layer-relaunch", "--remote-debugging-port=0", "--remote-debugging-address=127.0.0.1", "--user-data-dir=" + profile, "--no-first-run", "--no-default-browser-check", "--disable-background-networking", "--disable-component-update", "--disable-sync", "--disable-extensions", "--force-device-scale-factor=1", "about:blank" };
            FrameBrowserJob? job = null; Process? child = null;
            try { job = new FrameBrowserJob(); child = job.Start(executable, arguments); }
            catch { job?.Dispose(); if (child != null) { try { child.Kill(true); } catch (InvalidOperationException) { } child.Dispose(); } DeleteProfile(profile); throw; }
            var owner = new Browser(child, profile, job);
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); startup.CancelAfter(TimeSpan.FromSeconds(12));
            try
            {
                var portFile = Path.Combine(profile, "DevToolsActivePort");
                while (!File.Exists(portFile)) { if (child.HasExited) throw new InvalidOperationException("The CSS preview browser exited during startup."); await Task.Delay(50, startup.Token).ConfigureAwait(false); }
                var port = int.Parse((await File.ReadAllLinesAsync(portFile, startup.Token).ConfigureAwait(false))[0]);
                using var http = new HttpClient(new HttpClientHandler { UseProxy = false });
                using var targets = JsonDocument.Parse(await http.GetStringAsync($"http://127.0.0.1:{port}/json/list", startup.Token).ConfigureAwait(false));
                var endpoint = targets.RootElement.EnumerateArray().First(target => target.GetProperty("type").GetString() == "page").GetProperty("webSocketDebuggerUrl").GetString()!;
                var uri = new Uri(endpoint); if (!uri.IsLoopback || uri.Port != port) throw new InvalidDataException("CSS preview endpoint is not owned loopback.");
                owner.socket.Options.Proxy = null; await owner.socket.ConnectAsync(uri, startup.Token).ConfigureAwait(false);
                await owner.Command("Network.enable", new { }, startup.Token).ConfigureAwait(false);
                await owner.Command("Network.setBlockedURLs", new { urls = new[] { "*" } }, startup.Token).ConfigureAwait(false);
                await owner.Command("Emulation.setDefaultBackgroundColorOverride", new { color = new { r = 0, g = 0, b = 0, a = 0 } }, startup.Token).ConfigureAwait(false);
                await owner.Command("Page.enable", new { }, startup.Token).ConfigureAwait(false);
                return owner;
            }
            catch { owner.Dispose(); throw; }
        }

        // EN: Exchange one CDP command, skip notifications, cap replies, and surface protocol errors instead of accepting an incomplete image.
        // ZH: 交换一个 CDP 命令，跳过通知、限制响应，并显示协议错误而非接受不完整图像。
        async Task<JsonElement> Command(string method, object parameters, CancellationToken cancellationToken)
        {
            var id = ++nextId; var bytes = JsonSerializer.SerializeToUtf8Bytes(new { id, method, @params = parameters });
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            var buffer = new byte[65536];
            while (true)
            {
                using var message = new MemoryStream(); WebSocketReceiveResult part;
                do
                {
                    part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                    if (part.MessageType == WebSocketMessageType.Close) throw new IOException("CSS preview browser connection closed.");
                    message.Write(buffer, 0, part.Count); if (message.Length > 32_000_000) throw new InvalidDataException("CSS preview reply exceeded the raster memory limit.");
                } while (!part.EndOfMessage);
                using var document = JsonDocument.Parse(message.ToArray()); var root = document.RootElement;
                if (!root.TryGetProperty("id", out var replyId) || replyId.GetInt32() != id) continue;
                if (root.TryGetProperty("error", out var error)) throw new InvalidOperationException("CSS preview: " + error.GetProperty("message").GetString());
                return root.GetProperty("result").Clone();
            }
        }

        // EN: Resolve CSS in a 640×420 context, derive full paint bounds from computed lengths, and downsample to four million pixels without dropping layers.
        // ZH: 在 640×420 上下文解析 CSS，从计算后长度推导完整绘制边界，并降采样到四百万像素而不丢弃图层。
        public async Task<FrameImage> RenderAsync(string data, CancellationToken cancellationToken)
        {
            await Command("Emulation.setDeviceMetricsOverride", new { width = 640, height = 420, deviceScaleFactor = 1, mobile = false }, cancellationToken).ConfigureAwait(false);
            var script = """
                (async()=>{
                // EN: Build an inert local document and assign raw CSS via custom properties as the main app does.
                // ZH: 创建无活动内容的本地文档，与主程序一样通过自定义属性设置原始 CSS。
                const p=DATA;document.head.innerHTML=`<meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'"><style>html,body{margin:0;background:transparent;color:#2c3140;font-family:"PingFang SC","Microsoft YaHei",-apple-system,system-ui,sans-serif}#world{position:absolute;transform-origin:0 0}#card{position:absolute;box-sizing:border-box;border-style:solid;background:var(--ts-bg,#fff);border-color:var(--ts-border,#dfe3eb);border-width:var(--ts-border-width,1.5px);border-radius:var(--ts-radius,12px);box-shadow:var(--ts-shadow,0 2px 8px #17203314)}</style>`;
                document.body.innerHTML='<div id="world"><div id="card"></div></div>';
                const world=document.getElementById('world'),card=document.getElementById('card');
                for(const [k,v] of Object.entries({'--ts-bg':p.background,'--ts-border':p.border,'--ts-border-width':p.borderWidth+'px','--ts-radius':p.radius+'px','--ts-color':p.color,'--ts-font-size':p.fontSize+'px','--ts-shadow':p.shadow}))world.style.setProperty(k,v);
                card.style.width=p.width+'px';card.style.height=p.height+'px';
                const computed=getComputedStyle(card),shadow=computed.boxShadow;
                // EN: Split top-level components only; computed color functions may contain commas and nested math.
                // ZH: 仅拆分顶层部分；计算后的颜色函数可包含逗号和嵌套计算。
                const layers=[];let depth=0,token='',tokens=[];
                for(const c of shadow+','){if(c==='(')depth++;if(c===')')depth--;if(!depth&&(c===','||/\s/.test(c))){if(token){tokens.push(token);token=''}if(c===','){layers.push(tokens);tokens=[]}}else token+=c}
                let left=0,top=0,right=p.width,bottom=p.height;
                for(const parts of layers){if(parts.includes('inset'))continue;const n=parts.filter(t=>/^[-+\d.e]+px$/i.test(t)).map(parseFloat);if(n.length<2)continue;const pad=Math.max(0,(n[2]||0)*1.5+(n[3]||0))+2;left=Math.min(left,n[0]-pad);top=Math.min(top,n[1]-pad);right=Math.max(right,p.width+n[0]+pad);bottom=Math.max(bottom,p.height+n[1]+pad)}
                left=Math.floor(left);top=Math.floor(top);right=Math.ceil(right);bottom=Math.ceil(bottom);
                const width=right-left,height=bottom-top,scale=Math.min(1,Math.sqrt(3980000/(width*height)),16380/width,16380/height);
                const pixelWidth=Math.max(1,Math.ceil(width*scale)),pixelHeight=Math.max(1,Math.ceil(height*scale));
                // EN: Freeze computed paint before resizing the viewport, retaining em/rem/vw/currentColor/var resolution.
                // ZH: 调整视口前冻结计算后的绘制值，保留 em/rem/vw/currentColor/var 的解析结果。
                const paint={background:computed.background,border:computed.border,borderRadius:computed.borderRadius,color:computed.color,fontSize:computed.fontSize,boxShadow:shadow};Object.assign(card.style,paint);
                card.style.left=(-left)+'px';card.style.top=(-top)+'px';world.style.transform='scale('+scale+')';
                const invalid=shadow==='none'&&!CSS.supports('box-shadow',p.shadow)&&!['inherit','initial','unset','revert','revert-layer'].includes(p.shadow.trim().toLowerCase());
                return {left,top,width:pixelWidth/scale,height:pixelHeight/scale,pixelWidth,pixelHeight,invalid,reduced:scale<1,shadow};
                })()
                """.Replace("DATA", data, StringComparison.Ordinal);
            var evaluation = await Command("Runtime.evaluate", new { expression = script, returnByValue = true, awaitPromise = true }, cancellationToken).ConfigureAwait(false);
            if (evaluation.TryGetProperty("exceptionDetails", out var exception)) throw new InvalidOperationException("CSS frame computation failed: " + exception.ToString());
            var bounds = evaluation.GetProperty("result").GetProperty("value");
            if ((long)bounds.GetProperty("pixelWidth").GetInt32() * bounds.GetProperty("pixelHeight").GetInt32() > MaximumPixels) throw new InvalidDataException("CSS frame exceeded the raster memory limit.");
            await Command("Emulation.setDeviceMetricsOverride", new { width = bounds.GetProperty("pixelWidth").GetInt32(), height = bounds.GetProperty("pixelHeight").GetInt32(), deviceScaleFactor = 1, mobile = false }, cancellationToken).ConfigureAwait(false);
            await Command("Runtime.evaluate", new { expression = "new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)))", awaitPromise = true }, cancellationToken).ConfigureAwait(false);
            var shot = await Command("Page.captureScreenshot", new { format = "png", captureBeyondViewport = false }, cancellationToken).ConfigureAwait(false);
            using var png = new MemoryStream(Convert.FromBase64String(shot.GetProperty("data").GetString()!));
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = png; bitmap.EndInit(); bitmap.Freeze();
            return new FrameImage(bitmap, bounds.GetProperty("left").GetDouble(), bounds.GetProperty("top").GetDouble(), bounds.GetProperty("width").GetDouble(), bounds.GetProperty("height").GetDouble(), bounds.GetProperty("invalid").GetBoolean(), bounds.GetProperty("reduced").GetBoolean(), bounds.GetProperty("shadow").GetString()!);
        }

        // EN: Stop only the owned process tree and delete only the verified unique profile beneath the editor's temporary directory.
        // ZH: 仅停止自有进程树，并仅删除编辑器临时目录下经过验证的唯一配置目录。
        public void Dispose()
        {
            socket.Dispose();
            job.Dispose();
            try { if (!process.HasExited) { process.Kill(true); process.WaitForExit(2000); } } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
            process.Dispose();
            DeleteProfile(profile);
        }

        // EN: Remove only an owned GUID profile under the verified temporary root, including early launch failures before a Browser exists.
        // ZH: 仅删除已验证临时根目录下自有 GUID 配置，也覆盖 Browser 对象建立前的启动失败。
        static void DeleteProfile(string profile)
        {
            var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "KnotJotTextStyleEditor")) + Path.DirectorySeparatorChar;
            if (Path.GetFullPath(profile).StartsWith(root, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(profile).StartsWith("frame-", StringComparison.Ordinal) && Guid.TryParseExact(Path.GetFileName(profile)[6..], "N", out _))
                for (var attempt = 0; attempt < 30 && Directory.Exists(profile); attempt++)
                {
                    try { Directory.Delete(profile, true); return; }
                    catch (IOException) { Thread.Sleep(50); }
                    catch (UnauthorizedAccessException) { Thread.Sleep(50); }
                }
        }
    }
}
