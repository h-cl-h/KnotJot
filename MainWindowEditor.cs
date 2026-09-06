// Maintain the element/view map and editing gestures, history, layers, masks, and properties.
// 维护元素与视图映射，以及编辑手势、历史、图层、蒙版与属性。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.Json;
using System.Windows.Shapes;
using IoPath = System.IO.Path;

namespace KnotJotUiEditor
{
    /// <summary>
    /// 画布与图层交互（partial）：添加部件/图形/手绘/图片、拖动缩放、自动吸附、
    /// 图层面板（排序/显隐/删除）、稳定目标蒙版、画笔模式、各类属性面板。
    /// </summary>
    public partial class MainWindow
    {
        internal readonly List<CanvasElement> _elements = new List<CanvasElement>();   // 索引越大越在上层
        private readonly Dictionary<CanvasElement, UIElement> _views = new Dictionary<CanvasElement, UIElement>();
        private readonly Dictionary<ShapeElement, Line> _lineHits = new Dictionary<ShapeElement, Line>();
        private CanvasElement _sel;
        private readonly HashSet<CanvasElement> _selection = new HashSet<CanvasElement>();
        private bool _syncingLayers;
        private bool _syncingShape;
        private int _shapeCounter, _inkCounter, _imgCounter;

        // 拖拽状态
        private bool _moving;
        private int _handle = -1;            // 0..7 缩放手柄；100/101 直线端点；-1 无
        private Point _down;
        private double _oX, _oY, _oW, _oH, _oX2, _oY2;
        private List<Point> _inkOrig;
        private bool _dragUndoPending;       // 真正动了才记撤销，防止点一下就多一条
        private readonly Dictionary<CanvasElement, MoveOrigin> _moveOrigins = new Dictionary<CanvasElement, MoveOrigin>();

        private class MoveOrigin
        {
            public double X, Y, X2, Y2, W, H;
            public List<Point> Points;
        }

        // 绘制工具（拖出图形）
        private string _tool;
        private bool _drawing;
        private Point _drawStart;
        private ShapeElement _drawShape;

        private const double SnapTol = 5;
        private const double SqTol = 8;      // 1:1 磁吸阈值

        // ================= 撤销 / 重做（整画布快照） =================
        private readonly List<string> _undoStack = new List<string>();
        private readonly List<string> _redoStack = new List<string>();
        private string _lastUndoKey;
        private DateTime _lastUndoTime;

        // Serialize the active element list into an undo and per-target project snapshot.
        // 将活动元素列表序列化为撤销与分目标工程快照。
        private string SnapshotJson()
        {
            ModelSafety.Normalize(_elements);
            var dto = new ProjectDto { schemaVersion = 4, elements = _elements.Select(ToDto).Where(/* Discard unsupported or absent decoded elements. 丢弃不支持或缺失的解码元素。 */ x => x != null).ToList() };
            return JsonSerializer.Serialize(dto);
        }

        /// <summary>在"即将改动"之前调用。coalesceKey 相同且间隔小于 1 秒的连续改动合并成一步（滑滑块/敲颜色不炸栈）。</summary>
        // Save the pre-edit snapshot, coalesce related edits, and clear invalidated redo history.
        // 保存编辑前快照，合并相关编辑，并清除失效的重做历史。
        internal void PushUndo(string coalesceKey = null)
        {
            if (coalesceKey != null && coalesceKey == _lastUndoKey
                && (DateTime.Now - _lastUndoTime).TotalMilliseconds < 1000)
            {
                _lastUndoTime = DateTime.Now;
                return;
            }
            _undoStack.Add(SnapshotJson());
            if (_undoStack.Count > 100) _undoStack.RemoveAt(0);
            _redoStack.Clear();
            _lastUndoKey = coalesceKey;
            _lastUndoTime = DateTime.Now;
        }

        // Clear undo and redo history when replacing the current design.
        // 替换当前设计时清空撤销与重做历史。
        internal void ResetUndo()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            _lastUndoKey = null;
        }

        // Move the current snapshot to redo and restore the most recent undo snapshot.
        // 将当前快照移至重做列表，并恢复最近的撤销快照。
        internal void Undo()
        {
            if (_undoStack.Count == 0) { SetStatus("没有可撤销的操作了。"); return; }
            _redoStack.Add(SnapshotJson());
            string json = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            _lastUndoKey = null;
            RestoreSnapshot(json);
            SetStatus("已撤销（Ctrl+Y 重做）。");
        }

