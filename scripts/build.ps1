[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $repoRoot 'artifacts'
$framework64 = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework32 = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$compiler = if (Test-Path -LiteralPath $framework64) { $framework64 } else { $framework32 }

if (-not (Test-Path -LiteralPath $compiler)) {
    throw '.NET Framework 4.x C# compiler not found.'
}

New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
Get-ChildItem -LiteralPath $artifacts -File -ErrorAction SilentlyContinue | Remove-Item -Force

$app = Join-Path $artifacts 'ParsecAzertyFix-PORTABLE.exe'
$setup = Join-Path $artifacts 'ParsecAzertyFix-INSTALLER.exe'

& $compiler /nologo /target:winexe /platform:anycpu /optimize+ `
    /out:$app `
    /reference:System.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    (Join-Path $repoRoot 'src\AssemblyInfo.cs') `
    (Join-Path $repoRoot 'src\ParsecAzertyFix.cs')
if ($LASTEXITCODE -ne 0) { throw "Application build failed with exit code $LASTEXITCODE." }

& $compiler /nologo /target:winexe /platform:anycpu /optimize+ `
    /out:$setup `
    /reference:System.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    /resource:"$app,ParsecAzertyFix.Payload.exe" `
    (Join-Path $repoRoot 'installer\AssemblyInfo.cs') `
    (Join-Path $repoRoot 'installer\Installer.cs')
if ($LASTEXITCODE -ne 0) { throw "Installer build failed with exit code $LASTEXITCODE." }

$hashLines = Get-FileHash -LiteralPath $app, $setup -Algorithm SHA256 |
    ForEach-Object { '{0}  {1}' -f $_.Hash.ToLowerInvariant(), (Split-Path -Leaf $_.Path) }
$hashLines | Set-Content -LiteralPath (Join-Path $artifacts 'SHA256-CHECKSUMS.txt') -Encoding ascii

Get-ChildItem -LiteralPath $artifacts | Select-Object Name, Length, LastWriteTime
