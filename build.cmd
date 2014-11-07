@echo Off

SET BASEDIR=%~dp0

set config=%1
if "%config%" == "" (
    set config=Release
)

%WINDIR%\Microsoft.NET\Framework\v4.0.30319\msbuild "%BASEDIR%Build\Build.proj" /p:Configuration="%config%";ExcludeXmlAssemblyFiles=false /v:M /fl /flp:LogFile="%BASEDIR%msbuild.log";Verbosity=Normal /nr:false /m
