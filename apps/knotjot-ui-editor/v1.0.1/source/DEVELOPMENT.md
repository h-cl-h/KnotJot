# KnotJot UI Editor V1.0.1 development

User workflows are documented in [README.md](README.md). This document covers the maintained Windows source, build inputs and regression entry points.
用户操作见 [README.md](README.md)，本文集中说明维护中的 Windows 源码、构建输入和回归入口。

## Build prerequisites / 构建条件

Use Windows and a .NET SDK that supports `net8.0-windows` and WPF. The project targets .NET 8; the maintained source has also been built with SDK 10.0.301. Restore obtains the exact WebView2 package version declared in [KnotJot.UiEditor.csproj](KnotJot.UiEditor.csproj). The installed WebView2 Runtime is separately required when exercising browser previews. NSIS 3 is required only to build the installer.
使用 Windows 和支持 `net8.0-windows`、WPF 的 .NET SDK。项目目标为 .NET 8，维护源码也曾使用 SDK 10.0.301 构建。还原依赖时按工程声明获取固定版本的 WebView2 包；运行浏览器预览另需已安装的 WebView2 Runtime。仅生成安装包时需要 NSIS 3。

Run the following commands from `apps/knotjot-ui-editor/v1.0.1/source`. From a source archive opened directly at this directory, no additional path prefix is needed.
以下命令在 `apps/knotjot-ui-editor/v1.0.1/source` 执行；源码归档直接打开在本目录时，无需额外路径前缀。

```powershell
dotnet restore KnotJot.UiEditor.csproj
dotnet build KnotJot.UiEditor.csproj -c Release
pwsh -NoProfile -File ./tests/BrandingSmoke.ps1
```

The branding check only reads local sources; it does not launch the application or installer. Use PowerShell 7 for UTF-8 source compatibility.
品牌检查只读取本地源码，不启动应用或安装器；使用 PowerShell 7 读取 UTF-8 源码。

## Publish and package / 发布与打包

The current [installer.nsi](installer.nsi) recursively includes `bin/Release/net8.0-windows/win-x64/publish/*` and writes its setup file to `../installers`. Publish to that exact directory before invoking NSIS; the script does not accept a `PUBLISH_DIR` override. These commands build a package and do not install it.
当前安装脚本递归包含上述发布目录，并把安装包输出到 `../installers`。调用 NSIS 前应发布到该精确目录；脚本不支持 `PUBLISH_DIR` 覆盖。以下命令生成安装包，不执行安装。

```powershell
dotnet publish KnotJot.UiEditor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
New-Item -ItemType Directory -Force ../installers | Out-Null
makensis installer.nsi
```

The output is `../installers/KnotJot-UI-Editor-Setup-1.0.1.exe`. Retain preceding release files before intentionally rebuilding that path. Verify the published runtime, user documentation, protocol and license files before distribution. Keep the current README and its linked developer/license documents together in the release payload.
输出为上述 V1.0.1 安装包；有意重建该路径前保留之前发行文件。分发前检查运行文件、用户说明、协议及许可证，并将当前 README 和所链接的开发、许可文档一起提供。

Do not rename the assembly `KnotJot界面编辑器`, namespace `KnotJotUiEditor`, installed executable, project-format identifiers or legacy connection aliases. The English display name and project filename do not change those compatibility contracts.
不得因英文显示名或规范工程名而更改程序集、命名空间、已安装可执行文件、工程格式标识或旧连接别名，这些均为兼容约定。

## Module map / 模块职责

| Source | Purpose / 职责 |
| --- | --- |
| `MainWindow.xaml` and `MainWindow.xaml.cs` | Window commands, document state, file dialogs, preview lifecycle and synchronization / 窗口命令、文档状态、文件对话框、预览生命周期及同步 |
| `MainWindowEditor.cs` | Canvas gestures, layer selection/order/locks, images, ink and raster mask preview / 画布手势、图层选择层级锁定、图片、手绘及蒙版预览 |
| `MainWindowDesign.cs` | Per-target design snapshots and text-region editing / 按目标保存设计及文本区编辑 |
| `DesignTargets.cs` and `Elements.cs` | Target catalog, element geometry and stable element/mask identities / 目标目录、几何及稳定元素蒙版身份 |
| `CssBuilder.cs` | Target-scoped CSS and SVG with functional text placement / 分目标 CSS、SVG 及保留功能的文字布局 |
| `ModelSafety.cs`, `ProjectFileService.cs`, `LegacyProjectMigrator.cs` | Normalization, bounded project IO and legacy migration / 规范化、有界工程读写及旧格式迁移 |
| `AtomicFile.cs` | Complete temporary write followed by same-directory replacement / 完整临时写入后在同目录替换 |
| `KnotJotConnection.cs`, `UiSkinProtocol.cs` | Main-app path handshake, library validation and synchronized skin updates / 主程序路径握手、库校验及皮肤同步 |
| `PreviewService.cs`, `DirtyGuard.cs` | Preview dimming/diagnostics and shared save-discard-cancel handling / 预览弱化诊断及统一保存放弃取消处理 |

## Editing and rendering contracts / 编辑及渲染约定

Element order is bottom to top; the layer list displays the reverse order. Multi-layer moves preserve relative order. Locked layers remain selectable for inspection but are excluded from edits. Rotation is applied consistently to artwork, selection geometry and handles; group resizing preserves proportions where the stored geometry cannot represent shear.
元素按底至顶存储，图层列表反向显示；多层移动保留相对顺序。锁定层仍可选中查看，但排除编辑。图稿、选择几何和手柄一致处理旋转，模型无法表达错切时采用保持比例的组合缩放。

