$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$project = $PSScriptRoot
$source = Join-Path $project 'Pet.cs'
$sprite = Join-Path $project 'assets\w-sprites.png'
$binary = Join-Path $project 'WDesktopPet.exe'
$manifest = Join-Path $project 'app.manifest'
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/win32manifest:$manifest" "/resource:$sprite,w-sprites.png" "/out:$binary" $source
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output "Built $project/WDesktopPet.exe"
