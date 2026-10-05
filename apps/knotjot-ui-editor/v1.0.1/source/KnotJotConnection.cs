using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;

namespace KnotJotUiEditor
{
    public sealed class KnotJotSkinLibrary
    {
        [JsonPropertyName("format")] public string Format { get; set; } = UiSkinProtocol.Current.Format;
        [JsonPropertyName("version")] public int Version { get; set; } = 1;
        [JsonPropertyName("revision")] public long Revision { get; set; }
        [JsonPropertyName("writerId")] public string WriterId { get; set; } = "";
        [JsonPropertyName("updatedUtc")] public string UpdatedUtc { get; set; } = "";
        [JsonPropertyName("current")] public string Current { get; set; } = "default";
        [JsonPropertyName("skins")] public Dictionary<string, JsonElement> Skins { get; set; } = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
    }

    public sealed class KnotJotSkinRecord
    {
        [JsonPropertyName("name")] public string Name { get; set; }
        [JsonPropertyName("kind")] public string Kind { get; set; } = "css";
        [JsonPropertyName("css")] public string Css { get; set; }
        [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; } = 1;
        [JsonPropertyName("updatedUtc")] public string UpdatedUtc { get; set; }
        [JsonPropertyName("contentHash")] public string ContentHash { get; set; }
        [JsonPropertyName("writerId")] public string WriterId { get; set; }
        [JsonPropertyName("sourceFile")] public string SourceFile { get; set; }
        [JsonPropertyName("editorData")] public JsonElement? EditorData { get; set; }
    }

    public sealed class KnotJotSyncResult
    {
        public string File { get; set; }
        public string SkinId { get; set; }
        public string ContentHash { get; set; }
        public long Revision { get; set; }
    }

    public sealed class KnotJotConnectionInfo
    {
        public string Target { get; set; }
        public string LibraryPath { get; set; }
    }

    /// <summary>KnotJot 路径握手、连接持久化和动态皮肤库写入。</summary>
    public static class KnotJotConnection
    {
        // Read the CSS byte limit from the shared UI-skin protocol definition.
        // 从共享 UI 皮肤协议定义读取 CSS 字节上限。
        public static int MaxCssBytes => UiSkinProtocol.Current.Limits.CssBytes;
        private static readonly string WriterId = "knotjot-ui-editor-" + Environment.ProcessId + "-" + Guid.NewGuid().ToString("N");
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true, PropertyNameCaseInsensitive = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        // Resolve connection storage from supported environment overrides or the user profile.
        // 根据受支持的环境变量覆盖或用户配置目录确定连接存储位置。
        public static string ConnectionConfigPath
        {
            get
            {
                var custom = Environment.GetEnvironmentVariable("KNOTJOT_UI_EDITOR_DATA_DIR");
                if (string.IsNullOrWhiteSpace(custom))
                    custom = Environment.GetEnvironmentVariable("BMAP_UI_EDITOR_DATA_DIR");
                var dir = string.IsNullOrWhiteSpace(custom)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KnotJot界面编辑器") : custom;
                return Path.Combine(dir, "connection.json");
            }
        }

        // Generate a persistent UI skin identifier with the protocol's ui_ prefix.
        // 生成带协议 ui_ 前缀的持久 UI 皮肤标识符。
        public static string NewSkinId() { return "ui_" + Guid.NewGuid().ToString("N"); }

        // Retain a protocol-valid skin ID or generate a replacement for invalid input.
        // 保留符合协议的皮肤 ID，并为无效输入生成替代值。
        public static string NormalizeSkinId(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, UiSkinProtocol.Current.IdPattern) ? value : NewSkinId();
        }

