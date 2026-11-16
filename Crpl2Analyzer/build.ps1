# 构建脚本：用系统自带的 .NET Framework 编译器生成单文件 exe
# 无需安装任何 SDK / 运行时，Windows 10/11 自带 .NET Framework 4.8
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::GetEncoding(936) } catch { }
$root = $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw "找不到 csc.exe" }

$src = Get-ChildItem (Join-Path $root 'src\*.cs') | ForEach-Object { $_.FullName }
$out = Join-Path $root 'Crpl2Analyzer.exe'
$icon = Join-Path $root 'app.ico'

$args = @(
    '/nologo', '/target:winexe', '/optimize+', '/platform:anycpu', '/langversion:5',
    ('/out:' + $out),
    '/reference:System.dll,System.Drawing.dll,System.Windows.Forms.dll,System.Core.dll',
    '/nowarn:219,169,414,649'
)
if (Test-Path $icon) { $args += ('/win32icon:' + $icon) }
$args += $src

& $csc $args
if ($LASTEXITCODE -ne 0) { throw "编译失败 (exit $LASTEXITCODE)" }
Write-Host ("构建成功: " + $out + "  (" + [math]::Round((Get-Item $out).Length / 1KB, 1) + " KB)")
