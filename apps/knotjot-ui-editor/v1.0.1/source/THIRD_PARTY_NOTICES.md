# Third-party notices / 第三方声明

KnotJot 界面编辑器自有代码采用 GPL-3.0-only，完整条款见同目录 `LICENSE`。

## Microsoft WebView2

- NuGet package: `Microsoft.Web.WebView2`
- Locked version: `1.0.4078.44`
- Purpose: KnotJot-compatible real preview in the WPF editor.
- License copy: `THIRD_PARTY_LICENSES/WebView2/LICENSE.txt`
- Notice copy: `THIRD_PARTY_LICENSES/WebView2/NOTICE.txt`

The two files above preserve the complete text from the locally restored NuGet package version shown above; repository line endings may be normalized.

## .NET runtime and libraries

Self-contained Windows publishing may redistribute Microsoft .NET runtime and library files. Their license and third-party notices are preserved as:

- `THIRD_PARTY_LICENSES/dotnet-LICENSE.txt`
- `THIRD_PARTY_LICENSES/dotnet-ThirdPartyNotices.txt`

## Source-code provenance

V1.0.0 does not copy source code from Core2D, Nodify, GongSolutions.WPF.DragDrop, SkiaSharp, AvalonDock, or another GitHub project. Multi-selection, layer drag/drop, masks, atomic storage, synchronization, and tests in this version are project-owned implementations. Earlier planning evaluated public projects only as possible design references; none became a V1.0.0 dependency.

No MS-PL code is included. The project file contains one third-party package reference, WebView2, pinned to the exact version above.