        // Resolve shortcuts and validate an existing executable or main.js connection target.
        // 解析快捷方式，并验证可执行文件或 main.js 连接目标存在。
        public static string ResolveTarget(string file)
        {
            if (string.IsNullOrWhiteSpace(file)) throw new ArgumentException("主程序路径不能为空。", nameof(file));
            var full = Path.GetFullPath(file);
            if (full.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("系统快捷方式服务不可用。");
                dynamic shell = Activator.CreateInstance(type);
                dynamic shortcut = shell.CreateShortcut(full);
                full = Path.GetFullPath((string)shortcut.TargetPath);
            }
            if (!File.Exists(full)) throw new FileNotFoundException("KnotJot 主程序或 main.js 不存在。", full);
            var ext = Path.GetExtension(full);
            if (!ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".js", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("请选择 KnotJot.exe、源码 main.js 或指向它们的快捷方式。");
            if (ext.Equals(".js", StringComparison.OrdinalIgnoreCase) && !Path.GetFileName(full).Equals("main.js", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("源码连接必须选择 main.js。");
            return full;
        }

        // Query the main application's skin-library location and persist the validated connection.
        // 查询主程序的皮肤库位置，并保存经过验证的连接。
        public static KnotJotConnectionInfo Connect(string target)
        {
            target = ResolveTarget(target);
            string libraryPath = QueryLibraryPath(target);
            var result = new KnotJotConnectionInfo { Target = target, LibraryPath = libraryPath };
            SaveConnection(result);
            return result;
        }

        // Validate target and library paths before atomically saving connection metadata.
        // 先验证目标与库路径，再原子保存连接元数据。
        public static void SaveConnection(KnotJotConnectionInfo connection)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            connection.Target = ResolveTarget(connection.Target);
            connection.LibraryPath = ValidateLibraryPath(connection.LibraryPath);
            AtomicFile.WriteUtf8(ConnectionConfigPath, JsonSerializer.Serialize(connection, Options));
        }

        // Restore validated connection metadata with a legacy-profile fallback; return null on failure.
        // 恢复经验证的连接元数据，支持旧配置回退，失败时返回 null。
        public static KnotJotConnectionInfo RestoreConnection()
        {
            try
            {
                string file = ConnectionConfigPath;
                if (!File.Exists(file))
                {
                    string legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BMAP界面编辑器", "connection.json");
                    if (!File.Exists(legacy)) return null;
                    file = legacy;
                }
                var data = JsonSerializer.Deserialize<KnotJotConnectionInfo>(File.ReadAllText(file), Options);
                if (data == null) return null;
                data.Target = ResolveTarget(data.Target);
                data.LibraryPath = ValidateLibraryPath(data.LibraryPath);
                return data;
            }
            catch { return null; }
        }

        // Request the main application's library path through the compatible file handshake and validate its reply.
        // 通过兼容的文件握手查询主程序库路径，并验证回复。
        public static string QueryLibraryPath(string target)
        {
            target = ResolveTarget(target);
            var testOverride = Environment.GetEnvironmentVariable("KNOTJOT_UI_SKINS_FILE");
            if (!string.IsNullOrWhiteSpace(testOverride)) return ValidateLibraryPath(testOverride);

            string reply = Path.Combine(Path.GetTempPath(), "knotjot-skins-location-" + Guid.NewGuid().ToString("N") + ".json");
            Process process = null;
            try
            {
                ProcessStartInfo psi;
                // V1.0.0 主程序最初发布时使用这个内部握手名；新版主程序仍保留该别名，
                // 因此统一发送旧握手可以同时连接首发安装包和更新后的实时预览。
                string flag = "--bmap-ui-skins-location-file=\"" + reply + "\"";
                if (target.EndsWith("main.js", StringComparison.OrdinalIgnoreCase))
                {
                    string electron = Path.Combine(Path.GetDirectoryName(target), "node_modules", "electron", "dist", "electron.exe");
                    if (!File.Exists(electron)) throw new InvalidOperationException("KnotJot 源码目录缺少 node_modules/electron；请先安装主程序依赖，或选择已安装的 KnotJot.exe。");
                    psi = new ProcessStartInfo(electron, "\"" + target + "\" " + flag);
                }
                else psi = new ProcessStartInfo(target, flag);
                psi.UseShellExecute = false; psi.CreateNoWindow = true; psi.WindowStyle = ProcessWindowStyle.Hidden;
                process = Process.Start(psi) ?? throw new InvalidOperationException("无法启动 KnotJot 路径查询。");

                var watch = Stopwatch.StartNew();
                while (watch.ElapsedMilliseconds < 10000 && !File.Exists(reply)) Thread.Sleep(50);
                if (!File.Exists(reply)) throw new TimeoutException("KnotJot 未在 10 秒内返回皮肤库位置；连接可能失效或版本不兼容。");
                using var doc = JsonDocument.Parse(File.ReadAllText(reply));
                var root = doc.RootElement;
                if (!root.TryGetProperty("format", out var format) || format.GetString() != UiSkinProtocol.Current.LocationFormat
                    || !root.TryGetProperty("file", out var file))
                    throw new InvalidDataException("KnotJot 返回了不兼容的路径握手数据。");
                return ValidateLibraryPath(file.GetString());
            }
            finally
            {
                try { if (File.Exists(reply)) File.Delete(reply); } catch { }
                process?.Dispose();
            }
        }

        // Update the stable skin record, CSS hash, editable payload, and revision before atomically saving the library.
        // 更新稳定皮肤记录、CSS 哈希、可编辑数据与修订号，再原子保存皮肤库。
        public static KnotJotSyncResult Write(string target, string skinId, string name, string css, string editorJson, string sourceFile)
        {
            target = ResolveTarget(target);
            string libraryPath = QueryLibraryPath(target);
            skinId = NormalizeSkinId(skinId);
            css = css ?? "";
            if (Encoding.UTF8.GetByteCount(css) > MaxCssBytes) throw new InvalidDataException("生成的 UI CSS 超过共享协议的 " + MaxCssBytes + " 字节上限。");
            string hash;
            using (var sha = SHA256.Create()) hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(css)));

            var library = ReadLibraryWithLegacyMigration(libraryPath);
            var now = DateTime.UtcNow.ToString("O");
            var record = new KnotJotSkinRecord
            {
                Name = SafeName(name), Css = css, UpdatedUtc = now, ContentHash = hash,
                WriterId = WriterId, SourceFile = ExistingSourceFile(sourceFile)
            };
            if (!string.IsNullOrWhiteSpace(editorJson))
            {
                if (Encoding.UTF8.GetByteCount(editorJson) > 12 * 1024 * 1024) throw new InvalidDataException("可编辑工程数据超过 12 MB 上限。");
                ProjectFileService.ValidatePayload(editorJson);
                using var editorDoc = JsonDocument.Parse(editorJson); record.EditorData = editorDoc.RootElement.Clone();
            }
            using (var doc = JsonDocument.Parse(JsonSerializer.Serialize(record, Options))) library.Skins[skinId] = doc.RootElement.Clone();
            library.Format = UiSkinProtocol.Current.Format; library.Version = UiSkinProtocol.Current.Version;
            library.Revision = Math.Max(0, library.Revision) + 1; library.WriterId = WriterId;
            library.UpdatedUtc = now; library.Current = skinId;
            // Check the complete merged output before touching the original library; replacement at capacity is valid.
            // 写原库之前校验合并后的完整输出；容量上限内的同 ID 替换有效。
            string payload = JsonSerializer.Serialize(library, Options);
            ValidateLibraryPayload(payload, UiSkinProtocol.Current.Format);
            AtomicFile.WriteUtf8(libraryPath, payload);
            SaveConnection(new KnotJotConnectionInfo { Target = target, LibraryPath = libraryPath });
            return new KnotJotSyncResult { File = libraryPath, SkinId = skinId, ContentHash = hash, Revision = library.Revision };
        }

