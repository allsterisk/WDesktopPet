$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.Application]::EnableVisualStyles()
$project = Split-Path -Parent $PSScriptRoot
$output = Join-Path $project 'verification'
New-Item -ItemType Directory -Path $output -Force | Out-Null
# Isolate this test from the user's position, size and selected skin.
$settings = Join-Path $output 'ui-smoke-settings.txt'
[System.IO.File]::WriteAllLines($settings, [string[]]@('40', '40', '2', 'False', 'cyclone-joker'))
$assembly = [System.Reflection.Assembly]::LoadFrom((Join-Path $project 'WDesktopPet.exe'))
$petType = $assembly.GetType('WDesktopPet.Pet', $true)
$constructor = $petType.GetConstructor([type[]]@([string]))
$form = $null
function Check([bool]$condition, [string]$reason) { if (-not $condition) { throw $reason } }
try {
    $form = $constructor.Invoke([object[]]@([string]$settings))
    $form.Show()
    [System.Windows.Forms.Application]::DoEvents()
    $skins = $form.ContextMenuStrip.Items | Where-Object Text -eq '切换皮肤'
    $sleep = $form.ContextMenuStrip.Items | Where-Object Text -eq '休息一下'
    $sizes = $form.ContextMenuStrip.Items | Where-Object Text -eq '桌宠大小'
    Check ($skins.DropDownItems.Count -eq 4) 'Expected four skin menu entries'
    $sleep.PerformClick()
    foreach ($item in $skins.DropDownItems) {
        $position = $form.Location
        $item.PerformClick()
        [System.Windows.Forms.Application]::DoEvents()
        Check ($sleep.Checked) 'Skin switch must preserve sleep'
        Check ($form.Location -eq $position) 'Skin switch must preserve position'
        Check (@($skins.DropDownItems | Where-Object Checked).Count -eq 1) 'Exactly one skin must be selected'
        Check ($item.Checked) 'Clicked skin must be checked'
        Check ($form.Text.Contains($item.Text)) 'Window title must reflect selected skin'
        Check (([System.IO.File]::ReadAllLines($settings))[4] -eq $item.Tag) 'Skin selection must persist'
        foreach ($size in $sizes.DropDownItems) {
            $size.PerformClick()
            [System.Windows.Forms.Application]::DoEvents()
            Check ($form.Width -in @(160, 320, 480)) 'Unexpected size'
            Check ($item.Checked) 'Resizing must preserve skin selection'
        }
    }
    $form.Close()
    $form.Dispose()
    $form = $constructor.Invoke([object[]]@([string]$settings))
    $form.Show()
    [System.Windows.Forms.Application]::DoEvents()
    $skins = $form.ContextMenuStrip.Items | Where-Object Text -eq '切换皮肤'
    $selected = @($skins.DropDownItems | Where-Object Checked)
    Check ($selected.Count -eq 1 -and $selected[0].Tag -eq 'fang-joker') 'Restart must restore FangJoker'
    Check ($form.Width -eq 480) 'Restart must restore size'
    [System.IO.File]::WriteAllText((Join-Path $output 'ui-smoke-result.txt'), 'PASS: native layered window; all four real skin-menu actions; exclusive selection; sleep and position preserved; all size-menu actions; saved skin and size restored on restart.')
    Get-Content (Join-Path $output 'ui-smoke-result.txt')
} finally {
    if ($null -ne $form) { $form.Close(); $form.Dispose() }
}
