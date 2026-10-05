using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using KnotJotTextStyleEditor;

internal static class Program
{
    // EN: Fail the smoke suite immediately with the precise behavioral assertion message.
    // ZH: 行为断言不满足时立即中止 smoke 测试，并显示具体信息。
    static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    // EN: Locate a sizing or input-rule option by its stable tag rather than translated display text.
    // ZH: 按稳定标签查找尺寸或输入规则选项，避免依赖翻译后的显示文字。
    static int IndexOfTag(ComboBox box, string tag) => box.Items.Cast<ComboBoxItem>().ToList().FindIndex( /* EN: Match a control option by stable tag for locale-independent assertions. ZH: 按稳定标签匹配控件选项，使断言不依赖语言。 */x => x.Tag?.ToString() == tag);
    // EN: Independently measure the actual preview typography and padding to detect clipped text.
    // ZH: 独立测量实际预览字体及内边距，以发现文字裁切。
    static double RequiredTextHeight(TextBox preview)
    {
        var horizontalInsets = preview.Padding.Left + preview.Padding.Right + 4;
        var verticalInsets = preview.Padding.Top + preview.Padding.Bottom + 4;
        var probe = new TextBlock
        {
            Text = preview.Text,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = preview.FontFamily,
            FontSize = preview.FontSize,
            FontWeight = preview.FontWeight
        };
        probe.Measure(new Size(Math.Max(1, preview.Width - horizontalInsets), double.PositiveInfinity));
        return probe.DesiredSize.Height + verticalInsets;
    }

    // EN: Verify all legacy/unknown sizing values normalize to uniform while stretch is preserved.
    // ZH: 验证所有旧版或未知尺寸值规范为 uniform，同时保留 stretch。
    static void CheckModeMigration()
    {
        foreach (var legacy in new[]
        {
            "auto",
            "uniform",
            "fixed",
            "unknown",
            ""
        }

        )
        {
            var migrated = new TextBoxStyle
            {
                TextSizing = new TextSizing
                {
                    Mode = legacy
                }
            };
            ModelSafety.Normalize(migrated);
            Expect(migrated.TextSizing.Mode == "uniform", $"legacy mode '{legacy}' did not migrate to uniform");
        }

        var stretch = new TextBoxStyle
        {
            TextSizing = new TextSizing
            {
                Mode = "stretch"
            }
        };
        ModelSafety.Normalize(stretch);
        Expect(stretch.TextSizing.Mode == "stretch", "stretch mode did not survive migration");
    }

    // EN: Check allowed types, anchored regex, maximum length, and required completion semantics.
    // ZH: 校验允许类型、完整正则匹配、长度上限及必填完成态语义。
    static void CheckRuleEngine()
    {
        Expect(InputRuleValidator.IsAllowed("012345", new InputRules { Type = "number" }), "number rejected digits");
        Expect(!InputRuleValidator.IsAllowed("-1", new InputRules { Type = "number" }), "number accepted sign");
        Expect(!InputRuleValidator.IsAllowed("1.2", new InputRules { Type = "number" }), "number accepted decimal point");
        Expect(InputRuleValidator.IsAllowed("AbZ", new InputRules { Type = "letter" }), "letter rejected ASCII letters");
        Expect(!InputRuleValidator.IsAllowed("A1", new InputRules { Type = "letter" }), "letter accepted digit");
        Expect(InputRuleValidator.IsAllowed("中文汉字", new InputRules { Type = "chinese" }), "chinese rejected Han characters");
        Expect(!InputRuleValidator.IsAllowed("中文 A", new InputRules { Type = "chinese" }), "chinese accepted whitespace/ASCII");
        Expect(InputRuleValidator.IsAllowed("Az09", new InputRules { Type = "alnum" }), "alnum rejected valid text");
        Expect(!InputRuleValidator.IsAllowed("Az-09", new InputRules { Type = "alnum" }), "alnum accepted punctuation");
        Expect(InputRuleValidator.IsAllowed("123", new InputRules { Type = "regex", Pattern = "[0-9]+" }), "regex rejected whole match");
        Expect(!InputRuleValidator.IsAllowed("123x", new InputRules { Type = "regex", Pattern = "[0-9]+" }), "regex accepted partial match");
        Expect(!InputRuleValidator.IsAllowed("1234", new InputRules { Type = "number", MaxLength = 3 }), "maximum length not enforced");
        Expect(!InputRuleValidator.IsCompleteValueValid("", new InputRules { Required = true }), "required accepted empty value");
        Expect(!InputRuleValidator.IsCompleteValueValid("   ", new InputRules { Required = true }), "required accepted whitespace-only value");
        Expect(!InputRuleValidator.IsCompleteValueValid("", new InputRules { Type = "regex", Pattern = "[0-9]+" }), "regex completion accepted a non-matching empty value");
    }

