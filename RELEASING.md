# Releasing KnotJot V1.0.1

## English

This repository keeps the three current products under `apps/<product>/v1.0.1/source`. The root `shared` directory is a required build input. User guides are the root README and each app's README; each contains the complete English edition before the Chinese edition. Build details belong in DEVELOPMENT documents.

### Build inputs and order

Use Windows x64, Node.js 22.12+ with npm, a .NET SDK able to build .NET 8 WPF, and NSIS 3 with MUI2. Keep the dependency lockfile and license copies. The editors publish self-contained .NET runtime files; users do not need the SDK.

1. In `apps/knotjot/v1.0.1/source`, run `npm ci`, the source checks below, and `npm run dist:win`.
2. In each editor's source directory, publish its project, then run `makensis installer.nsi`:

```powershell
# UI Editor source directory
dotnet publish KnotJot.UiEditor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o bin/Release/net8.0-windows/win-x64/publish
New-Item -ItemType Directory -Force ../installers | Out-Null
makensis installer.nsi

# Text Style Editor source directory
dotnet publish KnotJot.TextStyleEditor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o bin/Release/net8.0-windows/win-x64/publish
New-Item -ItemType Directory -Force ../installers | Out-Null
makensis installer.nsi
```

3. Once all three component setup files exist, run `makensis KnotJotSuite.nsi` in `apps/knotjot/v1.0.1/source/packaging`.

Every product writes to its sibling `installers` directory. The suite reads those exact component filenames. Preserve the layout and executable identities.

### Verify before publishing

From the main app source directory:

```powershell
npm run test:source-layout
node --test tests/branding-release.test.js
npm run test:core
```

From each editor source directory, use PowerShell 7 to run `pwsh -NoProfile -File tests/BrandingSmoke.ps1`. Refer to its DEVELOPMENT guide for additional tests. GUI tests and native installation acceptance need a suitable test environment.

Confirm release versions, source/package agreement, README language order and links, all bundled licenses, and SHA-256 values. Record manual acceptance still outstanding in `RELEASE-STATUS.md`; do not promote source checks to completed native UI tests.

The 2026-10-05 lockfile refresh moves the development-only `http-cache-semantics` dependency to 4.3.0. The npm audit reports no listed vulnerabilities, but a separate shared-cache probe still reproduces the behavior described in [GHSA-ch52-4w7c-c8xp](https://github.com/advisories/GHSA-ch52-4w7c-c8xp); this update is not proof of an upstream fix. The dependency is absent from the app payload. The default Electron build downloader leaves HTTP caching disabled; two requests to the same test URL fetched two distinct responses. Custom build downloaders or shared-cache options require a separate assessment.

### Source push and release attachments

Commit the clean source tree, lockfile, shared inputs and documentation. Do not commit `node_modules`, `bin`, `obj`, browser profiles, personal configuration, internal test evidence, generated installers or local animation projects. The supplied `.gitignore` covers these local outputs; `.gitattributes` preserves reviewed source bytes without line-ending conversion.

For an existing remote repository, start from its existing history and apply this source tree on a release branch. Review the diff before committing and pushing. Do not replace remote history. Version tags and the published release must refer to the reviewed source commit.

Attach the five V1.0.1 EXEs, the complete source ZIP, `RELEASE-NOTES.md`, and `SHA256SUMS.txt` to the release. Keep license and notice files in the source and app payloads. `RELEASE-MANIFEST.json` describes each prepared attachment and its hash. A local preparation folder is not itself an online release.

---

## 中文

仓库将三个当前产品保留在 `apps/<product>/v1.0.1/source`，根目录 `shared` 是必需构建输入。根 README 和各软件 README 面向用户，均先完整英文、后完整中文；构建细节放入 DEVELOPMENT。

### 构建输入与顺序

使用 Windows x64、Node.js 22.12+ 和 npm、可构建 .NET 8 WPF 的 .NET SDK，以及含 MUI2 的 NSIS 3。保留依赖锁文件和许可证。编辑器发布自包含运行时，用户无需 SDK。

1. 在主程序 source 目录执行 `npm ci`、上述源码检查和 `npm run dist:win`。
2. 分别进入两个编辑器的 source 目录，按英文部分命令发布对应工程，创建 `../installers` 输出目录，再运行 `makensis installer.nsi`。
3. 三个组件安装包均生成后，在主程序 `source/packaging` 执行 `makensis KnotJotSuite.nsi`。

每个产品输出到同版本的 `installers`，套件读取三个组件的精确文件名。保留目录结构及程序身份。

### 发布前验证

在主程序 source 执行 `test:source-layout`、`branding-release.test.js` 及 `test:core`；在两个编辑器 source 使用 PowerShell 7 执行 `tests/BrandingSmoke.ps1`。更多测试见各 DEVELOPMENT；GUI 测试及原生安装验收需要适合的测试环境。

核对版本、源码与安装包一致性、README 语言顺序及链接、随附许可证和 SHA-256。仍需人工验收的范围记录在 `RELEASE-STATUS.md`，源码检查不能记作原生界面实测完成。

2026-10-05 锁文件将仅构建使用的 `http-cache-semantics` 更新为 4.3.0。npm 审计未列出漏洞，但独立共享缓存样本仍复现 [GHSA-ch52-4w7c-c8xp](https://github.com/advisories/GHSA-ch52-4w7c-c8xp) 所述行为，不能据此宣称上游问题已修复。应用载荷不包含此依赖；默认 Electron 构建下载器未启用 HTTP 缓存，两次访问相同测试地址获取了两份不同响应。自定义构建下载器或共享缓存选项需另外评估。

### 推送源码与上传发行附件

提交干净源码、锁文件、共享输入及文档。依赖、编译目录、浏览器配置、私人设置、内部测试证据、生成安装包及本地动画工程不进入源码提交。提供的 `.gitignore` 排除这些本地产物；`.gitattributes` 禁止自动换行转换，保留已审查源码字节。

推送已有远端仓库时，应以远端现有历史为起点，在发布分支应用这份源码并检查差异，再提交推送，不替换远端历史。版本标签和正式发行应指向审查后的源码提交。

发行附件包含五个 V1.0.1 EXE、完整源码 ZIP、发行说明及 `SHA256SUMS.txt`；源码和程序载荷均保留许可证及声明。`RELEASE-MANIFEST.json` 记录准备好的附件及哈希。本地准备目录本身不表示已经上线发布。
