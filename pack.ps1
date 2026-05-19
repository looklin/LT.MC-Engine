# 打包脚本：生成 .nupkg 文件
# 使用方法：.\pack.ps1

$ErrorActionPreference = "Stop"

# 配置变量
$Configuration = "Release"
$OutputDir = Join-Path $PSScriptRoot "nuget-packages"

# 创建输出目录
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

# 清空旧文件
Get-ChildItem -Path $OutputDir -Filter "*.nupkg" | Remove-Item -Force

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  MC Engine NuGet Package Builder" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 打包轻量模式
Write-Host "[1/2] 打包轻量模式项目..." -ForegroundColor Yellow
$LightProj = Join-Path $PSScriptRoot "MC.Engine.Light/src/MC.Engine.Light.csproj"
dotnet pack $LightProj -c $Configuration -o $OutputDir --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "错误: 轻量模式打包失败" -ForegroundColor Red
    exit 1
}
Write-Host "  完成: 轻量模式打包成功" -ForegroundColor Green
Write-Host ""

# 打包完整模式
Write-Host "[2/2] 打包完整模式项目..." -ForegroundColor Yellow
$FullProj = Join-Path $PSScriptRoot "MC.Engine/src/MC.Engine.csproj"
dotnet pack $FullProj -c $Configuration -o $OutputDir --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "错误: 完整模式打包失败" -ForegroundColor Red
    exit 1
}
Write-Host "  完成: 完整模式打包成功" -ForegroundColor Green
Write-Host ""

# 列出输出文件
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  打包结果" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Get-ChildItem -Path $OutputDir -Filter "*.nupkg" | ForEach-Object {
    $sizeKB = [math]::Round($_.Length / 1KB, 2)
    Write-Host "  $_ ($sizeKB KB)" -ForegroundColor White
}
Write-Host ""
Write-Host "打包完成！文件位于: $OutputDir" -ForegroundColor Green
