# KnotJot Text Style Editor V1.0.1

Create reusable text-box styles for KnotJot. Combine a readable text area with shapes, images, borders and shadows, then use the design on your maps.
You can work and save files without connecting to the main application.

[English](#english) | [Chinese](#chinese)

## English

### What you can do

- Draw rectangles, rounded rectangles, ellipses and lines, or import images.
- Arrange decorative layers around one real text-input region.
- Set fonts, colors, borders, corner rounding and CSS frame shadows.
- Move, resize, rotate, reorder, hide and lock layers, with snapping and Undo/Redo.
- Use clipping, alpha or luminance masks, and normal, multiply, screen or overlay blending.
- Try input rules and fixed-aspect or free-stretch sizing in a separate Preview tab.
- Save an editable style file, share it, or add styles to a connected KnotJot library.

V1.0.1 is based on V1.0.0, with fixes to layer editing, masks, shadows, history and synchronization. The editor supports English and Chinese; use the language button to switch.

### Download and install

Choose the Windows x64 file from the project's release distribution:

| You need | File |
| --- | --- |
| Text Style Editor only | `KnotJot-Text-Style-Editor-Setup-1.0.1.exe` |
| KnotJot and its companion editors | `KnotJot-Suite-Setup-1.0.1.exe` |

Run the installer, choose its language and installation folder, then open **KnotJot Text Style Editor** from the Start menu or desktop shortcut. The self-contained release includes .NET; users do not need the .NET SDK.
An installed **Microsoft Edge** is needed to render CSS frame shadows in Preview. The design tools and saving remain available when Edge is unavailable.

### Make your first text-box style

1. Choose **New design**. Enter a recognizable name and a simple style ID, such as `my_style-2`.
2. Add a shape or image for the decoration. Adjust its fill, outline and position in the properties panel.
3. Draw the **text input region** inside the decoration. Keep it visible and give it enough space for real text.
4. Choose the font, text color, alignment and size strategy. Add an input rule only if this style needs one.
5. Open **Preview** and type short and long examples. Check wrapping, input restrictions, resizing and shadows.
6. Choose **Save** to create a `.knotjot-textstyle` file in a folder you control.
7. To use the style in KnotJot, choose **Connect**, select the main executable or shortcut, then **Save & sync**. Select your text boxes in KnotJot and apply the style from its text-box style controls.

Start with a simple frame and text area. Add extra decoration after the basic style is comfortable to read.

### The text input region

Every style needs one visible text-input region with a usable width and height before it can be saved or synced. This region is where the real text box receives text; shapes and images provide its decoration.
The preview sample helps you test the style and is not the content of a particular mind map.

Use the text tool to draw or replace the region, and use its properties to adjust position, size and alignment. Redrawing replaces the existing region rather than creating a second one.
Leave room for longer text and check the result at more than one box size. The font list comes from fonts installed in Windows. Sharing a style does not install its fonts on another computer.

### Layers, clipping and masks

Select a layer in the canvas or layer list. Reorder it, hide it to compare the result, or lock it to protect the design. Locked items must be unlocked before moving, resizing, deleting, reordering or editing their properties.
Use multi-selection and an empty-canvas selection rectangle to move artwork together. Grid, edge and center snapping help align decorations; Undo/Redo lets you compare alternatives.

Clipping and masks work on **adjacent layers**: the effect layer acts on the artwork immediately below it. Keep the pair together when changing their order. The bottom layer has no lower target for these effects.
Use the layer's context menu to choose the effect:

| Effect | Result |
| --- | --- |
| Intersection clip | Keeps the lower artwork inside the cutter's outline. |
| Subtraction clip | Removes the cutter's outline from the lower artwork. |
| Alpha mask | Uses the mask's transparency to control the lower artwork. |
| Luminance mask | Bright, opaque mask areas reveal more than dark or transparent areas. |
| Inverse mask | Reverses the corresponding alpha or luminance result. |

Image transparency, layer opacity and rotation participate in masks. Selection outlines and editing guides are only editing aids; they are not extra borders in the saved style.

### Preview, input and sizing

The **Design** tab is for arranging artwork. **Preview** lets you type into the actual text region and try the sizing rules. Choose a fixed aspect ratio when the artwork should keep its proportions, or free stretch when width and height should adapt separately.
Input rules can limit character count, allowed character types or empty values; advanced rules can use a regular expression. Test both an allowed value and a value that should be rejected before sharing a restricted style.

CSS frame shadows can include inner and outer shadows. Edge renders them when the Preview tab is selected. When a composite design replaces the ordinary outer frame, its ordinary frame shadow is hidden.
Preview uses a default frame environment, so theme-dependent colors or relative sizes can differ in the main app; check the applied style there too. If Edge is unavailable or a render fails, read the status beside the shadow field. You can return to Design and save your work.

### Save, sync and share

| Action | What it does |
| --- | --- |
| Save / Save As | Writes a standalone `.knotjot-textstyle` file to your chosen location. |
| Connect | Chooses the KnotJot application and style library for future syncs. |
| Save & sync, with an associated file | Saves that file, then updates the connected library. |
| Save & sync, without an associated file | Updates the connected library only; a standalone file is still unsaved. |

Use **Save** first if you want a portable backup as well as a style in KnotJot.
Saving and synchronization have separate success states: a file can be saved even if the later library update fails.
Repeated syncs normally update the same style, while the library can hold several different styles.
A running KnotJot can refresh its styles without restarting.

Share the `.knotjot-textstyle` file to let another person open and edit it.
Images are embedded, so image-heavy files can be larger; fonts remain dependent on the recipient's installed fonts.
Legacy `.bmaptextstyle` files and supported style JSON remain readable. Keep an original copy and use **Save As** when updating an older file.
A style file can contain several styles; editing one preserves its other entries.
UI skins belong to **KnotJot UI Editor** and use a separate library from these text-box styles.

### Common questions

**Save or sync asks for a text region.** Draw a visible region with positive width and height. A decorative rectangle alone is not a text-input region.

**A property is highlighted and saving is blocked.** Correct the reported number, color, ratio or input rule. Invalid regular expressions must be fixed before saving or syncing.

**A layer will not move or change.** Check locks, including any locked item in a group selection. Unlock the intended item first.

**Shadows are missing.** Select Preview, check the Edge status and confirm that the ordinary frame has not been replaced by the composite design.

**The style looks different on another computer.** Check installed fonts and the main application's theme and box size. Test the shared file in that environment.

**Sync reports a conflict or damaged library.** Keep your standalone copy. Reopen the latest library after a conflict, and back up an unreadable library before choosing any rebuild operation.

### Development and license

See [DEVELOPMENT.md](DEVELOPMENT.md) for source builds, tests and implementation notes.
The editor's own code is licensed under **GPL-3.0-only**: see [LICENSE](LICENSE).
Runtime and third-party notices are in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

---

<a id="chinese"></a>

## 中文

### 认识 KnotJot 文本框样式编辑器

为 KnotJot 制作可重复使用的文本框样式，将清晰可读的文字区域与图形、图片、边框和阴影组合起来，应用到自己的导图。
无需连接主程序，也能独立设计和保存文件。

- 绘制矩形、圆角矩形、椭圆、直线，或导入图片。
- 围绕一个真正的文字输入区域安排装饰图层。
- 设置字体、颜色、边框、圆角及 CSS 外框阴影。
- 移动、缩放、旋转、排序、隐藏和锁定图层，配合磁吸及撤销重做。
- 使用剪切、透明度或亮度蒙版，以及正常、正片叠底、滤色和叠加混合。
- 在独立预览中测试输入规则、固定长宽比或自由拉伸。
- 保存、分享可编辑样式，或把多个样式加入连接的 KnotJot 样式库。

V1.0.1 完全基于 V1.0.0，修复了图层编辑、蒙版、阴影、历史记录及同步等问题。
编辑器支持中英文，可用语言按钮切换。

### 下载与安装

从项目发行文件中选择 Windows x64 安装包：

| 需要安装的内容 | 文件 |
| --- | --- |
| 仅文本框样式编辑器 | `KnotJot-Text-Style-Editor-Setup-1.0.1.exe` |
| KnotJot 及配套编辑器 | `KnotJot-Suite-Setup-1.0.1.exe` |

运行安装包，选择语言和安装目录，然后从开始菜单或桌面快捷方式打开 **KnotJot Text Style Editor**。
自包含发行版带有 .NET，使用时不需要安装 .NET SDK。
预览中的 CSS 外框阴影需要已安装的 **Microsoft Edge**；Edge 不可用时，仍可使用设计工具并保存文件。

### 制作第一个文本框样式

1. 点击“新建设计”，填写容易辨认的名称和简单样式 ID，例如 `my_style-2`。
2. 添加图形或图片作为装饰，通过属性调整填充、描边和位置。
3. 在装饰内部画出“文字输入区域”，保持可见，并给实际文字留出足够空间。
4. 选择字体、文字颜色、对齐和尺寸策略；确有需要时再添加输入规则。
5. 打开“预览”，输入短句和长句，检查换行、输入限制、缩放及阴影。
6. 点击“保存”，在自己选择的文件夹中创建 `.knotjot-textstyle` 文件。
7. 需要应用到 KnotJot 时，点击“连接主程序”，选择主程序或快捷方式，再点“保存并同步”。在主程序选中文本框，通过文本框样式选项应用该样式。

先制作简单外框与文字区域，确认阅读舒适后再增加装饰。

### 文字输入区域

保存或同步前，样式必须有一个可见且宽高有效的文字输入区域。它负责接收真正的文本，图形与图片负责装饰。
预览示例用于测试样式，不是某份导图的正文。
使用文字工具绘制或替换区域，在属性中调整位置、大小与对齐；重新绘制会替换已有区域，不会创建第二个。
为较长内容留出空间，并测试不同文本框尺寸。字体列表来自 Windows 已安装字体，分享样式不会替对方安装字体。

### 图层、剪切与蒙版

在画布或图层列表选择图层，调整顺序、临时隐藏或锁定。移动、缩放、删除、排序或修改属性前需先解锁。
可以多选或在空白画布框选，让图稿一起移动；网格、边缘和中心磁吸帮助对齐，撤销与重做便于比较方案。

剪切和蒙版作用于**相邻图层**：效果图层影响紧邻其下方的图稿。调整层级时应保持这一对图层相邻，最底层没有下方目标。
在图层右键菜单中选择效果：

| 效果 | 结果 |
| --- | --- |
| 相交剪切 | 仅保留下方图稿与切刀轮廓相交的部分。 |
| 相减剪切 | 从下方图稿中挖掉切刀轮廓覆盖的部分。 |
| Alpha 蒙版 | 按蒙版透明度控制下方图稿的显示。 |
| 亮度蒙版 | 明亮且不透明的部分显示更多，暗色或透明部分显示更少。 |
| 反向蒙版 | 将对应的透明度或亮度结果反转。 |

图片透明度、图层不透明度和旋转都会参与蒙版计算。选中轮廓和编辑辅助线不会成为保存样式的额外边框。

### 预览、输入与尺寸

“设计”用于安排图稿，“预览”用于在真正的文字区域内打字并测试尺寸规则。
希望保持图稿比例时选择固定长宽比；希望宽高分别适应时选择自由拉伸。
输入规则可以限制字符数量、类型和空值，也可使用自定义正则表达式。分享受限样式前，请分别试验应接受和应拒绝的内容。

CSS 外框阴影支持内阴影和外阴影，选中预览页时由 Edge 渲染；组合设计替换普通外框时，普通外框阴影会隐藏。
预览使用默认外框环境，主题相关颜色或相对尺寸可能与主程序不同，应用后还应检查实际效果。
Edge 不可用或渲染失败时，查看阴影字段旁的状态；仍可返回设计页并保存。

### 保存、同步与共享

| 操作 | 作用 |
| --- | --- |
| 保存／另存为 | 把独立 `.knotjot-textstyle` 文件写入选择的位置。 |
| 连接主程序 | 选择后续同步使用的 KnotJot 与样式库。 |
| 已关联文件时“保存并同步” | 先保存该文件，再更新连接的样式库。 |
| 未关联文件时“保存并同步” | 只更新连接的样式库，独立文件仍未保存。 |

希望同时保留可携带备份和主程序样式时，请先执行“保存”。文件保存与同步分别记录成功状态；文件可能已保存，而后续库更新失败。
重复同步通常更新同一样式，库中可以保留多个不同样式；运行中的 KnotJot 可以刷新样式，无需重启。

分享 `.knotjot-textstyle` 文件即可让对方继续编辑。图片嵌入文件，图片较多时文件会更大；字体仍依赖对方电脑的安装情况。
旧 `.bmaptextstyle` 和受支持的样式 JSON 仍可打开；更新旧文件时保留原件并使用“另存为”。
一个文件可以包含多个样式，编辑其中一项会保留其他条目。
UI 皮肤由 **KnotJot UI Editor** 制作，与文本框样式使用不同的库。

### 常见问题

**保存或同步提示缺少文字区域。** 绘制一个可见且宽高为正的文字输入区域，普通装饰矩形不能代替它。

**属性被标红，无法保存。** 按提示修正数字、颜色、比例或输入规则；无效正则表达式需要修正后才能保存与同步。

**图层无法移动或修改。** 检查锁定状态，多选时也检查组内是否有锁定对象，再解锁需要编辑的内容。

**看不到阴影。** 切换到预览，检查 Edge 状态，并确认普通外框没有被组合设计替换。

**换一台电脑外观不同。** 检查已安装字体、主程序主题和文本框尺寸，并在目标环境测试共享文件。

**同步提示冲突或库损坏。** 保留独立文件；遇到冲突后重新打开最新库，库无法读取时先备份，再决定是否重建。

### 开发与许可证

源码构建、测试及实现说明见 [DEVELOPMENT.md](DEVELOPMENT.md)。
编辑器自有代码采用 **GPL-3.0-only**，完整文本见 [LICENSE](LICENSE)。
运行时及第三方声明见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
