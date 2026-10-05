// Define editable canvas element types, stable identity, and serializable appearance properties.
// 定义可编辑画布元素类型、稳定身份与可序列化外观属性。
using System.Collections.Generic;
using System.Windows;
using System;

namespace KnotJotUiEditor
{
    /// <summary>一个可调的颜色属性。</summary>
    public class PropVal
    {
        public string Key;
        public string Label;
        public string Value;
        public string Default;
        // Initialize an editable property and remember its original default for change detection.
        // 初始化可编辑属性，并记录原始默认值以检测修改。
        public PropVal(string key, string label, string def) { Key = key; Label = label; Value = def; Default = def; }
        // Report whether the editable value differs from the catalog default.
        // 判断可编辑值是否不同于目录默认值。
        public bool Changed { get { return Value != Default; } }
    }

    /// <summary>画布上的一个图层元素（组件 / 图形 / 手绘 / 图片）。索引越大越在上层。</summary>
    public abstract class CanvasElement
    {
        public string Id = "el_" + Guid.NewGuid().ToString("N");
        public string Name = "元素";
        public bool Visible = true;
        public bool IsLocked;
        public double Rotation;
        public string BlendMode = "normal";
        public abstract string TypeId { get; }
    }

    /// <summary>原版界面部件的一个实例（位置固定=它在真实界面里的位置，颜色可改）。</summary>
    public class ComponentElement : CanvasElement
    {
        public string CompId;
        public List<PropVal> Props = new List<PropVal>();
        // Expose the serialized discriminator for this concrete canvas element type.
        // 提供该画布元素具体类型的序列化判别标记。
        public override string TypeId { get { return "component"; } }
        // Find an editable component property by its stable key.
        // 按稳定键查找可编辑部件属性。
        public PropVal Prop(string key) { return Props.Find(/* Match the component property key. 匹配部件属性键。 */ p => p.Key == key); }
        // Read the named component color, falling back to black when the property is absent.
        // 读取指定部件颜色，属性缺失时回退为黑色。
        public string Get(string key) { var p = Prop(key); return p != null ? p.Value : "#000000"; }
    }

    /// <summary>手搓图形。line 用 X,Y=起点、X2,Y2=终点，其余用 X,Y,W,H。</summary>
    public class ShapeElement : CanvasElement
    {
        public string Kind;                 // rect / roundrect / ellipse / line
        public double X, Y, W, H;
        public double X2, Y2;
        public string Fill = "#5b8def";
        public bool NoFill;
        public string Stroke = "#3a6bd8";
        public double StrokeW;
        public double Radius = 12;
        public double Opacity = 1.0;
        public bool LockAspect;             // 当前图形自己的 1:1 锁，不会串到其他图形
        public bool IsMask;                 // 作为蒙版（仅 rect/roundrect/ellipse）
        public string MaskMode = "alpha";  // alpha / alpha-inverse / luminance / luminance-inverse
        public string MaskTargetId;         // 稳定引用目标；旧文件为空时迁移相邻下层
        // Expose the serialized discriminator for this concrete canvas element type.
        // 提供该画布元素具体类型的序列化判别标记。
        public override string TypeId { get { return Kind; } }
    }

    /// <summary>画笔手绘的一笔。</summary>
    public class InkElement : CanvasElement
    {
        public List<Point> Points = new List<Point>();
        public string Color = "#5b8def";
        public double Width = 3;
        public double Opacity = 1.0;
        // Expose the serialized discriminator for this concrete canvas element type.
        // 提供该画布元素具体类型的序列化判别标记。
        public override string TypeId { get { return "ink"; } }
    }

    /// <summary>导入的 JPG / PNG 图片。</summary>
    public class ImageElement : CanvasElement
    {
        public string Base64;
        public string Mime = "image/png";
        public double X, Y, W, H;
        public double Opacity = 1.0;
        // Expose the serialized discriminator for this concrete canvas element type.
        // 提供该画布元素具体类型的序列化判别标记。
        public override string TypeId { get { return "image"; } }
    }

    /// <summary>
    /// 文本区（内容槽）：部件里"文字放这儿"的区域。
    /// 默认绑定成部件本来的文字（节点可编辑的字 / 按钮标签）；高级模式可改成固定文字。
    /// 导出时它决定内边距 / 对齐 / 字号 / 字色，让你画的外框里能正常显示并编辑文字。
    /// </summary>
    public class TextRegionElement : CanvasElement
    {
        public double X, Y, W, H;
        public string Text = "文字";          // 编辑器里显示的示例；固定模式下=真正导出的字
        public bool CustomText;               // true=用 Text 固定文字；false=用部件本来的文字
        public double FontSize = 14;
        public string Color = "#23262e";
        public string AlignH = "left";        // left / center / right
        public string AlignV = "middle";      // top / middle / bottom
        public bool Bold;
        // Expose the serialized discriminator for this concrete canvas element type.
        // 提供该画布元素具体类型的序列化判别标记。
        public override string TypeId { get { return "textregion"; } }
    }
}
