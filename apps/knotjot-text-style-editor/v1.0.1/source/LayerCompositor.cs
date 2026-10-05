using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace KnotJotTextStyleEditor;

// EN: Compose authored layer pixels in one common coordinate system; geometry stays in the model for independent hit testing and selection.
// ZH: 在统一坐标系中合成设计图层像素；模型保留几何信息，独立用于命中及选择。
public static class LayerCompositor
{
    const int MaximumPixels = 4_000_000;
    // EN: Bound raster memory to four million pixels, combine each adjacent cutter, then alpha-compose visible layers using their selected blend mode.
    // ZH: 将栅格内存限制为四百万像素，处理相邻切刀后，按指定混合模式及透明度合成可见图层。
    public static BitmapSource Render(IReadOnlyList<StyleLayer> layers, double width, double height, Func<StyleLayer, double, double, FrameworkElement> visual)
    {
        var scale = Math.Min(1, Math.Sqrt(MaximumPixels / Math.Max(1, width * height)));
        int w = Math.Max(1, (int)Math.Floor(width * scale)), h = Math.Max(1, (int)Math.Floor(height * scale));
        var output = new byte[w * h * 4];
        for (var i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            if (!layer.IsVisible || i > 0 && (layer.ClipMode != "none" || layer.MaskMode != "none")) continue;
            var pixels = Rasterize(layer, w, h, scale, visual);
            var cutter = i + 1 < layers.Count && layers[i + 1].IsVisible ? layers[i + 1] : null;
            if (cutter != null && (cutter.MaskMode != "none" || cutter.ClipMode != "none"))
            {
                var mask = Rasterize(cutter, w, h, scale, visual, cutter.MaskMode == "none");
                ApplyMask(pixels, mask, cutter.MaskMode, cutter.ClipMode);
            }
            Blend(output, pixels, layer.BlendMode);
        }
        var result = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, output, w * 4);
        result.Freeze(); return result;
    }

    // EN: Rasterize the entire transformed layer against transparency so source and target rotations remain independent; clip cutters use geometry only.
    // ZH: 将完整变换图层栅格化到透明背景，令源及目标旋转独立；几何剪切切刀只采用轮廓。
    static byte[] Rasterize(StyleLayer layer, int width, int height, double scale, Func<StyleLayer, double, double, FrameworkElement> visual, bool geometryOnly = false)
    {
        var item = layer;
        if (geometryOnly) item = new StyleLayer { Type = layer.Type == "image" ? "rect" : layer.Type, X = layer.X, Y = layer.Y, W = layer.W, H = layer.H, X1 = layer.X1, Y1 = layer.Y1, X2 = layer.X2, Y2 = layer.Y2, Radius = layer.Radius, Rotation = layer.Rotation, Fill = "#FFFFFF", Stroke = "#FFFFFF", StrokeWidth = layer.Type == "line" ? layer.StrokeWidth : 0 };
        var element = visual(item, width / 100d, height / 100d);
        if (element is Shape shape)
        {
            shape.Fill = item.Type == "line" ? Brushes.Transparent : CssBrush(item.Fill);
            shape.Stroke = CssBrush(item.Stroke);
            shape.StrokeThickness *= scale;
            if (shape is Rectangle rectangle) { rectangle.RadiusX *= scale; rectangle.RadiusY *= scale; }
        }
        var canvas = new Canvas { Width = width, Height = height, ClipToBounds = true };
        canvas.Children.Add(element); canvas.Measure(new Size(width, height)); canvas.Arrange(new Rect(0, 0, width, height));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(canvas);
        var pixels = new byte[width * height * 4]; bitmap.CopyPixels(pixels, width * 4, 0);
        return pixels;
    }

    // EN: Multiply all premultiplied target channels by sampled alpha or luminance; inversion includes transparent pixels outside a transformed mask.
    // ZH: 用采样透明度或亮度乘以目标所有预乘通道；反向模式包含变换蒙版外的透明像素。
    static void ApplyMask(byte[] target, byte[] mask, string mode, string clip)
    {
        bool luminance = mode is "luminance" or "inverseLuminance";
        bool inverse = mode is "inverseAlpha" or "inverseLuminance" || mode == "none" && clip == "subtract";
        for (var p = 0; p < target.Length; p += 4)
        {
            // Premultiplied RGB already includes image alpha, brush alpha and layer opacity. / 预乘 RGB 已包含图片透明度、画刷透明度和图层不透明度。
            double amount = luminance ? (.2126 * mask[p + 2] + .7152 * mask[p + 1] + .0722 * mask[p]) / 255 : mask[p + 3] / 255d;
            if (inverse) amount = 1 - amount;
            for (var c = 0; c < 4; c++) target[p + c] = (byte)Math.Round(target[p + c] * amount);
        }
    }

    // EN: Implement sRGB source-over blending with unpremultiplied blend colors and premultiplied output, including translucent backdrops.
    // ZH: 在 sRGB 中实现源覆盖混合，混合颜色使用非预乘值、输出使用预乘值，并处理半透明背景。
    static void Blend(byte[] backdrop, byte[] source, string mode)
    {
        for (var p = 0; p < backdrop.Length; p += 4)
        {
            var sa = source[p + 3] / 255d; if (sa <= 0) continue;
            var ba = backdrop[p + 3] / 255d;
            for (var c = 0; c < 3; c++)
            {
                var s = source[p + c] / (255 * sa);
                var b = ba > 0 ? backdrop[p + c] / (255 * ba) : 0;
                var mix = mode switch { "multiply" => s * b, "screen" => s + b - s * b, "overlay" => b <= .5 ? 2 * s * b : 1 - 2 * (1 - s) * (1 - b), _ => s };
                backdrop[p + c] = (byte)Math.Clamp(Math.Round(255 * (sa * (1 - ba) * s + sa * ba * mix + (1 - sa) * ba * b)), 0, 255);
            }
            backdrop[p + 3] = (byte)Math.Round(255 * (sa + ba * (1 - sa)));
        }
    }

    // EN: Interpret CSS hex alpha in trailing position and rgb/rgba colors, retaining named colors and transparent without external dependencies.
    // ZH: 按 CSS 尾随透明度解析十六进制及 rgb/rgba 颜色，同时支持命名色和透明色，无需外部依赖。
    public static Brush CssBrush(string? value)
    {
        try
        {
            var text = (value ?? "transparent").Trim();
            if (text.StartsWith('#') && text.Length == 9) text = "#" + text[7..9] + text[1..7];
            else if (text.StartsWith('#') && text.Length == 5) text = "#" + new string(text[4], 2) + new string(text[1], 2) + new string(text[2], 2) + new string(text[3], 2);
            if (text.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                var parts = text[(text.IndexOf('(') + 1)..text.LastIndexOf(')')].Split(',').Select(p => double.Parse(p.Trim(), CultureInfo.InvariantCulture)).ToArray();
                return new SolidColorBrush(Color.FromArgb((byte)Math.Round(255 * Math.Clamp(parts.Length > 3 ? parts[3] : 1, 0, 1)), (byte)Math.Clamp(parts[0], 0, 255), (byte)Math.Clamp(parts[1], 0, 255), (byte)Math.Clamp(parts[2], 0, 255)));
            }
            return (Brush)new BrushConverter().ConvertFromString(text)!;
        }
        catch { return Brushes.Transparent; }
    }
}
