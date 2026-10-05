using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// EN: Store only the process/display boundary outputs; source validation and launch branches execute unchanged production method bodies.
// ZH: 仅保存进程和提示边界输出；路径校验及启动分支执行未经改写的生产方法体。
public static class LaunchCapture
{
    public static ProcessStartInfo? StartInfo;
    public static string? Error;
}

internal static class Program
{
    static readonly List<object> results = [];
    static int failures;

    // EN: Fail on an observable launch-contract mismatch, with independently defined expectations.
    // ZH: 启动约定不符合独立定义的预期时，明确报告失败。
    static void Expect(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    // EN: Retain every independent result even when the frozen baseline fails earlier cases.
    // ZH: 即使冻结基线的前序用例失败，也保留各项独立结果。
    static void Check(string name, Action action)
    {
        try { action(); results.Add(new { name, pass = true }); Console.WriteLine("PASS " + name); }
        catch (Exception error) { failures++; results.Add(new { name, pass = false, error = error.ToString() }); Console.WriteLine("FAIL " + name + ": " + error.GetBaseException().Message); }
    }

    // EN: Compile the original Launch and ResolveTarget methods verbatim; replace only OS process creation and dialog display to avoid file associations and live app profiles.
    // ZH: 原样编译 Launch 与 ResolveTarget，仅替换操作系统进程创建和对话框显示，避免文件关联和真实应用配置受到影响。
    static Action<string> CompileLaunch(string sourceFile, bool textEditor)
    {
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile)).GetRoot();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m => m.Identifier.Text is "Launch" or "ResolveTarget").ToArray();
        Expect(methods.Length == 2, "Expected one production Launch and one production ResolveTarget method.");
        var setup = textEditor
            ? "string? connectedTarget; bool Connect() => false; string T(string zh, string en) => en; public void Invoke(string target) { connectedTarget = target; Launch(); }"
            : "public void Invoke(string target) { Launch(target); }";
        var source = """
            using System;
            using System.IO;
            using System.Diagnostics;
            using IOPath = System.IO.Path;
            public sealed class LaunchHarness {
            private static class Process {
                public static System.Diagnostics.Process? Start(ProcessStartInfo info) { LaunchCapture.StartInfo = info; return null; }
            }
            private static class MessageBox {
                public static void Show(string message) { LaunchCapture.Error = message; }
            }
            """ + setup + string.Join("\n", methods.Select(m => m.ToFullString())) + "}";
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(LaunchCapture).Assembly.Location).Distinct().Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("LaunchHarness_" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        Expect(emitted.Success, "Production launch harness did not compile: " + string.Join("\n", emitted.Diagnostics));
        var type = Assembly.Load(stream.ToArray()).GetType("LaunchHarness")!;
        var instance = Activator.CreateInstance(type);
        return target =>
        {
            LaunchCapture.StartInfo = null; LaunchCapture.Error = null;
            try { type.GetMethod("Invoke")!.Invoke(instance, [target]); }
            catch (TargetInvocationException error) { LaunchCapture.Error = error.InnerException!.Message; }
        };
    }

    // EN: Create independent source folders containing spaces, Chinese characters and shell metacharacters; only the harmless test apphost is installed as Electron.
    // ZH: 建立含空格、中文及外壳特殊字符的独立源码目录，仅将无害测试程序放在 Electron 位置。
    static string Fixture(string output, string label, bool electron = true, string entryName = "main.js")
    {
        var directory = Path.Combine(output, label + " 中文 source & percent% !");
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, entryName);
        File.WriteAllText(target, "// Isolated launch fixture; never passed to a script host.\n");
        if (electron)
        {
            var runtime = Path.Combine(directory, "node_modules", "electron", "dist");
            Directory.CreateDirectory(runtime);
            foreach (var file in Directory.GetFiles(AppContext.BaseDirectory))
                File.Copy(file, Path.Combine(runtime, Path.GetFileName(file)), true);
            File.Copy(Path.Combine(AppContext.BaseDirectory, "SourceLaunchSmoke.exe"), Path.Combine(runtime, "electron.exe"), true);
        }
        return target;
    }

    // EN: Check the process contract that must be passed to Windows, including one safely escaped source argument and the source working directory.
    // ZH: 检查传入 Windows 的进程约定，包含安全转义的单个源码参数和源码工作目录。
    static ProcessStartInfo ExpectSourceLaunch(Action<string> launch, string target)
    {
        launch(target);
        Expect(LaunchCapture.Error == null, "Unexpected launch error: " + LaunchCapture.Error);
        var info = LaunchCapture.StartInfo;
        Expect(info != null, "No process was prepared.");
        var sourceDirectory = Path.GetDirectoryName(target)!;
        Expect(info!.FileName == Path.Combine(sourceDirectory, "node_modules", "electron", "dist", "electron.exe"), "Source must launch its local Electron executable; actual=" + info.FileName);
        Expect(!info.UseShellExecute, "Source launch must not depend on shell/PATH or .js file associations.");
        Expect(info.WorkingDirectory == sourceDirectory, "Source working directory was not selected.");
        Expect(info.ArgumentList.Count == 1 && info.ArgumentList[0] == target && info.Arguments.Length == 0, "main.js must remain one exact path argument.");
        return info;
    }

    // EN: Run source-launch regressions for both editors, then exercise Windows argument delivery through a harmless child mode without opening KnotJot.
    // ZH: 对两个编辑器运行源码启动回归，再通过无害子进程模式验证 Windows 参数传递，不打开 KnotJot。
    static int Main(string[] args)
    {
        var childRecord = Environment.GetEnvironmentVariable("KNOTJOT_SOURCE_LAUNCH_FIXTURE_RECORD");
        if (!string.IsNullOrEmpty(childRecord))
        {
            File.WriteAllText(childRecord, JsonSerializer.Serialize(new { executable = Environment.ProcessPath, arguments = args, directory = Environment.CurrentDirectory }));
            return 0;
        }
        if (args.Length != 3) throw new ArgumentException("Usage: SourceLaunchSmoke <text MainWindow.xaml.cs> <UI KnotJotConnection.cs> <new-evidence-directory>");
        var output = Path.GetFullPath(args[2]); Directory.CreateDirectory(output);
        for (var index = 0; index < 2; index++)
        {
            var label = index == 0 ? "text" : "ui";
            var launch = CompileLaunch(Path.GetFullPath(args[index]), index == 0);
            Check(label + "-source-local-electron", () => ExpectSourceLaunch(launch, Fixture(output, label + "-source")));
            Check(label + "-uppercase-source", () => ExpectSourceLaunch(launch, Fixture(output, label + "-uppercase", entryName: "MAIN.JS")));
            Check(label + "-source-missing-electron", () =>
            {
                launch(Fixture(output, label + "-missing-dependency", electron: false));
                Expect(LaunchCapture.StartInfo == null, "Missing Electron must fail before starting any process.");
                var message = LaunchCapture.Error ?? "";
                Expect(message.Contains("node_modules/electron") && message.Contains("KnotJot.exe") && (message.Contains("依赖") || message.Contains("dependencies")), "Missing Electron must explain installing dependencies or selecting the installed EXE.");
            });
            Check(label + "-source-removed-after-connect", () =>
            {
                var target = Fixture(output, label + "-missing-source"); File.Delete(target); launch(target);
                Expect(LaunchCapture.StartInfo == null && !string.IsNullOrWhiteSpace(LaunchCapture.Error), "Removed source must fail before starting Electron.");
            });
            Check(label + "-installed-exe-preserved", () =>
            {
                var target = Path.Combine(output, label + " installed 中文.exe"); File.WriteAllText(target, "fixture"); launch(target);
                var info = LaunchCapture.StartInfo;
                Expect(LaunchCapture.Error == null && info != null && info.FileName == target && info.UseShellExecute, "Installed EXE shell launch changed.");
                Expect(info!.Arguments.Length == 0 && info.ArgumentList.Count == 0, "Installed EXE received source arguments.");
            });
            Check(label + "-actual-child-arguments", () =>
            {
                var target = Fixture(output, label + "-child"); var info = ExpectSourceLaunch(launch, target);
                var record = Path.Combine(output, label + "-child-received.json");
                info.Environment["KNOTJOT_SOURCE_LAUNCH_FIXTURE_RECORD"] = record;
                using var process = Process.Start(info)!;
                if (!process.WaitForExit(10000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Fixture child did not finish."); }
                Expect(process.ExitCode == 0 && File.Exists(record), "Fixture process failed to record its inputs.");
                using var doc = JsonDocument.Parse(File.ReadAllText(record)); var root = doc.RootElement;
                Expect(root.GetProperty("executable").GetString() == info.FileName, "Windows started a different executable.");
                Expect(root.GetProperty("arguments").GetArrayLength() == 1 && root.GetProperty("arguments")[0].GetString() == target, "Windows split or changed the source path argument.");
                Expect(root.GetProperty("directory").GetString() == Path.GetDirectoryName(target), "Child inherited the wrong working directory.");
            });
        }
        File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        return failures == 0 ? 0 : 1;
    }
}
