using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using KnotJotTextStyleEditor;

internal static class Program
{
    const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
    static readonly List<object> results = [];
    static int failures;
    // EN: Invoke the repaired reader or the baseline's immediate ReadAllText operation so the same sharing-lock tests produce genuine red/green evidence.
    // ZH: 调用修复读取器或基线立即 ReadAllText 操作，使同一共享锁测试产生真实红绿证据。
    static string Read(string file, DateTime deadline)
    {
        var type = typeof(MainWindow).Assembly.GetType("KnotJotTextStyleEditor.KnotJotConnection");
        try { return type == null ? File.ReadAllText(file) : (string)type.GetMethod("ReadReplyText", Flags)!.Invoke(null, [file, deadline])!; }
        catch (TargetInvocationException error) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException!).Throw(); throw; }
    }
    // EN: Report exact failures without skipping independent error-handling cases. ZH: 精确报告失败，不跳过独立错误处理用例。
    static void Expect(bool condition, string message) { if (!condition) throw new Exception(message); }
    // EN: Retain one result for every real-file or executable handshake check. ZH: 为每项真实文件或程序握手检查保留结果。
    static void Check(string name, Action action) { try { action(); results.Add(new { name, pass = true }); Console.WriteLine("PASS " + name); } catch (Exception error) { failures++; results.Add(new { name, pass = false, error = error.ToString() }); Console.WriteLine("FAIL " + name + " " + error.GetBaseException().Message); } }
    // EN: Keep a valid reply exclusively locked after bytes reach disk, reproducing the writer/reader overlap without touching user files.
    // ZH: 有效响应字节写入磁盘后保持独占锁，复现读写重叠，不接触用户文件。
    static FileStream Locked(string file, string text)
    {
        var stream = new FileStream(file, FileMode.Create, FileAccess.ReadWrite, FileShare.None); var bytes = Encoding.UTF8.GetBytes(text); stream.Write(bytes); stream.Flush(true); return stream;
    }
    // EN: Run an actual locked EXE reply mode or the isolated regression suite; each timeout is short but shares the caller's original deadline.
    // ZH: 执行真实持锁程序响应模式或隔离回归套件；每项超时较短，但始终共用调用方原期限。
    [STAThread]
    static int Main(string[] args)
    {
        var replyArg = args.FirstOrDefault(arg => arg.StartsWith("--knotjot-text-styles-location-file=", StringComparison.Ordinal));
        if (replyArg != null)
        {
            var file = replyArg.Split('=', 2)[1]; var payload = JsonSerializer.Serialize(new { format = "knotjot-text-styles-location", version = 1, file = Environment.GetEnvironmentVariable("KNOTJOT_TEST_LOCKED_LOCATION_FILE") }); using var writer = Locked(file, payload); Thread.Sleep(350); return 0;
        }
        var output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output); var target = Path.Combine(output, "custom-text-styles.json");
        Environment.SetEnvironmentVariable("KNOTJOT_TEXT_STYLE_EDITOR_DATA_DIR", Path.Combine(output, "profile")); Environment.SetEnvironmentVariable("KNOTJOT_TEST_LOCKED_LOCATION_FILE", target);
        var valid = JsonSerializer.Serialize(new { format = "knotjot-text-styles-location", version = 1, file = target });
        Check("released sharing lock returns the exact typed reply", () => { var file = Path.Combine(output, "released.json"); var writer = Locked(file, valid); var read = Task.Run(() => Read(file, DateTime.UtcNow.AddSeconds(2))); Thread.Sleep(120); bool waited = !read.IsCompleted; writer.Dispose(); var text = read.GetAwaiter().GetResult(); Expect(waited && text == valid && StyleLibraryLocation.ParseReply(text) == target, "Reader did not wait for and preserve the complete reply."); });
        Check("permanent lock times out with original sharing error", () => { var file = Path.Combine(output, "permanent.json"); using var writer = Locked(file, valid); var watch = Stopwatch.StartNew(); try { Read(file, DateTime.UtcNow.AddMilliseconds(180)); throw new Exception("Locked reply was accepted."); } catch (TimeoutException error) { Expect(error.InnerException is IOException && watch.ElapsedMilliseconds >= 140 && watch.ElapsedMilliseconds < 1500, "Timeout lost sharing error or restarted the deadline."); } });
        Check("expired original deadline does not restart a wait", () => { var file = Path.Combine(output, "expired.json"); using var writer = Locked(file, valid); var watch = Stopwatch.StartNew(); try { Read(file, DateTime.UtcNow.AddSeconds(-1)); throw new Exception("Expired locked reply succeeded."); } catch (TimeoutException error) { Expect(error.InnerException is IOException && watch.ElapsedMilliseconds < 100, "Expired caller deadline was replaced."); } });
        Check("missing file remains a direct permanent error", () => { var watch = Stopwatch.StartNew(); try { Read(Path.Combine(output, "missing.json"), DateTime.UtcNow.AddSeconds(3)); throw new Exception("Missing reply accepted."); } catch (FileNotFoundException) { Expect(watch.ElapsedMilliseconds < 150, "Non-sharing IO failure was retried."); } });
        Check("invalid JSON remains invalid instead of being retried", () => { var file = Path.Combine(output, "invalid.json"); File.WriteAllText(file, "{invalid"); var watch = Stopwatch.StartNew(); try { StyleLibraryLocation.ParseReply(Read(file, DateTime.UtcNow.AddSeconds(3))); throw new Exception("Invalid JSON accepted."); } catch (JsonException) { Expect(watch.ElapsedMilliseconds < 150 && File.ReadAllText(file) == "{invalid", "Invalid data was retried or changed."); } });
        Check("real locked executable handshake succeeds and cleans reply", () => { _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; var window = new MainWindow(); var executable = Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".exe"); var before = Directory.GetFiles(Path.GetTempPath(), "knotjot-text-styles-location-*.json").ToHashSet(); try { var actual = (string)typeof(MainWindow).GetMethod("ResolveConnectedLibrary", Flags)!.Invoke(window, [executable])!; Expect(actual == target && !Directory.GetFiles(Path.GetTempPath(), "knotjot-text-styles-location-*.json").Except(before).Any(), "Locked EXE reply failed or was not cleaned."); } finally { window.Close(); } });
        File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true })); Console.WriteLine($"CONNECTION_REPLY checks={results.Count} failures={failures}"); return failures == 0 ? 0 : 1;
    }
}