A mask is a non-line shape with a stable target ID. Alpha, luminance and inverse modes sample transformed fill/stroke pixels in common canvas coordinates; image targets retain transparency. Reordering does not retarget a mask. Adjacent-layer recovery exists only for older files without stable references.
蒙版为具有稳定目标 ID 的非线条图形。透明度、亮度及反向模式在公共画布坐标采样变换后的填充和描边，图片目标保留透明度；重排不改变目标，相邻层恢复仅用于缺少稳定引用的旧文件。

Text regions place the real component text. Advanced fixed text is an explicit export choice. Per-component designs and export options belong to the standalone project, so source changes must preserve save/open equivalence.
文本区安排部件真正的文字，高级固定文字属于显式导出选项。分部件设计及导出设置属于独立工程，修改时必须保留保存打开的一致性。

## Persistence and connection / 保存及连接

Standalone Save and dynamic Sync are separate commands. A `.knotjot-ui` project retains editable design data; a synchronized library record contains generated CSS and editable project data under a stable skin ID. Repeated syncs update that ID. The actual main-app handshake determines the library location; the preview-page preference is independent of this connection.
独立保存与动态同步是两个命令。独立工程保留可编辑设计，库记录同时包含生成 CSS、可编辑数据及稳定皮肤 ID，重复同步更新同一 ID。库位置来自真实主程序握手，与预览页面偏好相互独立。

Validate the exact serialized candidate before replacing a project or merging a library. Treat [protocols/ui-skin-protocol.json](protocols/ui-skin-protocol.json) and production validation as authoritative for limits. Invalid existing libraries must remain recoverable. Readable legacy `.bmapui` inputs and migration routes must remain supported; see [FORMAT.md](FORMAT.md) and [MIGRATION.md](MIGRATION.md) for the retained format background.
替换工程或合并库前校验精确序列化候选，限制以随源码协议及生产校验为准；无效旧库应保持可恢复。保留旧 `.bmapui` 输入和迁移入口，格式背景见上述文档。

## Preview lifecycle / 预览生命周期

WebView2 can render the built-in sample document or a selected local KnotJot page. Initialization registers callbacks once even when first callers overlap. A mapped page is reusable only after its current navigation succeeds. Navigation failures invalidate that state; explicit Retry starts a fresh navigation, while normal edits to an already loaded page inject CSS without resetting its DOM. Stale navigation/injection completions cannot replace newer state.
WebView2 渲染内置示例或指定的本地主程序页面；并发首次初始化只注册一次回调。当前导航成功后才复用映射页，失败使其失效，显式重试重新导航，已加载页面的普通编辑只注入 CSS 而不重置 DOM；旧异步完成不得覆盖新状态。

Switching preview source retires previous callbacks. Browser-runtime absence is separate from a navigation error; a navigation regression test does not prove the missing-runtime case.
切换来源时使旧回调失效；缺失浏览器运行时与页面导航错误不同，导航回归不等于缺失运行时测试。

## Focused verification / 针对性验证

Run Windows/WPF tests in an isolated development environment. They instantiate production controls and may use browser profiles or temporary files; they are not a full manual installation or input-device acceptance test. Do not point test connections at a working user's application or library.
在隔离开发环境运行 Windows/WPF 测试。测试实例化生产控件，可能使用浏览器配置及临时文件，不代表完整安装或输入设备验收；测试连接不要指向用户工作中的应用和库。

```powershell
dotnet run --project tests/CoreSmoke/CoreSmoke.csproj -c Release
dotnet run --project tests/PreviewRetrySmoke/PreviewRetrySmoke.csproj -c Release -- .test-output/preview-retry
```

CoreSmoke covers model safety, migration, masks, atomic persistence and synchronization contracts. PreviewRetrySmoke uses an offscreen host and isolated WebView2 profile to cover concurrent initialization, repeated navigation failure/retry, preserved DOM on CSS refresh, built-in recovery and obsolete asynchronous callbacks. Use a new evidence directory for each run.
CoreSmoke 覆盖模型安全、迁移、蒙版、原子保存及同步协议；PreviewRetrySmoke 使用屏幕外宿主及隔离 WebView2 配置，覆盖并发初始化、连续失败重试、普通 CSS 刷新保留 DOM、内置恢复及过期回调。每次使用新的证据目录。

`tests/ApprovedSmoke` provides additional production-handler and pixel regressions. Its save/open tests should run through the PreviewRetrySmoke wrapper, which redirects preference/recent-file paths before invoking the suite:
ApprovedSmoke 提供其他生产处理函数及像素回归；保存打开测试应通过 PreviewRetrySmoke 包装入口运行，先重定向偏好及最近文件路径：

```powershell
dotnet build tests/ApprovedSmoke/ApprovedSmoke.csproj -c Release
dotnet run --project tests/PreviewRetrySmoke/PreviewRetrySmoke.csproj -c Release -- --approved tests/ApprovedSmoke/bin/Release/net8.0-windows/ApprovedSmoke.dll .test-output/approved
```

## Documentation and licensing / 文档与许可

Keep user READMEs as a complete English edition followed by a complete Chinese edition. Keep implementation details here, and explain changed code sections in English followed immediately by Chinese. Preserve historical versions and original hashes when preparing a new delivery.
用户 README 先提供完整英文，再提供完整中文；实现细节集中在开发文档，代码修改区块先英文紧接中文说明。准备新交付时保留历史版本及原始哈希。

Review [LICENSE](LICENSE), [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and the retained third-party license directory before redistribution.
再分发前检查项目许可、第三方声明及许可目录。
