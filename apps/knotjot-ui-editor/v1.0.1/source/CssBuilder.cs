using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;

namespace KnotJotUiEditor
{
    /// <summary>
    /// 把「部件配色 + 手搓图形/手绘/图片」拼成可直接注入 KnotJot 的 CSS（.knotjot-ui 写法 B）。
    /// 原则：**只输出改过的东西**——没添加的部件、没改动的颜色一概不写，软件里自动保持原版。
    /// 图形按落点分宿主：顶栏 → #toolbar；节点卡片 → .card（软件里每张卡片都带上）；其余 → body（垫底）。
    /// 位置按「就近的角」锚定，窗口大小变化不跑偏。蒙版通过稳定目标 ID 输出为 SVG mask。
    /// </summary>
    public static class CssBuilder
    {
        public const double DesignW = 1200, DesignH = 700;

        public static readonly Rect ToolbarBox = new Rect(0, 0, 1200, 54);
        public static readonly Rect[] CardBoxes =
        {
            new Rect(137, 326, 156, 52),
            new Rect(380, 279, 150, 46),
            new Rect(380, 379, 150, 46),
        };
        public static readonly Rect BodyBox = new Rect(0, 0, DesignW, DesignH);

        // Format rounded geometry with invariant decimal separators for CSS and SVG.
        // 使用不受区域设置影响的小数点格式输出取整后的 CSS 与 SVG 几何值。
        private static string F(double v)
        {
            return Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);
        }

        // Emit changed component properties and overlay decorations as a complete CSS skin.
        // 将已修改部件属性与叠加装饰输出为完整 CSS 皮肤。
        public static string Build(List<CanvasElement> elements, string name)
        {
            var sb = new StringBuilder();
            sb.AppendLine("/* 由 KnotJot 界面编辑器生成 · " + (name ?? "") + " · 未修改的部分自动保持原版 */");

            foreach (var el in elements)
            {
                var ce = el as ComponentElement;
                if (ce == null || !ce.Visible) continue;
                var def = ComponentLib.Find(ce.CompId);
                if (def == null) continue;
                foreach (var p in ce.Props)
                {
                    if (!p.Changed) continue;
                    string tpl;
                    if (def.CssTemplates.TryGetValue(p.Key, out tpl))
                        sb.AppendLine(tpl.Replace("@V", p.Value));
                }
            }

            AppendDecorations(sb, elements);
            return sb.ToString();
        }

        // ===== 图形 / 手绘 / 图片 → 各宿主的多层 background =====

        private class Layer { public string Img, Pos, Size; }

        // Group decoration layers by host selector and reverse stacking for CSS backgrounds.
        // 按宿主选择器归组装饰图层，并反转层序以符合 CSS 背景叠放规则。
        private static void AppendDecorations(StringBuilder sb, List<CanvasElement> elements)
        {
            var byHost = new Dictionary<string, List<Layer>>();

            // 索引越大越在上层；CSS 背景第一层在最上 → 逆序遍历
            for (int i = elements.Count - 1; i >= 0; i--)
            {
                var el = elements[i];
                if (!el.Visible) continue;
                var sh = el as ShapeElement;
                if (sh != null && sh.IsMask) continue;      // 遮罩本身不输出

                ShapeElement mask = IsDecoratable(el) ? FindMask(elements, i, el) : null;
                AddElementLayer(byHost, el, mask);
            }

            foreach (var kv in byHost)
            {
                string imgs = string.Join(",", kv.Value.Select(/* Select the background image value. 提取背景图像值。 */ l => l.Img));
                string poss = string.Join(",", kv.Value.Select(/* Select the background position value. 提取背景位置值。 */ l => l.Pos));
                string sizes = string.Join(",", kv.Value.Select(/* Select the background size value. 提取背景尺寸值。 */ l => l.Size));
                string extra = kv.Key == "body" ? "background-attachment:fixed!important;" : "";
                sb.AppendLine(kv.Key + "{background-image:" + imgs + "!important;background-position:" + poss
                    + "!important;background-size:" + sizes + "!important;background-repeat:no-repeat!important;" + extra + "}");
            }
        }