        // Move the current snapshot to undo and restore the most recent redo snapshot.
        // 将当前快照移至撤销列表，并恢复最近的重做快照。
        internal void Redo()
        {
            if (_redoStack.Count == 0) { SetStatus("没有可重做的操作了。"); return; }
            _undoStack.Add(SnapshotJson());
            string json = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);
            _lastUndoKey = null;
            RestoreSnapshot(json);
            SetStatus("已重做。");
        }

        // Rebuild elements, canvas views, masks, and layer order from a serialized history entry.
        // 根据序列化历史记录重建元素、画布视图、蒙版与图层顺序。
        private void RestoreSnapshot(string json)
        {
            ProjectDto dto = null;
            try { dto = JsonSerializer.Deserialize<ProjectDto>(json); } catch { }
            ClearElements();
            if (dto != null && dto.elements != null)
                foreach (var d in dto.elements)
                {
                    var el = FromDto(d);
                    if (el != null) AddElementSilent(el);
                }
            ModelSafety.Normalize(_elements);
            foreach (var el in _elements) UpdateView(el);
            RebuildZ();
            ApplyMasks();
            RefreshLayers();
            ClearSelection();
            MarkDirty();
        }

        // ================= 选中框 / 手柄 / 辅助线 =================
        private Rectangle _outline;
        private Rectangle _marquee;
        private readonly Rectangle[] _handles = new Rectangle[8];
        private readonly Rectangle[] _ends = new Rectangle[2];
        private Line _vGuide, _hGuide;
        private readonly List<Rectangle> _regionRects = new List<Rectangle>();
        private bool _marqueeActive;
        private Point _marqueeStart;
        private HashSet<CanvasElement> _marqueeBase = new HashSet<CanvasElement>();
        private Rect _groupResizeBox = Rect.Empty;
        private Point _layerDragStart;
        private List<CanvasElement> _layerDragItems = new List<CanvasElement>();

        // Create selection outlines, resize handles, and snap guides above the drawing surface.
        // 在绘图区上方创建选择轮廓、缩放手柄与吸附辅助线。
        private void InitAdorners()
        {
            _outline = new Rectangle
            {
                Stroke = BrushOf("#5b8def"),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            Overlay.Children.Add(_outline);
            Panel.SetZIndex(_outline, 1000);

            _marquee = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(28, 91, 141, 239)),
                Stroke = BrushOf("#5b8def"), StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 3, 2 }, IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            Overlay.Children.Add(_marquee);
            Panel.SetZIndex(_marquee, 1002);

            var cursors = new[] { Cursors.SizeNWSE, Cursors.SizeNS, Cursors.SizeNESW, Cursors.SizeWE,
                                  Cursors.SizeNWSE, Cursors.SizeNS, Cursors.SizeNESW, Cursors.SizeWE };
            for (int i = 0; i < 8; i++)
            {
                var h = new Rectangle
                {
                    Width = 9, Height = 9,
                    Fill = Brushes.White,
                    Stroke = BrushOf("#5b8def"),
                    StrokeThickness = 1.2,
                    Cursor = cursors[i],
                    Tag = i,
                    Visibility = Visibility.Collapsed
                };
                h.MouseLeftButtonDown += HandleDown;
                h.MouseMove += DragMove;
                h.MouseLeftButtonUp += DragUp;
                Overlay.Children.Add(h);
                Panel.SetZIndex(h, 1001);
                _handles[i] = h;
            }
            for (int i = 0; i < 2; i++)
            {
                var h = new Rectangle
                {
                    Width = 11, Height = 11, RadiusX = 5.5, RadiusY = 5.5,
                    Fill = Brushes.White,
                    Stroke = BrushOf("#5b8def"),
                    StrokeThickness = 1.2,
                    Cursor = Cursors.Cross,
                    Tag = 100 + i,
                    Visibility = Visibility.Collapsed
                };
                h.MouseLeftButtonDown += HandleDown;
                h.MouseMove += DragMove;
                h.MouseLeftButtonUp += DragUp;
                Overlay.Children.Add(h);
                Panel.SetZIndex(h, 1001);
                _ends[i] = h;
            }

            _vGuide = new Line { Stroke = BrushOf("#e0533d"), StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 5, 3 }, IsHitTestVisible = false, Visibility = Visibility.Collapsed, Y1 = 0, Y2 = 700 };
            _hGuide = new Line { Stroke = BrushOf("#e0533d"), StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 5, 3 }, IsHitTestVisible = false, Visibility = Visibility.Collapsed, X1 = 0, X2 = 1200 };
            Overlay.Children.Add(_vGuide);
            Overlay.Children.Add(_hGuide);
            Panel.SetZIndex(_vGuide, 999);
            Panel.SetZIndex(_hGuide, 999);
        }

        // ================= 添加元素 =================
        // Translate a shape-kind identifier into the label displayed by drawing tools.
        // 将图形类型标识符转换为绘图工具显示的名称。
        private static string KindName(string kind)
        {
            switch (kind)
            {
                case "roundrect": return "圆角矩形";
                case "ellipse": return "圆形";
                case "line": return "直线";
                default: return "矩形";
            }
        }

        // Choose a layer-list icon based on the element type and shape kind.
        // 根据元素类型与图形种类选择图层列表图标。
        private static string ElementIcon(CanvasElement el)
        {
            if (el is ComponentElement) return "🧩";
            if (el is InkElement) return "🖊";
            if (el is ImageElement) return "🖼";
            if (el is TextRegionElement) return "🅣";
            var s = el as ShapeElement;
            if (s != null)
                switch (s.Kind)
                {
                    case "roundrect": return "▢";
                    case "ellipse": return "◯";
                    case "line": return "╱";
                    default: return "▭";
                }
            return "·";
        }

        // Insert an element and create its canvas view without creating an undo boundary.
        // 插入元素并创建其画布视图，不创建新的撤销边界。
        internal void AddElementSilent(CanvasElement el, int index = -1)
        {
            if (string.IsNullOrEmpty(el.Id) || _elements.Any(/* Detect duplicate element IDs before insertion. 插入前检测重复元素 ID。 */ x => x.Id == el.Id)) el.Id = "el_" + Guid.NewGuid().ToString("N");
            if (index < 0 || index > _elements.Count) index = _elements.Count;
            _elements.Insert(index, el);
            CreateView(el);
            UpdateView(el);
        }

        // Record undo, insert and select the element, then update layers and preview state.
        // 记录撤销、插入并选中元素，然后更新图层与预览状态。
        internal void AddElement(CanvasElement el, int index = -1)
        {
            PushUndo();
            RbViewCanvas.IsChecked = true;
            AddElementSilent(el, index);
            RebuildZ();
            ApplyMasks();
            RefreshLayers();
            SelectElement(el);
            MarkDirty();
        }

        // Select an existing component instance or add a new instance from its catalog definition.
        // 选中已有部件实例，或根据目录定义添加新实例。
        internal void AddComponent(string id)
        {
            var def = ComponentLib.Find(id);
            if (def == null) return;
            var exist = _elements.OfType<ComponentElement>().FirstOrDefault(/* Find an existing instance of the chosen component. 查找所选部件的已有实例。 */ c => c.CompId == id);
            if (exist != null)
            {
                SelectElement(exist);
                SetStatus("「" + def.Name + "」已经在画布上了，点右侧改它的颜色。");
                return;
            }
            var ce = def.CreateInstance();
            AddElement(ce, id == "stage" ? 0 : -1);
            SetStatus("已添加原版「" + def.Name + "」：点它改颜色；不改就保持原版。");
        }

        /// <summary>给当前选中的图形/手绘/图片盖一个遮罩（插到它正上方，立刻能看到裁剪效果）。</summary>
        // Create a rounded mask over the selected drawable element using its bounds.
        // 根据选中可绘制元素的边界，在其上方创建圆角蒙版。
        internal void AddMaskOverSelected()
        {
            var target = _sel;
            if (target == null || !IsMaskTarget(target))
            {
                SetStatus("先选中一个图形 / 手绘 / 图片，再点「🎭 加遮罩」。");
                return;
            }
            var r = CssBuilder.BBoxOfElement(target);
            var m = new ShapeElement
            {
                Kind = "roundrect",
                IsMask = true,
                MaskMode = "alpha",
                MaskTargetId = target.Id,
                Name = "遮罩 " + (++_shapeCounter),
                Radius = Math.Max(6, Math.Min(24, Math.Min(r.Width, r.Height) / 4)),
                X = r.X + r.Width * 0.12,
                Y = r.Y + r.Height * 0.12,
                W = Math.Max(20, r.Width * 0.76),
                H = Math.Max(20, r.Height * 0.76)
            };
            AddElement(m, _elements.IndexOf(target) + 1);
            SetStatus("遮罩已盖在「" + target.Name + "」上：只露出遮罩范围内的部分。拖动/缩放这个虚线框试试；不要了就删掉它。");
        }

        // ================= 绘制工具：选工具 → 在画布上拖出图形 =================
        // Toggle the requested drawing tool and expose its pointer-capture layer.
        // 切换指定绘图工具，并显示其指针捕获图层。
        internal void SetTool(string kind)
        {
            if (_tool == kind) { ClearTool(); return; }
            _tool = kind;
            if (BtnPen.IsChecked == true) BtnPen.IsChecked = false;
            RbViewCanvas.IsChecked = true;
            DrawLayer.Visibility = Visibility.Visible;
            HighlightToolButtons();
            SetStatus("在设计框里按住拖出一个「" + KindName(kind) + "」——长宽接近时磁吸成正方形/正圆（按住 Shift 强制 1:1）。");
        }

        // Exit the active drawing tool and hide its input overlay.
        // 退出活动绘图工具并隐藏输入叠加层。
        internal void ClearTool()
        {
            _tool = null;
            DrawLayer.Visibility = Visibility.Collapsed;
            HighlightToolButtons();
        }

        // Update drawing-button highlights to match the selected tool.
        // 更新绘图按钮高亮，使其与所选工具一致。
        private void HighlightToolButtons()
        {
            SetBtnActive(BtnAddRect, _tool == "rect");
            SetBtnActive(BtnAddRound, _tool == "roundrect");
            SetBtnActive(BtnAddEllipse, _tool == "ellipse");
            SetBtnActive(BtnAddLine, _tool == "line");
        }

        // Apply active or inactive colors and font weight to a drawing button.
        // 为绘图按钮应用活动或非活动颜色与字重。
        private static void SetBtnActive(Button b, bool on)
        {
            b.Background = on ? BrushOf("#dbe8ff") : Brushes.White;
            b.FontWeight = on ? FontWeights.Bold : FontWeights.Normal;
        }

        // Start a shape at the pointer position, capture input, and record its undo boundary.
        // 在指针位置开始创建图形、捕获输入并记录撤销边界。
        private void DrawDown(object sender, MouseButtonEventArgs e)
        {
            if (_tool == null) return;
            var p = e.GetPosition(Stage);
            _drawStart = p;
            _drawing = true;
            PushUndo();
            var s = new ShapeElement { Kind = _tool, Name = KindName(_tool) + " " + (++_shapeCounter) };
            if (_tool == "line") { s.X = p.X; s.Y = p.Y; s.X2 = p.X; s.Y2 = p.Y; s.NoFill = true; s.StrokeW = 3; s.Stroke = "#5b8def"; }
            else { s.X = p.X; s.Y = p.Y; s.W = 1; s.H = 1; }
            _drawShape = s;
            AddElementSilent(s);
            RebuildZ();
            UpdateView(s);
            _sel = s;
            DrawLayer.CaptureMouse();
            Focus();
            e.Handled = true;
        }

        // Update the in-progress shape geometry from pointer movement and size constraints.
        // 根据指针移动与尺寸约束更新正在绘制的图形几何。
        private void DrawMove(object sender, MouseEventArgs e)
        {
            if (!_drawing || _drawShape == null) return;
            var cur = e.GetPosition(Stage);
            var s = _drawShape;
            if (s.Kind == "line") { s.X2 = cur.X; s.Y2 = cur.Y; UpdateView(s); return; }

            double dx = cur.X - _drawStart.X, dy = cur.Y - _drawStart.Y;
            double w = Math.Abs(dx), h = Math.Abs(dy);
            bool forceSq = s.LockAspect || (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            bool snapped = false;
            double m = Math.Max(w, h);
            if (forceSq) { w = h = m; snapped = true; }
            else if (Math.Abs(w - h) <= Math.Max(SqTol, 0.1 * m)) { w = h = m; snapped = true; }

            s.W = Math.Max(1, w); s.H = Math.Max(1, h);
            s.X = dx < 0 ? _drawStart.X - s.W : _drawStart.X;
            s.Y = dy < 0 ? _drawStart.Y - s.H : _drawStart.Y;
            UpdateView(s);
            if (snapped) SetStatus((s.Kind == "ellipse" ? "正圆" : "正方形") + " · 磁吸 1:1（" + Math.Round(s.W) + "×" + Math.Round(s.H) + "）");
        }

        // Finish drawing, repair tiny geometry, select the new shape, and refresh layers.
        // 完成绘图、修正过小几何、选中新图形并刷新图层。
        private void DrawUp(object sender, MouseButtonEventArgs e)
        {
            if (!_drawing) return;
            _drawing = false;
            DrawLayer.ReleaseMouseCapture();
            var s = _drawShape;
            _drawShape = null;
            if (s != null)
            {
                if (s.Kind == "line")
                {
                    if (Math.Abs(s.X2 - s.X) + Math.Abs(s.Y2 - s.Y) < 6) { s.X2 = s.X + 140; }   // 只是点了一下 → 给默认长度
                }
                else if (s.W < 6 && s.H < 6)
                {
                    s.W = s.Kind == "ellipse" ? 90 : 140; s.H = 90;   // 只是点了一下 → 给默认尺寸
                }
                UpdateView(s);
            }
            RebuildZ();
            ApplyMasks();
            RefreshLayers();
            if (s != null) SelectElement(s);
            ClearTool();   // 画完一个回到选择态；想再画点一次工具
            MarkDirty();
            e.Handled = true;
        }

        // Insert a default-sized shape at a staggered canvas position.
        // 在交错的画布位置插入默认尺寸图形。
        internal void AddShape(string kind)
        {
            var s = new ShapeElement { Kind = kind };
            double off = (_shapeCounter % 8) * 24;
            _shapeCounter++;
            s.Name = KindName(kind) + " " + _shapeCounter;
            if (kind == "line")
            {
                s.X = 520 + off; s.Y = 150 + off; s.X2 = 680 + off; s.Y2 = 150 + off;
                s.NoFill = true; s.StrokeW = 3; s.Stroke = "#5b8def";
            }
            else if (kind == "ellipse") { s.X = 560 + off; s.Y = 120 + off; s.W = 90; s.H = 90; }
            else { s.X = 540 + off; s.Y = 110 + off; s.W = 140; s.H = 90; }
            AddElement(s);
            SetStatus("已添加「" + KindName(kind) + "」：拖动摆放（自动吸附），右侧改颜色；放到顶栏/卡片上=装饰它们。");
        }

        // Read a selected image, decode its dimensions, and embed a scaled editable image element.
        // 读取所选图片、解码尺寸，并嵌入缩放后的可编辑图片元素。
        private void ImportImage()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "图片 (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg" };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                byte[] bytes = File.ReadAllBytes(dlg.FileName);
                if (bytes.Length > 12 * 1024 * 1024) throw new InvalidDataException("图片超过 12 MB，请先压缩或缩小后再导入。");
                string ext = IoPath.GetExtension(dlg.FileName).ToLowerInvariant();
                string mime = ext == ".png" ? "image/png" : "image/jpeg";
                var bi = BitmapFromBytes(bytes);
                if (bi.PixelWidth > 8192 || bi.PixelHeight > 8192 || (long)bi.PixelWidth * bi.PixelHeight > 40000000)
                    throw new InvalidDataException("图片像素过大（最长边 8192，最多 4000 万像素）。");
                double w = bi.PixelWidth, h = bi.PixelHeight;
                double scale = Math.Min(1.0, Math.Min(420.0 / Math.Max(1, w), 320.0 / Math.Max(1, h)));
                w = Math.Max(8, w * scale);
                h = Math.Max(8, h * scale);
                var im = new ImageElement
                {
                    Name = "图片 " + (++_imgCounter),
                    Base64 = Convert.ToBase64String(bytes),
                    Mime = mime,
                    X = 600 - w / 2, Y = 377 - h / 2, W = w, H = h
                };
                AddElement(im);
                SetStatus("图片已导入：拖动摆放、拖角缩放；在它上面放一个「遮罩」图形可裁成圆形/圆角。");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "导入失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Decode image bytes with on-load caching and freeze the bitmap for canvas reuse.
        // 使用加载时缓存解码图片字节，并冻结位图供画布复用。
        private static BitmapImage BitmapFromBytes(byte[] bytes)
        {
            var bi = new BitmapImage();
            using (var ms = new MemoryStream(bytes))
            {
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = ms;
                bi.EndInit();
            }
            bi.Freeze();
            return bi;
        }

        // ================= 视图创建 / 刷新 =================
        // Create the WPF visual appropriate to each element type and attach selection or drag input.
        // 为各元素类型创建适当的 WPF 可视对象，并连接选择或拖动输入。
        private void CreateView(CanvasElement el)
        {
            UIElement v = null;

            var ce = el as ComponentElement;
            if (ce != null) v = BuildComponentView(ce);

            var s = el as ShapeElement;
            if (s != null)
            {
                Shape sh;
                switch (s.Kind)
                {
                    case "ellipse": sh = new Ellipse(); break;
                    case "line": sh = new Line { StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round }; break;
                    default: sh = new Rectangle(); break;
                }
                sh.Tag = el;
                sh.Cursor = Cursors.SizeAll;
                HookDrag(sh);
                v = sh;
                if (s.Kind == "line")
                {
                    var hit = new Line { Stroke = Brushes.Transparent, StrokeThickness = 14, Tag = el, Cursor = Cursors.SizeAll };
                    HookDrag(hit);
                    _lineHits[s] = hit;
                    Stage.Children.Add(hit);
                }
            }

            var k = el as InkElement;
            if (k != null)
            {
                var pl = new Polyline
                {
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Tag = el,
                    Cursor = Cursors.SizeAll
                };
                HookDrag(pl);
                v = pl;
            }

            var im = el as ImageElement;
            if (im != null)
            {
                var img = new Image { Stretch = Stretch.Fill, Tag = el, Cursor = Cursors.SizeAll };
                try { img.Source = BitmapFromBytes(Convert.FromBase64String(im.Base64)); } catch { }
                HookDrag(img);
                v = img;
            }

            var tr = el as TextRegionElement;
            if (tr != null)
            {
                var bd = new Border
                {
                    BorderBrush = BrushOf("#993556"),
                    BorderThickness = new Thickness(1.4),
                    Background = new SolidColorBrush(Color.FromArgb(20, 212, 83, 126)),
                    Tag = el,
                    Cursor = Cursors.SizeAll
                };
                bd.BorderBrush = new SolidColorBrush(Color.FromRgb(0x99, 0x35, 0x56)) { Opacity = 0.9 };
                var tbk = new TextBlock { Tag = "txt", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 2, 4, 2), IsHitTestVisible = false };
                bd.Child = tbk;
                HookDrag(bd);
                v = bd;
            }

            if (v == null) return;
            _views[el] = v;
            Stage.Children.Add(v);
        }

        // Build a component sample and wire its transparent hit regions to selection.
        // 构建部件示例，并将透明点击区域连接到选择操作。
        private Canvas BuildComponentView(ComponentElement ce)
        {
            var g = ComponentVisuals.Build(ce);
            foreach (var hit in g.Children.OfType<Rectangle>().Where(/* Locate hit rectangles tagged with this component. 查找标记为该部件的点击矩形。 */ r => ReferenceEquals(r.Tag, ce)).ToList())
                hit.MouseLeftButtonDown += /* Select the clicked component and consume its pointer event. 选中被点击部件，并消费指针事件。 */ (s, e) => { SelectElement(ce); e.Handled = true; };
            return g;
        }

        // Replace the component visual after property changes and restore its stacking and selection.
        // 属性变化后替换部件可视对象，并恢复叠放与选择状态。
        internal void RebuildComponentView(ComponentElement ce)
        {
            UIElement old;
            if (_views.TryGetValue(ce, out old)) Stage.Children.Remove(old);
            var g = BuildComponentView(ce);
            g.Visibility = ce.Visible ? Visibility.Visible : Visibility.Collapsed;
            _views[ce] = g;
            Stage.Children.Add(g);
            RebuildZ();
            ApplyMasks();
            if (ReferenceEquals(_sel, ce)) ShowSelectionVisual();
        }

        // Apply element geometry, visibility, opacity, and styling to its existing WPF visual.
        // 将元素几何、显隐、透明度与样式应用到现有 WPF 可视对象。
        internal void UpdateView(CanvasElement el)
        {
            UIElement v;
            if (!_views.TryGetValue(el, out v)) return;
            v.Visibility = el.Visible ? Visibility.Visible : Visibility.Collapsed;

            var s = el as ShapeElement;
            if (s != null)
            {
                var sh = (Shape)v;
                sh.Opacity = s.IsMask ? 1.0 : s.Opacity;
                if (s.Kind == "line")
                {
                    var ln = (Line)sh;
                    ln.X1 = s.X; ln.Y1 = s.Y; ln.X2 = s.X2; ln.Y2 = s.Y2;
                    ln.Stroke = BrushOf(s.Stroke);
                    ln.StrokeThickness = Math.Max(1, s.StrokeW);
                    var hit = _lineHits[s];
                    hit.X1 = s.X; hit.Y1 = s.Y; hit.X2 = s.X2; hit.Y2 = s.Y2;
                    hit.Visibility = v.Visibility;
                }
                else
                {
                    Canvas.SetLeft(sh, s.X);
                    Canvas.SetTop(sh, s.Y);
                    sh.Width = Math.Max(1, s.W);
                    sh.Height = Math.Max(1, s.H);
                    var rc = sh as Rectangle;
                    if (rc != null)
                    {
                        rc.RadiusX = s.Kind == "roundrect" ? s.Radius : 0;
                        rc.RadiusY = rc.RadiusX;
                    }
                    if (s.IsMask)
                    {
                        // 遮罩画成灰虚线半透明块，不参与配色
                        sh.Fill = new SolidColorBrush(Color.FromArgb(36, 110, 115, 130));
                        sh.Stroke = BrushOf("#8a90a0");
                        sh.StrokeThickness = 1.5;
                        sh.StrokeDashArray = new DoubleCollection { 4, 3 };
                    }
                    else
                    {
                        sh.StrokeDashArray = null;
                        sh.Fill = s.NoFill ? Brushes.Transparent : (Brush)BrushOf(s.Fill);
                        sh.Stroke = s.StrokeW > 0 ? BrushOf(s.Stroke) : null;
                        sh.StrokeThickness = s.StrokeW;
                    }
                }
            }

            var k = el as InkElement;
            if (k != null)
            {
                var pl = (Polyline)v;
                pl.Points = new PointCollection(k.Points);
                pl.Stroke = BrushOf(k.Color);
                pl.StrokeThickness = Math.Max(1, k.Width);
                pl.Opacity = k.Opacity;
            }

            var im = el as ImageElement;
            if (im != null)
            {
                Canvas.SetLeft(v, im.X);
                Canvas.SetTop(v, im.Y);
                var img = (Image)v;
                img.Width = Math.Max(4, im.W);
                img.Height = Math.Max(4, im.H);
                img.Opacity = im.Opacity;
            }

            var tr = el as TextRegionElement;
            if (tr != null)
            {
                var bd = (Border)v;
                Canvas.SetLeft(bd, tr.X);
                Canvas.SetTop(bd, tr.Y);
                bd.Width = Math.Max(8, tr.W);
                bd.Height = Math.Max(8, tr.H);
                var tbk = bd.Child as TextBlock;
                if (tbk != null)
                {
                    tbk.Text = string.IsNullOrEmpty(tr.Text) ? "文字" : tr.Text;
                    tbk.FontSize = tr.FontSize;
                    tbk.FontWeight = tr.Bold ? FontWeights.Bold : FontWeights.Normal;
                    tbk.Foreground = BrushOf(tr.Color);
                    tbk.TextAlignment = tr.AlignH == "center" ? TextAlignment.Center : tr.AlignH == "right" ? TextAlignment.Right : TextAlignment.Left;
                    tbk.VerticalAlignment = tr.AlignV == "top" ? VerticalAlignment.Top : tr.AlignV == "bottom" ? VerticalAlignment.Bottom : VerticalAlignment.Center;
                    tbk.HorizontalAlignment = HorizontalAlignment.Stretch;
                }
            }

            if (ReferenceEquals(el, _sel)) ShowSelectionVisual();
        }

        // Assign WPF Z-indices from the bottom-to-top model ordering.
        // 按模型从底到顶的顺序分配 WPF 层级索引。
        internal void RebuildZ()
        {
            for (int i = 0; i < _elements.Count; i++)
            {
                UIElement v;
                if (_views.TryGetValue(_elements[i], out v)) Panel.SetZIndex(v, 10 + i * 2);
                var s = _elements[i] as ShapeElement;
                Line hit;
                if (s != null && _lineHits.TryGetValue(s, out hit)) Panel.SetZIndex(hit, 11 + i * 2);
            }
        }

        // ================= 遮罩 =================
        // Accept drawable non-mask elements as eligible mask targets.
        // 允许非蒙版的可绘制元素作为蒙版目标。
        private static bool IsMaskTarget(CanvasElement el)
        {
            var s = el as ShapeElement;
            if (s != null) return !s.IsMask;
            return el is InkElement || el is ImageElement;
        }

        // Resolve the canvas mask with the same version-specific target rules used by export.
        // 使用与导出一致的本版本目标规则解析画布蒙版。
        private ShapeElement FindMaskForTarget(int targetIndex, CanvasElement target)
        {
            if (target == null || !IsMaskTarget(target)) return null;
            var stable = _elements.OfType<ShapeElement>()
                .LastOrDefault(/* Match visible non-line masks to the stable target ID. 按稳定目标 ID 匹配可见非直线蒙版。 */ m => m.IsMask && m.Visible && m.Kind != "line" && m.MaskTargetId == target.Id);
            if (stable != null) return stable;

            // V0.0.3 及更早工程没有 MaskTargetId：读取后仍允许按旧的相邻规则预览，
            // ModelSafety 在保存/快照时会把它迁移成稳定 ID。
            if (targetIndex + 1 < _elements.Count)
            {
                var legacy = _elements[targetIndex + 1] as ShapeElement;
                if (legacy != null && legacy.IsMask && legacy.Visible && legacy.Kind != "line"
                    && string.IsNullOrEmpty(legacy.MaskTargetId)) return legacy;
            }
            return null;
        }

        // Transform mask geometry into the target visual's coordinates and apply inverse geometry when needed.
        // 将蒙版几何转换为目标可视对象坐标，并在需要时应用反向几何。
        private static Geometry MaskGeometry(ShapeElement mask, CanvasElement target)
        {
            double tx = 0, ty = 0;
            var targetShape = target as ShapeElement;
            if (targetShape != null && targetShape.Kind != "line") { tx = -targetShape.X; ty = -targetShape.Y; }
            var image = target as ImageElement;
            if (image != null) { tx = -image.X; ty = -image.Y; }

            Geometry inside;
            if (mask.Kind == "ellipse")
                inside = new EllipseGeometry(new Point(mask.X + mask.W / 2 + tx, mask.Y + mask.H / 2 + ty), mask.W / 2, mask.H / 2);
            else
                inside = new RectangleGeometry(new Rect(mask.X + tx, mask.Y + ty, Math.Max(1, mask.W), Math.Max(1, mask.H)),
                    mask.Kind == "roundrect" ? mask.Radius : 0,
                    mask.Kind == "roundrect" ? mask.Radius : 0);

            if (mask.MaskMode != "alpha-inverse" && mask.MaskMode != "luminance-inverse") return inside;
            var box = CssBuilder.BBoxOfElement(target);
            double pad = targetShape != null ? Math.Max(2, targetShape.StrokeW + 2) : 2;
            var outer = new RectangleGeometry(new Rect(box.X + tx - pad, box.Y + ty - pad,
                Math.Max(1, box.Width + pad * 2), Math.Max(1, box.Height + pad * 2)));
            return new CombinedGeometry(GeometryCombineMode.Exclude, outer, inside);
        }

        // Refresh clipping and opacity masks on visible canvas elements.
        // 刷新可见画布元素的裁剪与透明度蒙版。
        internal void ApplyMasks()
        {
            for (int i = 0; i < _elements.Count; i++)
            {
                var el = _elements[i];
                UIElement v;
                if (!_views.TryGetValue(el, out v)) continue;
                Geometry clip = null;
                var mask = FindMaskForTarget(i, el);
                if (mask != null) clip = MaskGeometry(mask, el);
                var fe = v as FrameworkElement;
                if (fe != null) fe.Clip = clip;
            }
        }

        // ================= 图层面板 =================
        // Rebuild the reversed layer list and synchronize visibility, mask labels, and selection.
        // 重建逆序图层列表，并同步显隐、蒙版标签与选择状态。
        internal void RefreshLayers()
        {
            _syncingLayers = true;
            LayerList.Items.Clear();
            for (int i = _elements.Count - 1; i >= 0; i--)
            {
                var el = _elements[i];
                var elRef = el;
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                var chk = new CheckBox { IsChecked = el.Visible, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
                chk.Click += /* Record layer visibility changes and refresh masks and preview. 记录图层显隐变化，并刷新蒙版与预览。 */ (s, e) =>
                {
                    PushUndo();
                    elRef.Visible = chk.IsChecked == true;
                    UpdateView(elRef);
                    ApplyMasks();
                    MarkDirty();
                };
                var se = el as ShapeElement;
                string suffix = se != null && se.IsMask ? " · 蒙版/" + MaskModeName(se.MaskMode) : "";
                if (el.IsLocked) suffix += " · 🔒";
                row.Children.Add(chk);
                row.Children.Add(new TextBlock { Text = ElementIcon(el) + " " + el.Name + suffix, VerticalAlignment = VerticalAlignment.Center });
                LayerList.Items.Add(new ListBoxItem { Content = row, Tag = el, IsSelected = _selection.Contains(el) });
            }
            TxtEmptyHint.Visibility = _elements.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            _syncingLayers = false;
        }

        // Map stored mask mode identifiers to user-facing mode labels.
        // 将保存的蒙版模式标识映射为用户可见名称。
        private static string MaskModeName(string mode)
        {
            switch (mode) { case "alpha-inverse": return "反向 Alpha"; case "luminance": return "亮度"; case "luminance-inverse": return "反向亮度"; default: return "Alpha"; }
        }

        // Move selected layers one step while maintaining their relative stacking order.
        // 将选中图层移动一级，并保持它们之间的相对层序。
        internal void MoveSelectedLayer(int delta)
        {
            if (_sel == null) return;
            if (_selection.Any(/* Detect locked members in the selected set. 检测选择集合中的锁定成员。 */ x => x.IsLocked)) { SetStatus("所选图层含锁定项；请先解锁再调整层级。"); return; }
            if (_selection.Count > 1)
            {
                PushUndo();
                var ordered = _selection.OrderBy(/* Order layers by their model stacking index. 按模型层级索引排序图层。 */ x => _elements.IndexOf(x)).ToList();
                if (delta > 0) ordered.Reverse();
                foreach (var item in ordered)
                {
                    int at = _elements.IndexOf(item), to = at + Math.Sign(delta);
                    if (at < 0 || to < 0 || to >= _elements.Count || _selection.Contains(_elements[to])) continue;
                    _elements.RemoveAt(at); _elements.Insert(to, item);
                }
                RebuildZ(); ApplyMasks(); RefreshLayers(); MarkDirty(); return;
            }
            int i = _elements.IndexOf(_sel);
            int j = i + delta;
            if (i < 0 || j < 0 || j >= _elements.Count) return;
            PushUndo();
            _elements.RemoveAt(i);
            _elements.Insert(j, _sel);
            RebuildZ();
            ApplyMasks();
            RefreshLayers();
            MarkDirty();
        }

        // Move the selected layer set to the top or bottom of the artwork stack.
        // 将选中图层集合移至作品图层栈顶部或底部。
        internal void MoveSelectedLayerTo(bool top)
        {
            if (_sel == null) return;
            if (_selection.Any(/* Detect locked members in the selected set. 检测选择集合中的锁定成员。 */ x => x.IsLocked)) { SetStatus("所选图层含锁定项；请先解锁再调整层级。"); return; }
            if (_selection.Count > 1)
            {
                PushUndo(); var ordered = _elements.Where(_selection.Contains).ToList();
                _elements.RemoveAll(_selection.Contains); if (top) _elements.AddRange(ordered); else _elements.InsertRange(0, ordered);
                RebuildZ(); ApplyMasks(); RefreshLayers(); MarkDirty(); return;
            }
            PushUndo();
            if (!_elements.Remove(_sel)) return;
            if (top) _elements.Add(_sel);
            else _elements.Insert(0, _sel);
            RebuildZ();
            ApplyMasks();
            RefreshLayers();
            MarkDirty();
        }

        // Remove all model elements and visuals and clear selection overlays.
        // 删除全部模型元素与可视对象，并清空选择叠加层。
        internal void ClearElements()
        {
            foreach (var el in _elements.ToList()) RemoveElement(el);
            _sel = null;
            _selection.Clear();
            HideShapeAdorner();
            HideRegionHighlight();
        }

        // Remove one element from the model-to-view map and canvas collection.
        // 从模型到视图映射与画布集合中删除一个元素。
        private void RemoveElement(CanvasElement el)
        {
            UIElement v;
            if (_views.TryGetValue(el, out v)) Stage.Children.Remove(v);
            _views.Remove(el);
            var s = el as ShapeElement;
            Line hit;
            if (s != null && _lineHits.TryGetValue(s, out hit))
            {
                Stage.Children.Remove(hit);
                _lineHits.Remove(s);
            }
            _elements.Remove(el);
        }

        // Record undo, delete eligible selected elements, and refresh masks and layer state.
        // 记录撤销、删除允许删除的选中元素，并刷新蒙版与图层状态。
        internal void DeleteSelected()
        {
            var el = _sel; if (el == null) return;
            var targets = (_selection.Count > 0 ? _selection : new HashSet<CanvasElement> { el }).Where(/* Keep only editable unlocked layers. 仅保留可编辑的未锁定图层。 */ x => !x.IsLocked).ToList();
            if (targets.Count == 0) { SetStatus("所选图层已锁定；请先解锁再删除。"); return; }
            PushUndo();
            foreach (var target in targets) RemoveElement(target);
            ClearSelection();
            RebuildZ();
            ApplyMasks();
            RefreshLayers();
            MarkDirty();
            SetStatus(targets.Count == 1 ? "已删除「" + targets[0].Name + "」。" : "已删除 " + targets.Count + " 个图层。");
        }

        // Apply one lock state to the selected layer set and refresh its edit affordances.
        // 为选中图层集合应用统一锁定状态，并刷新编辑操作显示。
        internal void ToggleSelectedLock()
        {
            var targets = _selection.Count > 0 ? _selection.ToList() : (_sel != null ? new List<CanvasElement> { _sel } : new List<CanvasElement>());
            if (targets.Count == 0) return; PushUndo(); bool lockNow = targets.Any(/* Keep only editable unlocked layers. 仅保留可编辑的未锁定图层。 */ x => !x.IsLocked);
            foreach (var item in targets) item.IsLocked = lockNow;
            RefreshLayers(); ShowSelectionVisual(); MarkDirty(); SetStatus(lockNow ? "已锁定所选图层。" : "已解锁所选图层。");
        }

        // Move editable selected elements by a keyboard delta with coalesced undo history.
        // 按键盘位移移动可编辑选中元素，并合并撤销历史。
        internal void NudgeSelected(double dx, double dy)
        {
            if (_selection.Count > 1)
            {
                var targets = _selection.Where(/* Keep only editable unlocked layers. 仅保留可编辑的未锁定图层。 */ x => !x.IsLocked).ToList(); if (targets.Count == 0) { SetStatus("所选图层已锁定。"); return; }
                PushUndo("nudge-group:" + string.Join("-", targets.Select(/* Select stable element IDs for undo grouping. 提取稳定元素 ID 以归组撤销。 */ x => x.Id).OrderBy(/* Sort identifiers by their own value. 按标识符自身值排序。 */ x => x)));
                foreach (var item in targets) { TranslateElement(item, dx, dy); UpdateView(item); }
                ApplyMasks(); ShowSelectionVisual(); MarkDirty(); return;
            }
            if (_sel != null && _sel.IsLocked) { SetStatus("图层已锁定。"); return; }
            if (_sel != null) PushUndo("nudge:" + _sel.Id);
            var s = _sel as ShapeElement;
            if (s != null)
            {
                s.X += dx; s.Y += dy;
                if (s.Kind == "line") { s.X2 += dx; s.Y2 += dy; }
                UpdateView(s); ApplyMasks(); MarkDirty();
                return;
            }
            Rect box;
            if (TryGetBox(_sel, out box)) { SetBox(_sel, box.X + dx, box.Y + dy, box.Width, box.Height); UpdateView(_sel); ApplyMasks(); MarkDirty(); return; }
            var k = _sel as InkElement;
            if (k != null)
            {
                for (int i = 0; i < k.Points.Count; i++) k.Points[i] = new Point(k.Points[i].X + dx, k.Points[i].Y + dy);
                UpdateView(k); ApplyMasks(); MarkDirty();
            }
        }

        // Translate shape endpoints, box geometry, or ink points by a relative delta.
        // 按相对位移平移图形端点、盒子几何或手绘点。
        private static void TranslateElement(CanvasElement el, double dx, double dy)
        {
            var s = el as ShapeElement;if (s != null) { s.X += dx; s.Y += dy; if (s.Kind == "line") { s.X2 += dx; s.Y2 += dy; } return; }
            Rect box;if (TryGetBox(el, out box)) { SetBox(el, box.X + dx, box.Y + dy, box.Width, box.Height); return; }
            var k = el as InkElement;if (k != null) for (int i = 0; i < k.Points.Count; i++) k.Points[i] = new Point(k.Points[i].X + dx, k.Points[i].Y + dy);
        }

        // ================= 选中 =================
        // Make one element the active selection and synchronize its property panel and outline.
        // 将一个元素设为活动选择，并同步属性面板与轮廓。
        internal void SelectElement(CanvasElement el, bool syncLayers = true)
        {
            _selection.Clear();
            if (el != null) _selection.Add(el);
            _sel = el;
            ShowPanelFor(el);
            ShowSelectionVisual();
            if (syncLayers)
            {
                _syncingLayers = true;
                LayerList.UnselectAll();
                foreach (ListBoxItem item in LayerList.Items)
                    item.IsSelected = ReferenceEquals(item.Tag, el);
                _syncingLayers = false;
            }
        }

        // Normalize a multi-selection to existing elements and choose its primary property-panel item.
        // 将多选集合规范为现有元素，并选择属性面板的主元素。
        internal void SetSelection(IEnumerable<CanvasElement> elements, CanvasElement primary = null, bool syncLayers = true)
        {
            _selection.Clear();
            foreach (var el in elements ?? Enumerable.Empty<CanvasElement>()) if (el != null && _elements.Contains(el)) _selection.Add(el);
            _sel = primary != null && _selection.Contains(primary) ? primary : _selection.LastOrDefault();
            if (_selection.Count == 1) ShowPanelFor(_sel);
            else
            {
                PanelComp.Visibility = PanelShape.Visibility = PanelInk.Visibility = PanelImage.Visibility = PanelText.Visibility = Visibility.Collapsed;
                PanelTips.Visibility = Visibility.Visible;
                SetStatus(_selection.Count == 0 ? "未选择图层。" : "已选择 " + _selection.Count + " 个图层；可整体移动、微调、锁定、删除或调整层级。");
            }
            ShowSelectionVisual();
            if (syncLayers)
            {
                _syncingLayers = true;
                foreach (ListBoxItem item in LayerList.Items) item.IsSelected = _selection.Contains(item.Tag as CanvasElement);
                _syncingLayers = false;
            }
        }

        // Add or remove one element from the multi-selection while maintaining a primary item.
        // 在多选集合中添加或移除一个元素，并维护主元素。
        internal void ToggleSelection(CanvasElement el)
        {
            if (el == null) return;
            var next = new HashSet<CanvasElement>(_selection);
            if (!next.Add(el)) next.Remove(el);
            SetSelection(next, next.Contains(el) ? el : next.LastOrDefault());
        }

        // Clear active selection, property-panel visibility, and layer-list selection.
        // 清除活动选择、属性面板显示与图层列表选择。
        internal void ClearSelection()
        {
            _selection.Clear();
            _sel = null;
            HideShapeAdorner();
            HideRegionHighlight();
            _syncingLayers = true;
            LayerList.UnselectAll();
            _syncingLayers = false;
            PanelComp.Visibility = Visibility.Collapsed;
            PanelShape.Visibility = Visibility.Collapsed;
            PanelInk.Visibility = Visibility.Collapsed;
            PanelImage.Visibility = Visibility.Collapsed;
            PanelText.Visibility = Visibility.Collapsed;
            PanelTips.Visibility = Visibility.Visible;
        }

        // Show the component, shape, ink, image, or text panel matching the selected element.
        // 显示与选中元素对应的部件、图形、手绘、图片或文本面板。
        private void ShowPanelFor(CanvasElement el)
        {
            PanelComp.Visibility = Visibility.Collapsed;
            PanelShape.Visibility = Visibility.Collapsed;
            PanelInk.Visibility = Visibility.Collapsed;
            PanelImage.Visibility = Visibility.Collapsed;
            PanelText.Visibility = Visibility.Collapsed;
            PanelTips.Visibility = Visibility.Collapsed;

            var ce = el as ComponentElement;
            if (ce != null)
            {
                BuildCompPanel(ce);
                PanelComp.Visibility = Visibility.Visible;
                SetStatus("正在调整「" + ce.Name + "」——改颜色立即生效；「恢复原版色」=和原版一样（不写进导出）。");
                return;
            }
            var s = el as ShapeElement;
            if (s != null) { SyncShapePanel(); PanelShape.Visibility = Visibility.Visible; return; }
            var k = el as InkElement;
            if (k != null) { SyncInkPanel(); PanelInk.Visibility = Visibility.Visible; return; }
            var im = el as ImageElement;
            if (im != null) { SyncImagePanel(); PanelImage.Visibility = Visibility.Visible; return; }
            var tr = el as TextRegionElement;
            if (tr != null) { SyncTextPanel(); PanelText.Visibility = Visibility.Visible; return; }
            PanelTips.Visibility = Visibility.Visible;
        }

        // Display bounds and resize handles appropriate to the selected element or group.
        // 显示适合选中元素或组合的边界与缩放手柄。
        private void ShowSelectionVisual()
        {
            HideShapeAdorner();
            HideRegionHighlight();
            if (_sel == null) return;
            if (_selection.Count > 1)
            {
                Rect union = SelectionBounds(_selection.Where(/* Keep elements currently visible on the canvas. 保留当前在画布可见的元素。 */ x => x.Visible));
                bool canResize = _selection.All(/* Allow grouped resizing only for unlocked non-component elements. 仅允许未锁定且非部件元素参与组合缩放。 */ x => !x.IsLocked && !(x is ComponentElement));
                if (!union.IsEmpty) ShowOutline(union, canResize);
                return;
            }

            var ce = _sel as ComponentElement;
            if (ce != null)
            {
                var def = ComponentLib.Find(ce.CompId);
                if (def != null) ShowBoxHighlights(def.Boxes);
                return;
            }
            var s = _sel as ShapeElement;
            if (s != null && s.Kind == "line")
            {
                SetCenter(_ends[0], s.X, s.Y);
                SetCenter(_ends[1], s.X2, s.Y2);
                return;
            }
            var k = _sel as InkElement;
            if (k != null)
            {
                ShowOutline(CssBuilder.InkBBox(k), false);
                return;
            }
            Rect r;
            if (s != null) r = new Rect(s.X, s.Y, Math.Max(1, s.W), Math.Max(1, s.H));
            else if (TryGetBox(_sel, out r)) { }
            else return;
            ShowOutline(r, true);
        }

        // 盒状元素（图片 / 文本区）统一的取/设边框
        // Read rectangular bounds from box-based element types without changing them.
        // 读取基于盒子的元素类型矩形边界，不修改元素。
        private static bool TryGetBox(CanvasElement el, out Rect r)
        {
            var im = el as ImageElement;
            if (im != null) { r = new Rect(im.X, im.Y, Math.Max(1, im.W), Math.Max(1, im.H)); return true; }
            var tr = el as TextRegionElement;
            if (tr != null) { r = new Rect(tr.X, tr.Y, Math.Max(1, tr.W), Math.Max(1, tr.H)); return true; }
            r = default(Rect); return false;
        }

        // Write rectangular geometry back to the supported box-based element type.
        // 将矩形几何写回受支持的盒子类元素。
        private static void SetBox(CanvasElement el, double x, double y, double w, double h)
        {
            var im = el as ImageElement;
            if (im != null) { im.X = x; im.Y = y; im.W = w; im.H = h; return; }
            var tr = el as TextRegionElement;
            if (tr != null) { tr.X = x; tr.Y = y; tr.W = w; tr.H = h; }
        }

        // Union the bounds of selected elements into one group rectangle.
        // 将选中元素边界合并为一个组合矩形。
        private static Rect SelectionBounds(IEnumerable<CanvasElement> elements)
        {
            Rect result = Rect.Empty;
            foreach (var item in elements ?? Enumerable.Empty<CanvasElement>())
            {
                var box = CssBuilder.BBoxOfElement(item);
                if (result.IsEmpty) result = box; else result.Union(box);
            }
            return result;
        }

        // Scale each captured element from the original group bounds into the resized group bounds.
        // 将每个已捕获元素从原组合边界缩放到新组合边界。
        private static void ResizeFromOrigin(CanvasElement el, MoveOrigin o, Rect from, Rect to)
        {
            if (from.Width <= 0 || from.Height <= 0) return;
            double sx = to.Width / from.Width, sy = to.Height / from.Height;
            Func<double, double> mapX = /* Map an original X coordinate into resized group bounds. 将原 X 坐标映射至缩放后的组合边界。 */ x => to.X + (x - from.X) * sx;
            Func<double, double> mapY = /* Map an original Y coordinate into resized group bounds. 将原 Y 坐标映射至缩放后的组合边界。 */ y => to.Y + (y - from.Y) * sy;
            var s = el as ShapeElement;
            if (s != null)
            {
                if (s.Kind == "line") { s.X = mapX(o.X); s.Y = mapY(o.Y); s.X2 = mapX(o.X2); s.Y2 = mapY(o.Y2); }
                else
                {
                    s.X = mapX(o.X); s.Y = mapY(o.Y); s.W = Math.Max(1, o.W * sx); s.H = Math.Max(1, o.H * sy);
                    if (s.LockAspect) { double m = Math.Max(s.W, s.H); s.W = m; s.H = m; }
                }
                return;
            }
            if (el is ImageElement || el is TextRegionElement)
            {
                SetBox(el, mapX(o.X), mapY(o.Y), Math.Max(1, o.W * sx), Math.Max(1, o.H * sy));
                return;
            }
            var ink = el as InkElement;
            if (ink != null && o.Points != null)
                for (int i = 0; i < ink.Points.Count && i < o.Points.Count; i++) ink.Points[i] = new Point(mapX(o.Points[i].X), mapY(o.Points[i].Y));
        }

        // Begin blank-canvas marquee selection and capture the drag origin.
        // 开始空白画布框选，并记录拖动起点。
        private void MarqueeDown(object sender, MouseButtonEventArgs e)
        {
            if (_tool != null || e.ChangedButton != MouseButton.Left) return;
            _marqueeStart = e.GetPosition(Stage);
            _marqueeBase = (Keyboard.Modifiers & ModifierKeys.Control) != 0
                ? new HashSet<CanvasElement>(_selection) : new HashSet<CanvasElement>();
            if (_marqueeBase.Count == 0) ClearSelection();
            _marqueeActive = true;
            Canvas.SetLeft(_marquee, _marqueeStart.X); Canvas.SetTop(_marquee, _marqueeStart.Y);
            _marquee.Width = _marquee.Height = 0; _marquee.Visibility = Visibility.Visible;
            Stage.CaptureMouse(); Focus(); e.Handled = true;
        }

        // Resize the marquee rectangle while the selection drag is active.
        // 在框选拖动期间调整框选矩形尺寸。
        private void MarqueeMove(object sender, MouseEventArgs e)
        {
            if (!_marqueeActive || e.LeftButton != MouseButtonState.Pressed) return;
            var p = e.GetPosition(Stage);
            double x = Math.Min(p.X, _marqueeStart.X), y = Math.Min(p.Y, _marqueeStart.Y);
            Canvas.SetLeft(_marquee, x); Canvas.SetTop(_marquee, y);
            _marquee.Width = Math.Abs(p.X - _marqueeStart.X); _marquee.Height = Math.Abs(p.Y - _marqueeStart.Y);
        }

        // Select visible elements intersecting the completed marquee and release pointer capture.
        // 选中与完成框选区域相交的可见元素，并释放指针捕获。
        private void MarqueeUp(object sender, MouseButtonEventArgs e)
        {
            if (!_marqueeActive) return;
            Stage.ReleaseMouseCapture(); _marqueeActive = false; _marquee.Visibility = Visibility.Collapsed;
            var p = e.GetPosition(Stage);
            var box = new Rect(new Point(Math.Min(p.X, _marqueeStart.X), Math.Min(p.Y, _marqueeStart.Y)),
                new Point(Math.Max(p.X, _marqueeStart.X), Math.Max(p.Y, _marqueeStart.Y)));
            var next = new HashSet<CanvasElement>(_marqueeBase);
            if (box.Width >= 3 || box.Height >= 3)
                foreach (var item in _elements.Where(/* Keep elements currently visible on the canvas. 保留当前在画布可见的元素。 */ x => x.Visible))
                    if (CssBuilder.BBoxOfElement(item).IntersectsWith(box)) next.Add(item);
            SetSelection(next, next.LastOrDefault());
            e.Handled = true;
        }

        // Inflate a selection rectangle and position its resize handles around the bounds.
        // 外扩选择矩形，并在边界周围定位缩放手柄。
        private void ShowOutline(Rect r, bool withHandles)
        {
            r.Inflate(3, 3);
            Canvas.SetLeft(_outline, r.X);
            Canvas.SetTop(_outline, r.Y);
            _outline.Width = r.Width;
            _outline.Height = r.Height;
            _outline.Visibility = Visibility.Visible;
            if (!withHandles) return;
            double cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            SetCenter(_handles[0], r.Left, r.Top);
            SetCenter(_handles[1], cx, r.Top);
            SetCenter(_handles[2], r.Right, r.Top);
            SetCenter(_handles[3], r.Right, cy);
            SetCenter(_handles[4], r.Right, r.Bottom);
            SetCenter(_handles[5], cx, r.Bottom);
            SetCenter(_handles[6], r.Left, r.Bottom);
            SetCenter(_handles[7], r.Left, cy);
        }

        // Position a selection handle around its requested center point.
        // 以指定中心点定位选择手柄。
        private static void SetCenter(Rectangle h, double cx, double cy)
        {
            Canvas.SetLeft(h, cx - h.Width / 2);
            Canvas.SetTop(h, cy - h.Height / 2);
            h.Visibility = Visibility.Visible;
        }

        // Hide the selection outline, handles, and snapping guides.
        // 隐藏选择轮廓、手柄与吸附辅助线。
        internal void HideShapeAdorner()
        {
            if (_outline == null) return;
            _outline.Visibility = Visibility.Collapsed;
            foreach (var h in _handles) h.Visibility = Visibility.Collapsed;
            foreach (var h in _ends) h.Visibility = Visibility.Collapsed;
            HideGuides();
        }

        // Overlay highlighted rectangles for fixed component hit regions.
        // 为固定部件点击区域叠加高亮矩形。
        private void ShowBoxHighlights(Rect[] boxes)
        {
            foreach (var box in boxes)
            {
                var rc = new Rectangle
                {
                    Stroke = BrushOf("#7b6cf0"),
                    StrokeThickness = 1.6,
                    StrokeDashArray = new DoubleCollection { 5, 3 },
                    IsHitTestVisible = false,
                    Width = box.Width + 6,
                    Height = box.Height + 6
                };
                Canvas.SetLeft(rc, box.X - 3);
                Canvas.SetTop(rc, box.Y - 3);
                Overlay.Children.Add(rc);
                Panel.SetZIndex(rc, 998);
                _regionRects.Add(rc);
            }
        }

        // Remove the temporary component-region highlight rectangles.
        // 移除临时部件区域高亮矩形。
        internal void HideRegionHighlight()
        {
            foreach (var rc in _regionRects) Overlay.Children.Remove(rc);
            _regionRects.Clear();
        }

        // ================= 拖动 / 缩放 =================
        // Attach element pointer events to shared drag and resize handlers.
        // 将元素指针事件连接到共享拖动与缩放处理器。
        private void HookDrag(UIElement el)
        {
            el.MouseLeftButtonDown += ElDown;
            el.MouseMove += DragMove;
            el.MouseLeftButtonUp += DragUp;
        }

        // Cache the active element geometry before a single-element drag or resize.
        // 在单元素拖动或缩放前缓存活动元素几何。
        private void StoreOrig(CanvasElement el)
        {
            var s = el as ShapeElement;
            if (s != null) { _oX = s.X; _oY = s.Y; _oW = s.W; _oH = s.H; _oX2 = s.X2; _oY2 = s.Y2; return; }
            Rect bx;
            if (TryGetBox(el, out bx)) { _oX = bx.X; _oY = bx.Y; _oW = bx.Width; _oH = bx.Height; return; }
            var k = el as InkElement;
            if (k != null)
            {
                _inkOrig = new List<Point>(k.Points);
                var r = CssBuilder.InkBBox(k);
                _oX = r.X; _oY = r.Y; _oW = r.Width; _oH = r.Height;
            }
        }

        // Snapshot one element's geometry for a grouped drag or resize.
        // 为组合拖动或缩放记录一个元素的几何快照。
        private static MoveOrigin CaptureOrigin(CanvasElement el)
        {
            var o = new MoveOrigin(); var s = el as ShapeElement;
            if (s != null) { o.X = s.X; o.Y = s.Y; o.X2 = s.X2; o.Y2 = s.Y2; o.W = s.W; o.H = s.H; return o; }
            Rect box;if (TryGetBox(el, out box)) { o.X = box.X; o.Y = box.Y; o.W = box.Width; o.H = box.Height; return o; }
            var k = el as InkElement;if (k != null) { o.Points = new List<Point>(k.Points); var r = CssBuilder.InkBBox(k); o.X = r.X; o.Y = r.Y; o.W = r.Width; o.H = r.Height; }
            return o;
        }

        // Recompute element coordinates from the captured origin plus the current drag delta.
        // 根据捕获起点与当前拖动位移重新计算元素坐标。
        private static void MoveFromOrigin(CanvasElement el, MoveOrigin o, double dx, double dy)
        {
            var s = el as ShapeElement;if (s != null) { s.X = o.X + dx; s.Y = o.Y + dy; if (s.Kind == "line") { s.X2 = o.X2 + dx; s.Y2 = o.Y2 + dy; } return; }
            Rect box;if (TryGetBox(el, out box)) { SetBox(el, o.X + dx, o.Y + dy, o.W, o.H); return; }
            var k = el as InkElement;if (k != null && o.Points != null) for (int i = 0; i < k.Points.Count && i < o.Points.Count; i++) k.Points[i] = new Point(o.Points[i].X + dx, o.Points[i].Y + dy);
        }

        // Resolve the clicked element, update selection, and capture permitted drag origins.
        // 解析被点击元素、更新选择，并捕获允许拖动的起始状态。
        private void ElDown(object sender, MouseButtonEventArgs e)
        {
            var el = ((FrameworkElement)sender).Tag as CanvasElement;
            if (el == null) return;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) { ToggleSelection(el); e.Handled = true; return; }
            if (!_selection.Contains(el)) SelectElement(el);
            else { _sel = el; if (_selection.Count == 1) ShowPanelFor(el); ShowSelectionVisual(); }
            if (el.IsLocked) { SetStatus("「" + el.Name + "」已锁定；仍可选择，但不能拖动或缩放。"); e.Handled = true; return; }
            _moving = true;
            _handle = -1;
            _down = e.GetPosition(Stage);
            StoreOrig(el);
            _moveOrigins.Clear(); foreach (var item in _selection.Where(/* Keep only editable unlocked layers. 仅保留可编辑的未锁定图层。 */ x => !x.IsLocked)) _moveOrigins[item] = CaptureOrigin(item);
            _dragUndoPending = true;
            ((UIElement)sender).CaptureMouse();
            Focus();   // 让 Delete / 方向键立即可用
            e.Handled = true;
        }

        // Start resize from a selection handle and capture original element or group bounds.
        // 从选择手柄开始缩放，并捕获原元素或组合边界。
        private void HandleDown(object sender, MouseButtonEventArgs e)
        {
            if (_sel == null || _sel.IsLocked) return;
            if (_selection.Count > 1 && _selection.Any(/* Detect locked or fixed component members that block grouped resizing. 检测阻止组合缩放的锁定或固定部件成员。 */ x => x.IsLocked || x is ComponentElement))
            {
                SetStatus("组合内含锁定层或固定原版部件，不能整体缩放。");
                return;
            }
            _handle = (int)((FrameworkElement)sender).Tag;
            _moving = false;
            _down = e.GetPosition(Stage);
            if (_selection.Count > 1)
            {
                _groupResizeBox = SelectionBounds(_selection);
                _moveOrigins.Clear(); foreach (var item in _selection) _moveOrigins[item] = CaptureOrigin(item);
            }
            else { _groupResizeBox = Rect.Empty; StoreOrig(_sel); }
            _dragUndoPending = true;
            ((UIElement)sender).CaptureMouse();
            e.Handled = true;
        }

        // Apply drag or resize geometry, snapping, and mask updates while the pointer is held.
        // 按住指针期间应用拖动或缩放几何、吸附及蒙版更新。
        private void DragMove(object sender, MouseEventArgs e)
        {
            if (!_moving && _handle < 0) return;
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (_sel == null) return;

            if (_dragUndoPending) { PushUndo(); _dragUndoPending = false; }   // 真动了才记一步撤销
            var p = e.GetPosition(Stage);
            double dx = p.X - _down.X, dy = p.Y - _down.Y;
            var cands = SnapCands(_sel);
            double gx = double.NaN, gy = double.NaN;

            if (!_moving && _handle >= 0 && _handle < 8 && _selection.Count > 1 && !_groupResizeBox.IsEmpty)
            {
                bool left = _handle == 0 || _handle == 6 || _handle == 7;
                bool right = _handle == 2 || _handle == 3 || _handle == 4;
                bool top = _handle == 0 || _handle == 1 || _handle == 2;
                bool bottom = _handle == 4 || _handle == 5 || _handle == 6;
                double l = _groupResizeBox.Left, t = _groupResizeBox.Top, r = _groupResizeBox.Right, b = _groupResizeBox.Bottom;
                if (left) l = Math.Min(r - 10, l + dx);
                if (right) r = Math.Max(l + 10, r + dx);
                if (top) t = Math.Min(b - 10, t + dy);
                if (bottom) b = Math.Max(t + 10, b + dy);
                var resized = new Rect(l, t, r - l, b - t);
                foreach (var kv in _moveOrigins) { ResizeFromOrigin(kv.Key, kv.Value, _groupResizeBox, resized); UpdateView(kv.Key); }
                ApplyMasks(); ShowSelectionVisual(); return;
            }

            if (_moving && _selection.Count > 1)
            {
                foreach (var kv in _moveOrigins) { MoveFromOrigin(kv.Key, kv.Value, dx, dy); UpdateView(kv.Key); }
                ApplyMasks(); ShowSelectionVisual(); return;
            }

            var s = _sel as ShapeElement;
            bool box = TryGetBox(_sel, out _);   // 图片 / 文本区
            var k = _sel as InkElement;

            if (_moving)
            {
                if (s != null && s.Kind == "line")
                {
                    double nx = _oX + dx, ny = _oY + dy, nx2 = _oX2 + dx, ny2 = _oY2 + dy;
                    double ddx = SnapDelta(new[] { nx, nx2, (nx + nx2) / 2 }, cands.xs, ref gx);
                    double ddy = SnapDelta(new[] { ny, ny2, (ny + ny2) / 2 }, cands.ys, ref gy);
                    s.X = nx + ddx; s.Y = ny + ddy; s.X2 = nx2 + ddx; s.Y2 = ny2 + ddy;
                }
                else if (s != null || box)
                {
                    double w = s != null ? s.W : _oW, h = s != null ? s.H : _oH;
                    double nx = _oX + dx, ny = _oY + dy;
                    nx += SnapDelta(new[] { nx, nx + w / 2, nx + w }, cands.xs, ref gx);
                    ny += SnapDelta(new[] { ny, ny + h / 2, ny + h }, cands.ys, ref gy);
                    if (s != null) { s.X = nx; s.Y = ny; }
                    else SetBox(_sel, nx, ny, w, h);
                }
                else if (k != null && _inkOrig != null)
                {
                    double nx = _oX + dx, ny = _oY + dy;
                    double ddx = SnapDelta(new[] { nx, nx + _oW / 2, nx + _oW }, cands.xs, ref gx);
                    double ddy = SnapDelta(new[] { ny, ny + _oH / 2, ny + _oH }, cands.ys, ref gy);
                    double tx = dx + ddx, ty = dy + ddy;
                    for (int i = 0; i < k.Points.Count && i < _inkOrig.Count; i++)
                        k.Points[i] = new Point(_inkOrig[i].X + tx, _inkOrig[i].Y + ty);
                }
            }
            else if (_handle >= 100 && s != null)
            {
                double nx = (_handle == 100 ? _oX : _oX2) + dx;
                double ny = (_handle == 100 ? _oY : _oY2) + dy;
                nx += SnapDelta(new[] { nx }, cands.xs, ref gx);
                ny += SnapDelta(new[] { ny }, cands.ys, ref gy);
                if (_handle == 100) { s.X = nx; s.Y = ny; }
                else { s.X2 = nx; s.Y2 = ny; }
            }
            else if (s != null || box)
            {
                bool left = _handle == 0 || _handle == 6 || _handle == 7;
                bool right = _handle == 2 || _handle == 3 || _handle == 4;
                bool top = _handle == 0 || _handle == 1 || _handle == 2;
                bool bottom = _handle == 4 || _handle == 5 || _handle == 6;

                double l = _oX, t = _oY, r = _oX + _oW, b2 = _oY + _oH;
                if (left) { l = _oX + dx; l += SnapDelta(new[] { l }, cands.xs, ref gx); if (r - l < 10) l = r - 10; }
                if (right) { r = _oX + _oW + dx; r += SnapDelta(new[] { r }, cands.xs, ref gx); if (r - l < 10) r = l + 10; }
                if (top) { t = _oY + dy; t += SnapDelta(new[] { t }, cands.ys, ref gy); if (b2 - t < 10) t = b2 - 10; }
                if (bottom) { b2 = _oY + _oH + dy; b2 += SnapDelta(new[] { b2 }, cands.ys, ref gy); if (b2 - t < 10) b2 = t + 10; }
                if (s != null) { s.X = l; s.Y = t; s.W = r - l; s.H = b2 - t; }
                else SetBox(_sel, l, t, r - l, b2 - t);
                if (s != null && s.LockAspect && s.Kind != "line") { double mm = Math.Max(s.W, s.H); s.W = mm; s.H = mm; }
            }

            ShowGuides(gx, gy);
            UpdateView(_sel);
            ApplyMasks();   // 遮罩或被裁层移动时实时更新裁剪
        }

        // Release pointer capture, clear drag guides, and commit the edit's dirty state.
        // 释放指针捕获、清除拖动辅助线，并提交修改状态。
        private void DragUp(object sender, MouseButtonEventArgs e)
        {
            if (!_moving && _handle < 0) return;
            ((UIElement)sender).ReleaseMouseCapture();
            bool changed = !_dragUndoPending;
            _moving = false;
            _handle = -1;
            _groupResizeBox = Rect.Empty;
            HideGuides();
            if (changed) MarkDirty();
            if (_sel is ShapeElement && PanelShape.Visibility == Visibility.Visible) SyncShapePanel();   // 拖完更新尺寸数值
            e.Handled = true;
        }

        // ================= 吸附 =================
        // Collect canvas and other-element edges and centers as alignment candidates.
        // 收集画布及其他元素的边缘与中心作为对齐候选。
        private (List<double> xs, List<double> ys) SnapCands(CanvasElement except)
        {
            var xs = new List<double> { 0, 600, 1200 };
            var ys = new List<double> { 0, 350, 700 };
            // 设计框的左/中/右、上/中/下（水平和垂直都能吸附）
            var f = ActiveTarget.FrameRect;
            xs.Add(f.X); xs.Add(f.X + f.Width / 2); xs.Add(f.Right);
            ys.Add(f.Y); ys.Add(f.Y + f.Height / 2); ys.Add(f.Bottom);
            foreach (var el in _elements)
            {
                if (ReferenceEquals(el, except) || !el.Visible) continue;
                var ce = el as ComponentElement;
                if (ce != null)
                {
                    var def = ComponentLib.Find(ce.CompId);
                    if (def != null)
                        foreach (var b in def.Boxes)
                        {
                            xs.Add(b.X); xs.Add(b.X + b.Width / 2); xs.Add(b.Right);
                            ys.Add(b.Y); ys.Add(b.Y + b.Height / 2); ys.Add(b.Bottom);
                        }
                    continue;
                }
                var r = CssBuilder.BBoxOfElement(el);
                xs.Add(r.X); xs.Add(r.X + r.Width / 2); xs.Add(r.Right);
                ys.Add(r.Y); ys.Add(r.Y + r.Height / 2); ys.Add(r.Bottom);
            }
            return (xs, ys);
        }

        // Choose the nearest eligible alignment delta and expose its guide coordinate.
        // 选择最近的有效对齐位移，并提供辅助线坐标。
        private static double SnapDelta(IEnumerable<double> edges, List<double> cands, ref double guide)
        {
            double best = double.NaN;
            double g = guide;
            foreach (var e in edges)
            {
                foreach (var c in cands)
                {
                    double d = c - e;
                    if (Math.Abs(d) <= SnapTol && (double.IsNaN(best) || Math.Abs(d) < Math.Abs(best)))
                    {
                        best = d; g = c;
                    }
                }
            }
            if (double.IsNaN(best)) return 0;
            guide = g;
            return best;
        }

        // Display horizontal and vertical guides only for active snap coordinates.
        // 仅为活动吸附坐标显示水平与垂直辅助线。
        private void ShowGuides(double gx, double gy)
        {
            if (!double.IsNaN(gx)) { _vGuide.X1 = _vGuide.X2 = gx; _vGuide.Visibility = Visibility.Visible; }
            else _vGuide.Visibility = Visibility.Collapsed;
            if (!double.IsNaN(gy)) { _hGuide.Y1 = _hGuide.Y2 = gy; _hGuide.Visibility = Visibility.Visible; }
            else _hGuide.Visibility = Visibility.Collapsed;
        }

        // Hide both snapping-guide lines at the end of alignment feedback.
        // 对齐反馈结束时隐藏两条吸附辅助线。
        private void HideGuides()
        {
            _vGuide.Visibility = Visibility.Collapsed;
            _hGuide.Visibility = Visibility.Collapsed;
        }

        // ================= 画笔 =================
        // Connect pen enablement, color, and width controls to live ink drawing settings.
        // 将画笔开关、颜色与宽度控件连接到实时手绘设置。
        private void WirePen()
        {
            BtnPen.Checked += /* Show ink input and pen controls in canvas mode. 在画布模式显示手绘输入与画笔控件。 */ (s, e) =>
            {
                RbViewCanvas.IsChecked = true;
                Pen.Visibility = Visibility.Visible;
                PanelPen.Visibility = Visibility.Visible;
                UpdatePenAttrs();
                SetStatus("画笔模式：直接在画布上画，松手一笔=一个图层。再点一次「画笔」退出。");
            };
            BtnPen.Unchecked += /* Hide ink input and pen controls after pen mode is disabled. 画笔模式关闭后隐藏手绘输入与画笔控件。 */ (s, e) =>
            {
                Pen.Visibility = Visibility.Collapsed;
                PanelPen.Visibility = Visibility.Collapsed;
            };
            TxtPen.TextChanged += /* Update the pen swatch and drawing color from hex input. 根据十六进制输入更新画笔色块与绘制颜色。 */ (s, e) =>
            {
                string n = ColorUtil.NormalizeHex(TxtPen.Text);
                if (n != null) SetSwatch(SwPen, n);
                UpdatePenAttrs();
            };
            BtnPenPick.Click += /* Choose the live drawing-pen color. 选择实时绘制画笔颜色。 */ (s, e) => PickColor(TxtPen);
            SldPenW.ValueChanged += /* Display and apply the drawing-pen width. 显示并应用绘制画笔宽度。 */ (s, e) =>
            {
                if (LblPenW != null) LblPenW.Text = "画笔粗细：" + Math.Round(SldPenW.Value, 1).ToString("0.#");
                UpdatePenAttrs();
            };
            Pen.StrokeCollected += OnStrokeCollected;
            SetSwatch(SwPen, "#5b8def");
        }

        // Apply normalized pen color and width to the WPF ink drawing attributes.
        // 将规范画笔颜色与宽度应用到 WPF 手绘属性。
        private void UpdatePenAttrs()
        {
            if (Pen == null) return;
            string hex = ColorUtil.NormalizeHex(TxtPen.Text) ?? "#5b8def";
            var c = ColorUtil.ParseHex(hex).Value;
            Pen.DefaultDrawingAttributes = new DrawingAttributes
            {
                Color = Color.FromRgb(c.R, c.G, c.B),
                Width = SldPenW.Value,
                Height = SldPenW.Value,
                FitToCurve = true
            };
        }

        // Sample the completed WPF stroke into a persistent ink element and remove the temporary stroke.
        // 将完成的 WPF 笔画采样为持久手绘元素，并移除临时笔画。
        private void OnStrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
        {
            var pts = new List<Point>();
            var last = new Point(double.MinValue, double.MinValue);
            foreach (var sp in e.Stroke.StylusPoints)
            {
                var p = new Point(sp.X, sp.Y);
                if (pts.Count == 0 || Math.Abs(p.X - last.X) + Math.Abs(p.Y - last.Y) >= 1.5)
                {
                    pts.Add(p);
                    last = p;
                }
            }
            Pen.Strokes.Remove(e.Stroke);
            if (pts.Count < 2) return;
            var k = new InkElement
            {
                Name = "手绘 " + (++_inkCounter),
                Points = pts,
                Color = ColorUtil.NormalizeHex(TxtPen.Text) ?? "#5b8def",
                Width = Math.Round(SldPenW.Value, 1)
            };
            AddElement(k);
        }

        // ================= 图形属性面板 =================
        // Bind layer selection and ordering plus shape geometry, fill, stroke, and mask controls.
        // 绑定图层选择与排序，以及图形几何、填充、描边和蒙版控件。
        private void WireShapePanel()
        {
            LayerList.SelectionChanged += /* Synchronize multi-layer list selection with the canvas selection. 将多图层列表选择与画布选择同步。 */ (s, e) =>
            {
                if (_syncingLayers) return;
                var selected = LayerList.SelectedItems.OfType<ListBoxItem>().Select(/* Read the model element stored in a layer row's tag. 读取图层行标签中保存的模型元素。 */ x => x.Tag as CanvasElement).Where(/* Discard unsupported or absent decoded elements. 丢弃不支持或缺失的解码元素。 */ x => x != null).ToList();
                var added = e.AddedItems.OfType<ListBoxItem>().LastOrDefault();
                SetSelection(selected, added != null ? added.Tag as CanvasElement : selected.LastOrDefault(), false);
            };
            LayerList.PreviewMouseLeftButtonDown += /* Capture a layer-reordering origin while leaving visibility checkboxes independent. 捕获图层重排起点，并保持显隐复选框独立操作。 */ (s, e) =>
            {
                if (FindVisualParent<CheckBox>(e.OriginalSource as DependencyObject) != null) return;
                _layerDragStart = e.GetPosition(LayerList);
                _layerDragItems.Clear();
            };
            LayerList.PreviewMouseMove += /* Start dragging selected unlocked layers after the movement threshold is exceeded. 移动超过阈值后开始拖动选中的未锁定图层。 */ (s, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed || _layerDragItems.Count > 0) return;
                var p = e.GetPosition(LayerList);
                if (Math.Abs(p.X - _layerDragStart.X) < SystemParameters.MinimumHorizontalDragDistance
                    && Math.Abs(p.Y - _layerDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                _layerDragItems = LayerList.SelectedItems.OfType<ListBoxItem>()
                    .Select(/* Read the model element stored in a layer row's tag. 读取图层行标签中保存的模型元素。 */ x => x.Tag as CanvasElement).Where(/* Discard unsupported or absent decoded elements. 丢弃不支持或缺失的解码元素。 */ x => x != null).Distinct().ToList();
                if (_layerDragItems.Count == 0 || _layerDragItems.Any(/* Detect locked members in the selected set. 检测选择集合中的锁定成员。 */ x => x.IsLocked))
                {
                    if (_layerDragItems.Any(/* Detect locked members in the selected set. 检测选择集合中的锁定成员。 */ x => x.IsLocked)) SetStatus("锁定图层不能拖拽排序。");
                    _layerDragItems.Clear(); return;
                }
                var data = new DataObject("KnotJotUiEditor.LayerDrag", "move");
                DragDrop.DoDragDrop(LayerList, data, DragDropEffects.Move);
                _layerDragItems.Clear();
            };
            LayerList.DragOver += /* Accept the editor's layer-drag format as a move operation. 将编辑器图层拖动格式接受为移动操作。 */ (s, e) =>
            {
                e.Effects = e.Data.GetDataPresent("KnotJotUiEditor.LayerDrag") ? DragDropEffects.Move : DragDropEffects.None;
                e.Handled = true;
            };
            LayerList.Drop += /* Insert dragged layers at the drop position and refresh ordering, masks, and selection. 在放置位置插入拖动图层，并刷新层序、蒙版与选择。 */ (s, e) =>
            {
                if (!e.Data.GetDataPresent("KnotJotUiEditor.LayerDrag") || _layerDragItems.Count == 0) return;
                var row = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
                var target = row != null ? row.Tag as CanvasElement : null;
                if (target == null || _layerDragItems.Contains(target)) return;
                bool visualAbove = e.GetPosition(row).Y < Math.Max(1, row.ActualHeight) / 2;
                var moving = _elements.Where(_layerDragItems.Contains).ToList();
                PushUndo(); _elements.RemoveAll(_layerDragItems.Contains);
                int at = _elements.IndexOf(target); if (at < 0) return;
                int insert = Math.Max(0, Math.Min(_elements.Count, at + (visualAbove ? 1 : 0)));
                _elements.InsertRange(insert, moving);
                SetSelection(moving, moving.LastOrDefault(), false);
                RebuildZ(); ApplyMasks(); RefreshLayers(); ShowSelectionVisual(); MarkDirty();
                SetStatus("已拖拽调整 " + moving.Count + " 个图层的层级。"); e.Handled = true;
            };

            TxtFill.TextChanged += /* Validate and apply the shape fill color. 验证并应用图形填充颜色。 */ (s, e) =>
            {
                var sh = _sel as ShapeElement;
                if (_syncingShape || sh == null) return;
                string n = ColorUtil.NormalizeHex(TxtFill.Text);
                if (n == null) return;
                PushUndo("fill:" + sh.GetHashCode());
                sh.Fill = n;
                SetSwatch(SwFill, n);
                UpdateView(sh);
                MarkDirty();
            };
            TxtStroke.TextChanged += /* Validate and apply the shape stroke color. 验证并应用图形描边颜色。 */ (s, e) =>
            {
                var sh = _sel as ShapeElement;
                if (_syncingShape || sh == null) return;
                string n = ColorUtil.NormalizeHex(TxtStroke.Text);
                if (n == null) return;
                PushUndo("stroke:" + sh.GetHashCode());
                sh.Stroke = n;
                SetSwatch(SwStroke, n);
                UpdateView(sh);
                MarkDirty();
            };
            ChkNoFill.Click += /* Toggle whether the selected shape has a fill. 切换选中图形是否填充。 */ (s, e) =>
            {
                var sh = _sel as ShapeElement;
                if (_syncingShape || sh == null) return;
                PushUndo();
                sh.NoFill = ChkNoFill.IsChecked == true;
                if (sh.NoFill && sh.StrokeW <= 0)
                {
                    sh.StrokeW = 2;   // 全透明会看不见，自动补个边框
                    SyncShapePanel();
                }
                UpdateView(sh);
                MarkDirty();
            };
            SldStrokeW.ValueChanged += /* Apply the stroke-width slider with coalesced undo. 应用描边宽度滑块并合并撤销。 */ (s, e) =>
            {
                var sh = _sel as ShapeElement;
                if (_syncingShape || sh == null) return;
                PushUndo("strokew:" + sh.GetHashCode());
                sh.StrokeW = Math.Round(SldStrokeW.Value, 1);
                LblStrokeW.Text = (sh.Kind == "line" ? "线条粗细：" : "边框粗细：") + sh.StrokeW.ToString("0.#");
                UpdateView(sh);
                MarkDirty();
            };
            SldRadius.ValueChanged += /* Apply the corner-radius slider with coalesced undo. 应用圆角半径滑块并合并撤销。 */ (s, e) =>
            {
                var sh = _sel as ShapeElement;
                if (_syncingShape || sh == null) return;
                PushUndo("radius:" + sh.GetHashCode());
                sh.Radius = Math.Round(SldRadius.Value);
                LblRadius.Text = "圆角：" + sh.Radius.ToString("0");
                UpdateView(sh);
                ApplyMasks();
                MarkDirty();
            };
            SldOpacity.ValueChanged += /* Apply shape opacity and refresh its mask effects. 应用图形透明度并刷新蒙版效果。 */ (s, e) =>
            {
                var sh = _sel as ShapeElement;
                if (_syncingShape || sh == null) return;
                PushUndo("opacity:" + sh.GetHashCode());
                sh.Opacity = Math.Round(SldOpacity.Value) / 100.0;
                LblOpacity.Text = "透明度：" + Math.Round(SldOpacity.Value) + "%";
                UpdateView(sh);
                MarkDirty();
            };
            ChkMask.Click += /* Toggle the shape's mask role and initialize a valid target when possible. 切换图形蒙版角色，并在可能时初始化有效目标。 */ (s, e) =>
            {
                var sh = _sel as ShapeElement;
                if (_syncingShape || sh == null || sh.Kind == "line") return;
                PushUndo();
                sh.IsMask = ChkMask.IsChecked == true;
                if (sh.IsMask)
                {
                    var candidates = _elements.Where(/* Find eligible mask targets other than the mask itself. 查找蒙版本身之外的有效目标。 */ x => !ReferenceEquals(x, sh) && IsMaskTarget(x)).ToList();
                    var current = candidates.FirstOrDefault(/* Match the selected mask's stable target ID. 匹配所选蒙版的稳定目标 ID。 */ x => x.Id == sh.MaskTargetId);
                    if (current == null)
                    {
                        int at = _elements.IndexOf(sh);
                        current = at > 0 && IsMaskTarget(_elements[at - 1]) ? _elements[at - 1] : candidates.FirstOrDefault();
                    }
                    sh.MaskTargetId = current != null ? current.Id : null;
                    sh.MaskMode = string.IsNullOrEmpty(sh.MaskMode) ? "alpha" : sh.MaskMode;
                }
                else sh.MaskTargetId = null;
                UpdateView(sh);
                ApplyMasks();
                RefreshLayers();
                SyncShapePanel();
                MarkDirty();
                SetStatus(sh.IsMask
                    ? "已设为蒙版：它通过稳定 ID 作用到指定图层，图层重排后仍保持关联。"
                    : "已取消遮罩，恢复为普通图形。");
            };
            CmbMaskMode.SelectionChanged += /* Change the selected mask mode and refresh clipping. 更改所选蒙版模式并刷新裁剪。 */ (s, e) =>
            {
                var sh = _sel as ShapeElement;
                var item = CmbMaskMode.SelectedItem as ComboBoxItem;
                if (_syncingShape || sh == null || !sh.IsMask || item == null) return;
                string mode = item.Tag as string ?? "alpha";
                if (sh.MaskMode == mode) return;
                PushUndo(); sh.MaskMode = mode; ApplyMasks(); RefreshLayers(); MarkDirty();
            };
            CmbMaskTarget.SelectionChanged += /* Bind the mask to the selected target element's stable ID. 将蒙版绑定到所选目标元素的稳定 ID。 */ (s, e) =>
            {
                var sh = _sel as ShapeElement;
                var target = CmbMaskTarget.SelectedItem as CanvasElement;
                if (_syncingShape || sh == null || !sh.IsMask) return;
                string id = target != null ? target.Id : null;
                if (sh.MaskTargetId == id) return;
                PushUndo(); sh.MaskTargetId = id; ApplyMasks(); RefreshLayers(); MarkDirty();
                SetStatus(target == null ? "蒙版当前没有有效目标。" : "蒙版现在作用到「" + target.Name + "」，重排图层不会断开。");
            };
            BtnFillPick.Click += /* Choose the selected shape's fill color. 选择当前图形填充颜色。 */ (s, e) => PickColor(TxtFill);
            BtnStrokePick.Click += /* Choose the selected shape's stroke color. 选择当前图形描边颜色。 */ (s, e) => PickColor(TxtStroke);
            BtnDelShape.Click += /* Delete eligible selected elements. 删除允许删除的选中元素。 */ (s, e) => DeleteSelected();
            BtnMaskShape.Click += /* Create a mask for the selected drawable element. 为选中可绘制元素创建蒙版。 */ (s, e) => AddMaskOverSelected();

            // 参数化尺寸（相对设计框的百分比）——回车或失焦生效
            System.Windows.Input.KeyEventHandler onEnter = /* Commit size-field edits when Enter is pressed. 按回车时提交尺寸字段编辑。 */ (s, e) => { if (e.Key == Key.Enter) ApplySizeFields(); };
            TxtSizeW.KeyDown += onEnter; TxtSizeH.KeyDown += onEnter; TxtSizeX.KeyDown += onEnter; TxtSizeY.KeyDown += onEnter;
            TxtSizeW.LostFocus += /* Commit pending shape-size field values. 提交待应用的图形尺寸字段值。 */ (s, e) => ApplySizeFields();
            TxtSizeH.LostFocus += /* Commit pending shape-size field values. 提交待应用的图形尺寸字段值。 */ (s, e) => ApplySizeFields();
            TxtSizeX.LostFocus += /* Commit pending shape-size field values. 提交待应用的图形尺寸字段值。 */ (s, e) => ApplySizeFields();
            TxtSizeY.LostFocus += /* Commit pending shape-size field values. 提交待应用的图形尺寸字段值。 */ (s, e) => ApplySizeFields();
            ChkSquare.Click += /* Persist the shape's aspect lock and reconcile its dimensions. 保存图形比例锁定，并协调尺寸。 */ (s, e) =>
            {
                var sh = _sel as ShapeElement;
                if (_syncingShape || sh == null || sh.Kind == "line") return;
                bool value = ChkSquare.IsChecked == true;
                if (sh.LockAspect == value) return;
                PushUndo();
                sh.LockAspect = value;
                if (sh.LockAspect)
                {
                    double m = Math.Max(sh.W, sh.H);
                    sh.W = m; sh.H = m;
                }
                UpdateView(sh); ApplyMasks(); SyncShapePanel(); MarkDirty();
            };
        }

        // Walk the WPF visual tree upward to find the requested ancestor type.
        // 沿 WPF 可视树向上查找指定类型的祖先。
        private static T FindVisualParent<T>(DependencyObject source) where T : DependencyObject
        {
            var current = source;
            while (current != null)
            {
                var hit = current as T; if (hit != null) return hit;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        // Parse shape size inputs, honor aspect constraints, and refresh geometry and masks.
        // 解析图形尺寸输入、遵守比例约束，并刷新几何与蒙版。
        private void ApplySizeFields()
        {
            var sh = _sel as ShapeElement;
            if (_syncingShape || sh == null || sh.Kind == "line") return;
            var f = ActiveTarget.FrameRect;
            double pw, ph, px, py;
            if (!double.TryParse(TxtSizeW.Text, out pw)) pw = sh.W / f.Width * 100;
            if (!double.TryParse(TxtSizeH.Text, out ph)) ph = sh.H / f.Height * 100;
            if (!double.TryParse(TxtSizeX.Text, out px)) px = (sh.X - f.X) / f.Width * 100;
            if (!double.TryParse(TxtSizeY.Text, out py)) py = (sh.Y - f.Y) / f.Height * 100;
            PushUndo();
            sh.W = Math.Max(1, pw / 100.0 * f.Width);
            sh.H = Math.Max(1, ph / 100.0 * f.Height);
            if (sh.LockAspect) { double m = Math.Max(sh.W, sh.H); sh.W = m; sh.H = m; }
            sh.X = f.X + px / 100.0 * f.Width;
            sh.Y = f.Y + py / 100.0 * f.Height;
            UpdateView(sh); ApplyMasks(); SyncShapePanel(); MarkDirty();
        }

        // Populate shape and mask controls from the active shape without recursively editing it.
        // 根据活动图形填充图形与蒙版控件，避免递归触发编辑。
        private void SyncShapePanel()
        {
            var s = _sel as ShapeElement;
            if (s == null) return;
            _syncingShape = true;
            ShapeTitle.Text = "图形：" + s.Name;
            RowFill.Visibility = s.Kind == "line" ? Visibility.Collapsed : Visibility.Visible;
            RowRadius.Visibility = s.Kind == "roundrect" ? Visibility.Visible : Visibility.Collapsed;
            RowSize.Visibility = s.Kind == "line" ? Visibility.Collapsed : Visibility.Visible;
            ChkMask.Visibility = s.Kind == "line" ? Visibility.Collapsed : Visibility.Visible;
            if (s.Kind != "line")
            {
                var f = ActiveTarget.FrameRect;
                TxtSizeW.Text = Math.Round(s.W / f.Width * 100, 1).ToString("0.#");
                TxtSizeH.Text = Math.Round(s.H / f.Height * 100, 1).ToString("0.#");
                TxtSizeX.Text = Math.Round((s.X - f.X) / f.Width * 100, 1).ToString("0.#");
                TxtSizeY.Text = Math.Round((s.Y - f.Y) / f.Height * 100, 1).ToString("0.#");
                ChkSquare.IsChecked = s.LockAspect;
            }
            TxtFill.Text = s.Fill;
            SetSwatch(SwFill, s.Fill);
            ChkNoFill.IsChecked = s.NoFill;
            TxtStroke.Text = s.Stroke;
            SetSwatch(SwStroke, s.Stroke);
            SldStrokeW.Value = s.StrokeW;
            LblStrokeW.Text = (s.Kind == "line" ? "线条粗细：" : "边框粗细：") + s.StrokeW.ToString("0.#");
            SldRadius.Value = s.Radius;
            LblRadius.Text = "圆角：" + s.Radius.ToString("0");
            SldOpacity.Value = Math.Round(s.Opacity * 100);
            LblOpacity.Text = "透明度：" + Math.Round(s.Opacity * 100) + "%";
            ChkMask.IsChecked = s.IsMask;
            RowMaskMode.Visibility = s.IsMask && s.Kind != "line" ? Visibility.Visible : Visibility.Collapsed;
            foreach (ComboBoxItem item in CmbMaskMode.Items)
                if ((item.Tag as string ?? "alpha") == (s.MaskMode ?? "alpha")) { CmbMaskMode.SelectedItem = item; break; }
            var targets = _elements.Where(/* List mask targets excluding the currently selected shape. 列出蒙版目标并排除当前选中图形。 */ x => !ReferenceEquals(x, s) && IsMaskTarget(x)).ToList();
            CmbMaskTarget.ItemsSource = targets;
            CmbMaskTarget.SelectedItem = targets.FirstOrDefault(/* Restore the target selector from the mask's saved ID. 根据蒙版保存的 ID 恢复目标选择器。 */ x => x.Id == s.MaskTargetId);
            _syncingShape = false;
        }

        // ================= 手绘 / 图片属性面板 =================
        // Bind ink appearance and image-opacity controls, deletion, and mask actions.
        // 绑定手绘外观与图片透明度控件，以及删除和蒙版操作。
        private void WireInkImagePanels()
        {
            TxtInk.TextChanged += /* Validate and apply the selected ink color. 验证并应用所选手绘颜色。 */ (s, e) =>
            {
                var k = _sel as InkElement;
                if (_syncingShape || k == null) return;
                string n = ColorUtil.NormalizeHex(TxtInk.Text);
                if (n == null) return;
                PushUndo("inkcolor:" + k.GetHashCode());
                k.Color = n;
                SetSwatch(SwInk, n);
                UpdateView(k);
                MarkDirty();
            };
            BtnInkPick.Click += /* Choose the selected ink stroke's color. 选择当前手绘笔画颜色。 */ (s, e) => PickColor(TxtInk);
            SldInkW.ValueChanged += /* Apply selected ink width with coalesced undo. 应用选中手绘宽度并合并撤销。 */ (s, e) =>
            {
                var k = _sel as InkElement;
                if (_syncingShape || k == null) return;
                PushUndo("inkw:" + k.GetHashCode());
                k.Width = Math.Round(SldInkW.Value, 1);
                LblInkW.Text = "粗细：" + k.Width.ToString("0.#");
                UpdateView(k);
                MarkDirty();
            };
            SldInkOpacity.ValueChanged += /* Apply selected ink opacity with coalesced undo. 应用选中手绘透明度并合并撤销。 */ (s, e) =>
            {
                var k = _sel as InkElement;
                if (_syncingShape || k == null) return;
                PushUndo("inkop:" + k.GetHashCode());
                k.Opacity = Math.Round(SldInkOpacity.Value) / 100.0;
                LblInkOpacity.Text = "透明度：" + Math.Round(SldInkOpacity.Value) + "%";
                UpdateView(k);
                MarkDirty();
            };
            BtnDelInk.Click += /* Delete eligible selected elements. 删除允许删除的选中元素。 */ (s, e) => DeleteSelected();
            BtnMaskInk.Click += /* Create a mask for the selected drawable element. 为选中可绘制元素创建蒙版。 */ (s, e) => AddMaskOverSelected();

            SldImgOpacity.ValueChanged += /* Apply selected image opacity with coalesced undo. 应用选中图片透明度并合并撤销。 */ (s, e) =>
            {
                var im = _sel as ImageElement;
                if (_syncingShape || im == null) return;
                PushUndo("imgop:" + im.GetHashCode());
                im.Opacity = Math.Round(SldImgOpacity.Value) / 100.0;
                LblImgOpacity.Text = "透明度：" + Math.Round(SldImgOpacity.Value) + "%";
                UpdateView(im);
                MarkDirty();
            };
            BtnDelImg.Click += /* Delete eligible selected elements. 删除允许删除的选中元素。 */ (s, e) => DeleteSelected();
            BtnMaskImg.Click += /* Create a mask for the selected drawable element. 为选中可绘制元素创建蒙版。 */ (s, e) => AddMaskOverSelected();
        }

        // Display selected ink color, width, and opacity while suppressing control feedback.
        // 显示所选手绘颜色、宽度与透明度，并阻止控件反馈。
        private void SyncInkPanel()
        {
            var k = _sel as InkElement;
            if (k == null) return;
            _syncingShape = true;
            InkTitle.Text = "手绘：" + k.Name;
            TxtInk.Text = k.Color;
            SetSwatch(SwInk, k.Color);
            SldInkW.Value = k.Width;
            LblInkW.Text = "粗细：" + k.Width.ToString("0.#");
            SldInkOpacity.Value = Math.Round(k.Opacity * 100);
            LblInkOpacity.Text = "透明度：" + Math.Round(k.Opacity * 100) + "%";
            _syncingShape = false;
        }

        // Display the selected image's opacity while suppressing control feedback.
        // 显示所选图片透明度，并阻止控件反馈。
        private void SyncImagePanel()
        {
            var im = _sel as ImageElement;
            if (im == null) return;
            _syncingShape = true;
            ImgTitle.Text = "图片：" + im.Name;
            SldImgOpacity.Value = Math.Round(im.Opacity * 100);
            LblImgOpacity.Text = "透明度：" + Math.Round(im.Opacity * 100) + "%";
            _syncingShape = false;
        }
    }
}