    // EN: Exercise live WPF rule controls, translation, dirty indicators, image preview, and measured growth in both sizing modes.
    // ZH: 验证 WPF 实时规则控件、翻译、修改指示、图片预览及两种尺寸模式的实测增长。
    [STAThread]
    static void Main()
    {
        CheckRuleEngine();
        CheckModeMigration();
        _ = new Application();
        var window = new MainWindow();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(MainWindow);
        var style = (TextBoxStyle)(type.GetField("style", flags)!.GetValue(window) ?? throw new Exception("style missing"));
        var refresh = type.GetMethod("RefreshPreview", flags) ?? throw new Exception("RefreshPreview missing");
        var paste = type.GetMethod("PreviewPaste", flags) ?? throw new Exception("PreviewPaste missing");
        var applyLanguage = type.GetMethod("ApplyLanguage", flags) ?? throw new Exception("ApplyLanguage missing");
        var readControls = type.GetMethod("ReadControls", flags) ?? throw new Exception("ReadControls missing");
        var snapshot = type.GetMethod("Snapshot", flags) ?? throw new Exception("Snapshot missing");
        var updateSyncState = type.GetMethod("UpdateSyncState", flags) ?? throw new Exception("UpdateSyncState missing");
        var updateSaveButton = type.GetMethod("UpdateSaveButton", flags) ?? throw new Exception("UpdateSaveButton missing");
        var measurePreviewText = type.GetMethod("MeasurePreviewText", flags) ?? throw new Exception("MeasurePreviewText missing");
        var lastSyncedSnapshot = type.GetField("lastSyncedSnapshot", flags) ?? throw new Exception("lastSyncedSnapshot missing");
        var lastSavedSnapshot = type.GetField("lastSavedSnapshot", flags) ?? throw new Exception("lastSavedSnapshot missing");
        var currentFile = type.GetField("currentFile", flags) ?? throw new Exception("currentFile missing");
        var inputType = (ComboBox)window.FindName("InputTypeBox");
        var maxLength = (TextBox)window.FindName("MaxLengthBox");
        var pattern = (TextBox)window.FindName("PatternBox");
        var required = (CheckBox)window.FindName("RequiredBox");
        var sizeMode = (ComboBox)window.FindName("SizeModeBox");
        var aspect = (TextBox)window.FindName("AspectBox");
        var fontSize = (TextBox)window.FindName("FontSizeBox");
        var syncButton = (Button)window.FindName("SyncBtn");
        var saveButton = (Button)window.FindName("SaveBtn");
        var preview = (TextBox)window.FindName("PreviewText");
        var card = (Border)window.FindName("PreviewCard");
        var layers = (Canvas)window.FindName("PreviewLayers");
        var status = (TextBlock)window.FindName("StatusText");
        // EN: Check both sizing tags and translated labels, without coupling persistence to the UI language.
        // ZH: 检查两种尺寸标签及翻译文字，不将持久化与界面语言绑定。
        Expect(card.MinHeight == 0, "preview card still forces a height different from the shared layer coordinate system");
        Expect(sizeMode.Items.Count == 2, $"sizing selector still has {sizeMode.Items.Count} options");
        Expect(IndexOfTag(sizeMode, "uniform") == 0 && IndexOfTag(sizeMode, "stretch") == 1, "sizing selector tags are incorrect");
        Expect((sizeMode.Items[0] as ComboBoxItem)?.Content?.ToString() == "固定长宽比", "Chinese uniform label is incorrect");
        Expect((sizeMode.Items[1] as ComboBoxItem)?.Content?.ToString() == "自由拉伸", "Chinese stretch label is incorrect");
        applyLanguage.Invoke(window, [true ]);
        Expect((sizeMode.Items[0] as ComboBoxItem)?.Content?.ToString() == "Fixed aspect ratio", "English uniform label is incorrect");
        Expect((sizeMode.Items[1] as ComboBoxItem)?.Content?.ToString() == "Free stretch", "English stretch label is incorrect");
        applyLanguage.Invoke(window, [false ]);
        // EN: Exercise exact snapshot equality: edits make save/sync dirty and exact reversal restores their badges.
        // ZH: 验证快照精确相等：编辑标记保存同步为已修改，准确还原则恢复状态。
        readControls.Invoke(window, null);
        lastSyncedSnapshot.SetValue(window, snapshot.Invoke(window, null));
        updateSyncState.Invoke(window, [null ]);
        Expect(syncButton.Content?.ToString()?.Contains("已同步") == true, "sync button did not enter synced state");
        currentFile.SetValue(window, Path.Combine(Path.GetTempPath(), "saved.knotjot-textstyle"));
        lastSavedSnapshot.SetValue(window, snapshot.Invoke(window, null));
        updateSaveButton.Invoke(window, null);
        sizeMode.SelectedIndex = IndexOfTag(sizeMode, "stretch");
        Expect(syncButton.Content?.ToString() == "保存并同步", "sizing change did not mark the synchronized style dirty");
        Expect(saveButton.Content?.ToString()?.Contains("保存") == true && saveButton.Content?.ToString()?.Contains("已保存") == false, "standalone save did not become dirty");
        sizeMode.SelectedIndex = IndexOfTag(sizeMode, "uniform");
        Expect(syncButton.Content?.ToString()?.Contains("已同步") == true, "exactly reverting a change did not restore synced state");
        Expect(saveButton.Content?.ToString()?.Contains("已保存") == true, "exactly reverting a change did not restore saved state");
        fontSize.Text = "15";
        Expect(syncButton.Content?.ToString() == "保存并同步", "font-size change did not mark the style dirty");
        fontSize.Text = "14";
        Expect(syncButton.Content?.ToString()?.Contains("已同步") == true, "reverting font size did not restore synced state");
        // EN: Ensure rule controls update immediately and reject invalid paste while allowing valid replacements.
        // ZH: 确保规则控件立即更新，拒绝无效粘贴并允许有效替换。
        inputType.SelectedIndex = IndexOfTag(inputType, "number");
        maxLength.Text = "3";
        pattern.Text = "[0-9]+";
        required.IsChecked = true;
        Expect(style.TextRules.Type == "number" && style.TextRules.MaxLength == 3 && style.TextRules.Pattern == "[0-9]+" && style.TextRules.Required, "rule controls did not update model immediately");
        Expect(preview.MaxLength == 0, "WPF UTF-16 MaxLength should be disabled in favor of shared Unicode code-point validation");
        preview.Text = "";
        refresh.Invoke(window, null);
        Expect(card.Width > 0 && preview.BorderThickness.Left > 0, "required did not mark empty preview immediately");
        preview.Text = "1";
        refresh.Invoke(window, null);
        Expect(preview.BorderThickness.Left == 0, "valid required preview stayed invalid");
        maxLength.Text = "0";
        preview.Text = "12";
        preview.SelectionStart = preview.Text.Length;
        status.Text = "unchanged";
        var invalidData = new DataObject();
        invalidData.SetData(DataFormats.UnicodeText, "3a");
        paste.Invoke(window, [preview, new DataObjectPastingEventArgs(invalidData, false, DataFormats.UnicodeText)]);
        Expect(status.Text != "unchanged", "invalid paste was not rejected");
        status.Text = "unchanged";
        var validData = new DataObject();
        validData.SetData(DataFormats.UnicodeText, "34");
        paste.Invoke(window, [preview, new DataObjectPastingEventArgs(validData, false, DataFormats.UnicodeText)]);
        Expect(status.Text == "unchanged", "valid paste was rejected");
        // EN: Verify embedded-image preview rendering and safe finite measurement after corrupt geometry.
        // ZH: 验证内嵌图片预览渲染及几何损坏后的有限测量。
        inputType.SelectedIndex = IndexOfTag(inputType, "any");
        required.IsChecked = false;
        style.TextRegion = new TextRegion
        {
            X = 10,
            Y = 10,
            W = 80,
            H = 80
        };
        style.Layers.Clear();
        style.Layers.Add(new StyleLayer { Type = "image", X = 0, Y = 0, W = 100, H = 100, ImageData = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=" });
        style.TextSizing.Width = 999;
        style.TextSizing.Height = 777;
        style.TextSizing.Aspect = 1.8;
        refresh.Invoke(window, null);
        Expect(layers.Children.OfType<Image>().Any(), "embedded image layer was not rendered in the independent preview");
        var measuredNaN = (PreviewDimensions)(measurePreviewText.Invoke(window, [double.NaN]) ?? throw new Exception("NaN measurement missing"));
        Expect(double.IsFinite(measuredNaN.Width) && double.IsFinite(measuredNaN.Height), "NaN width reached WPF measurement");
        style.TextRegion = new TextRegion
        {
            X = double.NaN,
            Y = double.NaN,
            W = double.NaN,
            H = double.NaN
        };
        refresh.Invoke(window, null);
        Expect(double.IsFinite(card.Width) && double.IsFinite(card.Height), "corrupted text region produced a non-finite preview");
        style.TextRegion = new TextRegion
        {
            X = 10,
            Y = 10,
            W = 80,
            H = 80
        };
        sizeMode.SelectedIndex = IndexOfTag(sizeMode, "uniform");
        // EN: Compare empty, one-, and two-character capacity before fixed-aspect growth starts.
        // ZH: 在等比增长开始前比较空内容、单字和双字容量。
        preview.Text = "";
        refresh.Invoke(window, null);
        var emptyUniformWidth = card.Width;
        var emptyUniformHeight = card.Height;
        preview.Text = "字";
        refresh.Invoke(window, null);
        Expect(Math.Abs(card.Width - emptyUniformWidth) < .05 && Math.Abs(card.Height - emptyUniformHeight) < .05, "empty text and one character do not share the minimum uniform size");
        preview.Text = "字字";
        refresh.Invoke(window, null);
        Expect(Math.Abs(card.Width - emptyUniformWidth) < .05 && Math.Abs(card.Height - emptyUniformHeight) < .05, "uniform grew before the drawn text region was full");
        Expect(preview.VerticalContentAlignment == VerticalAlignment.Center, "preview content is not vertically centered");
        Expect(Math.Abs(card.Width / card.Height - 1.8) < .001, "uniform preview changed aspect ratio");
        Expect(RequiredTextHeight(preview) <= preview.Height + 1, "short uniform preview clips text");
        sizeMode.SelectedIndex = IndexOfTag(sizeMode, "stretch");
        Expect(!aspect.IsEnabled, "aspect input stayed enabled in free-stretch mode");
        preview.Text = "";
        refresh.Invoke(window, null);
        var emptyStretchWidth = card.Width;
        var emptyStretchHeight = card.Height;
        preview.Text = "字";
        refresh.Invoke(window, null);
        Expect(Math.Abs(card.Width - emptyStretchWidth) < .05 && Math.Abs(card.Height - emptyStretchHeight) < .05, "empty text and one character do not share the minimum stretch size");
        preview.Text = "字字";
        refresh.Invoke(window, null);
        Expect(Math.Abs(card.Width - emptyStretchWidth) < .05 && Math.Abs(card.Height - emptyStretchHeight) < .05, "stretch grew before the drawn text region was full");
        Expect(Math.Abs(card.Width / card.Height - 1.8) < .001, "free-stretch minimum frame flipped the authored orientation");
        // EN: Increase text lengths to verify monotonic stretch growth, no clipping, and shared card/layer/region dimensions.
        // ZH: 逐步增加文字长度，验证自由拉伸单调增长、不裁切及卡片图层区域共用尺寸。
        double previousWidth = 0, previousHeight = 0;
        foreach (var length in new[]
        {
            20,
            100,
            500,
            2_000
        }

        )
        {
            preview.Text = new string ('中', length);
            refresh.Invoke(window, null);
            Expect(card.Width + .01 >= previousWidth && card.Height + .01 >= previousHeight, $"stretch size shrank at {length} characters");
            var requiredHeight = RequiredTextHeight(preview);
            Expect(requiredHeight <= preview.Height + 1, $"stretch clips at {length} characters: required={requiredHeight:0.##}, region={preview.Height:0.##}");
            previousWidth = card.Width;
            previousHeight = card.Height;
        }

        Expect(card.Height > 260, $"stretch did not grow for long text: {card.Width:0.##}x{card.Height:0.##}");
        Expect(card.Width > emptyStretchWidth && card.Height > emptyStretchHeight, $"stretch did not grow both axes for long text: {card.Width:0.##}x{card.Height:0.##}");
        Expect(card.Width <= ProductLimits.MaximumPreviewDimension && card.Height <= ProductLimits.MaximumPreviewDimension, "stretch exceeded product preview dimensions");
        Expect(Math.Abs(preview.Width - card.Width * .8) < .01 && Math.Abs(preview.Height - card.Height * .8) < .01, "text region did not scale with card");
        Expect(Math.Abs(layers.Width - card.Width) < .01 && Math.Abs(layers.Height - card.Height) < .01, "layers did not scale with card");
        var stretchWidth = card.Width;
        var stretchHeight = card.Height;
        sizeMode.SelectedIndex = IndexOfTag(sizeMode, "uniform");
        Expect(aspect.IsEnabled, "aspect input stayed disabled in fixed-aspect mode");
        // EN: Repeat long-text checks for fixed-aspect growth and ensure it differs from independent-axis stretching.
        // ZH: 对等比增长重复长文字检查，确保与独立轴拉伸有所区别。
        previousWidth = previousHeight = 0;
        foreach (var length in new[]
        {
            20,
            100,
            500,
            2_000,
            5_000
        }

        )
        {
            preview.Text = new string ('A', length);
            refresh.Invoke(window, null);
            Expect(card.Width + .01 >= previousWidth && card.Height + .01 >= previousHeight, $"uniform size shrank at {length} characters");
            Expect(Math.Abs(card.Width / card.Height - 1.8) < .001, $"uniform changed aspect ratio at {length} characters");
            var requiredHeight = RequiredTextHeight(preview);
            Expect(requiredHeight <= preview.Height + 1, $"uniform clips at {length} characters: required={requiredHeight:0.##}, region={preview.Height:0.##}");
            previousWidth = card.Width;
            previousHeight = card.Height;
        }

        Expect(card.Width > 360 && card.Height > 260, $"uniform remained capped at {card.Width:0.##}x{card.Height:0.##}");
        Expect(Math.Abs(stretchWidth / stretchHeight - card.Width / card.Height) > .1, "free stretch behaves like fixed-aspect sizing");
        Console.WriteLine($"PREVIEW_RULES_SIZING_SMOKE_OK stretch={stretchWidth:0.##}x{stretchHeight:0.##} uniform={card.Width:0.##}x{card.Height:0.##}");
        window.Close();
    }
}
