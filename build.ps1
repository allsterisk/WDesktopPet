param([string]$OutputPath)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$project = $PSScriptRoot
$source = Join-Path $project 'Pet.cs'
$binary = if ($OutputPath) { [System.IO.Path]::GetFullPath($OutputPath) } else { Join-Path $project 'WDesktopPet.exe' }
$manifest = Join-Path $project 'app.manifest'
$resources = @('w-sprites.png', 'heat-metal.png', 'luna-trigger.png', 'fang-joker.png') | ForEach-Object {
    $asset = Join-Path $project "assets\$_"
    "/resource:$asset,$_"
}
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/win32manifest:$manifest" $resources "/out:$binary" $source
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output "Built $binary"
