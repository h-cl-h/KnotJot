using System.Reflection;
using System.Text.Json;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using KnotJotTextStyleEditor;

internal static class Program
{
    // EN: Use a temporary source target and isolated connection data to verify first-sync ID allocation and same-style updates.
    // ZH: 使用临时源码目标及隔离连接数据，验证首次同步 ID 分配和同样式更新。
    [STAThread]
    static void Main()
    {
        var root = Path.Combine(Path.GetTempPath(), "bmap-multi-style-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var target = Path.Combine(root, "main.js");
        File.WriteAllText(target, "// test");
        Environment.SetEnvironmentVariable("KNOTJOT_TEXT_STYLE_EDITOR_DATA_DIR", Path.Combine(root, "editor-data"));
        try
        {
            _ = new Application();
            var window = new MainWindow();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(MainWindow);
            type.GetField("connectedRoot", flags)!.SetValue(window, root);
            type.GetField("connectedTarget", flags)!.SetValue(window, target);
            var sync = type.GetMethod("Sync", flags)!;
            var newStyle = type.GetMethod("NewStyle", flags)!;
            var styleField = type.GetField("style", flags)!;
            var nameBox = (TextBox)window.FindName("NameBox");
            var idBox = (TextBox)window.FindName("IdBox");
            // EN: Create valid drawable geometry and reuse a requested ID to test collision handling on successive synchronizations.
            // ZH: 创建有效可绘制几何并复用请求 ID，以测试连续同步时的重名处理。
            void Prepare(string name)
            {
                var s = (TextBoxStyle)styleField.GetValue(window)!;
                s.TextRegion = new TextRegion
                {
                    X = 10,
                    Y = 10,
                    W = 80,
                    H = 80
                };
                s.Layers.Add(new StyleLayer { X = 5, Y = 5, W = 90, H = 90 });
                nameBox.Text = name;
                idBox.Text = "my-text-style";
                // EN: Associate each fixture with a standalone project so Save & Sync really saves it before the guarded New command.
                // ZH: 为每个样本关联独立项目，使保存并同步实际保存后再执行有未保存保护的新建命令。
                type.GetField("currentFile", flags)!.SetValue(window, Path.Combine(root, name + ".knotjot-textstyle"));
            }

            // EN: Sync two fresh documents requesting the same ID; neither may overwrite the other.
            // ZH: 同步两个请求相同 ID 的新文档，确保互不覆盖。
            Prepare("第一个样式");
            sync.Invoke(window, [false ]);
            newStyle.Invoke(window, null);
            Prepare("第二个样式");
            sync.Invoke(window, [false ]);
            var file = StyleLibraryLocation.ForSource(target);
            var lib = JsonSerializer.Deserialize<StyleLibrary>(File.ReadAllText(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            if (lib.Styles.Count != 2 || lib.Styles.Select( /* EN: Collect IDs to verify case-insensitive uniqueness. ZH: 收集 ID 以校验不区分大小写的唯一性。 */x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 2 || !lib.Styles.Any( /* EN: Require the second style to receive the collision suffix. ZH: 要求第二个样式取得重名后缀。 */x => x.Id == "my-text-style-2"))
                throw new Exception("第二个样式被覆盖");
            // EN: Resync an edited document and ensure it updates its existing entry rather than adding another.
            // ZH: 重新同步已编辑文档，确保更新原条目而非新增。
            nameBox.Text = "第二个样式修改";
            sync.Invoke(window, [false ]);
            lib = JsonSerializer.Deserialize<StyleLibrary>(File.ReadAllText(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            if (lib.Styles.Count != 2)
                throw new Exception("继续编辑同一样式时错误新增");
            Console.WriteLine("MULTI_STYLE_SYNC_SMOKE_OK count=2 ids=" + string.Join(',', lib.Styles.Select( /* EN: Report all persisted IDs in the success marker. ZH: 在成功标记中报告所有已保存 ID。 */x => x.Id)));
            window.Close();
        }
        finally
        {
            Environment.SetEnvironmentVariable("KNOTJOT_TEXT_STYLE_EDITOR_DATA_DIR", null);
            // EN: Keep sync inputs/results for a user-requested audit, otherwise clean up the isolated fixture.
            // ZH: 用户要求审查留档时保留同步输入输出，否则清理隔离样本。
            if (Environment.GetEnvironmentVariable("KNOTJOT_KEEP_TEST_FILES") != "1") Directory.Delete(root, true);
        }
    }
}
