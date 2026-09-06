// Map selectable design targets to frame bounds, runtime selectors, and preview metadata.
// 将可选设计目标映射到设计框边界、运行时选择器与预览元数据。
using System.Collections.Generic;
using System.Windows;

namespace KnotJotUiEditor
{
    /// <summary>
    /// 一个"可设计的部件"：你选它 → 画布上出现它的设计框（有大小范围）→ 你画出它的新样子，
    /// 框内的内容导出后成为这个部件在 KnotJot 里的外观，并保留它的文字/功能。
    /// </summary>
    public class DesignTarget
    {
        public string Id;
        public string Name;
        public string Category;
        public double FrameW, FrameH;       // 设计框参考尺寸（画布上居中放置）
        public string SkinSelector;         // 要换外观的元素
        public string InnerSelector;        // 文字所在元素（内边距/对齐作用于它）；为空=无文字
        public bool HasText;
        public Thickness TextInset;         // 默认文本区相对设计框的内缩（左上右下）
        public string PreviewGroup;         // 同组任一目标已设计时，预览中整组保持正常显示
        public string[] PreviewSelectors;   // 内置/真实预览中该组的稳定选择器

        // 画布上设计框的中心（所有目标共用画布中央，一次只设计一个）
        public const double Cx = 600, Cy = 330;

        // Center the design frame at the target's configured canvas position.
        // 在目标配置的画布位置居中放置设计框。
        public Rect FrameRect { get { return new Rect(Cx - FrameW / 2, Cy - FrameH / 2, FrameW, FrameH); } }

        // Inset the target frame to produce a text region with a minimum usable size.
        // 按目标内边距缩进设计框，生成具有最小可用尺寸的文本区。
        public Rect DefaultTextRect
        {
            get
            {
                var f = FrameRect;
                return new Rect(f.X + TextInset.Left, f.Y + TextInset.Top,
                    System.Math.Max(10, f.Width - TextInset.Left - TextInset.Right),
                    System.Math.Max(10, f.Height - TextInset.Top - TextInset.Bottom));
            }
        }
    }

