$ErrorActionPreference = 'Stop'

$sourceRoot = [System.IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$versionRoot = Split-Path $sourceRoot -Parent

# EN: Fail immediately when a branding or layout invariant is violated.
# ZH: 品牌或目录约定不满足时立即失败。
function Need {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

# EN: Check normalized product/version folders while preserving the user-facing application identity.
# ZH: 检查规范化产品版本目录，同时保留用户可见的应用身份。
Need ((Split-Path $versionRoot -Leaf) -eq 'v1.0.1') 'version folder must use the normalized v1.0.1 name'
Need ((Split-Path (Split-Path $versionRoot -Parent) -Leaf) -eq 'knotjot-text-style-editor') 'product folder must use the normalized knotjot-text-style-editor slug'
Need (Test-Path -LiteralPath (Join-Path $sourceRoot 'KnotJot.TextStyleEditor.csproj')) 'normalized Text Style Editor project filename is missing'
$projectFile = Get-ChildItem -LiteralPath $sourceRoot -File -Filter '*.csproj' | Where-Object {
    (Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8) -match '<AssemblyName>KnotJot'
} | Select-Object -First 1
Need ($null -ne $projectFile) 'KnotJot Text Style Editor project file is missing'

# EN: Inspect maintained user-visible sources for versioned branding and old-product leakage.
# ZH: 检查维护的用户可见源码是否具备版本品牌且未残留旧产品名称。
$visibleFiles = @(
    'MainWindow.xaml',
    'MainWindow.xaml.cs',
    'README.md',
    'THIRD_PARTY_NOTICES.md',
    'installer.nsi'
)
$visibleText = ($visibleFiles | ForEach-Object {
    Get-Content -LiteralPath (Join-Path $sourceRoot $_) -Raw -Encoding UTF8
}) -join "`n"

Need ($visibleText -notmatch 'BMAP 文本框样式编辑器|BMAP Text (Box )?Style Editor|BMAP文本框样式编辑器\.exe|BMAP-Text-Style-Editor') 'visible Text Style Editor branding still contains the old BMAP product name'
Need ($visibleText -match 'KnotJot Text Style Editor V1\.0\.1') 'KnotJot Text Style Editor V1.0.1 title is missing'
Need ($visibleText -match 'KnotJot-Text-Style-Editor-Setup-1\.0\.1\.exe') 'KnotJot Text Style Editor installer filename is missing'

# EN: Check the unchanged binary assembly identity and product version independently of the project filename.
# ZH: 独立于项目文件名检查未改变的二进制程序集身份和产品版本。
$projectText = Get-Content -LiteralPath $projectFile.FullName -Raw -Encoding UTF8
Need ($projectText -match '<AssemblyName>KnotJot文本框样式编辑器</AssemblyName>') 'Text Style Editor assembly name is not KnotJot branded'
Need ($projectText -match '<Version>1\.0\.1</Version>') 'Text Style Editor project version is not 1.0.1'

Write-Output 'KNOTJOT_TEXT_STYLE_EDITOR_BRANDING_OK'
