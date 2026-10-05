# KnotJot V1.0.1 development

## English

The user guide is [README.md](README.md). Keep this source at `apps/knotjot/v1.0.1/source` inside the three-app repository. Packaging reads the root `shared` directory through a relative path.

Use Windows x64, Node.js **22.12 or later**, and npm. The lockfile pins Electron 44.4.5, electron-builder 26.15.3 and Electron Packager 20.3.0. DOMPurify is constrained to 3.4.16 through an override; jsPDF remains 4.2.1. From this source directory:

```powershell
npm ci
npm run test:source-layout
node --test tests/branding-release.test.js
npm run test:core
npm audit
npm exec electron -- tests/release-runtime-smoke.js
npm start
```

The first commands install dependencies, run source/model checks and audit the complete dependency tree. The hidden runtime check saves PNG/JPEG/PDF and native file-drop fixtures under the sibling `preview/testing/release-runtime` directory (override with `KNOTJOT_TEST_OUTPUT`). Electron downloads its runtime on first use; `node node_modules/electron/install.js` can prepare it explicitly. `npm start` opens the app. Additional Electron tests listed in `package.json` may create windows and local test profiles; run them in a suitable test session.

```powershell
npm run dist:win
```

The setup and portable executables go to `../installers`. Preserve `resources`, `src`, shared protocols, license copies and `package-lock.json`. Dependencies, temporary test outputs and packaging caches are local artifacts. The repository release guide explains companion and suite build order.

Keep the application ID, executable name, map formats and editor handshakes compatible. V1.0.1 derives from V1.0.0 and does not incorporate the separate later branch.

Native drag/drop obtains paths through the preload's bounded `getPathForFile(File)` wrapper; renderer-created Files have no native path. Electron 44's Windows Open dialog initially uses Downloads. Its network stack also changes conditional TLS client-certificate handling; this release does not change AI transport code or claim external-service acceptance. Manual native acceptance remains listed in the repository's RELEASE-STATUS.

---

## 中文

用户说明见 [README.md](README.md)。保留三软件仓库中的 `apps/knotjot/v1.0.1/source` 结构，打包通过相对路径读取根目录 `shared`。

使用 Windows x64、**Node.js 22.12 或更高版本**及 npm。锁文件固定 Electron 44.4.5、electron-builder 26.15.3 和 Electron Packager 20.3.0；通过 override 将 DOMPurify 约束为 3.4.16，jsPDF 保持 4.2.1。在当前 source 目录执行：

```powershell
npm ci
npm run test:source-layout
node --test tests/branding-release.test.js
npm run test:core
npm audit
npm exec electron -- tests/release-runtime-smoke.js
npm start
```

前面的命令安装依赖、执行源码和模型检查，并审计完整依赖树。隐藏运行时测试会将 PNG/JPEG/PDF 及原生文件拖入样本保存到同级 `preview/testing/release-runtime`（可通过 `KNOTJOT_TEST_OUTPUT` 指定目录）。Electron 首次使用时下载运行内核，也可执行 `node node_modules/electron/install.js` 提前准备。`npm start` 会打开应用。`package.json` 中更多 Electron 测试可能打开窗口及创建测试配置，请在合适的测试会话运行。

```powershell
npm run dist:win
```

安装版和便携版输出至 `../installers`。保留资源、源码、共享协议、许可证和锁文件；依赖、临时测试输出及打包缓存属于本地产物。配套编辑器和套件构建顺序见仓库发布指南。

保持应用 ID、可执行文件名、导图格式及编辑器握手兼容。V1.0.1 源于 V1.0.0，没有合并独立后续分支。

原生文件拖入通过预加载受限接口 `getPathForFile(File)` 取得路径，渲染器创建的 File 不具有原生路径。Electron 44 的 Windows 打开对话框初始使用“下载”目录；其网络栈也改变了特定 TLS 客户端证书场景的处理。本次不改 AI 传输代码，也不宣称完成外部服务验收。人工原生验收继续记录在仓库 RELEASE-STATUS 中。