        // Identify shapes, ink, and images that can become exported decoration layers.
        // 识别可导出为装饰图层的图形、手绘与图片。
        private static bool IsDecoratable(CanvasElement el)
        {
            return el is ShapeElement || el is InkElement || el is ImageElement;
        }

        // Resolve the target's mask according to this version's stable-ID or adjacent-layer rules.
        // 按本版本的稳定 ID 或相邻图层规则查找目标蒙版。
        internal static ShapeElement FindMask(IList<CanvasElement> elements, int targetIndex, CanvasElement target)
        {
            if (elements == null || target == null || !IsDecoratable(target)) return null;
            var stable = elements.OfType<ShapeElement>()
                .LastOrDefault(/* Match visible non-line masks to the stable target ID. 按稳定目标 ID 匹配可见非直线蒙版。 */ m => m.IsMask && m.Visible && m.Kind != "line" && m.MaskTargetId == target.Id);
            if (stable != null) return stable;
            if (targetIndex + 1 < elements.Count)
            {
                var legacy = elements[targetIndex + 1] as ShapeElement;
                if (legacy != null && legacy.IsMask && legacy.Visible && legacy.Kind != "line"
                    && string.IsNullOrEmpty(legacy.MaskTargetId)) return legacy;
            }
            return null;
        }

        // Convert a drawable element and optional mask into a positioned CSS background layer.
        // 将可绘制元素及可选蒙版转换为定位的 CSS 背景图层。
        private static void AddElementLayer(Dictionary<string, List<Layer>> byHost, CanvasElement el, ShapeElement mask)
        {
            if (el is InkElement ink && ink.Points.Count < 2 || el is ImageElement image && string.IsNullOrEmpty(image.Base64)) return;
            var bbox = RotatedBounds(el);
            double pad = el is ShapeElement shapeElement ? Math.Ceiling(shapeElement.StrokeW) + 1 : el is InkElement stroke ? Math.Ceiling(stroke.Width) + 1 : 0;
            double opacity = ElementOpacity(el);
            string inner = DesignElementSvg(el, bbox.X - pad, bbox.Y - pad);
            if (inner == null) return;

            double svgW = Math.Max(1, bbox.Width + pad * 2), svgH = Math.Max(1, bbox.Height + pad * 2);
            string defs = "";
            if (mask != null)
            {
                string shape = ClipSvg(mask, bbox, pad, MaskShapeAttributes(mask));
                if (shape != null)
                {
                    string outside = IsInverseMask(mask) ? "white" : "black";
                    defs = "<defs><mask id='m' maskUnits='userSpaceOnUse' maskContentUnits='userSpaceOnUse' "
                        + "x='0' y='0' width='" + F(svgW) + "' height='" + F(svgH) + "' style='mask-type:luminance' color-interpolation='sRGB'>"
                        + "<rect x='0' y='0' width='" + F(svgW) + "' height='" + F(svgH) + "' fill='" + outside + "'/>"
                        + shape + "</mask></defs>";
                    inner = "<g mask='url(#m)'>" + inner + "</g>";
                }
            }
            if (opacity < 0.999) inner = "<g opacity='" + F(opacity) + "'>" + inner + "</g>";

            string svg = "<svg xmlns='http://www.w3.org/2000/svg' width='" + F(svgW) + "' height='" + F(svgH) + "'>" + defs + inner + "</svg>";
            string img = "url(\"data:image/svg+xml," + Uri.EscapeDataString(svg) + "\")";
            AddToHost(byHost, bbox, MakePositioned(bbox, pad, img));
        }

        // Translate decoration bounds into background position and size relative to its host.
        // 将装饰边界转换为相对于宿主的背景位置与尺寸。
        private static Layer MakePositioned(Rect bbox, double pad, string img)
        {
            Rect box = HostBox(bbox);
            double svgW = Math.Max(1, bbox.Width + pad * 2), svgH = Math.Max(1, bbox.Height + pad * 2);
            double dx = bbox.X - pad - box.X, dy = bbox.Y - pad - box.Y;
            string posX = (bbox.X + bbox.Width / 2) < box.X + box.Width / 2
                ? "left " + F(dx) + "px"
                : "right " + F(box.Width - (dx + svgW)) + "px";
            string posY = (bbox.Y + bbox.Height / 2) < box.Y + box.Height / 2
                ? "top " + F(dy) + "px"
                : "bottom " + F(box.Height - (dy + svgH)) + "px";
            return new Layer { Img = img, Pos = posX + " " + posY, Size = F(svgW) + "px " + F(svgH) + "px" };
        }

