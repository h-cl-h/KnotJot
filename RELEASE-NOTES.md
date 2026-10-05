# KnotJot V1.0.1

## English

V1.0.1 improves the three KnotJot apps using V1.0.0 as their baseline.

### KnotJot

- More reliable selection, group dragging, zoomed-canvas coordinates, Copy/Cut/Paste and menu layering.
- A background control for each sheet, correctly scaled previews and automatic image fitting for limited canvases.
- Original inserted image data is retained while display size remains adjustable.
- Better brace/spider switching, timeline spacing and matrix containment.
- Editable callouts and boundary titles; empty boundary titles disappear, and boundaries can be deleted.
- Parent to-do completion can propagate to descendants, with a General setting to disable it.
- Improved recovery copies, saved undo history and Fit All Content.
- Correct mask transparency and inverse luminance, and explicit zero-width borders.
- Refreshed the desktop runtime and sanitizer dependency, updated build tools, and preserved the original save destination when dragging in a map with the new runtime.
- Fixed the first-launch skin shortcut initialization in both interface languages.
- AI presets now refresh the displayed credential destination; timeout and server-error messages include recovery guidance.

### Companion editors

- UI Editor: improved selection and rotated transforms, mask previews, save/sync validation and reliable real-preview retry.
- Text Style Editor: improved layer locks, grouped undo, file/library save state, image masks, CSS inset/outer-shadow preview and connection reply handling.
- Installers offer English and Simplified Chinese and use English product names.
- User guides now provide a complete English edition followed by a complete Chinese edition, with build instructions in separate documents.
- Copying UI preview diagnostics handles a busy clipboard without closing the editor and confirms a successful copy.
- Both editors launch a connected source checkout through its local Electron runtime and explain missing dependencies.

### Compatibility

Existing map/project formats and editor connection identities are retained. New summary creation was removed; older summaries remain readable. The Windows Open dialog now starts in Downloads; navigate to your map folder as needed. AI remains optional and depends on a compatible external or local service. See the individual guides for preview runtime requirements. Manual acceptance remains listed in RELEASE-STATUS.md.

---

## 中文

V1.0.1 以三个软件各自的 V1.0.0 为基础进行改进。

### KnotJot 主程序

- 改进选择、多选拖动、缩放坐标、复制剪切粘贴及菜单层级。
- 每张画布提供背景入口，预览比例正确，有限画布可自动适配导入背景图片。
- 插入图片保留原始数据，同时支持调整显示大小。
- 改进大括号与蜘蛛网切换、时间轴间距及矩阵包含关系。
- 标注和外框标题可编辑；空外框标题自动消失，外框可删除。
- 完成父级待办时可同步完成后代待办，可在通用设置关闭。
- 改进恢复副本、持久撤销历史及适配全部内容。
- 修正蒙版透明度、反向亮度及显式零边框显示。
- 更新桌面运行内核、净化依赖和构建工具，兼容新版运行时拖入导图后的原位置保存。
- 修复两种界面语言首次启动时皮肤快捷项的初始化异常。
- AI 预设切换后同步更新密钥目的地址，超时和服务器错误提供恢复建议。

### 配套编辑器

- 界面编辑器：改进选择、旋转变换、蒙版预览、保存同步校验及真实预览重试。
- 文本框样式编辑器：改进图层锁定、连续操作撤销、文件与样式库保存状态、图片蒙版、CSS 内外阴影预览及连接响应处理。
- 安装器提供英文和简体中文，使用英文产品名。
- 使用指南改为完整英文在前、完整中文在后，构建说明单独存放。
- 界面预览诊断复制遇到剪贴板占用时保留编辑器并提示重试，成功复制后提供确认。
- 两个编辑器通过源码自带的 Electron 运行程序启动已连接源码，缺少依赖时提供明确说明。

### 兼容性

保留现有导图、工程格式和编辑器连接身份。已移除新建概要，旧概要仍可读取。Windows 打开对话框现从“下载”目录开始，可按需转到导图所在文件夹。AI 可选，依赖兼容的外部或本地服务。预览运行环境要求见各软件指南；人工待验收范围继续记录在 RELEASE-STATUS.md。
