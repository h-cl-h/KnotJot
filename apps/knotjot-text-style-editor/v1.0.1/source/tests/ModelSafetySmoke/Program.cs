using System.Text.Json;
using System.IO;
using KnotJotTextStyleEditor;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// EN: Verify current, legacy, and raw JSON suffix compatibility while rejecting the UI-editor suffix.
// ZH: 验证当前、旧版及原始 JSON 后缀兼容，同时拒绝界面编辑器后缀。
if (!StyleFileExtensions.IsSupported("legacy.bmaptextstyle"))
    throw new Exception("legacy style extension is not supported");
if (!StyleFileExtensions.IsSupported("new.knotjot-textstyle"))
    throw new Exception("new style extension is not supported");
if (!StyleFileExtensions.IsSupported("portable.json"))
    throw new Exception("raw JSON style is not supported");
if (StyleFileExtensions.IsSupported("wrong.knotjot-ui"))
    throw new Exception("UI extension must be rejected");
if (StyleFileExtensions.DefaultExtension != ".knotjot-textstyle")
    throw new Exception("new style extension must be the save default");
// EN: Stress normalization of non-finite model values and verify the result is serializable.
// ZH: 压力测试非有限模型值的规范化，并验证结果可以序列化。
var style = new TextBoxStyle
{
    Radius = double.PositiveInfinity,
    FontSize = double.NaN,
    TextRegion = new TextRegion
    {
        X = double.NegativeInfinity,
        Y = double.NaN,
        W = double.PositiveInfinity,
        H = double.NegativeInfinity
    },
    TextSizing = new TextSizing
    {
        Width = double.PositiveInfinity,
        Height = double.NaN,
        MaxWidth = double.NegativeInfinity,
        Aspect = double.PositiveInfinity
    },
    Layers = [new StyleLayer
    {
        X = double.PositiveInfinity,
        Y = double.NaN,
        W = double.NegativeInfinity,
        H = double.PositiveInfinity,
        Opacity = double.NaN,
        Rotation = double.PositiveInfinity
    }

    ]
};
for (var i = 0; i < 10_000; i++)
    ModelSafety.Normalize(style);
var output = JsonSerializer.Serialize(style);
if (output.Contains("Infinity", StringComparison.OrdinalIgnoreCase) || output.Contains("NaN", StringComparison.OrdinalIgnoreCase))
    throw new Exception("非有限数值仍存在");
if (style.Layers.Any( /* EN: Detect any remaining non-finite layer geometry after normalization. ZH: 检测规范化后仍存在的非有限图层几何值。 */x => !double.IsFinite(x.X) || !double.IsFinite(x.Y) || !double.IsFinite(x.W) || !double.IsFinite(x.H)))
    throw new Exception("图层坐标未清洗");
// EN: Check embedded-image admission and percentage-to-card one-character baseline calculations.
// ZH: 检查内嵌图片准入及百分比到卡片的单字基准换算。
var validImage = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";
var imageStyle = new TextBoxStyle
{
    Layers = [new StyleLayer
    {
        Type = "image",
        ImageData = validImage
    }, new StyleLayer
    {
        Type = "image",
        ImageData = "https://example.invalid/a.png"
    }

    ]
};
ModelSafety.Normalize(imageStyle);
if (imageStyle.Layers[0].ImageData != validImage || imageStyle.Layers[1].ImageData.Length != 0)
    throw new Exception("图片数据安全清洗错误");
var measuredStyle = new TextBoxStyle
{
    TextRegion = new TextRegion
    {
        X = 10,
        Y = 10,
        W = 50,
        H = 25
    },
    TextSizing = new TextSizing
    {
        Mode = "uniform",
        Width = 999,
        Height = 777,
        Aspect = 2
    }
};
var uniform = PreviewSizingCalculator.Calculate(measuredStyle, "", new PreviewDimensions(30, 32));
if (Math.Abs(uniform.Width - 256) > .001 || Math.Abs(uniform.Height - 128) > .001)
    throw new Exception("固定比例没有按一个字的写字区域换算外框");
