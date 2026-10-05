<div align="center">
  <img src="resources/icons/icon-256.png" width="120" alt="KnotJot">
  <h1>KnotJot V1.0.1</h1>
  <p><b>Turn ideas into maps, plans, and visual notes.</b></p>
</div>

[Releases](https://github.com/h-cl-h/KnotJot/releases) · [Project](https://github.com/h-cl-h/KnotJot)

## English

### Meet KnotJot

KnotJot is a free, open-source mind-mapping app for Windows. Collect ideas, organize projects, and keep visual notes in a workspace you can make your own. Core editing works locally without an account. Save maps as `.knotjot` files wherever you choose; existing `.bmap` maps can still be opened.

V1.0.1 builds on V1.0.0, improving everyday editing, per-sheet backgrounds, images, layouts, and the two companion editors.

### Download and install

Choose a **Windows x64** download from the [Releases page](https://github.com/h-cl-h/KnotJot/releases):

| Download | Choose it when… |
|---|---|
| `KnotJot-Suite-Setup-1.0.1.exe` | You want KnotJot and the optional UI and text-style editors in one installer. |
| `KnotJot-Setup-1.0.1.exe` | You want to install only the mind-mapping app. |
| `KnotJot-1.0.1-Portable.exe` | You want to run the app without an installation wizard. |

The installers offer English and Simplified Chinese. After installation, open **KnotJot** from the desktop or Start menu. The portable app may still save preferences on this computer; keep your map files separately when moving between computers.

If Windows shows an unknown-publisher prompt, confirm the download came from the project and compare its SHA-256 with the release's `SHA256SUMS.txt`. Keep a copy of existing maps before trying a new version.

### Your first map

1. Create a new map from the start screen, or open a `.knotjot` or `.bmap` file.
2. Double-click an empty area of the canvas to add a topic, then enter your text.
3. Select a topic and press **Tab**, or use its menu to add a child topic.
4. Drag topics into place. Select several topics to move them together, or use **Smart Tidy** for an automatic arrangement.
5. Use the bottom tabs to add another sheet. Choose **Background** beside the tabs to change the current sheet's color or image.
6. Press **Ctrl+S** and choose where to save your map. Use the File menu to export it for sharing.

Shortcuts can be changed in Settings. Your installation's menu and shortcut list show the active bindings.

### Organize ideas your way

- **Pick a structure:** brace maps, spider-web maps, logic charts, org charts, trees, timelines, fishbones, matrices, and tree tables. Switch a whole map or a branch as your ideas develop.
- **Use several sheets:** keep related maps in one file, rename sheets, or open a branch in a separate sheet.
- **Work in the outline:** edit the hierarchy, rearrange parents and children, collapse branches, and update progress in a linear view.
- **Add context:** attach markers, notes, tags, links, images, dates, assignees, and progress.
- **Plan tasks:** use to-dos and Gantt view. Completing a parent to-do can complete descendant to-dos too; turn this off in **Settings → General** to track them independently.
- **Copy and arrange:** topic menus include Copy and Cut, alongside **Ctrl+C / Ctrl+X** and **Ctrl+V**. Callouts and boundary titles can be edited, moved, and deleted.

### Backgrounds, images, and appearance

Each sheet can have its own background. Use **Background** beside the sheet tabs, including when a custom UI skin is active. Preview zoom scales the reference topic together with the background so you can judge their proportions.

Choose an unlimited canvas with a tiled image, or a limited canvas with adjustable bounds. A newly imported background image fits the limited canvas automatically; its scale and position remain adjustable. PNG, JPEG, and WebP backgrounds are supported.

Images inserted into topics keep their original image data. Resizing changes their display size. Large embedded images can make map files larger.

Use **Settings → Appearance** for the original UI palette and default appearance. Custom skins have their own visual design. **View → Fit All Content** brings topics, images, callouts, boundaries, and a limited canvas into view.

### Make your own styles

**KnotJot UI Editor** designs the workspace skin: topics, menus, toolbars, and other UI components. **KnotJot Text Style Editor** designs individual text-box styles, including the frame and editable text region.

Connect an editor to KnotJot, sync the design to its library, and select that skin or text style in KnotJot. Changes to the selected design can refresh in the running app. The two editors use separate libraries. Save an editable project file in the editor as well when you want to keep or share the design.

### Saving and recovery

Your map is saved to the location you choose. Sheets and their backgrounds stay in the same map file. Exported pictures or PDFs are useful for sharing; keep the `.knotjot` original for later editing.

**Settings → General** includes automatic recovery copies and saved undo history. After an interrupted session, KnotJot can offer available unsaved recovery copies at startup. Review the recovered copy before saving it; the original file is not automatically replaced. Recovery copies complement regular saves and backups.

### Common questions

**Something is outside the view.** Use **View → Fit All Content** and check the selected sheet tab.

**A locked topic will not move or change.** Unlock it from its menu. Locking protects it without changing its normal appearance.

**Original UI colors do not change a custom skin.** Select the original UI to use its palette controls, or edit the skin in UI Editor. The sheet background is adjustable separately.

**Where is Add Summary?** New summary creation has been removed. Existing summaries remain readable. Use a callout or boundary for new annotations.

**AI cannot connect.** AI is optional and needs a compatible service, model, and any credentials required by that service. Check those settings and the provider's availability. If the service reports an expired or invalid key, replace it in AI settings; retrying cannot renew a key. For a timeout, check the connection or wait for a local model to finish loading. Normal map editing remains available without AI; compatibility with every external service is not guaranteed.

**A map opens in an older app.** In Windows, right-click the file, choose **Open with**, and select **KnotJot**. An existing default-app choice can remain after an upgrade, including for older `.bmap` files.

### Data, help, and license

Core editing is local. AI requests go to your configured endpoint when you use AI; choose the provider and shared content carefully. Keep API keys out of shared examples and bug reports.

Report reproducible problems on [Issues](https://github.com/h-cl-h/KnotJot/issues), including the app version and steps. Remove personal content from sample files.

KnotJot is licensed under **GPL-3.0-only**. See [LICENSE](LICENSE) and [third-party notices](THIRD_PARTY_NOTICES.md). Building and contributing are covered in [DEVELOPMENT.md](DEVELOPMENT.md).

---

## 中文

### 认识 KnotJot

KnotJot 是一款免费、开源的 Windows 思维导图工具。你可以用它收集想法、梳理项目，或制作可视化笔记，并按自己的习惯调整工作区。核心编辑在本地完成，不需要账号。导图以 `.knotjot` 文件保存在你选择的位置，已有的 `.bmap` 导图仍可打开。

V1.0.1 基于 V1.0.0 改进了日常编辑、逐画布背景、图片、布局及两个配套编辑器。

### 下载与安装

在[发行页面](https://github.com/h-cl-h/KnotJot/releases)选择适合你的 **Windows x64** 文件：

| 下载文件 | 适用情况 |
|---|---|
| `KnotJot-Suite-Setup-1.0.1.exe` | 希望通过一个安装器选择安装主程序、界面编辑器和文本框样式编辑器。 |
| `KnotJot-Setup-1.0.1.exe` | 只需要安装思维导图主程序。 |
| `KnotJot-1.0.1-Portable.exe` | 希望不经过安装向导直接运行主程序。 |

安装器提供英文和简体中文。安装后，从桌面或开始菜单打开 **KnotJot**。便携版仍可能在当前电脑保存偏好设置；换电脑使用时，请单独带走需要的导图。

如果 Windows 提示未知发布者，请核实下载来源，并与发行附件中的 `SHA256SUMS.txt` 核对。尝试新版本前，保留一份已有导图的备份。

### 制作第一张导图

1. 从开始页面新建导图，或打开 `.knotjot`、`.bmap` 文件。
2. 双击画布空白处添加主题，然后输入文字。
3. 选中主题后按 **Tab**，或通过主题菜单添加子文本框。
4. 拖动主题调整位置。选中多个主题可以一起移动，也可以使用**智能整理**自动排布。
5. 通过底部标签添加另一张画布。点击标签旁的**背景**，修改当前画布的底色或图片。
6. 按 **Ctrl+S**，选择保存位置。需要分享时，可从文件菜单导出。

快捷键可以在设置中修改；程序菜单和快捷键列表显示当前生效的按键。

### 按自己的方式整理想法

- **选择结构**：大括号图、蜘蛛网图、逻辑图、组织结构图、树状图、时间轴、鱼骨图、矩阵和树形表格，可切换整张导图或单个分支。
- **使用多张画布**：一个文件放多张相关导图，可以改名，也可以把分支展开到另一张画布。
- **通过大纲编辑**：在线性视图中调整层级、改变父子关系、折叠分支和更新进度。
- **补充信息**：添加标记、备注、标签、链接、图片、日期、负责人和进度。
- **安排任务**：使用待办和甘特图。完成父级待办时，可以同步完成后代待办；需要分别管理时，在**设置 → 通用**中关闭这一选项。
- **复制和排版**：主题菜单提供复制、剪切入口，也可用 **Ctrl+C / Ctrl+X**，用 **Ctrl+V** 粘贴；标注和外框标题可以编辑、移动及删除。

### 背景、图片与外观

每张画布都可以有自己的背景。点击底部标签旁的**背景**，启用自定义 UI 时也能使用。预览缩放时，参考文本框会和背景一起缩放，方便判断比例。

图片背景可以选择不限制画布大小并平铺，也可以使用可调整边界的有限画布。向有限画布导入新图片时会自动适配，之后仍可调整图片比例和位置。支持 PNG、JPEG 和 WebP。

插入主题的图片保留原始图片数据，拖动缩放只改变显示大小。嵌入较大的图片会增加导图文件体积。

**设置 → 外观**用于调整原版 UI 配色及默认外观；自定义皮肤有自己的设计。**视图 → 适配全部内容**可以把主题、图片、标注、外框和有限画布带回可见范围。

### 制作自己的样式

**KnotJot UI Editor** 用于设计整个工作区的皮肤，例如主题、菜单、工具栏等部件。**KnotJot Text Style Editor** 用于设计单个文本框样式，包括外框及可输入文字的区域。

将编辑器连接到 KnotJot，把设计同步到主程序的库，然后在 KnotJot 中选择对应皮肤或文本框样式。修改当前选用的设计后，运行中的主程序可以刷新外观。两类设计使用不同的样式库；需要备份或分享设计时，也请在编辑器中保存可编辑的独立工程。

### 保存与恢复

导图保存在你选择的位置，多张画布及各自背景保存在同一个文件中。导出的图片或 PDF 适合分享；之后还要修改时，请保留 `.knotjot` 原文件。

**设置 → 通用**提供自动保存恢复副本和保留撤销历史的选项。会话意外中断后，下次启动时可以选择可用的未保存恢复副本。恢复后可先检查再保存，不会自动替换原文件；恢复副本也不能替代平时的保存与备份。

### 常见问题

**内容好像移出了画面。** 使用**视图 → 适配全部内容**，并检查当前画布标签。

**锁定的主题不能移动或修改。** 先从菜单解锁。锁定用于保护内容，不改变主题平时的外观。

**原版 UI 配色对自定义皮肤没有效果。** 切回原版 UI 使用配色控件，或用界面编辑器修改皮肤；画布背景可单独调整。

**找不到“添加概要”。** 已移除新建概要功能，旧概要仍可读取。新标记内容可使用标注或外框。

**AI 无法连接。** AI 是可选功能，需要兼容服务、模型及服务要求的凭据。请核对设置和服务状态。服务提示密钥过期或无效时，请在 AI 设置中更换密钥，重试不能续期；超时时，请检查连接或等待本地模型加载完成。不使用 AI 也能正常编辑，但不保证与所有外部服务兼容。

**导图被旧软件打开。** 在 Windows 中右键单击文件，选择**打开方式**并选中 **KnotJot**。升级后系统可能保留原来的默认应用，包括旧 `.bmap` 文件。

### 数据、帮助与许可

核心编辑在本地进行。使用 AI 时，请求会发送到配置的服务地址，请自行选择服务商及提交内容。不要把 API Key 放进共享样本或问题报告。

可通过[问题反馈](https://github.com/h-cl-h/KnotJot/issues)提供程序版本及复现步骤；提交示例文件前，先移除私人内容。

KnotJot 采用 **GPL-3.0-only**，见 [LICENSE](LICENSE) 和[第三方声明](THIRD_PARTY_NOTICES.md)。构建或参与开发请阅读 [DEVELOPMENT.md](DEVELOPMENT.md)。
