# KnotJot UI Editor V1.0.1

Design the look of KnotJot, one component at a time.
Create a quiet workspace, illustrated controls, or a coordinated skin for your maps.
Components you leave undesigned keep their original appearance.

[English](#english) | [Chinese](#chinese)

## English

### What you can do

- Design nodes, buttons, toolbars, menus and other KnotJot interface components.
- Combine rectangles, rounded rectangles, ellipses, lines, freehand drawing and imported images.
- Position the real component text inside your artwork with a text region.
- Arrange, select, move, resize, hide and lock layers; use masks to shape their visible areas.
- Check your work in an interactive browser preview, including sample text and resizing.
- Save an editable project on its own, or sync a skin to a connected KnotJot installation.

V1.0.1 is based on V1.0.0 and includes fixes to selection, rotated artwork, masks and preview retry.
Existing project formats and connections remain compatible.
UI Editor currently uses Chinese control labels; this edition describes their functions in English.

### Download and install

Use the Windows x64 files from the project's release distribution:

| You need | File |
| --- | --- |
| UI Editor only | `KnotJot-UI-Editor-Setup-1.0.1.exe` |
| KnotJot and its companion editors | `KnotJot-Suite-Setup-1.0.1.exe` |

Run the chosen installer, select its language and installation folder, then open **KnotJot UI Editor** from the Start menu or desktop shortcut.
The self-contained release includes its .NET runtime; you do not need the .NET SDK to use it.
**Microsoft Edge WebView2 Runtime** is needed for the browser preview.
You can still design and save projects when that preview is unavailable.

### Make your first skin

1. Choose **New** and give the skin a recognizable name.
2. Select a component in the left-hand list, such as a normal node. Start with one component so changes are easy to compare.
3. Add a rounded rectangle or image inside its design frame. Adjust fill, outline and size in the properties panel.
4. Select or add the component's **text region**. Leave room around the real text and try a longer sample.
5. Switch to **Real Preview** and test the sample component. Return to **Canvas** to adjust layers, text placement and masks.
6. Choose **Save .knotjot-ui** to keep an editable project in a folder you control.
7. To use it in KnotJot, choose **Connect to main application**, select its executable or shortcut, and then **Sync to KnotJot**. Select the skin in KnotJot's UI skin controls if it is not already active.

Repeat the design steps for other components. Switching components keeps their separate designs in the same project.

### Text regions keep text useful

A text region tells the skin where the component's real text belongs and how it should align.
It is not another decorative shape or a replacement mind-map document.
For a component with text, use its existing region or add one with the text-region tool.
There is one region per designed component; pressing the tool again selects the existing region.

Move and resize the region, then adjust its sample, font and alignment in the properties panel.
Keep the normal text source for labels and editable content that should come from KnotJot.
Advanced fixed-text options are for deliberate decoration; check the result before applying them to an editable component.

### Work with layers and masks

Use the layer list to select artwork, change its order, hide it temporarily or lock it against edits.
Ctrl-click adds or removes items from a selection; dragging over empty canvas selects an area.
Move or resize a selection together, and use Undo/Redo to compare changes.
Unlock a layer before changing its position, size, properties or order.

To mask artwork, select a shape, image or freehand layer and choose **Add mask**.
A mask shape is linked to that target. Reordering layers keeps this link; the target can also be changed in the mask properties.
A non-line shape can be made into a mask. Images can be targets, but are not mask shapes in this editor.

| Mode | Effect |
| --- | --- |
| Alpha | The mask's opacity controls how much of the target remains visible. |
| Inverse Alpha | Reverses the alpha result. |
| Luminance | Bright, opaque mask areas reveal more; dark or transparent areas reveal less. |
| Inverse Luminance | Reverses the luminance result. |

### Preview your skin

**Canvas** is for arranging artwork. **Real Preview** shows the generated skin on interactive sample components.
The built-in preview is the easiest starting point and does not need a main-app source checkout.
Preferences also allow a local KnotJot `index.html` to be used as the preview page when that file is available.
Connecting for synchronization and choosing a preview page are separate settings.

If page loading fails, use **Retry**. If a selected local page has moved, choose it again or switch back to the **built-in preview**.
Use **Copy diagnostic information** when reporting a preview failure. If another app is using the clipboard, the editor keeps your project open and asks you to try copying again after the clipboard is available.
Both browser-preview sources need WebView2; changing sources does not install that runtime.
The preview is a place to test appearance, not a substitute for checking the active skin in KnotJot.

### Save, sync and share

| Action | What it does |
| --- | --- |
| Save / Save As | Writes the editable `.knotjot-ui` project to your chosen file. |
| Connect | Chooses which KnotJot application and skin library receive future syncs. |
| Sync to KnotJot | Updates the connected skin library; it does not save your standalone project file. |

Save and sync are independent. After an edit, do both when you want an up-to-date project backup and an up-to-date skin in KnotJot.
Repeated syncs of the same project update its existing skin. A running KnotJot can refresh an active skin without restarting.
UI skins and text-box styles have separate libraries: use **KnotJot Text Style Editor** for a reusable style applied to individual text boxes.

Share the `.knotjot-ui` file when someone needs to continue editing your design.
Imported images are embedded in the project, so image-heavy projects can be larger.
Legacy `.bmapui` projects remain readable. Open an older project and use **Save As** to keep the original alongside the new copy.

### Common questions

**My preview is blank or shows an error.** Check that WebView2 Runtime is available, retry the page, or return to the built-in preview if a custom page is missing. Keep working in Canvas and save your project while resolving the preview issue.

**My text is hidden or cannot be edited as expected.** Check the text region, its placement and its text-source setting. Test a longer label and avoid covering the intended text area with decoration.

**I cannot move or edit a layer.** Check its lock state. Also confirm that you have selected the intended component and layer.

**KnotJot has not changed after Save.** Save only writes the project. Connect to the intended KnotJot, sync, and select the corresponding UI skin in the main app.

**Saving or syncing is rejected.** Read the reported field or file error. Reduce oversized imported images where necessary. Preserve a backup of an unreadable library instead of replacing it with an empty file.

### Development and license

For source builds, tests and implementation notes, see [DEVELOPMENT.md](DEVELOPMENT.md).
The editor's own code is licensed under **GPL-3.0-only**: see [LICENSE](LICENSE).
Runtime and dependency notices are in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

---

<a id="chinese"></a>

## 中文

### 认识 KnotJot 界面编辑器

按部件设计 KnotJot 的外观，制作简洁工作区、插画风控件或风格统一的界面皮肤。
没有设计的部件继续保持原版外观。

- 设计节点、按钮、工具栏、菜单等界面部件。
- 组合矩形、圆角矩形、椭圆、直线、手绘和导入图片。
- 用文本区安排部件真正的文字，让装饰与文字配合。
- 选择、移动、缩放、排序、隐藏和锁定图层，用蒙版控制显示范围。
- 在浏览器预览中试打字、试拉伸，检查使用效果。
- 独立保存可编辑工程，或连接 KnotJot 同步皮肤。

V1.0.1 完全基于 V1.0.0，包含选择、旋转图稿、蒙版及预览重试等修复，保留既有工程与连接兼容性。
当前界面编辑器的控件使用中文标签。

### 下载与安装

从项目发行文件中选择 Windows x64 安装包：

| 需要安装的内容 | 文件 |
| --- | --- |
| 仅界面编辑器 | `KnotJot-UI-Editor-Setup-1.0.1.exe` |
| KnotJot 及配套编辑器 | `KnotJot-Suite-Setup-1.0.1.exe` |

运行安装包，选择语言和安装目录，然后从开始菜单或桌面快捷方式打开 **KnotJot UI Editor**。
自包含发行版带有 .NET 运行时，使用软件不需要安装 .NET SDK。
浏览器预览需要 **Microsoft Edge WebView2 Runtime**；预览不可用时，仍可在画布中设计并保存工程。

### 制作第一款皮肤

1. 点击“新建”，为皮肤起一个容易辨认的名称。
2. 在左侧选择一个部件，例如普通节点。先只设计一个部件，方便比较效果。
3. 在设计框中添加圆角矩形或图片，通过右侧属性调整填充、描边和尺寸。
4. 选中或添加该部件的“文本区”，为真正的文字留出空间，并尝试较长的示例。
5. 切换到“真实预览”，测试示例部件；返回“画布”继续调整图层、文本区和蒙版。
6. 点击“保存 .knotjot-ui”，把可编辑工程保存到自己选择的文件夹。
7. 需要在 KnotJot 中使用时，点击“连接主程序…”，选择主程序或快捷方式，再点“同步到 KnotJot”。若未启用，在主程序的 UI 皮肤选项中选择该皮肤。

继续设计其他部件即可；切换部件时，各部件的设计分别保存在同一个工程中。

### 文本区为什么重要

文本区规定部件真正的文字放在哪里、如何对齐，它不是装饰图形，也不是另一份思维导图。
带文字的部件可以选中已有文本区，或用“文本区”工具添加；每个部件只有一个，再次点击工具会选中已有区域。
移动、缩放文本区后，在属性中调整示例、字体和对齐。
需要保留 KnotJot 标签或可编辑内容时，使用正常文字来源；高级固定文字适合有意制作的装饰，应用前应检查效果。

### 图层与蒙版

通过图层列表选中图稿、调整顺序、临时隐藏或锁定。按住 Ctrl 点击可增减选择，在空白画布拖动可框选。
多个选中图层可以一起移动或缩放；用撤销、重做比较效果。修改位置、尺寸、属性或层级前，先解锁图层。

选中图形、图片或手绘后点击“加遮罩”，新蒙版会绑定该目标；调整图层顺序不会改变绑定，也可在蒙版属性中更换目标。
非线条图形可以作为蒙版；图片可以被遮罩，但不能在本编辑器中充当蒙版形状。

| 模式 | 效果 |
| --- | --- |
| Alpha | 按蒙版的不透明度控制目标显示程度。 |
| 反向 Alpha | 将 Alpha 结果反转。 |
| 亮度 | 明亮且不透明的部分显示更多，暗色或透明部分显示更少。 |
| 反向亮度 | 将亮度结果反转。 |

### 检查预览

“画布”用于编辑图稿，“真实预览”把生成的皮肤应用到可交互示例部件。
初次使用建议保留内置预览，不需要主程序源码；拥有本地 KnotJot `index.html` 时，也可在偏好中指定该页面。
同步连接与预览页面来源是两项独立设置。

页面加载失败时点击“重试”。指定页面被移动后，重新选择文件或“切回内置预览”。
反馈预览失败时可点击“复制诊断信息”。如果其他程序正在占用剪贴板，编辑器会保留当前工程并提示稍后重试复制。
两种浏览器预览都需要 WebView2，切换来源不会安装运行时。预览用于试验外观，启用皮肤后仍应在主程序中检查实际效果。

### 保存、同步与共享

| 操作 | 作用 |
| --- | --- |
| 保存／另存为 | 将可编辑的 `.knotjot-ui` 工程写入所选文件。 |
| 连接主程序 | 选择后续同步使用的 KnotJot 与皮肤库。 |
| 同步到 KnotJot | 更新连接的皮肤库，不会保存独立工程文件。 |

保存与同步相互独立。编辑后希望工程备份与主程序皮肤都更新，需要分别执行两项操作。
同一工程重复同步会更新已有皮肤；运行中的 KnotJot 可以刷新当前皮肤，无需重启。
UI 皮肤与文本框样式使用不同的库；制作应用到单个文本框的样式，请使用 **KnotJot Text Style Editor**。

分享 `.knotjot-ui` 文件即可让对方继续编辑。导入图片会嵌入工程，图片较多时文件会更大。
仍可打开旧 `.bmapui` 工程；建议用“另存为”保留新副本，同时保存旧文件。

### 常见问题

**预览空白或报错。** 检查 WebView2 Runtime，重试页面；自定义页面丢失时切回内置预览。排查期间仍可在画布设计并保存。

**文字被挡住或不能按预期编辑。** 检查文本区的位置和文字来源，尝试较长标签，并避免用装饰覆盖预期文字区域。

**图层不能移动或修改。** 检查锁定状态，并确认当前部件及选中图层正确。

**保存后主程序没有变化。** 保存只写入工程；请连接正确的 KnotJot，执行同步，并在主程序选择对应的 UI 皮肤。

**保存或同步被拒绝。** 按提示检查属性或文件错误，必要时缩小过大的导入图片。库文件无法读取时先保留备份，不要用空文件覆盖。

### 开发与许可证

源码构建、测试及实现说明见 [DEVELOPMENT.md](DEVELOPMENT.md)。
编辑器自有代码采用 **GPL-3.0-only**，完整文本见 [LICENSE](LICENSE)。
运行时及依赖声明见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
