; EN: Install the V1.0.0-derived patch with selectable language and English labels; keep executable identity.
; ZH: 安装基于 V1.0.0 的补丁，提供语言选择和英文名称，保留程序身份。
Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
Name "KnotJot UI Editor"
OutFile "..\installers\KnotJot-UI-Editor-Setup-1.0.1.exe"
VIProductVersion "1.0.1.0"
VIAddVersionKey "ProductName" "KnotJot UI Editor"
VIAddVersionKey "FileDescription" "KnotJot UI Editor Setup"
VIAddVersionKey "FileVersion" "1.0.1.0"
VIAddVersionKey "ProductVersion" "1.0.1"
VIAddVersionKey "LegalCopyright" "happymore"
InstallDir "$LOCALAPPDATA\Programs\KnotJot UI Editor"
RequestExecutionLevel user
SetCompressor /SOLID lzma
!define MUI_ICON "assets\icon.ico"
!define MUI_UNICON "assets\icon.ico"
!define MUI_LANGDLL_ALLLANGUAGES
!define MUI_LANGDLL_REGISTRY_ROOT "HKCU"
!define MUI_LANGDLL_REGISTRY_KEY "Software\KnotJotUIEditor"
!define MUI_LANGDLL_REGISTRY_VALUENAME "InstallerLanguage"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "SimpChinese"

; EN: Choose the installation language at startup and restore it for uninstall prompts.
; ZH: 启动时选择安装语言，并在卸载提示中恢复该语言。
Function .onInit
  !insertmacro MUI_LANGDLL_DISPLAY
FunctionEnd
Function un.onInit
  !insertmacro MUI_UNGETLANGUAGE
FunctionEnd

; EN: Install the complete runtime and licenses, English shortcuts and installed-app metadata.
; ZH: 安装完整运行文件与许可证，创建英文快捷方式和已安装应用信息。
Section
  SetOutPath "$INSTDIR"
  File /r "bin\Release\net8.0-windows\win-x64\publish\*"
  File "LICENSE"
  File "THIRD_PARTY_NOTICES.md"
  SetOutPath "$INSTDIR\THIRD_PARTY_LICENSES"
  File /r "THIRD_PARTY_LICENSES\*"
  SetOutPath "$INSTDIR"
  ; EN: A legacy uninstaller identifies the same V1.0.0 install; remove only its known aliases.
  ; ZH: 旧卸载程序标识同目录的 V1.0.0 安装，仅清除已知旧别名。
  IfFileExists "$INSTDIR\卸载.exe" 0 legacy_cleanup_done
  Delete "$DESKTOP\KnotJot 界面编辑器.lnk"
  Delete "$SMPROGRAMS\KnotJot 界面编辑器\KnotJot 界面编辑器.lnk"
  RMDir "$SMPROGRAMS\KnotJot 界面编辑器"
  Delete "$INSTDIR\卸载.exe"
  legacy_cleanup_done:
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\KnotJot UI Editor"
  CreateShortcut "$SMPROGRAMS\KnotJot UI Editor\KnotJot UI Editor.lnk" "$INSTDIR\KnotJot界面编辑器.exe"
  CreateShortcut "$DESKTOP\KnotJot UI Editor.lnk" "$INSTDIR\KnotJot界面编辑器.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\KnotJotUIEditor" "DisplayName" "KnotJot UI Editor"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\KnotJotUIEditor" "DisplayVersion" "1.0.1"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\KnotJotUIEditor" "Publisher" "happymore"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\KnotJotUIEditor" "DisplayIcon" "$INSTDIR\KnotJot界面编辑器.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\KnotJotUIEditor" "UninstallString" '"$INSTDIR\Uninstall.exe"'
SectionEnd

; EN: Remove installed runtime and shortcuts; keep roaming user projects and settings.
; ZH: 移除安装的运行文件与快捷方式，保留漫游目录中的用户工程和设置。
Section "Uninstall"
  Delete "$DESKTOP\KnotJot UI Editor.lnk"
  Delete "$SMPROGRAMS\KnotJot UI Editor\KnotJot UI Editor.lnk"
  RMDir "$SMPROGRAMS\KnotJot UI Editor"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\KnotJotUIEditor"
  Delete "$INSTDIR\KnotJot界面编辑器.exe"
  Delete "$INSTDIR\LICENSE"
  Delete "$INSTDIR\THIRD_PARTY_NOTICES.md"
  Delete "$INSTDIR\README.md"
  ; EN: Remove only the development guide shipped by this installer.
  ; ZH: 仅移除本安装器随附的开发指南。
  Delete "$INSTDIR\DEVELOPMENT.md"
  RMDir /r "$INSTDIR\THIRD_PARTY_LICENSES"
  Delete "$INSTDIR\Microsoft.Web.WebView2.Core.xml"
  Delete "$INSTDIR\Microsoft.Web.WebView2.WinForms.xml"
  Delete "$INSTDIR\Microsoft.Web.WebView2.Wpf.xml"
  Delete "$INSTDIR\protocols\ui-skin-protocol.json"
  RMDir "$INSTDIR\protocols"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
SectionEnd


