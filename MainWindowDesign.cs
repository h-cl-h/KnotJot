// Store per-target snapshots and synchronize the design frame and text-region editor.
// 保存分目标快照，并同步设计框与文本区编辑器。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace KnotJotUiEditor
{
    /// <summary>
    /// 部件皮肤设计（partial）：选一个部件 → 出现有大小范围的设计框 → 画出它的新样子 +
    /// 放文本区 → 导出成保留功能的皮肤。每个部件的设计各存一份，可来回切换。
    /// </summary>
    public partial class MainWindow
    {
        private string _activeTarget;
        private readonly Dictionary<string, string> _designs = new Dictionary<string, string>();
        private readonly Dictionary<string, TreeViewItem> _targetNodes = new Dictionary<string, TreeViewItem>();
        private bool _switching;

        // 设计框可视化（放在 Overlay 里，不可交互）
        private Rectangle _frameRc;
        private TextBlock _frameLbl;
        private readonly Rectangle[] _dim = new Rectangle[4];
        private bool _syncingText;

        // Create frame overlays and populate the design-target picker from the target catalog.
        // 创建设计框叠加层，并从目标目录填充设计目标选择器。
        private void InitDesign()
        {
            _frameRc = new Rectangle
            {
                Stroke = BrushOf("#378ADD"),
                StrokeThickness = 1.6,
                StrokeDashArray = new DoubleCollection { 6, 4 },
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            Overlay.Children.Add(_frameRc);
            Panel.SetZIndex(_frameRc, 900);

            _frameLbl = new TextBlock { Foreground = BrushOf("#185FA5"), FontSize = 12, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
            Overlay.Children.Add(_frameLbl);
            Panel.SetZIndex(_frameLbl, 901);

            for (int i = 0; i < 4; i++)
            {
                _dim[i] = new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(28, 20, 24, 34)), IsHitTestVisible = false, Visibility = Visibility.Collapsed };
                Overlay.Children.Add(_dim[i]);
                Panel.SetZIndex(_dim[i], 890);
            }

            // 左侧"选部件来设计"树（分类可展开）
            DesignTree.Items.Clear();
            _targetNodes.Clear();
            foreach (var cat in DesignTargetLib.Categories)
            {
                var node = new TreeViewItem { Header = cat, IsExpanded = cat == "节点" || cat == "顶部工具栏", FontWeight = FontWeights.SemiBold };
                foreach (var t in DesignTargetLib.All.Where(/* Filter design targets to the current category. 将设计目标筛选为当前分类。 */ x => x.Category == cat))
                {
                    var leaf = new TreeViewItem { Header = t.Name, Tag = t.Id, FontWeight = FontWeights.Normal };
                    leaf.Selected += /* Switch targets on tree selection while avoiding recursive updates. 树选择变化时切换目标，并避免递归更新。 */ (s, e) => { if (!_switching) SwitchTarget((string)((TreeViewItem)s).Tag); e.Handled = true; };
                    _targetNodes[t.Id] = leaf;
                    node.Items.Add(leaf);
                }
                DesignTree.Items.Add(node);
            }
        }

        // Resolve the selected design target, falling back to the first catalog entry.
        // 解析所选设计目标，缺失时回退到目录首项。
        internal DesignTarget ActiveTarget { get { return DesignTargetLib.Find(_activeTarget) ?? DesignTargetLib.All[0]; } }

        /// <summary>切换正在设计的部件。会先把当前设计存起来，再载入目标部件的设计。</summary>
        // Snapshot the previous target, restore the requested design, and refresh frame and layer views.
        // 保存上一目标快照，恢复指定设计，并刷新设计框与图层视图。
        internal void SwitchTarget(string id, bool firstLoad = false)
        {
            ClearTool();
            if (!firstLoad && _activeTarget != null)
                _designs[_activeTarget] = SnapshotJson();

            _activeTarget = id;
            _switching = true;
            TreeViewItem node;
            if (_targetNodes.TryGetValue(id, out node))
            {
                var parent = node.Parent as TreeViewItem;
                if (parent != null) parent.IsExpanded = true;
                node.IsSelected = true;
            }
            _switching = false;

            ClearElements();
            ResetUndo();

            var t = ActiveTarget;
            string json;
            if (_designs.TryGetValue(id, out json) && !string.IsNullOrEmpty(json))
                LoadDesignJson(json);
            else if (t.HasText)
            {
                // 首次设计带文字的部件：自动放一个文本区，让"文字放哪"一目了然
                var tr = new TextRegionElement
                {
                    Name = "文本区",
                    X = t.DefaultTextRect.X, Y = t.DefaultTextRect.Y, W = t.DefaultTextRect.Width, H = t.DefaultTextRect.Height,
                    Text = t.Id == "card" || t.Id == "cardSel" ? "节点文字" : "按钮",
                    AlignH = (t.Id == "card" || t.Id == "cardSel") ? "left" : "center"
                };
                AddElementSilent(tr);
            }

            RebuildZ();
            ApplyMasks();
            RefreshLayers();
            ClearSelection();
            DrawFrame();
            if (DesignHint != null)
                DesignHint.Text = "正在设计：" + t.Name + (_exportSettings.ClipToFrame ? "（只有框内会显示）" : "（自由：框外也会导出）");
            if (WebHost.Visibility == Visibility.Visible) RefreshPreview();
        }

        // Deserialize a stored target design and create its editable elements and canvas views.
        // 反序列化已存目标设计，并创建可编辑元素与画布视图。
        private void LoadDesignJson(string json)
        {
            ProjectDto dto = null;
            try { dto = System.Text.Json.JsonSerializer.Deserialize<ProjectDto>(json); } catch { }
            if (dto == null || dto.elements == null) return;
            foreach (var d in dto.elements)
            {
                var el = FromDto(d);
                if (el != null) AddElementSilent(el);
            }
            ModelSafety.Normalize(_elements);
            foreach (var el in _elements) UpdateView(el);
        }

        /// <summary>把当前画布并入 _designs，返回完整字典（保存/预览用）。</summary>
        // Snapshot the active target and materialize all stored target designs for preview or export.
        // 保存活动目标快照，并将全部目标设计还原为预览或导出数据。
        internal Dictionary<string, List<CanvasElement>> CollectDesigns()
        {
            if (_activeTarget != null) _designs[_activeTarget] = SnapshotJson();
            var map = new Dictionary<string, List<CanvasElement>>();
            foreach (var kv in _designs)
            {
                ProjectDto dto = null;
                try { dto = System.Text.Json.JsonSerializer.Deserialize<ProjectDto>(kv.Value); } catch { }
                if (dto == null || dto.elements == null) continue;
                var list = dto.elements.Select(FromDto).Where(/* Discard unsupported or absent decoded elements. 丢弃不支持或缺失的解码元素。 */ x => x != null).ToList();
                ModelSafety.Normalize(list);
                if (list.Count > 0) map[kv.Key] = list;
            }
            return map;
        }

        // Position the target frame and outside-frame shading according to export constraints.
        // 根据导出约束定位目标设计框与框外阴影区域。
        internal void DrawFrame()
        {
            var t = ActiveTarget;
            var f = t.FrameRect;
            Canvas.SetLeft(_frameRc, f.X); Canvas.SetTop(_frameRc, f.Y);
            _frameRc.Width = f.Width; _frameRc.Height = f.Height;
            _frameRc.Visibility = Visibility.Visible;

            _frameLbl.Text = "▸ " + t.Name + " 设计框";
            Canvas.SetLeft(_frameLbl, f.X);
            Canvas.SetTop(_frameLbl, f.Y - 20);
            _frameLbl.Visibility = Visibility.Visible;

            // 框外压暗，表示"只显示框内"；自由模式（不限定）就不压暗
            bool clip = _exportSettings.ClipToFrame;
            if (clip)
            {
                SetRect(_dim[0], 0, 0, 1200, f.Y);                          // 上
                SetRect(_dim[1], 0, f.Bottom, 1200, 700 - f.Bottom);       // 下
                SetRect(_dim[2], 0, f.Y, f.X, f.Height);                   // 左
                SetRect(_dim[3], f.Right, f.Y, 1200 - f.Right, f.Height);  // 右
            }
            foreach (var d in _dim) d.Visibility = clip ? Visibility.Visible : Visibility.Collapsed;
        }

        // Set overlay rectangle coordinates and clamp negative dimensions to zero.
        // 设置叠加矩形坐标，并将负尺寸限制为零。
        private static void SetRect(Rectangle rc, double x, double y, double w, double h)
        {
            Canvas.SetLeft(rc, x); Canvas.SetTop(rc, y);
            rc.Width = Math.Max(0, w); rc.Height = Math.Max(0, h);
        }

        // ================= 文本区工具 =================
        // Select the existing text region or insert one at the target's default text bounds.
        // 选中已有文本区，或在目标默认文本边界插入一个文本区。
        internal void AddTextRegionTool()
        {
            var exist = _elements.OfType<TextRegionElement>().FirstOrDefault();
            if (exist != null) { SelectElement(exist); SetStatus("这个部件已经有一个文本区了——文字放这儿。"); return; }
            var t = ActiveTarget;
            var r = t.HasText ? t.DefaultTextRect : new Rect(t.FrameRect.X + 12, t.FrameRect.Y + 12, t.FrameRect.Width - 24, t.FrameRect.Height - 24);
            var tr = new TextRegionElement
            {
                Name = "文本区", X = r.X, Y = r.Y, W = r.Width, H = r.Height,
                Text = "文字", AlignH = "left"
            };
            AddElement(tr);
            SetStatus("已加文本区：里面就是这个部件真正的文字（可在右侧改示例、字号、对齐）。");
        }

        // ================= 文本区属性面板 =================
        // Bind text content, typography, alignment, and text-source choices to the selected region.
        // 将文本内容、字体、对齐与文字来源选项绑定到选中文本区。
        private void WireTextPanel()
        {
            CmbTextSource.ItemsSource = new[] { "部件本来的文字", "自己打固定文字" };
            TxtRegionText.TextChanged += /* Record text edits and refresh the selected text-region visual. 记录文字编辑并刷新选中文本区视图。 */ (s, e) =>
            {
                var tr = _sel as TextRegionElement;
                if (_syncingText || tr == null) return;
                PushUndo("txt:" + tr.GetHashCode());
                tr.Text = TxtRegionText.Text;
                UpdateView(tr); MarkDirty();
            };
            SldTextSize.ValueChanged += /* Apply the text-size slider with coalesced undo. 应用字号滑块并合并撤销。 */ (s, e) =>
            {
                var tr = _sel as TextRegionElement;
                if (_syncingText || tr == null) return;
                PushUndo("txtsz:" + tr.GetHashCode());
                tr.FontSize = Math.Round(SldTextSize.Value);
                LblTextSize.Text = "字号：" + tr.FontSize.ToString("0");
                UpdateView(tr); MarkDirty();
            };
            TxtTextColor.TextChanged += /* Validate and apply the selected text color. 验证并应用所选文字颜色。 */ (s, e) =>
            {
                var tr = _sel as TextRegionElement;
                if (_syncingText || tr == null) return;
                string n = ColorUtil.NormalizeHex(TxtTextColor.Text);
                if (n == null) return;
                PushUndo("txtcol:" + tr.GetHashCode());
                tr.Color = n; SetSwatch(SwText, n);
                UpdateView(tr); MarkDirty();
            };
            BtnTextColorPick.Click += /* Choose the text-region color. 选择文本区颜色。 */ (s, e) => PickColor(TxtTextColor);
            ChkTextBold.Click += /* Toggle bold text with an undo boundary. 在记录撤销边界后切换粗体。 */ (s, e) =>
            {
                var tr = _sel as TextRegionElement;
                if (_syncingText || tr == null) return;
                PushUndo();
                tr.Bold = ChkTextBold.IsChecked == true;
                UpdateView(tr); MarkDirty();
            };
            BtnAlignL.Click += /* Align the text region to the left. 将文本区左对齐。 */ (s, e) => SetAlign("left");
            BtnAlignC.Click += /* Center the text region horizontally. 将文本区水平居中。 */ (s, e) => SetAlign("center");
            BtnAlignR.Click += /* Align the text region to the right. 将文本区右对齐。 */ (s, e) => SetAlign("right");
            CmbTextSource.SelectionChanged += /* Record the choice between live application text and fixed custom text. 记录使用主程序实时文字或固定自定义文字的选择。 */ (s, e) =>
            {
                var tr = _sel as TextRegionElement;
                if (_syncingText || tr == null) return;
                bool value = CmbTextSource.SelectedIndex == 1;
                if (tr.CustomText == value) return;
                PushUndo("txtsrc:" + tr.Id);
                tr.CustomText = value;
                UpdateView(tr);
                MarkDirty();
                if (WebHost.Visibility == Visibility.Visible) RefreshPreview();
            };
            BtnDelText.Click += /* Delete eligible selected elements. 删除允许删除的选中元素。 */ (s, e) => DeleteSelected();
        }

        // Record undo and update the selected text region's horizontal and vertical alignment.
        // 记录撤销，并更新选中文本区的水平与垂直对齐。
        private void SetAlign(string a)
        {
            var tr = _sel as TextRegionElement;
            if (tr == null) return;
            PushUndo();
            tr.AlignH = a;
            UpdateView(tr); MarkDirty();
            SyncTextPanel();
        }

        // Populate text controls from the selected region while suppressing feedback events.
        // 从选中文本区填充文字控件，并阻止反馈事件重复写入。
        private void SyncTextPanel()
        {
            var tr = _sel as TextRegionElement;
            if (tr == null) return;
            _syncingText = true;
            TxtRegionText.Text = tr.Text;
            SldTextSize.Value = tr.FontSize;
            LblTextSize.Text = "字号：" + tr.FontSize.ToString("0");
            TxtTextColor.Text = tr.Color;
            SetSwatch(SwText, tr.Color);
            ChkTextBold.IsChecked = tr.Bold;
            BtnAlignL.FontWeight = tr.AlignH == "left" ? FontWeights.Bold : FontWeights.Normal;
            BtnAlignC.FontWeight = tr.AlignH == "center" ? FontWeights.Bold : FontWeights.Normal;
            BtnAlignR.FontWeight = tr.AlignH == "right" ? FontWeights.Bold : FontWeights.Normal;
            // 内容来源：只有高级模式能选"固定文字"
            RowTextSource.Visibility = Prefs.Advanced || _exportSettings.AllowCustomText ? Visibility.Visible : Visibility.Collapsed;
            CmbTextSource.IsEnabled = _exportSettings.AllowCustomText;
            CmbTextSource.SelectedIndex = tr.CustomText ? 1 : 0;
            _syncingText = false;
        }
    }
}
