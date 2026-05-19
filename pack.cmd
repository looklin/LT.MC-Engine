@echo off
REM 打包脚本：生成 .nupkg 文件
REM 使用方法：pack.cmd

echo ========================================
echo   MC Engine NuGet Package Builder
echo ========================================
echo.

REM 创建输出目录
if not exist "nuget-packages" mkdir nuget-packages

REM 打包轻量模式
echo [1/2] 打包轻量模式项目...
cd MC.Engine.Light
dotnet pack src/MC.Engine.Light.csproj -c Release -o ..\nuget-packages --no-restore
if errorlevel 1 (
    echo 错误: 轻量模式打包失败
    cd ..
    pause
    exit /b 1
)
cd ..
echo   完成: 轻量模式打包成功
echo.

REM 打包完整模式
echo [2/2] 打包完整模式项目...
cd MC.Engine
dotnet pack src/MC.Engine.csproj -c Release -o ..\nuget-packages --no-restore
if errorlevel 1 (
    echo 错误: 完整模式打包失败
    cd ..
    pause
    exit /b 1
)
cd ..
echo   完成: 完整模式打包成功
echo.

echo ========================================
echo   打包结果
echo ========================================
dir nuget-packages\*.nupkg
echo.
echo 打包完成！文件位于: nuget-packages/
pause
