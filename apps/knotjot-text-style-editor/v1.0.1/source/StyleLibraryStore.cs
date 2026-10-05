using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KnotJotTextStyleEditor;
// EN: Carry the failing path and optional corrupt backup alongside the original persistence error.
// ZH: 在原始持久化错误之外附带失败路径及可选损坏备份。
public sealed class StyleLibraryException(string message, string file, string? backupFile = null, Exception? inner = null) : Exception(message, inner)
{
    public string FilePath { get; } = file;
    public string? BackupFile { get; } = backupFile;
}

// EN: Return validated content with disk revision, SHA-256 fingerprint, and legacy-migration status.
// ZH: 返回已校验内容及磁盘版本、SHA-256 指纹和旧格式迁移状态。
public sealed record LoadedStyleLibrary(StyleLibrary Library, string FilePath, long Revision, string Fingerprint, bool MigratedLegacyFormat);
// EN: Report the committed file, incremented revision, process writer ID, and intended backup path.
// ZH: 报告已提交文件、递增版本、进程写入者 ID 及预定备份路径。
public sealed record LibraryWriteResult(string FilePath, long Revision, string WriterId, string BackupFile);
public static class StyleLibraryStore
{
    // EN: Share camelCase JSON, case-insensitive per-path gates, and a process-specific writer identity across all library operations.
    // ZH: 所有样式库操作共享驼峰 JSON、忽略路径大小写的锁，以及进程专用写入者标识。
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
    static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);
    static readonly string WriterId = $"knotjot-text-style-editor-{Environment.ProcessId}-{Guid.NewGuid():N}";
    // EN: Select persistence diagnostics using the shared application language flag.
    // ZH: 根据应用共享语言标记选择持久化诊断文字。
    static string L(string zh, string en) => UiState.English ? en : zh;
    // EN: Read and validate library bytes with revision/fingerprint metadata, optionally backing up corrupt content before reporting failure.
    // ZH: 读取并校验样式库字节及版本指纹，失败报告前可选择备份损坏内容。
    public static LoadedStyleLibrary Load(string file, bool backupCorrupt = false)
    {
        file = Path.GetFullPath(file);
        if (!File.Exists(file))
            return new(new StyleLibrary(), file, 0, "", false);
        try
        {
            var bytes = File.ReadAllBytes(file);
            var library = JsonSerializer.Deserialize<StyleLibrary>(bytes, Json) ?? throw new InvalidDataException(L("样式库根对象为空。", "The style library root is null."));
            var migrated = ValidateAndMigrate(library);
            return new(library, file, library.Revision, Convert.ToHexString(SHA256.HashData(bytes)), migrated);
        }
        catch (Exception ex)when (ex is JsonException or InvalidDataException or NotSupportedException)
        {
            var backup = backupCorrupt ? BackupCorrupt(file) : null;
            throw new StyleLibraryException(ex.Message, file, backup, ex);
        }
    }

    // EN: Accept supported v1 envelopes, reject null/duplicate entries, normalize each style, and migrate the legacy format identifier.
    // ZH: 接受支持的第一版封装、拒绝空或重复条目、规范化各样式，并迁移旧格式标识。
    public static bool ValidateAndMigrate(StyleLibrary library)
    {
        if (library == null)
            throw new InvalidDataException(L("样式库为空。", "The style library is null."));
        var migrated = string.Equals(library.Format, "thoughtcanvas-text-styles", StringComparison.OrdinalIgnoreCase);
        if (!migrated && !string.Equals(library.Format, "knotjot-text-styles", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(L($"不支持的样式库格式：{library.Format}", $"Unsupported style-library format: {library.Format}"));
        if (library.Version != 1)
            throw new InvalidDataException(L($"不支持的样式库版本：{library.Version}", $"Unsupported style-library version: {library.Version}"));
        if (library.Styles == null)
            throw new InvalidDataException(L("styles 字段必须是数组。", "The styles field must be an array."));
        if (library.StyleSettings == null)
            throw new InvalidDataException(L("styleSettings 字段必须是对象。", "The styleSettings field must be an object."));
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var style in library.Styles)
        {
            if (style == null)
                throw new InvalidDataException(L("styles 数组包含空记录。", "The styles array contains a null record."));
            if (string.IsNullOrWhiteSpace(style.Id))
                throw new InvalidDataException(L("每套样式都必须有非空 ID。", "Every style must have a non-empty ID."));
            if (!ids.Add(style.Id))
                throw new InvalidDataException(L($"样式 ID 重复：{style.Id}", $"Duplicate style ID: {style.Id}"));
            style.Layers ??= [];
            style.TextRegion ??= new();
            style.TextRules ??= new();
            style.TextSizing ??= new();
            ModelSafety.Normalize(style);
        }

        library.Format = "knotjot-text-styles";
        library.WriterId ??= "";
        library.DefaultStyleId = string.IsNullOrWhiteSpace(library.DefaultStyleId) ? "classic" : library.DefaultStyleId;
        return migrated;
    }

    // EN: Serialize writers to the same normalized path within this process and always release the semaphore.
    // ZH: 在本进程内串行化同一规范化路径的写入，并确保释放信号量。
    public static LibraryWriteResult Write(string file, StyleLibrary library, long? expectedRevision = null)
    {
        file = Path.GetFullPath(file);
        var gate = Gates.GetOrAdd(file, /* EN: Create the per-path single-writer semaphore on first use. ZH: 首次使用时为路径创建单写入信号量。 */ _ => new SemaphoreSlim(1, 1));
        gate.Wait();
        try
        {
            return WriteCore(file, library, expectedRevision);
        }
        finally
        {
            gate.Release();
        }
    }

    // EN: Hold a cross-process lock, reject stale revisions, flush a sibling temporary file, and replace the library with rollback backup.
    // ZH: 持有跨进程锁、拒绝过期版本、刷写同目录临时文件，并通过回滚备份替换样式库。
    static LibraryWriteResult WriteCore(string file, StyleLibrary library, long? expectedRevision)
    {
        ValidateAndMigrate(library);
        var directory = Path.GetDirectoryName(file) ?? throw new InvalidOperationException("The style-library path has no directory.");
        Directory.CreateDirectory(directory);
        using var processLock = AcquireProcessLock(file);
        long diskRevision = 0;
        if (File.Exists(file))
            diskRevision = Load(file).Revision;
        if (expectedRevision.HasValue && diskRevision != expectedRevision.Value)
            throw new StyleLibraryException(L($"样式库已被外部修改（预期 revision {expectedRevision.Value}，实际 {diskRevision}），请重新加载后再保存。", $"The style library changed outside this editor (expected revision {expectedRevision.Value}, found {diskRevision}). Reload it before saving."), file);
        library.Revision = Math.Max(diskRevision, library.Revision) + 1;
        library.WriterId = WriterId;
        var payload = JsonSerializer.SerializeToUtf8Bytes(library, Json);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(file)}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");
        var backup = file + ".bak";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.WriteThrough))
            {
                stream.Write(payload);
                stream.Flush(true);
            }

            if (File.Exists(file))
            {
                try
                {
                    File.Replace(temporary, file, backup, true);
                }
                catch (PlatformNotSupportedException)
                {
                    ReplaceWithRollback(temporary, file, backup);
                }
                catch (IOException)
                {
                    ReplaceWithRollback(temporary, file, backup);
                }
            }
            else
                File.Move(temporary, file);
            return new(file, library.Revision, WriterId, backup);
        }
        finally
        {
            try
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
            catch
            {
            }
        }
    }

    // EN: Fallback from atomic replacement by moving the old file to backup and restoring it if installation of the new file fails.
    // ZH: 原子替换不可用时先移动旧文件到备份，新文件安装失败则恢复旧文件。
    static void ReplaceWithRollback(string temporary, string file, string backup)
    {
        if (File.Exists(backup))
            File.Delete(backup);
        File.Move(file, backup);
        try
        {
            File.Move(temporary, file);
        }
        catch
        {
            if (!File.Exists(file) && File.Exists(backup))
                File.Move(backup, file);
            throw;
        }
    }

    // EN: Acquire an exclusive delete-on-close lock file, retrying forty times at 50 ms intervals before reporting contention.
    // ZH: 获取关闭即删除的排他锁文件，以五十毫秒间隔重试四十次，仍失败则报告占用。
    static FileStream AcquireProcessLock(string file)
    {
        var lockFile = file + ".lock";
        Exception? last = null;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            try
            {
                return new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            }
            catch (IOException ex)
            {
                last = ex;
                Thread.Sleep(50);
            }
        }

        throw new StyleLibraryException(L("样式库正在被另一个进程写入。", "The style library is busy in another process."), file, inner: last);
    }

    // EN: Copy malformed input to a timestamped sibling file without replacing the source.
    // ZH: 将损坏输入复制到带时间戳的同目录文件，保留原始内容。
    static string BackupCorrupt(string file)
    {
        var directory = Path.GetDirectoryName(file)!;
        var stem = Path.GetFileNameWithoutExtension(file);
        var extension = Path.GetExtension(file);
        var backup = Path.Combine(directory, $"{stem}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss-fff}{extension}");
        File.Copy(file, backup, false);
        return backup;
    }
}
