# KnotJot Text Style Editor V1.0.1 第三方组件与许可证

编辑器自有代码采用 GPL-3.0-only，完整条款见同目录 `LICENSE`。

## .NET 8 与 WPF

编辑器发布为 `win-x64` 自包含单文件，包含 Microsoft .NET 8 运行时和 Windows Desktop/WPF 组件。Microsoft .NET 运行时源码采用 MIT，完整许可副本见：

- `THIRD_PARTY_LICENSES/dotnet-LICENSE.txt`
- `THIRD_PARTY_LICENSES/dotnet-ThirdPartyNotices.txt`

第二个文件包含 .NET 运行时所带第三方组件的原始声明；构建安装包或再分发时不得删除这两个文件。

## 没有集成的调研项目

早期调研曾评估 RichCanvas、AvalonDock、MVVMDiagramDesigner、Nodify、Paper.js 和 Excalidraw。当前 `.csproj` 没有任何第三方 `PackageReference`，源码也没有这些项目的命名空间、源码文件或二进制，因此它们不是本版本的分发依赖，也不应在发布说明中写成“已集成”。

编辑器的字体列表读取 Windows 已安装字体，不随程序打包字体文件。用户选择的字体不因此被重新分发。
