@echo Off

SET BASEDIR=%~dp0

set config=%1
if "%config%" == "" (
    set config=Release
)

if exist "%ProgramFiles%\MSBuild\12.0\Bin\msbuild.exe" (
  set MsBuildPath="%ProgramFiles%\MSBuild\12.0\Bin\msbuild.exe"
) else if defined ProgramFiles(x86) if exist "%ProgramFiles(x86)%\MSBuild\12.0\Bin\msbuild.exe" (
  set MsBuildPath="%ProgramFiles(x86)%\MSBuild\12.0\Bin\msbuild.exe"
) else (
  set MsBuildPath="%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe"
)

%MsBuildPath% "%BASEDIR%Build\Build.proj" /p:Configuration="%config%";ExcludeXmlAssemblyFiles=false /v:M /fl /flp:LogFile="%BASEDIR%msbuild.log";Verbosity=Normal /nr:false /m
