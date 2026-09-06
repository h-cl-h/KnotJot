// Define legacy component instances, color defaults, and CSS property templates.
// 定义旧部件实例、默认颜色与 CSS 属性模板。
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace KnotJotUiEditor
{
    /// <summary>
    /// 内置的「原版 UI 部件」清单：每个部件自带原版默认色（=不改就是原版），
    /// 每个颜色属性对应一段 CSS 模板（@V 换成颜色值），只有改过的才会写进导出 CSS。
    /// </summary>
    public class ComponentDef
    {
        public string Id;
        public string Name;
        public string Category;
        public List<PropVal> DefaultProps = new List<PropVal>();
        public Rect[] Boxes = new Rect[0];                       // 画布上的占位（吸附/高亮/点选用）
        public Dictionary<string, string> CssTemplates = new Dictionary<string, string>();

        // Copy the component's default properties into an independent editable instance.
        // 将部件默认属性复制到独立的可编辑实例。
        public ComponentElement CreateInstance()
        {
            var el = new ComponentElement { CompId = Id, Name = Name };
            foreach (var p in DefaultProps) el.Props.Add(new PropVal(p.Key, p.Label, p.Default));
            return el;
        }
    }

    public static class ComponentLib
    {
        // Ordered catalog used by component or target pickers and by CSS export.
        // 部件或目标选择器与 CSS 导出共同使用的有序目录。
        public static readonly List<ComponentDef> All = BuildAll();

        // Find the component definition with the exact stored component ID.
        // 按保存的部件 ID 查找对应定义。
        public static ComponentDef Find(string id) { return All.Find(/* Match the stored component ID. 匹配保存的部件 ID。 */ c => c.Id == id); }

        // Enumerate distinct component categories in catalog order.
        // 按目录顺序枚举不重复的部件分类。
        public static IEnumerable<string> Categories { get { return All.Select(/* Select the component category. 提取部件分类。 */ c => c.Category).Distinct(); } }

        // Define built-in component bounds, editable color properties, and matching CSS selectors.
        // 定义内置部件的边界、可编辑颜色属性与对应 CSS 选择器。
        private static List<ComponentDef> BuildAll()
        {
            var list = new List<ComponentDef>();

            // ================= 顶部工具栏 =================
            var toolbar = new ComponentDef { Id = "toolbar", Name = "顶栏底板", Category = "顶部工具栏", Boxes = new[] { new Rect(0, 0, 1200, 54) } };
            toolbar.DefaultProps.Add(new PropVal("bg", "背景色", "#ffffff"));
            toolbar.DefaultProps.Add(new PropVal("border", "底边线", "#e2e5ec"));
            toolbar.CssTemplates["bg"] = "#toolbar{background-color:@V!important;}";
            toolbar.CssTemplates["border"] = "#toolbar{border-bottom-color:@V!important;}.vsep{background:@V!important;}";
            list.Add(toolbar);

            var title = new ComponentDef { Id = "title", Name = "标题与圆点", Category = "顶部工具栏", Boxes = new[] { new Rect(18, 15, 148, 24) } };
            title.DefaultProps.Add(new PropVal("text", "标题文字", "#23262e"));
            title.DefaultProps.Add(new PropVal("dot", "圆点", "#5b8def"));
            title.CssTemplates["text"] = ".title{color:@V!important;}";
            title.CssTemplates["dot"] = ".dot{background:@V!important;}";
            list.Add(title);

            var btn = new ComponentDef { Id = "btn", Name = "普通按钮", Category = "顶部工具栏", Boxes = new[] { new Rect(190, 10, 92, 34), new Rect(402, 10, 80, 34) } };
            btn.DefaultProps.Add(new PropVal("bg", "背景色", "#ffffff"));
            btn.DefaultProps.Add(new PropVal("text", "文字色", "#23262e"));
            btn.DefaultProps.Add(new PropVal("border", "边框色", "#e2e5ec"));
            btn.CssTemplates["bg"] = ".btn{background-color:@V!important;}.btn.ghost{background:transparent!important;}";
            btn.CssTemplates["text"] = ".btn{color:@V!important;}";
            btn.CssTemplates["border"] = ".btn{border-color:@V!important;}.btn.ghost{border-color:transparent!important;}";
            list.Add(btn);

            var prim = new ComponentDef { Id = "btnPrimary", Name = "主按钮（＋文本框）", Category = "顶部工具栏", Boxes = new[] { new Rect(292, 10, 100, 34) } };
            prim.DefaultProps.Add(new PropVal("bg", "背景色", "#5b8def"));
            prim.DefaultProps.Add(new PropVal("text", "文字色", "#ffffff"));
            prim.CssTemplates["bg"] = ".btn.primary,.btn.toggle-on{background-color:@V!important;border-color:@V!important;}";
            prim.CssTemplates["text"] = ".btn.primary,.btn.toggle-on{color:@V!important;}";
            list.Add(prim);

            var fname = new ComponentDef { Id = "fname", Name = "文件名文字", Category = "顶部工具栏", Boxes = new[] { new Rect(1060, 19, 84, 18) } };
            fname.DefaultProps.Add(new PropVal("color", "文字色", "#8a90a0"));
            fname.CssTemplates["color"] = ".fname,#hint{color:@V!important;}";
            list.Add(fname);

            // ================= 画布区 =================
            var stage = new ComponentDef { Id = "stage", Name = "画布背景与网格", Category = "画布区", Boxes = new[] { new Rect(0, 54, 1200, 646) } };
            stage.DefaultProps.Add(new PropVal("bg", "背景色", "#f5f6f8"));
            stage.DefaultProps.Add(new PropVal("grid", "网格线", "#e7e9ee"));
            stage.CssTemplates["bg"] = ":root{--bg:@V!important;}html,body{background-color:@V!important;}";
            stage.CssTemplates["grid"] = ":root{--grid:@V!important;}#world{background-image:linear-gradient(@V 1px,transparent 1px),linear-gradient(90deg,@V 1px,transparent 1px)!important;}";
            list.Add(stage);

            var card = new ComponentDef { Id = "card", Name = "普通节点卡片", Category = "画布区", Boxes = new[] { new Rect(380, 279, 150, 46), new Rect(380, 379, 150, 46) } };
            card.DefaultProps.Add(new PropVal("bg", "背景色", "#ffffff"));
            card.DefaultProps.Add(new PropVal("border", "边框色", "#e2e5ec"));
            card.DefaultProps.Add(new PropVal("text", "文字色", "#23262e"));
            card.CssTemplates["bg"] = ":root{--card:@V!important;}.card{background-color:@V!important;}";
            card.CssTemplates["border"] = ":root{--card-border:@V!important;}.card{border-color:@V!important;}";
            card.CssTemplates["text"] = ":root{--ink:@V!important;}.card-inner{color:@V!important;}";
            list.Add(card);

            var cardSel = new ComponentDef { Id = "cardSel", Name = "选中节点卡片", Category = "画布区", Boxes = new[] { new Rect(135, 324, 160, 56) } };
            cardSel.DefaultProps.Add(new PropVal("bg", "背景色", "#ffffff"));
            cardSel.DefaultProps.Add(new PropVal("accent", "选中边框（主色）", "#5b8def"));
            cardSel.DefaultProps.Add(new PropVal("ring", "外圈光环", "#e8f0ff"));
            cardSel.DefaultProps.Add(new PropVal("text", "文字色", "#23262e"));
            cardSel.CssTemplates["bg"] = ".node.sel .card{background-color:@V!important;}";
            cardSel.CssTemplates["accent"] = ":root{--accent:@V!important;}.node.sel .card{border-color:@V!important;}";
            cardSel.CssTemplates["ring"] = ":root{--accent-soft:@V!important;}.node.sel .card{box-shadow:0 0 0 2px @V!important;}";
            cardSel.CssTemplates["text"] = ".node.sel .card .card-inner{color:@V!important;}";
            list.Add(cardSel);

            var brace = new ComponentDef { Id = "brace", Name = "大括号", Category = "画布区", Boxes = new[] { new Rect(330, 272, 42, 160) } };
            brace.DefaultProps.Add(new PropVal("color", "线条色", "#9aa3b8"));
            brace.CssTemplates["color"] = ":root{--brace:@V!important;}.brace-path{stroke:@V!important;}";
            list.Add(brace);

            var link = new ComponentDef { Id = "link", Name = "连线与锚点（蜘蛛网）", Category = "画布区", Boxes = new[] { new Rect(600, 420, 180, 100) } };
            link.DefaultProps.Add(new PropVal("color", "连线色", "#9aa3b8"));
            link.DefaultProps.Add(new PropVal("anchor", "锚点色", "#5b8def"));
            link.CssTemplates["color"] = ".link-line{stroke:@V!important;}";
            link.CssTemplates["anchor"] = ".anchor{stroke:@V!important;}";
            list.Add(link);

            // ================= 弹窗与浮条 =================
            var menu = new ComponentDef { Id = "menu", Name = "右键菜单 / 弹窗", Category = "弹窗与浮条", Boxes = new[] { new Rect(840, 130, 190, 150) } };
            menu.DefaultProps.Add(new PropVal("bg", "背景色", "#ffffff"));
            menu.DefaultProps.Add(new PropVal("text", "文字色", "#23262e"));
            menu.DefaultProps.Add(new PropVal("border", "边框/分隔线", "#e2e5ec"));
            menu.DefaultProps.Add(new PropVal("hoverBg", "悬停底色", "#e8f0ff"));
            menu.DefaultProps.Add(new PropVal("hoverText", "悬停文字", "#5b8def"));
            menu.CssTemplates["bg"] = ".popmenu,#ctx,#settingsBox,#colorPanel{background:@V!important;}";
            menu.CssTemplates["text"] = ".popmenu,#ctx,#settingsBox,#colorPanel,.mi,#ctx .item{color:@V!important;}";
            menu.CssTemplates["border"] = ".popmenu,#ctx,#settingsBox,#colorPanel{border-color:@V!important;}.msep,.sep{background:@V!important;}";
            menu.CssTemplates["hoverBg"] = ".mi:hover,.mi.active,#ctx .item:hover{background:@V!important;}";
            menu.CssTemplates["hoverText"] = ".mi:hover,.mi.active,#ctx .item:hover{color:@V!important;}";
            list.Add(menu);

            var fbar = new ComponentDef { Id = "floatbar", Name = "底部操作条", Category = "弹窗与浮条", Boxes = new[] { new Rect(500, 628, 200, 40) } };
            fbar.DefaultProps.Add(new PropVal("bg", "背景色", "#ffffff"));
            fbar.DefaultProps.Add(new PropVal("text", "图标/文字色", "#23262e"));
            fbar.DefaultProps.Add(new PropVal("border", "边框色", "#e2e5ec"));
            fbar.DefaultProps.Add(new PropVal("hoverBg", "悬停底色", "#e8f0ff"));
            fbar.CssTemplates["bg"] = "#floatBar,.actions{background:@V!important;}";
            fbar.CssTemplates["text"] = ".act{color:@V!important;}";
            fbar.CssTemplates["border"] = "#floatBar,.actions{border-color:@V!important;}";
            fbar.CssTemplates["hoverBg"] = ".act:hover{background:@V!important;}";
            list.Add(fbar);

            return list;
        }
    }
}