        // Launch validated source entries through local Electron with one path argument and source working directory; keep installed EXE shell launch unchanged.
        // 经校验的源码入口通过本地 Electron 以单个路径参数及源码工作目录启动；已安装程序的外壳启动保持不变。
        public static void Launch(string target)
        {
            target = ResolveTarget(target);
            if (target.EndsWith("main.js", StringComparison.OrdinalIgnoreCase))
            {
                var directory = Path.GetDirectoryName(target);
                var electron = Path.Combine(directory, "node_modules", "electron", "dist", "electron.exe");
                if (!File.Exists(electron))
                    throw new InvalidOperationException("KnotJot 源码目录缺少 node_modules/electron；请先安装主程序依赖，或选择已安装的 KnotJot.exe。");
                var info = new ProcessStartInfo(electron) { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true };
                info.ArgumentList.Add(target);
                Process.Start(info);
            }
            else Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }

        // Prefer the current library and import the legacy library only when the current file is absent.
        // 优先读取当前库，仅在当前文件缺失时导入旧库。
        private static KnotJotSkinLibrary ReadLibraryWithLegacyMigration(string file)
        {
            if (File.Exists(file)) return ReadLibrary(file, UiSkinProtocol.Current.Format);
            string legacy = LegacyThoughtCanvasLibraryPath();
            return File.Exists(legacy) ? ReadLibrary(legacy, "thoughtcanvas-ui-skins", allowMissingFormat: true) : new KnotJotSkinLibrary();
        }