        // Choose the toolbar, node, or canvas host rectangle containing the decoration center.
        // 按装饰中心所在位置选择工具栏、节点或画布宿主矩形。
        private static Rect HostBox(Rect bbox)
        {
            var c = new Point(bbox.X + bbox.Width / 2, bbox.Y + bbox.Height / 2);
            if (ToolbarBox.Contains(c)) return ToolbarBox;
            foreach (var b in CardBoxes) if (b.Contains(c)) return b;
            return BodyBox;
        }

        // Map the decoration center to the CSS selector of its visual host.
        // 将装饰中心位置映射到可视宿主的 CSS 选择器。
        private static string HostSelector(Rect bbox)
        {
            var c = new Point(bbox.X + bbox.Width / 2, bbox.Y + bbox.Height / 2);
            if (ToolbarBox.Contains(c)) return "#toolbar";
            foreach (var b in CardBoxes) if (b.Contains(c)) return ".card";
            return "body";
        }

        // Append a generated layer to the list belonging to its host selector.
        // 将生成的图层加入所属宿主选择器的列表。
        private static void AddToHost(Dictionary<string, List<Layer>> byHost, Rect bbox, Layer layer)
        {
            string host = HostSelector(bbox);
            List<Layer> list;
            if (!byHost.TryGetValue(host, out list)) { list = new List<Layer>(); byHost[host] = list; }
            list.Add(layer);
        }

        // ===== 各类元素的 SVG 片段 =====

        // Serialize a shape's local geometry, fill, and stroke as an SVG element.
        // 将图形局部几何、填充与描边序列化为 SVG 元素。
        private static string ShapeSvg(ShapeElement s, Rect bbox, double pad)
        {
            string fill = s.NoFill ? "none" : s.Fill;
            string strokeAttr = s.StrokeW > 0
                ? " stroke='" + s.Stroke + "' stroke-width='" + F(s.StrokeW) + "'"
                : "";
            switch (s.Kind)
            {
                case "ellipse":
                    return "<ellipse cx='" + F(pad + bbox.Width / 2) + "' cy='" + F(pad + bbox.Height / 2)
                        + "' rx='" + F(bbox.Width / 2) + "' ry='" + F(bbox.Height / 2) + "' fill='" + fill + "'" + strokeAttr + "/>";
                case "line":
                    return "<line x1='" + F(s.X - bbox.X + pad) + "' y1='" + F(s.Y - bbox.Y + pad)
                        + "' x2='" + F(s.X2 - bbox.X + pad) + "' y2='" + F(s.Y2 - bbox.Y + pad)
                        + "' stroke='" + s.Stroke + "' stroke-width='" + F(Math.Max(1, s.StrokeW)) + "' stroke-linecap='round'/>";
                default:
                    string rx = s.Kind == "roundrect" ? " rx='" + F(s.Radius) + "'" : "";
                    return "<rect x='" + F(pad) + "' y='" + F(pad) + "' width='" + F(bbox.Width) + "' height='" + F(bbox.Height)
                        + "'" + rx + " fill='" + fill + "'" + strokeAttr + "/>";
            }
        }

        // Convert sampled ink points to an SVG path with rounded joins and caps.
        // 将采样手绘点转换为带圆角连接与端点的 SVG 路径。
        private static string InkSvg(InkElement k, Rect bbox, double pad)
        {
            var sb = new StringBuilder("M ");
            for (int i = 0; i < k.Points.Count; i++)
            {
                var p = k.Points[i];
                if (i > 0) sb.Append(" L ");
                sb.Append(F(p.X - bbox.X + pad)).Append(' ').Append(F(p.Y - bbox.Y + pad));
            }
            return "<path d='" + sb + "' fill='none' stroke='" + k.Color + "' stroke-width='" + F(k.Width)
                + "' stroke-linecap='round' stroke-linejoin='round'/>";
        }

