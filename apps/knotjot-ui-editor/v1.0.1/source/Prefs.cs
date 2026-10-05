using System;
using System.IO;
using System.Text.Json;

namespace KnotJotUiEditor
{
    /// <summary>
    /// 偏好设置（持久化到 %AppData%\KnotJot界面编辑器\prefs.json）。
    /// 只保存跨工程的编辑器偏好；会改变导出结果的值仅作为新工程默认值。
    /// </summary>
    public static class Prefs
    {
        // 真实预览：未修改的原版底图显示方式 0=完整 1=淡化 2=隐藏
        public static int PreviewOriginalMode = 1;
        // 真实预览：淡化程度（PreviewOriginalMode==1 时用）0.05~0.8
        public static double PreviewDim = 0.30;

        // 真实预览来源：0=内置模拟；1=真实软件（加载 KnotJot 的 index.html）
        public static int PreviewSource = 0;
        public static string KnotJotPath = "";

        // 高级模式：解锁"自由编辑（不限定设计框）"与"自己画文本区/固定文字"
        public static bool Advanced = false;
        // 高级模式下：是否仍限定到设计框（false=自由，超出框也照样导出）
        public static bool ConstrainFrame = true;
        // 高级模式下：文本区是否用自己打的固定文字（false=用部件本来的文字）
        public static bool CustomTextRegion = false;

        /// <summary>是否把设计裁剪到框内（普通模式恒 true；高级模式看 ConstrainFrame）。</summary>
        // Derive the effective frame-clipping preference from the advanced-mode settings.
        // 根据高级模式设置计算实际设计框裁剪偏好。
        public static bool ClipToFrame { get { return !Advanced || ConstrainFrame; } }
        /// <summary>文本区是否允许固定文字（普通模式恒 false）。</summary>
        // Derive whether fixed custom text is enabled by the current preference mode.
        // 根据当前偏好模式计算是否允许固定自定义文字。
        public static bool AllowCustomText { get { return Advanced && CustomTextRegion; } }

        private static readonly string PathFile = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KnotJot界面编辑器", "prefs.json");
        private static readonly string LegacyPathFile = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BMAP界面编辑器", "prefs.json");

        private class Dto
        {
            public int previewOriginalMode { get; set; }
            public double previewDim { get; set; }
            public bool advanced { get; set; }
            public bool constrainFrame { get; set; } = true;
            public bool customTextRegion { get; set; }
            public int previewSource { get; set; }
            public string knotJotPath { get; set; } = "";
            // 仅用于读取旧偏好；保存时不再写旧品牌字段。
            public string tcPath { get; set; } = "";
        }

        // Restore persisted editor preferences and retain defaults when configuration cannot be read.
        // 恢复已保存编辑器偏好，无法读取配置时保留默认值。
        public static void Load()
        {
            try
            {
                string file = File.Exists(PathFile) ? PathFile : LegacyPathFile;
                if (!File.Exists(file)) return;
                var d = JsonSerializer.Deserialize<Dto>(File.ReadAllText(file));
                if (d == null) return;
                PreviewOriginalMode = d.previewOriginalMode < 0 || d.previewOriginalMode > 2 ? 1 : d.previewOriginalMode;
                PreviewDim = Clamp(d.previewDim, 0.05, 0.8, 0.30);
                Advanced = d.advanced;
                ConstrainFrame = d.constrainFrame;
                CustomTextRegion = d.customTextRegion;
                PreviewSource = d.previewSource == 1 ? 1 : 0;
                KnotJotPath = !string.IsNullOrWhiteSpace(d.knotJotPath) ? d.knotJotPath : (d.tcPath ?? "");
            }
            catch { }
        }

        // Persist editor preference values under the user's application-data directory.
        // 在用户应用数据目录保存编辑器偏好值。
        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathFile));
                var d = new Dto
                {
                    previewOriginalMode = PreviewOriginalMode,
                    previewDim = PreviewDim,
                    advanced = Advanced,
                    constrainFrame = ConstrainFrame,
                    customTextRegion = CustomTextRegion,
                    previewSource = PreviewSource,
                    knotJotPath = KnotJotPath
                };
                AtomicFile.WriteUtf8(PathFile, JsonSerializer.Serialize(d, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        // Constrain a stored numeric preference to its accepted range.
        // 将保存的数值偏好限制在允许范围内。
        private static double Clamp(double v, double lo, double hi, double fb)
        {
            if (double.IsNaN(v) || v < lo || v > hi) return v < lo && v > 0 ? lo : (v > hi ? hi : fb);
            return v;
        }
    }
}
