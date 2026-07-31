# KnotJot V1.0.0 第三方组件与许可证

KnotJot 项目自有代码采用 GPL-3.0-only，完整条款见同目录 `LICENSE`。下面列出的文件不是重新授权为 GPL；它们继续适用各自的许可证。完整许可副本位于 `resources/licenses/`，并会随 Windows 安装包一起分发。

## 运行时与 JavaScript

| 组件 | 版本/用途 | 许可证 | 本地许可副本 |
|---|---|---|---|
| [Electron](https://github.com/electron/electron) | 31.x，桌面运行时 | MIT；Chromium/Node.js 等另见运行时自带 notices | `Electron-LICENSE.txt` |
| [jsPDF](https://github.com/parallax/jsPDF) | 4.2.1，PDF 导出 | MIT，打包文件内还保留其组成模块声明 | `jsPDF-LICENSE.txt` |

Electron 发行目录还包含 `LICENSE.electron.txt` 和 `LICENSES.chromium.html`；打包或再分发时不得删除。

## 随程序分发的 CSS 与设计令牌

| 本地文件 | 上游项目 | 版本 | 许可证 | 本地许可副本 |
|---|---|---:|---|---|
| `resources/skins/coreui/coreui.min.css` | [CoreUI](https://github.com/coreui/coreui) | 5.8.0 | MIT | `CoreUI-LICENSE.txt` |
| `resources/skins/nes/nes.css` | [NES.css](https://github.com/nostalgic-css/NES.css) | development snapshot | MIT | `NES.css-LICENSE.txt` |
| `resources/skins/nes/nes.css` 中的重置样式 | [Bootstrap](https://github.com/twbs/bootstrap) / [Normalize.css](https://github.com/necolas/normalize.css) | Bootstrap Reboot 4.1.3 | MIT | `Bootstrap-4.1.3-LICENSE.txt` / `Normalize.css-LICENSE.txt` |
| `resources/skins/photon/photon.css` | [Photon](https://github.com/connors/photon) | 0.1.2 | MIT | `Photon-LICENSE.txt` |
| `resources/skins/pico/pico.css` | [Pico CSS](https://github.com/picocss/pico) | 2.1.1 | MIT | `Pico.css-LICENSE.txt` |
| `resources/skins/shoelace/shoelace-light.css` | [Shoelace](https://github.com/shoelace-style/shoelace) | light theme snapshot | MIT | `Shoelace-LICENSE.txt` |
| `resources/skins/tailwind/tailwind-skin.css` | [Tailwind CSS](https://github.com/tailwindlabs/tailwindcss) / [daisyUI](https://github.com/saadeghi/daisyui) design tokens | custom skin | MIT | `Tailwind-CSS-LICENSE.txt` / `daisyUI-LICENSE.txt` |
| `resources/skins/water/water.css` | [Water.css](https://github.com/kognise/water.css) | snapshot | MIT | `Water.css-LICENSE.txt` |
| `resources/skins/win7/7.css` | [7.css](https://github.com/khang-nd/7.css) | 0.21.1 | MIT | `7.css-LICENSE.txt` |
| `resources/skins/win98/98.css` | [98.css](https://github.com/jdan/98.css) | 0.1.21 | MIT | `98.css-LICENSE.txt` |
| `resources/skins/xp/XP.css` | [XP.css](https://github.com/botoxparty/XP.css) | 0.2.6 | MIT | `XP.css-LICENSE.txt` |

`*-skin.css` 和 `ui-theme.css` 是为 KnotJot 编写的适配层；其中引用上游设计令牌或框架观感的部分仍按上表保留来源和许可。`skeleton/ui-theme.css` 只是本项目自定义主题，没有复制 Skeleton 框架文件，因此不把 Skeleton 列为分发依赖。

## 字体

`resources/skins/nes/PressStart2P.ttf` 来自 [Press Start 2P](https://github.com/google/fonts/tree/main/ofl/pressstart2p)，版权声明为 “Copyright 2012 The Press Start 2P Project Authors”，采用 SIL Open Font License 1.1，并保留保留字体名 “Press Start 2P”。完整条款见 `Press-Start-2P-OFL-1.1.txt`。

98.css 和 XP.css 的 CSS 会尝试引用 `ms_sans_serif*.woff*`，但这些字体文件没有进入本仓库或安装包；程序会回退到 Windows 系统字体。不要从非授权来源补入并重新分发这些字体。

## 兼容性说明

以上实际分发的 MIT 组件允许与 GPL-3.0-only 程序共同分发，OFL 1.1 字体也可随软件打包；条件是保留原版权、许可和免责声明，并且字体本身继续遵守 OFL。第三方项目名称和商标只用于来源说明，不代表上游作者为 KnotJot 背书。
