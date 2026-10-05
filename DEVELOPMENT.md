# KnotJot branch development

## English

This branch keeps only KnotJot V1.0.1 and the shared inputs. The application source is [apps/knotjot/v1.0.1/source](apps/knotjot/v1.0.1/source/); follow its [development guide](apps/knotjot/v1.0.1/source/DEVELOPMENT.md) for prerequisites and individual build/test commands. Preserve this relative layout. No application source bytes have been changed for the branch split.

[RELEASE-STATUS.md](RELEASE-STATUS.md) is the current acceptance record and supersedes earlier maintenance-scope statements in bundled development guides. The remaining uninstall case was closed by user confirmation, not by an independently observed agent test.

### Cross-app and suite builds

An individual product builds from its own branch. The three-component installer and tests using another app require a combined build workspace. Fetch the other branches, export their missing apps directories into a separate build copy, and retain this branch's shared directory. For example, in a disposable main-branch build copy after fetching origin:

```powershell
git archive --format=zip --output=ui-source.zip origin/knotjot-ui-editor apps/knotjot-ui-editor
Expand-Archive ui-source.zip -DestinationPath .
git archive --format=zip --output=text-source.zip origin/knotjot-text-style-editor apps/knotjot-text-style-editor
Expand-Archive text-source.zip -DestinationPath .
```

From an editor branch, export `apps/knotjot` from `origin/main` in the same way when a test needs the main app. Run dependency installation in each required source directory. Build each component installer before the suite; see [RELEASING.md](RELEASING.md). Installer-payload tests require generated EXEs and their SHA256SUMS files, which are intentionally absent from a source checkout. The Text Style Editor frame-shadow oracle additionally needs Electron in the main app's source directory. Keep exported archives, binaries, dependencies, profiles and test outputs local.

The root user guide is a branch-adapted copy with rebased links. Bundled app guides and executable sources remain identical to the verified V1.0.1 delivery.

## 中文

本分支仅保留主程序 V1.0.1 和共享输入。程序源码位于 [apps/knotjot/v1.0.1/source](apps/knotjot/v1.0.1/source/)，环境要求及单软件构建测试命令见其[开发说明](apps/knotjot/v1.0.1/source/DEVELOPMENT.md)。请保留相对目录结构；分支拆分没有修改任何程序源码字节。

当前验收结论以 [RELEASE-STATUS.md](RELEASE-STATUS.md) 为准，覆盖随附开发指南中较早维护批次的范围表述。剩余卸载项目由用户确认关闭，不记作代理独立观察的测试。

### 跨软件及套件构建

单个软件可从自身分支构建。三软件套件和依赖其他软件的测试需要组合工作目录：获取其他分支，将缺失的 apps 子目录导出到单独构建副本，并保留当前 shared 目录。英文部分命令展示了在获取远端分支后，向一次性主程序构建副本导入两个编辑器源码。若从编辑器分支开始，同样从 origin/main 导出 apps/knotjot，供需要主程序的测试使用。

在每个必需 source 目录安装对应依赖。先生成三个单独安装器再打包套件，步骤见 [RELEASING.md](RELEASING.md)。安装载荷测试需要生成的 EXE 及 SHA256SUMS 文件，纯源码提交不包含这些产物。文本框样式编辑器的阴影对比参考图还需要主程序源码目录中的 Electron。导出归档、二进制、依赖、配置及测试输出应保留本地。

根使用说明为调整了链接的分支展示副本；随附软件指南及可执行源码与已核验 V1.0.1 交付完全一致。
