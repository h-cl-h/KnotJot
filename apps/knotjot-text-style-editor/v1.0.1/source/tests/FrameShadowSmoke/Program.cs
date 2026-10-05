using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KnotJotTextStyleEditor;

internal static class Program
{
    // EN: Load browser PNGs into the same premultiplied pixel format as WPF before comparing alpha and color. ZH: 将浏览器 PNG 转为与 WPF 相同的预乘像素格式，再比较透明度和颜色。
    static byte[] ReadPixels(string file)
    {
        var decoder = BitmapDecoder.Create(new Uri(file), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var bitmap = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Pbgra32, null, 0);
        var pixels = new byte[320 * 240 * 4]; bitmap.CopyPixels(pixels, 320 * 4, 0); return pixels;
    }
    // EN: Compare real frame rendering with independent Electron output; retain baseline failures without requiring new APIs. ZH: 比较真实外框渲染和独立 Electron 输出，无需新接口即可保留基线失败。
    [STAThread]
    static int Main(string[] args)
    {
        var oracle = Path.GetFullPath(args[0]); var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
        var results = new List<object>(); var failures = 0;
        foreach (var item in JsonDocument.Parse(File.ReadAllText(Path.Combine(oracle, "cases.json"))).RootElement.EnumerateArray())
        {
            var id = item.GetProperty("id").GetString()!;
            try
            {
                var surface = new Canvas { Width = 160, Height = 80 };
                var method = typeof(FrameShadow).GetMethod("Apply")!;
                object?[] values = [surface, 160d, 80d, item.GetProperty("radius").GetDouble(), item.GetProperty("shadow").GetString(), item.GetProperty("bg").GetString(), item.GetProperty("border").GetString(), item.GetProperty("borderWidth").GetDouble(), item.GetProperty("color").GetString(), 14d];
                method.Invoke(null, values.Take(method.GetParameters().Length).ToArray());
                var canvas = new Canvas { Width = 320, Height = 240 }; Canvas.SetLeft(surface, 80); Canvas.SetTop(surface, 80); canvas.Children.Add(surface);
                canvas.Measure(new Size(320, 240)); canvas.Arrange(new Rect(0, 0, 320, 240)); canvas.UpdateLayout();
                var bitmap = new RenderTargetBitmap(320, 240, 96, 96, PixelFormats.Pbgra32); bitmap.Render(canvas);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var file = File.Create(Path.Combine(output, id + ".png"))) encoder.Save(file);
                var actual = new byte[320 * 240 * 4]; bitmap.CopyPixels(actual, 320 * 4, 0); var expected = ReadPixels(Path.Combine(oracle, id + ".png"));
                long difference = 0; int severe = 0; for (var i = 0; i < actual.Length; i++) { var delta = Math.Abs(actual[i] - expected[i]); difference += delta; if (delta > 20) severe++; }
                var mean = difference / (double)actual.Length; var fraction = severe / (double)actual.Length; bool pass = mean < .1 && fraction < .001;
                if (!pass) failures++; results.Add(new { id, pass, meanAbsoluteChannelError = mean, fractionOver20 = fraction }); Console.WriteLine($"{(pass ? "PASS" : "FAIL")} {id} mean={mean:0.000} severe={fraction:0.0000}");
            }
            catch (Exception error) { failures++; results.Add(new { id, pass = false, error = error.GetBaseException().ToString() }); Console.WriteLine("FAIL " + id + " " + error.GetBaseException().Message); }
        }
        typeof(FrameShadow).GetMethod("Shutdown")?.Invoke(null, null);
        File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true })); return failures == 0 ? 0 : 1;
    }
}
