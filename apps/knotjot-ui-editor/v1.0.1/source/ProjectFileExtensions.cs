using System;

namespace KnotJotUiEditor;

public static class ProjectFileExtensions
{
    public const string DefaultExtension = ".knotjot-ui";
    public const string OpenFilter = "KnotJot UI 工程 (*.knotjot-ui;*.bmapui)|*.knotjot-ui;*.bmapui|所有文件 (*.*)|*.*";

    // Accept current .knotjot-ui and legacy .bmapui paths using case-insensitive extension checks.
    // 以不区分大小写的扩展名检查接受当前 .knotjot-ui 与旧 .bmapui 路径。
    public static bool IsSupported(string file) => file != null &&
        (file.EndsWith(DefaultExtension, StringComparison.OrdinalIgnoreCase) ||
         file.EndsWith(".bmapui", StringComparison.OrdinalIgnoreCase));
}
