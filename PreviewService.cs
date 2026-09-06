using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace KnotJotUiEditor
{
    public static class PreviewService
    {
        // Use target preview metadata to dim undesigned groups while preserving designed groups.
        // 使用目标预览元数据淡化未设计分组，并保留已设计分组。
        public static string BuildDimCss(IDictionary<string, List<CanvasElement>> designs, int mode, double dim)
        {
            if (mode == 0) return "";
            string opacity = mode == 2 ? "0" : Math.Max(.05, Math.Min(.8, dim)).ToString("0.##", CultureInfo.InvariantCulture);
            var sb = new StringBuilder();
            foreach (var group in DesignTargetLib.All.Where(/* Keep catalog targets that define a preview group. 保留定义了预览分组的目录目标。 */ t => !string.IsNullOrWhiteSpace(t.PreviewGroup)).GroupBy(/* Select the target's preview group key. 提取目标预览分组键。 */ t => t.PreviewGroup))
            {
                if (group.Any(/* Determine whether a preview group includes a designed target. 判断预览分组是否包含已设计目标。 */ t => designs != null && designs.ContainsKey(t.Id))) continue;
                var selectors = group.SelectMany(/* Read preview selectors, defaulting absent metadata to an empty list. 读取预览选择器，元数据缺失时使用空列表。 */ t => t.PreviewSelectors ?? Array.Empty<string>()).Where(/* Discard empty preview selectors. 丢弃空预览选择器。 */ x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
                if (selectors.Length > 0) sb.Append(string.Join(",", selectors)).Append("{opacity:").Append(opacity).Append("!important;}");
            }
            return sb.ToString();
        }

        // Describe preview source, failure, and CSS size without including private CSS content.
        // 描述预览来源、错误与 CSS 大小，不包含私有 CSS 内容。
        public static string CreateDiagnostic(string source, string targetPath, string css, Exception error)
        {
            string hash;
            using (var sha = SHA256.Create()) hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(css ?? "")));
            return "预览来源：" + (source ?? "未知") + Environment.NewLine
                + "目标：" + (string.IsNullOrWhiteSpace(targetPath) ? "内置预览" : Path.GetFullPath(targetPath)) + Environment.NewLine
                + "CSS SHA-256：" + hash + Environment.NewLine
                + "错误类型：" + (error == null ? "未知" : error.GetType().Name) + Environment.NewLine
                + "错误：" + (error == null ? "未知错误" : error.Message);
        }
    }
}