measuredStyle.TextSizing.Mode = "stretch";
var stretch = PreviewSizingCalculator.Calculate(measuredStyle, "", new PreviewDimensions(30, 32));
if (Math.Abs(stretch.Width - 256) > .001 || Math.Abs(stretch.Height - 128) > .001)
    throw new Exception("自由拉伸的最小外框没有保留画出的长宽比");
// EN: Generate a large local BMP and ensure the import encoder reduces it into a supported embedded payload.
// ZH: 生成大型本地 BMP 并验证导入编码器将其缩减为支持的内嵌数据。
var largeBmp = Path.Combine(Path.GetTempPath(), "bmap-large-import-" + Guid.NewGuid().ToString("N") + ".bmp");
try
{
    const int width = 2400, height = 2400, stride = width * 4;
    var pixels = new byte[stride * height];
    var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, pixels, stride);
    var encoder = new BmpBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(source));
    using (var stream = File.Create(largeBmp))
        encoder.Save(stream);
    if (new FileInfo(largeBmp).Length <= 20L * 1024 * 1024)
        throw new Exception("测试图片没有超过 20 MB");
    var imported = ImageImportPipeline.Encode(largeBmp);
    if (!ModelSafety.IsSupportedImageData(imported.DataUrl) || imported.PixelWidth <= 0 || imported.PixelHeight <= 0)
        throw new Exception("超过 20 MB 的图片没有成功转换");
}
finally
{
    if (File.Exists(largeBmp))
        // EN: Preserve reproducible image inputs when the audit explicitly requests retained test files.
        // ZH: 审查要求保留测试文件时，保留可复现的图片输入。
        if (Environment.GetEnvironmentVariable("KNOTJOT_KEEP_TEST_FILES") != "1") File.Delete(largeBmp);
}

// EN: Verify sequential collision suffixes for newly synchronized style identities.
// ZH: 验证新同步样式 ID 连续重名时的数字后缀。
var lib = new StyleLibrary
{
    Styles = [new TextBoxStyle
    {
        Id = "my-text-style",
        Name = "第一个"
    }

    ]
};
var second = StyleIdentity.UniqueId("my-text-style", [lib]);
if (second != "my-text-style-2")
    throw new Exception("第二个样式 ID 未自动分离");
lib.Styles.Add(new TextBoxStyle { Id = second, Name = "第二个" });
var third = StyleIdentity.UniqueId("my-text-style", [lib]);
if (third != "my-text-style-3")
    throw new Exception("第三个样式 ID 未自动分离");
// EN: Locate shared vectors by ascending from the test working directory and assert C#/JavaScript input-rule parity.
// ZH: 从测试工作目录向上查找共享向量，断言 C# 与 JavaScript 输入规则一致。
var search = new DirectoryInfo(Directory.GetCurrentDirectory());
string? vectorFile = null;
while (search != null)
{
    var candidate = Path.Combine(search.FullName, "shared", "text-input-rule-vectors.json");
    if (File.Exists(candidate))
    {
        vectorFile = candidate;
        break;
    }

    search = search.Parent;
}

if (vectorFile == null)
    throw new Exception("共享输入规则测试向量不存在");
