using System.IO;
using System.Windows;

namespace KnotJotTextStyleEditor;
public partial class App : Application
{
    // EN: Register UI-thread exception recovery so one failed operation is logged and reported without exiting.
    // ZH: 注册界面线程异常恢复，将失败操作记入日志并提示用户，避免直接退出。
    public App()
    {
        DispatcherUnhandledException += /* EN: Log and present a recoverable UI error, then mark it handled. ZH: 记录并提示可恢复界面异常，再将其标记为已处理。 */ (_, e) =>
        {
            LogException(e.Exception);
            MessageBox.Show((UiState.English ? "The operation failed, but the app can continue. A diagnostic log was saved.\n\n" : "本次操作遇到异常，已阻止程序退出。可继续操作；诊断记录已保存。\n\n") + e.Exception.Message, UiState.English ? "Operation not completed" : "操作未完成", MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Handled = true;
        };
    }

    // EN: Append timestamped diagnostics under LocalApplicationData; logging failures must not replace the original error.
    // ZH: 在本地应用数据目录追加带时间戳的诊断信息；日志失败不能掩盖原始错误。
    public static void LogException(Exception ex)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KnotJotTextStyleEditor");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "error.log"), $"[{DateTime.Now:O}] {ex}\n\n");
        }
        catch
        {
        }
    }
}