        // Validate library size, format, and record limits before returning its deserialized data.
        // 返回反序列化数据前验证皮肤库大小、格式与记录数量限制。
        private static KnotJotSkinLibrary ReadLibrary(string file, string expectedFormat, bool allowMissingFormat = false)
        {
            try
            {
                if (new FileInfo(file).Length > UiSkinProtocol.Current.Limits.LibraryBytes) throw new InvalidDataException("现有 UI 皮肤库超过 16 MB 上限。");
                return ValidateLibraryPayload(File.ReadAllText(file), expectedFormat, allowMissingFormat);
            }
            catch (JsonException ex) { throw new InvalidDataException("现有 UI 皮肤库不是合法 JSON；已保留原文件。", ex); }
        }

        // Apply identical reader/writer limits to a complete library payload without changing any files.
        // 对完整皮肤库数据执行一致的读写限制，不改变任何文件。
        private static KnotJotSkinLibrary ValidateLibraryPayload(string text, string expectedFormat, bool allowMissingFormat = false)
        {
            if (Encoding.UTF8.GetByteCount(text) > UiSkinProtocol.Current.Limits.LibraryBytes) throw new InvalidDataException("现有 UI 皮肤库超过 16 MB 上限。");
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.TryGetProperty("format", out var format))
            {
                if (format.GetString() != expectedFormat) throw new InvalidDataException("现有 UI 皮肤库标识不兼容。");
            }
            else if (!allowMissingFormat) throw new InvalidDataException("现有 UI 皮肤库缺少格式标识。");
            var result = JsonSerializer.Deserialize<KnotJotSkinLibrary>(text, Options) ?? new KnotJotSkinLibrary();
            result.Skins = result.Skins ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            if (result.Skins.Count > UiSkinProtocol.Current.Limits.SkinCount) throw new InvalidDataException("现有 UI 皮肤数量超过 500 个上限。");
            result.Format = UiSkinProtocol.Current.Format;
            return result;
        }


        // Resolve the legacy skin-library path from an override or historical profile location.
        // 从覆盖变量或历史配置位置解析旧皮肤库路径。
        private static string LegacyThoughtCanvasLibraryPath()
        {
            var custom = Environment.GetEnvironmentVariable("THOUGHTCANVAS_UI_SKINS_FILE");
            return string.IsNullOrWhiteSpace(custom)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ThoughtCanvas", "ui-skins.json")
                : Path.GetFullPath(custom);
        }

        // Retain a source-project path only when it names an existing supported project.
        // 仅在路径指向已存在且受支持的工程时保留来源文件。
        private static string ExistingSourceFile(string sourceFile)
        {
            if (string.IsNullOrWhiteSpace(sourceFile)) return null;
            string full = Path.GetFullPath(sourceFile);
            return File.Exists(full) ? full : null;
        }

        // Normalize and validate the writable-library path returned by the application handshake.
        // 规范并验证程序握手返回的可写皮肤库路径。
        private static string ValidateLibraryPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException("KnotJot 没有返回皮肤库路径。");
            string full = Path.GetFullPath(value);
            if (!Path.GetFileName(full).Equals(UiSkinProtocol.Current.FileName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("KnotJot 返回的皮肤库文件名无效。");
            return full;
        }

        // Trim and bound the skin display name, supplying a default for empty input.
        // 清理并限制皮肤显示名称长度，为空输入提供默认值。
        private static string SafeName(string value)
        {
            value = (value ?? "自定义 UI").Replace("\0", "").Replace("\r", " ").Replace("\n", " ").Trim();
            if (value.Length > 60) value = value.Substring(0, 60);
            return string.IsNullOrWhiteSpace(value) ? "自定义 UI" : value;
        }
    }
}
