using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Text;
using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;

namespace KnotJotTextStyleEditor;
// EN: Share the current language with model-derived display labels and persistence diagnostics.
// ZH: 向模型显示名称及持久化诊断共享当前语言。
public static class UiState
{
    public static bool English { get; set; }
}

// EN: Centralize character, preview-dimension, and encoded-image limits shared by validation and rendering.
// ZH: 集中定义校验与渲染共用的字符数、预览尺寸和编码图片上限。
public static class ProductLimits
{
    public const int MaximumCharacters = 10_000;
    public const double MaximumPreviewDimension = 3_000;
    public const int MaximumEncodedImageBytes = 24 * 1024 * 1024;
}

// EN: Persist the v1 library envelope, optimistic revision, writer identity, style settings, and ordered style entries.
// ZH: 保存第一版样式库封装、乐观版本、写入者标识、样式设置及有序样式条目。
public sealed class StyleLibrary
{
    public string Format { get; set; } = "knotjot-text-styles";
    public int Version { get; set; } = 1;
    public long Revision { get; set; }
    public string WriterId { get; set; } = "";
    public string DefaultStyleId { get; set; } = "classic";
    public Dictionary<string, StyleSetting> StyleSettings { get; set; } = [];
    public List<TextBoxStyle> Styles { get; set; } = [];
}

// EN: Associate a style ID with its application scope in the library settings dictionary.
// ZH: 在样式库设置字典中将样式 ID 关联到应用范围。
public sealed class StyleSetting
{
    public string Scope { get; set; } = "all";
}

// EN: Represent one portable text-box composition: legacy frame fields, ordered layers, exactly one text region, input rules, and sizing.
// ZH: 表示一个可移植文本框构图：旧外框字段、有序图层、唯一文字区域、输入规则及尺寸策略。
public sealed class TextBoxStyle
{
    public string Id { get; set; } = "custom-style";
    public string Name { get; set; } = "自定义样式";
    public string Scope { get; set; } = "all";
    public bool ReplaceFrame { get; set; } = true;
    public string Bg { get; set; } = "#FFFFFF";
    public string Border { get; set; } = "#5B8DEF";
    public string Color { get; set; } = "#2C3140";
    public double Radius { get; set; } = 12;
    public double BorderWidth { get; set; } = 1.5;
    public string Shadow { get; set; } = "0 5px 16px #17203322";
    public string FontFamily { get; set; } = "Microsoft YaHei";
    public double FontSize { get; set; } = 14;
    public double FontWeight { get; set; } = 400;
    public string Padding { get; set; } = "11px 18px";
    public List<StyleLayer> Layers { get; set; } = [];
    public TextRegion TextRegion { get; set; } = new();
    public InputRules TextRules { get; set; } = new();
    public TextSizing TextSizing { get; set; } = new();
}

// EN: Store percentage geometry and visual/effect attributes; line endpoint direction and embedded image data survive JSON round trips.
// ZH: 保存百分比几何及外观效果属性；直线端点方向和内嵌图片可随 JSON 往返保存。
public sealed class StyleLayer
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; }
    public string Type { get; set; } = "rect";
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? X1 { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Y1 { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? X2 { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Y2 { get; set; }
    public string Fill { get; set; } = "#DDE9FF";
    public string Stroke { get; set; } = "#5B8DEF";
    public double StrokeWidth { get; set; } = 1;
    public double Radius { get; set; }
    public double Opacity { get; set; } = 1;
    public double Rotation { get; set; }
    public string BlendMode { get; set; } = "normal";
    public string ClipMode { get; set; } = "none";
    public string MaskMode { get; set; } = "none";
    public string ImageData { get; set; } = "";

    // EN: Localize the layer visibility toggle without serializing UI-only text.
    // ZH: 本地化图层显示开关提示，并排除界面文字的序列化。
    [JsonIgnore]
    public string VisibilityToolTip => UiState.English ? "Show/hide" : "显示/隐藏";

    // EN: Localize the layer lock toggle without adding fields to saved styles.
    // ZH: 本地化图层锁定开关提示，不向保存样式添加字段。
    [JsonIgnore]
    public string LockToolTip => UiState.English ? "Lock" : "锁定";

    // EN: Compose the localized layer type/name and clip/mask badges for the layer list.
    // ZH: 组合本地化图层类型或名称及剪切蒙版标记，用于图层列表。
    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            var type = Type == "rect" ? (UiState.English ? "Rectangle" : "矩形") : Type == "ellipse" ? (UiState.English ? "Ellipse" : "椭圆") : Type == "line" ? (UiState.English ? "Line" : "直线") : Type == "image" ? (UiState.English ? "Image" : "图片") : Type;
            var name = string.IsNullOrWhiteSpace(Name) ? type : Name;
            return $"{name}{(ClipMode != "none" ? (UiState.English ? " · Clip" : " · 剪切") : "")}{(MaskMode != "none" ? (UiState.English ? " · Mask" : " · 蒙版") : "")}";
        }
    }
}

