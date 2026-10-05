# KnotJot Text Style Editor V1.0.1 development

User workflows are in [README.md](README.md). This document covers building, persistence contracts, rendering and focused checks for the maintained source.
用户操作见 [README.md](README.md)，本文集中说明维护源码的构建、保存约定、渲染及针对性检查。

## Build prerequisites / 构建条件

Use Windows with a .NET SDK supporting .NET 8 WPF. The project targets `net8.0-windows`; SDK 10.0.301 has also been used to build this source. It has no third-party NuGet package references. Microsoft Edge is a separate installed dependency for browser-rendered CSS frame previews; the editor can design and save without it. NSIS 3 is needed to create the installer.
使用 Windows 和支持 .NET 8 WPF 的 SDK。目标框架为 `net8.0-windows`，该源码也曾使用 SDK 10.0.301 构建，无第三方 NuGet 包引用。CSS 外框预览单独依赖已安装 Microsoft Edge，缺失时仍可设计保存。生成安装包需要 NSIS 3。

Run commands from `apps/knotjot-text-style-editor/v1.0.1/source`. The project is [KnotJot.TextStyleEditor.csproj](KnotJot.TextStyleEditor.csproj).
命令在上述源码目录执行，工程文件见链接。

```powershell
dotnet restore KnotJot.TextStyleEditor.csproj
dotnet build KnotJot.TextStyleEditor.csproj -c Release
pwsh -NoProfile -File ./tests/BrandingSmoke.ps1
```

BrandingSmoke only reads source files and does not launch the editor or installer. PowerShell 7 is the documented runtime for UTF-8 source checks.
品牌检查只读取源码，不启动编辑器或安装器；UTF-8 源码检查使用 PowerShell 7。

## Publish and package / 发布与打包

The current [installer.nsi](installer.nsi) consumes the complete `bin/Release/net8.0-windows/win-x64/publish/*` tree recursively. Its output is fixed at `../installers/KnotJot-Text-Style-Editor-Setup-1.0.1.exe`. The historical `PUBLISH_DIR` and `INSTALLER_OUTPUT` override instructions do not apply to this script.
当前安装脚本递归包含完整的上述发布目录，输出固定为版本目录下的 V1.0.1 安装包。历史 `PUBLISH_DIR` 和 `INSTALLER_OUTPUT` 覆盖说明不适用于当前脚本。

```powershell
dotnet publish KnotJot.TextStyleEditor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
New-Item -ItemType Directory -Force ../installers | Out-Null
makensis installer.nsi
```

These commands build an installer without running it. Preserve earlier release bytes before intentionally rebuilding the same output. Verify that the runtime, current user README, linked DEVELOPMENT document, project license and third-party notices are present in the distribution; NSIS adds the license directory from source as well.
以上命令只构建安装包，不运行安装。重建同一输出前保留旧发行字节；分发前检查运行文件、当前 README、所链接 DEVELOPMENT、项目许可和第三方声明，NSIS 还会从源码加入许可目录。

Keep the assembly `KnotJot文本框样式编辑器`, namespace `KnotJotTextStyleEditor`, assembly version, executable name, style-library markers and legacy extension/handshake aliases stable. The V1.0.1 display and file versions do not authorize changing these compatibility identities.
保持程序集、命名空间、程序集版本、可执行文件、样式库标记及旧后缀握手别名稳定；V1.0.1 显示与文件版本不意味着可更改兼容身份。

## Module map / 模块职责

| Source | Responsibility / 职责 |
| --- | --- |
| `MainWindow.xaml` | Home, command bar, drawing tools, Design/Preview tabs, layers and properties / 开始页、命令栏、绘图工具、设计预览页、图层和属性 |
| `MainWindow.xaml.cs` | Gestures, selection/locks, property history, validation, rendering orchestration, file operations and sync / 手势、选择锁定、属性历史、校验、渲染调度、文件及同步 |
| `Models.cs` | Style/layer/text-region model, normalized geometry, input rules, images and sizing / 样式图层文字区域模型、规范几何、输入规则、图片和尺寸 |
| `LayerCompositor.cs` | Shared design/preview alpha composition, clipping, masks and blend modes / 设计预览共用透明度合成、剪切、蒙版及混合 |
| `FrameShadow.cs`, `FrameBrowserJob.cs` | Isolated Edge rendering and owned browser-process cleanup / 隔离 Edge 渲染及自有浏览器进程清理 |
| `StyleLibraryStore.cs` | Validated library loading, revisions, corrupt backups and atomic writes / 库读取校验、版本、损坏备份及原子写入 |
| `StyleFileExtensions.cs` | Standalone save/open filters and legacy formats / 独立文件筛选及旧格式 |
| `StyleLibraryLocation.cs`, `KnotJotConnection.cs` | Source-layout resolution and typed main-executable path replies / 源码布局定位及主程序路径响应 |
| `App.xaml.cs` | Application startup, exception handling and diagnostic log / 应用启动、异常处理及日志 |

