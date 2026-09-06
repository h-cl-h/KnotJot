// Coordinate window lifecycle, document state, preview, and file commands.
// 协调窗口生命周期、文档状态、预览与文件命令。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KnotJotUiEditor
{
    /// <summary>
    /// 应用框架：开始页 / 最近使用 / 组件库 / 打开保存(含 V0.0.1 旧档迁移) / 部件调色面板 / WebView2 真实预览。
    /// 画布与图层交互在 MainWindowEditor.cs（partial）。
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _ready;
        private bool _webReady;
        private string _currentPath;
        private string _connectedTarget;
        private string _connectedLibraryPath;
        private string _skinId = KnotJotConnection.NewSkinId();
        private string _lastSyncedHash;
        private bool _warnedUnsavedSync;
        private bool _dirty;
        private bool _loadingPanel;
        private ProjectExportSettings _exportSettings = new ProjectExportSettings();

        private static readonly string RecentPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KnotJot界面编辑器", "recent.json");
        private static readonly string LegacyRecentPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BMAP界面编辑器", "recent.json");

        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        // 一键整套原版时的图层顺序（从底到顶）
        // Stable component creation order for the historical full-set project mode.
        // 历史整套工程模式使用的稳定部件创建顺序。
        private static readonly string[] FullSetOrder =
        {
            "stage", "brace", "link", "card", "cardSel", "toolbar", "title", "btn", "btnPrimary", "fname", "floatbar", "menu"
        };

        // Initialize the XAML window and attach its lifecycle and keyboard callbacks.
        // 初始化 XAML 窗口，并连接生命周期与键盘回调。
        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Closing += OnClosingWindow;
        }

        // Restore preferences, initialize editing panels, and choose the startup project or home page.
        // 恢复偏好设置、初始化编辑面板，并选择启动工程或首页。
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Prefs.Load();
            InitAdorners();
            InitDesign();
            WireEvents();
            WireShapePanel();
            WireInkImagePanels();
            WirePen();
            WireTextPanel();
            NewDoc();
            SetConnected(KnotJotConnection.RestoreConnection());
            _ready = true;
            string requested = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault(/* Find an existing supported project in startup arguments. 从启动参数查找存在且受支持的工程。 */ x => ProjectFileExtensions.IsSupported(x) && File.Exists(x));
            if (!string.IsNullOrEmpty(requested)) TryOpenPath(requested); else ShowHome();
        }

        // Cancel window closure when the shared unsaved-changes guard rejects leaving.
        // 共享未保存修改检查拒绝离开时，取消关闭窗口。
        private void OnClosingWindow(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!ConfirmDiscard()) e.Cancel = true;
        }

        // ================= WebView2 =================
        private string _pendingInject;   // 真实预览：导航完成后要注入的皮肤 CSS
        private bool _realLoaded;
        private string _realUrl;
        private string _previewDiagnostic;
        private string _lastPreviewCss = "";

        // Initialize WebView2 once and attach preview navigation and skin-injection callbacks.
        // 仅初始化一次 WebView2，并连接预览导航与皮肤注入回调。
        private async System.Threading.Tasks.Task EnsureWebAsync()
        {
            if (_webReady) return;
            try
            {
                await Web.EnsureCoreWebView2Async();
                Web.CoreWebView2.Settings.AreDevToolsEnabled = false;
                Web.CoreWebView2.Settings.IsStatusBarEnabled = false;
                Web.CoreWebView2.NavigationCompleted += /* Report failed navigation or inject pending CSS after navigation completes. 导航完成后报告失败或注入待应用 CSS。 */ async (s, e) =>
                {
                    if (!e.IsSuccess)
                    {
                        ShowPreviewError(new InvalidOperationException("WebView2 导航失败：" + e.WebErrorStatus), Prefs.PreviewSource == 1 ? "KnotJot 真实页面" : "内置预览", Prefs.PreviewSource == 1 ? Prefs.KnotJotPath : null, _lastPreviewCss);
                        return;
                    }
                    if (!string.IsNullOrEmpty(_pendingInject)) await InjectSkinAsync(_pendingInject);
                };
                _webReady = true;
            }
            catch (Exception ex)
            {
                ShowPreviewError(ex, "WebView2 初始化", null, _lastPreviewCss);
            }
        }

        // Insert or replace the preview style element using JSON-escaped CSS and report script failures.
        // 使用 JSON 转义后的 CSS 插入或替换预览样式元素，并报告脚本失败。
        private async System.Threading.Tasks.Task InjectSkinAsync(string css)
        {
            try
            {
                string js = "(function(){var id='knotjot-skin';var s=document.getElementById(id);"
                    + "if(!s){s=document.createElement('style');s.id=id;document.head.appendChild(s);}"
                    + "s.textContent=" + JsonSerializer.Serialize(css) + ";})();";
                await Web.CoreWebView2.ExecuteScriptAsync(js);
                HidePreviewError();
            }
            catch (Exception ex) { ShowPreviewError(ex, "KnotJot CSS 注入", Prefs.KnotJotPath, css); }
        }

        /// <summary>真实预览：把 KnotJot 的 index.html 加载进来并注入当前皮肤。</summary>
        // Map the configured application folder into WebView2 and load its real UI for skin preview.
        // 将配置的主程序目录映射至 WebView2，并加载真实界面预览皮肤。
        private async void LoadRealPreview(string css)
        {
            try
            {
                string folder = Path.GetDirectoryName(Prefs.KnotJotPath);
                string file = Path.GetFileName(Prefs.KnotJotPath);
                string url = "https://knotjot-preview/" + file;
                if (_realLoaded && _realUrl == url)
                {
                    _pendingInject = css;
                    await InjectSkinAsync(css);   // 已加载：只更新皮肤，不重新加载
                    return;
                }
                Web.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "knotjot-preview", folder, Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
                _pendingInject = css;
                _realUrl = url;
                _realLoaded = true;
                Web.CoreWebView2.Navigate(url);
                SetStatus("真实预览：已加载 KnotJot 本体并套用当前皮肤。");
            }
            catch (Exception ex)
            {
                ShowPreviewError(ex, "KnotJot 真实页面", Prefs.KnotJotPath, css);
            }
        }

        // Display the preview failure and prepare a diagnostic that omits CSS content.
        // 显示预览错误，并准备不包含 CSS 内容的诊断信息。
        private void ShowPreviewError(Exception ex, string source, string path, string css)
        {
            _previewDiagnostic = PreviewService.CreateDiagnostic(source, path, css, ex);
            TxtPreviewError.Text = ex.Message + "\n\n可重试、切回内置预览，或复制不含 CSS 内容的诊断信息。";
            PreviewErrorPanel.Visibility = Visibility.Visible;
            Web.Visibility = Visibility.Collapsed;
            SetStatus("预览失败：" + ex.Message);
        }

        // Hide the preview error panel and restore the WebView surface.
        // 隐藏预览错误面板并恢复 WebView 显示。
        private void HidePreviewError()
        {
            PreviewErrorPanel.Visibility = Visibility.Collapsed;
            Web.Visibility = Visibility.Visible;
        }

        // ================= 页面切换 =================
        // Hide the editor including its native WebView and rebuild the recent-project home page.
        // 隐藏含原生 WebView 的编辑器，并重建最近工程首页。
        private void ShowHome()
        {
            EditorRoot.Visibility = Visibility.Collapsed;
            HomeRoot.Visibility = Visibility.Visible;
            RenderRecent();
        }

        // Hide the home page and reveal the editing workspace.
        // 隐藏首页并显示编辑工作区。
        private void ShowEditor()
        {
            HomeRoot.Visibility = Visibility.Collapsed;
            EditorRoot.Visibility = Visibility.Visible;
        }

        // ================= 最近使用 =================
        private class RecentItem { public string path { get; set; } public string name { get; set; } public string type { get; set; } }

        // Read the recent-project list, tolerating missing or damaged preference data.
        // 读取最近工程列表，并容忍缺失或损坏的偏好数据。
        private List<RecentItem> LoadRecent()
        {
            try
            {
                string file = File.Exists(RecentPath) ? RecentPath : LegacyRecentPath;
                if (File.Exists(file))
                    return JsonSerializer.Deserialize<List<RecentItem>>(File.ReadAllText(file)) ?? new List<RecentItem>();
            }
            catch { }
            return new List<RecentItem>();
        }

        // Persist the recent-project list in the user's editor configuration directory.
        // 在用户编辑器配置目录保存最近工程列表。
        private void SaveRecent(List<RecentItem> list)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(RecentPath));
                File.WriteAllText(RecentPath, JsonSerializer.Serialize(list, JsonOpts), new System.Text.UTF8Encoding(false));
            }
            catch { }
        }

        // Deduplicate the opened path and move it to the front of the bounded recent list.
        // 去除已打开路径的重复项，并将其移至有限长度的最近列表首位。
        private void AddRecent(string path)
        {
            var list = LoadRecent();
            list.RemoveAll(/* Match recent paths without case sensitivity. 不区分大小写匹配最近路径。 */ x => string.Equals(x.path, path, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, new RecentItem { path = path, name = Path.GetFileName(path), type = "界面方案" });
            if (list.Count > 12) list = list.GetRange(0, 12);
            SaveRecent(list);
        }

        // Show existing recent projects or an empty-state hint on the home page.
        // 在首页显示仍存在的最近工程，或显示空列表提示。
        private void RenderRecent()
        {
            RecentList.Children.Clear();
            var list = LoadRecent().Where(/* Keep recent entries whose files still exist. 保留文件仍存在的最近记录。 */ x => !string.IsNullOrEmpty(x.path) && File.Exists(x.path)).ToList();
            if (list.Count == 0)
            {
                RecentList.Children.Add(new TextBlock { Text = "暂无最近文件 —— 新建或打开后会出现在这里", Foreground = BrushOf("#8a90a0"), FontSize = 13 });
                return;
            }
            foreach (var it in list) RecentList.Children.Add(CreateRecentRow(it));
        }

        // Build a clickable recent-project row with its label and stored path.
        // 构建含名称与保存路径的可点击最近工程行。
        private Border CreateRecentRow(RecentItem it)
        {
            var b = new Border
            {
                Background = BrushOf("#ffffff"),
                BorderBrush = BrushOf("#eceef2"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = Cursors.Hand
            };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var ic = new TextBlock { Text = "🖼", FontSize = 20, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            Grid.SetColumn(ic, 0);
            var mid = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            mid.Children.Add(new TextBlock { Text = it.name, FontSize = 14, FontWeight = FontWeights.Medium, Foreground = BrushOf("#23262e") });
            mid.Children.Add(new TextBlock { Text = it.path, FontSize = 11.5, Foreground = BrushOf("#8a90a0"), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 1, 0, 0) });
            Grid.SetColumn(mid, 1);
            var chip = new Border { Background = BrushOf("#eef1f6"), CornerRadius = new CornerRadius(7), Padding = new Thickness(9, 3, 9, 3), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            chip.Child = new TextBlock { Text = it.type ?? "界面方案", FontSize = 11.5, Foreground = BrushOf("#5b6070") };
            Grid.SetColumn(chip, 2);

            g.Children.Add(ic); g.Children.Add(mid); g.Children.Add(chip);
            b.Child = g;
            string p = it.path;
            b.MouseLeftButtonUp += /* Open the recent project through the unsaved-change guard. 经未保存修改检查打开最近工程。 */ (s, e) => TryOpenPath(p);
            return b;
        }

        // Convert a valid hex color to a WPF brush and use transparency for invalid colors.
        // 将有效十六进制颜色转换为 WPF 画刷，无效颜色使用透明色。
        internal static SolidColorBrush BrushOf(string hex)
        {
            var c = ColorUtil.ParseHex(hex);
            return c != null ? new SolidColorBrush(Color.FromRgb(c.Value.R, c.Value.G, c.Value.B)) : Brushes.Transparent;
        }

        // ================= 事件挂接 =================
        // Connect file, drawing, layer, preview, and preference controls to editor commands.
        // 将文件、绘图、图层、预览与偏好控件连接到编辑器命令。
        private void WireEvents()
        {
            BtnHome.Click += /* Return to the recent-project home page. 返回最近工程首页。 */ (s, e) => ShowHome();
            HomeNewBtn.Click += /* Confirm pending edits before creating and showing a new project. 创建并显示新工程前确认待保存修改。 */ (s, e) => { if (ConfirmDiscard()) { NewDoc(); ShowEditor(); } };
            HomeOpenBtn.Click += /* Show the supported-project file picker. 显示受支持工程的文件选择器。 */ (s, e) => OpenFile();
            BtnNew.Click += /* Confirm pending edits before resetting the project. 重置工程前确认待保存修改。 */ (s, e) => { if (ConfirmDiscard()) NewDoc(); };
            BtnOpen.Click += /* Show the supported-project file picker. 显示受支持工程的文件选择器。 */ (s, e) => OpenFile();
            BtnSave.Click += /* Save to the current project path when available. 存在当前工程路径时保存到该路径。 */ (s, e) => { Save(false); };
            BtnSaveAs.Click += /* Choose a new path with Save As. 通过另存为选择新路径。 */ (s, e) => { Save(true); };
            BtnConnect.Click += /* Choose and connect the main application. 选择并连接主程序。 */ (s, e) => ConnectToMain();
            BtnSync.Click += /* Write the current skin to the connected library. 将当前皮肤写入连接的皮肤库。 */ (s, e) => SyncToMain();
            BtnLaunch.Click += /* Launch the connected main application. 启动已连接主程序。 */ (s, e) => LaunchMain();

            TxtName.TextChanged += /* Mark user edits dirty after initialization finishes. 初始化结束后将用户编辑标记为修改。 */ (s, e) => { if (_ready) MarkDirty(); };

            BtnUndo.Click += /* Restore the preceding undo snapshot. 恢复上一撤销快照。 */ (s, e) => Undo();
            BtnRedo.Click += /* Restore the pending redo snapshot. 恢复待重做快照。 */ (s, e) => Redo();
            BtnPrefs.Click += /* Open editor preference controls. 打开编辑器偏好控件。 */ (s, e) => OpenPrefs();

            BtnAddRect.Click += /* Activate rectangle drawing. 启用矩形绘制。 */ (s, e) => SetTool("rect");
            BtnAddRound.Click += /* Activate rounded-rectangle drawing. 启用圆角矩形绘制。 */ (s, e) => SetTool("roundrect");
            BtnAddEllipse.Click += /* Activate ellipse drawing. 启用椭圆绘制。 */ (s, e) => SetTool("ellipse");
            BtnAddLine.Click += /* Activate line drawing. 启用直线绘制。 */ (s, e) => SetTool("line");
            BtnAddText.Click += /* Select or add the target text region. 选择或新增目标文本区。 */ (s, e) => AddTextRegionTool();
            BtnAddImage.Click += /* Import an image into the design. 将图片导入设计。 */ (s, e) => ImportImage();

            DrawLayer.MouseLeftButtonDown += DrawDown;
            DrawLayer.MouseMove += DrawMove;
            DrawLayer.MouseLeftButtonUp += DrawUp;

            BtnLayerUp.Click += /* Raise the selected layers one level. 将选中图层上移一级。 */ (s, e) => MoveSelectedLayer(1);
            BtnLayerDown.Click += /* Lower the selected layers one level. 将选中图层下移一级。 */ (s, e) => MoveSelectedLayer(-1);
            BtnLayerTop.Click += /* Move selected layers to the top. 将选中图层移至顶部。 */ (s, e) => MoveSelectedLayerTo(true);
            BtnLayerBottom.Click += /* Move selected layers to the bottom. 将选中图层移至底部。 */ (s, e) => MoveSelectedLayerTo(false);
            BtnLayerLock.Click += /* Toggle locks on the selected layers. 切换选中图层锁定状态。 */ (s, e) => ToggleSelectedLock();
            BtnLayerDel.Click += /* Delete eligible selected elements. 删除允许删除的选中元素。 */ (s, e) => DeleteSelected();

            RbViewCanvas.Checked += /* Hide the native WebView and reveal the editable canvas. 隐藏原生 WebView 并显示可编辑画布。 */ (s, e) =>
            {
                if (!_ready) return;
                WebHost.Visibility = Visibility.Collapsed;   // WebView2 是原生窗口，必须收起才能露出画布
                CanvasView.Visibility = Visibility.Visible;
            };
            RbViewPreview.Checked += /* Stop canvas tools, initialize WebView2, and show the generated preview. 停止画布工具、初始化 WebView2，并显示生成预览。 */ async (s, e) =>
            {
                if (!_ready) return;
                if (BtnPen.IsChecked == true) BtnPen.IsChecked = false;
                ClearTool();
                CanvasView.Visibility = Visibility.Collapsed;
                WebHost.Visibility = Visibility.Visible;
                await EnsureWebAsync();
                RefreshPreview();
            };
            BtnPreviewRetry.Click += /* Retry preview initialization after clearing the error panel. 清除错误面板后重试预览初始化。 */ async (s, e) => { HidePreviewError(); await EnsureWebAsync(); RefreshPreview(); };
            BtnPreviewBuiltin.Click += /* Switch back to the built-in preview and persist that choice. 切换回内置预览并保存该选择。 */ (s, e) =>
            {
                Prefs.PreviewSource = 0; Prefs.Save(); _realLoaded = false; HidePreviewError(); RefreshPreview();
            };
            BtnPreviewCopy.Click += /* Copy the prepared privacy-safe preview diagnostic. 复制已准备且不含私有内容的预览诊断。 */ (s, e) =>
            {
                if (!string.IsNullOrEmpty(_previewDiagnostic)) Clipboard.SetText(_previewDiagnostic);
            };

            Stage.MouseLeftButtonDown += MarqueeDown;
            Stage.MouseMove += MarqueeMove;
            Stage.MouseLeftButtonUp += MarqueeUp;
            PreviewKeyDown += OnKey;
            DragOver += /* Accept file-drop input as a copy operation. 将文件拖入输入接受为复制操作。 */ (s, e) =>
            {
                e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
                e.Handled = true;
            };
            Drop += /* Open the first supported file from the dropped paths. 从拖入路径中打开首个受支持文件。 */ (s, e) =>
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                string file = files == null ? null : files.FirstOrDefault(ProjectFileExtensions.IsSupported);
                if (!string.IsNullOrEmpty(file)) TryOpenPath(file);
                e.Handled = true;
            };
        }

        // Apply the save/discard decision before replacing the current editing session.
        // 在替换当前编辑会话前执行保存或放弃的决策检查。
        private bool ConfirmDiscard()
        {
            return DirtyGuard.Confirm(this, _dirty, /* Attempt a normal save for the unsaved-change guard. 为未保存修改检查尝试普通保存。 */ () => Save(false));
        }

        // Route editor shortcuts while allowing text fields to keep their normal keyboard editing.
        // 分派编辑器快捷键，同时保留文本框的正常键盘编辑。
        private void OnKey(object sender, KeyEventArgs e)
        {
            if (EditorRoot.Visibility != Visibility.Visible) return;
            if (Keyboard.FocusedElement is TextBox) return;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                if (e.Key == Key.Z) { Undo(); e.Handled = true; return; }
                if (e.Key == Key.Y) { Redo(); e.Handled = true; return; }
            }
            if (e.Key == Key.Escape) { ClearTool(); ClearSelection(); e.Handled = true; return; }
            if (_sel == null) return;

            double step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 10 : 1;
            switch (e.Key)
            {
                case Key.Delete: DeleteSelected(); e.Handled = true; break;
                case Key.Left: NudgeSelected(-step, 0); e.Handled = true; break;
                case Key.Right: NudgeSelected(step, 0); e.Handled = true; break;
                case Key.Up: NudgeSelected(0, -step); e.Handled = true; break;
                case Key.Down: NudgeSelected(0, step); e.Handled = true; break;
            }
        }

        // ================= 新建 =================
        // Reset designs, selection, undo state, and file identity for a fresh project.
        // 为新工程重置设计、选择、撤销状态与文件身份。
        private void NewDoc()
        {
            _designs.Clear();
            _activeTarget = null;
            ClearElements();
            ResetUndo();
            _currentPath = null;
            _skinId = KnotJotConnection.NewSkinId();
            _lastSyncedHash = null;
            _warnedUnsavedSync = false;
            BtnSync.Content = "同步到 KnotJot";
            _exportSettings = new ProjectExportSettings { ClipToFrame = Prefs.ClipToFrame, AllowCustomText = Prefs.AllowCustomText };
            TxtName.Text = "自定义界面";
            ClearSelection();
            SwitchTarget("card", firstLoad: true);   // 默认从"文本框/节点"开始设计
            _dirty = false;
            UpdateTitle();
            SetStatus("选左侧一个部件 → 在设计框里画它的新样子、放文本区 → 导出后这个部件就用你画的样子且保留功能。");
            RbViewCanvas.IsChecked = true;
            if (BtnPen.IsChecked == true) BtnPen.IsChecked = false;
        }

        // ================= 部件面板 =================
        // Create property editors for the selected component and wire validated color updates.
        // 为选中部件创建属性编辑器，并连接经验证的颜色更新。
        internal void BuildCompPanel(ComponentElement ce)
        {
            _loadingPanel = true;
            CompTitle.Text = "部件：" + ce.Name;
            PanelCompProps.Children.Clear();

            foreach (var prop in ce.Props)
            {
                var p = prop;
                PanelCompProps.Children.Add(new TextBlock { Text = p.Label, Margin = new Thickness(0, 10, 0, 3), Foreground = BrushOf("#5b6070") });

                var row = new StackPanel { Orientation = Orientation.Horizontal };
                var sw = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(5), BorderBrush = BrushOf("#cccccc"), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 6, 0) };
                SetSwatch(sw, p.Value);
                var tb = new TextBox { Width = 96, Text = p.Value };
                var pick = new Button { Content = "取色", Height = 28, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(6, 0, 0, 0), Cursor = Cursors.Hand };

                tb.TextChanged += /* Validate the property color, record undo, and rebuild its component view. 验证属性颜色、记录撤销并重建部件视图。 */ (s, e) =>
                {
                    if (_loadingPanel) return;
                    string n = ColorUtil.NormalizeHex(tb.Text);
                    if (n == null) { SetStatus("颜色没认出来：填 6 位十六进制，如 0a8a6f（# 可省略，3 位简写也行）"); return; }
                    PushUndo("comp:" + ce.CompId + ":" + p.Key);
                    p.Value = n;
                    SetSwatch(sw, n);
                    RebuildComponentView(ce);
                    MarkDirty();
                };
                pick.Click += /* Choose a color for this component property. 为该部件属性选择颜色。 */ (s, e) => PickColor(tb);

                row.Children.Add(sw); row.Children.Add(tb); row.Children.Add(pick);
                PanelCompProps.Children.Add(row);

                // 选中光环支持一键从主色推导（与 KnotJot 的 ×1.7 完全一致）
                if (ce.CompId == "cardSel" && p.Key == "ring")
                {
                    var auto = new Button { Content = "从选中边框自动推导", Height = 26, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, Cursor = Cursors.Hand };
                    auto.Click += /* Derive a lighter property color from the component accent. 根据部件强调色派生较浅属性颜色。 */ (s, e) => { tb.Text = ColorUtil.HexDark(ce.Get("accent"), 1.7); };
                    PanelCompProps.Children.Add(auto);
                }
            }

            var reset = new Button { Content = "恢复此部件原版色", Height = 26, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 14, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, Cursor = Cursors.Hand };
            reset.Click += /* Reset component properties to catalog defaults with undo support. 在支持撤销的情况下将部件属性重置为目录默认值。 */ (s, e) =>
            {
                PushUndo();
                foreach (var p in ce.Props) p.Value = p.Default;
                RebuildComponentView(ce);
                BuildCompPanel(ce);
                MarkDirty();
            };
            PanelCompProps.Children.Add(reset);
            _loadingPanel = false;
        }

        // ================= 取色 / 色块 =================
        // Open the native color dialog and write the chosen RGB color back to the text field.
        // 打开系统颜色对话框，并将选择的 RGB 颜色写回文本框。
        internal void PickColor(TextBox target)
        {
            using (var dlg = new System.Windows.Forms.ColorDialog { FullOpen = true, AnyColor = true })
            {
                var cur = ColorUtil.ParseHex(target.Text);
                if (cur != null)
                    dlg.Color = System.Drawing.Color.FromArgb(cur.Value.R, cur.Value.G, cur.Value.B);
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    target.Text = ColorUtil.FromRgb(dlg.Color.R, dlg.Color.G, dlg.Color.B);
            }
        }

        // Preview a hex input as a swatch, falling back when parsing fails.
        // 将十六进制输入显示为色块，解析失败时使用回退颜色。
        internal static void SetSwatch(Border b, string hex)
        {
            var p = ColorUtil.ParseHex(hex);
            b.Background = p != null
                ? new SolidColorBrush(Color.FromRgb(p.Value.R, p.Value.G, p.Value.B))
                : Brushes.Transparent;
        }

        // ================= 状态 / 标题 / 预览 =================
        // Replace the editor's status message with the latest operation result.
        // 将编辑器状态文字替换为最近操作结果。
        internal void SetStatus(string t) { TxtStatus.Text = t; }

        // Mark the project modified, update its title, and refresh the generated preview.
        // 标记工程已修改，更新标题并刷新生成的预览。
        internal void MarkDirty()
        {
            _dirty = true;
            if (!string.IsNullOrEmpty(_lastSyncedHash))
            {
                _lastSyncedHash = null;
                BtnSync.Content = "同步到 KnotJot";
            }
            UpdateTitle();
            if (WebHost.Visibility == Visibility.Visible) RefreshPreview();
        }

        // Display the current filename and unsaved marker in the window's document title.
        // 在窗口文档标题中显示当前文件名与未保存标记。
        private void UpdateTitle()
        {
            string f = string.IsNullOrEmpty(_currentPath) ? "未命名" : Path.GetFileName(_currentPath);
            TxtCurrent.Text = f + (_dirty ? " ●" : "");
            Title = "KnotJot 界面编辑器 V1.0.0 — " + f + (_dirty ? " ●" : "");
        }

        // Collect designs, generate CSS, and update the active built-in or real-application preview.
        // 收集设计并生成 CSS，更新当前内置或真实主程序预览。
        private void RefreshPreview()
        {
            if (!_webReady) return;
            var designs = CollectDesigns();
            string css = CssBuilder.BuildSkins(designs, TxtName.Text, _exportSettings);
            _lastPreviewCss = css;

            if (Prefs.PreviewSource == 1)
            {
                if (string.IsNullOrWhiteSpace(Prefs.KnotJotPath) || !File.Exists(Prefs.KnotJotPath))
                {
                    ShowPreviewError(new FileNotFoundException("指定的 KnotJot index.html 不存在。", Prefs.KnotJotPath), "KnotJot 真实页面", Prefs.KnotJotPath, css);
                    return;
                }
                LoadRealPreview(css);   // 真实软件预览
                return;
            }

            _pendingInject = null;
            string html = "<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\"><style>"
                + Preview.BaseCss + "</style><style>" + Preview.TestCss + "</style><style>" + css
                + "</style><style>" + PreviewService.BuildDimCss(designs, Prefs.PreviewOriginalMode, Prefs.PreviewDim) + "</style></head><body>" + Preview.TestMarkup + "</body></html>";
            try { HidePreviewError(); Web.NavigateToString(html); }
            catch (Exception ex) { ShowPreviewError(ex, "内置预览", null, css); }
        }

        // ================= 偏好设置 =================
        // Build the modal preference controls for canvas constraints, text behavior, dimming, and application preview.
        // 构建画布约束、文本行为、淡化与主程序预览的模态偏好控件。
        private void OpenPrefs()
        {
            var win = new Window
            {
                Title = "偏好设置",
                Width = 400,
                SizeToContent = SizeToContent.Height,
                MaxHeight = 640,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = BrushOf("#f5f6f8"),
                FontFamily = this.FontFamily,
                FontSize = 13
            };
            var sp = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };

            Func<string, TextBlock> Head = /* Create a bold preference-section heading. 创建加粗偏好分区标题。 */ t => new TextBlock
            { Text = t, FontWeight = FontWeights.Bold, FontSize = 14, Foreground = BrushOf("#23262e"), Margin = new Thickness(0, 14, 0, 6) };
            Func<string, TextBlock> Note = /* Create a wrapped preference explanation. 创建可折行偏好说明。 */ t => new TextBlock
            { Text = t, TextWrapping = TextWrapping.Wrap, Foreground = BrushOf("#8a90a0"), FontSize = 11.5, Margin = new Thickness(0, 2, 0, 0) };

            // ===== 高级模式 =====
            sp.Children.Add(Head("高级模式"));

            var chkAdv = new CheckBox
            {
                Content = "开启高级模式（自由编辑，不限定设计框；可自己画文本区/固定文字）",
                IsChecked = Prefs.Advanced
            };
            sp.Children.Add(chkAdv);

            var advBox = new StackPanel { Margin = new Thickness(0, 8, 0, 0), Visibility = Prefs.Advanced ? Visibility.Visible : Visibility.Collapsed };

            advBox.Children.Add(new TextBlock { Text = "编辑区限制", Foreground = BrushOf("#5b6070"), Margin = new Thickness(0, 4, 0, 3) });
            var cmbFrame = new ComboBox { Width = 220, HorizontalAlignment = HorizontalAlignment.Left, Height = 28 };
            cmbFrame.ItemsSource = new[] { "限定到设计框（只显示框内）", "自由：框外也照样导出" };
            cmbFrame.SelectedIndex = _exportSettings.ClipToFrame ? 0 : 1;
            cmbFrame.SelectionChanged += /* Persist project frame clipping and refresh affected design views. 保存工程设计框裁剪设置，并刷新相关设计视图。 */ (s, e) =>
            {
                bool value = cmbFrame.SelectedIndex == 0;
                if (_exportSettings.ClipToFrame == value) return;
                _exportSettings.ClipToFrame = value;
                Prefs.ConstrainFrame = value; Prefs.Save(); DrawFrame(); MarkDirty();
                if (WebHost.Visibility == Visibility.Visible) RefreshPreview();
            };
            advBox.Children.Add(cmbFrame);

            advBox.Children.Add(new TextBlock { Text = "文本区", Foreground = BrushOf("#5b6070"), Margin = new Thickness(0, 12, 0, 3) });
            var cmbTxt = new ComboBox { Width = 220, HorizontalAlignment = HorizontalAlignment.Left, Height = 28 };
            cmbTxt.ItemsSource = new[] { "用部件本来的文字（有功能）", "允许自己画/打固定文字" };
            cmbTxt.SelectedIndex = _exportSettings.AllowCustomText ? 1 : 0;
            cmbTxt.SelectionChanged += /* Persist project custom-text permission and refresh text controls. 保存工程自定义文字权限，并刷新文字控件。 */ (s, e) =>
            {
                bool value = cmbTxt.SelectedIndex == 1;
                if (_exportSettings.AllowCustomText == value) return;
                _exportSettings.AllowCustomText = value;
                Prefs.CustomTextRegion = value; Prefs.Save(); MarkDirty();
                if (_sel is TextRegionElement) SyncTextPanel();
                if (WebHost.Visibility == Visibility.Visible) RefreshPreview();
            };
            advBox.Children.Add(cmbTxt);
            sp.Children.Add(advBox);

            chkAdv.Click += /* Toggle advanced preferences and their visible controls. 切换高级偏好及其可见控件。 */ (s, e) =>
            {
                Prefs.Advanced = chkAdv.IsChecked == true;
                Prefs.Save();
                advBox.Visibility = Prefs.Advanced ? Visibility.Visible : Visibility.Collapsed;
                DrawFrame();
                if (_sel is TextRegionElement) SyncTextPanel();
                if (WebHost.Visibility == Visibility.Visible) RefreshPreview();
            };
            sp.Children.Add(Note("这两项属于当前工程并会写入 .knotjot-ui；旧版 .bmapui 工程仍可打开。这里的选择同时作为以后新工程的默认值。"));

            // ===== 真实预览：没改过的原版 =====
            sp.Children.Add(Head("真实预览 · 没改过的原版部分"));

            var cmb = new ComboBox { Width = 160, HorizontalAlignment = HorizontalAlignment.Left, Height = 28 };
            cmb.ItemsSource = new[] { "完整显示", "淡化", "隐藏" };
            cmb.SelectedIndex = Prefs.PreviewOriginalMode;
            sp.Children.Add(cmb);

            var lblD = new TextBlock { Foreground = BrushOf("#5b6070"), Margin = new Thickness(0, 12, 0, 3) };
            var sldD = new Slider { Minimum = 5, Maximum = 80, Value = Math.Round(Prefs.PreviewDim * 100), TickFrequency = 1, IsSnapToTickEnabled = true };
            var noteD = Note("只淡化/隐藏你没动过的部分；改过颜色的部件保持完整，这样一眼就能看出你做了什么。");
            Action setLblD = /* Display the current dimming percentage. 显示当前淡化百分比。 */ () => lblD.Text = "淡化程度：" + Math.Round(sldD.Value) + "%";
            Action syncDimEnabled = /* Show dimming controls only in the dim-originals mode. 仅在淡化原版模式显示淡化控件。 */ () =>
            {
                lblD.Visibility = sldD.Visibility = cmb.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            };
            setLblD();
            syncDimEnabled();
            sldD.ValueChanged += /* Persist the dimming slider value and regenerate preview CSS. 保存淡化滑块值并重新生成预览 CSS。 */ (s, e) => { Prefs.PreviewDim = Math.Round(sldD.Value) / 100.0; setLblD(); Prefs.Save(); RefreshPreview(); };
            cmb.SelectionChanged += /* Persist the original-component display mode and refresh dimming controls. 保存原版部件显示模式并刷新淡化控件。 */ (s, e) =>
            {
                Prefs.PreviewOriginalMode = cmb.SelectedIndex;
                Prefs.Save(); syncDimEnabled(); RefreshPreview();
            };
            sp.Children.Add(lblD);
            sp.Children.Add(sldD);
            sp.Children.Add(noteD);

            // ===== 真实预览来源 =====
            sp.Children.Add(Head("真实预览用哪个"));
            var cmbSrc = new ComboBox { Width = 260, HorizontalAlignment = HorizontalAlignment.Left, Height = 28 };
            cmbSrc.ItemsSource = new[] { "内置完整画廊", "真实软件（加载 KnotJot 本体）" };
            cmbSrc.SelectedIndex = Prefs.PreviewSource;
            sp.Children.Add(cmbSrc);

            var lblPath = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = BrushOf("#8a90a0"), FontSize = 11.5, Margin = new Thickness(0, 6, 0, 0) };
            Action setPath = /* Display the configured main-application preview path. 显示已配置的主程序预览路径。 */ () => lblPath.Text = string.IsNullOrEmpty(Prefs.KnotJotPath) ? "未指定 index.html" : "已指定：" + Prefs.KnotJotPath;
            setPath();
            var btnPick = new Button { Content = "指定 KnotJot 的 index.html…", Height = 28, Padding = new Thickness(10, 0, 10, 0), Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, Cursor = Cursors.Hand };
            var srcBox = new StackPanel { Visibility = Prefs.PreviewSource == 1 ? Visibility.Visible : Visibility.Collapsed };
            btnPick.Click += /* Choose the main application's HTML entry for real preview. 选择主程序 HTML 入口供真实预览使用。 */ (s, e) =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "KnotJot 主程序 (index.html)|index.html;*.html|所有文件 (*.*)|*.*" };
                if (dlg.ShowDialog(win) == true)
                {
                    Prefs.KnotJotPath = dlg.FileName; Prefs.Save(); setPath();
                    _realLoaded = false;
                    if (WebHost.Visibility == Visibility.Visible) RefreshPreview();
                }
            };
            srcBox.Children.Add(btnPick);
            srcBox.Children.Add(lblPath);
            sp.Children.Add(srcBox);
            cmbSrc.SelectionChanged += /* Persist the preview source and reveal its relevant path controls. 保存预览来源并显示相关路径控件。 */ (s, e) =>
            {
                Prefs.PreviewSource = cmbSrc.SelectedIndex; Prefs.Save();
                srcBox.Visibility = Prefs.PreviewSource == 1 ? Visibility.Visible : Visibility.Collapsed;
                _realLoaded = false;
                if (WebHost.Visibility == Visibility.Visible) RefreshPreview();
            };
            sp.Children.Add(Note("真实软件预览会加载 KnotJot 页面并注入当前 CSS；Electron 文件对话框在预览中不可用。"));

            var close = new Button { Content = "关闭", Height = 30, Width = 88, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0), Cursor = Cursors.Hand };
            close.Click += /* Close the preference dialog. 关闭偏好对话框。 */ (s, e) => win.Close();
            sp.Children.Add(close);

            win.Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = sp };
            win.ShowDialog();
        }

        // ================= 保存 / 打开 =================
        private class ProjectDto
        {
            public int schemaVersion { get; set; } = 4;
            public List<ElementDto> elements { get; set; }
        }

        private class ElementDto
        {
            public string type { get; set; }
            public string id { get; set; }
            public string kind { get; set; }   // V0.0.1 旧档的图形类型字段
            public string name { get; set; }
            public bool visible { get; set; } = true;
            public bool locked { get; set; }
            public double rotation { get; set; }
            public string blendMode { get; set; }
            public string comp { get; set; }
            public Dictionary<string, string> props { get; set; }
            public double x { get; set; }
            public double y { get; set; }
            public double w { get; set; }
            public double h { get; set; }
            public double x2 { get; set; }
            public double y2 { get; set; }
            public string fill { get; set; }
            public bool noFill { get; set; }
            public string stroke { get; set; }
            public double strokeW { get; set; }
            public double radius { get; set; }
            public double opacity { get; set; } = 1;
            public bool isMask { get; set; }
            public bool lockAspect { get; set; }
            public string maskMode { get; set; }
            public string maskTargetId { get; set; }
            public List<double[]> points { get; set; }
            public string color { get; set; }
            public double width { get; set; }
            public string base64 { get; set; }
            public string mime { get; set; }
            // 文本区
            public string text { get; set; }
            public bool customText { get; set; }
            public double fontSize { get; set; } = 14;
            public string alignH { get; set; }
            public string alignV { get; set; }
            public bool bold { get; set; }
        }

        // Copy a concrete element into the serializable project DTO, retaining its type-specific data.
        // 将具体元素复制为可序列化工程 DTO，并保留类型专属数据。
        private ElementDto ToDto(CanvasElement el)
        {
            var d = new ElementDto { id = el.Id, name = el.Name, visible = el.Visible, locked = el.IsLocked, rotation = el.Rotation, blendMode = el.BlendMode };
            var ce = el as ComponentElement;
            if (ce != null)
            {
                d.type = "component";
                d.comp = ce.CompId;
                d.props = ce.Props.ToDictionary(/* Select the property key for serialized lookup. 提取属性键用于序列化查找。 */ p => p.Key, /* Select the property's current serialized value. 提取属性当前序列化值。 */ p => p.Value);
                return d;
            }
            var s = el as ShapeElement;
            if (s != null)
            {
                d.type = s.Kind;
                d.x = s.X; d.y = s.Y; d.w = s.W; d.h = s.H; d.x2 = s.X2; d.y2 = s.Y2;
                d.fill = s.Fill; d.noFill = s.NoFill; d.stroke = s.Stroke; d.strokeW = s.StrokeW;
                d.radius = s.Radius; d.opacity = s.Opacity; d.lockAspect = s.LockAspect; d.isMask = s.IsMask; d.maskMode = s.MaskMode; d.maskTargetId = s.MaskTargetId;
                return d;
            }
            var k = el as InkElement;
            if (k != null)
            {
                d.type = "ink";
                d.points = k.Points.Select(/* Round ink coordinates for compact project storage. 对手绘坐标取整以压缩工程存储。 */ p => new[] { Math.Round(p.X, 1), Math.Round(p.Y, 1) }).ToList();
                d.color = k.Color; d.width = k.Width; d.opacity = k.Opacity;
                return d;
            }
            var im = el as ImageElement;
            if (im != null)
            {
                d.type = "image";
                d.x = im.X; d.y = im.Y; d.w = im.W; d.h = im.H;
                d.base64 = im.Base64; d.mime = im.Mime; d.opacity = im.Opacity;
                return d;
            }
            var tr = el as TextRegionElement;
            if (tr != null)
            {
                d.type = "textregion";
                d.x = tr.X; d.y = tr.Y; d.w = tr.W; d.h = tr.H;
                d.text = tr.Text; d.customText = tr.CustomText; d.fontSize = tr.FontSize;
                d.color = tr.Color; d.alignH = tr.AlignH; d.alignV = tr.AlignV; d.bold = tr.Bold;
                return d;
            }
            return null;
        }

        // Reconstruct the supported element type from stored data and apply compatibility defaults.
        // 根据保存的数据重建受支持的元素类型，并应用兼容默认值。
        private CanvasElement FromDto(ElementDto d)
        {
            if (d == null) return null;
            if (string.IsNullOrEmpty(d.type)) d.type = d.kind;   // 兼容 V0.0.1 旧档
            if (string.IsNullOrEmpty(d.type)) return null;
            if (d.type == "component")
            {
                var def = ComponentLib.Find(d.comp);
                if (def == null) return null;
                var ce = def.CreateInstance();
                ce.Name = string.IsNullOrEmpty(d.name) ? def.Name : d.name;
                ce.Visible = d.visible;
                if (d.props != null)
                    foreach (var kv in d.props)
                    {
                        var p = ce.Prop(kv.Key);
                        string n = ColorUtil.NormalizeHex(kv.Value);
                        if (p != null && n != null) p.Value = n;
                    }
                ce.Id = d.id; ce.IsLocked = d.locked; ce.Rotation = d.rotation; ce.BlendMode = d.blendMode;
                return ce;
            }
            if (d.type == "ink")
            {
                var k = new InkElement
                {
                    Name = string.IsNullOrEmpty(d.name) ? "手绘" : d.name,
                    Visible = d.visible,
                    Color = ColorUtil.NormalizeHex(d.color) ?? "#5b8def",
                    Width = d.width <= 0 ? 3 : d.width,
                    Opacity = Clamp01(d.opacity)
                };
                if (d.points != null)
                    foreach (var p in d.points)
                        if (p != null && p.Length >= 2) k.Points.Add(new Point(p[0], p[1]));
                k.Id = d.id; k.IsLocked = d.locked; k.Rotation = d.rotation; k.BlendMode = d.blendMode;
                return k.Points.Count >= 2 ? k : null;
            }
            if (d.type == "image")
            {
                if (string.IsNullOrEmpty(d.base64)) return null;
                var image = new ImageElement
                {
                    Name = string.IsNullOrEmpty(d.name) ? "图片" : d.name,
                    Visible = d.visible,
                    Base64 = d.base64,
                    Mime = string.IsNullOrEmpty(d.mime) ? "image/png" : d.mime,
                    X = d.x, Y = d.y, W = Math.Max(4, d.w), H = Math.Max(4, d.h),
                    Opacity = Clamp01(d.opacity)
                };
                image.Id = d.id; image.IsLocked = d.locked; image.Rotation = d.rotation; image.BlendMode = d.blendMode;
                return image;
            }
            if (d.type == "textregion")
            {
                var textRegion = new TextRegionElement
                {
                    Name = string.IsNullOrEmpty(d.name) ? "文本区" : d.name,
                    Visible = d.visible,
                    X = d.x, Y = d.y, W = Math.Max(8, d.w), H = Math.Max(8, d.h),
                    Text = d.text ?? "文字",
                    CustomText = d.customText,
                    FontSize = d.fontSize <= 0 ? 14 : d.fontSize,
                    Color = ColorUtil.NormalizeHex(d.color) ?? "#23262e",
                    AlignH = string.IsNullOrEmpty(d.alignH) ? "left" : d.alignH,
                    AlignV = string.IsNullOrEmpty(d.alignV) ? "middle" : d.alignV,
                    Bold = d.bold
                };
                textRegion.Id = d.id; textRegion.IsLocked = d.locked; textRegion.Rotation = d.rotation; textRegion.BlendMode = d.blendMode;
                return textRegion;
            }
            // 图形
            var s = new ShapeElement
            {
                Kind = d.type,
                Name = string.IsNullOrEmpty(d.name) ? "图形" : d.name,
                Visible = d.visible,
                X = d.x, Y = d.y, W = d.w, H = d.h, X2 = d.x2, Y2 = d.y2,
                Fill = ColorUtil.NormalizeHex(d.fill) ?? "#5b8def",
                NoFill = d.noFill,
                Stroke = ColorUtil.NormalizeHex(d.stroke) ?? "#3a6bd8",
                StrokeW = d.strokeW,
                Radius = d.radius,
                Opacity = Clamp01(d.opacity),
                LockAspect = d.lockAspect,
                IsMask = d.isMask,
                MaskMode = d.maskMode,
                MaskTargetId = d.maskTargetId
            };
            s.Id = d.id; s.IsLocked = d.locked; s.Rotation = d.rotation; s.BlendMode = d.blendMode;
            if (s.Kind != "rect" && s.Kind != "roundrect" && s.Kind != "ellipse" && s.Kind != "line") return null;
            return s;
        }

        // Apply the historical opacity fallback where nonpositive values mean fully opaque.
        // 应用历史透明度回退规则，将非正值视为完全不透明。
        private static double Clamp01(double v)
        {
            if (v <= 0) return 1.0;
            return Math.Min(1.0, v);
        }

        /// <summary>未设计的目标会保持原版；这里只拦截真正会导出空白或失效的配置。</summary>
        // Check designed targets for required drawable and text content before export.
        // 导出前检查已设计目标所需的绘制内容与文本内容。
        private bool ConfirmValidDesigns(Dictionary<string, List<CanvasElement>> map)
        {
            var problems = new List<string>();
            foreach (var kv in map)
            {
                var target = DesignTargetLib.Find(kv.Key);
                foreach (var tr in kv.Value.OfType<TextRegionElement>())
                    if (_exportSettings.AllowCustomText && tr.CustomText && string.IsNullOrWhiteSpace(tr.Text))
                        problems.Add((target?.Name ?? kv.Key) + "：固定文字为空");
                foreach (var mask in kv.Value.OfType<ShapeElement>().Where(/* Select mask elements for export validation. 选择蒙版元素以进行导出验证。 */ x => x.IsMask))
                    if (string.IsNullOrEmpty(mask.MaskTargetId) || !kv.Value.Any(/* Find the element referenced by the mask target ID. 查找蒙版目标 ID 引用的元素。 */ x => x.Id == mask.MaskTargetId))
                        problems.Add((target?.Name ?? kv.Key) + "：蒙版“" + mask.Name + "”没有有效目标");
            }
            if (problems.Count == 0) return true;
            string detail = string.Join("\n", problems.Take(8).Select(/* Prefix an export problem with a bullet marker. 为导出问题添加项目符号前缀。 */ x => "• " + x));
            if (problems.Count > 8) detail += "\n• 另有 " + (problems.Count - 8) + " 项";
            MessageBox.Show(this, "以下配置会导致内容空白或失效，请先修复：\n\n" + detail,
                "无法保存", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        // Build the standalone project payload, obtain a destination, and update saved-file state after writing.
        // 生成独立工程数据、取得目标路径，并在写入后更新保存状态。
        private bool Save(bool forceDialog)
        {
            if (!TryBuildSkinPayload(out var name, out _, out var json)) return false;

            string path = _currentPath;
            if (forceDialog || string.IsNullOrEmpty(path))
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = ProjectFileExtensions.OpenFilter,
                    FileName = SanitizeFileName(name) + ProjectFileExtensions.DefaultExtension,
                    DefaultExt = ProjectFileExtensions.DefaultExtension
                };
                if (dlg.ShowDialog(this) != true) return false;
                path = dlg.FileName;
            }

            try
            {
                AtomicFile.WriteUtf8(path, json);
                _currentPath = path;
                _dirty = false;
                UpdateTitle();
                SetStatus("已保存：" + path + "。可继续编辑，或单独同步到 KnotJot。");
                AddRecent(path);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // Collect and validate designs, assign project identity, and serialize the editable project with CSS.
        // 收集并验证设计、确定工程身份，并将可编辑工程与 CSS 序列化。
        private bool TryBuildSkinPayload(out string name, out string css, out string json)
        {
            name = string.IsNullOrWhiteSpace(TxtName.Text) ? "自定义界面" : TxtName.Text.Trim();
            css = null; json = null;
            if (_activeTarget != null) _designs[_activeTarget] = SnapshotJson();
            var map = CollectDesigns();
            if (!ConfirmValidDesigns(map)) return false;
            css = CssBuilder.BuildSkins(map, name, _exportSettings);
            var designsMap = new Dictionary<string, ProjectDto>();
            foreach (var kv in _designs)
            {
                ProjectDto dto = null;
                try { dto = JsonSerializer.Deserialize<ProjectDto>(kv.Value); } catch { }
                if (dto != null && dto.elements != null && dto.elements.Count > 0) designsMap[kv.Key] = dto;
            }
            _skinId = KnotJotConnection.NormalizeSkinId(_skinId);
            var obj = new
            {
                app = UiSkinProtocol.Current.ProjectApp, id = _skinId, name, css, editor = "knotjot-ui-editor", editorVersion = "1.0.0",
                version = 5, schema = UiSkinProtocol.Current.ProjectSchema, schemaVersion = UiSkinProtocol.Current.ProjectSchemaVersion, activeTarget = _activeTarget,
                export = new { clipToFrame = _exportSettings.ClipToFrame, customTextAllowed = _exportSettings.AllowCustomText }, designs = designsMap
            };
            json = JsonSerializer.Serialize(obj, JsonOpts);
            return true;
        }

        // Reflect the current connection target and library path in the connection controls.
        // 在连接控件中显示当前连接目标与皮肤库路径。
        private void SetConnected(KnotJotConnectionInfo connection)
        {
            _connectedTarget = connection?.Target;
            _connectedLibraryPath = connection?.LibraryPath;
            bool connected = !string.IsNullOrWhiteSpace(_connectedTarget) && !string.IsNullOrWhiteSpace(_connectedLibraryPath);
            TxtConnection.Text = connected ? "● 已连接 KnotJot：" + _connectedLibraryPath : "未连接主程序（可独立使用）";
            TxtConnection.Foreground = BrushOf(connected ? "#238b57" : "#8a90a0");
            BtnLaunch.IsEnabled = connected;
        }

        // Choose a main-application target and show the validated handshake result or connection error.
        // 选择主程序目标，并显示经过验证的握手结果或连接错误。
        private bool ConnectToMain()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择 KnotJot 主程序、源码 main.js 或桌面快捷方式",
                Filter = "KnotJot|*.exe;*.lnk;main.js|可执行文件|*.exe|快捷方式|*.lnk|main.js|main.js"
            };
            if (dlg.ShowDialog(this) != true) return false;
            try
            {
                var connection = KnotJotConnection.Connect(dlg.FileName); SetConnected(connection);
                SetStatus("已连接 KnotJot；皮肤库将写入：" + connection.LibraryPath); return true;
            }
            catch (Exception ex)
            {
                TxtConnection.Text = "● 连接失败：" + ex.Message;
                TxtConnection.Foreground = BrushOf("#c53a4b");
                BtnLaunch.IsEnabled = false;
                SetStatus("连接失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "连接失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // Generate the current skin and update its stable library record while preserving standalone save semantics.
        // 生成当前皮肤并更新稳定库记录，同时保留独立工程的保存语义。
        private void SyncToMain()
        {
            if (string.IsNullOrWhiteSpace(_connectedTarget) && !ConnectToMain()) return;
            if ((_dirty || string.IsNullOrEmpty(_currentPath)) && !_warnedUnsavedSync)
            {
                var answer = MessageBox.Show(this,
                    "“同步到 KnotJot”只同步当前内存中的皮肤，不会保存 .knotjot-ui 工程。\n未保存工程不会写入 sourceFile。仍要同步？",
                    "工程尚未保存", MessageBoxButton.OKCancel, MessageBoxImage.Information, MessageBoxResult.Cancel);
                if (answer != MessageBoxResult.OK) return;
                _warnedUnsavedSync = true;
            }
            if (!TryBuildSkinPayload(out var name, out var css, out var json)) return;
            try
            {
                string sourceFile = !_dirty && !string.IsNullOrEmpty(_currentPath) && File.Exists(_currentPath) ? _currentPath : null;
                var result = KnotJotConnection.Write(_connectedTarget, _skinId, name, css, json, sourceFile);
                _skinId = result.SkinId; _lastSyncedHash = result.ContentHash;
                _connectedLibraryPath = result.File;
                BtnSync.Content = "✓ 已同步 · 再点可更新";
                SetConnected(new KnotJotConnectionInfo { Target = _connectedTarget, LibraryPath = result.File });
                SetStatus("已同步到运行中的 KnotJot（通常 1 秒内刷新）：" + result.File + " · revision " + result.Revision);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "同步失败（原皮肤库已保留）", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        // Ensure a connection exists before launching the selected main application.
        // 确认存在连接后启动所选主程序。
        private void LaunchMain()
        {
            if (string.IsNullOrWhiteSpace(_connectedTarget) && !ConnectToMain()) return;
            try { KnotJotConnection.Launch(_connectedTarget); SetStatus("已启动主程序。"); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "无法启动主程序", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        // Replace invalid filename characters and provide a name for blank input.
        // 替换文件名非法字符，并为空输入提供名称。
        private static string SanitizeFileName(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return string.IsNullOrWhiteSpace(s) ? "未命名" : s;
        }

        // Choose a supported project file and route it through the project-opening workflow.
        // 选择受支持的工程文件，并进入工程打开流程。
        private void OpenFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = ProjectFileExtensions.OpenFilter
            };
            if (dlg.ShowDialog(this) != true) return;
            TryOpenPath(dlg.FileName);
        }

        // Run the unsaved-change guard before opening another project path.
        // 打开另一个工程路径前检查未保存修改。
        private bool TryOpenPath(string path)
        {
            if (!ConfirmDiscard()) return false;
            return OpenPath(path);
        }

        // Load and migrate the selected project, then rebuild design state and recent-file metadata.
        // 加载并迁移所选工程，然后重建设计状态与最近文件元数据。
        private bool OpenPath(string path)
        {
            if (!File.Exists(path))
            {
                MessageBox.Show(this, "文件不存在：\n" + path, "打开失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                RenderRecent();
                return false;
            }
            try
            {
                using var doc = ProjectFileService.Open(path);
                var root = doc.RootElement;
                string app = root.TryGetProperty("app", out var a) ? a.GetString() : null;
                bool legacyApp = app == "brace-mindmap-ui";
                if (app != UiSkinProtocol.Current.ProjectApp && !legacyApp)
                    throw new InvalidDataException("这不是 KnotJot UI 工程（工程标识不受支持）。");

                var migration = LegacyProjectMigrator.Read(root, JsonOpts);
                var export = ReadProjectExportSettings(root, out bool exportMigrated);
                if (export == null) return false;
                migration.Migrated = migration.Migrated || exportMigrated || legacyApp;
                string name = root.TryGetProperty("name", out var n) ? (n.GetString() ?? "自定义界面") : "自定义界面";
                string skinId = KnotJotConnection.NormalizeSkinId(root.TryGetProperty("id", out var sid) ? sid.GetString() : null);
                string act = DesignTargetLib.Find(migration.ActiveTarget) != null ? migration.ActiveTarget : migration.Designs.Keys.FirstOrDefault() ?? "card";

                NewDoc();
                _designs.Clear();
                foreach (var kv in migration.Designs) _designs[kv.Key] = kv.Value;
                _exportSettings = export;
                _activeTarget = null;
                SwitchTarget(act, firstLoad: true);
                TxtName.Text = name;
                _skinId = skinId;
                _lastSyncedHash = null; BtnSync.Content = "同步到 KnotJot";

                _currentPath = migration.Migrated ? null : path;
                _dirty = migration.Migrated;
                UpdateTitle();
                AddRecent(path);
                ShowEditor();
                if (migration.Migrated)
                {
                    string warnings = migration.Warnings.Count == 0 ? "" : " " + string.Join("；", migration.Warnings);
                    SetStatus("已从 " + migration.SourceVersion + " 迁移到 KnotJot schema；请检查后另存为新 .knotjot-ui。" + warnings);
                }
                else SetStatus("已打开：" + path);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "打开失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // Restore project-level export choices or explicitly migrate legacy preference-dependent behavior.
        // 恢复工程级导出选项，或明确迁移旧版依赖全局偏好的行为。
        private ProjectExportSettings ReadProjectExportSettings(JsonElement root, out bool migrated)
        {
            migrated = false;
            if (root.TryGetProperty("export", out var export) && export.ValueKind == JsonValueKind.Object)
            {
                bool clip = !export.TryGetProperty("clipToFrame", out var c) || c.ValueKind != JsonValueKind.False;
                bool custom = export.TryGetProperty("customTextAllowed", out var t) && t.ValueKind == JsonValueKind.True;
                return new ProjectExportSettings { ClipToFrame = clip, AllowCustomText = custom };
            }
            var answer = MessageBox.Show(this,
                "这个旧工程没有保存导出设置。\n\n是：采用当前偏好作为迁移值\n否：采用安全默认值（限定设计框、禁用固定文字）\n取消：不打开",
                "迁移导出设置", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
            if (answer == MessageBoxResult.Cancel) return null;
            migrated = true;
            return answer == MessageBoxResult.Yes
                ? new ProjectExportSettings { ClipToFrame = Prefs.ClipToFrame, AllowCustomText = Prefs.AllowCustomText }
                : new ProjectExportSettings { ClipToFrame = true, AllowCustomText = false };
        }
    }
}
