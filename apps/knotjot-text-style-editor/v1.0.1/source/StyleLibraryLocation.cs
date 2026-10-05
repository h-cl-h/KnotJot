using System.IO;
using System.Text.Json;

namespace KnotJotTextStyleEditor;

// EN: Resolve the stable main-app library contract for source trees and typed executable responses without modifying either target.
// ZH: 解析源码树及可执行程序类型化响应的稳定主程序样式库契约，不修改连接目标。
public static class StyleLibraryLocation
{
    // EN: Prefer an explicit portable override, then the refactored resources directory; retain existing legacy sibling layouts.
    // ZH: 优先使用明确便携目录，其次使用重构后的资源目录，并保留已有旧版同级布局。
    public static string ForSource(string target)
    {
        var portable = Environment.GetEnvironmentVariable("KNOTJOT_TEXT_STYLES_DIR");
        if (!string.IsNullOrWhiteSpace(portable)) return Path.GetFullPath(Path.Combine(portable, "custom-text-styles.json"));
        var root = Path.GetDirectoryName(Path.GetFullPath(target))!;
        var resources = Path.Combine(root, "resources", "text-styles");
        var legacy = Path.Combine(root, "text-styles");
        var directory = Directory.Exists(resources) || Directory.Exists(Path.Combine(root, "resources")) || !Directory.Exists(legacy) ? resources : legacy;
        return Path.Combine(directory, "custom-text-styles.json");
    }

    // EN: Parse numeric protocol version 1 and an absolute file path, rejecting unrelated JSON objects, directories and unsupported responses.
    // ZH: 解析数字版本 1 及绝对文件路径，拒绝无关 JSON 对象、目录及不支持的响应。
    public static string ParseReply(string text)
    {
        var response = JsonSerializer.Deserialize<LocationReply>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (response?.Format != "knotjot-text-styles-location" || response.Version != 1 || string.IsNullOrWhiteSpace(response.File) || !Path.IsPathFullyQualified(response.File) || Path.EndsInDirectorySeparator(response.File) || Directory.Exists(response.File))
            throw new InvalidDataException("Invalid KnotJot style-library location response / 主程序返回的样式库位置响应无效");
        return Path.GetFullPath(response.File);
    }

    // EN: Keep the executable handshake's numeric version and textual fields typed independently.
    // ZH: 独立保留可执行程序握手的数字版本及文本字段类型。
    sealed class LocationReply
    {
        public string? Format { get; set; }
        public int Version { get; set; }
        public string? File { get; set; }
    }
}
