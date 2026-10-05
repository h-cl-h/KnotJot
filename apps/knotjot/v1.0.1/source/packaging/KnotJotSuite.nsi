Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"

!define PRODUCT_NAME "KnotJot Suite 1.0.1"
!define PRODUCT_VERSION "1.0.1"
!define PRODUCT_FILE_VERSION "1.0.1.0"
!define MAIN_INSTALLER "..\..\installers\KnotJot-Setup-1.0.1.exe"
!define UI_INSTALLER "..\..\..\..\knotjot-ui-editor\v1.0.1\installers\KnotJot-UI-Editor-Setup-1.0.1.exe"
!define TEXT_INSTALLER "..\..\..\..\knotjot-text-style-editor\v1.0.1\installers\KnotJot-Text-Style-Editor-Setup-1.0.1.exe"

Name "${PRODUCT_NAME}"
Caption "${PRODUCT_NAME}"
OutFile "..\..\installers\KnotJot-Suite-Setup-1.0.1.exe"
VIProductVersion "${PRODUCT_FILE_VERSION}"
VIAddVersionKey "ProductName" "${PRODUCT_NAME}"
VIAddVersionKey "FileDescription" "KnotJot Suite Installer"
VIAddVersionKey "FileVersion" "${PRODUCT_FILE_VERSION}"
VIAddVersionKey "ProductVersion" "${PRODUCT_VERSION}"
VIAddVersionKey "CompanyName" "happymore"
VIAddVersionKey "LegalCopyright" "happymore"

RequestExecutionLevel user
SetCompress off
SetDatablockOptimize on
CRCCheck on
ShowInstDetails show
BrandingText "KnotJot V1.0.1"

!define MUI_ABORTWARNING
!define MUI_COMPONENTSPAGE_SMALLDESC
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "SimpChinese"
; EN: Select the language before displaying suite pages.
; ZH: 显示套件页面前选择安装语言。
Function .onInit
  !insertmacro MUI_LANGDLL_DISPLAY
FunctionEnd

Var ChildExitCode
Var ChildDisplayName

; EN: Translate installation progress and failures using the chosen wizard language.
; ZH: 根据向导所选语言翻译安装进度和失败提示。
LangString INSTALLING ${LANG_ENGLISH} "Installing $ChildDisplayName..."
LangString INSTALLING ${LANG_SIMPCHINESE} "正在安装 $ChildDisplayName..."
LangString INSTALLED ${LANG_ENGLISH} "$ChildDisplayName installed."
LangString INSTALLED ${LANG_SIMPCHINESE} "$ChildDisplayName 安装完成。"
LangString INSTALL_FAILED ${LANG_ENGLISH} "$ChildDisplayName failed (exit code: $ChildExitCode).$\r$\nSome components may already be installed. Retry the component installer or uninstall the installed components."
LangString INSTALL_FAILED ${LANG_SIMPCHINESE} "$ChildDisplayName 安装失败（退出代码：$ChildExitCode）。$\r$\n可能已经完成部分安装，请单独运行对应安装包重试或卸载已经安装的组件。"

; EN: Run the extracted component silently and stop the suite on the first child-installer failure.
; ZH: 静默运行解包组件，并在首个子安装器失败时终止套件安装。
!macro RunInstaller FILE_NAME DISPLAY_NAME
  StrCpy $ChildDisplayName "${DISPLAY_NAME}"
  DetailPrint "$(INSTALLING)"
  ClearErrors
  ExecWait '"$PLUGINSDIR\${FILE_NAME}" /S' $ChildExitCode
  ${If} ${Errors}
    StrCpy $ChildExitCode 1
  ${EndIf}
  ${If} $ChildExitCode != 0
    DetailPrint "$(INSTALL_FAILED)"
    SetErrorLevel $ChildExitCode
    MessageBox MB_ICONSTOP|MB_OK "$(INSTALL_FAILED)" /SD IDOK
    Quit
  ${EndIf}
  DetailPrint "$(INSTALLED)"
!macroend

; EN: Install the named component and create its required application resources.
; ZH: 安装具名组件并创建其必需的应用资源。
Section "KnotJot V1.0.1 (required)" SecMain
  SectionIn RO
  SetOutPath "$PLUGINSDIR"
  File /oname=KnotJot-Setup-1.0.1.exe "${MAIN_INSTALLER}"
  !insertmacro RunInstaller "KnotJot-Setup-1.0.1.exe" "KnotJot V1.0.1"
SectionEnd

; EN: Install the named component and create its required application resources.
; ZH: 安装具名组件并创建其必需的应用资源。
Section "KnotJot UI Editor V1.0.1" SecUiEditor
  SetOutPath "$PLUGINSDIR"
  File /oname=KnotJot-UI-Editor-Setup-1.0.1.exe "${UI_INSTALLER}"
  !insertmacro RunInstaller "KnotJot-UI-Editor-Setup-1.0.1.exe" "KnotJot UI Editor V1.0.1"
SectionEnd

; EN: Install the named component and create its required application resources.
; ZH: 安装具名组件并创建其必需的应用资源。
Section "KnotJot Text Style Editor V1.0.1" SecTextEditor
  SetOutPath "$PLUGINSDIR"
  File /oname=KnotJot-Text-Style-Editor-Setup-1.0.1.exe "${TEXT_INSTALLER}"
  !insertmacro RunInstaller "KnotJot-Text-Style-Editor-Setup-1.0.1.exe" "KnotJot Text Style Editor V1.0.1"
SectionEnd

LangString DESC_SecMain ${LANG_ENGLISH} "KnotJot mind-map application (required)."
LangString DESC_SecUiEditor ${LANG_ENGLISH} "Design and synchronize the application appearance."
LangString DESC_SecTextEditor ${LANG_ENGLISH} "Design and synchronize text box styles."
LangString DESC_SecMain ${LANG_SIMPCHINESE} "KnotJot 主程序，必须安装。"
LangString DESC_SecUiEditor ${LANG_SIMPCHINESE} "设计并同步 KnotJot 的整套 UI 外观。"
LangString DESC_SecTextEditor ${LANG_SIMPCHINESE} "设计并同步 KnotJot 的文本框样式。"

; EN: Attach component descriptions to the installer selection page.
; ZH: 为安装器组件选择页面附加说明。
!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecMain} $(DESC_SecMain)
  !insertmacro MUI_DESCRIPTION_TEXT ${SecUiEditor} $(DESC_SecUiEditor)
  !insertmacro MUI_DESCRIPTION_TEXT ${SecTextEditor} $(DESC_SecTextEditor)
!insertmacro MUI_FUNCTION_DESCRIPTION_END

