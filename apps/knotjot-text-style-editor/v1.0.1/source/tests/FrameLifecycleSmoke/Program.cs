using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KnotJotTextStyleEditor;

internal static class Program
{
    const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static readonly List<object> results = [];
    static string output = "";
    static int failures;
    // EN: Fail the current independent check without hiding later lifecycle failures. ZH: 独立报告当前检查失败，不掩盖后续生命周期故障。
    static void Expect(bool condition, string message) { if (!condition) throw new Exception(message); }
    // EN: Record pass/fail evidence for every independent scenario. ZH: 为每个独立场景记录通过或失败证据。
    static void Check(string name, Action action) { try { action(); results.Add(new { name, pass = true }); Console.WriteLine("PASS " + name); } catch (Exception error) { failures++; results.Add(new { name, pass = false, error = error.GetBaseException().ToString() }); Console.WriteLine("FAIL " + name + " " + error.GetBaseException().Message); } }
    // EN: Access only the test-owned hidden window/browser state needed to verify real asynchronous behavior. ZH: 仅访问测试拥有的隐藏窗口及浏览器状态，以验证真实异步行为。
    static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, Flags)!.GetValue(owner)!;
    // EN: Invoke an existing zero-argument editor refresh in the hidden test window. ZH: 在隐藏测试窗口调用已有无参数编辑器刷新。
    static void Call(object owner, string name) => owner.GetType().GetMethod(name, Flags)!.Invoke(owner, null);
    // EN: Retrieve the latest frame task rather than waiting on a superseded request. ZH: 获取最新外框任务，避免等待已被替代的请求。
    static Task Pending(MainWindow window) => Get<Task>(window, "framePreviewTask");
    // EN: Enter the real preview tab explicitly; ordinary design windows intentionally do not start Chromium.
    // ZH: 明确进入真实预览页；普通设计窗口刻意不启动 Chromium。
    static MainWindow PreviewWindow() { var window = new MainWindow(); ((TabControl)window.FindName("WorkTabs")).SelectedIndex = 1; return window; }
    // EN: Pump the WPF dispatcher with a hard deadline so queued continuations run without showing or automating native windows.
    // ZH: 在严格期限内驱动 WPF 分派器，使排队续体执行，不显示或自动操作原生窗口。
    static void Pump(Task task)
    {
        var deadline = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            if (deadline.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Hidden WPF frame exceeded the test deadline.");
            var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }
    // EN: Render and retain the complete native result while enforcing the actual allocated bitmap dimensions.
    // ZH: 渲染并保存完整原生结果，同时检查实际分配位图的尺寸。
    static FrameShadow.FrameImage Render(string shadow, double width = 160, double height = 80, CancellationToken token = default)
    {
        var result = FrameShadow.RenderAsync(width, height, 20, shadow, "transparent", "transparent", 0, "#3080a0", 14, token).GetAwaiter().GetResult();
        Expect((long)result.Bitmap.PixelWidth * result.Bitmap.PixelHeight <= FrameShadow.MaximumPixels, "Final bitmap exceeded four million pixels."); return result;
    }
    // EN: Save the actual bounded frame as reviewable PNG evidence. ZH: 将实际有限外框保存为可审阅 PNG 证据。
    static void Save(FrameShadow.FrameImage image, string name) { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image.Bitmap)); using var file = File.Create(Path.Combine(output, name + ".png")); encoder.Save(file); }
    // EN: Capture the browser object and its exact profile; tests never select processes by browser name alone.
    // ZH: 获取浏览器对象及其精确配置目录；测试绝不单凭浏览器名称选择进程。
    static (Process process, string profile) Owner()
    {
        var browser = typeof(FrameShadow).GetField("browser", Flags)!.GetValue(null)!; return (Get<Process>(browser, "process"), Get<string>(browser, "profile"));
    }
    // EN: Verify resource limits, cancellation recovery, real WPF layer ordering and latest-request publication, then shut down the owned renderer.
    // ZH: 验证资源边界、取消恢复、真实 WPF 图层顺序及最新请求发布，然后关闭自有渲染器。
    [STAThread]
    static int Main(string[] args)
    {
        // EN: This explicit test-child mode creates one hidden sleeping descendant to verify job inheritance after the launcher exits.
        // ZH: 此明确测试子模式创建一个隐藏休眠后代，以验证启动器退出后的作业继承。
        if (args[0] == "--owned-descendant")
        {
            var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe")) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-Command", "Start-Sleep -Seconds 30" }) start.ArgumentList.Add(argument);
            using var child = Process.Start(start)!; File.WriteAllText(args[1], child.Id.ToString()); return 0;
        }
        output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("KNOTJOT_TEXT_STYLE_EDITOR_DATA_DIR", Path.Combine(output, "profile"));
        _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Check("suspended creation owns a descendant after launcher exit", () => {
            var type = typeof(FrameShadow).Assembly.GetType("KnotJotTextStyleEditor.FrameBrowserJob")!; using var job = (IDisposable)Activator.CreateInstance(type)!;
            var file = Path.Combine(output, "owned-descendant.pid"); var childArgs = string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase) ? new[] { Assembly.GetExecutingAssembly().Location, "--owned-descendant", file } : new[] { "--owned-descendant", file };
            using var launcher = (Process)type.GetMethod("Start")!.Invoke(job, [Environment.ProcessPath!, childArgs])!;
            Expect(launcher.WaitForExit(5000) && File.Exists(file), "Owned launcher did not create the recorded descendant.");
            using var descendant = Process.GetProcessById(int.Parse(File.ReadAllText(file))); job.Dispose(); Expect(descendant.WaitForExit(2000), "Descendant escaped ownership before launcher exit.");
        });
        Check("suspended creation rejects a missing executable", () => {
            var type = typeof(FrameShadow).Assembly.GetType("KnotJotTextStyleEditor.FrameBrowserJob")!; using var job = (IDisposable)Activator.CreateInstance(type)!; bool rejected = false;
            try { type.GetMethod("Start")!.Invoke(job, [Path.Combine(output, "missing-browser.exe"), Array.Empty<string>()]); } catch (TargetInvocationException error) when (error.InnerException is System.ComponentModel.Win32Exception) { rejected = true; }
            Expect(rejected, "Missing executable did not fail before resuming a process.");
        });
        Check("initial and design-only editing never starts a browser", () => { FrameShadow.Shutdown(); var window = new MainWindow(); ((TextBox)window.FindName("ShadowBox")).Text = "inset 3px 5px 2px red"; Pump(Task.Delay(250)); Expect(typeof(FrameShadow).GetField("browser", Flags)!.GetValue(null) == null && Pending(window).IsCompleted, "Design-only editing started a browser."); window.Close(); });
        Check("leaving preview cancels its pending request", () => { FrameShadow.Shutdown(); var window = PreviewWindow(); var task = Pending(window); ((TabControl)window.FindName("WorkTabs")).SelectedIndex = 0; Pump(task); Expect(typeof(FrameShadow).GetField("browser", Flags)!.GetValue(null) == null, "Hidden preview launched after leaving its tab."); window.Close(); });
        Check("integer pixel ceiling at wide boundary", () => { var image = Render("0 0 0 transparent", 16380, 244.5); Save(image, "wide-budget"); Expect(image.Reduced && image.Width >= 16380, "Wide bounds were truncated instead of reduced."); });
        Check("large shadow preserves full bounds and signals reduction", () => { var image = Render("-12000px 1800px 900px 200px #0008, inset 5px 0 0 red"); Save(image, "large-shadow"); Expect(image.Reduced && image.Left < -13000 && image.Width > 13000 && image.ComputedShadow.Contains("inset"), "Large shadow was truncated or silently dropped."); });
        Check("hundred layers are retained", () => { var image = Render(string.Join(",", Enumerable.Range(0, 100).Select(i => $"{i % 15}px {i % 9}px 0 1px #0001"))); Save(image, "hundred-layers"); Expect(image.ComputedShadow.Split("rgba(").Length - 1 == 100, "Layer count was truncated."); });
        Check("one frame cache includes colors and background", () => { var first = Render("0 8px 3px red"); var same = Render("0 8px 3px red"); var changed = Render("0 8px 3px blue"); Expect(ReferenceEquals(first, same) && !ReferenceEquals(first, changed), "Cache omitted the actual CSS request."); });
        Check("cancel before request preserves active renderer", () => { var before = Owner().process.Id; using var c = new CancellationTokenSource(); c.Cancel(); try { Render("0 1px red", token: c.Token); throw new Exception("Cancelled render succeeded."); } catch (OperationCanceledException) { } Expect(Owner().process.Id == before, "A queued cancelled request replaced the active browser."); });
        Check("in flight cancellation cleans session and next request recovers", () => { var previous = Owner(); var processId = previous.process.Id; using var c = new CancellationTokenSource(5); var watch = Stopwatch.StartNew(); try { Render("100000px 0 500px red", token: c.Token); throw new Exception("Expected in-flight cancellation."); } catch (OperationCanceledException) { } Expect(watch.Elapsed.TotalSeconds < 5, "Cancellation waited for the full browser deadline."); var recovered = Render("inset 0 0 0 4px blue"); Expect(recovered.ComputedShadow.Contains("inset") && Owner().process.Id != processId, "Cancelled protocol session was reused."); Expect(!Directory.Exists(previous.profile), "Cancelled owned profile remained."); });
        Check("unexpected owned browser exit fails visibly then recovers", () => { var previous = Owner(); previous.process.Kill(true); previous.process.WaitForExit(2000); bool rejected = false; try { Render("0 6px 2px green"); } catch { rejected = true; } Expect(rejected, "Dead browser produced a false successful frame."); Expect(Render("0 6px 2px green").Bitmap != null, "Next request did not recover."); });
        Check("shutdown during startup cannot publish a late renderer", () => {
            FrameShadow.Shutdown(); var task = FrameShadow.RenderAsync(160, 80, 20, "0 7px 2px #abc", "transparent", "transparent", 0, "#000", 14); Thread.Sleep(5); FrameShadow.Shutdown();
            bool cancelled = false; try { task.GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; }
            Expect(cancelled && typeof(FrameShadow).GetField("browser", Flags)!.GetValue(null) == null, "Shutdown allowed an in-flight startup to publish a browser."); Render("0 7px 2px #abc");
        });
        Check("preview failure stays visible while style saving works", () => {
            var previous = Owner(); previous.process.Kill(true); previous.process.WaitForExit(2000); var window = PreviewWindow(); ((TextBox)window.FindName("ShadowBox")).Text = "inset 1px 3px 2px orange"; Pump(Pending(window));
            var status = (TextBlock)window.FindName("FramePreviewStatus"); Expect(status.ToolTip != null && ((Canvas)window.FindName("PreviewShadow")).Children.Count == 0, "Browser failure was silently treated as a completed frame.");
            var file = Path.Combine(output, "preview-failure.knotjot-textstyle"); typeof(MainWindow).GetMethod("SaveStandaloneTo", Flags)!.Invoke(window, [file]); Expect(File.ReadAllText(file).Contains("inset 1px 3px 2px orange"), "Preview failure disabled style saving.");
            Call(window, "RefreshPreview"); Pump(Pending(window)); Expect(status.ToolTip == null && ((Canvas)window.FindName("PreviewShadow")).Children.Count == 1, "Retry failed to clear browser error."); window.Close();
        });
        Check("latest hidden WPF request wins and clears stale diagnostics", () => {
            var window = PreviewWindow(); var shadow = (TextBox)window.FindName("ShadowBox"); var bg = (TextBox)window.FindName("BgBox");
            shadow.Text = "inset 0 0 0 100px red"; Pump(Task.Delay(165)); var old = Pending(window);
            bg.Text = "transparent"; shadow.Text = "inset 0 0 0 100px blue"; ((TextBlock)window.FindName("FramePreviewStatus")).ToolTip = "old failure"; Pump(Pending(window)); Pump(old);
            var surface = (Canvas)window.FindName("PreviewShadow"); var image = (BitmapSource)((Image)surface.Children[0]).Source; var pixel = new byte[4]; image.CopyPixels(new Int32Rect(image.PixelWidth / 2, image.PixelHeight / 2, 1, 1), pixel, 4, 0);
            Expect(pixel[0] > 240 && pixel[2] < 5, "Superseded red frame overwrote the latest blue frame.");
            Expect(((SolidColorBrush)((Border)window.FindName("PreviewCard")).Background).Color.A == 0, "WPF background covered the inset shadow.");
            Expect(((TextBlock)window.FindName("FramePreviewStatus")).ToolTip == null, "Recovered preview retained a stale error."); window.Close();
        });
        Check("full shadow extent participates in preview scrolling", () => { var window = PreviewWindow(); ((TextBox)window.FindName("ShadowBox")).Text = "600px 0 0 20px red"; Pump(Pending(window)); var card = (Border)window.FindName("PreviewCard"); var surface = (Canvas)window.FindName("PreviewShadow"); Expect(card.Margin.Right > 620 && card.Margin == surface.Margin, "Shadow overflow was silently clipped by the old fixed margin."); window.Close(); });
        Check("closing pending window cancels publication", () => { var window = PreviewWindow(); ((TextBox)window.FindName("ShadowBox")).Text = "inset 0 0 0 20px green"; var task = Pending(window); window.Close(); Pump(task); Expect(task.IsCompletedSuccessfully, "Closing leaked an asynchronous error."); });
        Check("composite replacement suppresses full frame", () => { var window = PreviewWindow(); var style = Get<TextBoxStyle>(window, "style"); style.ReplaceFrame = true; style.Layers.Add(new StyleLayer()); Call(window, "RefreshPreview"); Pump(Pending(window)); Expect(((Canvas)window.FindName("PreviewShadow")).Children.Count == 0, "Composite replacement retained ordinary frame shadows."); window.Close(); });
        Check("shutdown removes only owned profile and renderer", () => { Render("0 5px 0 purple"); var previous = Owner(); var id = previous.process.Id; FrameShadow.Shutdown(); Expect(!Directory.Exists(previous.profile), "Shutdown left the owned profile."); Expect(typeof(FrameShadow).GetField("browser", Flags)!.GetValue(null) == null, "Shutdown retained the renderer."); bool exists; try { using var p = Process.GetProcessById(id); exists = !p.HasExited; } catch (ArgumentException) { exists = false; } Expect(!exists, "Owned process survived shutdown."); });
        FrameShadow.Shutdown(); File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true })); Console.WriteLine($"FRAME_LIFECYCLE checks={results.Count} failures={failures}"); return failures == 0 ? 0 : 1;
    }
}
