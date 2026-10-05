using System.IO;

namespace KnotJotTextStyleEditor;

// EN: Handle the brief file-sharing overlap between a main-app location reply appearing and its writer closing, without changing the protocol.
// ZH: 处理主程序路径响应出现到写入方关闭之间的短暂共享锁重叠，不改变协议。
internal static class KnotJotConnection
{
    // EN: Retry only Windows sharing/lock violations within the caller's existing deadline; preserve permanent IO errors and leave JSON validation to the caller.
    // ZH: 仅在调用方原期限内重试 Windows 共享或锁冲突；保留永久 IO 错误，并由调用方继续验证 JSON。
    internal static string ReadReplyText(string response, DateTime deadline)
    {
        while (true)
        {
            try { return File.ReadAllText(response); }
            catch (IOException error) when ((error.HResult & 0xffff) is 32 or 33)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero) throw new TimeoutException("The main-app location reply remained locked until the original query deadline.", error);
                Thread.Sleep((int)Math.Min(50, Math.Max(1, remaining.TotalMilliseconds)));
            }
        }
    }
}
