using System;
using System.IO;
using System.Text.Json;
using System.Text;

namespace KnotJotUiEditor
{
    /// <summary>在窗口状态改变前完成文件大小、JSON 深度与工程复杂度检查。</summary>
    public static class ProjectFileService
    {
        public const long MaxFileBytes = 32L * 1024 * 1024;
        public const int MaxDesigns = 128;
        public const int MaxElementsPerDesign = 1000;
        public const int MaxTotalElements = 5000;
        public const int MaxImages = 256;
        public const int MaxImageChars = 12 * 1024 * 1024;
        public const int MaxImageBytes = MaxImageChars / 4 * 3;
        public const int MaxCssBytes = 8 * 1024 * 1024;
        public const int MaxTotalImageChars = 24 * 1024 * 1024;
        public const int MaxTextChars = 10000;
        public const int MaxInkPoints = 100000;
        public const int MaxJsonNodes = 250000;

        // Check file size, parse bounded-depth JSON, and validate complexity before returning a document.
        // 检查文件大小、解析有深度限制的 JSON，并在返回文档前验证复杂度。
        public static JsonDocument Open(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("工程路径不能为空。", nameof(path));
            var info = new FileInfo(path);
            if (!info.Exists) throw new FileNotFoundException("工程文件不存在。", path);
            if (info.Length > MaxFileBytes)
                throw new InvalidDataException("工程文件大小为 " + FormatBytes(info.Length) + "，超过 32 MB 上限；文件尚未读取。");

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 64
                });
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("工程 JSON 无效（行 " + (ex.LineNumber ?? 0) + "，字节位置 " + (ex.BytePositionInLine ?? 0) + "）。", ex);
            }

            try { Validate(doc.RootElement); return doc; }
            catch { doc.Dispose(); throw; }
        }

        // Validate exact UTF-8 output with the same depth, size, and resource rules used by Open.
        // 按 Open 相同的深度、大小与资源规则校验确切 UTF-8 输出。
        public static void ValidatePayload(string json)
        {
            if (Encoding.UTF8.GetByteCount(json) > MaxFileBytes) throw new InvalidDataException("工程文件超过 32 MB 上限。");
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
            Validate(doc.RootElement);
        }

        // Accept canonical raw Base64 only, bounded both before and after decoding; data URLs are not this field's format.
        // 仅接受规范的原始 Base64，并限制编码和解码大小；此字段不接受 data URL。
        public static void ValidateImageData(string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (value.Length > MaxImageChars || value.Length % 4 != 0) throw new InvalidDataException("图片 Base64 超过 12 MB 或编码无效。");
            byte[] data;
            try { data = Convert.FromBase64String(value); }
            catch (FormatException ex) { throw new InvalidDataException("图片 Base64 编码无效。", ex); }
            if (data.Length > MaxImageBytes || Convert.ToBase64String(data) != value) throw new InvalidDataException("图片编码不规范或解码大小超过 9 MB。");
        }

        // Validate root shape and aggregate design, element, image, and text resource limits.
        // 验证根结构，以及设计、元素、图片与文本资源的总量限制。
        public static void Validate(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("工程根节点必须是 JSON 对象。");
            int nodes = 0, totalElements = 0, images = 0, totalImageChars = 0;
            CountNodes(root, "$", null, ref nodes, ref images, ref totalImageChars);
            if (nodes > MaxJsonNodes) throw new InvalidDataException("工程 JSON 节点超过 " + MaxJsonNodes + " 个上限。");

            if (root.TryGetProperty("designs", out var designs))
            {
                if (designs.ValueKind != JsonValueKind.Object) throw new InvalidDataException("字段 $.designs 必须是对象。");
                int count = 0;
                foreach (var design in designs.EnumerateObject())
                {
                    count++;
                    if (count > MaxDesigns) throw new InvalidDataException("字段 $.designs 超过 " + MaxDesigns + " 个设计目标上限。");
                    totalElements += ValidateElementContainer(design.Value, "$.designs." + design.Name);
                }
            }
            if (root.TryGetProperty("project", out var project) && project.ValueKind == JsonValueKind.Object)
            {
                totalElements += ValidateElementArray(project, "elements", "$.project.elements");
                totalElements += ValidateElementArray(project, "shapes", "$.project.shapes");
            }
            if (totalElements > MaxTotalElements) throw new InvalidDataException("工程元素总数为 " + totalElements + "，超过 " + MaxTotalElements + " 个上限。");
            if (images > MaxImages) throw new InvalidDataException("工程图片数量为 " + images + "，超过 " + MaxImages + " 张上限。");
            if (totalImageChars > MaxTotalImageChars) throw new InvalidDataException("工程嵌入图片数据总量超过 24 MB 上限。");
        }

        // Require an object design container and count its bounded element array.
        // 要求设计容器为对象，并统计受限的元素数组。
        private static int ValidateElementContainer(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.Object) throw new InvalidDataException("字段 " + path + " 必须是对象。");
            return ValidateElementArray(value, "elements", path + ".elements");
        }

        // Validate an optional element array and enforce the per-design element limit.
        // 验证可选元素数组，并执行单设计元素数量限制。
        private static int ValidateElementArray(JsonElement owner, string property, string path)
        {
            if (!owner.TryGetProperty(property, out var array)) return 0;
            if (array.ValueKind != JsonValueKind.Array) throw new InvalidDataException("字段 " + path + " 必须是数组。");
            int count = array.GetArrayLength();
            if (count > MaxElementsPerDesign) throw new InvalidDataException("字段 " + path + " 含 " + count + " 个元素，超过单设计 " + MaxElementsPerDesign + " 个上限。");
            return count;
        }

        // Traverse JSON recursively to bound total nodes, ink points, strings, and embedded images.
        // 递归遍历 JSON，限制总节点数、手绘点、字符串与嵌入图片。
        private static void CountNodes(JsonElement value, string path, string propertyName, ref int nodes, ref int images, ref int totalImageChars)
        {
            nodes++;
            if (nodes > MaxJsonNodes) return;
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var p in value.EnumerateObject()) CountNodes(p.Value, path + "." + p.Name, p.Name, ref nodes, ref images, ref totalImageChars);
                    break;
                case JsonValueKind.Array:
                    if (propertyName == "points" && value.GetArrayLength() > MaxInkPoints)
                        throw new InvalidDataException("字段 " + path + " 超过 " + MaxInkPoints + " 个手绘点上限。");
                    int i = 0;
                    foreach (var item in value.EnumerateArray()) CountNodes(item, path + "[" + i++ + "]", propertyName, ref nodes, ref images, ref totalImageChars);
                    break;
                case JsonValueKind.String:
                    int length = (value.GetString() ?? "").Length;
                    if (propertyName == "base64")
                    {
                        images++; totalImageChars += length;
                        ValidateImageData(value.GetString());
                    }
                    else if (propertyName == "css")
                    {
                        if (Encoding.UTF8.GetByteCount(value.GetString() ?? "") > MaxCssBytes)
                            throw new InvalidDataException("字段 " + path + " 超过 8 MB CSS 上限。");
                    }
                    else if (propertyName == "text" && length > MaxTextChars)
                        throw new InvalidDataException("字段 " + path + " 超过 " + MaxTextChars + " 个字符上限。");
                    else if (length > 100000)
                        throw new InvalidDataException("字段 " + path + " 超过 100000 个字符上限。");
                    break;
            }
        }

        // Format file size in rounded megabytes for validation error messages.
        // 将文件大小格式化为四舍五入的 MB 值，用于验证错误信息。
        private static string FormatBytes(long value)
        {
            return Math.Round(value / 1024d / 1024d, 2) + " MB";
        }
    }
}