// EN: Store the unique editable input rectangle in canvas percentages; display/lock metadata accompanies its bounds.
// ZH: 以画布百分比保存唯一可编辑输入矩形，并附带可见性与锁定数据。
public sealed class TextRegion
{
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }

    // EN: Localize the unique text region's visibility toggle without persisting the tooltip.
    // ZH: 本地化唯一文字区域的显示提示，不保存该提示。
    [JsonIgnore]
    public string VisibilityToolTip => UiState.English ? "Show/hide" : "显示/隐藏";

    // EN: Localize the text-region lock tooltip independently of stored geometry.
    // ZH: 本地化文字区域锁定提示，与保存的几何数据分离。
    [JsonIgnore]
    public string LockToolTip => UiState.English ? "Lock" : "锁定";

    // EN: Return the localized fixed name of the one editable text region.
    // ZH: 返回唯一可编辑文字区域的本地化固定名称。
    [JsonIgnore]
    public string DisplayName => UiState.English ? "Text input region" : "文字输入区域";
}

// EN: Describe Unicode scalar count, allowed input type, anchored regex pattern, and required completion state.
// ZH: 描述 Unicode 标量计数、允许输入类型、完整正则模式及必填完成态。
public sealed class InputRules
{
    public int MaxLength { get; set; }
    public string Type { get; set; } = "any";
    public string Pattern { get; set; } = "";
    public bool Required { get; set; }
}

// EN: Keep legacy width/height fields for compatibility; mode and authored aspect drive current measured preview growth.
// ZH: 保留旧宽高字段用于兼容；模式与设计比例控制当前实测预览增长。
public sealed class TextSizing
{
    public string Mode { get; set; } = "uniform";
    public double Width { get; set; } = 220;
    public double Height { get; set; } = 96;
    public double MaxWidth { get; set; } = 360;
    public double Aspect { get; set; } = 1.8;
}

public static class TextSizingModes
{
    public const string Uniform = "uniform";
    public const string Stretch = "stretch";
    // EN: Earlier files used auto/fixed; only stretch remains distinct from the uniform fallback.
    // ZH: 旧文件曾使用 auto/fixed；现在仅 stretch 与统一的等比回退策略区分。
    // EN: Keep stretch explicitly; migrate legacy, unknown, or missing sizing modes to uniform.
    // ZH: 明确保留 stretch，其余旧值、未知值或缺失模式迁移为 uniform。
    public static string Normalize(string? mode) => string.Equals(mode, Stretch, StringComparison.OrdinalIgnoreCase) ? Stretch : Uniform;
}

public static class InputRuleValidator
{
    // EN: Count Unicode scalar values instead of UTF-16 code units to match shared input-rule vectors.
    // ZH: 按 Unicode 标量值而非 UTF-16 码元计数，以匹配共享输入规则向量。
    public static int CountCharacters(string? value) => (value ?? "").EnumerateRunes().Count();
    // EN: Compile an anchored whole-value regex with a 100 ms timeout and return syntax diagnostics.
    // ZH: 编译带完整值锚点和一百毫秒超时的正则，并返回语法诊断。
    public static bool IsPatternValid(string? pattern, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(pattern))
            return true;
        try
        {
            _ = new Regex($"\\A(?:{pattern})\\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            return true;
        }
        catch (Exception ex)when (ex is ArgumentException or RegexMatchTimeoutException)
        {
            error = ex.Message;
            return false;
        }
    }

    // EN: Check character-count limits and whole-value type rules while allowing empty non-regex editing states.
    // ZH: 检查字符数上限及完整内容类型规则，同时允许非正则输入的空编辑状态。
    public static bool IsAllowed(string? value, InputRules? rules)
    {
        value ??= "";
        rules ??= new InputRules();
        if (rules.MaxLength > 0 && CountCharacters(value) > rules.MaxLength)
            return false;
        if (rules.Type == "regex" && !string.IsNullOrWhiteSpace(rules.Pattern))
            return MatchesWholeValue(value, rules.Pattern);
        if (value.Length == 0)
            return true;
        return rules.Type switch
        {
            "number" => Regex.IsMatch(value, "^[0-9]*$", RegexOptions.CultureInvariant),
            "letter" => Regex.IsMatch(value, "^[A-Za-z]*$", RegexOptions.CultureInvariant),
            "chinese" => Regex.IsMatch(value, "^[\\u3400-\\u9FFF]*$", RegexOptions.CultureInvariant),
            "alnum" => Regex.IsMatch(value, "^[A-Za-z0-9]*$", RegexOptions.CultureInvariant),
            _ => true
        };
    }

