; Define per-user installer metadata and retained executable branding.
; 定义用户级安装器元数据与保留的可执行文件品牌。
Unicode true
Name "KnotJot 界面编辑器"
OutFile "..\installers\KnotJot-UI-Editor-Setup-1.0.0.exe"
VIProductVersion "1.0.0.0"
VIAddVersionKey "ProductName" "KnotJot 界面编辑器"
VIAddVersionKey "FileDescription" "KnotJot 界面编辑器 V1.0.0 安装程序"
VIAddVersionKey "FileVersion" "1.0.0.0"
VIAddVersionKey "ProductVersion" "1.0.0"
VIAddVersionKey "LegalCopyright" "happymore"
InstallDir "$LOCALAPPDATA\Programs\KnotJot UI Editor"
RequestExecutionLevel user
SetCompressor /SOLID lzma
; Show destination selection and installation progress pages.
; 显示目标目录选择与安装进度页面。
Page directory
Page instfiles
UninstPage uninstConfirm
UninstPage instfiles

; Install the published application, protocol, licenses, and launch shortcuts.
; 安装已发布程序、协议、许可证与启动快捷方式。
Section "安装"
  SetOutPath "$INSTDIR"
  File "bin\Release\net8.0-windows\win-x64\publish\KnotJot界面编辑器.exe"
  SetOutPath "$INSTDIR\protocols"
  File "bin\Release\net8.0-windows\win-x64\publish\protocols\ui-skin-protocol.json"
  SetOutPath "$INSTDIR"
  File "LICENSE"
  File "THIRD_PARTY_NOTICES.md"
  File /r "THIRD_PARTY_LICENSES"
  WriteUninstaller "$INSTDIR\卸载.exe"
  CreateDirectory "$SMPROGRAMS\KnotJot 界面编辑器"
  CreateShortcut "$SMPROGRAMS\KnotJot 界面编辑器\KnotJot 界面编辑器.lnk" "$INSTDIR\KnotJot界面编辑器.exe"
  CreateShortcut "$DESKTOP\KnotJot 界面编辑器.lnk" "$INSTDIR\KnotJot界面编辑器.exe"
SectionEnd

; Remove this installation and its launch shortcuts.
; 移除本次安装及其启动快捷方式。
Section "Uninstall"
  Delete "$DESKTOP\KnotJot 界面编辑器.lnk"
  Delete "$SMPROGRAMS\KnotJot 界面编辑器\KnotJot 界面编辑器.lnk"
  RMDir "$SMPROGRAMS\KnotJot 界面编辑器"
  RMDir /r "$INSTDIR"
SectionEnd
