using Microsoft.Win32;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using IOPath = System.IO.Path;

namespace KnotJotTextStyleEditor;
public partial class MainWindow : Window
{
    // EN: Keep document, selection, drag, undo, preview cache, and connection state local to the WPF window.
    // ZH: 将文档、选择、拖动、撤销、预览缓存及连接状态保存在 WPF 窗口中。
    readonly JsonSerializerOptions json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
    readonly Dictionary<DependencyObject, string> originalUiText = new();
    readonly Dictionary<FrameworkElement, string> originalTooltips = new();
    readonly List<string> history = [];
    readonly List<string> recentColors = [];
    readonly HashSet<int> selectedLayers = [];
    readonly Dictionary<int, Point> groupDragStarts = [];
    readonly DispatcherTimer previewTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(80)
    };
    readonly Dictionary<string, PreviewDimensions> measurementCache = new();
    // EN: Cancel superseded frame requests and retain their task for deterministic hidden-window verification.
    // ZH: 取消被替代的外框请求，并保留任务供确定的隐藏窗口验证。
    CancellationTokenSource? framePreviewCancellation;
    Task framePreviewTask = Task.CompletedTask;
    string? lastFramePreviewKey;
    // EN: Coalesce a continuous control edit until focus leaves, a command begins, or the short idle interval expires.
    // ZH: 合并连续控件编辑，直到失焦、开始命令或短暂停顿时提交。
    readonly DispatcherTimer liveHistoryTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    bool pendingLiveHistory;
    int historyIndex = -1;
    bool restoringHistory, syncingLayerSelection, hasSynced, hasStandaloneSave, suppressLiveControlSync = true, closingApproved, previewCapped;
    bool english;
    TextBoxStyle style = new();
    string tool = "select";
    int selected = -1;
    Point down, layerDragStart, lineEnd;
    double dragLayerStartX, dragLayerStartY, dragTextStartX, dragTextStartY, resizeX, resizeY, resizeW, resizeH;
    int resizeHandle;
    bool dragging, resizing, resizeTargetText, dragChanged, selectedText, marqueeSelecting;
    FrameworkElement? draft;
    Rectangle? marqueeElement;
    Window? floatingTools;
    string? connectedTarget;
    string? connectedRoot;
    string? connectedLibraryPath;
    string? currentFile;
    string? lastSyncedId;
    string? lastSyncedSnapshot;
    string? lastSavedSnapshot;
    string? lastPreviewVisualSnapshot;
    string? lastDesignVisualSnapshot;
    BitmapSource? lastDesignPixels;
    StyleLibrary? currentLibrary;
    int currentLibraryIndex = -1;
    // EN: Initialize installed fonts, wire editor commands and live preview events, then restore the connection and show Home.
    // ZH: 初始化系统字体、编辑命令及实时预览事件，再恢复连接并显示开始页。
    public MainWindow()
    {
        InitializeComponent();
        FontBox.ItemsSource = Fonts.SystemFontFamilies.Select( /* EN: Project installed font family names. ZH: 提取已安装字体名称。 */f => f.Source).Distinct().OrderBy( /* EN: Sort font names alphabetically. ZH: 按名称排序字体。 */x => x).ToList();
        FontBox.SelectedItem = FontBox.Items.Cast<string>().FirstOrDefault( /* EN: Prefer Microsoft YaHei when installed. ZH: 已安装时优先选择微软雅黑。 */x => x.Equals("Microsoft YaHei", StringComparison.OrdinalIgnoreCase)) ?? FontBox.Items.Cast<string>().FirstOrDefault();
        SizeModeBox.SelectedIndex = 0;
        InputTypeBox.SelectedIndex = 0;
        // EN: Wire drawing and canvas-selection gestures to the same model-backed command handlers.
        // ZH: 将绘制及画布选择操作连接到相同的模型命令处理器。
        SelectTool.Click += /* EN: Activate selection and movement. ZH: 启用选择与移动。 */ (_, _) => SetTool("select");
        RectTool.Click += /* EN: Activate rectangle drawing. ZH: 启用矩形绘制。 */ (_, _) => SetTool("rect");
        RoundTool.Click += /* EN: Activate rounded-rectangle drawing. ZH: 启用圆角矩形绘制。 */ (_, _) => SetTool("round");
        EllipseTool.Click += /* EN: Activate ellipse drawing. ZH: 启用椭圆绘制。 */ (_, _) => SetTool("ellipse");
        LineTool.Click += /* EN: Activate line drawing. ZH: 启用直线绘制。 */ (_, _) => SetTool("line");
        ImportImageBtn.Click += /* EN: Open the raster-image import workflow. ZH: 打开位图导入流程。 */ (_, _) => ImportImage();
        TextTool.Click += /* EN: Activate unique text-region drawing. ZH: 启用唯一文字区域绘制。 */ (_, _) => SetTool("text");
        DesignCanvas.MouseLeftButtonDown += CanvasDown;
        DesignCanvas.MouseMove += CanvasMove;
        DesignCanvas.MouseLeftButtonUp += CanvasUp;
        LayerList.PreviewMouseLeftButtonDown += /* EN: Remember the list pointer origin for drag-threshold detection. ZH: 记录列表指针起点以判断拖动阈值。 */ (_, e) => layerDragStart = e.GetPosition(LayerList);
        LayerList.PreviewMouseMove += LayerListMouseMove;
        LayerList.PreviewDragOver += LayerListDragOver;
        LayerList.DragLeave += /* EN: Clear insertion feedback when the drag leaves the list. ZH: 拖动离开列表时清除插入提示。 */ (_, _) => ClearLayerDropIndicator();
        LayerList.Drop += LayerListDrop;
        LayerList.PreviewMouseRightButtonDown += LayerListRightClick;
        LayerList.SelectionChanged += /* EN: Mirror list multi-selection into canvas indices without recursive binding updates. ZH: 将列表多选映射到画布索引并避免递归绑定更新。 */ (_, _) =>
        {
            if (syncingLayerSelection)
                return;
            selectedLayers.Clear();
            selectedText = false;
            foreach (var item in LayerList.SelectedItems)
            {
                if (item is StyleLayer layer)
                {
                    var i = style.Layers.IndexOf(layer);
                    if (i >= 0)
                        selectedLayers.Add(i);
                }
                else if (ReferenceEquals(item, style.TextRegion))
                    selectedText = true;
            }

            selected = LayerList.SelectedItem is StyleLayer primary ? style.Layers.IndexOf(primary) : -1;
            LoadLayerFields();
            RebuildCanvas();
        };
        DeleteBtn.Click += /* EN: Delete the current object selection. ZH: 删除当前选择对象。 */ (_, _) => DeleteSelected();
        UpBtn.Click += /* EN: Move the selected group upward one position. ZH: 将选中组上移一个位置。 */ (_, _) => MoveLayer(1);
        DownBtn.Click += /* EN: Move the selected group downward one position. ZH: 将选中组下移一个位置。 */ (_, _) => MoveLayer(-1);
        TopBtn.Click += /* EN: Place the selected group above all other layers. ZH: 将选中组放在所有其他图层上方。 */ (_, _) => MoveLayerExtreme(true);
        BottomBtn.Click += /* EN: Place the selected group below all other layers. ZH: 将选中组放在所有其他图层下方。 */ (_, _) => MoveLayerExtreme(false);
        UndoBtn.Click += /* EN: Restore the preceding document snapshot. ZH: 恢复前一个文档快照。 */ (_, _) => Undo();
        RedoBtn.Click += /* EN: Restore the following document snapshot. ZH: 恢复后一个文档快照。 */ (_, _) => Redo();
        ApplyBtn.Click += /* EN: Apply general and layer fields, redraw both views, and commit one history state. ZH: 应用通用及图层字段，重绘两个视图并记录一次历史。 */ (_, _) =>
        {
            ReadControls();
            ApplyLayerFields();
            RebuildCanvas();
            RefreshPreview();
            CommitHistory();
            Status(T("属性已应用", "Properties applied"));
        };
        NewBtn.Click += /* EN: Start a guarded blank style. ZH: 创建受未保存保护的空白样式。 */ (_, _) => NewStyle();
        OpenBtn.Click += /* EN: Open a saved style or library. ZH: 打开已保存样式或样式库。 */ (_, _) => OpenStyle();
        SaveBtn.Click += /* EN: Save using the current file association. ZH: 保存到当前关联文件。 */ (_, _) => SaveStandalone();
        SaveAsBtn.Click += /* EN: Force a new save destination. ZH: 要求选择新的保存目标。 */ (_, _) => SaveStandalone(true);
        ConnectBtn.Click += /* EN: Choose and persist the application connection. ZH: 选择并保存主程序连接。 */ (_, _) => Connect();
        SyncBtn.Click += /* EN: Sync the current document to the connected library. ZH: 将当前文档同步到已连接样式库。 */ (_, _) => Sync(false);
        LaunchBtn.Click += /* EN: Launch the connected application. ZH: 启动已连接主程序。 */ (_, _) => Launch();
        FloatToolsBtn.Click += /* EN: Open or activate the floating tool palette. ZH: 打开或激活浮动工具栏。 */ (_, _) => ShowFloatingTools();
        HomeBtn.Click += /* EN: Return Home only when unsaved-work handling allows it. ZH: 仅在未保存处理允许时返回开始页。 */ (_, _) =>
        {
            if (GuardUnsaved())
                ShowStart();
        };
        StartNewBtn.Click += /* EN: Show the editor after creation leaves a clean document. ZH: 创建后文档处于已保存基准状态时显示编辑器。 */ (_, _) =>
        {
            NewStyle();
            if (!IsDirty)
                ShowEditor();
        };
        StartOpenBtn.Click += /* EN: Show the editor only after an accepted open operation. ZH: 仅在打开成功后显示编辑器。 */ (_, _) =>
        {
            if (OpenStyle())
                ShowEditor();
        };
        StartConnectBtn.Click += /* EN: Show the editor only after an accepted connection. ZH: 仅在连接成功后显示编辑器。 */ (_, _) =>
        {
            if (Connect())
                ShowEditor();
        };
        LanguageBtn.Click += /* EN: Toggle the editor's display language. ZH: 切换编辑界面语言。 */ (_, _) => ApplyLanguage(!english);
        StartLanguageBtn.Click += /* EN: Toggle the Home page's display language. ZH: 切换开始页界面语言。 */ (_, _) => ApplyLanguage(!english);
        WorkTabs.SelectionChanged += /* EN: Render CSS only while Preview is selected; leaving cancels that window's pending frame without disturbing editing. ZH: 仅选中预览页时渲染 CSS；离开时取消该窗口待处理外框，不影响编辑。 */ (_, _) =>
        {
            if (WorkTabs.SelectedIndex == 1)
            {
                ReadControls();
                RefreshPreview();
                UpdateSyncState();
            }
            else { framePreviewCancellation?.Cancel(); lastFramePreviewKey = null; }
        };
        SizeModeBox.SelectionChanged += LivePreviewStyleChanged;
        // EN: Keep preview/rule/property controls live while suppression flags prevent programmatic loading from creating edits.
        // ZH: 保持预览、规则及属性实时响应，同时用抑制标记避免程序加载被视为编辑。
        InputTypeBox.SelectionChanged += LivePreviewRulesChanged;
        MaxLengthBox.TextChanged += LivePreviewRulesChanged;
        PatternBox.TextChanged += LivePreviewRulesChanged;
        RequiredBox.Checked += LivePreviewRulesChanged;
        RequiredBox.Unchecked += LivePreviewRulesChanged;
        foreach (var box in new[]
        {
            NameBox,
            IdBox,
            BgBox,
            BorderBox,
            ColorBox,
            RadiusBox,
            BorderWidthBox,
            ShadowBox,
            FontWeightBox,
            PaddingBox,
            FontSizeBox,
            AspectBox
        }

        )
            box.TextChanged += LiveStyleControlChanged;
        FontBox.SelectionChanged += LiveStyleControlChanged;
        foreach (var box in new[]
        {
            LayerNameBox,
            LayerFillBox,
            LayerStrokeBox,
            StrokeWidthBox,
            LayerRadiusBox,
            OpacityBox,
            RotationBox,
            XBox,
            YBox,
            WBox,
            HBox
        }

        )
            box.TextChanged += LiveLayerControlChanged;
        BlendModeBox.SelectionChanged += LivePreviewStyleChanged;
        foreach (var pair in new[]
        {
            (BgColorBtn, BgBox),
            (BorderColorBtn, BorderBox),
            (TextColorBtn, ColorBox),
            (LayerFillColorBtn, LayerFillBox),
            (LayerStrokeColorBtn, LayerStrokeBox)
        }

        )
            pair.Item1.Click += /* EN: Open the picker for this paired color field. ZH: 为当前配对颜色字段打开选择器。 */ (_, _) => PickColor(pair.Item2);
        PreviewText.TextChanged += /* EN: Revalidate changed preview text and debounce its layout refresh. ZH: 重新校验预览文字变化并延迟合并布局刷新。 */ (_, _) =>
        {
            ApplyPreviewValidity();
            SchedulePreviewRefresh();
        };
        PreviewText.PreviewTextInput += ValidatePreviewTextInput;
        DataObject.AddPastingHandler(PreviewText, PreviewPaste);
        KeyDown += OnKey;
        previewTimer.Tick += /* EN: Stop the one-shot debounce timer before rendering the latest preview. ZH: 渲染最新预览前停止单次防抖计时器。 */ (_, _) =>
        {
            previewTimer.Stop();
            RefreshPreview();
        };
        Closing += WindowClosing;
        // EN: Cancel only this window's pending CSS frame when it closes; the shared owned browser exits with the application.
        // ZH: 窗口关闭时仅取消本窗口待处理的 CSS 外框；共享自有浏览器随应用退出。
        Closed += (_, _) => { framePreviewCancellation?.Cancel(); framePreviewCancellation?.Dispose(); };
        liveHistoryTimer.Tick += (_, _) => FlushPendingHistory();
        AddHandler(Keyboard.LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((_, _) => FlushPendingHistory()));
        // EN: Finish live edits before mouse-driven selection or binding changes, keeping the following discrete action in its own history step.
        // ZH: 鼠标触发选择或绑定变化前完成实时编辑，令后续离散操作拥有独立历史步骤。
        PreviewMouseDown += (_, _) => FlushPendingHistory();
        NewStyle();
        suppressLiveControlSync = false;
        ReadInputRuleControls();
        RestoreConnection();
        CaptureUiText(this);
        ShowStart();
    }

    // EN: Parse a finite number using the current culture, falling back for incomplete or non-finite input.
    // ZH: 按当前区域格式读取有限数字，对未完成或非有限输入使用回退值。
    static double Num(string? value, double fallback = 0) => double.TryParse(value, out var n) && double.IsFinite(n) ? n : fallback;
    // EN: Accept a positive decimal or numerator/denominator ratio without admitting division by zero.
    // ZH: 接受正小数或分子/分母比例，并排除除零情况。
    static double Ratio(string? value, double fallback = 0)
    {
        var s = (value ?? "").Trim();
        var parts = s.Split('/');
        if (parts.Length == 2)
        {
            var a = Num(parts[0]);
            var b = Num(parts[1]);
            var r = b > 0 ? a / b : fallback;
            return a > 0 && double.IsFinite(r) ? r : fallback;
        }

        var n = Num(s, fallback);
        return n > 0 && double.IsFinite(n) ? n : fallback;
    }

    // EN: Convert a WPF color string, substituting a known brush when parsing fails.
    // ZH: 转换 WPF 颜色字符串，解析失败时采用已知的备用画刷。
    static Brush BrushOf(string? value, string fallback = "#000000")
    {
        try
        {
            return (Brush)new BrushConverter().ConvertFromString(string.IsNullOrWhiteSpace(value) ? fallback : value)!;
        }
        catch
        {
            return (Brush)new BrushConverter().ConvertFromString(fallback)!;
        }
    }

    // EN: Validate a numeric control within its allowed range and display a field-level error without assigning invalid model values.
    // ZH: 按允许范围校验数字控件并显示字段错误，避免将无效值写入模型。
    bool TryReadNumber(TextBox box, double minimum, double maximum, out double value)
    {
        var valid = double.TryParse(box.Text, out value) && double.IsFinite(value) && value >= minimum && value <= maximum;
        box.BorderBrush = valid ? SystemColors.ControlDarkBrush : BrushOf("#E64D5F");
        box.ToolTip = valid ? null : T($"请输入 {minimum:0.###} 到 {maximum:0.###} 之间的有限数字", $"Enter a finite number from {minimum:0.###} to {maximum:0.###}");
        if (!valid)
            ValidationText.Text = box.ToolTip?.ToString() ?? T("数值无效", "Invalid number");
        return valid;
    }

    // EN: Validate decimal or fraction aspect input and mark out-of-range ratios beside the control.
    // ZH: 校验小数或分数形式的宽高比，在控件旁标记超出范围的输入。
    bool TryReadRatio(TextBox box, double minimum, double maximum, out double value)
    {
        value = Ratio(box.Text, double.NaN);
        var valid = double.IsFinite(value) && value >= minimum && value <= maximum;
        box.BorderBrush = valid ? SystemColors.ControlDarkBrush : BrushOf("#E64D5F");
        box.ToolTip = valid ? null : T($"请输入 {minimum:0.##} 到 {maximum:0.##} 的比例（可写 16/9）", $"Enter a ratio from {minimum:0.##} to {maximum:0.##} (16/9 is accepted)");
        if (!valid)
            ValidationText.Text = box.ToolTip?.ToString() ?? T("比例无效", "Invalid ratio");
        return valid;
    }

    // EN: Validate a brush string, optionally permitting transparency, and expose parsing errors in the property panel.
    // ZH: 校验画刷字符串，可选择允许透明色，并在属性面板显示解析错误。
    bool TryReadColor(TextBox box, bool allowTransparent, out string value)
    {
        value = box.Text.Trim();
        var valid = allowTransparent && value.Equals("transparent", StringComparison.OrdinalIgnoreCase);
        if (!valid)
            try
            {
                _ = new BrushConverter().ConvertFromString(value);
                valid = true;
            }
            catch
            {
            }

        box.BorderBrush = valid ? SystemColors.ControlDarkBrush : BrushOf("#E64D5F");
        box.ToolTip = valid ? null : T("颜色格式无效，请使用 HEX 或系统颜色选择器", "Invalid color; use HEX or the system color picker");
        if (!valid)
            ValidationText.Text = box.ToolTip?.ToString() ?? T("颜色无效", "Invalid color");
        return valid;
    }

    // EN: Offer standard and recent swatches plus validated text entry, retaining at most eight recent opaque colors.
    // ZH: 提供标准色、最近颜色及经过校验的文字输入，最多保存八个最近非透明颜色。
    void PickColor(TextBox box)
    {
        try
        {
            var input = new TextBox
            {
                Text = box.Text,
                Margin = new Thickness(8),
                MinWidth = 210
            };
            var palette = new WrapPanel
            {
                Margin = new Thickness(8)
            };
            var standardColors = new[]
            {
                "#FFFFFF",
                "#000000",
                "#2C3140",
                "#5B8DEF",
                "#347FFF",
                "#238B57",
                "#E64D5F",
                "#FF8A00",
                "#FFD166",
                "#A56EFF",
                "transparent"
            };
            foreach (var hex in recentColors.Concat(standardColors).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var swatch = new Button
                {
                    Background = BrushOf(hex, "#FFFFFF"),
                    BorderBrush = BrushOf("#8791A5"),
                    Width = 34,
                    Height = 28,
                    Margin = new Thickness(3),
                    ToolTip = hex
                };
                swatch.Click += /* EN: Copy the chosen palette swatch into the dialog input. ZH: 将选中的色块复制到对话框输入。 */ (_, _) => input.Text = hex;
                palette.Children.Add(swatch);
            }

            var ok = new Button
            {
                Content = T("应用颜色", "Apply color"),
                IsDefault = true,
                Margin = new Thickness(8),
                MinWidth = 100
            };
            var cancel = new Button
            {
                Content = T("取消", "Cancel"),
                IsCancel = true,
                Margin = new Thickness(8),
                MinWidth = 80
            };
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            var panel = new StackPanel();
            panel.Children.Add(palette);
            panel.Children.Add(input);
            panel.Children.Add(buttons);
            var dialog = new Window
            {
                Owner = this,
                Title = T("颜色选择器", "Color picker"),
                Content = panel,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            ok.Click += /* EN: Accept the color dialog only after validating its input. ZH: 仅在颜色输入通过校验后接受对话框。 */ (_, _) =>
            {
                if (TryReadColor(input, true, out _))
                    dialog.DialogResult = true;
            };
            if (dialog.ShowDialog() == true)
            {
                var chosen = input.Text.Trim();
                box.Text = chosen;
                if (!chosen.Equals("transparent", StringComparison.OrdinalIgnoreCase))
                {
                    recentColors.RemoveAll( /* EN: Remove previous occurrences of the chosen recent color. ZH: 移除最近颜色中该颜色的旧记录。 */x => x.Equals(chosen, StringComparison.OrdinalIgnoreCase));
                    recentColors.Insert(0, chosen);
                    if (recentColors.Count > 8)
                        recentColors.RemoveRange(8, recentColors.Count - 8);
                }

                UpdateColorSwatches();
            }
        }
        catch (Exception ex)
        {
            Status(T("无法打开颜色选择器：", "Could not open color picker: ") + ex.Message);
        }
    }

    // EN: Reflect each color field in its picker button, with a safe fallback for unfinished text.
    // ZH: 将各颜色字段反映到选择按钮，未完成输入使用安全备用色。
    void UpdateColorSwatches()
    {
        foreach (var pair in new[]
        {
            (BgColorBtn, BgBox),
            (BorderColorBtn, BorderBox),
            (TextColorBtn, ColorBox),
            (LayerFillColorBtn, LayerFillBox),
            (LayerStrokeColorBtn, LayerStrokeBox)
        }

        )
        {
            pair.Item1.Foreground = BrushOf(pair.Item2.Text, "#5B8DEF");
        }
    }

    // EN: Select dynamic window text using the current UI language flag.
    // ZH: 根据当前界面语言标记选择窗口动态文字。
    string T(string zh, string en) => english ? en : zh;
    // EN: Compare the normalized current document with the last saved snapshot to detect unsaved changes.
    // ZH: 将规范化后的当前文档与最近保存快照比较，判断是否有未保存修改。
    bool IsDirty => lastSavedSnapshot != null && !string.Equals(Snapshot(), lastSavedSnapshot, StringComparison.Ordinal);

    // EN: Display the Home overlay and hide the editing surface without replacing the document.
    // ZH: 显示开始页并隐藏编辑界面，同时保留当前文档。
    void ShowStart()
    {
        StartPage.Visibility = Visibility.Visible;
        EditorRoot.Visibility = Visibility.Collapsed;
    }

    // EN: Reveal the editing surface and dismiss the Home overlay.
    // ZH: 显示编辑界面并收起开始页。
    void ShowEditor()
    {
        StartPage.Visibility = Visibility.Collapsed;
        EditorRoot.Visibility = Visibility.Visible;
    }

    // EN: Remember original logical-tree labels and tooltips so language switching can restore them exactly.
    // ZH: 记录逻辑树原始标签及提示，确保切换语言后可以准确恢复。
    void CaptureUiText(DependencyObject root)
    {
        if (root is TextBlock tb && tb != ConnectionText && tb != StatusText && tb != ValidationText)
            originalUiText.TryAdd(tb, tb.Text);
        else if (root is HeaderedContentControl hc && hc.Header is string hs)
            originalUiText.TryAdd(hc, hs);
        else if (root is ContentControl cc && cc.Content is string cs)
            originalUiText.TryAdd(cc, cs);
        if (root is FrameworkElement fe && fe.ToolTip is string tip)
            originalTooltips.TryAdd(fe, tip);
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            CaptureUiText(child);
    }

    // EN: Map captured original UI labels to English; stable control tags and persisted model values are not translated.
    // ZH: 将记录的原始界面标签映射为英文；稳定控件标签及模型保存值不翻译。
    static readonly Dictionary<string, string> En = new()
    {
        ["开始页"] = "Home",
        ["新建"] = "New",
        ["打开样式"] = "Open style",
        ["保存"] = "Save",
        ["另存为…"] = "Save as…",
        ["撤销"] = "Undo",
        ["重做"] = "Redo",
        ["连接主程序…"] = "Connect…",
        ["更换连接…"] = "Change connection…",
        ["保存并同步"] = "Save & sync",
        ["打开主程序"] = "Launch app",
        ["浮动工具"] = "Floating tools",
        ["绘制工具"] = "Tools",
        ["选择 / 移动"] = "Select / Move",
        ["矩形"] = "Rectangle",
        ["圆角矩形"] = "Rounded rectangle",
        ["椭圆"] = "Ellipse",
        ["直线"] = "Line",
        ["导入图片…"] = "Import image…",
        ["文本区域（重画会替换）"] = "Text region (redraw to replace)",
        ["比例吸附：1:1 / 4:3 / 3:2 / 16:9"] = "Ratio snap: 1:1 / 4:3 / 3:2 / 16:9",
        ["吸附其他图层的边缘和中心"] = "Snap to layer edges and centers",
        ["网格吸附"] = "Snap to grid",
        ["网格大小（像素）"] = "Grid size (pixels)",
        ["绘制长宽比（0=自由，例如 1、1.5、16/9）"] = "Draw aspect ratio (0=free; e.g. 1, 1.5, 16/9)",
        ["右侧图层面板可拖动排序。"] = "Drag layers on the right to reorder.",
        ["设计画布"] = "Design",
        ["独立预览"] = "Preview",
        ["图层"] = "Layers",
        ["上移"] = "Move up",
        ["下移"] = "Move down",
        ["置顶"] = "To top",
        ["置底"] = "To bottom",
        ["删除"] = "Delete",
        ["样式属性"] = "Style properties",
        ["样式名称"] = "Style name",
        ["样式 ID（英文字母/数字/短横线/下划线）"] = "Style ID (English letters, digits, hyphens, underscores)",
        ["底色"] = "Background",
        ["边框色"] = "Border",
        ["文字色"] = "Text color",
        ["圆角"] = "Corner radius",
        ["字体（读取 Windows 已安装字体）"] = "Font (installed Windows fonts)",
        ["字号"] = "Font size",
        ["尺寸策略"] = "Sizing",
        ["固定长宽比"] = "Fixed aspect ratio",
        ["自由拉伸"] = "Free stretch",
        ["文本框长宽比（可手动输入，如 1、1.5、16/9）"] = "Text-box aspect ratio (e.g. 1, 1.5, 16/9)",
        ["写字区域目标宽度（像素）"] = "Text-region target width (px)",
        ["写字区域目标高度（像素）"] = "Text-region target height (px)",
        ["输入限制"] = "Input rules",
        ["最多字符（0=不限）"] = "Maximum characters (0=unlimited)",
        ["允许类型"] = "Allowed input",
        ["任意"] = "Any",
        ["仅数字"] = "Numbers only",
        ["仅字母"] = "Letters only",
        ["仅中文"] = "Chinese only",
        ["数字和字母"] = "Letters and numbers",
        ["自定义正则"] = "Custom regex",
        ["不允许空值"] = "Required",
        ["选中对象"] = "Selected object",
        ["名称"] = "Name",
        ["填充"] = "Fill",
        ["描边（始终实线）"] = "Stroke (always solid)",
        ["位置和尺寸（相对画布百分比）"] = "Position and size (% of canvas)",
        ["宽度"] = "Width",
        ["高度"] = "Height",
        ["应用属性并刷新预览"] = "Apply and refresh",
        ["边框宽度"] = "Border width",
        ["字重"] = "Font weight",
        ["阴影（CSS 格式）"] = "Shadow (CSS)",
        ["内边距（CSS 格式）"] = "Padding (CSS)",
        ["线宽"] = "Stroke width",
        ["图层圆角"] = "Layer radius",
        ["透明度"] = "Opacity",
        ["旋转角度"] = "Rotation",
        ["混合模式"] = "Blend mode",
        ["正常"] = "Normal",
        ["正片叠底"] = "Multiply",
        ["滤色"] = "Screen",
        ["叠加"] = "Overlay",
        ["KnotJot"] = "KnotJot",
        ["文本框样式编辑器"] = "Text Box Style Editor",
        ["开始"] = "Start",
        ["新建设计"] = "New design",
        ["连接主程序"] = "Connect app",
        ["可以不连接主程序独立设计和预览；连接后可直接同步样式。"] = "Design and preview independently, or connect to KnotJot for direct sync.",
        ["未连接主程序（可独立使用）"] = "Not connected (standalone mode)",
        ["在这里输入文字，测试样式和拉伸效果"] = "Type here to test the style and resizing",
        ["显示/隐藏"] = "Show/hide",
        ["锁定"] = "Lock",
        ["选择颜色"] = "Choose color"
    };
    // EN: Translate captured UI strings, refresh model-derived layer labels, and rebuild connection/save/sync status text.
    // ZH: 翻译已记录的界面文字、刷新模型图层名称，并重建连接、保存及同步状态。
    void ApplyLanguage(bool useEnglish)
    {
        english = useEnglish;
        UiState.English = english;
        foreach (var pair in originalUiText)
        {
            var value = english && En.TryGetValue(pair.Value, out var translated) ? translated : pair.Value;
            if (pair.Key is TextBlock tb)
                tb.Text = value;
            else if (pair.Key is HeaderedContentControl hc)
                hc.Header = value;
            else if (pair.Key is ContentControl cc)
                cc.Content = value;
        }

        foreach (var pair in originalTooltips)
            pair.Key.ToolTip = english && En.TryGetValue(pair.Value, out var translated) ? translated : pair.Value;
        LanguageBtn.Content = StartLanguageBtn.Content = english ? "中文" : "EN";
        if (connectedTarget == null)
            ConnectionText.Text = T("未连接主程序（可独立使用）", "Not connected (standalone mode)");
        else
            SetConnected(connectedTarget, connectedLibraryPath);
        LayerList.Items.Refresh();
        UpdateDocumentState();
        UpdateSyncState();
        Status(T("就绪", "Ready"));
    }

    // EN: Replace the status-bar message with the latest operation feedback.
    // ZH: 以最新操作反馈替换状态栏文字。
    void Status(string text) => StatusText.Text = text;
    // EN: Prefer the portable/test data override, retain the legacy override, then fall back to roaming application data.
    // ZH: 优先使用便携或测试数据覆盖目录，保留旧覆盖变量，再回退到漫游应用数据目录。
    static string ConnectionConfigPath
    {
        get
        {
            var testOrPortableData = Environment.GetEnvironmentVariable("KNOTJOT_TEXT_STYLE_EDITOR_DATA_DIR");
            if (string.IsNullOrWhiteSpace(testOrPortableData))
                testOrPortableData = Environment.GetEnvironmentVariable("BMAP_TEXT_STYLE_EDITOR_DATA_DIR");
            var directory = string.IsNullOrWhiteSpace(testOrPortableData) ? IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KnotJotTextStyleEditor") : testOrPortableData;
            return IOPath.Combine(directory, "connection.json");
        }
    }

    // EN: Record the target and resolved library path while showing whether the target still exists.
    // ZH: 记录目标程序及解析后的样式库路径，并显示目标是否仍有效。
    void SetConnected(string target, string? libraryPath = null)
    {
        connectedTarget = target;
        connectedRoot = IOPath.GetDirectoryName(target);
        connectedLibraryPath = libraryPath;
        ConnectBtn.Visibility = Visibility.Visible;
        StartConnectBtn.Visibility = Visibility.Visible;
        ConnectBtn.Content = StartConnectBtn.Content = T("更换连接…", "Change connection…");
        var valid = File.Exists(target) && !string.IsNullOrWhiteSpace(libraryPath);
        ConnectionText.Foreground = BrushOf(valid ? "#238B57" : "#C53A4B");
        ConnectionText.FontWeight = FontWeights.SemiBold;
        ConnectionText.Text = valid ? T("● 已连接", "● Connected") + $" · {FileVersionInfo.GetVersionInfo(target).ProductVersion ?? "KnotJot"}\n{T("主程序：", "App: ")}{target}\n{T("样式库：", "Style library: ")}{libraryPath}" : T("● 连接已失效：", "● Connection is no longer valid: ") + target;
    }

    // EN: Read the saved connection, migrate the legacy config location if needed, and ignore unavailable targets.
    // ZH: 读取已保存连接，必要时兼容旧配置目录，并忽略不可用目标。
    void RestoreConnection()
    {
        try
        {
            var file = ConnectionConfigPath;
            if (!File.Exists(file))
            {
                var legacy = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BMAPTextStyleEditor", "connection.json");
                if (!File.Exists(legacy))
                    return;
                file = legacy;
            }

            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file), json);
            var target = data?.GetValueOrDefault("target");
            var library = data?.GetValueOrDefault("library");
            if (!string.IsNullOrWhiteSpace(target) && File.Exists(target))
            {
                // EN: Re-resolve source connections so saved legacy guesses cannot send later syncs to the wrong directory.
                // ZH: 重新解析源码连接，避免保存的旧布局猜测令后续同步写入错误目录。
                if (target.EndsWith("main.js", StringComparison.OrdinalIgnoreCase))
                    library = StyleLibraryLocation.ForSource(target);
                SetConnected(target, library);
            }
        }
        catch
        {
        }
    }

    // EN: Persist target and library paths as UTF-8 JSON in the configured editor data directory.
    // ZH: 将目标及样式库路径以 UTF-8 JSON 保存到编辑器配置目录。
    void SaveConnection()
    {
        if (connectedTarget == null)
            return;
        Directory.CreateDirectory(IOPath.GetDirectoryName(ConnectionConfigPath)!);
        File.WriteAllText(ConnectionConfigPath, JsonSerializer.Serialize(new Dictionary<string, string> { ["target"] = connectedTarget, ["library"] = connectedLibraryPath ?? "" }, json), new System.Text.UTF8Encoding(false));
    }

    // EN: Normalize geometry and rules before serializing the document used by history and dirty-state comparison.
    // ZH: 在序列化历史及修改状态快照之前，规范化几何数据和输入规则。
    string Snapshot()
    {
        ModelSafety.Normalize(style);
        return JsonSerializer.Serialize(style, json);
    }

    // EN: Derive the Save label and title marker from the current snapshot and standalone file association.
    // ZH: 依据当前快照及独立文件关联更新保存按钮与标题修改标记。
    void UpdateDocumentState(string? currentSnapshot = null)
    {
        if (SaveBtn == null)
            return;
        currentSnapshot ??= Snapshot();
        var dirty = lastSavedSnapshot != null && !string.Equals(currentSnapshot, lastSavedSnapshot, StringComparison.Ordinal);
        hasStandaloneSave = currentFile != null && !dirty;
        SaveBtn.Content = dirty ? T("● 保存", "● Save") : currentFile != null ? T("✓ 已保存", "✓ Saved") : T("保存", "Save");
        // EN: Keep the installed product name in English while retaining localized controls and file identities.
        // ZH: 安装后的产品名保持英文，其他控件及文件身份维持原有本地化与兼容性。
        var baseTitle = "KnotJot Text Style Editor V1.0.1";
        Title = dirty ? baseTitle + " *" : baseTitle;
    }

    // EN: Refresh the document save indicator through the shared snapshot-based state calculation.
    // ZH: 通过共享快照状态计算刷新文档保存指示。
    void UpdateSaveButton() => UpdateDocumentState();
    // EN: Compare against the last successful sync snapshot to update the button label, color, and tooltip.
    // ZH: 对比最近成功同步快照，更新同步按钮的文字、颜色和提示。
    void UpdateSyncState(string? currentSnapshot = null)
    {
        if (SyncBtn == null)
            return;
        currentSnapshot ??= Snapshot();
        hasSynced = lastSyncedSnapshot != null && string.Equals(currentSnapshot, lastSyncedSnapshot, StringComparison.Ordinal);
        SyncBtn.Content = hasSynced ? T("✓ 已同步", "✓ Synced") : T("保存并同步", "Save & sync");
        SyncBtn.Background = BrushOf(hasSynced ? "#238B57" : "#5B8DEF");
        SyncBtn.ToolTip = T("保存当前样式并同步到已连接的 KnotJot", "Save the current style to the connected KnotJot");
    }

    // EN: Before leaving a dirty document, honor Save, Discard, or Cancel and propagate a failed/cancelled save.
    // ZH: 离开已修改文档前处理保存、放弃或取消，并传递保存失败或取消结果。
    bool GuardUnsaved()
    {
        if (lastSavedSnapshot == null || !IsDirty)
            return true;
        var result = MessageBox.Show(T("当前样式有未保存的修改。\n\n“是”保存，“否”放弃，“取消”留在当前样式。", "This style has unsaved changes.\n\nChoose Yes to save, No to discard, or Cancel to stay here."), T("未保存的修改", "Unsaved changes"), MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Cancel)
            return false;
        if (result == MessageBoxResult.No)
            return true;
        return SaveStandalone();
    }

    // EN: Cancel closing when the unsaved-change guard rejects it; hidden test windows bypass interactive prompting.
    // ZH: 未保存保护拒绝时取消关闭；隐藏测试窗口跳过交互提示。
    void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (closingApproved || !IsVisible)
            return;
        if (!GuardUnsaved())
            e.Cancel = true;
        else
            closingApproved = true;
    }

    // EN: Replace history with the current normalized document and reset the undo cursor.
    // ZH: 以当前规范化文档重置历史及撤销游标。
    void ResetHistory()
    {
        liveHistoryTimer.Stop();
        pendingLiveHistory = false;
        history.Clear();
        history.Add(Snapshot());
        historyIndex = 0;
        UpdateHistoryButtons();
    }

    // EN: Deduplicate snapshots, discard the redo branch after new edits, cap history at 100 states, and refresh persistence indicators.
    // ZH: 去重快照、新编辑后丢弃重做分支、将历史限制为一百项，并刷新保存同步状态。
    void CommitHistory()
    {
        liveHistoryTimer.Stop();
        pendingLiveHistory = false;
        if (restoringHistory)
            return;
        string state;
        try
        {
            state = Snapshot();
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            Status(T("本次操作未写入撤销历史", "The operation was not added to undo history"));
            return;
        }

        UpdateSyncState(state);
        UpdateDocumentState(state);
        if (historyIndex >= 0 && history[historyIndex] == state)
            return;
        if (historyIndex + 1 < history.Count)
            history.RemoveRange(historyIndex + 1, history.Count - historyIndex - 1);
        history.Add(state);
        historyIndex = history.Count - 1;
        if (history.Count > 100)
        {
            history.RemoveAt(0);
            historyIndex--;
        }

        UpdateHistoryButtons();
    }

    // EN: Enable undo and redo only when the history cursor has a valid neighboring state.
    // ZH: 仅在历史游标存在有效相邻状态时启用撤销或重做。
    void UpdateHistoryButtons()
    {
        if (UndoBtn == null)
            return;
        UndoBtn.IsEnabled = historyIndex > 0 || pendingLiveHistory;
        RedoBtn.IsEnabled = historyIndex >= 0 && historyIndex < history.Count - 1;
    }

    // EN: Deserialize a selected snapshot without recording another history entry, then rebuild controls and persistence status.
    // ZH: 反序列化指定快照且不再次记录历史，然后重建控件及保存同步状态。
    void RestoreHistory(int index)
    {
        if (index < 0 || index >= history.Count)
            return;
        restoringHistory = true;
        try
        {
            style = JsonSerializer.Deserialize<TextBoxStyle>(history[index], json) ?? new();
            historyIndex = index;
            selected = -1;
            selectedLayers.Clear();
            selectedText = false;
            LoadControls();
            UpdateHistoryButtons();
        }
        finally
        {
            restoringHistory = false;
        }

        UpdateSyncState();
        UpdateDocumentState();
    }

    // EN: Move one snapshot backward and report the result when an earlier state exists.
    // ZH: 存在更早状态时后退一个快照并提示撤销结果。
    void Undo()
    {
        FlushPendingHistory();
        if (historyIndex <= 0)
            return;
        RestoreHistory(historyIndex - 1);
        Status(T("已撤销", "Undone"));
    }

    // EN: Move one snapshot forward and report the result when a redo state exists.
    // ZH: 存在重做状态时前进一个快照并提示结果。
    void Redo()
    {
        FlushPendingHistory();
        if (historyIndex < 0 || historyIndex >= history.Count - 1)
            return;
        RestoreHistory(historyIndex + 1);
        Status(T("已重做", "Redone"));
    }

    // EN: Record the binding-updated visibility and refresh both the design and independent preview.
    // ZH: 记录绑定更新后的可见状态，并刷新设计画布及独立预览。
    void LayerVisibilityChanged(object sender, RoutedEventArgs e)
    {
        CommitHistory();
        RebuildCanvas();
        RefreshPreview();
    }

    // EN: Record the binding-updated lock flag and rebuild editing affordances.
    // ZH: 记录绑定更新后的锁定标记并重建设计辅助控件。
    void LayerLockChanged(object sender, RoutedEventArgs e)
    {
        CommitHistory();
        LoadLayerFields();
        RebuildCanvas();
    }

    // EN: Delay snapshot allocation during typing while immediately enabling Undo; persistent commands flush the pending state.
    // ZH: 输入期间延迟分配快照但立即启用撤销，持久化命令会刷新待提交状态。
    void QueueLiveHistory()
    {
        if (restoringHistory || suppressLiveControlSync) return;
        pendingLiveHistory = true;
        liveHistoryTimer.Stop(); liveHistoryTimer.Start();
        UpdateHistoryButtons();
    }

    // EN: Commit the final model of a pending control edit once, preserving previously completed drawing gestures.
    // ZH: 将待处理控件编辑的最终模型提交一次，保留此前已完成的绘制操作。
    void FlushPendingHistory()
    {
        if (pendingLiveHistory) CommitHistory();
    }

    // EN: Block commands affecting a locked selected object, including the unique text region; unlocking remains available.
    // ZH: 阻止影响锁定选中对象的命令，包含唯一文字区域，同时保留解锁操作。
    bool SelectionLocked() => selectedText && style.TextRegion.IsLocked || selected >= 0 && selected < style.Layers.Count && style.Layers[selected].IsLocked || selectedLayers.Any(i => i >= 0 && i < style.Layers.Count && style.Layers[i].IsLocked);

    // EN: Change the active drawing mode and show its localized label in the status bar.
    // ZH: 切换当前绘制模式，并在状态栏显示本地化名称。
    void SetTool(string value)
    {
        tool = value;
        var label = value == "select" ? T("选择/移动", "Select/Move") : value == "rect" ? T("矩形", "Rectangle") : value == "round" ? T("圆角矩形", "Rounded rectangle") : value == "ellipse" ? T("椭圆", "Ellipse") : value == "line" ? T("直线", "Line") : T("文本区域", "Text region");
        Status(T("当前工具：", "Tool: ") + label);
    }

    // EN: Walk the visual parent chain to resolve the requested container type for hit-tested list elements.
    // ZH: 沿视觉父级链找到所需容器类型，用于列表命中元素。
    static T? Ancestor<T>(DependencyObject? x)
        where T : DependencyObject
    {
        while (x != null)
        {
            if (x is T t)
                return t;
            x = VisualTreeHelper.GetParent(x);
        }

        return null;
    }

    // EN: Start a layer-group drag only after the movement threshold, encoding stable layer IDs in the drag payload.
    // ZH: 超过移动阈值后才开始图层组拖放，并在拖放数据中编码稳定图层 ID。
    void LayerListMouseMove(object sender, MouseEventArgs e)
    {
        if (SelectionLocked()) return;
        if (e.LeftButton != MouseButtonState.Pressed || LayerList.SelectedItem is not StyleLayer layer)
            return;
        var p = e.GetPosition(LayerList);
        if (Math.Abs(p.X - layerDragStart.X) < 5 && Math.Abs(p.Y - layerDragStart.Y) < 5)
            return;
        var moving = selectedLayers.Count > 0 ? selectedLayers.OrderBy( /* EN: Order selected indices to preserve layer-group order. ZH: 排序选中索引以保留图层组顺序。 */i => i).Select( /* EN: Resolve selected indices to their model layers. ZH: 将选中索引解析为模型图层。 */i => style.Layers[i]).ToList() : [layer];
        var data = new DataObject();
        data.SetData("KnotJot.StyleLayerIds", string.Join("|", moving.Select( /* EN: Encode stable layer IDs in the drag payload. ZH: 在拖放数据中编码稳定图层 ID。 */x => x.Id)));
        DragDrop.DoDragDrop(LayerList, data, DragDropEffects.Move);
    }

    // EN: Accept only editor layer payloads and draw an insertion edge above or below the hovered item.
    // ZH: 仅接受编辑器图层数据，并在悬停项上方或下方绘制插入提示。
    void LayerListDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("KnotJot.StyleLayerIds"))
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        ClearLayerDropIndicator();
        var item = Ancestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (item?.DataContext is not StyleLayer)
            return;
        var below = e.GetPosition(item).Y >= item.ActualHeight / 2;
        item.BorderBrush = BrushOf("#347FFF");
        item.BorderThickness = below ? new Thickness(0, 0, 0, 3) : new Thickness(0, 3, 0, 0);
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    // EN: Remove temporary insertion borders from all realized layer-list containers.
    // ZH: 清除已生成图层列表容器上的临时插入边框。
    void ClearLayerDropIndicator()
    {
        foreach (var item in LayerList.Items.Cast<object>().Select( /* EN: Resolve the realized container for each list model item. ZH: 获取每个列表模型项已生成的容器。 */x => LayerList.ItemContainerGenerator.ContainerFromItem(x)).OfType<ListBoxItem>())
            item.BorderThickness = new Thickness(0);
    }

    // EN: Reinsert dragged layers in their original order, correcting the destination for removed items and preserving selection.
    // ZH: 按原顺序重新插入拖动图层，修正移除项造成的位置偏移并保留选择。
    void LayerListDrop(object sender, DragEventArgs e)
    {
        FlushPendingHistory();
        try
        {
            if (e.Data.GetData("KnotJot.StyleLayerIds")is not string encoded)
                return;
            var ids = encoded.Split('|', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var moving = style.Layers.Where( /* EN: Select dragged layers using the payload's ID set. ZH: 使用拖放数据 ID 集合筛选被拖图层。 */x => ids.Contains(x.Id)).ToList();
            if (moving.Count == 0 || moving.Any(x => x.IsLocked))
                return;
            var item = Ancestor<ListBoxItem>(e.OriginalSource as DependencyObject);
            var target = item?.DataContext as StyleLayer;
            var below = item != null && e.GetPosition(item).Y >= item.ActualHeight / 2;
            var originalTargetIndex = target == null ? style.Layers.Count : style.Layers.IndexOf(target) + (below ? 1 : 0);
            var removedBefore = style.Layers.Take(Math.Clamp(originalTargetIndex, 0, style.Layers.Count)).Count(moving.Contains);
            var insertAt = Math.Clamp(originalTargetIndex - removedBefore, 0, style.Layers.Count - moving.Count);
            foreach (var layer in moving)
                style.Layers.Remove(layer);
            style.Layers.InsertRange(insertAt, moving);
            var selectedIndices = Enumerable.Range(insertAt, moving.Count).ToList();
            RefreshLayerList(selectedIndices);
            CommitHistory();
            Status(T("图层组已移动", "Layer group moved"));
        }
        finally
        {
            ClearLayerDropIndicator();
        }
    }

    // EN: Select the clicked layer or text region before opening a layer-specific context menu.
    // ZH: 打开图层专用右键菜单前，选中点击的图层或文字区域。
    void LayerListRightClick(object sender, MouseButtonEventArgs e)
    {
        var item = Ancestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (item == null)
            return;
        if (item.DataContext is StyleLayer layer)
        {
            selected = style.Layers.IndexOf(layer);
            LayerList.SelectedItem = layer;
            item.ContextMenu = LayerContextMenu(selected);
        }
        else
        {
            selected = -1;
            selectedText = ReferenceEquals(item.DataContext, style.TextRegion);
            LayerList.SelectedItem = item.DataContext;
        }
    }

    // EN: Reuse a single owned tool window and wire its buttons to the same drawing/import commands.
    // ZH: 复用一个所属浮动工具窗口，并将按钮连接到相同绘制和导入命令。
    void ShowFloatingTools()
    {
        if (floatingTools != null)
        {
            floatingTools.Activate();
            return;
        }

        var panel = new WrapPanel
        {
            Margin = new Thickness(8)
        };
        foreach (var x in new[]
        {
            (T("选择", "Select"), "select"),
            (T("矩形", "Rectangle"), "rect"),
            (T("圆角", "Rounded"), "round"),
            (T("椭圆", "Ellipse"), "ellipse"),
            (T("直线", "Line"), "line"),
            (T("文本区域", "Text region"), "text")
        }

        )
        {
            var b = new Button
            {
                Content = x.Item1,
                Margin = new Thickness(3),
                MinWidth = 72
            };
            var id = x.Item2;
            b.Click += /* EN: Activate the tool captured by this floating button. ZH: 激活此浮动按钮捕获的工具。 */ (_, _) => SetTool(id);
            panel.Children.Add(b);
        }

        var imageButton = new Button
        {
            Content = T("导入图片…", "Import image…"),
            Margin = new Thickness(3),
            MinWidth = 92
        };
        imageButton.Click += /* EN: Import an image from the floating palette. ZH: 从浮动工具栏导入图片。 */ (_, _) => ImportImage();
        panel.Children.Add(imageButton);
        floatingTools = new Window
        {
            Title = T("工具", "Tools"),
            Owner = this,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.ToolWindow,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            Content = panel,
            Topmost = true
        };
        floatingTools.Closed += /* EN: Allow a new floating window after this one closes. ZH: 当前浮动窗口关闭后允许重新创建。 */ (_, _) => floatingTools = null;
        floatingTools.Show();
    }

    // EN: Encode a selected raster image and fit its aspect-preserving layer inside the 900 by 520 design canvas.
    // ZH: 编码选定的位图，并将保持比例的图片图层放入 900 乘 520 的设计画布。
    void ImportImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = T("导入图片", "Import image"),
            Filter = T("图片|*.png;*.jpg;*.jpeg;*.gif;*.bmp", "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp")
        };
        if (dialog.ShowDialog() != true)
            return;
        try
        {
            var imported = ImageImportPipeline.Encode(dialog.FileName);
            var imageAspect = imported.PixelWidth / Math.Max(1d, imported.PixelHeight);
            const double canvasAspect = 900d / 520d;
            double width, height;
            if (imageAspect >= canvasAspect)
            {
                width = 100;
                height = Math.Min(100, 100 * canvasAspect / imageAspect);
            }
            else
            {
                height = 100;
                width = Math.Min(100, 100 * imageAspect / canvasAspect);
            }

            var layer = new StyleLayer
            {
                Type = "image",
                Name = IOPath.GetFileNameWithoutExtension(dialog.FileName),
                X = (100 - width) / 2,
                Y = (100 - height) / 2,
                W = width,
                H = height,
                Fill = "transparent",
                Stroke = "transparent",
                StrokeWidth = 0,
                ImageData = imported.DataUrl
            };
            style.Layers.Add(layer);
            selected = style.Layers.Count - 1;
            selectedText = false;
            selectedLayers.Clear();
            selectedLayers.Add(selected);
            RefreshLayerList(selected);
            CommitHistory();
            SetTool("select");
            Status(T("图片已导入", "Image imported"));
        }
        catch (Exception ex)
        {
            MessageBox.Show(T("无法导入图片：", "Could not import image: ") + ex.Message);
        }
    }

    // EN: Snap a canvas point to the grid and nearby layer edges/centers, then clamp it to canvas bounds.
    // ZH: 将画布点吸附到网格及附近图层边缘或中心，再限制在画布范围内。
    Point SnapPoint(Point p, int ignoreIndex, out bool snapped)
    {
        snapped = false;
        double x = p.X, y = p.Y;
        if (GridSnapBox.IsChecked == true)
        {
            var g = Math.Clamp(Num(GridSizeBox.Text, 10), 2, 100);
            x = Math.Round(x / g) * g;
            y = Math.Round(y / g) * g;
            snapped = true;
        }

        if (ObjectSnapBox.IsChecked == true)
        {
            double bestX = 8.1, bestY = 8.1;
            for (int i = 0; i < style.Layers.Count; i++)
            {
                if (i == ignoreIndex)
                    continue;
                var l = style.Layers[i];
                foreach (var tx in new[]
                {
                    l.X * 9,
                    (l.X + l.W / 2) * 9,
                    (l.X + l.W) * 9
                }

                )
                {
                    var d = tx - p.X;
                    if (Math.Abs(d) < Math.Abs(bestX))
                        bestX = d;
                }

                foreach (var ty in new[]
                {
                    l.Y * 5.2,
                    (l.Y + l.H / 2) * 5.2,
                    (l.Y + l.H) * 5.2
                }

                )
                {
                    var d = ty - p.Y;
                    if (Math.Abs(d) < Math.Abs(bestY))
                        bestY = d;
                }
            }

            if (Math.Abs(bestX) <= 8)
            {
                x = p.X + bestX;
                snapped = true;
            }

            if (Math.Abs(bestY) <= 8)
            {
                y = p.Y + bestY;
                snapped = true;
            }
        }

        return new Point(Math.Clamp(x, 0, 900), Math.Clamp(y, 0, 520));
    }

    // EN: Adjust a moved layer using its edges and center, excluding other members of the same dragged group.
    // ZH: 使用移动图层的边缘和中心计算吸附，并排除同一拖动组中的其他成员。
    void SnapLayer(StyleLayer layer, int index, ref double x, ref double y, out bool snapped)
    {
        snapped = false;
        if (GridSnapBox.IsChecked == true)
        {
            var g = Math.Clamp(Num(GridSizeBox.Text, 10), 2, 100);
            x = Math.Round(x * 9 / g) * g / 9;
            y = Math.Round(y * 5.2 / g) * g / 5.2;
            snapped = true;
        }

        if (ObjectSnapBox.IsChecked != true)
            return;
        double dxBest = 8.1, dyBest = 8.1;
        var ownX = new[]
        {
            x * 9,
            (x + layer.W / 2) * 9,
            (x + layer.W) * 9
        };
        var ownY = new[]
        {
            y * 5.2,
            (y + layer.H / 2) * 5.2,
            (y + layer.H) * 5.2
        };
        for (int i = 0; i < style.Layers.Count; i++)
        {
            if (i == index || (groupDragStarts.Count > 1 && selectedLayers.Contains(i)))
                continue;
            var other = style.Layers[i];
            foreach (var a in ownX)
                foreach (var b in new[]
                {
                    other.X * 9,
                    (other.X + other.W / 2) * 9,
                    (other.X + other.W) * 9
                }

                )
                {
                    var d = b - a;
                    if (Math.Abs(d) < Math.Abs(dxBest))
                        dxBest = d;
                }

            foreach (var a in ownY)
                foreach (var b in new[]
                {
                    other.Y * 5.2,
                    (other.Y + other.H / 2) * 5.2,
                    (other.Y + other.H) * 5.2
                }

                )
                {
                    var d = b - a;
                    if (Math.Abs(d) < Math.Abs(dyBest))
                        dyBest = d;
                }
        }

        if (Math.Abs(dxBest) <= 8)
        {
            x += dxBest / 9;
            snapped = true;
        }

        if (Math.Abs(dyBest) <= 8)
        {
            y += dyBest / 5.2;
            snapped = true;
        }
    }

    // EN: Guard unsaved work, create a blank document, and reset file, sync, selection, and history state.
    // ZH: 保护未保存工作，创建空白文档，并重置文件、同步、选择及历史状态。
    void NewStyle()
    {
        if (!GuardUnsaved())
            return;
        style = new TextBoxStyle
        {
            Name = T("我的文本框样式", "My text-box style"),
            Id = "my-text-style"
        };
        selected = -1;
        selectedLayers.Clear();
        selectedText = false;
        currentFile = null;
        currentLibrary = null;
        currentLibraryIndex = -1;
        lastSyncedId = null;
        lastSyncedSnapshot = null;
        hasStandaloneSave = false;
        LoadControls();
        lastSavedSnapshot = Snapshot();
        ResetHistory();
        UpdateDocumentState(lastSavedSnapshot);
        UpdateSyncState();
        Status(T("已新建空白样式", "New blank style"));
    }

    // EN: Mirror canvas selection into the list while suppressing recursive selection-change events.
    // ZH: 将画布选择同步到列表，同时抑制递归选择变化事件。
    void SyncLayerSelection()
    {
        syncingLayerSelection = true;
        try
        {
            LayerList.SelectedItems.Clear();
            foreach (var i in selectedLayers.Where( /* EN: Discard stale selection indices outside the layer list. ZH: 丢弃超出图层列表的过期选择索引。 */i => i >= 0 && i < style.Layers.Count).OrderBy( /* EN: Apply list selection in model order. ZH: 按模型顺序应用列表选择。 */i => i))
                LayerList.SelectedItems.Add(style.Layers[i]);
            if (selectedText)
                LayerList.SelectedItems.Add(style.TextRegion);
            if (selected >= 0 && selected < style.Layers.Count)
                LayerList.ScrollIntoView(style.Layers[selected]);
            else if (selectedText)
                LayerList.ScrollIntoView(style.TextRegion);
        }
        finally
        {
            syncingLayerSelection = false;
        }
    }

    // EN: Begin selection/group dragging, marquee selection, or shape drafting; enforce locks and confirm text-region replacement.
    // ZH: 开始选择或组拖动、框选或图形草稿；处理锁定并确认文字区域替换。
    void CanvasDown(object sender, MouseButtonEventArgs e)
    {
        FlushPendingHistory();
        if (tool == "text" && style.TextRegion.IsLocked) return;
        ModelSafety.Normalize(style);
        var raw = e.GetPosition(DesignCanvas);
        dragging = true;
        dragChanged = false;
        DesignCanvas.CaptureMouse();
        if (tool == "select")
        {
            down = raw;
            var hitText = HitTextRegion(raw);
            var hit = selectedText && hitText ? -1 : HitLayer(raw);
            hitText = hit < 0 && hitText;
            var keepSelectedText = selectedText;
            marqueeSelecting = false;
            groupDragStarts.Clear();
            if (hit >= 0)
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    if (!selectedLayers.Add(hit))
                        selectedLayers.Remove(hit);
                }
                else if (!selectedLayers.Contains(hit))
                {
                    selectedLayers.Clear();
                    selectedLayers.Add(hit);
                    keepSelectedText = false;
                }

                if (!selectedLayers.Contains(hit))
                {
                    selected = selectedLayers.LastOrDefault(-1);
                    dragging = false;
                    DesignCanvas.ReleaseMouseCapture();
                    SyncLayerSelection();
                    RebuildCanvas();
                    return;
                }

                selected = hit;
                selectedText = keepSelectedText;
                if (SelectionLocked())
                {
                    dragging = false;
                    DesignCanvas.ReleaseMouseCapture();
                    SyncLayerSelection();
                    RebuildCanvas();
                    Status(T("所选图层包含锁定项", "The selection contains a locked layer"));
                    return;
                }

                foreach (var i in selectedLayers)
                    groupDragStarts[i] = new Point(style.Layers[i].X, style.Layers[i].Y);
                if (selectedText)
                {
                    dragTextStartX = style.TextRegion.X;
                    dragTextStartY = style.TextRegion.Y;
                }

                dragLayerStartX = style.Layers[hit].X;
                dragLayerStartY = style.Layers[hit].Y;
                SyncLayerSelection();
                RebuildCanvas();
            }
            else if (hitText)
            {
                selected = -1;
                selectedLayers.Clear();
                selectedText = true;
                SyncLayerSelection();
                if (style.TextRegion.IsLocked)
                {
                    dragging = false;
                    DesignCanvas.ReleaseMouseCapture();
                    Status(T("文字输入区域已锁定", "The text input region is locked"));
                }

                dragTextStartX = style.TextRegion.X;
                dragTextStartY = style.TextRegion.Y;
                Status(T("已选择文字输入区域", "Text input region selected"));
                RebuildCanvas();
            }
            else
            {
                selected = -1;
                selectedLayers.Clear();
                SyncLayerSelection();
                marqueeSelecting = true;
                marqueeElement = new Rectangle
                {
                    Fill = BrushOf("#1A347FFF"),
                    Stroke = BrushOf("#347FFF"),
                    StrokeThickness = 1.5,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(marqueeElement, raw.X);
                Canvas.SetTop(marqueeElement, raw.Y);
                DesignCanvas.Children.Add(marqueeElement);
            }

            return;
        }

        if (tool == "text" && style.TextRegion.W > 0 && style.TextRegion.H > 0 && MessageBox.Show(T("重画会替换现有文字输入区域，是否继续？", "Redrawing will replace the existing text input region. Continue?"), T("替换文字输入区域", "Replace text input region"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            dragging = false;
            DesignCanvas.ReleaseMouseCapture();
            SetTool("select");
            return;
        }

        down = SnapPoint(raw, -1, out _);
        draft = tool == "ellipse" ? new Ellipse() : new Rectangle();
        if (tool == "line")
        {
            draft = new Line
            {
                X1 = down.X,
                Y1 = down.Y,
                X2 = down.X,
                Y2 = down.Y
            };
            lineEnd = down;
        }

        if (draft is Shape s)
        {
            s.Fill = tool == "line" || tool == "text" ? Brushes.Transparent : BrushOf("#DDE9FF");
            s.Stroke = BrushOf(tool == "text" ? "#E64D5F" : "#5B8DEF");
            s.StrokeThickness = tool == "text" ? 2 : 1.5;
            if (tool == "text")
                s.StrokeDashArray = new DoubleCollection
                {
                    6,
                    4
                };
        }

        Canvas.SetLeft(draft, tool == "line" ? 0 : down.X);
        Canvas.SetTop(draft, tool == "line" ? 0 : down.Y);
        DesignCanvas.Children.Add(draft);
    }

    // EN: Translate explicit line endpoints with the same displacement as their bounds, preserving direction across normalization and saving.
    // ZH: 明确直线端点与边界使用相同位移，确保规范化及保存后仍保留方向。
    static void MoveLayerTo(StyleLayer layer, double x, double y)
    {
        if (layer.IsLocked) return;
        if (layer.Type == "line")
        {
            double dx = x - layer.X, dy = y - layer.Y;
            layer.X1 = (layer.X1 ?? layer.X) + dx;
            layer.Y1 = (layer.Y1 ?? layer.Y) + dy;
            layer.X2 = (layer.X2 ?? layer.X + layer.W) + dx;
            layer.Y2 = (layer.Y2 ?? layer.Y + layer.H) + dy;
        }
        layer.X = x; layer.Y = y;
    }

    // EN: Update the active resize, marquee, group move, line endpoint, or shape draft with snapping and bounds handling.
    // ZH: 根据当前操作更新缩放、框选、组移动、直线端点或图形草稿，并处理吸附和边界。
    void CanvasMove(object sender, MouseEventArgs e)
    {
        var raw = e.GetPosition(DesignCanvas);
        // EN: Recheck locks during gestures so selection changes cannot carry a locked region or layer along.
        // ZH: 手势执行时再次检查锁定，避免选择变化携带锁定区域或图层移动。
        if ((resizing || dragging && tool == "select") && SelectionLocked()) return;
        if (resizing)
        {
            ResizeSelected(raw);
            return;
        }

        if (!dragging)
            return;
        if (tool == "select" && marqueeSelecting && marqueeElement != null)
        {
            var marqueeX = Math.Min(down.X, raw.X);
            var marqueeY = Math.Min(down.Y, raw.Y);
            Canvas.SetLeft(marqueeElement, marqueeX);
            Canvas.SetTop(marqueeElement, marqueeY);
            marqueeElement.Width = Math.Abs(raw.X - down.X);
            marqueeElement.Height = Math.Abs(raw.Y - down.Y);
            return;
        }

        if (tool == "select" && selected >= 0 && selected < style.Layers.Count && groupDragStarts.Count > 0)
        {
            var primary = style.Layers[selected];
            double nx = dragLayerStartX + (raw.X - down.X) / 9.0, ny = dragLayerStartY + (raw.Y - down.Y) / 5.2;
            SnapLayer(primary, selected, ref nx, ref ny, out var layerSnapped);
            double groupDx = nx - dragLayerStartX, groupDy = ny - dragLayerStartY;
            double minDx = groupDragStarts.Max( /* EN: Compute the most restrictive leftward group displacement. ZH: 计算图层组最严格的向左位移边界。 */p => -p.Value.X), maxDx = groupDragStarts.Min( /* EN: Compute the most restrictive rightward group displacement. ZH: 计算图层组最严格的向右位移边界。 */p => 100 - style.Layers[p.Key].W - p.Value.X), minDy = groupDragStarts.Max( /* EN: Compute the most restrictive upward group displacement. ZH: 计算图层组最严格的向上位移边界。 */p => -p.Value.Y), maxDy = groupDragStarts.Min( /* EN: Compute the most restrictive downward group displacement. ZH: 计算图层组最严格的向下位移边界。 */p => 100 - style.Layers[p.Key].H - p.Value.Y);
            if (selectedText)
            {
                minDx = Math.Max(minDx, -dragTextStartX);
                maxDx = Math.Min(maxDx, 100 - style.TextRegion.W - dragTextStartX);
                minDy = Math.Max(minDy, -dragTextStartY);
                maxDy = Math.Min(maxDy, 100 - style.TextRegion.H - dragTextStartY);
            }

            groupDx = Math.Clamp(groupDx, minDx, maxDx);
            groupDy = Math.Clamp(groupDy, minDy, maxDy);
            foreach (var pair in groupDragStarts)
            {
                MoveLayerTo(style.Layers[pair.Key], pair.Value.X + groupDx, pair.Value.Y + groupDy);
            }

            if (selectedText)
            {
                style.TextRegion.X = dragTextStartX + groupDx;
                style.TextRegion.Y = dragTextStartY + groupDy;
            }

            dragChanged = Math.Abs(groupDx) > .001 || Math.Abs(groupDy) > .001;
            RebuildCanvas();
            LoadLayerFields();
            if (layerSnapped)
                Status(T("已吸附", "Snapped"));
            return;
        }

        if (tool == "select" && selectedText)
        {
            var tr = style.TextRegion;
            tr.X = Math.Clamp(dragTextStartX + (raw.X - down.X) / 9, 0, 100 - tr.W);
            tr.Y = Math.Clamp(dragTextStartY + (raw.Y - down.Y) / 5.2, 0, 100 - tr.H);
            dragChanged = true;
            RebuildCanvas();
            return;
        }

        var p = SnapPoint(raw, selected, out var pointSnapped);
        if (tool == "line" && draft is Line draftLine)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                var length = (p - down).Length;
                var angle = Math.Atan2(p.Y - down.Y, p.X - down.X);
                angle = Math.Round(angle / (Math.PI / 4)) * (Math.PI / 4);
                p = new Point(down.X + Math.Cos(angle) * length, down.Y + Math.Sin(angle) * length);
            }

            lineEnd = p;
            draftLine.X2 = p.X;
            draftLine.Y2 = p.Y;
            if (pointSnapped)
                Status(T("直线端点已吸附", "Line endpoint snapped"));
            return;
        }

        if (draft == null)
            return;
        double dx = p.X - down.X, dy = p.Y - down.Y, w = Math.Abs(dx), h = Math.Abs(dy);
        if (tool != "text" && tool != "line" && w > 0 && h > 0)
        {
            double ratio = Ratio(DrawRatioBox.Text), rawRatio = w / h;
            if (ratio <= 0 && SquareSnapBox.IsChecked == true)
            {
                double[] presets = [1, 4.0 / 3, 3.0 / 2, 16.0 / 9, 2];
                var nearest = presets.OrderBy( /* EN: Rank ratio presets by distance from the current draft ratio. ZH: 按与当前草稿比例的差距排序预设比例。 */v => Math.Abs(rawRatio - v)).First();
                if (Math.Abs(rawRatio - nearest) / nearest <= (nearest == 1 ? .14 : .075))
                    ratio = nearest;
            }

            if (ratio > 0)
            {
                if (rawRatio > ratio)
                    h = w / ratio;
                else
                    w = h * ratio;
                pointSnapped = true;
            }
        }

        double x = dx < 0 ? down.X - w : down.X, y = dy < 0 ? down.Y - h : down.Y;
        Canvas.SetLeft(draft, x);
        Canvas.SetTop(draft, y);
        draft.Width = Math.Max(1, w);
        draft.Height = Math.Max(1, h);
        if (pointSnapped)
            Status(T("吸附生效：网格 / 图层边缘中心 / 比例", "Snap: grid / layer edges and centers / ratio"));
    }

    // EN: Finish the mouse gesture, convert accepted drafts to percentage geometry, and commit one completed edit to history.
    // ZH: 结束鼠标操作，将有效草稿转为百分比几何数据，并将完成的编辑记录到历史。
    void CanvasUp(object sender, MouseButtonEventArgs e)
    {
        if (resizing)
        {
            resizing = false;
            DesignCanvas.ReleaseMouseCapture();
            RebuildCanvas();
            LoadLayerFields();
            CommitHistory();
            return;
        }

        if (!dragging)
            return;
        dragging = false;
        DesignCanvas.ReleaseMouseCapture();
        if (tool == "select")
        {
            if (marqueeSelecting)
            {
                var raw = e.GetPosition(DesignCanvas);
                var selection = new Rect(Math.Min(down.X, raw.X), Math.Min(down.Y, raw.Y), Math.Abs(raw.X - down.X), Math.Abs(raw.Y - down.Y));
                selectedLayers.Clear();
                if (selection.Width >= 3 && selection.Height >= 3)
                    for (int i = 0; i < style.Layers.Count; i++)
                    {
                        var l = style.Layers[i];
                        if (l.IsVisible && selection.IntersectsWith(new Rect(l.X * 9, l.Y * 5.2, l.W * 9, l.H * 5.2)))
                            selectedLayers.Add(i);
                    }

                var tr = style.TextRegion;
                selectedText = selection.Width >= 3 && selection.Height >= 3 && tr.W > 0 && tr.H > 0 && selection.IntersectsWith(new Rect(tr.X * 9, tr.Y * 5.2, tr.W * 9, tr.H * 5.2));
                selected = selectedLayers.Count == 0 ? -1 : selectedLayers.Max();
                marqueeSelecting = false;
                marqueeElement = null;
                SyncLayerSelection();
                RebuildCanvas();
                LoadLayerFields();
                var count = selectedLayers.Count + (selectedText ? 1 : 0);
                Status(T($"已选择 {count} 个对象", $"{count} objects selected"));
                return;
            }

            if (dragChanged)
                CommitHistory();
            return;
        }

        if (draft == null)
            return;
        if (tool == "line")
        {
            DesignCanvas.Children.Remove(draft);
            draft = null;
            if ((lineEnd - down).Length < 4)
                return;
            var x1 = down.X / 9;
            var y1 = down.Y / 5.2;
            var x2 = lineEnd.X / 9;
            var y2 = lineEnd.Y / 5.2;
            style.Layers.Add(new StyleLayer { Type = "line", X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, X = Math.Min(x1, x2), Y = Math.Min(y1, y2), W = Math.Max(.01, Math.Abs(x2 - x1)), H = Math.Max(.01, Math.Abs(y2 - y1)), Fill = "transparent", Stroke = "#5B8DEF", StrokeWidth = 1.5 });
            selected = style.Layers.Count - 1;
            selectedText = false;
            selectedLayers.Clear();
            selectedLayers.Add(selected);
            RefreshLayerList(selected);
            CommitHistory();
            SetTool("select");
            return;
        }

        double x = Canvas.GetLeft(draft), y = Canvas.GetTop(draft), w = draft.Width, h = draft.Height;
        DesignCanvas.Children.Remove(draft);
        draft = null;
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(w) || !double.IsFinite(h) || w < 4 || h < 4)
            return;
        if (tool == "text")
        {
            style.TextRegion = new TextRegion
            {
                X = x / 9,
                Y = y / 5.2,
                W = w / 9,
                H = h / 5.2
            };
            selected = -1;
            selectedLayers.Clear();
            selectedText = true;
            Status(T("文字区域已创建", "Text region created"));
        }
        else
        {
            style.Layers.Add(new StyleLayer { Type = tool == "round" ? "rect" : tool, X = x / 9, Y = y / 5.2, W = w / 9, H = h / 5.2, Fill = tool == "line" ? "transparent" : "#DDE9FF", Stroke = "#5B8DEF", StrokeWidth = 1.5, Radius = tool == "round" ? 14 : 0 });
            selected = style.Layers.Count - 1;
            selectedText = false;
            selectedLayers.Clear();
            selectedLayers.Add(selected);
        }

        LayerList.ItemsSource = style.Layers.Cast<object>().Append(style.TextRegion).ToList();
        SyncLayerSelection();
        LoadLayerFields();
        RebuildCanvas();
        RefreshPreview();
        CommitHistory();
        SetTool("select");
    }

    // EN: Search visible layers from front to back using inverse rotation and shape-specific hit geometry.
    // ZH: 从前向后查找可见图层，使用逆旋转和各图形专用命中几何。
    int HitLayer(Point p)
    {
        for (int i = style.Layers.Count - 1; i >= 0; i--)
        {
            var l = style.Layers[i];
            if (!l.IsVisible)
                continue;
            var x = l.X * 9;
            var y = l.Y * 5.2;
            var w = l.W * 9;
            var h = l.H * 5.2;
            var local = InverseRotate(p, new Point(x + w / 2, y + h / 2), l.Rotation);
            if (l.Type == "line")
            {
                var a = new Point((l.X1 ?? l.X) * 9, (l.Y1 ?? l.Y) * 5.2);
                var b = new Point((l.X2 ?? (l.X + l.W)) * 9, (l.Y2 ?? (l.Y + l.H)) * 5.2);
                if (DistanceToSegment(local, a, b) <= Math.Max(5, l.StrokeWidth / 2 + 3))
                    return i;
                continue;
            }

            if (l.Type == "ellipse")
            {
                if (w <= 0 || h <= 0)
                    continue;
                var nx = (local.X - (x + w / 2)) / (w / 2);
                var ny = (local.Y - (y + h / 2)) / (h / 2);
                if (nx * nx + ny * ny <= 1.05)
                    return i;
                continue;
            }

            if (local.X >= x && local.X <= x + w && local.Y >= y && local.Y <= y + h)
                return i;
        }

        return -1;
    }

    // EN: Transform a point back into an unrotated layer frame for hit testing.
    // ZH: 将点逆变换到未旋转图层坐标系以进行命中判断。
    static Point InverseRotate(Point p, Point center, double degrees)
    {
        if (Math.Abs(degrees) < .001)
            return p;
        var a = -degrees * Math.PI / 180;
        var dx = p.X - center.X;
        var dy = p.Y - center.Y;
        return new(center.X + dx * Math.Cos(a) - dy * Math.Sin(a), center.Y + dx * Math.Sin(a) + dy * Math.Cos(a));
    }

    // EN: Measure the closest distance to a bounded line segment, treating a zero-length segment as a point.
    // ZH: 计算到有限线段的最近距离，将零长度线段作为点处理。
    static double DistanceToSegment(Point p, Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= .0001)
            return (p - a).Length;
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared, 0, 1);
        return (p - new Point(a.X + t * dx, a.Y + t * dy)).Length;
    }

    // EN: Hit-test only a visible text region with positive dimensions in design-canvas coordinates.
    // ZH: 仅对可见且尺寸为正的文字区域进行设计画布命中判断。
    bool HitTextRegion(Point p)
    {
        var t = style.TextRegion;
        return t.IsVisible && t.W > 0 && t.H > 0 && p.X >= t.X * 9 && p.X <= (t.X + t.W) * 9 && p.Y >= t.Y * 5.2 && p.Y <= (t.Y + t.H) * 5.2;
    }

    // EN: Build the effect-source ellipse or rounded rectangle in the target layer's local coordinates, including source rotation.
    // ZH: 在目标图层局部坐标中构建效果源椭圆或圆角矩形，并包含源旋转。
    Geometry ClipGeometry(StyleLayer source, StyleLayer current, double sx, double sy)
    {
        var x = (source.X - current.X) * sx;
        var y = (source.Y - current.Y) * sy;
        var w = Math.Max(1, source.W * sx);
        var h = Math.Max(1, source.H * sy);
        Geometry g = source.Type == "ellipse" ? new EllipseGeometry(new Rect(x, y, w, h)) : new RectangleGeometry(new Rect(x, y, w, h), source.Radius, source.Radius);
        if (Math.Abs(source.Rotation) > .01)
            g.Transform = new RotateTransform(source.Rotation, x + w / 2, y + h / 2);
        return g;
    }

    // EN: Decode permitted embedded image data into a frozen bitmap and return null for malformed payloads.
    // ZH: 将允许的内嵌图片数据解码为冻结位图，数据损坏时返回空值。
    static ImageSource? ImageSourceFromData(string? value)
    {
        if (!ModelSafety.IsSupportedImageData(value))
            return null;
        try
        {
            var comma = value!.IndexOf(',');
            using var stream = new MemoryStream(Convert.FromBase64String(value[(comma + 1)..]));
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    // EN: Build clipping/masking choices relative to the adjacent lower layer, disabling effects on the bottom layer.
    // ZH: 创建相对于下方相邻图层的剪切和蒙版选项，并禁用最底层的效果。
    ContextMenu LayerContextMenu(int index)
    {
        var menu = new ContextMenu();
        var info = new MenuItem
        {
            Header = index > 0 ? T("作用于下方相邻图层", "Uses the adjacent layer below") : T("最底层没有下方图层", "Bottom layer has no layer below"),
            IsEnabled = false
        };
        menu.Items.Add(info);
        menu.Items.Add(new Separator());
        var cut = new MenuItem
        {
            Header = T("剪切", "Clip")
        };
        var mask = new MenuItem
        {
            Header = T("蒙版", "Mask")
        };
        menu.Items.Add(cut);
        menu.Items.Add(mask);
        menu.Items.Add(new Separator());
        // EN: Append a checked/enabled context-menu choice whose click executes the supplied layer-effect action.
        // ZH: 追加带勾选和启用状态的菜单选项，点击时执行传入的图层效果操作。
        MenuItem Add(MenuItem parent, string title, Action action, bool check, bool enabled = true)
        {
            var item = new MenuItem
            {
                Header = title,
                IsCheckable = true,
                IsChecked = check,
                IsEnabled = enabled
            };
            item.Click += /* EN: Execute the action assigned to this context-menu option. ZH: 执行分配给此右键菜单选项的操作。 */ (_, _) => action();
            parent.Items.Add(item);
            return item;
        }

        var l = style.Layers[index];
        Add(cut, T("不剪切", "No clip"), /* EN: Clear both clipping and masking for this layer. ZH: 清除此图层的剪切和蒙版。 */ () => SetLayerEffect(index, "none", "none"), l.ClipMode == "none");
        Add(cut, T("将本图形作为切刀：保留下方图层的相交部分", "Use this shape as cutter: keep intersection on layer below"), /* EN: Use this layer as the intersection cutter for the layer below. ZH: 将此图层作为下方图层的相交切刀。 */ () => SetLayerEffect(index, "intersect", "none"), l.ClipMode == "intersect", index > 0);
        Add(cut, T("将本图形作为切刀：从下方图层挖掉本图形", "Use this shape as cutter: subtract it from layer below"), /* EN: Use this layer as the subtraction cutter for the layer below. ZH: 将此图层作为下方图层的相减切刀。 */ () => SetLayerEffect(index, "subtract", "none"), l.ClipMode == "subtract", index > 0);
        Add(mask, T("没有蒙版", "No mask"), /* EN: Clear both masking and clipping from the mask submenu. ZH: 从蒙版子菜单清除蒙版和剪切。 */ () => SetLayerEffect(index, "none", "none"), l.MaskMode == "none");
        Add(mask, T("下方图层 Alpha 蒙版", "Alpha mask from layer below"), /* EN: Select the alpha mask mode and clear clipping. ZH: 选择透明度蒙版并清除剪切。 */ () => SetLayerEffect(index, "none", "alpha"), l.MaskMode == "alpha", index > 0);
        Add(mask, T("反向 Alpha 蒙版", "Inverse alpha mask"), /* EN: Select the inverse alpha mask mode and clear clipping. ZH: 选择反向透明度蒙版并清除剪切。 */ () => SetLayerEffect(index, "none", "inverseAlpha"), l.MaskMode == "inverseAlpha", index > 0);
        Add(mask, T("下方图层亮度蒙版", "Luminance mask from layer below"), /* EN: Select the luminance mask mode and clear clipping. ZH: 选择亮度蒙版并清除剪切。 */ () => SetLayerEffect(index, "none", "luminance"), l.MaskMode == "luminance", index > 0);
        Add(mask, T("反向亮度蒙版", "Inverse luminance mask"), /* EN: Select the inverse luminance mask mode and clear clipping. ZH: 选择反向亮度蒙版并清除剪切。 */ () => SetLayerEffect(index, "none", "inverseLuminance"), l.MaskMode == "inverseLuminance", index > 0);
        var up = new MenuItem
        {
            Header = T("上移一层", "Move layer up")
        };
        up.Click += /* EN: Move the context-selected layer group upward. ZH: 将右键选中图层组上移。 */ (_, _) => MoveLayer(1);
        menu.Items.Add(up);
        var down = new MenuItem
        {
            Header = T("下移一层", "Move layer down")
        };
        down.Click += /* EN: Move the context-selected layer group downward. ZH: 将右键选中图层组下移。 */ (_, _) => MoveLayer(-1);
        menu.Items.Add(down);
        return menu;
    }

    // EN: Assign the selected clip/mask mode, refresh the layer list, and commit the effect change.
    // ZH: 写入选定剪切或蒙版模式、刷新图层列表并记录效果变更。
    void SetLayerEffect(int index, string clip, string mask)
    {
        if (index < 0 || index >= style.Layers.Count || style.Layers[index].IsLocked)
            return;
        FlushPendingHistory();
        style.Layers[index].ClipMode = clip;
        style.Layers[index].MaskMode = mask;
        RefreshLayerList(index);
        CommitHistory();
        Status(clip == "subtract" ? T("剪切已生效：橙色实线是切刀轮廓，内部透明区域是挖孔", "Cut applied: orange outline is the cutter; the transparent interior is the hole") : clip == "intersect" ? T("相交剪切已生效：仅保留下方图层与切刀重叠部分", "Intersection applied: only the overlap with the layer below remains") : T("图层效果已应用", "Layer effect applied"));
    }

    // EN: Produce geometry and interaction visuals; shared pixel composition applies blend and mask effects before these selection affordances.
    // ZH: 生成几何及交互视觉；共享像素合成先应用混合和蒙版效果，再显示选择辅助控件。
    FrameworkElement VisualFor(StyleLayer l, double sx, double sy, StyleLayer? effectSource = null, int layerIndex = -1)
    {
        FrameworkElement el;
        if (l.Type == "image")
        {
            el = new Image
            {
                Source = ImageSourceFromData(l.ImageData),
                Stretch = Stretch.Fill
            };
        }
        else
        {
            Shape shape = l.Type == "ellipse" ? new Ellipse() : l.Type == "line" ? new Line
            {
                X1 = ((l.X1 ?? l.X) - l.X) * sx,
                Y1 = ((l.Y1 ?? l.Y) - l.Y) * sy,
                X2 = ((l.X2 ?? (l.X + l.W)) - l.X) * sx,
                Y2 = ((l.Y2 ?? (l.Y + l.H)) - l.Y) * sy,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            }

            : new Rectangle
            {
                RadiusX = l.Radius,
                RadiusY = l.Radius
            };
            shape.Fill = l.Type == "line" ? Brushes.Transparent : BrushOf(l.Fill, "#DDE9FF");
            shape.Stroke = BrushOf(l.Stroke, "#5B8DEF");
            shape.StrokeThickness = l.StrokeWidth;
            el = shape;
        }

        el.Width = Math.Max(1, l.W * sx);
        el.Height = Math.Max(1, l.H * sy);
        el.Opacity = l.Opacity;
        el.RenderTransform = new RotateTransform(l.Rotation, el.Width / 2, el.Height / 2);
        Canvas.SetLeft(el, l.X * sx);
        Canvas.SetTop(el, l.Y * sy);
        var effectMode = effectSource?.ClipMode ?? "none";
        if (effectSource != null && effectMode != "none")
        {
            var source = ClipGeometry(effectSource, l, sx, sy);
            el.Clip = effectMode == "subtract" ? Geometry.Combine(new RectangleGeometry(new Rect(0, 0, el.Width, el.Height)), source, GeometryCombineMode.Exclude, null) : source;
        }

        if (layerIndex >= 0)
        {
            el.ContextMenu = LayerContextMenu(layerIndex);
            el.MouseRightButtonDown += /* EN: Make the right-clicked visual the primary property selection. ZH: 将右击视觉对象设为主要属性选择。 */ (_, _) =>
            {
                selected = layerIndex;
                LoadLayerFields();
            };
            el.ContextMenu.Closed += /* EN: Restore list selection to the current layer when its menu closes. ZH: 菜单关闭时将列表选择恢复到当前图层。 */ (_, _) =>
            {
                if (selected >= 0 && selected < style.Layers.Count)
                    LayerList.SelectedIndex = selected;
            };
        }

        return el;
    }

    // EN: Place eight layer handles that capture original geometry and start resizing only for unlocked layers.
    // ZH: 布置八个图层缩放手柄，记录初始几何数据，并仅对未锁定图层开始缩放。
    void AddResizeHandles(StyleLayer l)
    {
        var left = l.X * 9;
        var top = l.Y * 5.2;
        var right = (l.X + l.W) * 9;
        var bottom = (l.Y + l.H) * 5.2;
        var cx = (left + right) / 2;
        var cy = (top + bottom) / 2;
        var points = new[]
        {
            (left, top),
            (cx, top),
            (right, top),
            (right, cy),
            (right, bottom),
            (cx, bottom),
            (left, bottom),
            (left, cy)
        };
        var cursors = new[]
        {
            Cursors.SizeNWSE,
            Cursors.SizeNS,
            Cursors.SizeNESW,
            Cursors.SizeWE,
            Cursors.SizeNWSE,
            Cursors.SizeNS,
            Cursors.SizeNESW,
            Cursors.SizeWE
        };
        for (int i = 0; i < points.Length; i++)
        {
            int handle = i;
            var h = new Rectangle
            {
                Width = 10,
                Height = 10,
                Fill = Brushes.White,
                Stroke = BrushOf("#347FFF"),
                StrokeThickness = 2,
                Cursor = cursors[i]
            };
            Canvas.SetLeft(h, points[i].Item1 - 5);
            Canvas.SetTop(h, points[i].Item2 - 5);
            h.MouseLeftButtonDown += /* EN: Start layer resizing from captured original bounds unless the layer is locked. ZH: 图层未锁定时按记录的原始边界开始缩放。 */ (_, e) =>
            {
                if (l.IsLocked)
                {
                    Status(T("图层已锁定", "Layer is locked"));
                    e.Handled = true;
                    return;
                }

                resizing = true;
                resizeTargetText = false;
                resizeHandle = handle;
                down = e.GetPosition(DesignCanvas);
                resizeX = l.X;
                resizeY = l.Y;
                resizeW = l.W;
                resizeH = l.H;
                DesignCanvas.CaptureMouse();
                e.Handled = true;
            };
            DesignCanvas.Children.Add(h);
        }
    }

    // EN: Place eight text-region handles with the same resize state while enforcing the region lock.
    // ZH: 布置八个文字区域手柄，复用缩放状态并处理文字区域锁定。
    void AddTextResizeHandles()
    {
        var t = style.TextRegion;
        var left = t.X * 9;
        var top = t.Y * 5.2;
        var right = (t.X + t.W) * 9;
        var bottom = (t.Y + t.H) * 5.2;
        var cx = (left + right) / 2;
        var cy = (top + bottom) / 2;
        var points = new[]
        {
            (left, top),
            (cx, top),
            (right, top),
            (right, cy),
            (right, bottom),
            (cx, bottom),
            (left, bottom),
            (left, cy)
        };
        var cursors = new[]
        {
            Cursors.SizeNWSE,
            Cursors.SizeNS,
            Cursors.SizeNESW,
            Cursors.SizeWE,
            Cursors.SizeNWSE,
            Cursors.SizeNS,
            Cursors.SizeNESW,
            Cursors.SizeWE
        };
        for (int i = 0; i < points.Length; i++)
        {
            var handle = i;
            var h = new Rectangle
            {
                Width = 10,
                Height = 10,
                Fill = Brushes.White,
                Stroke = BrushOf("#E64D5F"),
                StrokeThickness = 2,
                Cursor = cursors[i]
            };
            Canvas.SetLeft(h, points[i].Item1 - 5);
            Canvas.SetTop(h, points[i].Item2 - 5);
            h.MouseLeftButtonDown += /* EN: Start text-region resizing from captured original bounds unless it is locked. ZH: 文字区域未锁定时按记录的原始边界开始缩放。 */ (_, e) =>
            {
                if (t.IsLocked)
                {
                    Status(T("文字输入区域已锁定", "The text input region is locked"));
                    e.Handled = true;
                    return;
                }

                resizing = true;
                resizeTargetText = true;
                resizeHandle = handle;
                down = e.GetPosition(DesignCanvas);
                resizeX = t.X;
                resizeY = t.Y;
                resizeW = t.W;
                resizeH = t.H;
                DesignCanvas.CaptureMouse();
                e.Handled = true;
            };
            DesignCanvas.Children.Add(h);
        }
    }

    // EN: Resize the chosen edges, optionally preserve corner aspect, clamp to the canvas, and rescale line endpoints.
    // ZH: 缩放指定边缘，可在角点保持比例，限制在画布内并缩放直线端点。
    void ResizeSelected(Point p)
    {
        // EN: Validate the current lock at execution time as well as at handle capture, including the text-region resize path.
        // ZH: 除手柄捕获时检查外，执行缩放时再次验证当前锁定，包含文字区域路径。
        if (SelectionLocked() || resizeTargetText && style.TextRegion.IsLocked) return;
        if (!resizing || (!resizeTargetText && (selected < 0 || selected >= style.Layers.Count)))
            return;
        double dx = (p.X - down.X) / 9, dy = (p.Y - down.Y) / 5.2, x = resizeX, y = resizeY, w = resizeW, h = resizeH;
        bool moveLeft = resizeHandle is 0 or 6 or 7, moveRight = resizeHandle is 2 or 3 or 4, moveTop = resizeHandle is 0 or 1 or 2, moveBottom = resizeHandle is 4 or 5 or 6;
        if (moveLeft)
        {
            x = resizeX + dx;
            w = resizeW - dx;
        }

        if (moveRight)
            w = resizeW + dx;
        if (moveTop)
        {
            y = resizeY + dy;
            h = resizeH - dy;
        }

        if (moveBottom)
            h = resizeH + dy;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && (moveLeft || moveRight) && (moveTop || moveBottom))
        {
            var ratio = resizeW / Math.Max(.01, resizeH);
            if (w / Math.Max(.01, h) > ratio)
                h = w / ratio;
            else
                w = h * ratio;
            if (moveLeft)
                x = resizeX + resizeW - w;
            if (moveTop)
                y = resizeY + resizeH - h;
        }

        if (w < 1)
        {
            if (moveLeft)
                x -= 1 - w;
            w = 1;
        }

        if (h < 1)
        {
            if (moveTop)
                y -= 1 - h;
            h = 1;
        }

        x = Math.Clamp(x, 0, 99);
        y = Math.Clamp(y, 0, 99);
        w = Math.Clamp(w, 1, 100 - x);
        h = Math.Clamp(h, 1, 100 - y);
        if (resizeTargetText)
        {
            var t = style.TextRegion;
            t.X = x;
            t.Y = y;
            t.W = w;
            t.H = h;
        }
        else
        {
            var target = style.Layers[selected];
            if (target.Type == "line")
            {
                var rx1 = ((target.X1 ?? resizeX) - resizeX) / Math.Max(.01, resizeW);
                var ry1 = ((target.Y1 ?? resizeY) - resizeY) / Math.Max(.01, resizeH);
                var rx2 = ((target.X2 ?? (resizeX + resizeW)) - resizeX) / Math.Max(.01, resizeW);
                var ry2 = ((target.Y2 ?? (resizeY + resizeH)) - resizeY) / Math.Max(.01, resizeH);
                target.X1 = x + rx1 * w;
                target.Y1 = y + ry1 * h;
                target.X2 = x + rx2 * w;
                target.Y2 = y + ry2 * h;
            }

            target.X = x;
            target.Y = y;
            target.W = w;
            target.H = h;
        }

        RebuildCanvas();
        LoadLayerFields();
    }

    // EN: Recreate visible design layers, selected cutter outlines, the text-region guide, and resize handles.
    // ZH: 重建可见设计图层、选中切刀轮廓、文字区域辅助线及缩放手柄。
    void RebuildCanvas()
    {
        ModelSafety.Normalize(style);
        DesignCanvas.Children.Clear();
        // EN: Reuse authored pixels when only selection changes, keeping the existing model geometry for hit testing and resize handles.
        // ZH: 仅选择变化时复用设计像素，保留现有模型几何用于命中及缩放手柄。
        var signature = JsonSerializer.Serialize(style.Layers, json);
        if (lastDesignPixels == null || lastDesignVisualSnapshot != signature)
        {
            lastDesignPixels = LayerCompositor.Render(style.Layers, 900, 520, (layer, sx, sy) => VisualFor(layer, sx, sy));
            lastDesignVisualSnapshot = signature;
        }
        DesignCanvas.Children.Add(new Image { Width = 900, Height = 520, Source = lastDesignPixels, IsHitTestVisible = false });
        for (int i = 0; i < style.Layers.Count; i++)
        {
            var layer = style.Layers[i];
            if (!layer.IsVisible)
                continue;
            bool cutter = i > 0 && (layer.ClipMode != "none" || layer.MaskMode != "none");
            if (cutter && !selectedLayers.Contains(i))
                continue;
            var effectSource = i + 1 < style.Layers.Count && style.Layers[i + 1].IsVisible ? style.Layers[i + 1] : null;
            var el = VisualFor(layer, 9, 5.2, cutter ? null : effectSource, i);
            // EN: Keep per-layer context-menu hit surfaces transparent; only selected outlines are drawn over the composited image.
            // ZH: 保留透明的逐图层右键命中表面，仅在合成图片上显示选中轮廓。
            if (!cutter) el.Opacity = 0;
            if (cutter && el is Shape cutterOutline)
            {
                cutterOutline.Fill = Brushes.Transparent;
                cutterOutline.Stroke = BrushOf("#FF8A00");
                cutterOutline.StrokeThickness = 2.5;
                cutterOutline.Opacity = 1;
                el.ToolTip = T("橙色实线仅表示切刀轮廓；内部透明区域就是挖孔", "The orange outline marks the cutter; its transparent interior is the hole");
            }

            if (selectedLayers.Contains(i) && !cutter)
            {
                var outline = new Rectangle { Width = el.Width, Height = el.Height, Stroke = BrushOf("#3483FF"), StrokeThickness = i == selected ? 2 : 1, IsHitTestVisible = false, RenderTransform = el.RenderTransform };
                Canvas.SetLeft(outline, layer.X * 9); Canvas.SetTop(outline, layer.Y * 5.2);
                DesignCanvas.Children.Add(outline);
            }
            DesignCanvas.Children.Add(el);
        }

        var tr = style.TextRegion;
        if (tr.IsVisible && tr.W > 0 && tr.H > 0)
        {
            var r = new Rectangle
            {
                Width = tr.W * 9,
                Height = tr.H * 5.2,
                Stroke = BrushOf(selectedText ? "#347FFF" : "#E64D5F"),
                StrokeThickness = selectedText ? 3 : 2,
                StrokeDashArray = new DoubleCollection
                {
                    7,
                    4
                },
                Fill = BrushOf("#FFF5F688")
            };
            Canvas.SetLeft(r, tr.X * 9);
            Canvas.SetTop(r, tr.Y * 5.2);
            DesignCanvas.Children.Add(r);
            var label = new TextBlock
            {
                Text = T("文字输入区域", "Text input region"),
                Foreground = BrushOf("#B93345"),
                FontSize = 13
            };
            Canvas.SetLeft(label, tr.X * 9 + 8);
            Canvas.SetTop(label, tr.Y * 5.2 + 6);
            DesignCanvas.Children.Add(label);
        }

        if (selected >= 0 && selected < style.Layers.Count && !resizing)
            AddResizeHandles(style.Layers[selected]);
        else if (selectedText && tr.IsVisible && tr.W > 0 && tr.H > 0 && !resizing)
            AddTextResizeHandles();
    }

    // EN: Populate the selected object's property fields without triggering live writes and disable irrelevant layer controls.
    // ZH: 填充选中对象属性而不触发实时写入，并禁用不适用的图层控件。
    void LoadLayerFields()
    {
        var old = suppressLiveControlSync;
        suppressLiveControlSync = true;
        try
        {
            if (selectedText)
            {
                var t = style.TextRegion;
                SelectedObjectText.Text = T("选中对象：文字输入区域", "Selected: text input region");
                LayerNameBox.Text = t.DisplayName;
                LayerFillBox.Text = "transparent";
                LayerStrokeBox.Text = "#E64D5F";
                StrokeWidthBox.Text = "0";
                LayerRadiusBox.Text = "0";
                OpacityBox.Text = "1";
                RotationBox.Text = "0";
                XBox.Text = t.X.ToString("0.##");
                YBox.Text = t.Y.ToString("0.##");
                WBox.Text = t.W.ToString("0.##");
                HBox.Text = t.H.ToString("0.##");
                SetLayerSpecificEnabled(false);
            }
            else if (selected >= 0 && selected < style.Layers.Count)
            {
                var l = style.Layers[selected];
                SelectedObjectText.Text = T("选中对象：", "Selected: ") + l.DisplayName;
                LayerNameBox.Text = l.Name;
                LayerFillBox.Text = l.Fill;
                LayerStrokeBox.Text = l.Stroke;
                StrokeWidthBox.Text = l.StrokeWidth.ToString("0.##");
                LayerRadiusBox.Text = l.Radius.ToString("0.##");
                OpacityBox.Text = l.Opacity.ToString("0.###");
                RotationBox.Text = l.Rotation.ToString("0.##");
                BlendModeBox.SelectedIndex = Math.Max(0, BlendModeBox.Items.Cast<ComboBoxItem>().ToList().FindIndex( /* EN: Select the control option matching the stored blend mode. ZH: 选择与保存混合模式一致的控件选项。 */x => x.Tag?.ToString() == l.BlendMode));
                XBox.Text = l.X.ToString("0.##");
                YBox.Text = l.Y.ToString("0.##");
                WBox.Text = l.W.ToString("0.##");
                HBox.Text = l.H.ToString("0.##");
                SetLayerSpecificEnabled(!l.IsLocked);
            }
            else
            {
                SelectedObjectText.Text = T("选中对象", "Selected object");
                SetLayerSpecificEnabled(false);
            }

            foreach (var field in new[] { XBox, YBox, WBox, HBox }) field.IsEnabled = !SelectionLocked() && (selectedText || selected >= 0);
            UpdateColorSwatches();
        }
        finally
        {
            suppressLiveControlSync = old;
        }
    }

    // EN: Enable visual-layer properties together while leaving geometry fields available for text-region editing.
    // ZH: 统一启用图层外观属性，同时保留文字区域可编辑的几何字段。
    void SetLayerSpecificEnabled(bool enabled)
    {
        LayerNameBox.IsEnabled = enabled;
        LayerFillBox.IsEnabled = enabled;
        LayerStrokeBox.IsEnabled = enabled;
        StrokeWidthBox.IsEnabled = enabled;
        LayerRadiusBox.IsEnabled = enabled;
        OpacityBox.IsEnabled = enabled;
        RotationBox.IsEnabled = enabled;
        BlendModeBox.IsEnabled = enabled;
        LayerFillColorBtn.IsEnabled = enabled;
        LayerStrokeColorBtn.IsEnabled = enabled;
    }

    // EN: Validate selected-object fields, preserve line endpoint proportions, normalize the model, and refresh list labels.
    // ZH: 校验选中对象字段、保留直线端点比例、规范化模型并刷新列表标签。
    void ApplyLayerFields()
    {
        if (SelectionLocked()) return;
        if (!TryReadNumber(XBox, 0, 100, out var x) || !TryReadNumber(YBox, 0, 100, out var y) || !TryReadNumber(WBox, .01, 100, out var w) || !TryReadNumber(HBox, .01, 100, out var h))
            return;
        x = Math.Clamp(x, 0, 99.99);
        y = Math.Clamp(y, 0, 99.99);
        w = Math.Clamp(w, .01, 100 - x);
        h = Math.Clamp(h, .01, 100 - y);
        if (selectedText)
        {
            var t = style.TextRegion;
            t.X = x;
            t.Y = y;
            t.W = w;
            t.H = h;
            LayerList.Items.Refresh();
            return;
        }

        if (selected < 0 || selected >= style.Layers.Count)
            return;
        var l = style.Layers[selected];
        if (!TryReadColor(LayerFillBox, true, out var fill) || !TryReadColor(LayerStrokeBox, true, out var stroke) || !TryReadNumber(StrokeWidthBox, 0, 100, out var strokeWidth) || !TryReadNumber(LayerRadiusBox, 0, 500, out var radius) || !TryReadNumber(OpacityBox, 0, 1, out var opacity) || !TryReadNumber(RotationBox, -36000, 36000, out var rotation))
            return;
        if (l.Type == "line")
        {
            var rx1 = ((l.X1 ?? l.X) - l.X) / Math.Max(.01, l.W);
            var ry1 = ((l.Y1 ?? l.Y) - l.Y) / Math.Max(.01, l.H);
            var rx2 = ((l.X2 ?? (l.X + l.W)) - l.X) / Math.Max(.01, l.W);
            var ry2 = ((l.Y2 ?? (l.Y + l.H)) - l.Y) / Math.Max(.01, l.H);
            l.X1 = x + rx1 * w;
            l.Y1 = y + ry1 * h;
            l.X2 = x + rx2 * w;
            l.Y2 = y + ry2 * h;
        }

        l.Name = LayerNameBox.Text.Trim();
        l.Fill = fill;
        l.Stroke = stroke;
        l.StrokeWidth = strokeWidth;
        l.Radius = radius;
        l.Opacity = opacity;
        l.Rotation = rotation;
        l.BlendMode = (BlendModeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "normal";
        l.X = x;
        l.Y = y;
        l.W = w;
        l.H = h;
        ModelSafety.Normalize(style);
        LayerList.Items.Refresh();
        UpdateColorSwatches();
    }

    // EN: Delete selected layers in descending index order and optionally clear the unique text region before committing history.
    // ZH: 按索引倒序删除选中图层，可同时清空唯一文字区域，再记录历史。
    void DeleteSelected()
    {
        if (SelectionLocked()) return;
        FlushPendingHistory();
        var deleting = selectedLayers.Where( /* EN: Keep deletion indices within the current layer collection. ZH: 将删除索引限制在当前图层集合内。 */i => i >= 0 && i < style.Layers.Count).OrderByDescending( /* EN: Delete higher indices first so remaining indices stay valid. ZH: 先删除较大索引，确保其他索引继续有效。 */i => i).ToList();
        if (deleting.Count == 0 && selected >= 0 && selected < style.Layers.Count)
            deleting.Add(selected);
        var deletedText = selectedText;
        if (deleting.Count == 0 && !deletedText)
            return;
        foreach (var i in deleting)
            style.Layers.RemoveAt(i);
        if (deletedText)
            style.TextRegion = new TextRegion();
        selectedLayers.Clear();
        selectedText = false;
        RefreshLayerList(deleting.Count > 0 ? Math.Min(deleting.Min(), style.Layers.Count - 1) : -1);
        CommitHistory();
    }

    // EN: Rebuild list items and selection, then refresh selected fields, canvas geometry, and preview.
    // ZH: 重建列表项与选择，然后刷新选中字段、画布几何和预览。
    void RefreshLayerList(int index) => RefreshLayerList(index >= 0 ? [index] : []);
    // EN: Rebuild list items and selection, then refresh selected fields, canvas geometry, and preview.
    // ZH: 重建列表项与选择，然后刷新选中字段、画布几何和预览。
    void RefreshLayerList(IEnumerable<int> indices)
    {
        var selectedSet = indices.Where( /* EN: Filter requested selection to currently existing layers. ZH: 将请求选择过滤为当前存在图层。 */i => i >= 0 && i < style.Layers.Count).ToHashSet();
        LayerList.ItemsSource = null;
        LayerList.ItemsSource = style.Layers.Cast<object>().Append(style.TextRegion).ToList();
        selectedLayers.Clear();
        foreach (var index in selectedSet)
            selectedLayers.Add(index);
        selected = selectedLayers.Count > 0 ? selectedLayers.Max() : -1;
        SyncLayerSelection();
        LoadLayerFields();
        RebuildCanvas();
        RefreshPreview();
    }

    // EN: Move the selected layer group one list position while retaining group order and refusing edge overflow.
    // ZH: 保持组选中顺序并移动一个列表位置，拒绝越过顶部或底部。
    void MoveLayer(int delta)
    {
        if (SelectionLocked()) return;
        FlushPendingHistory();
        var moving = selectedLayers.Count > 0 ? selectedLayers.OrderBy( /* EN: Preserve source order when collecting a group for one-step movement. ZH: 收集单步移动组时保留原顺序。 */i => i).Select( /* EN: Resolve each ordered movement index to its layer. ZH: 将排序后的移动索引解析为图层。 */i => style.Layers[i]).ToList() : selected >= 0 && selected < style.Layers.Count ? [style.Layers[selected]] : [];
        if (moving.Count == 0)
            return;
        var first = style.Layers.IndexOf(moving.First());
        var last = style.Layers.IndexOf(moving.Last());
        if ((delta > 0 && last == style.Layers.Count - 1) || (delta < 0 && first == 0))
        {
            Status(delta > 0 ? T("已经是最上层", "Already at top") : T("已经是最下层", "Already at bottom"));
            return;
        }

        foreach (var layer in moving)
            style.Layers.Remove(layer);
        var insert = delta > 0 ? first + 1 : first - 1;
        style.Layers.InsertRange(Math.Clamp(insert, 0, style.Layers.Count), moving);
        var indices = moving.Select( /* EN: Recover selected indices after group reinsertion. ZH: 图层组重新插入后恢复选择索引。 */layer => style.Layers.IndexOf(layer)).ToList();
        RefreshLayerList(indices);
        CommitHistory();
        Status(delta > 0 ? T("图层组已上移", "Layer group moved up") : T("图层组已下移", "Layer group moved down"));
    }

    // EN: Move the selected layer group to the first or last drawing positions without reversing internal order.
    // ZH: 将选中图层组移动到最底部或最顶部绘制位置，保持组内顺序。
    void MoveLayerExtreme(bool top)
    {
        if (SelectionLocked()) return;
        FlushPendingHistory();
        var moving = selectedLayers.Count > 0 ? selectedLayers.OrderBy( /* EN: Preserve source order when collecting a group for extreme movement. ZH: 收集置顶或置底组时保留原顺序。 */i => i).Select( /* EN: Resolve each extreme-movement index to its layer. ZH: 将置顶或置底索引解析为图层。 */i => style.Layers[i]).ToList() : selected >= 0 && selected < style.Layers.Count ? [style.Layers[selected]] : [];
        if (moving.Count == 0)
            return;
        foreach (var layer in moving)
            style.Layers.Remove(layer);
        var insert = top ? style.Layers.Count : 0;
        style.Layers.InsertRange(insert, moving);
        RefreshLayerList(moving.Select( /* EN: Recover selection after moving the group to an edge. ZH: 将组移到边缘后恢复选择。 */layer => style.Layers.IndexOf(layer)));
        CommitHistory();
        Status(top ? T("图层组已置顶", "Layer group moved to top") : T("图层组已置底", "Layer group moved to bottom"));
    }

    // EN: Copy validated document fields to the style, sanitize its ID, normalize sizing/rules, and refresh color buttons.
    // ZH: 将有效文档字段写入样式、清理 ID、规范化尺寸与规则，并刷新颜色按钮。
    void ReadControls()
    {
        style.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? "自定义样式" : NameBox.Text.Trim();
        var id = Regex.Replace(IdBox.Text.Trim().ToLowerInvariant(), "[^a-z0-9_-]+", "-").Trim('-');
        style.Id = string.IsNullOrEmpty(id) ? "custom-style" : id;
        if (TryReadColor(BgBox, true, out var bg))
            style.Bg = bg;
        if (TryReadColor(BorderBox, true, out var border))
            style.Border = border;
        if (TryReadColor(ColorBox, false, out var color))
            style.Color = color;
        if (TryReadNumber(RadiusBox, 0, 500, out var radius))
            style.Radius = radius;
        if (TryReadNumber(BorderWidthBox, 0, 100, out var borderWidth))
            style.BorderWidth = borderWidth;
        if (TryReadNumber(FontWeightBox, 1, 1000, out var fontWeight))
            style.FontWeight = fontWeight;
        style.Shadow = ShadowBox.Text.Trim();
        style.Padding = PaddingBox.Text.Trim();
        style.FontFamily = FontBox.SelectedItem?.ToString() ?? SystemFonts.MessageFontFamily.Source;
        if (TryReadNumber(FontSizeBox, 1, 500, out var fontSize))
            style.FontSize = fontSize;
        style.ReplaceFrame = true;
        style.TextSizing.Mode = TextSizingModes.Normalize((SizeModeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString());
        if (TryReadRatio(AspectBox, .01, 100, out var aspect))
            style.TextSizing.Aspect = aspect;
        ReadInputRuleControls();
        ModelSafety.Normalize(style);
        UpdateColorSwatches();
    }

    // EN: Update rule fields, disable WPF UTF-16 length enforcement, and highlight invalid custom regex syntax.
    // ZH: 更新规则字段、禁用 WPF 的 UTF-16 长度限制，并标记无效自定义正则语法。
    void ReadInputRuleControls()
    {
        if (TryReadNumber(MaxLengthBox, 0, ProductLimits.MaximumCharacters, out var maximum))
            style.TextRules.MaxLength = Math.Clamp((int)maximum, 0, ProductLimits.MaximumCharacters);
        style.TextRules.Type = (InputTypeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "any";
        style.TextRules.Pattern = PatternBox.Text;
        style.TextRules.Required = RequiredBox.IsChecked == true;
        PreviewText.MaxLength = 0;
        if (style.TextRules.Type == "regex" && !InputRuleValidator.IsPatternValid(style.TextRules.Pattern, out var error))
        {
            PatternBox.BorderBrush = BrushOf("#E64D5F");
            PatternBox.ToolTip = error;
            ValidationText.Text = T("正则表达式无效，保存和同步已阻止：", "Invalid regular expression; save and sync are blocked: ") + error;
        }
        else
        {
            PatternBox.BorderBrush = SystemColors.ControlDarkBrush;
            PatternBox.ToolTip = null;
        }
    }

    // EN: Apply rule edits immediately and refresh preview validity plus saved/synced snapshot indicators.
    // ZH: 立即应用规则修改，并刷新预览有效性及保存同步快照指示。
    void LivePreviewRulesChanged(object? sender, RoutedEventArgs e)
    {
        if (suppressLiveControlSync)
            return;
        ReadInputRuleControls();
        RefreshPreview();
        UpdateSyncState();
        UpdateDocumentState();
        QueueLiveHistory();
    }

    // EN: Apply selector changes to style/layer fields, update sizing controls, and refresh preview and dirty state.
    // ZH: 将选择器变化应用到样式和图层字段，更新尺寸控件并刷新预览及修改状态。
    void LivePreviewStyleChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (suppressLiveControlSync)
            return;
        ReadControls();
        ApplyLayerFields();
        UpdateSizingControlState();
        RefreshPreview();
        UpdateSyncState();
        UpdateDocumentState();
        RebuildCanvas();
        QueueLiveHistory();
    }

    // EN: Reflect general property edits in the model and preview unless controls are being loaded programmatically.
    // ZH: 非程序加载控件时，将通用属性修改反映到模型与预览。
    void LiveStyleControlChanged(object? sender, RoutedEventArgs e)
    {
        if (suppressLiveControlSync)
            return;
        ReadControls();
        UpdateSizingControlState();
        RefreshPreview();
        UpdateSyncState();
        UpdateDocumentState();
        QueueLiveHistory();
    }

    // EN: Reflect selected visual-layer edits immediately while ignoring unloaded or absent selections.
    // ZH: 立即反映选中外观图层的修改，并忽略加载中或不存在的选择。
    void LiveLayerControlChanged(object? sender, RoutedEventArgs e)
    {
        if (suppressLiveControlSync || !selectedText && (selected < 0 || selected >= style.Layers.Count) || SelectionLocked())
            return;
        ApplyLayerFields();
        RebuildCanvas();
        RefreshPreview();
        UpdateSyncState();
        UpdateDocumentState();
        QueueLiveHistory();
    }

    // EN: Allow manual aspect editing only for the fixed-aspect sizing mode.
    // ZH: 仅在固定宽高比尺寸模式中允许手动修改比例。
    void UpdateSizingControlState()
    {
        var uniform = TextSizingModes.Normalize((SizeModeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()) == TextSizingModes.Uniform;
        AspectBox.IsEnabled = uniform;
    }

    // EN: Normalize imported state, fill controls with live events suppressed, and rebuild canvas/preview with migration feedback.
    // ZH: 规范化导入状态，抑制实时事件填充控件，再重建画布和预览并提示迁移结果。
    void LoadControls()
    {
        var migratedMaximum = style.TextRules?.MaxLength > ProductLimits.MaximumCharacters;
        ModelSafety.Normalize(style);
        suppressLiveControlSync = true;
        try
        {
            var rules = style.TextRules ??= new();
            var sizing = style.TextSizing ??= new();
            NameBox.Text = style.Name;
            IdBox.Text = style.Id;
            BgBox.Text = style.Bg;
            BorderBox.Text = style.Border;
            ColorBox.Text = style.Color;
            RadiusBox.Text = style.Radius.ToString();
            BorderWidthBox.Text = style.BorderWidth.ToString("0.##");
            ShadowBox.Text = style.Shadow;
            FontWeightBox.Text = style.FontWeight.ToString("0");
            PaddingBox.Text = style.Padding;
            FontSizeBox.Text = style.FontSize.ToString();
            AspectBox.Text = sizing.Aspect.ToString("0.###");
            MaxLengthBox.Text = rules.MaxLength.ToString();
            PatternBox.Text = rules.Pattern;
            RequiredBox.IsChecked = rules.Required;
            FontBox.SelectedItem = FontBox.Items.Cast<string>().FirstOrDefault( /* EN: Match the stored font name case-insensitively. ZH: 不区分大小写匹配保存的字体名称。 */x => x.Equals(style.FontFamily, StringComparison.OrdinalIgnoreCase)) ?? FontBox.Items.Cast<string>().FirstOrDefault();
            SizeModeBox.SelectedIndex = Math.Max(0, SizeModeBox.Items.Cast<ComboBoxItem>().ToList().FindIndex( /* EN: Select the stable tag matching the normalized sizing mode. ZH: 选择与规范化尺寸模式匹配的稳定标签。 */x => x.Tag?.ToString() == sizing.Mode));
            InputTypeBox.SelectedIndex = Math.Max(0, InputTypeBox.Items.Cast<ComboBoxItem>().ToList().FindIndex( /* EN: Select the stable tag matching the stored input type. ZH: 选择与保存输入类型匹配的稳定标签。 */x => x.Tag?.ToString() == rules.Type));
        }
        finally
        {
            suppressLiveControlSync = false;
        }

        UpdateSizingControlState();
        PreviewText.MaxLength = 0;
        LayerList.ItemsSource = style.Layers.Cast<object>().Append(style.TextRegion).ToList();
        LoadLayerFields();
        UpdateColorSwatches();
        RebuildCanvas();
        RefreshPreview();
        if (migratedMaximum)
            Status(T($"旧文件的最多字符数已迁移为产品上限 {ProductLimits.MaximumCharacters}", $"The legacy maximum length was migrated to the product limit of {ProductLimits.MaximumCharacters}"));
    }

    // EN: Restart the 80 ms debounce timer so typing does not rebuild the preview on every event.
    // ZH: 重启八十毫秒防抖计时器，避免每次输入事件都立即重建预览。
    void SchedulePreviewRefresh()
    {
        previewTimer.Stop();
        previewTimer.Start();
    }

    // EN: Interpret supported one-, two-, or four-value CSS-like padding and clamp extracted values to safe nonnegative bounds.
    // ZH: 解释支持的一、二或四值类 CSS 内边距，并将提取值限制为安全非负范围。
    static Thickness ParsePadding(string? value)
    {
        var parts = Regex.Matches(value ?? "", @"-?\d+(?:\.\d+)?").Select( /* EN: Parse each padding number and clamp it to zero through five hundred. ZH: 解析各内边距数字并限制在零到五百之间。 */x => double.TryParse(x.Value, out var n) ? Math.Clamp(n, 0, 500) : 0).ToArray();
        return parts.Length switch
        {
            1 => new(parts[0]),
            2 => new(parts[1], parts[0], parts[1], parts[0]),
            4 => new(parts[3], parts[0], parts[1], parts[2]),
            _ => new(18, 11, 18, 11)};
    }

    // EN: Synchronize card framing, typography, cached layer visuals, text-region placement, validity, and size-limit feedback.
    // ZH: 同步预览卡片外框、字体、缓存图层视觉、文字区域位置、有效性及尺寸上限提示。
    void RefreshPreview()
    {
        ModelSafety.Normalize(style);
        bool replace = style.ReplaceFrame && style.Layers.Any( /* EN: Enable replacement framing only when a visible layer exists. ZH: 仅在存在可见图层时启用替换外框。 */x => x.IsVisible);
        ApplyPreviewSizing();
        double pw = double.IsNaN(PreviewCard.Width) ? 420 : PreviewCard.Width, ph = double.IsNaN(PreviewCard.Height) ? 150 : PreviewCard.Height;
        previewCapped = pw >= ProductLimits.MaximumPreviewDimension - .01 || ph >= ProductLimits.MaximumPreviewDimension - .01;
        PreviewCard.Padding = replace ? new Thickness(0) : ParsePadding(style.Padding);
        PreviewCard.BorderThickness = replace ? new Thickness(0) : new Thickness(style.BorderWidth);
        PreviewCard.CornerRadius = new CornerRadius(style.Radius);
        // EN: Render the whole CSS frame behind content so inset shadows remain above background and outer shadows stay outside transparent interiors.
        // ZH: 在内容后方渲染完整 CSS 外框，使内阴影位于底色上方，外阴影保持在透明内部之外。
        QueueFramePreview(pw, ph, replace);
        PreviewText.Foreground = BrushOf(style.Color, "#2C3140");
        PreviewText.FontFamily = new FontFamily(style.FontFamily);
        PreviewText.FontSize = style.FontSize;
        PreviewText.FontWeight = FontWeight.FromOpenTypeWeight((int)Math.Clamp(style.FontWeight, 1, 999));
        PreviewText.VerticalContentAlignment = VerticalAlignment.Center;
        PreviewLayers.Width = pw;
        PreviewLayers.Height = ph;
        var visualSnapshot = JsonSerializer.Serialize(style.Layers, json) + $"|{pw:0.###}|{ph:0.###}";
        if (!string.Equals(lastPreviewVisualSnapshot, visualSnapshot, StringComparison.Ordinal))
        {
            PreviewLayers.Children.Clear();
            // EN: Use the same bounded compositor as the design canvas so image alpha, luminance masks and blend modes have identical semantics.
            // ZH: 与设计画布共用有限栅格合成器，使图片透明度、亮度蒙版及混合模式语义一致。
            PreviewLayers.Children.Add(new Image { Width = pw, Height = ph, Source = LayerCompositor.Render(style.Layers, pw, ph, (layer, sx, sy) => VisualFor(layer, sx, sy)), IsHitTestVisible = false });

            lastPreviewVisualSnapshot = visualSnapshot;
        }

        var tr = style.TextRegion;
        if (replace && tr.IsVisible && tr.W > 0 && tr.H > 0)
        {
            PreviewText.HorizontalAlignment = HorizontalAlignment.Left;
            PreviewText.VerticalAlignment = VerticalAlignment.Top;
            PreviewText.Width = tr.W * pw / 100;
            PreviewText.Height = tr.H * ph / 100;
            PreviewText.Margin = new Thickness(tr.X * pw / 100, tr.Y * ph / 100, 0, 0);
        }
        else
        {
            PreviewText.HorizontalAlignment = HorizontalAlignment.Stretch;
            PreviewText.VerticalAlignment = VerticalAlignment.Center;
            PreviewText.Width = double.NaN;
            PreviewText.Height = double.NaN;
            PreviewText.Margin = new Thickness(0);
        }

        ApplyPreviewValidity();
        if (previewCapped)
            Status(T($"预览已达到 {ProductLimits.MaximumPreviewDimension:0}×{ProductLimits.MaximumPreviewDimension:0} 上限，超出内容不会继续放大", $"Preview reached the {ProductLimits.MaximumPreviewDimension:0}×{ProductLimits.MaximumPreviewDimension:0} limit"));
    }

    // EN: Keep a safe WPF frame while the latest immutable CSS request runs; skip identical requests and suppress replaced frames as the main app does.
    // ZH: 最新不可变 CSS 请求执行期间保留安全 WPF 外框；跳过相同请求，并与主程序一样隐藏被组合替换的外框。
    void QueueFramePreview(double width, double height, bool replace)
    {
        // EN: The design tab and initial Home screen must not start a browser; entering Preview queues the latest complete style.
        // ZH: 设计页和初始开始页不可启动浏览器；进入预览页时才排队渲染最新完整样式。
        if (WorkTabs.SelectedIndex != 1)
        {
            framePreviewCancellation?.Cancel(); lastFramePreviewKey = null; framePreviewTask = Task.CompletedTask;
            return;
        }
        var key = JsonSerializer.Serialize(new { width, height, replace, style.Shadow, style.Bg, style.Border, style.BorderWidth, style.Radius, style.Color, style.FontSize });
        if (lastFramePreviewKey == key) return;
        lastFramePreviewKey = key;
        FramePreviewStatus.ToolTip = null;
        framePreviewCancellation?.Cancel(); framePreviewCancellation?.Dispose();
        framePreviewCancellation = new CancellationTokenSource(); var token = framePreviewCancellation.Token;
        PreviewShadow.Children.Clear(); PreviewShadow.Width = width; PreviewShadow.Height = height;
        PreviewShadow.Margin = PreviewCard.Margin = new Thickness(24);
        PreviewCard.Background = replace ? Brushes.Transparent : LayerCompositor.CssBrush(style.Bg);
        PreviewCard.BorderBrush = replace ? Brushes.Transparent : LayerCompositor.CssBrush(style.Border);
        ShadowBox.ToolTip = T("CSS 内/外阴影由 Microsoft Edge 离屏渲染；继承值使用主程序默认主题、16px 字号及独立预览 640×420 视口。", "CSS inset/outer shadows use headless Microsoft Edge; inherited values use the default main theme, 16px font and a 640×420 preview viewport.");
        if (replace) { FramePreviewStatus.Text = ""; framePreviewTask = Task.CompletedTask; return; }
        FramePreviewStatus.Text = T("正在渲染 CSS 外框…", "Rendering CSS frame…");
        framePreviewTask = RefreshFrameAsync(width, height, style.Radius, style.Shadow, style.Bg, style.Border, style.BorderWidth, style.Color, style.FontSize, token);
    }

    // EN: Debounce field edits, publish only the current complete frame, and expose runtime/invalid-CSS/resource diagnostics without disabling editing.
    // ZH: 对字段编辑防抖，仅发布当前完整外框，并显示运行时、无效 CSS、资源诊断，同时保持编辑可用。
    async Task RefreshFrameAsync(double width, double height, double radius, string shadow, string background, string border, double borderWidth, string color, double fontSize, CancellationToken token)
    {
        try
        {
            await Task.Delay(150, token).ConfigureAwait(false);
            var frame = await FrameShadow.RenderAsync(width, height, radius, shadow, background, border, borderWidth, color, fontSize, token).ConfigureAwait(false);
            // EN: Marshal publication explicitly because initial window construction may run before a WPF synchronization context is installed.
            // ZH: 显式将发布切换到界面线程，因为初始窗口构造可能早于 WPF 同步上下文安装。
            await Dispatcher.InvokeAsync(() =>
            {
                token.ThrowIfCancellationRequested();
                FrameShadow.Present(PreviewShadow, width, height, frame);
                // EN: Include complete shadow bounds in scrolling layout rather than clipping at the old fixed card margin.
                // ZH: 将完整阴影边界计入滚动布局，避免在旧固定卡片外边距处裁切。
                PreviewShadow.Margin = PreviewCard.Margin = new Thickness(24 + Math.Max(0, -frame.Left), 24 + Math.Max(0, -frame.Top), 24 + Math.Max(0, frame.Left + frame.Width - width), 24 + Math.Max(0, frame.Top + frame.Height - height));
                PreviewCard.Background = Brushes.Transparent; PreviewCard.BorderBrush = Brushes.Transparent;
                FramePreviewStatus.ToolTip = null;
                FramePreviewStatus.Text = frame.InvalidShadow ? T("CSS 阴影无效；与主程序一样不显示阴影。", "Invalid CSS shadow; no shadow is rendered, as in the main app.") : frame.Reduced ? T("CSS 外框已按完整范围降采样至四百万像素；大阴影可能较柔和。", "The complete CSS frame is downsampled to four million pixels; large shadows may appear softer.") : T("CSS 外框预览已更新。", "CSS frame preview updated.");
            }, DispatcherPriority.Render, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (token.IsCancellationRequested) return;
            // EN: Report failures on the owning dispatcher, ignoring windows closed while the browser was running.
            // ZH: 在所属分派器报告失败，忽略浏览器运行期间已关闭的窗口。
            await Dispatcher.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested) return;
                FramePreviewStatus.Text = T("CSS 外框预览不可用；可继续编辑和保存。请安装/修复 Microsoft Edge 后重试。", "CSS frame preview unavailable; editing and saving remain available. Install/repair Microsoft Edge and retry.");
                FramePreviewStatus.ToolTip = error.GetBaseException().Message;
                lastFramePreviewKey = null;
            });
        }
    }

    // EN: Assign the measured card dimensions to the preview container.
    // ZH: 将实测卡片尺寸赋给预览容器。
    void ApplyPreviewSizing()
    {
        if (PreviewCard == null || style == null)
            return;
        var size = CalculateMeasuredPreviewSize();
        PreviewCard.Width = size.Width;
        PreviewCard.Height = size.Height;
    }

    // EN: Start at one-character capacity, then fit real font measurements using uniform scale search or independent-axis growth.
    // ZH: 从单字容量开始，再通过等比缩放搜索或独立轴增长容纳真实字体测量结果。
    PreviewDimensions CalculateMeasuredPreviewSize()
    {
        const double maximum = ProductLimits.MaximumPreviewDimension;
        var text = PreviewText?.Text ?? "";
        var baseline = PreviewSizingCalculator.Calculate(style, text, MeasureText("字", double.PositiveInfinity));
        var region = style.TextRegion;
        if (text.Length == 0 || region.W <= 0 || region.H <= 0)
            return baseline;
        var widthFraction = Math.Clamp(region.W / 100, .001, 1);
        var heightFraction = Math.Clamp(region.H / 100, .001, 1);
        var mode = TextSizingModes.Normalize(style.TextSizing.Mode);
        // EN: Check whether measured wrapped text plus layout safety fits a candidate card's percentage text region.
        // ZH: 检查实测换行文字加布局余量是否适合候选卡片的百分比文字区域。
        bool Fits(double cardWidth, double cardHeight)
        {
            var required = MeasurePreviewText(cardWidth * widthFraction);
            var layoutSafety = InputRuleValidator.CountCharacters(text) > 2 ? Math.Max(4, style.FontSize * .35) : 0;
            return required.Width <= cardWidth * widthFraction + .5 && required.Height + layoutSafety <= cardHeight * heightFraction + .5;
        }

        if (Fits(baseline.Width, baseline.Height))
        {
            if (mode == TextSizingModes.Uniform && InputRuleValidator.CountCharacters(text) > 2)
            {
                var safetyScale = Math.Min(1.1, Math.Min(maximum / baseline.Width, maximum / baseline.Height));
                return new(baseline.Width * safetyScale, baseline.Height * safetyScale);
            }

            return baseline;
        }

        if (mode == TextSizingModes.Uniform)
        {
            var maximumScale = Math.Min(maximum / baseline.Width, maximum / baseline.Height);
            var low = 1d;
            var high = 1d;
            while (high < maximumScale && !Fits(baseline.Width * high, baseline.Height * high))
                high = Math.Min(maximumScale, high * 2);
            if (!Fits(baseline.Width * high, baseline.Height * high))
                return new(baseline.Width * high, baseline.Height * high);
            for (var i = 0; i < 32; i++)
            {
                var middle = (low + high) / 2;
                if (Fits(baseline.Width * middle, baseline.Height * middle))
                    high = middle;
                else
                    low = middle;
            }

            var finalScale = Math.Min(high * 1.08, maximumScale);
            return new(baseline.Width * finalScale, baseline.Height * finalScale);
        }

        // EN: Stretch starts at the composition baseline and grows only overflowing axes; layers and text share PreviewCard coordinates.
        // ZH: 自由拉伸从构图基准开始，仅扩展溢出的轴；图层和文字共用 PreviewCard 坐标。
        var baseRegionWidth = Math.Max(1, baseline.Width * widthFraction);
        var natural = MeasurePreviewText(double.PositiveInfinity);
        var targetRegionWidth = Math.Max(baseRegionWidth, Math.Min(420, natural.Width));
        var width = Math.Min(maximum, targetRegionWidth / widthFraction);
        var wrapped = MeasurePreviewText(width * widthFraction);
        var height = Math.Min(maximum, Math.Max(baseline.Height, (wrapped.Height + (InputRuleValidator.CountCharacters(text) > 2 ? Math.Max(4, style.FontSize * .35) : 0)) / heightFraction));
        return new(width, height);
    }

    // EN: Measure a WPF TextBlock with editor typography and textbox insets, caching finite results by font, width, and text.
    // ZH: 使用编辑器字体及文本框内边距测量 WPF 文本，并按字体、宽度和内容缓存有限结果。
    PreviewDimensions MeasureText(string text, double regionWidth)
    {
        var cacheKey = $"{style.FontFamily}|{style.FontSize:0.###}|{style.FontWeight:0.###}|{regionWidth:0.###}|{text}";
        if (measurementCache.TryGetValue(cacheKey, out var cached))
            return cached;
        var padding = PreviewText?.Padding ?? new Thickness(6, 4, 6, 4);
        var horizontalInsets = padding.Left + padding.Right + 4;
        var verticalInsets = padding.Top + padding.Bottom + 4;
        FontFamily family;
        try
        {
            family = new FontFamily(style.FontFamily);
        }
        catch
        {
            family = SystemFonts.MessageFontFamily;
        }

        var probe = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = family,
            FontSize = Math.Clamp(style.FontSize, 1, 500),
            FontWeight = FontWeight.FromOpenTypeWeight((int)Math.Clamp(style.FontWeight, 1, 999))
        };
        var availableWidth = double.IsFinite(regionWidth) ? Math.Max(1, regionWidth - horizontalInsets) : double.PositiveInfinity;
        probe.Measure(new Size(availableWidth, double.PositiveInfinity));
        var width = probe.DesiredSize.Width + horizontalInsets;
        var height = probe.DesiredSize.Height + verticalInsets;
        var result = new PreviewDimensions(double.IsFinite(width) && width > 0 ? width : Math.Max(1, style.FontSize + horizontalInsets), double.IsFinite(height) && height > 0 ? height : Math.Max(1, style.FontSize * 1.4 + verticalInsets));
        if (measurementCache.Count > 96)
            measurementCache.Clear();
        measurementCache[cacheKey] = result;
        return result;
    }

    // EN: Measure the current preview value at the supplied available text-region width.
    // ZH: 按给定文字区域宽度测量当前预览内容。
    PreviewDimensions MeasurePreviewText(double regionWidth) => MeasureText(PreviewText?.Text ?? "", regionWidth);
    // EN: Build the prospective value by replacing the current selection, matching normal text input and paste semantics.
    // ZH: 替换当前选择区以构建预期值，与正常文字输入及粘贴语义一致。
    string ProposedPreviewText(string inserted)
    {
        var start = PreviewText.SelectionStart;
        return PreviewText.Text.Remove(start, PreviewText.SelectionLength).Insert(start, inserted);
    }

    // EN: Validate a prospective preview value against the current document's shared input rules.
    // ZH: 按当前文档的共享输入规则校验预期预览内容。
    bool IsAllowed(string value) => InputRuleValidator.IsAllowed(value, style.TextRules);
    // EN: Show a red preview border when the complete value violates required or format rules.
    // ZH: 完整内容违反必填或格式规则时，在预览中显示红色边框。
    void ApplyPreviewValidity()
    {
        var valid = InputRuleValidator.IsCompleteValueValid(PreviewText.Text, style.TextRules);
        PreviewText.BorderThickness = valid ? new Thickness(0) : new Thickness(1.5);
        PreviewText.BorderBrush = valid ? Brushes.Transparent : BrushOf("#E64D5F");
    }

    // EN: Reject an insertion if the resulting whole preview value violates the active rules.
    // ZH: 插入后的完整预览内容违反当前规则时拒绝输入。
    void ValidatePreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (!IsAllowed(ProposedPreviewText(e.Text)))
        {
            e.Handled = true;
            Status(T("输入不符合当前限制", "Input does not match the current rules"));
        }
    }

    // EN: Read pasted text and cancel the paste if replacement of the current selection would violate the rules.
    // ZH: 读取粘贴文字，若替换当前选择后违反规则则取消粘贴。
    void PreviewPaste(object sender, DataObjectPastingEventArgs e)
    {
        var text = e.DataObject.GetData(DataFormats.UnicodeText) as string ?? e.DataObject.GetData(DataFormats.Text) as string ?? "";
        if (!IsAllowed(ProposedPreviewText(text)))
        {
            e.CancelCommand();
            Status(T("粘贴内容不符合当前限制", "Pasted text does not match the current rules"));
        }
    }

    // EN: Guard unsaved work, load a single style or validated library, choose an entry, and reset document snapshots/history.
    // ZH: 保护未保存修改、读取单样式或已校验样式库、选择条目，并重置文档快照与历史。
    bool OpenStyle()
    {
        if (!GuardUnsaved())
            return false;
        var d = new OpenFileDialog
        {
            Filter = StyleFileExtensions.OpenFilter
        };
        if (d.ShowDialog() != true)
            return false;
        try
        {
            var text = File.ReadAllText(d.FileName);
            using var document = JsonDocument.Parse(text);
            StyleLibrary library;
            var isLibrary = document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("styles", out _);
            if (isLibrary)
                library = StyleLibraryStore.Load(d.FileName, true).Library;
            else
            {
                var single = JsonSerializer.Deserialize<TextBoxStyle>(text, json) ?? throw new InvalidDataException(T("样式文件为空", "The style file is empty"));
                ModelSafety.Normalize(single);
                library = new StyleLibrary
                {
                    Styles = [single]
                };
            }

            if (library.Styles.Count == 0)
                throw new InvalidDataException(T("样式库中没有样式", "The style library contains no styles"));
            var index = library.Styles.Count == 1 ? 0 : ChooseStyle(library);
            if (index < 0)
                return false;
            currentLibrary = library;
            currentLibraryIndex = index;
            style = library.Styles[index];
            currentFile = d.FileName;
            lastSyncedId = null;
            lastSyncedSnapshot = null;
            selected = -1;
            selectedLayers.Clear();
            selectedText = false;
            LoadControls();
            lastSavedSnapshot = Snapshot();
            ResetHistory();
            UpdateDocumentState(lastSavedSnapshot);
            UpdateSyncState();
            Status(T($"已打开 {style.Name}（库内共 {library.Styles.Count} 套样式）", $"Opened {style.Name} ({library.Styles.Count} styles in library)"));
            return true;
        }
        catch (StyleLibraryException ex)
        {
            MessageBox.Show(T("样式库无效，原文件未被覆盖。损坏副本：", "The style library is invalid and was not overwritten. Corrupt backup: ") + (ex.BackupFile ?? T("未创建", "not created")) + "\n\n" + ex.Message, T("无法打开样式", "Could not open style"), MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        catch (Exception ex)
        {
            MessageBox.Show(T("无法打开样式：", "Could not open style: ") + ex.Message);
            return false;
        }
    }

    // EN: Display library names and IDs in a modal picker and return the selected index or cancellation sentinel.
    // ZH: 在模态选择器显示库内名称和 ID，返回选中索引或取消标记。
    int ChooseStyle(StyleLibrary library)
    {
        var list = new ListBox
        {
            MinWidth = 420,
            MinHeight = 240,
            ItemsSource = library.Styles.Select( /* EN: Display both human-readable names and stable IDs in the library chooser. ZH: 在样式库选择器同时显示名称与稳定 ID。 */x => $"{x.Name}  —  {x.Id}").ToList(),
            SelectedIndex = 0,
            Margin = new Thickness(12)
        };
        var open = new Button
        {
            Content = T("打开所选样式", "Open selected style"),
            IsDefault = true,
            MinWidth = 120,
            Margin = new Thickness(6)
        };
        var cancel = new Button
        {
            Content = T("取消", "Cancel"),
            IsCancel = true,
            MinWidth = 88,
            Margin = new Thickness(6)
        };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttons.Children.Add(open);
        buttons.Children.Add(cancel);
        var panel = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Bottom);
        panel.Children.Add(buttons);
        panel.Children.Add(list);
        var dialog = new Window
        {
            Owner = this,
            Title = T($"选择要编辑的样式（{library.Styles.Count}）", $"Choose a style to edit ({library.Styles.Count})"),
            Content = panel,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize
        };
        open.Click += /* EN: Accept the currently selected library entry. ZH: 接受当前选中的样式库条目。 */ (_, _) => dialog.DialogResult = true;
        return dialog.ShowDialog() == true ? list.SelectedIndex : -1;
    }

    // EN: Validate persistable fields and text region, preserve sibling library entries, and write atomically with same-file revision protection.
    // ZH: 校验可保存字段及文字区域、保留同库其他条目，并在同文件写入时使用版本保护进行原子保存。
    bool SaveStandalone() => SaveStandalone(false);
    // EN: Validate persistable fields and text region, preserve sibling library entries, and write atomically with same-file revision protection.
    // ZH: 校验可保存字段及文字区域、保留同库其他条目，并在同文件写入时使用版本保护进行原子保存。
    bool SaveStandalone(bool saveAs)
    {
        FlushPendingHistory();
        if (!ValidateBeforePersistence() || !EnsureTextRegion())
            return false;
        var file = saveAs ? null : currentFile;
        if (string.IsNullOrWhiteSpace(file))
        {
            var d = new SaveFileDialog
            {
                Filter = StyleFileExtensions.SaveFilter,
                DefaultExt = StyleFileExtensions.DefaultExtension,
                FileName = style.Id + StyleFileExtensions.DefaultExtension
            };
            if (!string.IsNullOrEmpty(currentFile))
                d.InitialDirectory = IOPath.GetDirectoryName(currentFile);
            if (d.ShowDialog() != true)
                return false;
            file = d.FileName;
        }

        try
        {
            var result = SaveStandaloneTo(file);
            Status(T($"样式已保存：{result.FilePath}", $"Style saved: {result.FilePath}"));
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(T("保存失败，原文件保持不变：", "Save failed; the original file was preserved: ") + ex.Message, T("保存失败", "Save failed"), MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    // EN: Write a detached standalone library candidate; only publish its association, revision and saved baseline after the atomic write succeeds.
    // ZH: 写入独立克隆的样式库候选，仅在原子写入成功后更新文件关联、版本及已保存基准。
    LibraryWriteResult SaveStandaloneTo(string file)
    {
        FlushPendingHistory();
        var library = currentLibrary == null ? new StyleLibrary() : JsonSerializer.Deserialize<StyleLibrary>(JsonSerializer.Serialize(currentLibrary, json), json)!;
        var index = currentLibraryIndex;
        if (index >= 0 && index < library.Styles.Count) library.Styles[index] = style;
        else { index = library.Styles.Count; library.Styles.Add(style); }
        if (library.Styles.Where((_, i) => i != index).Any(x => x.Id.Equals(style.Id, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException(T("库中已有相同样式 ID", "The library already contains this style ID"));
        var sameFile = currentFile != null && IOPath.GetFullPath(currentFile).Equals(IOPath.GetFullPath(file), StringComparison.OrdinalIgnoreCase);
        var written = StyleLibraryStore.Write(file, library, sameFile ? library.Revision : null);
        currentLibrary = library; currentLibraryIndex = index; currentFile = file;
        lastSavedSnapshot = Snapshot();
        UpdateDocumentState(lastSavedSnapshot);
        return written;
    }

    // EN: Reject invalid general numeric/color fields or regex syntax before any standalone or synchronized write.
    // ZH: 独立保存或同步写入前拒绝无效的通用数字、颜色字段或正则语法。
    bool ValidateBeforePersistence()
    {
        ValidationText.Text = "";
        ReadControls();
        ApplyLayerFields();
        var valid = TryReadColor(BgBox, true, out _) && TryReadColor(BorderBox, true, out _) && TryReadColor(ColorBox, false, out _) && TryReadNumber(RadiusBox, 0, 500, out _) && TryReadNumber(BorderWidthBox, 0, 100, out _) && TryReadNumber(FontWeightBox, 1, 1000, out _) && TryReadNumber(FontSizeBox, 1, 500, out _) && TryReadRatio(AspectBox, .01, 100, out _) && TryReadNumber(MaxLengthBox, 0, ProductLimits.MaximumCharacters, out _);
        if (style.TextRules.Type == "regex" && !InputRuleValidator.IsPatternValid(style.TextRules.Pattern, out var regexError))
        {
            ValidationText.Text = T("正则表达式无效：", "Invalid regular expression: ") + regexError;
            valid = false;
        }

        if (!valid)
        {
            Status(T("请先修正红色标出的属性", "Fix the highlighted properties first"));
            return false;
        }

        return true;
    }

    // EN: Require one visible positive-size text region and route the user to its drawing tool when missing.
    // ZH: 要求存在一个可见且尺寸为正的文字区域，缺失时引导到文字区域绘制工具。
    bool EnsureTextRegion()
    {
        if (style.TextRegion.IsVisible && style.TextRegion.W > 0 && style.TextRegion.H > 0)
            return true;
        ShowEditor();
        WorkTabs.SelectedIndex = 0;
        SetTool("text");
        MessageBox.Show(T("请先使用“文本区域”工具画出可见的文字输入区域，然后再保存。", "Draw a visible text input region with the Text Region tool before saving."), T("需要文字输入区域", "Text region required"), MessageBoxButton.OK, MessageBoxImage.Information);
        return false;
    }

    // EN: Write against the currently observed revision, using revision zero for a new library file.
    // ZH: 按当前读取的版本写入样式库，新文件使用版本零。
    void WriteLibrary(string file, StyleLibrary lib) => StyleLibraryStore.Write(file, lib, File.Exists(file) ? StyleLibraryStore.Load(file).Revision : 0);
    // EN: Resolve the chosen executable, source entry, or shortcut; save the connection only after locating its library.
    // ZH: 解析选定的程序、源码入口或快捷方式，定位样式库后才保存连接。
    bool Connect()
    {
        var d = new OpenFileDialog
        {
            Title = T("选择 KnotJot 主程序、源码 main.js 或桌面快捷方式", "Select KnotJot, source main.js, or a desktop shortcut"),
            Filter = T("KnotJot|*.exe;*.lnk;main.js|可执行文件|*.exe|快捷方式|*.lnk|main.js|main.js", "KnotJot|*.exe;*.lnk;main.js|Executable|*.exe|Shortcut|*.lnk|main.js|main.js")
        };
        if (d.ShowDialog() != true)
            return false;
        try
        {
            var target = ResolveTarget(d.FileName);
            if (!File.Exists(target))
                throw new FileNotFoundException(T("快捷方式指向的目标不存在", "Shortcut target does not exist"), target);
            var library = ResolveConnectedLibrary(target);
            SetConnected(target, library);
            SaveConnection();
            Status(T("连接成功，已取得 KnotJot 的真实样式库路径", "Connected and obtained KnotJot's actual style-library path"));
            return true;
        }
        catch (Exception ex)
        {
            Status(T("连接失败，仍保持原连接", "Connection failed; the previous connection is unchanged"));
            MessageBox.Show(T("连接失败：", "Connection failed: ") + ex.Message);
            return false;
        }
    }

    // EN: Resolve a Windows shortcut through WScript.Shell, leaving direct executable/source paths unchanged.
    // ZH: 通过 WScript.Shell 解析 Windows 快捷方式，直接程序或源码路径原样返回。
    static string ResolveTarget(string file)
    {
        if (!file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            return file;
        var t = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("系统快捷方式服务不可用");
        dynamic shell = Activator.CreateInstance(t)!;
        dynamic shortcut = shell.CreateShortcut(file);
        return (string)shortcut.TargetPath;
    }

    // EN: Resolve source libraries through the shared layout resolver; allow transient reply-write locks within the same 12-second deadline, validate JSON, and always attempt cleanup.
    // ZH: 通过共享布局解析器定位源码库；在同一十二秒期限内容忍响应写入的短暂锁，验证 JSON，并始终尝试清理。
    string ResolveConnectedLibrary(string target)
    {
        if (target.EndsWith("main.js", StringComparison.OrdinalIgnoreCase))
            return StyleLibraryLocation.ForSource(target);
        var response = IOPath.Combine(IOPath.GetTempPath(), $"knotjot-text-styles-location-{Environment.ProcessId}-{Guid.NewGuid():N}.json");
        try
        {
            var info = new ProcessStartInfo(target)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            info.ArgumentList.Add("--knotjot-text-styles-location-file=" + response);
            using var process = Process.Start(info) ?? throw new InvalidOperationException(T("无法启动路径查询", "Could not start the location query"));
            var deadline = DateTime.UtcNow.AddSeconds(12);
            while (!File.Exists(response) && DateTime.UtcNow < deadline)
            {
                // EN: A forwarded single-instance query may exit before its response arrives; keep the wait bounded without a busy loop.
                // ZH: 单实例转发查询可能先退出后返回响应，保持有限等待且避免忙循环。
                if (process.HasExited) Thread.Sleep(50); else process.WaitForExit(100);
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke( /* EN: Yield a dispatcher turn while waiting for the location-query response. ZH: 等待位置查询响应时让出一次界面分派执行机会。 */() =>
                {
                }, DispatcherPriority.Background);
            }

            if (!File.Exists(response))
                throw new TimeoutException(T("KnotJot 未返回样式库路径，请确认所选版本为 V1.0.0 或更高版本", "KnotJot did not return a style-library path; select V1.0.0 or later"));
            return StyleLibraryLocation.ParseReply(KnotJotConnection.ReadReplyText(response, deadline));
        }
        finally
        {
            try
            {
                if (File.Exists(response))
                    File.Delete(response);
            }
            catch
            {
            }
        }
    }

    // EN: Load the target revision, require confirmation before corrupt-library rebuild, allocate unique first-sync IDs, and atomically update the chosen style.
    // ZH: 读取目标版本、重建损坏库前要求确认、首次同步分配唯一 ID，并原子更新指定样式。
    void Sync(bool chooseAnother = false)
    {
        FlushPendingHistory();
        if (chooseAnother && !Connect())
            return;
        if (connectedTarget?.EndsWith("main.js", StringComparison.OrdinalIgnoreCase) == true)
            connectedLibraryPath = StyleLibraryLocation.ForSource(connectedTarget);
        if (connectedTarget == null || string.IsNullOrWhiteSpace(connectedLibraryPath) || !File.Exists(connectedTarget))
        {
            if (!Connect())
                return;
        }

        if (!ValidateBeforePersistence() || !EnsureTextRegion())
            return;
        var target = connectedTarget;
        var file = connectedLibraryPath;
        if (target == null || file == null)
            return;
        try
        {
            LoadedStyleLibrary loaded;
            try
            {
                loaded = StyleLibraryStore.Load(file, true);
            }
            catch (StyleLibraryException corrupt)
            {
                var answer = MessageBox.Show(T($"样式库解析失败，原文件未被覆盖。\n损坏备份：{corrupt.BackupFile}\n\n是否明确以空库重建？", $"The style library could not be parsed and was not overwritten.\nCorrupt backup: {corrupt.BackupFile}\n\nExplicitly rebuild it as an empty library?"), T("损坏的样式库", "Corrupt style library"), MessageBoxButton.YesNo, MessageBoxImage.Error);
                if (answer != MessageBoxResult.Yes)
                    return;
                File.Delete(file);
                loaded = new(new StyleLibrary(), file, 0, "", false);
            }

            var written = SyncLoadedLibrary(file, loaded);
            SetConnected(target, file);
            // EN: A connection-settings failure must not relabel a successfully written style as an unsuccessful sync.
            // ZH: 连接配置保存失败不能将已经成功写入的样式误标为同步失败。
            try { SaveConnection(); } catch (Exception configError) { App.LogException(configError); }
            UpdateSyncState(lastSyncedSnapshot);
            // EN: Compare the current document with the saved baseline; an unassociated standalone remains visibly dirty after library-only sync.
            // ZH: 用当前文档与保存基准比较；仅同步到库而未关联独立文件时，仍明确显示未保存状态。
            UpdateDocumentState();
            Status(T($"同步成功：{written.FilePath}（revision {written.Revision}）", $"Synced: {written.FilePath} (revision {written.Revision})"));
        }
        catch (Exception ex)
        {
            MessageBox.Show(T($"同步失败，目标未被部分覆盖：\n{file}\n", $"Sync failed; the target was not partially overwritten:\n{file}\n") + ex.Message, T("同步结果", "Sync result"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // EN: Preserve same-file entry ownership by its opened index, reject sibling ID collisions on a detached candidate, and advance each baseline only after its successful write.
    // ZH: 同文件按打开时的索引保留条目归属，在独立候选上拒绝同级 ID 冲突，各基准仅在对应写入成功后前进。
    LibraryWriteResult SyncLoadedLibrary(string file, LoadedStyleLibrary loaded)
    {
        FlushPendingHistory();
        var library = JsonSerializer.Deserialize<StyleLibrary>(JsonSerializer.Serialize(loaded.Library, json), json)!;
        bool sameFile = currentFile != null && IOPath.GetFullPath(currentFile).Equals(IOPath.GetFullPath(file), StringComparison.OrdinalIgnoreCase);
        int index;
        if (sameFile)
        {
            // EN: The mutable style ID is content, not destination ownership; the stored entry index is valid only for the revision originally opened.
            // ZH: 可变样式 ID 属于内容而非写入目标归属；保存的条目索引仅在原打开版本中有效。
            if (currentLibrary == null || currentLibraryIndex < 0 || currentLibraryIndex >= currentLibrary.Styles.Count || currentLibraryIndex >= library.Styles.Count)
                throw new InvalidDataException(T("当前文件缺少有效的样式条目关联，请重新打开。", "The current file has no valid style-entry association. Reopen it."));
            if (loaded.Revision != currentLibrary.Revision)
                throw new StyleLibraryException(T("样式库已被外部修改，请重新加载后再同步。", "The style library changed outside this editor. Reload it before syncing."), file);
            index = currentLibraryIndex;
            if (library.Styles.Where((_, i) => i != index).Any(sibling => sibling.Id.Equals(style.Id, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException(T("库中已有相同样式 ID", "The library already contains this style ID"));
        }
        else index = lastSyncedId != null && lastSyncedId.Equals(style.Id, StringComparison.OrdinalIgnoreCase) ? library.Styles.FindIndex(entry => entry.Id.Equals(style.Id, StringComparison.OrdinalIgnoreCase)) : -1;
        bool updating = sameFile || lastSyncedId != null && lastSyncedId.Equals(style.Id, StringComparison.OrdinalIgnoreCase);
        if (!updating)
        {
            var unique = StyleIdentity.UniqueId(style.Id, [library]);
            if (style.Id != unique) { style.Id = unique; IdBox.Text = unique; FlushPendingHistory(); }
        }
        if (index < 0) { index = library.Styles.Count; library.Styles.Add(style); } else library.Styles[index] = style;
        if (currentFile != null && !sameFile) SaveStandaloneTo(currentFile);
        // EN: If both destinations are the same file, protect the revision originally opened by this editor rather than overwriting external edits.
        // ZH: 两个目标为同一文件时保护编辑器最初打开的版本，避免覆盖外部修改。
        var written = StyleLibraryStore.Write(file, library, sameFile && currentLibrary != null ? currentLibrary.Revision : loaded.Revision);
        lastSyncedId = style.Id; lastSyncedSnapshot = Snapshot();
        if (sameFile) { currentLibrary = library; currentLibraryIndex = index; lastSavedSnapshot = lastSyncedSnapshot; }
        UpdateSyncState(); UpdateDocumentState();
        return written;
    }

    // EN: Launch source entries through their local Electron runtime with one path argument and source working directory; preserve installed EXE shell launch and request a connection when absent.
    // ZH: 源码入口由本地 Electron 运行时以单个路径参数及源码工作目录启动；保留已安装程序的外壳启动，无连接时先建立连接。
    void Launch()
    {
        if (connectedTarget == null)
        {
            Connect();
            if (connectedTarget == null)
                return;
        }

        try
        {
            var target = IOPath.GetFullPath(ResolveTarget(connectedTarget));
            if (target.EndsWith("main.js", StringComparison.OrdinalIgnoreCase))
            {
                if (!File.Exists(target))
                    throw new FileNotFoundException(T("KnotJot 源码 main.js 不存在，请重新选择主程序。", "KnotJot source main.js is missing; select the main application again."), target);
                var directory = IOPath.GetDirectoryName(target)!;
                var electron = IOPath.Combine(directory, "node_modules", "electron", "dist", "electron.exe");
                if (!File.Exists(electron))
                    throw new InvalidOperationException(T("KnotJot 源码目录缺少 node_modules/electron；请先安装主程序依赖，或选择已安装的 KnotJot.exe。", "The KnotJot source directory is missing node_modules/electron; install the main application's dependencies or select the installed KnotJot.exe."));
                var info = new ProcessStartInfo(electron) { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true };
                info.ArgumentList.Add(target);
                Process.Start(info);
            }
            else Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(T("无法打开主程序：", "Could not launch KnotJot: ") + ex.Message);
        }
    }

    // EN: Recognize controls whose own text-editing undo stack must retain history shortcuts.
    // ZH: 识别需要保留自身文字撤销快捷键的控件。
    static bool IsTextEditingFocus(IInputElement? focused) => focused is TextBox or PasswordBox || focused is ComboBox { IsEditable: true };
    // EN: Handle Ctrl+Z, Ctrl+Y, and Ctrl+Shift+Z only when a text editor does not own focus.
    // ZH: 仅在文字编辑控件未持有焦点时处理撤销与两种重做快捷键。
    bool HandleHistoryShortcut(Key key, ModifierKeys modifiers, bool textInputFocused)
    {
        if (textInputFocused || !modifiers.HasFlag(ModifierKeys.Control))
            return false;
        if (key == Key.Z)
        {
            if (modifiers.HasFlag(ModifierKeys.Shift))
                Redo();
            else
                Undo();
            return true;
        }

        if (key == Key.Y)
        {
            Redo();
            return true;
        }

        return false;
    }

    // EN: Route bracket commands explicitly to single-step or extreme ordering; shared command handlers enforce object locks.
    // ZH: 将方括号命令明确分派为单步或置顶置底排序，共享处理器负责对象锁定。
    bool HandleLayerOrderShortcut(Key key, ModifierKeys modifiers)
    {
        if (!modifiers.HasFlag(ModifierKeys.Control) || key is not (Key.OemCloseBrackets or Key.OemOpenBrackets)) return false;
        bool top = key == Key.OemCloseBrackets;
        if (modifiers.HasFlag(ModifierKeys.Shift)) MoveLayerExtreme(top); else MoveLayer(top ? 1 : -1);
        return true;
    }

    // EN: Route save, history, tool, ordering, delete, and nudge shortcuts while respecting focused text controls and movement locks.
    // ZH: 分派保存、历史、工具、排序、删除及微移快捷键，并尊重文字控件焦点与移动锁定。
    void OnKey(object sender, KeyEventArgs e)
    {
        var modifiers = e.KeyboardDevice.Modifiers;
        if (modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.S)
        {
            SaveStandalone(modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
            return;
        }

        if (HandleHistoryShortcut(e.Key, modifiers, IsTextEditingFocus(Keyboard.FocusedElement)))
        {
            e.Handled = true;
            return;
        }

        // EN: Text controls retain their own undo stack, and even noneditable combo focus blocks drawing-letter shortcuts.
        // ZH: 文字控件保留自身撤销栈，即使不可编辑下拉框获得焦点也会阻止绘图字母快捷键。
        if (Keyboard.FocusedElement is TextBox or PasswordBox or ComboBox)
            return;
        if (e.Key == Key.Delete)
        {
            DeleteSelected();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape || e.Key == Key.V)
        {
            SetTool("select");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.R)
        {
            SetTool("rect");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.E)
        {
            SetTool("ellipse");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.L)
        {
            SetTool("line");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.T)
        {
            SetTool("text");
            e.Handled = true;
            return;
        }

        if (HandleLayerOrderShortcut(e.Key, modifiers))
        {
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down && (selectedText || selected >= 0 && selected < style.Layers.Count))
        {
            double step = modifiers.HasFlag(ModifierKeys.Shift) ? .1 : 1;
            if (selectedText)
            {
                var t = style.TextRegion;
                if (t.IsLocked)
                {
                    Status(T("文字输入区域已锁定", "The text input region is locked"));
                    return;
                }

                if (e.Key == Key.Left)
                    t.X -= step;
                if (e.Key == Key.Right)
                    t.X += step;
                if (e.Key == Key.Up)
                    t.Y -= step;
                if (e.Key == Key.Down)
                    t.Y += step;
                t.X = Math.Clamp(t.X, 0, 100 - t.W);
                t.Y = Math.Clamp(t.Y, 0, 100 - t.H);
            }
            else
            {
                var l = style.Layers[selected];
                if (l.IsLocked)
                {
                    Status(T("图层已锁定", "Layer is locked"));
                    return;
                }

                var oldX = l.X;
                var oldY = l.Y;
                if (e.Key == Key.Left)
                    l.X -= step;
                if (e.Key == Key.Right)
                    l.X += step;
                if (e.Key == Key.Up)
                    l.Y -= step;
                if (e.Key == Key.Down)
                    l.Y += step;
                l.X = Math.Clamp(l.X, 0, 100 - l.W);
                l.Y = Math.Clamp(l.Y, 0, 100 - l.H);
                if (l.Type == "line")
                {
                    var dx = l.X - oldX;
                    var dy = l.Y - oldY;
                    l.X1 += dx;
                    l.X2 += dx;
                    l.Y1 += dy;
                    l.Y2 += dy;
                }
            }

            RebuildCanvas();
            LoadLayerFields();
            CommitHistory();
            e.Handled = true;
        }
    }
}
