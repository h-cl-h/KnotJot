using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using KnotJotUiEditor;

static class Program
{
    // Fail the smoke run with the named invariant when an assertion is false.
    // 断言为假时，以不变量名称报告冒烟检查失败。
    static void Need(bool value, string name)
    {
        if (!value) throw new InvalidOperationException("FAILED: " + name);
    }

    // Require the supplied operation to throw the expected exception type.
    // 要求指定操作抛出预期类型的异常。
    static void NeedThrows<T>(Action action, string name) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException("FAILED: " + name);
    }

    // Run core checks on an STA thread and convert uncaught failures to a nonzero exit code.
    // 在 STA 线程运行核心检查，并将未捕获失败转换为非零退出码。
    [STAThread]
    static int Main()
    {
        try { return Run(); }
        catch (Exception ex)
        {
            Console.Error.WriteLine("UI_EDITOR_CORE_FAILED " + ex);
            return 1;
        }
    }

    // Exercise formats, migration, model limits, masks, text export, atomic writes, and stable-ID synchronization.
    // 验证格式、迁移、模型限制、蒙版、文本导出、原子写入与稳定 ID 同步。
    static int Run()
    {
        // Check current and legacy project extension routing.
        // 检查当前与旧工程扩展名路由。
        Need(ProjectFileExtensions.IsSupported("legacy.bmapui"), "legacy UI extension is supported");
        Need(ProjectFileExtensions.IsSupported("new.knotjot-ui"), "new UI extension is supported");
        Need(!ProjectFileExtensions.IsSupported("wrong.knotjot-theme"), "theme extension is rejected");
        Need(ProjectFileExtensions.DefaultExtension == ".knotjot-ui", "new UI extension is the save default");

        // Normalize malformed geometry, duplicate IDs, legacy masks, and image payloads.
        // 规范异常几何、重复 ID、旧蒙版与图片数据。
        var legacyTarget = new ShapeElement { Id = "target_legacy", Name = "旧目标", Kind = "rect", X = 10, Y = 10, W = 100, H = 80 };
        var legacyMask = new ShapeElement { Id = "mask_legacy", Name = "旧蒙版", Kind = "ellipse", X = 20, Y = 20, W = 50, H = 40, IsMask = true };
        var unsafeShape = new ShapeElement { Id = "x", Name = "坏数据", X = double.NaN, Y = double.PositiveInfinity, W = -8, H = 999999, Opacity = 5, Fill = "invalid-color" };
        var unsafeImage = new ImageElement { Id = "image_safe", Name = "图片", Base64 = "not base64!", Mime = "text/plain" };
        var safe = new List<CanvasElement> { legacyTarget, legacyMask, unsafeShape, unsafeImage };
        ModelSafety.Normalize(safe);
        Need(legacyMask.MaskTargetId == legacyTarget.Id, "legacy mask migration");
        Need(safe.Select(/* Extract stable element IDs for uniqueness validation. 提取稳定元素 ID 以验证唯一性。 */ x => x.Id).Distinct(StringComparer.Ordinal).Count() == safe.Count, "unique stable ids");
        Need(double.IsFinite(unsafeShape.X) && double.IsFinite(unsafeShape.Y) && unsafeShape.W >= 1 && unsafeShape.H <= 20000, "finite geometry clamps");
        Need(unsafeShape.Opacity <= 1 && unsafeShape.Fill == "#5b8def", "style clamps");
        Need(unsafeImage.Base64 == "" && unsafeImage.Mime == "image/png", "embedded image validation");

        // Verify mask target stability, inverse output, and legacy adjacent fallback.
        // 验证蒙版目标稳定性、反向输出与旧相邻回退。
        var target = new ShapeElement { Id = "target_stable", Name = "目标", Kind = "roundrect", X = 100, Y = 100, W = 160, H = 90 };
        var unrelated = new ShapeElement { Id = "unrelated", Name = "中间层", Kind = "rect", X = 5, Y = 5, W = 10, H = 10 };
        var mask = new ShapeElement
        {
            Id = "mask_stable", Name = "稳定蒙版", Kind = "ellipse", X = 120, Y = 110, W = 80, H = 60,
            IsMask = true, MaskTargetId = target.Id, MaskMode = "alpha-inverse"
        };
        var reordered = new List<CanvasElement> { mask, unrelated, target };
        string inverseCss = Uri.UnescapeDataString(CssBuilder.Build(reordered, "stable-mask"));
        Need(inverseCss.Contains("<mask id='m'"), "stable id mask after reorder");
        Need(inverseCss.Contains("fill='white'"), "inverse mask outside is visible");
        Need(inverseCss.Contains("mask='url(#m)'"), "export applies svg mask");

        mask.MaskMode = "luminance"; mask.Fill = "#000000";
        string luminanceCss = Uri.UnescapeDataString(CssBuilder.Build(reordered, "luminance-mask"));
        Need(luminanceCss.Contains("fill='black'"), "normal mask outside is hidden");
        Need(luminanceCss.Contains("opacity='0'"), "luminance uses mask brightness");

        mask.MaskTargetId = null;
        string orphanCss = Uri.UnescapeDataString(CssBuilder.Build(reordered, "orphan-mask"));
        Need(!orphanCss.Contains("<mask id='m'"), "orphan non-adjacent mask is not guessed");
        var legacyCss = Uri.UnescapeDataString(CssBuilder.Build(new List<CanvasElement> { target, mask }, "legacy-adjacent"));
        Need(legacyCss.Contains("<mask id='m'"), "legacy adjacent mask remains compatible");

        // Preserve a per-shape aspect lock through model normalization.
        // 在模型规范化后保留每个图形的比例锁定。
        var square = new ShapeElement { Id = "square_lock", Name = "独立正方形锁", Kind = "rect", W = 30, H = 30, LockAspect = true };
        ModelSafety.Normalize(new List<CanvasElement> { square });
        Need(square.LockAspect, "per-shape square lock survives normalization");

        // Verify multiline text wrapping and project-local export reproducibility.
        // 验证多行文字折行与工程级导出的可重复性。
        var customText = new TextRegionElement
        {
            Id = "multiline_text", Name = "多行固定文字", X = 470, Y = 286, W = 90, H = 100,
            Text = "中文第一行\n\nEnglishLongWordWithoutSpaces", CustomText = true, FontSize = 14,
            AlignH = "center", AlignV = "middle", Color = "#112233"
        };
        var textDesign = new Dictionary<string, List<CanvasElement>>
        {
            ["card"] = new List<CanvasElement>
            {
                new ShapeElement { Id = "text_bg", Name = "背景", Kind = "rect", X = 470, Y = 286, W = 260, H = 88, Fill = "#ffffff" },
                customText
            }
        };
        var projectExport = new ProjectExportSettings { ClipToFrame = false, AllowCustomText = true };
        string multilineCss = Uri.UnescapeDataString(CssBuilder.BuildSkins(textDesign, "multiline", projectExport));
        Need(multilineCss.Contains("<tspan"), "multiline text uses tspan");
        Need(multilineCss.Split("<tspan").Length >= 4, "explicit blank line and width wrapping create multiple tspans");
        Need(CssBuilder.WrapText("这是一个需要按照可用宽度自动换行的句子", 42, 14).Count > 1, "CJK width wrapping");

        string reproducibleOne = CssBuilder.BuildSkins(textDesign, "stable-export", projectExport);
        Prefs.Advanced = false; Prefs.ConstrainFrame = true; Prefs.CustomTextRegion = false;
        string reproducibleTwo = CssBuilder.BuildSkins(textDesign, "stable-export", projectExport);
        Need(reproducibleOne == reproducibleTwo, "project export is independent from global preferences");
        Need(reproducibleOne != CssBuilder.BuildSkins(textDesign, "stable-export", new ProjectExportSettings()), "project export settings change output");

        // Require complete preview metadata and fixture coverage for target groups.
        // 要求目标分组具有完整预览元数据与示例覆盖。
        Need(DesignTargetLib.All.All(/* Require preview group and selector metadata for every target. 要求每个目标具有预览分组与选择器元数据。 */ t => !string.IsNullOrWhiteSpace(t.PreviewGroup) && t.PreviewSelectors != null && t.PreviewSelectors.Length > 0), "every design target has preview metadata");
        Need(DesignTargetLib.Find("btn").SkinSelector == ".btn:not(.primary):not(.toggle-on):not(.ghost)", "ordinary button selector excludes special buttons");
        string dimCss = PreviewService.BuildDimCss(new Dictionary<string, List<CanvasElement>> { ["menuBox"] = new List<CanvasElement>() }, 1, .3);
        Need(!dimCss.Contains(".popmenu") && !dimCss.Contains("#wire"), "metadata dimming preserves designed menu and does not hard-code wire");
        foreach (string token in new[] { "id=\"settingsBox\"", "id=\"colorPanel\"", "class=\"start-side\"", "class=\"newcard\"", "class=\"nc-thumb\"", "class=\"recent\"", "disabled" })
            Need(Preview.TestMarkup.Contains(token), "preview fixture covers " + token);

        // Isolate persistence tests from real editor profiles using a scratch data directory.
        // 使用临时数据目录将持久化测试与真实编辑器配置隔离。
        string artifactDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".artifacts"));
        Environment.SetEnvironmentVariable("KNOTJOT_UI_EDITOR_DATA_DIR", artifactDir);
        string atomicPath = Path.Combine(artifactDir, "atomic-save.txt");
        AtomicFile.WriteUtf8(atomicPath, "first");
        AtomicFile.WriteUtf8(atomicPath, "second-中文");
        Need(File.ReadAllText(atomicPath) == "second-中文", "atomic replace");
        Need(!Directory.GetFiles(artifactDir, "atomic-save.txt.tmp-*").Any(), "atomic temp cleanup");

        // Route the four historical schema fixtures through the migration reader.
        // 将四个历史结构夹具交给迁移读取器分派。
        string fixtureDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "fixtures"));
        var fixtureExpectations = new[]
        {
            (File: "project-v0.0.1.bmapui", Version: "V0.0.1", Target: "card", Marker: "#112233", Migrated: true),
            (File: "project-v0.0.2.bmapui", Version: "V0.0.2", Target: "toolbar", Marker: "#123456", Migrated: true),
            (File: "project-v0.0.3.bmapui", Version: "V0.0.3", Target: "menuBox", Marker: "menu_shape", Migrated: true),
            (File: "project-v1.0.0.bmapui", Version: "V1.0.0", Target: "card", Marker: "card_text", Migrated: false)
        };
        foreach (var fixture in fixtureExpectations)
        {
            using var projectDoc = ProjectFileService.Open(Path.Combine(fixtureDir, fixture.File));
            var migrated = LegacyProjectMigrator.Read(projectDoc.RootElement, new JsonSerializerOptions());
            Need(migrated.SourceVersion == fixture.Version, "schema route " + fixture.Version);
            Need(migrated.Designs.TryGetValue(fixture.Target, out var design) && design.Contains(fixture.Marker), "migration content " + fixture.Version);
            Need(migrated.Migrated == fixture.Migrated, "migration flag " + fixture.Version);
        }

        // Reject excessive project complexity before mutating the window state.
        // 在修改窗口状态前拒绝复杂度过高的工程。
        string excessivePath = Path.Combine(artifactDir, "excessive-elements.bmapui");
        string element = "{\"type\":\"rect\",\"id\":\"element_safe\",\"w\":1,\"h\":1}";
        AtomicFile.WriteUtf8(excessivePath, "{\"app\":\"knotjot-ui-editor-project\",\"designs\":{\"card\":{\"elements\":[" + string.Join(",", Enumerable.Repeat(element, ProjectFileService.MaxElementsPerDesign + 1)) + "]}}}");
        NeedThrows<InvalidDataException>(/* Open an excessive-element fixture to verify rejection before UI mutation. 打开元素过多的夹具，验证界面变更前拒绝输入。 */ () => { using var _ = ProjectFileService.Open(excessivePath); }, "oversized element collection rejected before window mutation");

        // Import a legacy library once while writing the current format marker.
        // 仅导入一次旧库，并写入当前格式标记。
        string fixtureMain = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "fixtures", "main.js"));
        string legacyFile = Path.Combine(artifactDir, "legacy-ui-skins.json");
        AtomicFile.WriteUtf8(legacyFile, "{\"format\":\"thoughtcanvas-ui-skins\",\"version\":1,\"current\":\"ui_legacy\",\"skins\":{\"ui_legacy\":{\"name\":\"旧皮肤\",\"kind\":\"css\",\"css\":\"body{}\"}}}");
        Environment.SetEnvironmentVariable("THOUGHTCANVAS_UI_SKINS_FILE", legacyFile);
        string legacyMigrationFile = Path.Combine(artifactDir, "legacy-migration-" + Guid.NewGuid().ToString("N"), "ui-skins.json");
        Environment.SetEnvironmentVariable("KNOTJOT_UI_SKINS_FILE", legacyMigrationFile);
        KnotJotConnection.Write(fixtureMain, "ui_new_after_legacy", "迁移后", "body{color:black}", null, null);
        using (var legacyDoc = JsonDocument.Parse(File.ReadAllText(legacyMigrationFile)))
            Need(legacyDoc.RootElement.GetProperty("format").GetString() == "knotjot-ui-skins" && legacyDoc.RootElement.GetProperty("skins").TryGetProperty("ui_legacy", out _), "legacy library is read once and new writes use KnotJot format");

        Environment.SetEnvironmentVariable("THOUGHTCANVAS_UI_SKINS_FILE", Path.Combine(artifactDir, "missing-legacy-ui-skins.json"));
        // Verify stable-ID replacement, increasing revisions, hashes, and editable sync payloads.
        // 验证稳定 ID 替换、修订递增、哈希与可编辑同步数据。
        string syncFile = Path.Combine(artifactDir, "sync-" + Guid.NewGuid().ToString("N"), "ui-skins.json");
        Environment.SetEnvironmentVariable("KNOTJOT_UI_SKINS_FILE", syncFile);
        string editorOne = "{\"app\":\"knotjot-ui-editor-project\",\"schema\":\"knotjot-ui-skin\",\"id\":\"ui_sync_smoke\",\"name\":\"同步测试\",\"css\":\"body{color:red}\"}";
        var syncOne = KnotJotConnection.Write(fixtureMain, "ui_sync_smoke", "同步测试", "body{color:red}", editorOne, null);
        var syncTwo = KnotJotConnection.Write(fixtureMain, "ui_sync_smoke", "同步测试二", "body{color:blue}", editorOne.Replace("red", "blue"), null);
        Need(syncTwo.Revision == syncOne.Revision + 1, "sync revision increments");
        Need(syncOne.ContentHash != syncTwo.ContentHash, "sync content hash changes");
        using (var syncDoc = JsonDocument.Parse(File.ReadAllText(syncFile)))
        {
            var root = syncDoc.RootElement;
            Need(root.GetProperty("format").GetString() == "knotjot-ui-skins", "sync library schema");
            Need(root.GetProperty("current").GetString() == "ui_sync_smoke", "sync selects current skin");
            var skins = root.GetProperty("skins");
            Need(skins.EnumerateObject().Count() == 1, "same id updates instead of duplicates");
            var record = skins.GetProperty("ui_sync_smoke");
            Need(record.GetProperty("css").GetString() == "body{color:blue}", "latest css replaces same skin");
            Need(record.GetProperty("editorData").GetProperty("app").GetString() == "knotjot-ui-editor-project", "editable project travels with sync");
            Need(record.GetProperty("contentHash").GetString() == syncTwo.ContentHash, "record content hash");
        }
        // Restore the persisted target and queried library path.
        // 恢复已保存目标与查询所得皮肤库路径。
        var restored = KnotJotConnection.RestoreConnection();
        Need(restored?.Target == fixtureMain && restored.LibraryPath == syncFile, "connection and queried location persistence");

        Console.WriteLine("UI_EDITOR_CORE_RESULT {\"modelSafety\":true,\"fourSchemaMigrations\":true,\"projectLimits\":true,\"projectExportReproducible\":true,\"multilineTspan\":true,\"previewCatalog\":true,\"stableMaskAfterReorder\":true,\"perShapeLock\":true,\"legacyLibraryMigration\":true,\"connectionPersistence\":true,\"sameIdDynamicUpdate\":true,\"editorDataSync\":true}");
        return 0;
    }
}
