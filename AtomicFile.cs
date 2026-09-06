using System;
using System.IO;
using System.Text;

namespace KnotJotUiEditor
{
    public static class AtomicFile
    {
        // Write UTF-8 to a sibling temporary file, replace the destination, and clean up on failure.
        // 先写入同目录 UTF-8 临时文件，再替换目标，并在失败时清理临时文件。
        public static void WriteUtf8(string path, string text)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("保存路径不能为空。", nameof(path));
            var full = Path.GetFullPath(path);
            var dir = Path.GetDirectoryName(full);
            if (string.IsNullOrEmpty(dir)) throw new IOException("无法确定保存目录。");
            Directory.CreateDirectory(dir);
            var tmp = full + ".tmp-" + Environment.ProcessId + "-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(tmp, text ?? "", new UTF8Encoding(false));
                if (File.Exists(full)) File.Replace(tmp, full, null, true);
                else File.Move(tmp, full);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }
    }
}
