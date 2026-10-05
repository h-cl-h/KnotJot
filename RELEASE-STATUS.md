# V1.0.1 acceptance scope / 验收范围

## English

V1.0.1 remains based on the three V1.0.0 products. Acceptance resumed after midnight in Beijing on 2026-10-05, including AI as authorized. This candidate includes additional repairs and rebuilt packages. The remaining uninstall case was closed by the user's personal test confirmation on 2026-10-05. Source delivery is tracked separately; no version tag or GitHub Release is part of this acceptance record.

New repairs cover the preview-diagnostic clipboard crash, source-checkout launch from both editors, stale AI credential-destination text after switching presets, and incomplete timeout/server-error guidance. Text Style Editor release builds omit debug symbols and local source paths; upgrades remove only its obsolete app-owned PDB. Both editors retain their external documentation, licenses and protocol inputs during repeated single-file publication; two consecutive publishes were checked for each editor.

Controlled AI acceptance passed 53 checks across three native process lifetimes: settings, five preset buttons, encrypted remembered keys, session-only keys, destinations, one optional-parameter retry and error guidance. The complete Node suite passed 53 checks; source-launch regression passed 12. Clipboard contention/recovery and existing editor regressions passed. These results cover the recorded samples, not every provider or feature.

The rebuilt main payload passed 12 runtime checks, including actual PNG/JPEG/PDF output, document reopening and native file save. Packaged source, production dependencies, installer payloads, versions, licenses, symbol privacy and suite component hashes are checked against their inputs. Extraction and callback tests remain distinct from native installation.

Real local Ollama inference using the existing model passed discovery, connection, streamed response and mind-map parsing. The built-in two-stage workflow generated seven topics; saving and reopening in a fresh process preserved the outline, all four chat messages and the generated state. A subsequent real Kimi cloud test retrieved four available models and passed connection, streaming and the built-in two-stage map workflow using kimi-k2.6. Reopening that cloud-generated document through a native file argument in a fresh packaged process preserved its outline, four messages and generated state; the session-only key was not restored. The saved OpenAI key had expired, and a newly supplied official key also returned HTTP 401 (invalid key). Successful cloud generation is now verified with Kimi; OpenAI success is not claimed. Only synthetic examples were submitted, and no model was downloaded.

Native testing observed all three standalone upgrade wizards in English or Chinese, an earlier English suite installation and the final Chinese suite installation. All three final installed executables match the verified package payloads, both seeded editor test files were retained, and the obsolete Text Style Editor PDB was removed. Explorer cold/hot checks passed for .knotjot double-click and .bmap Open with → KnotJot; the pre-existing Windows default for .bmap was preserved. Both editors launched the installed main app and the source checkout. The final installed UI Editor survived real clipboard contention and copied the exact diagnostic after release. A temporary test-tool window-reference failure was resolved and did not leave these checks incomplete.

The agent could not perform native uninstallation because the tool rejected the Text Style Editor uninstaller. On 2026-10-05 the user confirmed personal testing before requesting the GitHub push. This closes the remaining case as user-confirmed, not independently observed by the agent; no per-product uninstall logs or screenshots were supplied. Earlier tool-blocked evidence is retained.

The original feature audit has 679 rows: 144 runtime-verified, 532 source-reviewed, one installation/uninstallation row closed by user confirmation, and two removed as requested. AI rows distinguish controlled, local and cloud evidence; chat restoration now has its own runtime evidence. Source review is not full UI testing. The V1.0.0 animation environment was restored after the earlier installation tests. A fresh follow-up comparison found all 528 original files and 14 registry branches unchanged. One additional 117-byte appearance-preference database log was retained in place, so the profile directory is not claimed to be an exact file-set match. This follow-up did not install or uninstall apps, replace user profiles, or change the user's AI configuration. Test projects and evidence are retained separately.

Current full and production npm audits report zero listed vulnerabilities. A development-only upstream shared-cache behavior remains reproducible under an explicit-cache fixture. The app excludes that dependency and the default build downloader does not enable this cache. See [release instructions](RELEASING.md) for the bounded assessment; an audit count is not proof of an upstream fix.

## 中文

