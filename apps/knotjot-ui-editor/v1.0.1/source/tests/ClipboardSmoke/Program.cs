using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using KnotJotUiEditor;

// Catch escaped clipboard failures and lost project/diagnostic state through the actual wired WPF button.
// 通过真实绑定的 WPF 按钮捕获未处理的剪贴板失败及工程、诊断状态丢失。
static class Program
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly List<object> Results = new();
    static MainWindow window = null!;
    static int failures;

    // Keep all files isolated, restore the pre-test clipboard in memory, and never display a desktop window.
    // 隔离全部文件，在内存中保留并恢复测试前剪贴板，且不显示桌面窗口。
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length != 1) { Console.Error.WriteLine("Usage: ClipboardSmoke <new-evidence-directory>"); return 2; }
        string output = Path.GetFullPath(args[0]);
        if (Directory.Exists(output)) { Console.Error.WriteLine("Use a new evidence directory."); return 2; }
        Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        DataObject? original = null;
        bool captured = false;
        try
        {
            Environment.SetEnvironmentVariable("KNOTJOT_UI_EDITOR_DATA_DIR", Path.Combine(output, "connection"));
            IsolatePath(typeof(Prefs), "PathFile", Path.Combine(output, "prefs.json"));
            IsolatePath(typeof(Prefs), "LegacyPathFile", Path.Combine(output, "legacy-prefs.json"));
            IsolatePath(typeof(MainWindow), "RecentPath", Path.Combine(output, "recent.json"));
            IsolatePath(typeof(MainWindow), "LegacyRecentPath", Path.Combine(output, "legacy-recent.json"));
            original = CaptureClipboard(); captured = true;
            window = new MainWindow();
            foreach (string name in new[] { "InitAdorners", "InitDesign", "WireEvents", "WireShapePanel", "WireInkImagePanels", "WireTextPanel" }) Call(name);
            Call("SwitchTarget", "card", true);
            typeof(MainWindow).GetField("_ready", Flags)!.SetValue(window, true);
            ((TextBox)window.FindName("TxtName")).Text = "Clipboard recovery fixture";
            Call("AddElementSilent", new ShapeElement { Kind = "rect", X = 555, Y = 305, W = 90, H = 50, Fill = "#123456" }, -1);
            Call("PushUndo", new object?[] { null });
            string before = (string)Call("HistoryJson")!;
            string skinId = Field<string>("_skinId");
            bool dirty = Field<bool>("_dirty");
            int undoCount = Field<List<string>>("_undoStack").Count;
            Need(dirty, "Fixture must contain unsaved edits.");

            // Require no clipboard access or success feedback until a diagnostic exists.
            // 在诊断存在前，要求不访问剪贴板或报告成功。
            Check("empty-diagnostic-leaves-clipboard-and-status", () =>
            {
                Clipboard.SetText("clipboard-smoke-sentinel");
                string status = Status();
                using (var held = new ClipboardLock()) ClickCopy();
                Need(Clipboard.GetText() == "clipboard-smoke-sentinel", "Empty diagnostic replaced clipboard content.");
                Need(Status() == status, "Empty diagnostic reported a copy result.");
                return new { unchanged = true };
            });

            Call("ShowPreviewError", new InvalidOperationException("Synthetic preview unavailable"), "clipboard regression", null, "body { color: #123456; }");
            string diagnostic = Field<string>("_previewDiagnostic");
            string previewMessage = ((TextBlock)window.FindName("TxtPreviewError")).Text;
            Need(!string.IsNullOrEmpty(diagnostic), "Production error path did not prepare a diagnostic.");

            // Require recoverable feedback and unchanged project, history and diagnostic during repeated contention.
            // 连续占用时，要求提供可恢复提示，并保留工程、历史与诊断。
            Check("locked-clipboard-keeps-project-and-offers-retry", () =>
            {
                using (var held = new ClipboardLock())
                {
                    // Prove the lock rejects the same WPF API before testing the application's boundary.
                    // 先证明占用拒绝同一 WPF API，再验证应用自身边界。
                    bool rejected = false;
                    try { Clipboard.SetText("clipboard-lock-probe"); }
                    catch (ExternalException ex) when (ex.HResult == unchecked((int)0x800401D0)) { rejected = true; }
                    Need(rejected, "Native clipboard fixture did not produce CLIPBRD_E_CANT_OPEN.");
                    ClickCopy();
                    Need(Status().Contains("剪贴板") && (Status().Contains("重试") || Status().Contains("再试")), "Clipboard failure did not provide retry guidance.");
                    Need(Field<string>("_previewDiagnostic") == diagnostic, "Copy failure discarded the diagnostic.");
                    Need(((TextBlock)window.FindName("TxtPreviewError")).Text == previewMessage, "Copy failure replaced the original preview error.");
                    Need(((FrameworkElement)window.FindName("PreviewErrorPanel")).Visibility == Visibility.Visible, "Copy failure hid the diagnostic controls.");
                    Need((string)Call("HistoryJson")! == before && Field<string>("_skinId") == skinId && Field<bool>("_dirty") == dirty && Field<List<string>>("_undoStack").Count == undoCount, "Copy failure changed project or undo state.");
                    ClickCopy();
                }
                return new { lockHResult = "0x800401D0", status = Status(), projectPreserved = true, diagnosticPreserved = true };
            });

            // Require exact copying, visible confirmation and functional editing after the owner releases its lock.
            // 所有者释放占用后，要求准确复制、可见确认及继续编辑。
            Check("released-clipboard-copies-exact-diagnostic-and-confirms", () =>
            {
                ClickCopy();
                Need(Clipboard.GetText() == diagnostic, "Recovery did not copy the prepared diagnostic exactly.");
                Need(Status().Contains("已复制") && Status().Contains("诊断"), "Successful copy did not confirm completion.");
                Need(Field<string>("_previewDiagnostic") == diagnostic && (string)Call("HistoryJson")! == before, "Successful copy changed diagnostic or project.");
                typeof(MainWindow).GetField("_dirty", Flags)!.SetValue(window, false);
                ((TextBox)window.FindName("TxtName")).Text = "Editing continues after clipboard recovery";
                Need(Field<bool>("_dirty") && window.Title.EndsWith(" ●"), "Editor no longer marks new edits as unsaved after recovery.");
                return new { characters = diagnostic.Length, status = Status(), editingContinues = true };
            });
        }
        catch (Exception ex) { Record("setup", false, ex.ToString()); }
        finally
        {
            if (captured)
            {
                try
                {
                    if (original == null || original.GetFormats(false).Length == 0) Clipboard.Clear();
                    else Clipboard.SetDataObject(original, true);
                    Record("restore-pretest-clipboard", true, new { restored = true });
                }
                catch (Exception ex) { Record("restore-pretest-clipboard", false, ex.ToString()); }
            }
            if (window != null) typeof(MainWindow).GetField("_dirty", Flags)!.SetValue(window, false);
            app.Shutdown();
        }
        string assembly = typeof(MainWindow).Assembly.Location;
        File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { date = DateTimeOffset.Now, assembly, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))), failures, results = Results }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("CLIPBOARD_SMOKE failures=" + failures);
        return failures == 0 ? 0 : 1;
    }

    // Materialize clipboard formats before replacing them; keep private content in memory and out of evidence.
    // 替换前物化剪贴板格式，私人内容只保留在内存而不写入证据。
    static DataObject? CaptureClipboard()
    {
        var current = Clipboard.GetDataObject();
        if (current == null) return null;
        var snapshot = new DataObject();
        foreach (string format in current.GetFormats(false))
        {
            object value = current.GetData(format, false) ?? throw new InvalidOperationException("Cannot safely snapshot a clipboard format.");
            if (value is MemoryStream stream) value = new MemoryStream(stream.ToArray());
            snapshot.SetData(format, value, false);
        }
        return snapshot;
    }

    // Redirect immutable production paths within this test process before any persistence can occur.
    // 在任何持久化之前，仅在测试进程重定向不可变的生产路径。
    static void IsolatePath(Type type, string name, string destination)
    {
        RuntimeHelpers.RunClassConstructor(type.TypeHandle);
        var field = type.GetField(name, Flags)!;
        var setter = new DynamicMethod("SetClipboardSmokePath", typeof(void), new[] { typeof(string) }, typeof(Program), true);
        var il = setter.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Stsfld, field); il.Emit(OpCodes.Ret);
        setter.CreateDelegate<Action<string>>()(destination);
        Need((string)field.GetValue(null)! == destination, "Profile isolation failed.");
    }

    // Invoke existing production setup without adding test-only production methods.
    // 调用既有生产初始化，不增加生产测试专用方法。
    static object? Call(string name, params object?[] args)
    {
        try { return typeof(MainWindow).GetMethods(Flags).First(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(window, args); }
        catch (TargetInvocationException ex) { throw ex.InnerException!; }
    }

    // Inspect existing state to detect data loss after the user command.
    // 检查已有状态，检测用户命令之后的数据丢失。
    static T Field<T>(string name) => (T)typeof(MainWindow).GetField(name, Flags)!.GetValue(window)!;
    // Raise the actual wired copy event without desktop automation.
    // 不使用桌面自动化，触发实际绑定的复制事件。
    static void ClickCopy() => ((Button)window.FindName("BtnPreviewCopy")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    // Read the visible status control that provides feedback to the user.
    // 读取向用户提供反馈的可见状态控件。
    static string Status() => ((TextBlock)window.FindName("TxtStatus")).Text;
    // Fail only an observed application or fixture invariant.
    // 仅对已观察到的应用或样本约束报告失败。
    static void Need(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    // Retain independent results even when the pre-fix handler escapes with an exception.
    // 即使修复前处理函数抛出异常，也保留各项独立结果。
    static void Check(string name, Func<object> action) { try { Record(name, true, action()); } catch (Exception ex) { Record(name, false, ex.ToString()); } }
    // Write only synthetic fixture evidence and a failing exit-code count.
    // 仅记录合成样本证据及用于失败退出码的计数。
    static void Record(string name, bool passed, object evidence) { if (!passed) failures++; Results.Add(new { name, passed, evidence }); Console.WriteLine((passed ? "PASS " : "FAIL ") + name + " " + JsonSerializer.Serialize(evidence)); }

    // Hold the real system clipboard on another thread using a real message-only HWND, with bounded cleanup.
    // 使用真实消息专用 HWND 在另一线程占用系统剪贴板，并有界清理。
    sealed class ClipboardLock : IDisposable
    {
        readonly ManualResetEventSlim ready = new();
        readonly ManualResetEventSlim release = new();
        readonly Thread owner;
        Exception? failure;

        // Wait until exclusive ownership is established before allowing the production command to run.
        // 等待建立独占后，才允许生产命令运行。
        public ClipboardLock()
        {
            owner = new Thread(Hold) { IsBackground = true };
            owner.SetApartmentState(ApartmentState.STA); owner.Start();
            if (!ready.Wait(TimeSpan.FromSeconds(5))) { Dispose(); throw new TimeoutException("Clipboard lock setup timed out."); }
            if (failure != null) { Dispose(); throw failure; }
        }

        // Acquire and release the clipboard and HWND on the same owner thread even if assertions fail.
        // 即使断言失败，也在同一所有者线程获取与释放剪贴板及 HWND。
        void Hold()
        {
            IntPtr hwnd = IntPtr.Zero;
            bool opened = false;
            try
            {
                hwnd = CreateWindowExW(0, "STATIC", "ClipboardSmoke", 0, 0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (hwnd == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                opened = OpenClipboard(hwnd);
                if (!opened) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                ready.Set(); release.Wait(TimeSpan.FromSeconds(15));
            }
            catch (Exception ex) { failure = ex; ready.Set(); }
            finally { if (opened) CloseClipboard(); if (hwnd != IntPtr.Zero) DestroyWindow(hwnd); }
        }

        // End ownership synchronously so recovery assertions run only after the native lock is gone.
        // 同步结束占用，使恢复断言仅在原生锁释放后运行。
        public void Dispose()
        {
            release.Set();
            if (!owner.Join(TimeSpan.FromSeconds(5))) throw new TimeoutException("Clipboard lock cleanup timed out.");
            ready.Dispose(); release.Dispose();
        }

        // Create a non-visible owner handle; a null HWND does not reliably exclude the calling process.
        // 创建不可见所有者句柄；空 HWND 不能可靠排除调用进程。
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr CreateWindowExW(uint styleEx, string className, string name, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
        // Request exclusive clipboard ownership for the fixture HWND.
        // 为样本 HWND 请求独占剪贴板。
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)] static extern bool OpenClipboard(IntPtr hwnd);
        // Release the clipboard from its owning thread.
        // 从所有者线程释放剪贴板。
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)] static extern bool CloseClipboard();
        // Destroy only the message-only HWND created by this fixture.
        // 仅销毁本样本创建的消息专用 HWND。
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)] static extern bool DestroyWindow(IntPtr hwnd);
    }
}
