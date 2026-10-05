using System;

namespace KnotJotTextStyleEditor;
public static class StyleFileExtensions
{
    // EN: Keep save dialogs on the current extension while open dialogs retain legacy and raw JSON compatibility.
    // ZH: 保存对话框使用当前扩展名，打开对话框保留旧版及原始 JSON 兼容。
    public const string DefaultExtension = ".knotjot-textstyle";
    public const string OpenFilter = "KnotJot 文本框样式 (*.knotjot-textstyle;*.bmaptextstyle;*.json)|*.knotjot-textstyle;*.bmaptextstyle;*.json|所有文件|*.*";
    public const string SaveFilter = "KnotJot 文本框样式 (*.knotjot-textstyle)|*.knotjot-textstyle";
    // EN: Accept current, legacy, and raw JSON style suffixes case-insensitively while rejecting unrelated file formats.
    // ZH: 不区分大小写地接受当前、旧版及原始 JSON 样式后缀，并拒绝其他格式。
    public static bool IsSupported(string file) => file != null && (file.EndsWith(DefaultExtension, StringComparison.OrdinalIgnoreCase) || file.EndsWith(".bmaptextstyle", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
}