V1.0.1 继续基于三个软件各自的 V1.0.0。按授权于北京时间 2026-10-05 零点后恢复验收，并包含 AI。候选版增加修复并重新打包；剩余卸载项目已由用户于 2026-10-05 亲自测试确认关闭。源码推送另行记录，本验收记录不代表已打标签或发布 GitHub Release。

新增修复包括预览诊断复制遇剪贴板占用时崩溃、两个编辑器无法正确启动已连接源码、AI 切换预设后密钥目的地址未更新，以及部分超时和服务器错误缺少恢复建议。文本框样式编辑器发行构建不再包含调试符号及本地源码路径，升级仅清理本程序遗留的同名 PDB。两个编辑器连续进行单文件发布时也会保留外部说明、许可证及协议输入；每个编辑器均完成两次连续发布检查。

受控 AI 在三个原生进程生命周期中通过 53 项，覆盖设置、五个预设按钮、密钥加密记住、仅会话密钥、请求目的地、一次可选参数重试及错误提示。全部 Node 测试通过 53 项，源码启动回归通过 12 项，剪贴板占用恢复及编辑器既有回归通过。结论限于已测样本，不保证每家服务或所有功能均可用。

重新打包的主程序通过 12 项运行检查，包含真实 PNG/JPEG/PDF 输出、文档重开和原生文件保存。打包源码、生产依赖、安装载荷、版本、许可证、调试路径隐私及套件组件哈希均对照输入核验；解包及回调测试与原生安装分开记录。

电脑现有 Ollama 模型已通过真实模型发现、连接、流式回复及导图内容解析。内置两阶段流程生成七个主题，保存后在新进程重开，导图大纲、四条对话和已生成状态均保留。随后真实 Kimi 云端测试获取四个可用模型，选用 kimi-k2.6 后连接、流式回复及内置两阶段生成通过。生成文件经原生文件参数在新打包进程重开，大纲、四条对话和已生成状态均保留，会话密钥未被恢复。原保存的 OpenAI 密钥已过期，用户新提供的官方密钥也返回 HTTP 401（密钥无效）。云端正常生成现已通过 Kimi 验证，不宣称 OpenAI 正常生成通过；仅提交虚构示例，没有下载模型。

原生测试已观察三个单独安装器的英文或中文升级向导、较早候选版的英文套件安装，以及最终中文版套件安装。最终三个程序的安装文件均与已核验载荷一致，两个编辑器预置的测试文件保留正常，文本编辑器的旧 PDB 已移除。资源管理器实测 .knotjot 双击和 .bmap“打开方式 → KnotJot”的冷启动及运行中转交均通过，保留原有 .bmap 默认程序。两个编辑器均已启动安装版及源码版主程序。最终安装的界面编辑器遇真实剪贴板占用时保持运行，释放后准确复制诊断。测试工具一度引用旧窗口的问题已排除，未留下这些项目未完成。

代理此前因工具拒绝启动文本框样式编辑器卸载程序而未能执行原生卸载。2026-10-05 用户在要求推送 GitHub 前确认已亲自测试，剩余项目按“用户确认通过”关闭，不计为代理独立实测；用户未另附逐软件卸载日志或截图。此前工具受阻证据继续保留。

原功能审查共 679 行：144 条运行验证、532 条源码核查、1 条安装卸载综合项目由用户确认通过、2 条按要求移除。AI 行区分受控、本地及云端实际范围，对话恢复新增独立运行证据。源码核查不等于完整界面实测。较早安装测试结束后已恢复 V1.0.0 动画环境；本次新备份对照确认 528 个原文件和 14 项注册表分支均未变化。配置目录新增一个 117 字节的外观偏好数据库日志，已原位保留，因此不宣称目录文件集合完全一致。本次没有安装或卸载软件、替换原配置目录或改动用户 AI 配置；测试工程和证据另行保留。

当前完整及生产 npm 审计均未列出漏洞。仅用于构建的上游共享缓存行为在显式启用缓存的样本中仍可复现；应用不含该依赖，默认构建下载器也未启用该缓存。范围评估见[发行说明](RELEASING.md)，不能将审计计数当作上游问题已修复的证明。
