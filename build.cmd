@echo Off
set config=%1
if "%config%" == "" (
    set config=Release
)

:: use MSBuild from .net framework by default
set MsBuildExe="%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild"

:: prefer vs2015 then vs2013
if exist "%PROGRAMFILES%\MSBuild\14.0\Bin\MsBuild.exe" (
    set MsBuildExe="%PROGRAMFILES%\MSBuild\14.0\Bin\MsBuild.exe"
) else if exist "%PROGRAMFILES(X86)%\MSBuild\14.0\Bin\MsBuild.exe" (
    set MsBuildExe="%PROGRAMFILES(X86)%\MSBuild\14.0\Bin\MsBuild.exe"
) else if exist "%PROGRAMFILES%\MSBuild\12.0\Bin\MsBuild.exe" (
    set MsBuildExe="%PROGRAMFILES%\MSBuild\12.0\Bin\MsBuild.exe"
) else if exist "%PROGRAMFILES(X86)%\MSBuild\12.0\Bin\MsBuild.exe" (
    set MsBuildExe="%PROGRAMFILES(X86)%\MSBuild\12.0\Bin\MsBuild.exe"
)

%MsBuildExe% Build\Build.proj /p:Configuration="%config%";ExcludeXmlAssemblyFiles=false /v:M /fl /flp:LogFile=msbuild.log;Verbosity=Normal /nr:false /m