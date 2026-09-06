# KnotJot UI Editor V1.0.0 (English)

KnotJot UI Editor designs complete UI skins for KnotJot. Components you do not design keep their original appearance. You can save a standalone `.knotjot-ui` project or connect to KnotJot and update the same skin dynamically. Legacy `.bmapui` projects remain readable.

## Quick start

1. Pick a component on the left, such as a node, toolbar, button, or menu.
2. Draw vector shapes, ink, or imported images inside its design frame.
3. Add a text region when the real component must remain editable.
4. Use Real Preview to verify the skin against KnotJot-compatible markup.
5. Save the standalone project, or connect to KnotJot and sync it as a separate action.

A running KnotJot instance normally reloads the updated skin within about one second. The project keeps a stable UI ID, so repeated syncs update one record instead of creating duplicates.

## Main V1.0.0 features

- Multi-select, marquee select, grouped move and resize, layer locks, visibility, deletion, z-order buttons, and drag-and-drop reordering.
- Stable mask target IDs with Alpha, Inverse Alpha, Luminance, and Inverse Luminance modes.
- A 100-step undo history, normalized project data, bounded image imports, and atomic file replacement.
- A shared `knotjot-ui-skins` library with revision, writer ID, SHA-256 content hashes, editable project data, and live reload.
- Exact WebView2 package version `1.0.4078.44`.

## Compatibility and safety

Older adjacent masks are migrated when possible. Missing V1.0.0 fields receive safe defaults. Invalid or oversized project data is normalized or rejected, and a damaged existing skin library is preserved rather than silently overwritten.

## Build

```powershell
dotnet build "KnotJot.UiEditor.csproj" -c Release
dotnet run --project "tests\CoreSmoke\CoreSmoke.csproj" -c Release
dotnet publish "KnotJot.UiEditor.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
makensis installer.nsi
```

The editor is GPL-3.0-only. See [LICENSE](LICENSE), [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md), and `THIRD_PARTY_LICENSES` for complete notices.

---

# KnotJot 界面编辑器 V1.0.0

KnotJot 界面编辑器用于设计 KnotJot 的整套 UI 外观。没有设计的部件继续使用原版；设计过的部件会导出为带功能的 CSS 皮肤。它既可以独立保存 `.knotjot-ui`，也可以连接 KnotJot 后动态同步；旧 `.bmapui` 工程仍可打开。

## 普通用户快速开始

1. 在左侧选择要设计的部件，例如普通节点、选中节点、工具栏或菜单。
2. 在设计框中画矩形、圆角矩形、椭圆、线条，或导入图片、使用画笔。
3. 需要保留可输入文字时，放置“文本区”；未设计的部件会自动保持原版。
4. 点击“真实预览”检查节点、工具栏和菜单在真实结构中的效果。
5. 点击“保存 .knotjot-ui”保存工程；或先“连接主程序…”，再点“同步到 KnotJot”。两个操作语义独立。

同步后，运行中的 KnotJot 通常会在约 1 秒内更新。编辑器会先向主程序查询真实皮肤库路径；连续同步同一个工程会更新同一个 UI ID。独立 `.knotjot-ui` 文件可单独备份和移动，旧 `.bmapui` 工程仍可打开。

## V1.0.0 重点改进

- 图层支持多选、Ctrl 增减选择、空白拖拽框选、组合移动、组合缩放、微调、显隐、锁定、删除、置顶/置底和拖拽排序。
- 锁定图层仍可选择查看，但不能移动、缩放、删除或调整层级。
- 撤销历史提高到 100 步；连续微调继续合并为合理的撤销步骤。
- 蒙版使用稳定目标 ID，不再依赖“必须紧挨下一层”。图层重排后关联仍在。
- 蒙版支持 Alpha、反向 Alpha、亮度和反向亮度；WPF 画布预览与 CSS/SVG 导出使用同一目标规则。
- 工程保存采用同目录临时文件后原子替换；模型会限制异常坐标、无穷数、超大图片、过多元素和无效 Base64。
- 图片导入上限为 12 MB、最长边 8192 像素、总像素 4000 万。
- 新增主程序连接、稳定 UI ID、SHA-256 内容哈希、revision/writerId 和可编辑工程数据同步。
- WebView2 NuGet 版本固定为 `1.0.4078.44`，不再使用通配版本。

## 蒙版说明

选中图形、手绘或图片后点击“加遮罩”，编辑器会创建一个圆角矩形蒙版并绑定当前目标。也可以把普通非线条图形勾选为蒙版，再在右侧选择模式和目标图层。

- Alpha：显示蒙版覆盖范围。
- 反向 Alpha：隐藏蒙版覆盖范围，显示其余部分。
- 亮度：按蒙版填充色亮度与透明度决定显示强度。
- 反向亮度：对亮度结果取反。

旧版没有目标 ID 的相邻蒙版会在读取时迁移；旧文件不会被静默覆盖。

## 文件与兼容性

- 独立工程：新的 `.knotjot-ui` 使用 `app: "knotjot-ui-editor-project"`、`schema: "knotjot-ui-skin"` 和 version 5，并保存导出设置；旧 `.bmapui` 工程仍在迁移入口读取。
- 动态库：`knotjot-ui-skins` version 1。每条记录包含 CSS、内容哈希、写入者、时间和可编辑 `editorData`。
- V0.0.3 和更早工程缺少的新字段会自动补默认值；旧相邻蒙版会尽量迁移为稳定目标。
- UI 皮肤只负责界面结构和装饰；KnotJot 文本框样式文件与 UI 皮肤库分开保存。

## 安全限制

工程读取会规范化 ID、数值、颜色、透明度、混合模式、手绘点数和嵌入图片。最多保留 5000 个元素、每条手绘最多 100000 个点、单张嵌入图片 Base64 最多 16 MB 字符。动态皮肤库最多 500 条、总文件最多 16 MB、单条动态同步 CSS 最多 2 MB（以随程序发布的协议为准）。现有库损坏或标识不兼容时，同步会报错并保留原文件。

## 构建与测试

需要 Windows、.NET 8 SDK 和 WebView2 NuGet 包：

```powershell
dotnet build "KnotJot.UiEditor.csproj" -c Release
dotnet run --project "tests\CoreSmoke\CoreSmoke.csproj" -c Release
dotnet publish "KnotJot.UiEditor.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
makensis installer.nsi
```

CoreSmoke 覆盖模型安全、旧文件迁移、稳定蒙版、四种蒙版输出、原子保存、连接恢复、同 ID 动态更新和编辑工程同步。更多架构、格式与迁移细节见 [DEVELOPMENT.md](DEVELOPMENT.md)、[FORMAT.md](FORMAT.md) 和 [MIGRATION.md](MIGRATION.md)。

## 许可证

编辑器自有代码采用 GPL-3.0-only，完整文本见 [LICENSE](LICENSE)。WebView2 与自包含 .NET 运行时的声明和许可副本见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) 与 `THIRD_PARTY_LICENSES`。

