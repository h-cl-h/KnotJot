$ErrorActionPreference = 'Stop'

$sourceRoot = [System.IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$versionRoot = Split-Path $sourceRoot -Parent

# Stop at the first named branding or packaging invariant failure.
# 首次违反指定品牌或打包约束时停止检查。
function Need {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

# Validate migrated version folders while retaining published product branding.
# 验证迁移后的版本目录，并保留已发布产品品牌。
Need ((Split-Path $versionRoot -Leaf) -eq 'v1.0.0') 'version folder must use the v1.0.0 name'
Need ((Split-Path (Split-Path $versionRoot -Parent) -Leaf) -eq 'knotjot-ui-editor') 'product folder must use the knotjot-ui-editor slug'
# Select the application project by its retained branded assembly identity.
# 根据保留的品牌程序集身份选择应用工程。
$projectFile = Get-ChildItem -LiteralPath $sourceRoot -File -Filter '*.csproj' | Where-Object {
    # Match the application assembly within candidate project files.
    # 在候选工程中匹配应用程序集。
    (Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8) -match '<AssemblyName>KnotJot'
} | Select-Object -First 1
Need ($null -ne $projectFile) 'KnotJot UI Editor project file is missing'

# Inspect public labels, documentation, generated CSS branding, and installer metadata.
# 检查公开标签、文档、生成 CSS 品牌与安装器元数据。
$visibleFiles = @(
    'MainWindow.xaml',
    'MainWindow.xaml.cs',
    'CssBuilder.cs',
    'README.md',
    'THIRD_PARTY_NOTICES.md',
    'installer.nsi'
)
$visibleText = ($visibleFiles | ForEach-Object {
    # Read one visible-branding source for the combined assertion.
    # 为组合断言读取一个可见品牌来源。
    Get-Content -LiteralPath (Join-Path $sourceRoot $_) -Raw -Encoding UTF8
}) -join "`n"

Need ($visibleText -notmatch 'BMAP 界面编辑器|BMAP UI Editor|BMAP界面编辑器\.exe|BMAP-UI-Editor') 'visible UI Editor branding still contains the old BMAP product name'
Need ($visibleText -match 'KnotJot 界面编辑器 V1\.0\.0') 'KnotJot UI Editor V1.0.0 title is missing'
Need ($visibleText -match 'KnotJot-UI-Editor-Setup-1\.0\.0\.exe') 'KnotJot UI Editor installer filename is missing'

# Require stable assembly identity and a source-local protocol dependency.
# 要求稳定程序集身份与源码内协议依赖。
$projectText = Get-Content -LiteralPath $projectFile.FullName -Raw -Encoding UTF8
Need ($projectText -match '<AssemblyName>KnotJot界面编辑器</AssemblyName>') 'UI Editor assembly name is not KnotJot branded'
Need ($projectText -match '<Version>1\.0\.0</Version>') 'UI Editor project version is not 1.0.0'
Need ($projectText -match '<Content Include="protocols\\ui-skin-protocol\.json"') 'UI protocol must be included from inside the published source tree'
Need ($projectText -notmatch '<Content Include="\.\.\\') 'UI project must not require content outside the published source tree'

# Check the format marker in the packaged protocol.
# 检查随程序发布的协议格式标记。
$protocolFile = Join-Path $sourceRoot 'protocols\ui-skin-protocol.json'
Need (Test-Path -LiteralPath $protocolFile -PathType Leaf) 'standalone UI protocol file is missing from the published source tree'
$protocol = Get-Content -LiteralPath $protocolFile -Raw -Encoding UTF8 | ConvertFrom-Json
Need ($protocol.format -eq 'knotjot-ui-skins') 'standalone UI protocol has the wrong format marker'

# Require installer inputs from source-local publish output.
# 要求安装器输入来自源码本地发布输出。
$installerText = Get-Content -LiteralPath (Join-Path $sourceRoot 'installer.nsi') -Raw -Encoding UTF8
Need ($installerText -match 'File "bin\\Release\\net8\.0-windows\\win-x64\\publish\\KnotJot界面编辑器\.exe"') 'UI installer must consume the source-local publish output'
Need ($installerText -match 'File "bin\\Release\\net8\.0-windows\\win-x64\\publish\\protocols\\ui-skin-protocol\.json"') 'UI installer must include the published protocol definition'
Need ($installerText -notmatch 'File "\.\.\\实时预览\\') 'UI installer must not depend on the release preview folder'

# Preserve the handshake alias understood by the first V1.0.0 main application.
# 保留最初 V1.0.0 主程序能够识别的握手别名。
$connectionText = Get-Content -LiteralPath (Join-Path $sourceRoot 'KnotJotConnection.cs') -Raw -Encoding UTF8
Need ($connectionText -match '--bmap-ui-skins-location-file=') 'V1.0.0-compatible UI handshake alias is missing'

Write-Output 'KNOTJOT_UI_EDITOR_BRANDING_OK'
