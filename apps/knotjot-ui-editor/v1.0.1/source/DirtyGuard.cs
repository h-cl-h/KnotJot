using System;
using System.Windows;

namespace KnotJotUiEditor
{
    /// <summary>所有会替换或关闭当前工程的入口共用同一套未保存保护。</summary>
    public static class DirtyGuard
    {
        // Allow navigation only after save succeeds or discard is chosen; cancel preserves editing.
        // 仅在保存成功或选择放弃后允许跳转，取消则保留编辑状态。
        public static bool Confirm(Window owner, bool dirty, Func<bool> save)
        {
            if (!dirty) return true;
            var result = MessageBox.Show(owner,
                "当前方案有未保存的修改。\n\n是：先保存再继续\n否：不保存并继续\n取消：留在当前方案",
                "未保存的修改", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
            if (result == MessageBoxResult.Cancel) return false;
            if (result == MessageBoxResult.No) return true;
            return save != null && save();
        }
    }
}
