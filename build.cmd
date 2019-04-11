@echo Off
set config=%1
if "%config%" == "" (
    set config=Release
)

:: use MSBuild from .net framework by default
set MsBuildExe="%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild"

:: prefer vs2019, vs2017, vs2015 then vs2013
if exist "%PROGRAMFILES%\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe" (
    set MsBuildExe="%PROGRAMFILES%\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe"
) else if exist "%PROGRAMFILES(X86)%\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe" (
    set MsBuildExe="%PROGRAMFILES(X86)%\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe"
) else if exist "%PROGRAMFILES(X86)%\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe" (
    set MsBuildExe="%PROGRAMFILES(X86)%\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
) else if exist "%PROGRAMFILES(X86)%\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set MsBuildExe="%PROGRAMFILES(X86)%\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
) else if exist "%PROGRAMFILES%\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe" (
    set MsBuildExe="%PROGRAMFILES%\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe"
) else if exist "%PROGRAMFILES(X86)%\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe" (
    set MsBuildExe="%PROGRAMFILES(X86)%\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe"
) else if exist "%PROGRAMFILES(X86)%\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\MSBuild.exe" (
    set MsBuildExe="%PROGRAMFILES(X86)%\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\MSBuild.exe"
) else if exist "%PROGRAMFILES(X86)%\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe" (
    set MsBuildExe="%PROGRAMFILES(X86)%\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe"
) else if exist "%PROGRAMFILES%\MSBuild\14.0\Bin\MsBuild.exe" (
    set MsBuildExe="%PROGRAMFILES%\MSBuild\14.0\Bin\MsBuild.exe"
) else if exist "%PROGRAMFILES(X86)%\MSBuild\14.0\Bin\MsBuild.exe" (
    set MsBuildExe="%PROGRAMFILES(X86)%\MSBuild\14.0\Bin\MsBuild.exe"
) else if exist "%PROGRAMFILES%\MSBuild\12.0\Bin\MsBuild.exe" (
    set MsBuildExe="%PROGRAMFILES%\MSBuild\12.0\Bin\MsBuild.exe"
) else if exist "%PROGRAMFILES(X86)%\MSBuild\12.0\Bin\MsBuild.exe" (
    set MsBuildExe="%PROGRAMFILES(X86)%\MSBuild\12.0\Bin\MsBuild.exe"
)
echo %MsBuildExe%
%MsBuildExe% Build\Build.proj /p:Configuration="%config%";ExcludeXmlAssemblyFiles=false /v:M /fl /flp:LogFile=msbuild.log;Verbosity=Normal /nr:false /m