        // Express mask geometry relative to padded decoration bounds.
        // 以带留白的装饰边界为基准表示蒙版几何。
        private static string ClipSvg(ShapeElement m, Rect bbox, double pad, string attrs = "")
        {
            double ox = bbox.X - pad, oy = bbox.Y - pad;
            attrs += RotationAttributes(m, ox, oy);
            switch (m.Kind)
            {
                case "ellipse":
                    return "<ellipse cx='" + F(m.X + m.W / 2 - ox) + "' cy='" + F(m.Y + m.H / 2 - oy)
                        + "' rx='" + F(m.W / 2) + "' ry='" + F(m.H / 2) + "'" + attrs + "/>";
                case "rect":
                case "roundrect":
                    string rx = m.Kind == "roundrect" ? " rx='" + F(m.Radius) + "'" : "";
                    return "<rect x='" + F(m.X - ox) + "' y='" + F(m.Y - oy) + "' width='" + F(m.W) + "' height='" + F(m.H) + "'" + rx + attrs + "/>";
                default:
                    return null;
            }
        }

        // Identify inverse alpha and inverse luminance mask modes.
        // 识别反向 Alpha 与反向亮度蒙版模式。
        private static bool IsInverseMask(ShapeElement mask)
        {
            return mask.MaskMode == "alpha-inverse" || mask.MaskMode == "luminance-inverse";
        }

        // Map mask color to sRGB luminance (or full alpha strength), complementing inverse modes.
        // 将蒙版颜色映射到 sRGB 亮度（或完整 Alpha 强度），反向模式取补值。
        private static string MaskColor(ShapeElement mask, string color)
        {
            double strength = 1;
            if (mask.MaskMode == "luminance" || mask.MaskMode == "luminance-inverse")
            {
                string c = ColorUtil.NormalizeHex(color) ?? "#ffffff";
                strength = (.2126 * Convert.ToInt32(c.Substring(1, 2), 16) + .7152 * Convert.ToInt32(c.Substring(3, 2), 16) + .0722 * Convert.ToInt32(c.Substring(5, 2), 16)) / 255;
            }
            if (IsInverseMask(mask)) strength = 1 - strength;
            int channel = (int)Math.Round(strength * 255);
            return "rgb(" + channel + "," + channel + "," + channel + ")";
        }

        // Preserve fill, stroke, and grouped opacity so SVG mask samples match the WPF compositor.
        // 保留填充、描边与整体透明度，使 SVG 蒙版采样匹配 WPF 合成器。
        private static string MaskShapeAttributes(ShapeElement mask)
        {
            return " fill='" + (mask.NoFill ? "none" : MaskColor(mask, mask.Fill)) + "'"
                + (mask.StrokeW > 0 ? " stroke='" + MaskColor(mask, mask.Stroke) + "' stroke-width='" + F(mask.StrokeW) + "'" : "")
                + " opacity='" + F(Math.Max(0, Math.Min(1, mask.Opacity))) + "'";
        }

        // Apply a model rotation around its geometric center in an SVG coordinate system.
        // 在 SVG 坐标系中围绕几何中心应用模型旋转。
        private static string RotationAttributes(CanvasElement element, double ox, double oy)
        {
            if (element.Rotation == 0) return "";
            var box = BBoxOfElement(element);
            return " transform='rotate(" + F(element.Rotation) + " " + F(box.X + box.Width / 2 - ox) + " " + F(box.Y + box.Height / 2 - oy) + ")'";
        }

        // ================= 部件皮肤（V0.0.3 重做核心）=================
        // 每个"设计目标"的元素 → 裁到设计框内 → 一张 SVG 背景贴到该部件的选择器上，
        // 文本区 → 内边距/对齐/字色作用到部件真正的文字元素，从而保留功能。