    public static class DesignTargetLib
    {
        // Ordered catalog used by component or target pickers and by CSS export.
        // 部件或目标选择器与 CSS 导出共同使用的有序目录。
        public static readonly List<DesignTarget> All = new List<DesignTarget>
        {
            // ===== 节点 =====
            new DesignTarget{ Id="card", Name="普通节点 / 文本框", Category="节点",
                FrameW=260, FrameH=88, SkinSelector=".card", InnerSelector=".card-inner",
                HasText=true, TextInset=new Thickness(16,12,16,12), PreviewGroup="nodes", PreviewSelectors=new[]{".node"} },
            new DesignTarget{ Id="cardSel", Name="选中的节点", Category="节点",
                FrameW=260, FrameH=88, SkinSelector=".node.sel .card", InnerSelector=".node.sel .card-inner",
                HasText=true, TextInset=new Thickness(16,12,16,12), PreviewGroup="nodes", PreviewSelectors=new[]{".node"} },

            // ===== 顶部工具栏（拆细）=====
            new DesignTarget{ Id="toolbar", Name="顶栏底板", Category="顶部工具栏",
                FrameW=780, FrameH=60, SkinSelector="#toolbar", InnerSelector=null, HasText=false, PreviewGroup="toolbar", PreviewSelectors=new[]{"#toolbar"} },
            new DesignTarget{ Id="title", Name="标题文字", Category="顶部工具栏",
                FrameW=180, FrameH=32, SkinSelector=".title", InnerSelector=".title",
                HasText=true, TextInset=new Thickness(6,4,6,4), PreviewGroup="toolbar", PreviewSelectors=new[]{"#toolbar"} },
            new DesignTarget{ Id="dot", Name="品牌圆点", Category="顶部工具栏",
                FrameW=28, FrameH=28, SkinSelector=".dot", InnerSelector=null, HasText=false, PreviewGroup="toolbar", PreviewSelectors=new[]{"#toolbar"} },
            new DesignTarget{ Id="vsep", Name="工具栏分隔线", Category="顶部工具栏",
                FrameW=8, FrameH=30, SkinSelector=".vsep", InnerSelector=null, HasText=false, PreviewGroup="toolbar", PreviewSelectors=new[]{"#toolbar"} },
            new DesignTarget{ Id="btn", Name="普通按钮（文件等）", Category="顶部工具栏",
                FrameW=130, FrameH=42, SkinSelector=".btn:not(.primary):not(.toggle-on):not(.ghost)", InnerSelector=".btn:not(.primary):not(.toggle-on):not(.ghost)",
                HasText=true, TextInset=new Thickness(14,8,14,8), PreviewGroup="toolbar", PreviewSelectors=new[]{"#toolbar"} },
            new DesignTarget{ Id="btnPrimary", Name="主按钮（＋文本框）", Category="顶部工具栏",
                FrameW=150, FrameH=46, SkinSelector=".btn.primary,.btn.toggle-on", InnerSelector=".btn.primary,.btn.toggle-on",
                HasText=true, TextInset=new Thickness(16,8,16,8), PreviewGroup="toolbar", PreviewSelectors=new[]{"#toolbar"} },
            new DesignTarget{ Id="btnGhost", Name="幽灵按钮（整理）", Category="顶部工具栏",
                FrameW=130, FrameH=42, SkinSelector=".btn.ghost", InnerSelector=".btn.ghost",
                HasText=true, TextInset=new Thickness(14,8,14,8), PreviewGroup="toolbar", PreviewSelectors=new[]{"#toolbar"} },
            new DesignTarget{ Id="fname", Name="文件名文字", Category="顶部工具栏",
                FrameW=170, FrameH=28, SkinSelector=".fname", InnerSelector=".fname",
                HasText=true, TextInset=new Thickness(6,4,6,4), PreviewGroup="toolbar", PreviewSelectors=new[]{"#toolbar"} },

            // ===== 右键菜单（拆成容器/项/悬停/分隔）=====
            new DesignTarget{ Id="menuBox", Name="菜单容器（整块底板）", Category="右键菜单",
                FrameW=220, FrameH=170, SkinSelector=".popmenu,#ctx", InnerSelector=null, HasText=false, PreviewGroup="menu", PreviewSelectors=new[]{".popmenu","#ctx"} },
            new DesignTarget{ Id="menuItem", Name="菜单项（普通）", Category="右键菜单",
                FrameW=220, FrameH=40, SkinSelector=".mi,#ctx .item", InnerSelector=".mi,#ctx .item",
                HasText=true, TextInset=new Thickness(14,8,14,8), PreviewGroup="menu", PreviewSelectors=new[]{".popmenu","#ctx"} },
            new DesignTarget{ Id="menuItemHover", Name="菜单项（悬停/选中）", Category="右键菜单",
                FrameW=220, FrameH=40, SkinSelector=".mi:hover,.mi.active,#ctx .item:hover", InnerSelector=".mi:hover,.mi.active,#ctx .item:hover",
                HasText=true, TextInset=new Thickness(14,8,14,8), PreviewGroup="menu", PreviewSelectors=new[]{".popmenu","#ctx"} },
            new DesignTarget{ Id="menuSep", Name="菜单分隔线", Category="右键菜单",
                FrameW=200, FrameH=12, SkinSelector=".msep,.sep", InnerSelector=null, HasText=false, PreviewGroup="menu", PreviewSelectors=new[]{".popmenu","#ctx"} },

            // ===== 底部操作条 =====
            new DesignTarget{ Id="floatbar", Name="操作条容器", Category="底部操作条",
                FrameW=240, FrameH=52, SkinSelector="#floatBar,.actions", InnerSelector=null, HasText=false, PreviewGroup="floatbar", PreviewSelectors=new[]{"#floatBar",".actions"} },
            new DesignTarget{ Id="act", Name="操作按钮（图标）", Category="底部操作条",
                FrameW=52, FrameH=40, SkinSelector=".act", InnerSelector=".act",
                HasText=true, TextInset=new Thickness(6,6,6,6), PreviewGroup="floatbar", PreviewSelectors=new[]{"#floatBar",".actions"} },
            new DesignTarget{ Id="actHover", Name="操作按钮（悬停）", Category="底部操作条",
                FrameW=52, FrameH=40, SkinSelector=".act:hover", InnerSelector=".act:hover",
                HasText=true, TextInset=new Thickness(6,6,6,6), PreviewGroup="floatbar", PreviewSelectors=new[]{"#floatBar",".actions"} },

            // ===== 弹窗 / 面板 =====
            new DesignTarget{ Id="settings", Name="设置面板", Category="弹窗与面板",
                FrameW=340, FrameH=240, SkinSelector="#settingsBox", InnerSelector=null, HasText=false, PreviewGroup="settings", PreviewSelectors=new[]{"#settingsBox"} },
            new DesignTarget{ Id="colorPanel", Name="颜色面板", Category="弹窗与面板",
                FrameW=260, FrameH=190, SkinSelector="#colorPanel", InnerSelector=null, HasText=false, PreviewGroup="color-panel", PreviewSelectors=new[]{"#colorPanel"} },

            // ===== 开始页 =====
            new DesignTarget{ Id="startSide", Name="侧栏", Category="开始页",
                FrameW=230, FrameH=320, SkinSelector=".start-side", InnerSelector=null, HasText=false, PreviewGroup="start", PreviewSelectors=new[]{".start-side",".newcard",".recent .ri"} },
            new DesignTarget{ Id="newcard", Name="新建卡片", Category="开始页",
                FrameW=180, FrameH=150, SkinSelector=".newcard", InnerSelector=null, HasText=false, PreviewGroup="start", PreviewSelectors=new[]{".start-side",".newcard",".recent .ri"} },
            new DesignTarget{ Id="ncThumb", Name="卡片缩略图", Category="开始页",
                FrameW=160, FrameH=94, SkinSelector=".nc-thumb", InnerSelector=null, HasText=false, PreviewGroup="start", PreviewSelectors=new[]{".start-side",".newcard",".recent .ri"} },
            new DesignTarget{ Id="recentItem", Name="最近使用项", Category="开始页",
                FrameW=420, FrameH=52, SkinSelector=".recent .ri", InnerSelector=".recent .ri .rn",
                HasText=true, TextInset=new Thickness(44,10,80,10), PreviewGroup="start", PreviewSelectors=new[]{".start-side",".newcard",".recent .ri"} },
        };

        // Enumerate design categories without changing their first-occurrence order.
        // 枚举设计分类并保留首次出现的顺序。
        public static IEnumerable<string> Categories
        {
            get
            {
                var seen = new HashSet<string>();
                foreach (var t in All) if (seen.Add(t.Category)) yield return t.Category;
            }
        }

        // Find the design target matching the persistent target ID.
        // 查找与持久化目标 ID 匹配的设计目标。
        public static DesignTarget Find(string id) { return All.Find(/* Match the stored design target ID. 匹配保存的设计目标 ID。 */ t => t.Id == id); }
    }
}
