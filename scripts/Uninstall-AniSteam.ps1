$ErrorActionPreference = 'Stop'
$rootPath = [System.IO.Path]::GetFullPath($PSScriptRoot)
$configPath = Join-Path $rootPath 'app\Ani.cfg'
$patchPath = [System.IO.Path]::GetFullPath((Join-Path $rootPath 'app\ani-steam'))
$expectedPatchPath = [System.IO.Path]::GetFullPath((Join-Path $rootPath 'app\ani-steam'))
if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
    throw 'Run this script from the directory containing Ani.exe and app\Ani.cfg.'
}
foreach ($process in (Get-Process Ani,AniSteam -ErrorAction SilentlyContinue)) {
    if ($process.Path -and $process.Path.StartsWith($rootPath + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Close Animeko and AniSteam before uninstalling the patch.'
    }
}
$text = [System.IO.File]::ReadAllText($configPath)
$agentLine = 'java-options=-javaagent:$APPDIR\ani-steam\ani-steam-agent.jar'
$pattern = '(?m)^' + [regex]::Escape($agentLine) + '(?:\r?\n|$)'
$filteredText = [regex]::Replace($text, $pattern, '')
if ($filteredText -eq $text -and $text.Contains('ani-steam-agent.jar')) {
    throw 'The agent option was customized. Remove that option from app\Ani.cfg manually before deleting app\ani-steam.'
}
if ($filteredText -ne $text) {
    $backupPath = $configPath + '.before-anisteam-uninstall'
    Copy-Item -LiteralPath $configPath -Destination $backupPath -Force
    $temporaryPath = $configPath + '.anisteam-tmp'
    [System.IO.File]::WriteAllText($temporaryPath, $filteredText, [System.Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporaryPath -Destination $configPath -Force
}
if ($patchPath -ne $expectedPatchPath -or -not $patchPath.StartsWith($rootPath + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Patch directory is outside the installation.'
}
if (Test-Path -LiteralPath $patchPath) {
    $item = Get-Item -LiteralPath $patchPath -Force
    if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
        throw 'Refusing to remove a patch directory that is a junction or symbolic link.'
    }
    Remove-Item -LiteralPath $patchPath -Recurse -Force
}
Write-Host 'AniSteam removed. You can start Ani.exe normally.'
Write-Host 'Preferences and encrypted Steam login remain in %LOCALAPPDATA%\AniSteam.'