        // Export per-target designs as selector-scoped CSS with text and frame settings.
        // 将各设计目标按选择器导出为 CSS，并应用文本与设计框设置。
        public static string BuildSkins(Dictionary<string, List<CanvasElement>> designs, string name)
        {
            return BuildSkins(designs, name, new ProjectExportSettings());
        }

        // Export per-target designs as selector-scoped CSS with text and frame settings.
        // 将各设计目标按选择器导出为 CSS，并应用文本与设计框设置。
        public static string BuildSkins(Dictionary<string, List<CanvasElement>> designs, string name, ProjectExportSettings export)
        {
            export = export ?? new ProjectExportSettings();
            var sb = new StringBuilder();
            sb.AppendLine("/* 由 KnotJot 界面编辑器生成 · " + (name ?? "") + " · 部件皮肤（保留功能）·未设计的部件保持原版 */");
            if (designs == null) return sb.ToString();

            foreach (var kv in designs)
            {
                var t = DesignTargetLib.Find(kv.Key);
                if (t == null || kv.Value == null) continue;
                var els = kv.Value.Where(/* Keep visible design elements. 保留可见设计元素。 */ e => e.Visible).ToList();
                var drawable = els.Where(IsDrawable).ToList();
                var textRegion = els.OfType<TextRegionElement>().FirstOrDefault();
                if (drawable.Count == 0 && textRegion == null) continue;

                // 渲染框：普通/限定=设计框；自由=框∪所有元素
                Rect view = t.FrameRect;
                if (!export.ClipToFrame)
                    foreach (var e in drawable) view = Rect.Union(view, RotatedBounds(e));

                if (drawable.Count > 0)
                {
                    string svg = RenderDesignSvg(els, view, t, textRegion, export);
                    string img = "url(\"data:image/svg+xml," + Uri.EscapeDataString(svg) + "\")";
                    sb.AppendLine(t.SkinSelector + "{background-image:" + img
                        + "!important;background-size:100% 100%!important;background-repeat:no-repeat!important;"
                        + "background-color:transparent!important;border:none!important;box-shadow:none!important;}");
                }

                if (t.HasText && !string.IsNullOrEmpty(t.InnerSelector) && textRegion != null)
                {
                    var f = t.FrameRect;
                    double pl = Math.Max(0, textRegion.X - f.X), pt = Math.Max(0, textRegion.Y - f.Y);
                    double pr = Math.Max(0, f.Right - (textRegion.X + textRegion.W)), pb = Math.Max(0, f.Bottom - (textRegion.Y + textRegion.H));
                    var css = new StringBuilder();
                    css.Append("padding:" + F(pt) + "px " + F(pr) + "px " + F(pb) + "px " + F(pl) + "px!important;");
                    css.Append("text-align:" + textRegion.AlignH + "!important;");
                    css.Append("font-size:" + F(textRegion.FontSize) + "px!important;");
                    css.Append("font-weight:" + (textRegion.Bold ? "700" : "400") + "!important;");
                    bool custom = export.AllowCustomText && textRegion.CustomText;
                    css.Append("color:" + (custom ? "transparent" : textRegion.Color) + "!important;");
                    sb.AppendLine(t.InnerSelector + "{" + css + "}");
                }
            }
            return sb.ToString();
        }

        // Exclude masks from ordinary visible artwork while retaining supported drawable types.
        // 从普通可见作品中排除蒙版，保留受支持的可绘制类型。
        private static bool IsDrawable(CanvasElement el)
        {
            var s = el as ShapeElement;
            if (s != null) return !s.IsMask;   // 遮罩不单独画
            return el is InkElement || el is ImageElement;
        }

