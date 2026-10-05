using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace KnotJotUiEditor
{
    /// <summary>
    /// 与 KnotJot index.html 里的颜色推导保持逐位一致：
    ///   HexDark(hex,f) = 各通道 *f，四舍五入(远离0，与 JS Math.round 对正数一致)，封顶255。
    /// </summary>
    public static class ColorUtil
    {
        private static readonly Regex Six = new Regex("^[0-9a-fA-F]{6}$", RegexOptions.Compiled);
        private static readonly Regex Three = new Regex("^[0-9a-fA-F]{3}$", RegexOptions.Compiled);

        /// <summary>
        /// 智能规整成 "#rrggbb"（小写）：# 可省略、支持 3 位简写、大小写皆可、去空格；
        /// 无法识别返回 null。
        /// </summary>
        // Expand three-digit colors and normalize valid hex input to lowercase #rrggbb; reject other text.
        // 展开三位颜色，将有效输入规范为小写 #rrggbb，并拒绝其他文本。
        public static string NormalizeHex(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            if (s.StartsWith("#")) s = s.Substring(1);
            if (Six.IsMatch(s)) return "#" + s.ToLowerInvariant();
            if (Three.IsMatch(s)) return "#" + string.Concat(s.Select(/* Duplicate each shorthand hex digit. 重复每个简写十六进制数位。 */ c => new string(c, 2))).ToLowerInvariant();
            return null;
        }

        // Accept any color that NormalizeHex can convert, including three-digit shorthand.
        // 接受 NormalizeHex 能转换的颜色，包括三位简写。
        public static bool IsHex6(string s)
        {
            return NormalizeHex(s) != null;
        }

        /// <summary>解析成 RGB，失败返回 null（容忍缺 # 与 3 位简写）。</summary>
        // Parse a normalized color into RGB bytes; return null for invalid input.
        // 将规范颜色解析为 RGB 字节，无效输入返回 null。
        public static (byte R, byte G, byte B)? ParseHex(string hex)
        {
            string n = NormalizeHex(hex);
            if (n == null) return null;
            string h = n.Substring(1);
            byte r = byte.Parse(h.Substring(0, 2), NumberStyles.HexNumber);
            byte g = byte.Parse(h.Substring(2, 2), NumberStyles.HexNumber);
            byte b = byte.Parse(h.Substring(4, 2), NumberStyles.HexNumber);
            return (r, g, b);
        }

        /// <summary>复刻 JS hexDark：非法输入原样返回。</summary>
        // Scale RGB channels by the supplied factor and format the resulting hex color.
        // 按指定系数缩放 RGB 通道，并格式化为十六进制颜色。
        public static string HexDark(string hex, double f)
        {
            var p = ParseHex(hex);
            if (p == null) return hex;
            int r = Math.Min(255, (int)Math.Round(p.Value.R * f, MidpointRounding.AwayFromZero));
            int g = Math.Min(255, (int)Math.Round(p.Value.G * f, MidpointRounding.AwayFromZero));
            int b = Math.Min(255, (int)Math.Round(p.Value.B * f, MidpointRounding.AwayFromZero));
            return string.Format("#{0:x2}{1:x2}{2:x2}", r, g, b);
        }

        // Format RGB bytes as a lowercase six-digit CSS color.
        // 将 RGB 字节格式化为小写六位 CSS 颜色。
        public static string FromRgb(byte r, byte g, byte b)
        {
            return string.Format("#{0:x2}{1:x2}{2:x2}", r, g, b);
        }
    }
}
