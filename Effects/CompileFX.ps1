# 自动编译所有缺少.xnb和.fxc的.fx文件
# 用法：在Assets/Effects目录下运行，或指定目录参数
# 示例：.\CompileFX.ps1
#        .\CompileFX.ps1 -Dir "C:\path\to\effects"

param(
    [string]$Dir = $PSScriptRoot,
    [string]$Compiler = "C:\Users\Hommeng\Documents\My Games\Terraria\tModLoader\FXC\fxc.exe"
)

if (-not (Test-Path $Compiler)) {
    Write-Host "[错误] 找不到fxc.exe: $Compiler" -ForegroundColor Red
    exit 1
}

$fxFiles = Get-ChildItem -Path $Dir -Filter "*.fx" -File
if ($fxFiles.Count -eq 0) {
    Write-Host "[提示] 目录中没有.fx文件: $Dir" -ForegroundColor Yellow
    exit 0
}

$compiled = 0
$skipped = 0
$failed = 0

foreach ($fx in $fxFiles) {
    $baseName = $fx.BaseName
    $xnbPath = Join-Path $Dir "$baseName.xnb"
    $fxcPath = Join-Path $Dir "$baseName.fxc"

    if ((Test-Path $xnbPath) -or (Test-Path $fxcPath)) {
        Write-Host "[跳过] $($fx.Name) (已存在 $( if(Test-Path $xnbPath){'xnb'}else{'fxc'} ))" -ForegroundColor DarkGray
        $skipped++
        continue
    }

    Write-Host "[编译] $($fx.Name) -> $baseName.fxc" -ForegroundColor Cyan -NoNewline
    $output = & $Compiler /T fx_2_0 /Fo $fxcPath $fx.FullName 2>&1
    if (Test-Path $fxcPath) {
        Write-Host "  OK" -ForegroundColor Green
        $compiled++
    } else {
        Write-Host "  失败" -ForegroundColor Red
        $output | Where-Object { $_ -match "error" } | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
        $failed++
    }
}

Write-Host ""
Write-Host "完成: 编译 $compiled, 跳过 $skipped, 失败 $failed" -ForegroundColor White