        // Compose the target's drawable layers, masks, opacity, and optional fixed text into SVG.
        // 将目标的绘制图层、蒙版、透明度与可选固定文字合成为 SVG。
        private static string RenderDesignSvg(List<CanvasElement> els, Rect view, DesignTarget t, TextRegionElement textRegion, ProjectExportSettings export)
        {
            double ox = view.X, oy = view.Y;
            var body = new StringBuilder();
            int clipId = 0;

            for (int i = 0; i < els.Count; i++)
            {
                var el = els[i];
                if (!IsDrawable(el)) continue;
                string node = DesignElementSvg(el, ox, oy);
                if (node == null) continue;

                ShapeElement mask = FindMask(els, i, el);
                double op = ElementOpacity(el);
                if (mask != null)
                {
                    string cid = "c" + (clipId++);
                    string outside = IsInverseMask(mask) ? "white" : "black";
                    body.Append("<defs><mask id='" + cid + "' maskUnits='userSpaceOnUse' maskContentUnits='userSpaceOnUse' "
                        + "x='0' y='0' width='" + F(view.Width) + "' height='" + F(view.Height) + "' style='mask-type:luminance' color-interpolation='sRGB'>"
                        + "<rect x='0' y='0' width='" + F(view.Width) + "' height='" + F(view.Height) + "' fill='" + outside + "'/>"
                        + MaskClipSvg(mask, ox, oy, MaskShapeAttributes(mask)) + "</mask></defs>");
                    body.Append("<g mask='url(#" + cid + ")'" + (op < 0.999 ? " opacity='" + F(op) + "'" : "") + ">" + node + "</g>");
                }
                else if (op < 0.999)
                    body.Append("<g opacity='" + F(op) + "'>" + node + "</g>");
                else
                    body.Append(node);
            }

            if (textRegion != null && export.AllowCustomText && textRegion.CustomText && !string.IsNullOrEmpty(textRegion.Text))
                body.Append(CustomTextSvg(textRegion, ox, oy));

            return "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 " + F(view.Width) + " " + F(view.Height) + "'>" + body + "</svg>";
        }

        // Read opacity from the element's concrete drawable type.
        // 从元素的具体绘制类型读取透明度。
        private static double ElementOpacity(CanvasElement el)
        {
            var s = el as ShapeElement; if (s != null) return s.Opacity;
            var k = el as InkElement; if (k != null) return k.Opacity;
            var im = el as ImageElement; if (im != null) return im.Opacity;
            return 1.0;
        }

        // Convert shape, ink, or image geometry into SVG relative to the target origin.
        // 以目标原点为基准，将图形、手绘或图片几何转换为 SVG。
        private static string DesignElementSvg(CanvasElement el, double ox, double oy)
        {
            var node = DesignElementSvgCore(el, ox, oy);
            return node == null || el.Rotation == 0 ? node : "<g" + RotationAttributes(el, ox, oy) + ">" + node + "</g>";
        }

        // Serialize unrotated local geometry; its caller applies the common model transform.
        // 序列化未旋转局部几何，由调用方应用公共模型变换。
        private static string DesignElementSvgCore(CanvasElement el, double ox, double oy)
        {
            var s = el as ShapeElement;
            if (s != null)
            {
                string fill = s.NoFill ? "none" : s.Fill;
                string stroke = s.StrokeW > 0 ? " stroke='" + s.Stroke + "' stroke-width='" + F(s.StrokeW) + "'" : "";
                switch (s.Kind)
                {
                    case "ellipse":
                        return "<ellipse cx='" + F(s.X - ox + s.W / 2) + "' cy='" + F(s.Y - oy + s.H / 2)
                            + "' rx='" + F(s.W / 2) + "' ry='" + F(s.H / 2) + "' fill='" + fill + "'" + stroke + "/>";
                    case "line":
                        return "<line x1='" + F(s.X - ox) + "' y1='" + F(s.Y - oy) + "' x2='" + F(s.X2 - ox) + "' y2='" + F(s.Y2 - oy)
                            + "' stroke='" + s.Stroke + "' stroke-width='" + F(Math.Max(1, s.StrokeW)) + "' stroke-linecap='round'/>";
                    default:
                        string rx = s.Kind == "roundrect" ? " rx='" + F(s.Radius) + "'" : "";
                        return "<rect x='" + F(s.X - ox) + "' y='" + F(s.Y - oy) + "' width='" + F(s.W) + "' height='" + F(s.H) + "'" + rx + " fill='" + fill + "'" + stroke + "/>";
                }
            }
            var k = el as InkElement;
            if (k != null)
            {
                if (k.Points.Count < 2) return null;
                var d = new StringBuilder("M ");
                for (int i = 0; i < k.Points.Count; i++)
                {
                    if (i > 0) d.Append(" L ");
                    d.Append(F(k.Points[i].X - ox)).Append(' ').Append(F(k.Points[i].Y - oy));
                }
                return "<path d='" + d + "' fill='none' stroke='" + k.Color + "' stroke-width='" + F(k.Width) + "' stroke-linecap='round' stroke-linejoin='round'/>";
            }
            var im = el as ImageElement;
            if (im != null)
            {
                if (string.IsNullOrEmpty(im.Base64)) return null;
                return "<image x='" + F(im.X - ox) + "' y='" + F(im.Y - oy) + "' width='" + F(im.W) + "' height='" + F(im.H)
                    + "' href='data:" + im.Mime + ";base64," + im.Base64 + "' preserveAspectRatio='none'/>";
            }
            return null;
        }