## Editing and history / 编辑及历史

The style contains decorative layers and a single text region. Save and sync require that region to be visible with positive dimensions. Geometry is stored in relative coordinates. Numeric/color fields and regular expressions are validated before persistence; library IDs must remain unique and null layer records are rejected.
样式包含装饰图层和唯一文字区域，保存同步要求该区域可见且尺寸为正；几何按相对坐标保存。持久化前校验数值、颜色及正则表达式，库内 ID 必须唯一，并拒绝空图层记录。

Locks cover properties, effects, transforms, deletion, order and grouped edits, including the text region. Line translations update both endpoints and bounds. Single-step ordering and top/bottom ordering use distinct commands.
锁定涵盖属性、效果、变换、删除、排序及组合操作，也适用于文字区域；直线平移同时更新端点和边界，单步层级与置顶置底使用不同命令。

Continuous property edits are coalesced into history. Focus changes and persistence/undo command boundaries flush pending edits. Saved and synced state each compare against their own successful snapshot; neither is inferred from the other.
连续属性输入合并为历史步骤，在焦点变化及保存、同步、撤销等命令边界提交；已保存和已同步分别比较各自成功快照，不能相互推断。

## Composition and frame preview / 合成及外框预览

Decorative layers render in bottom-to-top order. An effect layer acts on the adjacent lower layer and is not painted as ordinary artwork. Clip modes use geometry; alpha/luminance masks sample rendered opacity, image transparency and transformed pixels. Inverse modes complement the mask. The design and independent preview use the same compositor for normal, multiply, screen and overlay blending.
装饰层按底至顶渲染，效果层作用于紧邻下方图层，自身不作为普通图稿绘制。剪切使用几何，透明度及亮度蒙版采样图层不透明度、图片透明度及变换后像素，反向模式取补；设计与独立预览共用四种混合模式合成器。

The CSS frame is rendered below editable WPF content by an isolated headless Edge process. It starts only for the selected Preview tab. Browser syntax handles inner/outer shadows, offsets, blur, spread and multiple layers. A composite replacement frame suppresses the ordinary frame shadow.
CSS 外框由隔离无头 Edge 渲染在可编辑 WPF 内容下方，仅选中预览页时启动；浏览器解析内外阴影、偏移、模糊、扩展及多层语法。组合替换外框时隐藏普通阴影。

Requests are cancelled when superseded, hidden or closed. An owned Windows process job contains child processes; cleanup removes the unique temporary profile. The normal browser sandbox remains enabled and page network requests are blocked. Output images are bounded and may be downsampled, while original model geometry is preserved.
请求被替代、隐藏或关闭时取消；Windows 自有进程作业涵盖子进程，清理时移除唯一临时配置。保留正常浏览器沙箱并阻止页面网络请求；输出图像有界，必要时降采样，原始模型几何保持不变。

Preview uses a default outer-card inheritance context, not the current main-app theme. Relative units and inherited colors can therefore differ in a running main app. Bitmap allocation limits do not represent a bound on total browser memory. Distinguish missing Edge, failed rendering and input validation in diagnostics and tests.
预览使用默认外框继承环境，不读取主程序当前主题，因此相对单位和继承颜色可能不同；位图分配限制不代表浏览器总内存上限。诊断与测试应区分缺失 Edge、渲染失败和输入校验。

## Files and synchronization / 文件及同步

Save writes a `.knotjot-textstyle` library containing the current editable entry and preserves other entries in an opened multi-style file. Legacy `.bmaptextstyle` and supported JSON inputs remain readable. Images are embedded; installed fonts are referenced, not redistributed.
保存将当前可编辑条目写入独立样式库，并保留已打开多样式文件的其他条目。旧后缀及受支持 JSON 仍可读取，图片嵌入文件，字体只引用本地安装而不再分发。

