$ErrorActionPreference = 'Stop'

$fw   = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$csc  = Join-Path $fw 'csc.exe'
$wpf  = Join-Path $fw 'WPF'
$meta = 'C:\Windows\System32\WinMetadata'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$out  = Join-Path $root 'bin\SideDrawer.exe'

New-Item -ItemType Directory -Force -Path (Join-Path $root 'bin') | Out-Null

$refs = @(
    (Join-Path $wpf  'WindowsBase.dll'),
    (Join-Path $wpf  'PresentationCore.dll'),
    (Join-Path $wpf  'PresentationFramework.dll'),
    (Join-Path $fw   'System.dll'),
    (Join-Path $fw   'System.Core.dll'),
    (Join-Path $fw   'System.Drawing.dll'),
    (Join-Path $fw   'System.Web.Extensions.dll'),
    (Join-Path $fw   'System.Xaml.dll'),
    (Join-Path $fw   'System.Runtime.dll'),
    (Join-Path $fw   'System.Runtime.WindowsRuntime.dll'),
    (Join-Path $meta 'Windows.Foundation.winmd'),
    (Join-Path $meta 'Windows.Media.winmd'),
    (Join-Path $meta 'Windows.Storage.winmd')
)

$args = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    ('/out:' + $out),
    ('/win32manifest:' + (Join-Path $root 'app.manifest')),
    ('/win32icon:' + (Join-Path $root 'app.ico'))
)
foreach ($r in $refs) {
    if (Test-Path -LiteralPath $r) { $args += ('/r:"' + $r + '"') }
    else { Write-Host "missing ref: $r" -ForegroundColor DarkYellow }
}

$sources = Get-ChildItem -Path $root -Filter '*.cs' -File | Where-Object { $_.Name -ne 'smtc_test.cs' }
foreach ($s in $sources) { $args += ('"' + $s.FullName + '"') }

& $csc @args
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED ($LASTEXITCODE)" -ForegroundColor Red; exit $LASTEXITCODE }
Write-Host "OK -> $out" -ForegroundColor Green
