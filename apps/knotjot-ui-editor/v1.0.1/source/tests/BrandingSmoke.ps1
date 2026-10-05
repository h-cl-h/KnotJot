$ErrorActionPreference = 'Stop'

$sourceRoot = [System.IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$versionRoot = Split-Path $sourceRoot -Parent

# Stop at the first named branding or packaging invariant failure.
# 首次违反指定品牌或打包约束时停止检查。
function Need {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

# Validate the current repair folder while retaining published compatibility identities.
# 验证当前修复目录，同时保留已发布兼容身份。
Need ((Split-Path $versionRoot -Leaf) -eq 'v1.0.1') 'version folder must use the v1.0.1 name'
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
# Bind each display label to its actual owner so unrelated documentation cannot hide a stale window title.
# 将显示标签绑定到实际所属文件，避免无关文档掩盖旧窗口标题。
$windowText = Get-Content -LiteralPath (Join-Path $sourceRoot 'MainWindow.xaml') -Raw -Encoding UTF8
$windowCode = Get-Content -LiteralPath (Join-Path $sourceRoot 'MainWindow.xaml.cs') -Raw -Encoding UTF8
$readmeText = Get-Content -LiteralPath (Join-Path $sourceRoot 'README.md') -Raw -Encoding UTF8
Need ($windowText -match 'Title="KnotJot UI Editor V1\.0\.1"') 'UI window must display the English V1.0.1 title'
Need ($windowText -match 'Text="V1\.0\.1"') 'UI window footer must display V1.0.1'
Need ($windowCode -match 'Title\s*=\s*"KnotJot UI Editor V1\.0\.1 — "\s*\+') 'UI title updates must retain the English V1.0.1 prefix'
Need ($readmeText -match '(?m)^# KnotJot UI Editor V1\.0\.1\r?$') 'published README must identify the current V1.0.1 product'

# Require stable assembly identity and a source-local protocol dependency.
# 要求稳定程序集身份与源码内协议依赖。
$projectText = Get-Content -LiteralPath $projectFile.FullName -Raw -Encoding UTF8
Need ($projectText -match '<AssemblyName>KnotJot界面编辑器</AssemblyName>') 'UI Editor assembly name is not KnotJot branded'
Need ($projectText -match '<Version>1\.0\.1</Version>') 'UI Editor project version is not 1.0.1'
Need ($projectText -match '<Product>KnotJot UI Editor</Product>') 'UI project must retain its English product label'
Need ($projectText -match '<Content Include="protocols\\ui-skin-protocol\.json"') 'UI protocol must be included from inside the published source tree'
Need ($projectText -notmatch '<Content Include="\.\.\\') 'UI project must not require content outside the published source tree'

# Check the format marker in the packaged protocol.
# 检查随程序发布的协议格式标记。
$protocolFile = Join-Path $sourceRoot 'protocols\ui-skin-protocol.json'
Need (Test-Path -LiteralPath $protocolFile -PathType Leaf) 'standalone UI protocol file is missing from the published source tree'
$protocol = Get-Content -LiteralPath $protocolFile -Raw -Encoding UTF8 | ConvertFrom-Json
Need ($protocol.format -eq 'knotjot-ui-skins') 'standalone UI protocol has the wrong format marker'

# Require the complete source-local publish tree, including the protocol, rather than an obsolete explicit file list.
# 要求递归包含完整源码本地发布目录及协议，不再依赖旧的显式文件列表。
$installerText = Get-Content -LiteralPath (Join-Path $sourceRoot 'installer.nsi') -Raw -Encoding UTF8
Need ($installerText -match '(?m)^OutFile "\.\.\\installers\\KnotJot-UI-Editor-Setup-1\.0\.1\.exe"\r?$') 'UI installer filename must identify version 1.0.1'
Need ($installerText -match '(?m)^\s*File /r "bin\\Release\\net8\.0-windows\\win-x64\\publish\\\*"\s*$') 'UI installer must recursively include its complete source-local publish output'
Need ($projectText -match '<Content Include="protocols\\ui-skin-protocol\.json"[^>]*CopyToPublishDirectory="PreserveNewest"') 'UI protocol must be copied into the recursively installed publish tree'
Need ($installerText -notmatch 'File "\.\.\\实时预览\\') 'UI installer must not depend on the release preview folder'

# Preserve the handshake alias understood by the first V1.0.0 main application.
# 保留最初 V1.0.0 主程序能够识别的握手别名。
$connectionText = Get-Content -LiteralPath (Join-Path $sourceRoot 'KnotJotConnection.cs') -Raw -Encoding UTF8
Need ($connectionText -match '--bmap-ui-skins-location-file=') 'V1.0.0-compatible UI handshake alias is missing'

Write-Output 'KNOTJOT_UI_EDITOR_BRANDING_OK'