        // Serialize an ellipse or rectangle mask using the target-local coordinate origin.
        // 使用目标局部坐标原点序列化椭圆或矩形蒙版。
        private static string MaskClipSvg(ShapeElement m, double ox, double oy, string attrs = "")
        {
            attrs += RotationAttributes(m, ox, oy);
            switch (m.Kind)
            {
                case "ellipse":
                    return "<ellipse cx='" + F(m.X - ox + m.W / 2) + "' cy='" + F(m.Y - oy + m.H / 2) + "' rx='" + F(m.W / 2) + "' ry='" + F(m.H / 2) + "'" + attrs + "/>";
                default:
                    string rx = m.Kind == "roundrect" ? " rx='" + F(m.Radius) + "'" : "";
                    return "<rect x='" + F(m.X - ox) + "' y='" + F(m.Y - oy) + "' width='" + F(m.W) + "' height='" + F(m.H) + "'" + rx + attrs + "/>";
            }
        }

        // Render fixed text with the element's position, alignment, font, and color.
        // 按元素的位置、对齐、字体与颜色绘制固定文字。
        internal static string CustomTextSvg(TextRegionElement tr, double ox, double oy)
        {
            double x; string a;
            if (tr.AlignH == "center") { x = tr.X - ox + tr.W / 2; a = "middle"; }
            else if (tr.AlignH == "right") { x = tr.X - ox + tr.W; a = "end"; }
            else { x = tr.X - ox; a = "start"; }
            var lines = WrapText(tr.Text ?? "", Math.Max(1, tr.W), Math.Max(1, tr.FontSize));
            double lineHeight = Math.Max(1, tr.FontSize * 1.25);
            double totalHeight = lines.Count * lineHeight;
            double top = tr.Y - oy;
            if (tr.AlignV == "middle") top += (tr.H - totalHeight) / 2;
            else if (tr.AlignV == "bottom") top += tr.H - totalHeight;
            double y = top + tr.FontSize;
            var spans = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                string esc = EscapeXml(lines[i]);
                if (esc.Length == 0) esc = "&#160;";
                spans.Append("<tspan x='").Append(F(x)).Append("' dy='").Append(i == 0 ? "0" : F(lineHeight)).Append("'>").Append(esc).Append("</tspan>");
            }
            return "<text x='" + F(x) + "' y='" + F(y) + "' fill='" + tr.Color + "' font-size='" + F(tr.FontSize)
                + "' font-family='sans-serif' font-weight='" + (tr.Bold ? "700" : "400")
                + "' text-anchor='" + a + "'>" + spans + "</text>";
        }

        // Wrap Unicode text elements to approximate width while preserving explicit blank lines.
        // 按估算宽度折行 Unicode 文本单元，并保留显式空行。
        public static List<string> WrapText(string text, double maxWidth, double fontSize)
        {
            var output = new List<string>();
            string normalized = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
            foreach (string paragraph in normalized.Split(new[] { '\n' }, StringSplitOptions.None))
            {
                if (paragraph.Length == 0) { output.Add(""); continue; }
                var line = new StringBuilder();
                double width = 0;
                var e = StringInfo.GetTextElementEnumerator(paragraph);
                while (e.MoveNext())
                {
                    string element = e.GetTextElement();
                    double next = TextElementWidth(element, fontSize);
                    if (line.Length > 0 && width + next > maxWidth)
                    {
                        output.Add(line.ToString().TrimEnd()); line.Clear(); width = 0;
                        if (string.IsNullOrWhiteSpace(element)) continue;
                    }
                    line.Append(element); width += next;
                }
                output.Add(line.ToString().TrimEnd());
            }
            if (output.Count == 0) output.Add("");
            return output;
        }

        // Estimate glyph width from whitespace, narrow Latin, wide Latin, and non-Latin categories.
        // 按空白、窄拉丁字符、宽拉丁字符及非拉丁字符类别估算字形宽度。
        private static double TextElementWidth(string value, double fontSize)
        {
            if (string.IsNullOrWhiteSpace(value)) return fontSize * .34;
            if (value.Length > 1 || value[0] > 127) return fontSize;
            char c = value[0];
            if ("ilI1|!.,:;'`".IndexOf(c) >= 0) return fontSize * .34;
            if ("MW@#%&".IndexOf(c) >= 0) return fontSize * .86;
            if (char.IsPunctuation(c)) return fontSize * .45;
            return fontSize * .58;
        }

        // Escape ampersands and angle brackets before inserting text into SVG markup.
        // 在文字插入 SVG 标记前转义与号及尖括号。
        private static string EscapeXml(string value)
        {
            return (value ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        // ===== 包围盒 =====

        // Return endpoint bounds for a line or rectangular bounds for other shapes.
        // 直线返回端点边界，其他图形返回矩形边界。
        public static Rect BBoxOf(ShapeElement s)
        {
            if (s.Kind == "line") return new Rect(new Point(s.X, s.Y), new Point(s.X2, s.Y2));
            return new Rect(s.X, s.Y, s.W, s.H);
        }

        // Calculate ink point bounds and keep empty or zero-size strokes measurable.
        // 计算手绘点边界，并为无点或零尺寸笔画保留可测量范围。
        public static Rect InkBBox(InkElement k)
        {
            if (k.Points.Count == 0) return new Rect(0, 0, 1, 1);
            double x1 = double.MaxValue, y1 = double.MaxValue, x2 = double.MinValue, y2 = double.MinValue;
            foreach (var p in k.Points)
            {
                if (p.X < x1) x1 = p.X;
                if (p.Y < y1) y1 = p.Y;
                if (p.X > x2) x2 = p.X;
                if (p.Y > y2) y2 = p.Y;
            }
            return new Rect(x1, y1, Math.Max(1, x2 - x1), Math.Max(1, y2 - y1));
        }

        // Expand export bounds around rotated artwork without changing the editor's unrotated geometry contract.
        // 扩展旋转图稿的导出边界，不改变编辑器未旋转几何契约。
        private static Rect RotatedBounds(CanvasElement element)
        {
            var box = BBoxOfElement(element);
            if (element.Rotation == 0) return box;
            return new System.Windows.Media.RotateTransform(element.Rotation, box.X + box.Width / 2, box.Y + box.Height / 2).TransformBounds(box);
        }

        // Dispatch bounding-box calculation by element type, including component hit regions.
        // 按元素类型计算边界，包含部件的点击区域。
        public static Rect BBoxOfElement(CanvasElement el)
        {
            var s = el as ShapeElement;
            if (s != null) return BBoxOf(s);
            var k = el as InkElement;
            if (k != null) return InkBBox(k);
            var im = el as ImageElement;
            if (im != null) return new Rect(im.X, im.Y, im.W, im.H);
            var tr = el as TextRegionElement;
            if (tr != null) return new Rect(tr.X, tr.Y, tr.W, tr.H);
            var ce = el as ComponentElement;
            if (ce != null)
            {
                var def = ComponentLib.Find(ce.CompId);
                if (def != null && def.Boxes.Length > 0)
                {
                    var r = def.Boxes[0];
                    for (int i = 1; i < def.Boxes.Length; i++) r.Union(def.Boxes[i]);
                    return r;
                }
            }
            return new Rect(0, 0, 1, 1);
        }
    }
}