using (var vectors = JsonDocument.Parse(File.ReadAllText(vectorFile)))
    foreach (var vector in vectors.RootElement.EnumerateArray())
    {
        var name = vector.GetProperty("name").GetString();
        var text = vector.GetProperty("text").GetString();
        var rules = JsonSerializer.Deserialize<InputRules>(vector.GetProperty("rules").GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var expected = vector.GetProperty("allowed").GetBoolean();
        if (InputRuleValidator.IsAllowed(text, rules) != expected)
            throw new Exception($"共享向量失败：{name}");
        if (vector.TryGetProperty("complete", out var complete) && InputRuleValidator.IsCompleteValueValid(text, rules) != complete.GetBoolean())
            throw new Exception($"共享完成态向量失败：{name}");
    }

// EN: Use an isolated library to verify revision increments, stale-write rejection, and preserved corrupt-file backups.
// ZH: 使用隔离样式库验证版本递增、拒绝过期写入及保留损坏文件备份。
var libraryRoot = Path.Combine(Path.GetTempPath(), "bmap-library-safety-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(libraryRoot);
var libraryFile = Path.Combine(libraryRoot, "custom-text-styles.json");
try
{
    var safeLibrary = new StyleLibrary
    {
        Styles = [new TextBoxStyle
        {
            Id = "safe-one",
            TextRegion = new TextRegion
            {
                W = 50,
                H = 50
            }
        }

        ]
    };
    var firstWrite = StyleLibraryStore.Write(libraryFile, safeLibrary, 0);
    if (firstWrite.Revision != 1 || StyleLibraryStore.Load(libraryFile).Library.Format != "knotjot-text-styles")
        throw new Exception("原子库写入没有设置格式或 revision");
    var stale = JsonSerializer.Deserialize<StyleLibrary>(File.ReadAllText(libraryFile), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    StyleLibraryStore.Write(libraryFile, safeLibrary, 1);
    var conflicted = false;
    try
    {
        StyleLibraryStore.Write(libraryFile, stale, 1);
    }
    catch (StyleLibraryException)
    {
        conflicted = true;
    }

    if (!conflicted)
        throw new Exception("外部 revision 冲突没有阻止覆盖");
    File.WriteAllText(libraryFile, "{\"format\":\"knotjot-text-styles\",\"styles\":[");
    StyleLibraryException? corrupt = null;
    try
    {
        StyleLibraryStore.Load(libraryFile, true);
    }
    catch (StyleLibraryException ex)
    {
        corrupt = ex;
    }

    if (corrupt?.BackupFile == null || !File.Exists(corrupt.BackupFile) || File.ReadAllText(libraryFile).Length == 0)
        throw new Exception("损坏库没有保留原文件和 corrupt 备份");
}
finally
{
    // EN: Retain the isolated library and corrupt/conflict fixtures for audit reproduction when requested.
    // ZH: 按要求保留隔离样式库以及损坏、冲突样本，以便复现审查。
    if (Environment.GetEnvironmentVariable("KNOTJOT_KEEP_TEST_FILES") != "1") Directory.Delete(libraryRoot, true);
}

// EN: Verify legacy bounding-box lines gain explicit endpoints without losing reverse endpoint direction.
// ZH: 验证旧边界框直线能补齐明确端点，并保留反向端点方向。
var lineStyle = new TextBoxStyle
{
    Layers = [new StyleLayer
    {
        Type = "line",
        X = 10,
        Y = 20,
        W = 30,
        H = 40
    }

    ]
};
ModelSafety.Normalize(lineStyle);
var line = lineStyle.Layers[0];
if (line.X1 != 10 || line.Y1 != 20 || line.X2 != 40 || line.Y2 != 60)
    throw new Exception("旧直线没有迁移为真实端点");
var slashStyle = new TextBoxStyle
{
    Layers = [new StyleLayer
    {
        Type = "line",
        X1 = 80,
        Y1 = 10,
        X2 = 20,
        Y2 = 90
    }

    ]
};
ModelSafety.Normalize(slashStyle);
if (slashStyle.Layers[0].X1 != 80 || slashStyle.Layers[0].X2 != 20)
    throw new Exception("反斜线端点方向丢失");
Console.WriteLine("MODEL_SAFETY_SMOKE_OK IMAGE_DATA_OK LARGE_IMAGE_IMPORT_OK ONE_CHARACTER_BASELINE_OK MULTI_STYLE_IDS_OK INPUT_RULE_PARITY_OK LIBRARY_ATOMIC_CORRUPT_CONFLICT_OK LINE_ENDPOINT_MIGRATION_OK");