Save & sync first saves an associated standalone file and then writes the connected library. With no associated file it writes only the library, retaining the unsaved standalone state. If both destinations are the same file, replacement belongs to the opened entry index, with its original revision protected. Reject a conflicting sibling ID before mutation.
保存并同步先写已关联独立文件，再写连接的库；未关联文件时仅写库，并保留独立文件未保存状态。两目标相同时，按打开的条目索引替换并保护原版本；修改前拒绝其他条目的 ID 冲突。

Each successful destination updates only its own saved/synced baseline. A later library failure does not undo an earlier successful standalone save or falsely mark the library synchronized. Preserve corrupt backups and require explicit confirmation before rebuilding an unreadable library.
各目标成功后仅更新自己的状态基准；后续库写入失败不会撤销已成功独立保存，也不能误标为已同步。保留损坏备份，重建不可读库前要求明确确认。

Executable connections query the main app for its actual style-library path and validate the reply format, numeric version and absolute path. Source connections share `StyleLibraryLocation`: explicit `KNOTJOT_TEXT_STYLES_DIR`, refactored `resources/text-styles`, then existing legacy sibling layout. Reply-file retries handle only transient Windows sharing/lock errors within the original deadline.
EXE 连接查询并校验真实库路径响应；源码连接共用定位规则，依次采用明确环境覆盖、重构资源目录及已有旧同级目录。读取响应仅在原期限内重试临时共享锁错误。

## Tests / 测试

Run WPF and connection suites in an isolated Windows development environment. They instantiate real controls and can create test files, browser processes and connection fixtures. Do not pass a working user's executable/profile to a test; GUI suites are separate from the read-only branding check.
在隔离 Windows 开发环境运行 WPF 和连接测试；测试会实例化真实控件，可能创建样本文件、浏览器进程及连接夹具，不要传入用户工作中的程序或配置。GUI 套件与只读品牌检查分开。

```powershell
dotnet run --project tests/ModelSafetySmoke/ModelSafetySmoke.csproj -c Release
dotnet run --project tests/EditorHistorySmoke/EditorHistorySmoke.csproj -c Release
dotnet run --project tests/MultiStyleSyncSmoke/MultiStyleSyncSmoke.csproj -c Release
dotnet run --project tests/PreviewRulesSizingSmoke/PreviewRulesSizingSmoke.csproj -c Release
```

`KNOTJOT_KEEP_TEST_FILES=1` retains fixtures in the original suites that support it. Tests requiring an output argument should use a new directory for each run:
支持该环境变量的原测试可保留样本；需要输出参数的测试每次使用新目录：

```powershell
dotnet run --project tests/ApprovedFollowupSmoke/ApprovedFollowupSmoke.csproj -c Release -- .test-output/approved
dotnet run --project tests/FrameLifecycleSmoke/FrameLifecycleSmoke.csproj -c Release -- .test-output/frame-lifecycle
dotnet run --project tests/ConnectionReplySmoke/ConnectionReplySmoke.csproj -c Release -- .test-output/connection-reply
```

ApprovedFollowupSmoke covers behavior and pixels; its optional second argument exercises a real main-app executable and must only target an isolated test environment. FrameLifecycleSmoke covers preview request/process cleanup. ConnectionReplySmoke covers typed replies and real temporary sharing locks. FrameShadowSmoke compares frames against independently generated Electron images; generate its oracle with `tests/FrameShadowSmoke/oracle.cjs` in an Electron test environment, then pass the oracle directory and a separate output directory to the C# suite.
获批续项测试涵盖行为及像素，可选第二参数会启动真实主程序，只能用于隔离测试环境。生命周期测试检查预览请求及进程清理，响应测试检查类型及真实临时共享锁；阴影像素测试使用独立 Electron 图像，应先在 Electron 测试环境生成 oracle，再向 C# 套件传入 oracle 与独立结果目录。

## Documentation and license / 文档与许可

Keep README as a complete English edition followed by a complete Chinese edition, with implementation details here. Explain modified code in English followed immediately by Chinese. Preserve historical source and original hashes when preparing deliveries.
README 先完整英文再完整中文，实现细节集中于本文；代码修改先英文紧接中文说明，交付时保留历史源码和原始哈希。

See [LICENSE](LICENSE), [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and the third-party license directory for redistribution terms.
再分发条款见项目许可、第三方声明及许可目录。
