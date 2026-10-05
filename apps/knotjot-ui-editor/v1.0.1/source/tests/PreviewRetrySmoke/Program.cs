using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using KnotJotUiEditor;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

// Exercise actual preview callbacks with an isolated browser, mapped HTML and preference destination.
// 使用隔离浏览器、映射 HTML 及偏好目标验证实际预览回调。
static class Program
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly List<object> Results = new();
    static readonly List<object> NavigationEvents = new();
    static string output = "";
    static int failures;
    static int navigationCount;
    static MainWindow ui = null!;
    static WebView2 web = null!;
    static string realFile = "";
    const string RealHtml = "<!doctype html><html><body><div id='audit-real' contenteditable='true'>mapped fixture</div></body></html>";
    const string DeferredHtml = "<!doctype html><html><head><script src='https://knotjot-audit.invalid/hold.js'></script></head><body><div id='audit-real' contenteditable='true'>mapped fixture</div></body></html>";

    // Run asynchronous WebView2 cases on an STA dispatcher and return a failing exit code for any regression.
    // 在 STA 消息循环运行异步 WebView2 案例，任何回归失败都返回失败退出码。
    [STAThread] static int Main(string[] args)
    {
        if (args[0] == "--approved") return RunApproved(args[1], args[2]);
        output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var completion = new TaskCompletionSource(); var frame = new DispatcherFrame();
        // Retain unexpected setup failures and always release the temporary dispatcher loop.
        // 保留意外初始化失败，并始终释放临时消息循环。
        app.Dispatcher.BeginInvoke(async () => { try { await Run(); completion.SetResult(); } catch (Exception ex) { completion.SetException(ex); } finally { frame.Continue = false; } });
        Dispatcher.PushFrame(frame);
        try { completion.Task.GetAwaiter().GetResult(); } catch (Exception ex) { Record("setup", false, ex.ToString()); }
        File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { date = DateTimeOffset.Now, failures, results = Results }, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(output, "navigation-events.json"), JsonSerializer.Serialize(NavigationEvents, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PREVIEW_RETRY_SMOKE failures=" + failures); app.Shutdown(); return failures == 0 ? 0 : 1;
    }

    // Record one result without suppressing subsequent independent cases.
    // 记录一个结果，不阻止后续独立案例。
    static void Record(string name, bool passed, object evidence) { if (!passed) failures++; Results.Add(new { name, passed, evidence }); Console.WriteLine((passed ? "PASS " : "FAIL ") + name + " " + JsonSerializer.Serialize(evidence)); }

    // Execute each case with its own bounded failure report.
    // 对每个案例执行有界的独立失败报告。
    static async Task Check(string name, Func<Task<object>> action) { try { Record(name, true, await action()); } catch (Exception ex) { Record(name, false, ex.ToString()); } }

    // Fail on an observable behavioral invariant.
    // 在可观察行为不满足约束时失败。
    static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    // Invoke the existing production path and unwrap its original failure.
    // 调用现有生产路径并展开其原始失败。
    static object? Call(string name, params object?[] args) { try { return ui.GetType().GetMethods(Flags).First(x => x.Name == name && x.GetParameters().Length == args.Length).Invoke(ui, args); } catch (TargetInvocationException ex) { throw ex.InnerException!; } }

    // Read existing state only for assertions; do not add a production-only test hook.
    // 仅为断言读取现有状态，不增加生产专用测试入口。
    static T Field<T>(string name) => (T)typeof(MainWindow).GetField(name, Flags)!.GetValue(ui)!;

    // Raise a real wired button callback without using desktop pointer automation.
    // 触发真实已绑定按钮回调，不使用桌面指针自动化。
    static void Click(string name) => ((Button)ui.FindName(name)).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    // Decode the real browser's JSON result for one expression.
    // 解码真实浏览器对一条表达式返回的 JSON 结果。
    static async Task<T> Js<T>(string expression)
    {
        string value = await web.ExecuteScriptAsync(expression);
        return value == "null" ? default! : JsonSerializer.Deserialize<T>(value)!;
    }

    // Pump browser events during a bounded asynchronous polling window.
    // 在有界异步轮询窗口中继续处理浏览器事件。
    static async Task Until(Func<Task<bool>> condition, string description) { for (int i = 0; i < 120; i++) { if (await condition()) return; await Task.Delay(50); } throw new TimeoutException(description); }

    // Inspect the actual error-panel visibility.
    // 检查实际错误面板的可见性。
    static bool ErrorVisible() => ((FrameworkElement)ui.FindName("PreviewErrorPanel")).Visibility == Visibility.Visible;

    // Redirect the readonly preference destination in this process before the first production Save call.
    // 在首次生产 Save 调用前，仅在本进程重定向只读偏好目标。
    static void IsolatePreferences()
    {
        IsolateFilePath(typeof(Prefs), "PathFile", Path.Combine(output, "isolated-prefs.json"));
    }

    // Change one static path only inside this audit process and verify the destination before any persistence.
    // 仅在本审查进程修改一个静态路径，并在任何持久化之前校验目标。
    static void IsolateFilePath(Type type, string name, string destination)
    {
        RuntimeHelpers.RunClassConstructor(type.TypeHandle);
        var field = type.GetField(name, Flags)!;
        var setter = new DynamicMethod("SetAuditPreferencePath", typeof(void), new[] { typeof(string) }, typeof(Program), true);
        var il = setter.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Stsfld, field); il.Emit(OpCodes.Ret);
        setter.CreateDelegate<Action<string>>()(destination); Need((string)field.GetValue(null)! == destination, "Preference path isolation failed");
    }

    // Execute the unchanged ApprovedSmoke entry point after isolating its save/open recent-list side effects.
    // 隔离保存与打开对最近列表的影响后，执行未改动的 ApprovedSmoke 入口。
    static int RunApproved(string assemblyFile, string evidenceDirectory)
    {
        output = Path.GetFullPath(evidenceDirectory); Directory.CreateDirectory(output); IsolatePreferences();
        IsolateFilePath(typeof(MainWindow), "RecentPath", Path.Combine(output, "recent.json"));
        IsolateFilePath(typeof(MainWindow), "LegacyRecentPath", Path.Combine(output, "missing-legacy-recent.json"));
        var assembly = Assembly.LoadFrom(Path.GetFullPath(assemblyFile));
        try { return (int)assembly.EntryPoint!.Invoke(null, new object[] { new[] { output } })!; }
        catch (TargetInvocationException ex) { Console.Error.WriteLine(ex.InnerException); return 1; }
    }

    // Restore the built-in page through its actual button so each case starts from known production state.
    // 通过实际按钮恢复内置页面，使每个案例从已知生产状态开始。
    static async Task Builtin()
    {
        var finished = new TaskCompletionSource();
        // Wait for a new navigation completion; the prior built-in DOM can still exist just after clicking.
        // 等待新的导航完成；点击后上一份内置 DOM 可能仍短暂存在。
        EventHandler<CoreWebView2NavigationCompletedEventArgs> loaded = (_, e) => { if (e.IsSuccess && web.Source?.ToString() == "about:blank") finished.TrySetResult(); };
        web.CoreWebView2.NavigationCompleted += loaded;
        try { Click("BtnPreviewBuiltin"); await finished.Task.WaitAsync(TimeSpan.FromSeconds(6)); }
        finally { web.CoreWebView2.NavigationCompleted -= loaded; }
        await Until(async () => await Js<bool>("!!document.getElementById('preview-nodes')"), "Built-in DOM");
        await Until(() => Task.FromResult(!ErrorVisible()), "Built-in error cleared");
    }

    // Point the actual RefreshPreview path at a test-owned virtual-host HTML fixture.
    // 让实际 RefreshPreview 路径指向测试自有的虚拟主机 HTML 样本。
    static void OpenReal() { Prefs.PreviewSource = 1; Prefs.KnotJotPath = realFile; Call("RefreshPreview"); }

    // Require the mapped document and its generated CSS to be ready, not merely navigation completion.
    // 要求映射文档及生成 CSS 就绪，不只依赖导航完成。
    static async Task RealReady()
    {
        await Until(async () => await Js<bool>("!!document.getElementById('audit-real') && !!document.getElementById('knotjot-skin')"), "Real page marker and CSS");
        await Until(() => Task.FromResult(!ErrorVisible()), "Real page error cleared");
    }

    // Cancel the mapped navigation inside WebView2 and wait for the real production error panel.
    // 在 WebView2 内取消映射导航，并等待真实生产错误面板。
    static async Task FailReal()
    {
        web.CoreWebView2.NavigationStarting += CancelReal;
        try { OpenReal(); await Until(() => Task.FromResult(ErrorVisible()), "Initial navigation failure"); }
        finally { web.CoreWebView2.NavigationStarting -= CancelReal; }
    }

    // Inject a controlled failure only for the test-owned mapped document.
    // 仅对测试自有映射文档注入受控失败。
    static void CancelReal(object? sender, CoreWebView2NavigationStartingEventArgs e) { if (e.Uri.StartsWith("https://knotjot-preview/")) e.Cancel = true; }

    // Retain a screenshot directly from WebView2 without capturing the user's desktop.
    // 直接从 WebView2 保留截图，不捕获用户桌面。
    static async Task Screenshot(string name) { using var file = File.Create(Path.Combine(output, name + ".png")); await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, file); }

    // Cover retry, repeated failure, ordinary CSS updates, pending navigation and obsolete callbacks.
    // 覆盖重试、连续失败、普通 CSS 更新、等待中的导航及过期回调。
    static async Task Run()
    {
        Environment.SetEnvironmentVariable("KNOTJOT_UI_EDITOR_DATA_DIR", Path.Combine(output, "ui-profile")); IsolatePreferences();
        ui = new MainWindow();
        foreach (string name in new[] { "InitAdorners", "InitDesign", "WireEvents", "WireShapePanel", "WireInkImagePanels", "WirePen", "WireTextPanel" }) Call(name);
        Call("SwitchTarget", "card", true); typeof(MainWindow).GetField("_ready", Flags)!.SetValue(ui, true);
        web = (WebView2)ui.FindName("Web"); ((Grid)ui.FindName("WebHost")).Children.Remove(web);
        var host = new Window { Content = web, Width = 1100, Height = 760, ShowInTaskbar = false, ShowActivated = false, Left = -15000, Top = 0 }; host.Show();
        string folder = Path.Combine(output, "mapped"); Directory.CreateDirectory(folder); realFile = Path.Combine(folder, "index.html");
        File.WriteAllText(realFile, RealHtml);
        try
        {
            // Start two production initialization calls before either resumes, using only an isolated browser profile.
            // 任一初始化恢复前启动两个生产调用，仅使用隔离浏览器配置。
            web.CreationProperties = new CoreWebView2CreationProperties { UserDataFolder = Path.Combine(output, "webview-profile") };
            var initialOne = (Task)Call("EnsureWebAsync")!; var initialTwo = (Task)Call("EnsureWebAsync")!;
            await Task.WhenAll(initialOne, initialTwo);
            // Count genuine browser navigations and retain IDs so ordering assertions remain reviewable.
            // 统计真实浏览器导航并保留 ID，使顺序断言可以复核。
            web.CoreWebView2.NavigationStarting += (_, e) => { navigationCount++; NavigationEvents.Add(new { phase = "starting", id = e.NavigationId, uri = e.Uri }); };
            // Record success and failure completion for the exact navigation IDs.
            // 记录各准确导航 ID 的成功与失败完成事件。
            web.CoreWebView2.NavigationCompleted += (_, e) => NavigationEvents.Add(new { phase = "completed", id = e.NavigationId, success = e.IsSuccess, error = e.WebErrorStatus.ToString() });
            File.WriteAllText(Path.Combine(output, "runtime.json"), JsonSerializer.Serialize(new { webview = web.CoreWebView2.Environment.BrowserVersionString, assembly = typeof(MainWindow).Assembly.Location, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(MainWindow).Assembly.Location))) }, new JsonSerializerOptions { WriteIndented = true }));

            // Concurrent first initialization must preserve loaded state and the CSS-only path after the first successful navigation.
            // 首次并发初始化必须在第一次导航成功后保留已加载状态与仅更新 CSS 的路径。
            await Check("U05-concurrent-initialization-css-only", async () =>
            {
                await Builtin(); OpenReal(); await RealReady(); int before = navigationCount; bool loadedBeforeEdit = Field<bool>("_realLoaded");
                ((TextBox)ui.FindName("TxtName")).Text = "concurrent-init-css"; Call("RefreshPreview");
                await Until(async () => await Js<bool>("!!document.getElementById('knotjot-skin') && document.getElementById('knotjot-skin').textContent.includes('concurrent-init-css')"), "CSS after concurrent initialization");
                var state = new { loadedBeforeEdit, loadedAfterEdit = Field<bool>("_realLoaded"), navigationDelta = navigationCount - before };
                Need(state.loadedBeforeEdit && state.loadedAfterEdit && state.navigationDelta == 0, "Concurrent initialization duplicated state transitions: " + JsonSerializer.Serialize(state));
                return state;
            });

            // A failed document must not remain eligible for CSS-only refresh.
            // 失败文档不能继续满足仅更新 CSS 的条件。
            await Check("U05-failure-invalidates-loaded", async () =>
            {
                await Builtin(); await FailReal(); Need(!Field<bool>("_realLoaded"), "Failed navigation retains _realLoaded=true");
                return new { loaded = Field<bool>("_realLoaded"), errorVisible = ErrorVisible() };
            });
            // The real Retry button must create a fresh navigation after removing the fault.
            // 移除故障后，真实重试按钮必须创建新导航。
            await Check("U05-retry-reloads-after-failure", async () =>
            {
                await Builtin(); await FailReal(); int before = navigationCount; Click("BtnPreviewRetry"); await RealReady();
                Need(navigationCount == before + 1, "Retry navigation delta=" + (navigationCount - before)); await Screenshot("retry-recovered");
                return new { navigationDelta = navigationCount - before, source = web.Source.ToString(), errorVisible = ErrorVisible() };
            });
            // Consecutive failing retries must remain visible before a later successful retry recovers.
            // 连续失败的重试必须保持错误可见，直到后续成功重试恢复。
            await Check("U05-repeated-failure-then-recovery", async () =>
            {
                await Builtin(); await FailReal(); int before = navigationCount; web.CoreWebView2.NavigationStarting += CancelReal;
                try
                {
                    for (int i = 0; i < 2; i++) { int previous = navigationCount; Click("BtnPreviewRetry"); await Until(() => Task.FromResult(navigationCount > previous && ErrorVisible()), "Repeated retry failure " + i); Need(!Field<bool>("_realLoaded"), "Failed retry cached as loaded"); }
                }
                finally { web.CoreWebView2.NavigationStarting -= CancelReal; }
                Click("BtnPreviewRetry"); await RealReady(); Need(navigationCount == before + 3, "Repeated retry navigation count mismatch");
                return new { navigationDelta = navigationCount - before, loaded = Field<bool>("_realLoaded"), errorVisible = ErrorVisible() };
            });
            // Successful same-page editing must preserve document state and inject updated CSS without navigating.
            // 成功页面上的编辑必须保留文档状态，只更新 CSS 而不导航。
            await Check("U05-successful-refresh-css-only", async () =>
            {
                await Builtin(); OpenReal(); await RealReady(); int before = navigationCount;
                await web.ExecuteScriptAsync("document.getElementById('audit-real').textContent='preserve edited content';window.auditSentinel=79;");
                for (int i = 0; i < 3; i++) { ((TextBox)ui.FindName("TxtName")).Text = "css-only-" + i; Call("RefreshPreview"); }
                await Until(async () => await Js<bool>("document.getElementById('knotjot-skin').textContent.includes('css-only-2')"), "Latest CSS-only edit");
                Need(navigationCount == before, "Ordinary refresh triggered navigation"); Need(await Js<bool>("window.auditSentinel===79 && document.getElementById('audit-real').textContent==='preserve edited content'"), "Ordinary refresh lost document state");
                return new { navigationDelta = navigationCount - before, preservedText = await Js<string>("document.getElementById('audit-real').textContent") };
            });
            // Explicit retries must reload even a cached success, and queued rapid retries must settle on the newest document.
            // 即使已成功缓存，显式重试仍须重新加载；快速排队重试必须最终稳定在最新文档。
            await Check("U05-explicit-and-rapid-retry", async () =>
            {
                await Builtin(); OpenReal(); await RealReady(); int before = navigationCount;
                await web.ExecuteScriptAsync("window.auditSentinel=79;"); Click("BtnPreviewRetry");
                await Until(() => Task.FromResult(navigationCount > before), "Explicit cached retry navigation"); await RealReady();
                Need(await Js<bool>("window.auditSentinel===undefined"), "Explicit retry retained the cached document");
                await Builtin(); before = navigationCount; OpenReal(); Click("BtnPreviewRetry"); Click("BtnPreviewRetry"); await RealReady();
                Need(Field<bool>("_realLoaded") && !ErrorVisible(), "Rapid retry never settled as a loaded preview");
                return new { rapidNavigationDelta = navigationCount - before, source = web.Source.ToString(), loaded = Field<bool>("_realLoaded") };
            });
            // Hold the actual document response while edits arrive, then require a single load with the latest CSS.
            // 在真实文档响应等待期间编辑，随后要求只加载一次并使用最新 CSS。
            await Check("U05-pending-navigation-coalesces-css", async () =>
            {
                await Builtin(); var held = new TaskCompletionSource<CoreWebView2Deferral>();
                // Delay only this test-owned document until the assertion releases its deferral.
                // 仅延迟测试自有文档，直到断言释放延迟句柄。
                EventHandler<CoreWebView2WebResourceRequestedEventArgs> hold = (_, e) => { e.Response = web.CoreWebView2.Environment.CreateWebResourceResponse(new MemoryStream(Array.Empty<byte>()), 200, "OK", "Content-Type: text/javascript"); held.TrySetResult(e.GetDeferral()); };
                File.WriteAllText(realFile, DeferredHtml);
                web.CoreWebView2.AddWebResourceRequestedFilter("https://knotjot-audit.invalid/hold.js", CoreWebView2WebResourceContext.Script); web.CoreWebView2.WebResourceRequested += hold;
                CoreWebView2Deferral? deferral = null;
                try
                {
                    int before = navigationCount; OpenReal(); deferral = await held.Task.WaitAsync(TimeSpan.FromSeconds(6));
                    bool earlyLoaded = Field<bool>("_realLoaded");
                    for (int i = 0; i < 3; i++) { ((TextBox)ui.FindName("TxtName")).Text = "pending-css-" + i; Call("RefreshPreview"); }
                    await Task.Delay(100); int pendingNavigations = navigationCount - before;
                    deferral.Complete(); deferral = null; await RealReady();
                    await Until(async () => await Js<bool>("document.getElementById('knotjot-skin').textContent.includes('pending-css-2')"), "Latest deferred CSS");
                    Need(!earlyLoaded, "Pending document marked loaded before successful completion"); Need(pendingNavigations == 1 && navigationCount == before + 1, "Pending CSS edits reloaded document");
                    return new { earlyLoaded, navigationDelta = navigationCount - before, latestCss = true };
                }
                finally { deferral?.Complete(); web.CoreWebView2.WebResourceRequested -= hold; web.CoreWebView2.RemoveWebResourceRequestedFilter("https://knotjot-audit.invalid/hold.js", CoreWebView2WebResourceContext.Script); File.WriteAllText(realFile, RealHtml); }
            });
            // A completion from an older CSS injection must not erase a newer preview error.
            // 旧 CSS 注入的完成不能清除更新的预览错误。
            await Check("U05-stale-injection-keeps-new-error", async () =>
            {
                await Builtin(); OpenReal(); await RealReady();
                var injection = (Task)Call("InjectSkinAsync", "body{color:#123456}")!;
                Call("ShowPreviewError", new InvalidOperationException("newer controlled preview failure"), "audit", realFile, "latest CSS");
                await injection; Need(ErrorVisible(), "Older injection completion hid the newer error");
                return new { errorVisible = ErrorVisible(), loaded = Field<bool>("_realLoaded") };
            });
            // Switching to built-in during a held navigation must ignore the obsolete real-page completion.
            // 在导航等待期间切回内置时，必须忽略过期真实页面的完成事件。
            await Check("U05-builtin-supersedes-pending-real", async () =>
            {
                await Builtin(); var held = new TaskCompletionSource<CoreWebView2Deferral>();
                // Hold the one pending document so the source switch overtakes it deterministically.
                // 延迟唯一等待中的文档，使来源切换确定先完成。
                EventHandler<CoreWebView2WebResourceRequestedEventArgs> hold = (_, e) => { e.Response = web.CoreWebView2.Environment.CreateWebResourceResponse(new MemoryStream(Array.Empty<byte>()), 200, "OK", "Content-Type: text/javascript"); held.TrySetResult(e.GetDeferral()); };
                File.WriteAllText(realFile, DeferredHtml);
                web.CoreWebView2.AddWebResourceRequestedFilter("https://knotjot-audit.invalid/hold.js", CoreWebView2WebResourceContext.Script); web.CoreWebView2.WebResourceRequested += hold;
                CoreWebView2Deferral? deferral = null;
                try
                {
                    OpenReal(); deferral = await held.Task.WaitAsync(TimeSpan.FromSeconds(6)); Click("BtnPreviewBuiltin"); deferral.Complete(); deferral = null;
                    await Until(async () => await Js<bool>("!!document.getElementById('preview-nodes')"), "Builtin superseded pending page"); await Task.Delay(150);
                    Need(!ErrorVisible() && Prefs.PreviewSource == 0 && !Field<bool>("_realLoaded"), "Obsolete real navigation changed built-in state");
                    return new { errorVisible = ErrorVisible(), source = web.Source.ToString(), loaded = Field<bool>("_realLoaded") };
                }
                finally { deferral?.Complete(); web.CoreWebView2.WebResourceRequested -= hold; web.CoreWebView2.RemoveWebResourceRequestedFilter("https://knotjot-audit.invalid/hold.js", CoreWebView2WebResourceContext.Script); File.WriteAllText(realFile, RealHtml); }
            });
            // Recover an ordinary failed real preview using the production built-in button and isolated persisted preference.
            // 使用生产内置按钮及隔离持久化偏好恢复普通失败的真实预览。
            await Check("U05-builtin-recovery-after-failure", async () =>
            {
                await Builtin(); await FailReal(); await Builtin();
                Need(await Js<int>("document.querySelectorAll('.card-inner[contenteditable=true]').length") == 5, "Built-in cards missing");
                Need(JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "isolated-prefs.json"))).RootElement.GetProperty("previewSource").GetInt32() == 0, "Builtin preference not persisted");
                await Screenshot("builtin-recovered"); return new { editableCards = 5, source = Prefs.PreviewSource, errorVisible = ErrorVisible() };
            });
        }
        finally { web.Dispose(); host.Close(); typeof(MainWindow).GetField("_dirty", Flags)!.SetValue(ui, false); ui.Close(); }
    }
}
