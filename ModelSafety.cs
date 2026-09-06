using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace KnotJotUiEditor
{
    public static class ModelSafety
    {
        public const int MaxElements = 5000;
        public const int MaxInkPoints = 100000;
        public const int MaxEmbeddedImageChars = 16 * 1024 * 1024;
        static readonly HashSet<string> MaskModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "alpha", "alpha-inverse", "luminance", "luminance-inverse" };
        static readonly Regex Base64 = new Regex("^[A-Za-z0-9+/]*={0,2}$", RegexOptions.Compiled);

        // Bound element counts and values, repair IDs and legacy mask links, and sanitize embedded resources.
        // 限制元素数量与数值，修复 ID 与旧蒙版关联，并清理嵌入资源。
        public static void Normalize(IList<CanvasElement> elements)
        {
            if (elements == null) return;
            while (elements.Count > MaxElements) elements.RemoveAt(elements.Count - 1);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var el in elements.Where(/* Discard unsupported or absent decoded elements. 丢弃不支持或缺失的解码元素。 */ x => x != null))
            {
                var id = SafeId(el.Id);
                while (!ids.Add(id)) id = "el_" + Guid.NewGuid().ToString("N");
                el.Id = id; el.Name = SafeText(el.Name, "元素", 80);
                el.Rotation = Clamp(Finite(el.Rotation), -3600, 3600);
                el.BlendMode = NormalizeBlend(el.BlendMode);
                var s = el as ShapeElement;
                if (s != null)
                {
                    s.X = Coord(s.X); s.Y = Coord(s.Y); s.X2 = Coord(s.X2); s.Y2 = Coord(s.Y2);
                    s.W = Clamp(Finite(s.W, 1), 1, 20000); s.H = Clamp(Finite(s.H, 1), 1, 20000);
                    s.StrokeW = Clamp(Finite(s.StrokeW), 0, 500); s.Radius = Clamp(Finite(s.Radius), 0, 10000); s.Opacity = Opacity(s.Opacity);
                    s.Fill = ColorUtil.NormalizeHex(s.Fill) ?? "#5b8def"; s.Stroke = ColorUtil.NormalizeHex(s.Stroke) ?? "#3a6bd8";
                    s.MaskMode = MaskModes.Contains(s.MaskMode ?? "") ? s.MaskMode.ToLowerInvariant() : "alpha";
                    if (!s.IsMask || s.Kind == "line") { s.IsMask = false; s.MaskTargetId = null; }
                }
                var ink = el as InkElement;
                if (ink != null)
                {
                    ink.Color = ColorUtil.NormalizeHex(ink.Color) ?? "#5b8def"; ink.Width = Clamp(Finite(ink.Width, 3), .1, 500); ink.Opacity = Opacity(ink.Opacity);
                    ink.Points = (ink.Points ?? new List<Point>()).Where(/* Reject ink points with non-finite coordinates. 拒绝坐标为非有限值的手绘点。 */ p => IsFinite(p.X) && IsFinite(p.Y)).Take(MaxInkPoints).Select(/* Clamp ink-point coordinates into the supported canvas range. 将手绘点坐标限制在受支持画布范围。 */ p => new Point(Coord(p.X), Coord(p.Y))).ToList();
                }
                var im = el as ImageElement;
                if (im != null)
                {
                    im.X = Coord(im.X); im.Y = Coord(im.Y); im.W = Clamp(Finite(im.W, 8), 4, 20000); im.H = Clamp(Finite(im.H, 8), 4, 20000); im.Opacity = Opacity(im.Opacity);
                    im.Mime = im.Mime == "image/jpeg" || im.Mime == "image/webp" ? im.Mime : "image/png";
                    if (string.IsNullOrEmpty(im.Base64) || im.Base64.Length > MaxEmbeddedImageChars || !Base64.IsMatch(im.Base64)) im.Base64 = "";
                }
                var tr = el as TextRegionElement;
                if (tr != null)
                {
                    tr.X = Coord(tr.X); tr.Y = Coord(tr.Y); tr.W = Clamp(Finite(tr.W, 8), 8, 20000); tr.H = Clamp(Finite(tr.H, 8), 8, 20000);
                    tr.Text = SafeText(tr.Text, "文字", 10000); tr.FontSize = Clamp(Finite(tr.FontSize, 14), 1, 1000); tr.Color = ColorUtil.NormalizeHex(tr.Color) ?? "#23262e";
                    tr.AlignH = tr.AlignH == "center" || tr.AlignH == "right" ? tr.AlignH : "left"; tr.AlignV = tr.AlignV == "top" || tr.AlignV == "bottom" ? tr.AlignV : "middle";
                }
            }
            var byId = elements.Where(/* Discard unsupported or absent decoded elements. 丢弃不支持或缺失的解码元素。 */ x => x != null).ToDictionary(/* Select stable element IDs for undo grouping. 提取稳定元素 ID 以归组撤销。 */ x => x.Id, StringComparer.Ordinal);
            for (int i = 0; i < elements.Count; i++)
            {
                var mask = elements[i] as ShapeElement; if (mask == null || !mask.IsMask) continue;
                if (string.IsNullOrEmpty(mask.MaskTargetId) && i > 0 && !(elements[i - 1] is TextRegionElement) && !(elements[i - 1] is ComponentElement)) mask.MaskTargetId = elements[i - 1].Id;
                if (mask.MaskTargetId == mask.Id || !byId.TryGetValue(mask.MaskTargetId ?? "", out var target) || target is ComponentElement || target is TextRegionElement || (target is ShapeElement ts && ts.IsMask)) mask.MaskTargetId = null;
            }
        }

        // Retain a valid element ID or generate a replacement stable identifier.
        // 保留有效元素 ID，或生成替代的稳定标识符。
        static string SafeId(string id) { id = Regex.Replace(id ?? "", "[^A-Za-z0-9_-]", ""); return id.Length > 80 || id.Length < 3 ? "el_" + Guid.NewGuid().ToString("N") : id; }
        // Trim and bound display text, using a fallback when it is empty.
        // 清理并限制显示文字长度，空值使用回退文本。
        static string SafeText(string s, string fb, int max) { s = (s ?? fb).Replace("\0", ""); return s.Length > max ? s.Substring(0, max) : s; }
        // Reject NaN and infinities before using numbers in editor geometry.
        // 在数值用于编辑器几何前排除 NaN 与无穷值。
        static bool IsFinite(double v) { return !double.IsNaN(v) && !double.IsInfinity(v); }
        // Replace non-finite values with the caller's fallback.
        // 将非有限值替换为调用方的回退值。
        static double Finite(double v, double fb = 0) { return IsFinite(v) ? v : fb; }
        // Constrain a finite canvas coordinate to the supported geometry range.
        // 将有限画布坐标限制在受支持的几何范围内。
        static double Coord(double v) { return Clamp(Finite(v), -100000, 100000); }
        // Replace invalid numbers and bound valid numbers to the supplied interval.
        // 替换无效数值，并将有效数值限制在指定区间。
        static double Clamp(double v, double lo, double hi) { return Math.Min(hi, Math.Max(lo, v)); }
        // Normalize opacity into the zero-to-one interval.
        // 将透明度规范至零到一的区间。
        static double Opacity(double v) { return Clamp(Finite(v, 1), 0.01, 1); }
        // Accept a supported blend mode or fall back to normal compositing.
        // 接受受支持的混合模式，否则回退为普通合成。
        static string NormalizeBlend(string v) { return new[] { "normal", "multiply", "screen", "overlay", "darken", "lighten" }.Contains(v) ? v : "normal"; }
    }
}