    // EN: Add required/nonblank validation to the editing-time rules, including empty-value regex checks.
    // ZH: 在编辑期规则上增加必填非空白检查，并校验空值是否符合正则。
    public static bool IsCompleteValueValid(string? value, InputRules? rules)
    {
        value ??= "";
        rules ??= new InputRules();
        if (rules.Required && string.IsNullOrWhiteSpace(value))
            return false;
        if (value.Length == 0 && rules.Type == "regex" && !string.IsNullOrWhiteSpace(rules.Pattern))
            return MatchesWholeValue(value, rules.Pattern);
        return IsAllowed(value, rules);
    }

    // EN: Match the entire value with bounded regex execution, rejecting malformed or timed-out patterns.
    // ZH: 以有时限的正则匹配完整内容，拒绝语法错误或超时的模式。
    static bool MatchesWholeValue(string value, string pattern)
    {
        try
        {
            return Regex.IsMatch(value, $"\\A(?:{pattern})\\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}

// EN: Carry finite measured width and height between pure sizing and WPF measurement.
// ZH: 在纯尺寸计算与 WPF 测量之间传递有限宽高。
public readonly record struct PreviewDimensions(double Width, double Height);
// EN: Return the encoded data URL together with actual decoded dimensions for aspect-preserving placement.
// ZH: 返回编码数据 URL 及实际解码尺寸，用于保持比例放置。
public readonly record struct ImportedImageData(string DataUrl, int PixelWidth, int PixelHeight);
public static class ImageImportPipeline
{
    static readonly int[] DecodeLimits = [4096, 3072, 2048, 1536, 1024, 768, 512, 384, 256];
    // EN: Decode supported raster files, reduce size/quality until encoded bytes fit the cap, and emit an embedded PNG or JPEG data URL.
    // ZH: 解码支持的位图，逐步降低尺寸或质量以满足编码字节上限，并输出内嵌 PNG 或 JPEG 数据 URL。
    public static ImportedImageData Encode(string file)
    {
        var extension = Path.GetExtension(file).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp"))
            throw new InvalidDataException(UiState.English ? "Unsupported image format" : "不支持的图片格式");
        var header = BitmapDecoder.Create(new Uri(file), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        var originalWidth = Math.Max(1, header.PixelWidth);
        var originalHeight = Math.Max(1, header.PixelHeight);
        var preserveAlpha = extension is ".png" or ".gif";
        byte[]? encoded = null;
        BitmapSource? finalBitmap = null;
        foreach (var limit in DecodeLimits)
        {
            var scale = Math.Min(1, limit / (double)Math.Max(originalWidth, originalHeight));
            var targetWidth = Math.Max(1, (int)Math.Round(originalWidth * scale));
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            bitmap.UriSource = new Uri(file);
            if (targetWidth < originalWidth)
                bitmap.DecodePixelWidth = targetWidth;
            bitmap.EndInit();
            bitmap.Freeze();
            foreach (var quality in preserveAlpha ? new[]
            {
                100
            }

            : new[]
            {
                92,
                84,
                76,
                68,
                58,
                48
            }

            )
            {
                BitmapEncoder encoder = preserveAlpha ? new PngBitmapEncoder() : new JpegBitmapEncoder
                {
                    QualityLevel = quality
                };
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = new MemoryStream();
                encoder.Save(stream);
                encoded = stream.ToArray();
                finalBitmap = bitmap;
                if (encoded.Length <= ProductLimits.MaximumEncodedImageBytes)
                    break;
            }

            if (encoded is { Length: <= ProductLimits.MaximumEncodedImageBytes })
                break;
        }

        if (encoded == null || finalBitmap == null)
            throw new InvalidDataException(UiState.English ? "Image could not be decoded" : "图片无法解码");
        if (encoded.Length > ProductLimits.MaximumEncodedImageBytes)
            throw new InvalidDataException(UiState.English ? $"Image remains {encoded.Length / 1024d / 1024d:0.0} MB after compression; the limit is {ProductLimits.MaximumEncodedImageBytes / 1024 / 1024} MB" : $"图片压缩后仍有 {encoded.Length / 1024d / 1024d:0.0} MB，超过 {ProductLimits.MaximumEncodedImageBytes / 1024 / 1024} MB 上限");
        var mime = preserveAlpha ? "image/png" : "image/jpeg";
        return new($"data:{mime};base64,{Convert.ToBase64String(encoded)}", finalBitmap.PixelWidth, finalBitmap.PixelHeight);
    }
}

public static class PreviewSizingCalculator
{
    const double MaximumSafeDimension = ProductLimits.MaximumPreviewDimension;
    // EN: Convert measured one-character capacity and percentage text-region dimensions into a safe aspect-preserving baseline card.
    // ZH: 将实测单字容量及百分比文字区域尺寸转换为安全且保持比例的卡片基准。
    public static PreviewDimensions Calculate(TextBoxStyle style, string? text, PreviewDimensions? singleCharacterRegion = null)
    {
        var sizing = style.TextSizing ?? new TextSizing();
        var fallback = new PreviewDimensions(Math.Clamp(style.FontSize, 1, 500) + 16, Math.Clamp(style.FontSize, 1, 500) * 1.4 + 12);
        var glyph = singleCharacterRegion is { Width: > 0, Height: > 0 } measured && double.IsFinite(measured.Width) && double.IsFinite(measured.Height) ? measured : fallback;
        var targetRegionWidth = Math.Clamp(glyph.Width, 1, MaximumSafeDimension);
        var targetRegionHeight = Math.Clamp(glyph.Height, 1, MaximumSafeDimension);
        var regionWidth = style.TextRegion.W;
        var regionHeight = style.TextRegion.H;
        var widthFraction = double.IsFinite(regionWidth) && regionWidth > 0 ? Math.Clamp(regionWidth / 100, .001, 1) : 1;
        var heightFraction = double.IsFinite(regionHeight) && regionHeight > 0 ? Math.Clamp(regionHeight / 100, .001, 1) : 1;
        var aspect = Math.Clamp(double.IsFinite(sizing.Aspect) ? sizing.Aspect : 1.8, .01, 100);
        // EN: Both modes start from the authored composition; independent-axis growth begins only after overflow.
        // ZH: 两种模式均从设计构图开始，仅在文字溢出后才分别扩展两条轴。
        var baseWidth = Math.Max(targetRegionWidth / widthFraction, targetRegionHeight * aspect / heightFraction);
        var baseHeight = baseWidth / aspect;
        var safeScale = Math.Min(1, Math.Min(MaximumSafeDimension / baseWidth, MaximumSafeDimension / baseHeight));
        baseWidth *= safeScale;
        baseHeight *= safeScale;
        // EN: Empty and one-character text share a baseline; MainWindow measures longer content using real fonts.
        // ZH: 空内容和单字共用基准；MainWindow 使用真实字体测量更长文字。
        return new(baseWidth, baseHeight);
    }
}

public static class ModelSafety
{
    // EN: Replace NaN or infinity with the field's domain-specific fallback before clamping.
    // ZH: 限制范围前，将非数字或无穷大替换为该字段的备用值。
    static double Finite(double value, double fallback) => double.IsFinite(value) ? value : fallback;
    // EN: Admit only bounded embedded raster data-URL prefixes; actual base64 decoding happens in the renderer.
    // ZH: 仅允许长度受限的内嵌位图数据 URL 前缀；实际 base64 解码由渲染器处理。
    public static bool IsSupportedImageData(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 36_000_000)
            return false;
        return value.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase) || value.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase) || value.StartsWith("data:image/gif;base64,", StringComparison.OrdinalIgnoreCase) || value.StartsWith("data:image/bmp;base64,", StringComparison.OrdinalIgnoreCase);
    }

    // EN: Repair missing nested objects, clamp non-finite geometry and limits, migrate line endpoints, and clear unsupported image payloads.
    // ZH: 修复缺失嵌套对象、约束非有限几何与上限、迁移直线端点，并清空不支持的图片数据。
    public static void Normalize(TextBoxStyle s)
    {
        s.Layers ??= [];
        s.TextRegion ??= new();
        s.TextRules ??= new();
        s.TextSizing ??= new();
        s.Radius = Math.Clamp(Finite(s.Radius, 12), 0, 500);
        s.BorderWidth = Math.Clamp(Finite(s.BorderWidth, 1.5), 0, 100);
        s.FontSize = Math.Clamp(Finite(s.FontSize, 14), 1, 500);
        s.FontWeight = Math.Clamp(Finite(s.FontWeight, 400), 1, 1000);
        foreach (var l in s.Layers)
        {
            // EN: Reject null layer records before field access so callers can preserve corrupt library bytes and report structured validation errors.
            // ZH: 访问字段前拒绝空图层记录，使调用方保留损坏原字节并报告结构化校验错误。
            if (l == null) throw new System.IO.InvalidDataException("layers contains a null record / 图层数组包含空记录");
            l.X = Math.Clamp(Finite(l.X, 0), 0, 100);
            l.Y = Math.Clamp(Finite(l.Y, 0), 0, 100);
            l.W = Math.Clamp(Finite(l.W, 1), .01, Math.Max(.01, 100 - l.X));
            l.H = Math.Clamp(Finite(l.H, 1), .01, Math.Max(.01, 100 - l.Y));
            if (l.Type == "line")
            {
                var x1 = Math.Clamp(Finite(l.X1 ?? l.X, l.X), 0, 100);
                var y1 = Math.Clamp(Finite(l.Y1 ?? l.Y, l.Y), 0, 100);
                var x2 = Math.Clamp(Finite(l.X2 ?? (l.X + l.W), l.X + l.W), 0, 100);
                var y2 = Math.Clamp(Finite(l.Y2 ?? (l.Y + l.H), l.Y + l.H), 0, 100);
                l.X1 = x1;
                l.Y1 = y1;
                l.X2 = x2;
                l.Y2 = y2;
                l.X = Math.Min(x1, x2);
                l.Y = Math.Min(y1, y2);
                l.W = Math.Max(.01, Math.Abs(x2 - x1));
                l.H = Math.Max(.01, Math.Abs(y2 - y1));
            }

            l.StrokeWidth = Math.Clamp(Finite(l.StrokeWidth, 1.5), 0, 100);
            l.Radius = Math.Clamp(Finite(l.Radius, 0), 0, 500);
            l.Opacity = Math.Clamp(Finite(l.Opacity, 1), 0, 1);
            l.Rotation = Math.Clamp(Finite(l.Rotation, 0), -36000, 36000);
            if (l.Type == "image" && !IsSupportedImageData(l.ImageData))
                l.ImageData = "";
        }

        var t = s.TextRegion;
        t.X = Math.Clamp(Finite(t.X, 0), 0, 100);
        t.Y = Math.Clamp(Finite(t.Y, 0), 0, 100);
        t.W = Math.Clamp(Finite(t.W, 0), 0, Math.Max(0, 100 - t.X));
        t.H = Math.Clamp(Finite(t.H, 0), 0, Math.Max(0, 100 - t.Y));
        var z = s.TextSizing;
        z.Mode = TextSizingModes.Normalize(z.Mode);
        z.Width = Math.Clamp(Finite(z.Width, 220), 1, ProductLimits.MaximumPreviewDimension);
        z.Height = Math.Clamp(Finite(z.Height, 96), 1, ProductLimits.MaximumPreviewDimension);
        z.MaxWidth = Math.Clamp(Finite(z.MaxWidth, 360), 1, ProductLimits.MaximumPreviewDimension);
        z.Aspect = Math.Clamp(Finite(z.Aspect, 1.8), .01, 100);
        s.TextRules.MaxLength = Math.Clamp(s.TextRules.MaxLength, 0, ProductLimits.MaximumCharacters);
    }
}

public static class StyleIdentity
{
    // EN: Allocate a case-insensitive unique ID across supplied libraries using a numeric suffix starting at two.
    // ZH: 在给定样式库中分配不区分大小写的唯一 ID，从数字后缀二开始。
    public static string UniqueId(string requested, IEnumerable<StyleLibrary> libraries)
    {
        var baseId = string.IsNullOrWhiteSpace(requested) ? "custom-style" : requested;
        var candidate = baseId;
        var suffix = 2;
        // EN: Check an ID against all styles in all supplied libraries with case-insensitive comparison.
        // ZH: 不区分大小写地检查 ID 是否存在于所有传入样式库的条目中。
        bool Exists(string id) => libraries.Any( /* EN: Search each candidate library for the requested ID. ZH: 在每个候选样式库中搜索请求 ID。 */lib => lib.Styles.Any( /* EN: Compare IDs case-insensitively across library entries. ZH: 不区分大小写比较库条目 ID。 */s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase)));
        while (Exists(candidate))
            candidate = baseId + "-" + suffix++;
        return candidate;
    }
}
