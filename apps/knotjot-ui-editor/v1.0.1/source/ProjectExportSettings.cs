namespace KnotJotUiEditor
{
    /// <summary>会影响 CSS 结果的工程级设置；必须随 .knotjot-ui 一起保存。</summary>
    public sealed class ProjectExportSettings
    {
        public bool ClipToFrame { get; set; } = true;
        public bool AllowCustomText { get; set; }

        // Copy export settings so project-level choices can be edited independently.
        // 复制导出设置，使工程级选项可以独立编辑。
        public ProjectExportSettings Clone()
        {
            return new ProjectExportSettings { ClipToFrame = ClipToFrame, AllowCustomText = AllowCustomText };
        }
    }
}
