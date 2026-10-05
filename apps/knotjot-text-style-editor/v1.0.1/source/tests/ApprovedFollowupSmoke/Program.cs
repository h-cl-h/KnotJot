using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KnotJotTextStyleEditor;

internal static class Program
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly List<object> results = [];
    static string output = "";
    static int failures;
    // EN: Call the existing production command with matching arity, without injecting unrelated native input. ZH: 按参数数量调用现有生产命令，不注入无关原生输入。
    static object? Call(object window, string name, params object[] args) => window.GetType().GetMethods(Flags).First(m => m.Name == name && m.GetParameters().Length == args.Length && m.GetParameters().Zip(args).All(p => p.First.ParameterType.IsInstanceOfType(p.Second))).Invoke(window, args);
    // EN: Read and seed bounded document/gesture state for deterministic hidden-window reproductions. ZH: 读取或设置有限文档及手势状态，用于确定的隐藏窗口复现。
    static T Get<T>(object window, string name) => (T)window.GetType().GetField(name, Flags)!.GetValue(window)!;
    static void Set(object window, string name, object? value) => window.GetType().GetField(name, Flags)!.SetValue(window, value);
    static TextBox Box(MainWindow window, string name) => (TextBox)window.FindName(name);
    static TextBoxStyle Style(MainWindow window) => Get<TextBoxStyle>(window, "style");
    // EN: Await the current asynchronous frame with bounded dispatcher pumping, keeping the original smoke suite independent of native desktop input.
    // ZH: 在期限内驱动分派器等待当前异步外框，使原 smoke 套件继续不依赖原生桌面输入。
    static void AwaitFrame(MainWindow window)
    {
        var task = Get<Task>(window, "framePreviewTask"); var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            if (watch.Elapsed.TotalSeconds > 30) throw new TimeoutException("CSS frame preview timed out.");
            var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }
    // EN: Fail one case precisely while allowing the remaining independent defects to be reproduced. ZH: 精确记录单项失败，同时继续复现其他独立缺陷。
    static void Expect(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Check(string id, Action action)
    {
        try { action(); results.Add(new { id, pass = true }); Console.WriteLine("PASS " + id); }
        catch (Exception error) { failures++; results.Add(new { id, pass = false, error = error.ToString() }); Console.WriteLine("FAIL " + id + " " + error.GetBaseException().Message); }
    }
    // EN: Build fresh hidden editor state, replacing both controls and history before each case. ZH: 每项用例创建新的隐藏编辑器状态，同时刷新控件和历史。
    static MainWindow Window(params StyleLayer[] layers)
    {
        var window = new MainWindow();
        Style(window).Layers.AddRange(layers);
        Style(window).TextRegion = new TextRegion { X = 10, Y = 10, W = 80, H = 80 };
        Call(window, "LoadControls");
        Call(window, "RefreshLayerList", layers.Length > 0 ? 0 : -1);
        Call(window, "ResetHistory");
        return window;
    }
    // EN: Supply a small rectangle with stable geometry and optional lock. ZH: 提供几何稳定的小矩形，并允许设置锁定。
    static StyleLayer Rect(bool locked = false) => new() { X = 10, Y = 10, W = 20, H = 20, IsLocked = locked };
    // EN: Render actual WPF pixels to an artifact and sample normalized positions in premultiplied BGRA space. ZH: 渲染实际 WPF 像素并保存图片，在预乘 BGRA 空间按归一位置采样。
    static byte[] Pixels(FrameworkElement view, string file, double x = .5, double y = .5)
    {
        var width = Math.Max(1, (int)Math.Ceiling(view.Width)); var height = Math.Max(1, (int)Math.Ceiling(view.Height));
        view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(output, file + ".png"))) encoder.Save(stream);
        var pixel = new byte[4]; bitmap.CopyPixels(new Int32Rect(Math.Clamp((int)(width * x), 0, width - 1), Math.Clamp((int)(height * y), 0, height - 1), 1, 1), pixel, 4, 0);
        return pixel;
    }
    // EN: Verify actual design and independent-preview blend pixels using independently calculated opaque reference colors. ZH: 用独立计算的不透明参考颜色验证设计画布及独立预览的实际混合像素。
    static void Blend(string mode, byte r, byte g, byte b)
    {
        var window = Window(new() { X = 0, Y = 0, W = 100, H = 100, Fill = "#804020", StrokeWidth = 0 }, new() { X = 0, Y = 0, W = 100, H = 100, Fill = "#4080E0", StrokeWidth = 0, BlendMode = mode });
        Set(window, "selected", -1); Get<HashSet<int>>(window, "selectedLayers").Clear(); Style(window).TextRegion.IsVisible = false;
        Call(window, "RebuildCanvas"); Call(window, "RefreshPreview");
        foreach (var name in new[] { "DesignCanvas", "PreviewLayers" }) {
            var pixel = Pixels((FrameworkElement)window.FindName(name), mode + "-" + name);
            Expect(Math.Abs(pixel[2] - r) <= 2 && Math.Abs(pixel[1] - g) <= 2 && Math.Abs(pixel[0] - b) <= 2, name + " " + mode + " pixel=" + string.Join(',', pixel));
        }
        window.Close();
    }
    // EN: Check mask opacity using a real WPF preview, keeping framing and text outside the sampled layer surface. ZH: 用真实 WPF 预览检查蒙版透明度，采样图层表面不含外框和文字。
    static void Mask(string mode, string fill, double opacity, byte alpha)
    {
        var window = Window(new() { X = 0, Y = 0, W = 100, H = 100, Fill = "#C86432", StrokeWidth = 0 }, new() { X = 0, Y = 0, W = 100, H = 100, Fill = fill, StrokeWidth = 0, MaskMode = mode, Opacity = opacity });
        var pixel = Pixels((FrameworkElement)window.FindName("PreviewLayers"), mode + "-" + fill.Trim('#') + "-" + opacity);
        Expect(Math.Abs(pixel[3] - alpha) <= 2, "Expected alpha " + alpha + "; pixel=" + string.Join(',', pixel)); window.Close();
    }
    // EN: Generate a PNG with opaque white and half-alpha black halves, then sample a rotated image mask in common canvas coordinates.
    // ZH: 生成左右分别为不透明白色及半透明黑色的 PNG，在统一画布坐标中采样旋转图片蒙版。
    static void ImageMask(string mode, double opacity, byte topAlpha, byte bottomAlpha, byte outsideAlpha)
    {
        var bytes = new byte[16 * 16 * 4];
        for (var y = 0; y < 16; y++) for (var x = 0; x < 16; x++) { var p = (y * 16 + x) * 4; bytes[p] = bytes[p + 1] = bytes[p + 2] = (byte)(x < 8 ? 255 : 0); bytes[p + 3] = (byte)(x < 8 ? 255 : 128); }
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(16, 16, 96, 96, PixelFormats.Bgra32, null, bytes, 64)));
        using var data = new MemoryStream(); encoder.Save(data);
        File.WriteAllBytes(Path.Combine(output, "image-mask-fixture.png"), data.ToArray());
        var window = Window(new() { X = 0, Y = 0, W = 100, H = 100, Fill = "#C86432", StrokeWidth = 0, Rotation = 180 }, new() { Type = "image", X = 25, Y = 25, W = 50, H = 50, ImageData = "data:image/png;base64," + Convert.ToBase64String(data.ToArray()), MaskMode = mode, Rotation = 90, Opacity = opacity });
        var view = (FrameworkElement)window.FindName("PreviewLayers");
        foreach (var sample in new[] { ("top", .5, .3, topAlpha), ("bottom", .5, .7, bottomAlpha), ("outside", .05, .05, outsideAlpha) }) {
            var pixel = Pixels(view, mode + "-image-" + sample.Item1, sample.Item2, sample.Item3);
            Expect(Math.Abs(pixel[3] - sample.Item4) <= 3, mode + " " + sample.Item1 + " alpha=" + pixel[3]);
        }
        window.Close();
    }
    // EN: Open the second entry of an isolated two-style library using the same object association as OpenStyle; connect its source target back to that exact file.
    // ZH: 按 OpenStyle 的对象关联方式打开隔离双样式库的第二项，并将源码目标连接到同一文件。
    static (MainWindow window, string file) SameFileWindow(string folder)
    {
        var root = Path.Combine(output, folder); Directory.CreateDirectory(root);
        var target = Path.Combine(root, "main.js"); File.WriteAllText(target, "// isolated source connection");
        var file = StyleLibraryLocation.ForSource(target);
        var sibling = new TextBoxStyle { Id = "untouched-sibling", Name = "Sibling artwork", Layers = [new StyleLayer { X = 23, Y = 17, W = 11, H = 31, Fill = "#ABCDEF" }] };
        var owned = new TextBoxStyle { Id = "owned-style", Name = "Owned entry", TextRegion = new TextRegion { X = 10, Y = 10, W = 80, H = 80 } };
        StyleLibraryStore.Write(file, new StyleLibrary { Styles = [sibling, owned] });
        var loaded = StyleLibraryStore.Load(file); var window = new MainWindow();
        Set(window, "style", loaded.Library.Styles[1]); Set(window, "currentLibrary", loaded.Library); Set(window, "currentLibraryIndex", 1); Set(window, "currentFile", file);
        Set(window, "lastSavedSnapshot", (string)Call(window, "Snapshot")!); Set(window, "connectedTarget", target); Set(window, "connectedLibraryPath", file);
        Call(window, "LoadControls"); Call(window, "ResetHistory");
        return (window, file);
    }
    // EN: Exercise approved lock, history, geometry, persistence and compositing regressions in an isolated profile. ZH: 在隔离配置中验证已批准的锁定、历史、几何、持久化及合成回归。
    [STAThread]
    static int Main(string[] args)
    {
        output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("KNOTJOT_TEXT_STYLE_EDITOR_DATA_DIR", Path.Combine(output, "profile"));
        // EN: Keep the test application alive after each hidden window closes, including dispatcher turns during the real EXE handshake.
        // ZH: 各隐藏窗口关闭后保持测试应用存活，包含真实 EXE 握手期间执行的分派循环。
        _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Check("T01 locked layer delete", () => { var w = Window(Rect(true)); Call(w, "DeleteSelected"); Expect(Style(w).Layers.Count == 1, "Locked layer was deleted"); w.Close(); });
        Check("T01 locked layer order", () => { var a = Rect(true); var w = Window(a, Rect(), Rect()); Call(w, "MoveLayer", 1); Expect(Style(w).Layers[0] == a, "Locked layer moved"); Call(w, "MoveLayerExtreme", true); Expect(Style(w).Layers[0] == a, "Locked layer moved to top"); w.Close(); });
        Check("T01 locked properties and effect", () => { var a = Rect(true); var w = Window(Rect(), a); Call(w, "RefreshLayerList", 1); Box(w, "WBox").Text = "60"; Call(w, "ApplyLayerFields"); Call(w, "SetLayerEffect", 1, "subtract", "none"); Expect(a.W == 20 && a.ClipMode == "none", "Locked fields or effects changed"); Expect(!Box(w, "WBox").IsEnabled, "Locked geometry field enabled"); a.IsLocked = false; Call(w, "LayerLockChanged", w, new RoutedEventArgs()); Expect(Box(w, "WBox").IsEnabled, "Unlock did not enable fields"); w.Close(); });
        Check("T01 locked text delete", () => { var w = Window(); Style(w).TextRegion.IsLocked = true; Set(w, "selectedText", true); Call(w, "DeleteSelected"); Expect(Style(w).TextRegion.W == 80, "Locked text region deleted"); w.Close(); });
        Check("T02 live style undo redo coalescing", () => { var w = Window(); Style(w).Layers.Add(Rect()); Call(w, "CommitHistory"); for (var i = 0; i < 30; i++) Box(w, "BgBox").Text = "#" + (0x123400 + i).ToString("X6"); Call(w, "Undo"); Expect(Style(w).Layers.Count == 1 && Style(w).Bg != "#12341D", "Undo discarded earlier shape instead of live color"); Call(w, "Redo"); Expect(Style(w).Bg == "#12341D", "Redo lost live color"); Expect(Get<List<string>>(w, "history").Count <= 3, "Continuous edits were not coalesced"); w.Close(); });
        Check("T02 live text region geometry", () => { var w = Window(); Set(w, "selectedText", true); Call(w, "LoadLayerFields"); Box(w, "XBox").Text = "15"; Expect(Style(w).TextRegion.X == 15, "Text region did not update live"); Call(w, "Undo"); Expect(Style(w).TextRegion.X == 10, "Text region edit not undoable"); w.Close(); });
        Check("T03 explicit reverse line group drag", () => { var a = new StyleLayer { Type = "line", X = 10, Y = 10, W = 20, H = 20, X1 = 30, Y1 = 10, X2 = 10, Y2 = 30 }; var w = Window(a, Rect()); foreach (var n in new[] { "SquareSnapBox", "ObjectSnapBox", "GridSnapBox" }) ((CheckBox)w.FindName(n)).IsChecked = false; var e = new MouseEventArgs(Mouse.PrimaryDevice, 0); var p = e.GetPosition((Canvas)w.FindName("DesignCanvas")); Set(w, "down", new Point(p.X - 90, p.Y - 52)); Set(w, "dragging", true); Set(w, "dragLayerStartX", 10d); Set(w, "dragLayerStartY", 10d); var starts = Get<Dictionary<int, Point>>(w, "groupDragStarts"); starts.Add(0, new(10, 10)); starts.Add(1, new(10, 10)); Call(w, "CanvasMove", w, e); Expect(a.X == 20 && a.X1 == 40 && a.X2 == 20 && a.Y1 == 20 && Style(w).Layers[1].X == 20, "Line bounds/endpoints did not move together"); w.Close(); });
        Check("T04 source resources resolver", () => { var w = Window(); var dir = Path.Combine(output, "source"); Directory.CreateDirectory(Path.Combine(dir, "resources", "text-styles")); File.WriteAllText(Path.Combine(dir, "main.js"), "// source fixture"); var expected = Path.Combine(dir, "resources", "text-styles", "custom-text-styles.json"); Expect((string)Call(w, "ResolveConnectedLibrary", Path.Combine(dir, "main.js"))! == expected, "Wrong refactored library location"); w.Close(); });
        Check("T05 sync saves associated standalone bytes", () => { var w = Window(Rect()); var standalone = Path.Combine(output, "standalone.knotjot-textstyle"); Set(w, "currentFile", standalone); Expect((bool)Call(w, "SaveStandalone", false)!, "Initial save failed"); var old = File.ReadAllText(standalone); Box(w, "NameBox").Text = "Updated standalone"; var target = Path.Combine(output, "fixture-main.js"); File.WriteAllText(target, "// fixture"); Set(w, "connectedTarget", target); Set(w, "connectedLibraryPath", Path.Combine(output, "sync.json")); Call(w, "Sync", false); Expect(old != File.ReadAllText(standalone), "Save and sync did not change associated file bytes"); Expect(Get<string>(w, "lastSavedSnapshot") == (string)Call(w, "Snapshot")!, "Successful save baseline was not advanced"); w.Close(); });
        Check("T06 frame shadow preview", () => { var w = Window(); ((TabControl)w.FindName("WorkTabs")).SelectedIndex = 1; Box(w, "ShadowBox").Text = "8px 12px 4px 3px #00000080"; Call(w, "RefreshPreview"); AwaitFrame(w); Expect(((Canvas)w.FindName("PreviewShadow")).Children.Count == 1, "No preview shadow rendering"); w.Close(); });
        Check("T08 null layer structured backup", () => { var file = Path.Combine(output, "null-layer.json"); const string original = "{\"format\":\"knotjot-text-styles\",\"version\":1,\"styles\":[{\"id\":\"broken\",\"layers\":[null]}]}"; File.WriteAllText(file, original); try { StyleLibraryStore.Load(file, true); throw new Exception("Null layer accepted"); } catch (StyleLibraryException error) { Expect(error.InnerException is InvalidDataException && error.BackupFile != null && File.Exists(error.BackupFile), "Missing structured corruption backup"); Expect(File.ReadAllText(file) == original && File.ReadAllText(error.BackupFile!) == original, "Corrupt original bytes changed"); } });
        Check("T09 normal pixels", () => Blend("normal", 64, 128, 224));
        Check("T09 multiply pixels", () => Blend("multiply", 32, 32, 28));
        Check("T09 screen pixels", () => Blend("screen", 160, 160, 228));
        Check("T09 overlay pixels", () => Blend("overlay", 65, 64, 56));
        Check("T10 black luminance", () => Mask("luminance", "#000000", 1, 0));
        Check("T10 white luminance opacity", () => Mask("luminance", "#FFFFFF", .5, 128));
        Check("T10 inverse white luminance", () => Mask("inverseLuminance", "#FFFFFF", 1, 0));
        Check("T10 alpha opacity", () => Mask("alpha", "#000000", .5, 128));
        Check("T10 inverse alpha opacity", () => Mask("inverseAlpha", "#FFFFFF", .5, 128));
        Check("O03 underscore ID compatibility", () => { var w = Window(); Box(w, "IdBox").Text = "style_one-2"; Call(w, "ReadControls"); Expect(Style(w).Id == "style_one-2", "Underscore was rejected"); w.Close(); });
        Check("T01 locked text group drag and resize", () => { var w = Window(Rect()); var region = Style(w).TextRegion; region.IsLocked = true; Set(w, "selectedText", true); Set(w, "dragging", true); Set(w, "down", new Point(-90, -52)); Get<Dictionary<int, Point>>(w, "groupDragStarts").Add(0, new(10, 10)); Call(w, "CanvasMove", w, new MouseEventArgs(Mouse.PrimaryDevice, 0)); Expect(region.X == 10 && Style(w).Layers[0].X == 10, "Locked selected text moved with group"); Set(w, "resizing", true); Set(w, "resizeTargetText", true); Set(w, "resizeW", 80d); Set(w, "resizeH", 80d); Set(w, "resizeHandle", 3); Call(w, "ResizeSelected", new Point(50, 0)); Expect(region.W == 80, "Locked region resized"); w.Close(); });
        Check("T02 rules layer edits save flush and dirty restoration", () => { var w = Window(Rect()); var baseline = (string)Call(w, "Snapshot")!; Set(w, "lastSavedSnapshot", baseline); Set(w, "lastSyncedSnapshot", baseline); Box(w, "LayerFillBox").Text = "#123456"; Call(w, "Undo"); Expect(Style(w).Layers[0].Fill != "#123456" && (string)Call(w, "Snapshot")! == baseline, "Layer undo did not restore persistence baselines"); Call(w, "Redo"); Box(w, "MaxLengthBox").Text = "17"; Call(w, "Undo"); Expect(Style(w).TextRules.MaxLength != 17 && Style(w).Layers[0].Fill == "#123456", "Rule edit merged with completed redo"); Call(w, "Redo"); Call(w, "SaveStandaloneTo", Path.Combine(output, "pending-save.json")); Expect(!Get<bool>(w, "pendingLiveHistory") && Get<string>(w, "lastSavedSnapshot") == (string)Call(w, "Snapshot")!, "Save did not flush pending edit"); w.Close(); });
        Check("T03 line persistence retains direction", () => { var line = new StyleLayer { Type = "line", X1 = 40, Y1 = 20, X2 = 20, Y2 = 40 }; var file = Path.Combine(output, "line.json"); StyleLibraryStore.Write(file, new StyleLibrary { Styles = [new TextBoxStyle { Layers = [line] }] }); var reopened = StyleLibraryStore.Load(file).Library.Styles[0].Layers[0]; Expect(reopened.X == 20 && reopened.X1 == 40 && reopened.X2 == 20 && reopened.Y1 == 20 && reopened.Y2 == 40, "Save/reopen changed line direction"); });
        Check("T04 legacy source and restore path", () => { var dir = Path.Combine(output, "legacy"); Directory.CreateDirectory(Path.Combine(dir, "text-styles")); var target = Path.Combine(dir, "main.js"); File.WriteAllText(target, "// legacy fixture"); Expect(StyleLibraryLocation.ForSource(target) == Path.Combine(dir, "text-styles", "custom-text-styles.json"), "Legacy sibling resolver lost"); var modern = Path.Combine(output, "source", "main.js"); File.WriteAllText(Path.Combine(output, "profile", "connection.json"), JsonSerializer.Serialize(new { target = modern, library = Path.Combine(output, "wrong.json") })); var w = new MainWindow(); Expect(Get<string>(w, "connectedLibraryPath") == StyleLibraryLocation.ForSource(modern), "Restore retained wrong cached source path"); w.Close(); });
        Check("T05 failed standalone then failed sync preserve independent baselines", () => { var w = Window(Rect()); var project = Path.Combine(output, "failure-project.json"); Call(w, "SaveStandaloneTo", project); var saved = Get<string>(w, "lastSavedSnapshot"); Box(w, "NameBox").Text = "Pending write"; var foreign = StyleLibraryStore.Load(project); StyleLibraryStore.Write(project, foreign.Library, foreign.Revision); try { Call(w, "SaveStandaloneTo", project); throw new Exception("Expected revision conflict"); } catch (TargetInvocationException e) { Expect(e.InnerException is StyleLibraryException, "Wrong revision conflict exception"); } Expect(Get<string>(w, "lastSavedSnapshot") == saved, "Failed save cleared dirty baseline"); Set(w, "currentLibrary", StyleLibraryStore.Load(project).Library); var syncFile = Path.Combine(output, "failed-sync.json"); var stale = StyleLibraryStore.Load(syncFile); StyleLibraryStore.Write(syncFile, new StyleLibrary()); try { Call(w, "SyncLoadedLibrary", syncFile, stale); throw new Exception("Expected target conflict"); } catch (TargetInvocationException e) { Expect(e.InnerException is StyleLibraryException, "Wrong sync conflict exception"); } Expect(Get<string>(w, "lastSavedSnapshot") == (string)Call(w, "Snapshot")!, "Successful standalone half was not marked saved"); Expect(Get<string?>(w, "lastSyncedSnapshot") == null, "Failed sync was marked synchronized"); Expect(StyleLibraryStore.Load(project).Library.Styles[0].Name == "Pending write", "Successful standalone bytes not updated"); w.Close(); });
        Check("T05 unsaved standalone sync retains dirty state", () => { var w = Window(Rect()); var saved = Get<string>(w, "lastSavedSnapshot"); Box(w, "NameBox").Text = "Unsaved but synced"; var file = Path.Combine(output, "unsaved-sync.json"); Call(w, "SyncLoadedLibrary", file, StyleLibraryStore.Load(file)); Expect(Get<string?>(w, "currentFile") == null && Get<string>(w, "lastSavedSnapshot") == saved && Get<string>(w, "lastSavedSnapshot") != (string)Call(w, "Snapshot")!, "Sync falsely marked unsaved project clean"); Expect(Get<string>(w, "lastSyncedSnapshot") == (string)Call(w, "Snapshot")!, "Successful sync baseline missing"); w.Close(); });
        // EN: Correct the original alpha assertion: CSS outer shadows exclude the card interior even when its background is transparent.
        // ZH: 修正原透明度断言：即使卡片底色透明，CSS 外阴影也必须排除卡片内部。
        Check("T06 shadow pixels spread and CSS alpha", () => {
            var w = Window(); ((TabControl)w.FindName("WorkTabs")).SelectedIndex = 1; Box(w, "BgBox").Text = "transparent"; Box(w, "BorderBox").Text = "transparent"; Box(w, "ShadowBox").Text = "8px 12px 4px 3px #00000080"; AwaitFrame(w);
            var surface = (Canvas)w.FindName("PreviewShadow"); var center = Pixels(surface, "frame-shadow-interior"); Expect(center[3] == 0, "Outer shadow leaked inside the transparent card.");
            var image = (Image)surface.Children[0]; var bitmap = (BitmapSource)image.Source; var pixel = new byte[4]; var x = (int)(surface.Width / 2 - Canvas.GetLeft(image)); var y = (int)(surface.Height + 10 - Canvas.GetTop(image)); bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
            Expect(pixel[3] > 80 && pixel[3] <= 128, "Outside blur/spread/offset lost CSS alpha."); Box(w, "ShadowBox").Text = "none"; AwaitFrame(w); Expect(Pixels(surface, "frame-none")[3] == 0, "None did not remove shadow pixels."); w.Close();
        });
        Check("T07 extreme shortcut dispatcher and locks", () => { var a = Rect(); var w = Window(a, Rect(), Rect(), Rect()); Expect((bool)Call(w, "HandleLayerOrderShortcut", Key.OemCloseBrackets, ModifierKeys.Control | ModifierKeys.Shift)!, "Top shortcut not handled"); Expect(Style(w).Layers[^1] == a, "Top shortcut moved only one layer"); Call(w, "HandleLayerOrderShortcut", Key.OemOpenBrackets, ModifierKeys.Control | ModifierKeys.Shift); Expect(Style(w).Layers[0] == a, "Bottom shortcut moved only one layer"); a.IsLocked = true; Call(w, "HandleLayerOrderShortcut", Key.OemCloseBrackets, ModifierKeys.Control | ModifierKeys.Shift); Expect(Style(w).Layers[0] == a, "Shortcut bypassed lock"); w.Close(); });
        Check("T09 translucent multiply pixels", () => { var w = Window(new() { X = 0, Y = 0, W = 100, H = 100, Fill = "#804020", StrokeWidth = 0, Opacity = .5 }, new() { X = 0, Y = 0, W = 100, H = 100, Fill = "#4080E0", StrokeWidth = 0, Opacity = .5, BlendMode = "multiply" }); var p = Pixels((Canvas)w.FindName("PreviewLayers"), "multiply-alpha"); Expect(Math.Abs(p[0] - 71) <= 2 && Math.Abs(p[1] - 56) <= 2 && Math.Abs(p[2] - 56) <= 2 && Math.Abs(p[3] - 192) <= 2, "Alpha blend pixels=" + string.Join(',', p)); w.Close(); });
        Check("T10 rotated image alpha", () => ImageMask("alpha", .5, 128, 64, 0));
        Check("T10 rotated image inverse alpha", () => ImageMask("inverseAlpha", .5, 127, 191, 255));
        Check("T10 rotated image luminance", () => ImageMask("luminance", .5, 128, 0, 0));
        Check("T10 rotated image inverse luminance", () => ImageMask("inverseLuminance", .5, 127, 255, 255));
        Check("T11 typed versioned reply validation", () => { var file = Path.GetFullPath(Path.Combine(output, "protocol.json")); Expect(StyleLibraryLocation.ParseReply(JsonSerializer.Serialize(new { format = "knotjot-text-styles-location", version = 1, file })) == file, "Numeric version rejected"); foreach (var reply in new[] { new { format = "wrong", version = 1, file }, new { format = "knotjot-text-styles-location", version = 2, file }, new { format = "knotjot-text-styles-location", version = 1, file = "relative.json" } }) { try { StyleLibraryLocation.ParseReply(JsonSerializer.Serialize(reply)); throw new Exception("Invalid response accepted"); } catch (InvalidDataException) { } } });
        if (args.Length > 1) Check("T11 actual main EXE and shortcut", () => { var exe = Path.GetFullPath(args[1]); var w = Window(); var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KnotJot", "text-styles", "custom-text-styles.json"); var before = Directory.GetFiles(Path.GetTempPath(), "knotjot-text-styles-location-*.json").ToHashSet(); var actual = (string)Call(w, "ResolveConnectedLibrary", exe)!; Expect(actual == expected, "Main EXE returned unexpected library path"); var link = Path.Combine(output, "connection-fixture.lnk"); dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!; dynamic shortcut = shell.CreateShortcut(link); shortcut.TargetPath = exe; shortcut.Save(); var resolved = (string)Call(w, "ResolveTarget", link)!; Expect(resolved == exe && (string)Call(w, "ResolveConnectedLibrary", resolved)! == expected, "Shortcut handshake failed"); Expect(!Directory.GetFiles(Path.GetTempPath(), "knotjot-text-styles-location-*.json").Except(before).Any(), "Response temporary file was not cleaned"); File.WriteAllText(Path.Combine(output, "actual-main-connection.json"), JsonSerializer.Serialize(new { exe, file = actual, numericVersion = 1, shortcut = true, cleaned = true })); w.Close(); });
        Check("T05 actual denied replacement preserves bytes revision and dirty", () => { var w = Window(Rect()); var file = Path.Combine(output, "denied-save.json"); Call(w, "SaveStandaloneTo", file); var original = File.ReadAllBytes(file); var baseline = Get<string>(w, "lastSavedSnapshot"); var revision = Get<StyleLibrary>(w, "currentLibrary").Revision; Box(w, "NameBox").Text = "Blocked replacement"; using (var held = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read)) { try { Call(w, "SaveStandaloneTo", file); throw new Exception("Expected blocked replacement failure"); } catch (TargetInvocationException e) { Expect(e.GetBaseException() is IOException, "Expected file replacement I/O error"); } } Expect(File.ReadAllBytes(file).SequenceEqual(original) && Get<string>(w, "lastSavedSnapshot") == baseline && Get<StyleLibrary>(w, "currentLibrary").Revision == revision, "Failed replacement mutated original or baseline/revision"); w.Close(); });
        Check("T05 same-file sync writes once and honors external revision", () => { var w = Window(Rect()); var file = Path.Combine(output, "same-target.json"); Call(w, "SaveStandaloneTo", file); Box(w, "NameBox").Text = "One destination"; Call(w, "SyncLoadedLibrary", file, StyleLibraryStore.Load(file)); Expect(StyleLibraryStore.Load(file).Library.Styles.Count == 1, "Same-file sync duplicated style"); Expect(Get<string>(w, "lastSavedSnapshot") == Get<string>(w, "lastSyncedSnapshot"), "Same-file baselines diverged"); var baseline = Get<string>(w, "lastSavedSnapshot"); var other = StyleLibraryStore.Load(file); StyleLibraryStore.Write(file, other.Library, other.Revision); Box(w, "NameBox").Text = "Conflict"; try { Call(w, "SyncLoadedLibrary", file, StyleLibraryStore.Load(file)); throw new Exception("Expected same-file revision conflict"); } catch (TargetInvocationException e) { Expect(e.InnerException is StyleLibraryException, "Unexpected same-file conflict type"); } Expect(Get<string>(w, "lastSavedSnapshot") == baseline, "Conflict falsely cleared dirty"); w.Close(); });
        Check("T09 bounded large preview raster", () => { var w = Window(); var bitmap = LayerCompositor.Render([], 3000, 3000, (layer, sx, sy) => (FrameworkElement)Call(w, "VisualFor", layer, sx, sy, new StyleLayer(), -1)!); Expect(bitmap.PixelWidth * bitmap.PixelHeight <= 4_000_000, "Raster pixel budget exceeded"); w.Close(); });
        Check("T10 black inverse luminance reveals target", () => Mask("inverseLuminance", "#000000", 1, 255));
        // EN: Reproduce independent-review failures through the outer Sync command and verify rejected collisions preserve both disk and in-memory siblings.
        // ZH: 通过外层 Sync 命令复现独立审查问题，并验证拒绝冲突时保留磁盘及内存中的同级条目。
        Check("T05 review unique ID rename full Sync keeps current entry", () => {
            var (w, file) = SameFileWindow("review-rename");
            var sibling = JsonSerializer.Serialize(Get<StyleLibrary>(w, "currentLibrary").Styles[0]);
            Box(w, "IdBox").Text = "renamed-owned-style"; Box(w, "NameBox").Text = "Renamed current entry";
            Call(w, "Sync", false);
            var saved = StyleLibraryStore.Load(file).Library;
            Expect(saved.Styles.Count == 2 && saved.Styles[1].Id == "renamed-owned-style" && !saved.Styles.Any(s => s.Id == "owned-style"), "ID rename appended or replaced the wrong entry");
            Expect(JsonSerializer.Serialize(saved.Styles[0]) == sibling && Get<int>(w, "currentLibraryIndex") == 1, "ID rename changed its sibling or current association");
            Expect(Get<string>(w, "lastSavedSnapshot") == (string)Call(w, "Snapshot")! && Get<bool>(w, "hasSynced"), "Successful same-file sync lost baselines"); w.Close();
        });
        Check("T05 review sibling ID collision preserves both library owners", () => {
            var (w, file) = SameFileWindow("review-collision");
            var disk = File.ReadAllBytes(file); var savedBaseline = Get<string>(w, "lastSavedSnapshot");
            var current = Get<StyleLibrary>(w, "currentLibrary"); var sibling = JsonSerializer.Serialize(current.Styles[0]);
            Box(w, "IdBox").Text = "untouched-sibling"; Box(w, "NameBox").Text = "Must never overwrite sibling";
            var loaded = StyleLibraryStore.Load(file); var loadedBefore = JsonSerializer.Serialize(loaded.Library);
            try { Call(w, "SyncLoadedLibrary", file, loaded); throw new Exception("Sibling ID collision was accepted"); }
            catch (TargetInvocationException e) { Expect(e.InnerException is InvalidDataException, "Collision did not produce a validation error"); }
            Expect(File.ReadAllBytes(file).SequenceEqual(disk), "Collision changed disk bytes");
            Expect(JsonSerializer.Serialize(current.Styles[0]) == sibling && JsonSerializer.Serialize(loaded.Library) == loadedBefore, "Collision corrupted an in-memory library sibling");
            Expect(ReferenceEquals(current, Get<StyleLibrary>(w, "currentLibrary")) && Get<int>(w, "currentLibraryIndex") == 1 && Get<string>(w, "lastSavedSnapshot") == savedBaseline && Get<string?>(w, "lastSyncedSnapshot") == null, "Collision advanced ownership or persistence baselines"); w.Close();
        });
        Check("T05 review unsaved full Sync retains visible dirty markers", () => {
            var w = Window(); var baseline = Get<string>(w, "lastSavedSnapshot");
            var dir = Path.Combine(output, "review-unsaved"); Directory.CreateDirectory(dir); var target = Path.Combine(dir, "main.js"); File.WriteAllText(target, "// isolated source"); Set(w, "connectedTarget", target);
            Box(w, "NameBox").Text = "Saved only to the connected library"; Call(w, "Sync", false);
            Expect(Get<string?>(w, "currentFile") == null && Get<string>(w, "lastSavedSnapshot") == baseline && baseline != (string)Call(w, "Snapshot")!, "Full Sync changed standalone baseline");
            Expect(w.Title.EndsWith(" *") && ((Button)w.FindName("SaveBtn")).Content.ToString()!.Contains('●'), "Full Sync cleared title/Save dirty markers");
            Expect(Get<bool>(w, "hasSynced") && StyleLibraryStore.Load(StyleLibraryLocation.ForSource(target)).Library.Styles[0].Name == "Saved only to the connected library", "Full Sync did not persist target or mark it synced"); w.Close();
        });
        File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        FrameShadow.Shutdown(); Console.WriteLine($"APPROVED_FOLLOWUP checks={results.Count} failures={failures}"); return failures == 0 ? 0 : 1;
    }
}
