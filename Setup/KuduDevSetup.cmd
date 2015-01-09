@echo off
setlocal

call :InstallDependencies
call :SetProfileEnv
call :CopyDependenciesFromProgramFileX86ToProgramFile
call :AddLegacyDependencies

:: work around for npm bug that it doesn`t create npm folder under user folder
set npmUserPath="%SYSTEMDRIVE%\Users\%username%\AppData\Roaming\npm"
if not exist %npmUserPath% (
    md %npmUserPath%
)

goto :EOF

:::::::::::::::::::::::::::::::::::::::::::::::::::::::::: InstallDependencies Begin :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
:InstallDependencies
set webpicmd="%ProgramFiles%\Microsoft\Web Platform Installer\WebPiCmd.exe"
if exist %webpicmd% (
    %webpicmd% /Install  /SuppressReboot /Products:KuduDevSetup /Log:"%~dp0WebPiCmdSetup.log" /Feeds:"%~dp0KuduSetupCustomWebPiFeed.xml"
) else (
    call :ColorText 0c "Microsoft Web Platform Installer is not installed."
    echo Please install Microsoft Web Platform Installer from http://www.microsoft.com/web/downloads/platform.aspx
)
goto :EOF
:::::::::::::::::::::::::::::::::::::::::::::::::::::::::: InstallDependencies End :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

:::::::::::::::::::::::::::::::::::::::::::::::::::::::::: SetProfileEnv Begin :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
:SetProfileEnv
echo.
:: set "setProfileEnvironment" to be true or remote if current OS is Windows 8/1 (https://github.com/projectkudu/kudu/wiki/Getting-started#additional-prerequisites-to-run-the-functional-tests)
set Version=
for /f "skip=1" %%v in ('wmic os get version') do if not defined Version set Version=%%v
for /f "delims=. tokens=1-3" %%a in ("%Version%") do (
  set Version.Major=%%a
  set Version.Minor=%%b
  set Version.Build=%%c
)

set GTR_EQ_WIN_81=
if %Version.Major%==6 if %Version.Minor% GTR 2 set GTR_EQ_WIN_81=1
if %Version.Major% GTR 6 set GTR_EQ_WIN_81=1

if defined GTR_EQ_WIN_81 (
    echo Removing applicationPoolDefaults.processModel.setProfileEnvironment attribute, since you are running on Windows 8.1
    %systemroot%\system32\inetsrv\APPCMD clear config -section:system.applicationHost/applicationPools /applicationPoolDefaults.processModel.setProfileEnvironment /commit:apphost
) else (
    :: set setProfileEnvironment to be true
    echo Updating 'applicationPoolDefaults.processModel.setProfileEnvironment' to be true
    %systemroot%\system32\inetsrv\APPCMD set config -section:system.applicationHost/applicationPools /applicationPoolDefaults.processModel.setProfileEnvironment:"true" /commit:apphost
)
goto :EOF
:::::::::::::::::::::::::::::::::::::::::::::::::::::::::: SetProfileEnv End :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

:::::::::::::::::::::::::::::::::::::::::::::::::::::::::: CopyDependenciesFromProgramFileX86ToProgramFile Begin :::::::::::::::::::::::::::::::::::::::::::::::
:CopyDependenciesFromProgramFileX86ToProgramFile
:: nodejs
set nodejsX86Path="%PROGRAMFILES(X86)%\nodejs"
set nodejsX64Path="%PROGRAMFILES%\nodejs"
if not exist %nodejsX86Path% (
    echo Copying %nodejsX64Path% to %nodejsX86Path%
    md %nodejsX86Path%
    xcopy %nodejsX64Path% %nodejsX86Path% /E /H /R /V /C /Q /Y
)

:: mercurial
set mercurialX86Path="%PROGRAMFILES(X86)%\Mercurial"
set mercurialX64Path="%PROGRAMFILES%\Mercurial"
if not exist %mercurialX86Path% (
    echo Copying %mercurialX64Path% to %mercurialX86Path%
    md %mercurialX86Path%
    xcopy %mercurialX64Path% %mercurialX86Path% /E /H /R /V /C /Q /Y
)

goto :EOF
:::::::::::::::::::::::::::::::::::::::::::::::::::::::::: CopyDependenciesFromProgramFileX86ToProgramFile End :::::::::::::::::::::::::::::::::::::::::::::::::

:::::::::::::::::::::::::::::::::::::::::::::::::::::::::: AddLegacyDependencies Begin :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
:::::::::::::::::::::::::::::::::::::::::::::::::::::::::: Depending on Git installation :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
:AddLegacyDependencies
:: install test case dependencies
if not exist "%PROGRAMFILES(X86)%\Git\bin" (
    call :ColorText 0c "Git is not installed, please re-run KuduDevSetup.cmd again."
    goto :EOF
)

echo.
set curlExe="%PROGRAMFILES(X86)%\Git\bin\curl.exe"
set uzipExe="%PROGRAMFILES(X86)%\Git\bin\unzip.exe"

:: *** old VS binary *** 
%curlExe% -S -s -o OldVSFiles.zip https://dl.dropboxusercontent.com/u/2209341/Kudu_Progx86_Msbuild_Microsoft_VisualStudio.zip
%uzipExe% -q -o OldVSFiles.zip -d OldVSFilesContainer

:: copy over old VS binary to MSBuild, some of the test cases are depending on them
set msbuildVSPath="%PROGRAMFILES(X86)%\MSBuild\Microsoft\VisualStudio"
if not exist %msbuildVSPath% (
    echo creating new folder %msbuildVSPath%
    md %msbuildVSPath%
)

:: perform copy
echo Adding old vs target file to %msbuildVSPath%
xcopy "OldVSFilesContainer\*" %msbuildVSPath% /E /H /R /V /C /Q /Y
:: clean up
rmdir OldVSFilesContainer /S /Q
del OldVSFiles.zip /F /Q

:: *** old nodejs exe *** 
set node082Path="%PROGRAMFILES(X86)%\nodejs\0.8.2"
set node0105Path="%PROGRAMFILES(X86)%\nodejs\0.10.5"
%curlExe% -S -s -o node.exe http://nodejs.org/dist/v0.8.2/node.exe

if not exist %node082Path% (
    echo creating new folder %node082Path%
    md %node082Path%
)

if not exist %node0105Path% (
    echo creating new folder %node0105Path%
    md %node0105Path%
)

:: perform copy
echo Adding node 0.8.2 and 0.10.5 to %node082Path% and %node0105Path%
xcopy node.exe %node082Path% /E /H /R /V /C /Q /Y
xcopy node.exe %node0105Path% /E /H /R /V /C /Q /Y
:: clean up
del node.exe /F /Q

:: *** System.Web.Mvc.dll *** 
echo Installing System.Web.Mvc.dll 3.0.0.0 to GAC
set gacUtilExe="%PROGRAMFILES(X86)%\Microsoft SDKs\Windows\v8.1A\bin\NETFX 4.5.1 Tools\x64\gacutil.exe"
if not exist %gacUtilExe% (
    set gacUtilExe="%PROGRAMFILES(X86)%\Microsoft SDKs\Windows\v8.1A\bin\NETFX 4.5.1 Tools\gacutil.exe"
) 
if not exist %gacUtilExe% (
    set gacUtilExe="%PROGRAMFILES(X86)%\Microsoft SDKs\Windows\v8.0A\bin\NETFX 4.0 Tools\x64\gacutil.exe"
) 
if not exist %gacUtilExe% (
    set gacUtilExe="%PROGRAMFILES(X86)%\Microsoft SDKs\Windows\v8.0A\bin\NETFX 4.0 Tools\gacutil.exe"
) 
if not exist %gacUtilExe% (
    set gacUtilExe="%PROGRAMFILES(X86)%\Microsoft SDKs\Windows\v7.0A\Bin\x64\gacutil.exe"
) 
if not exist %gacUtilExe% (
    set gacUtilExe="%PROGRAMFILES(X86)%\Microsoft SDKs\Windows\v7.0A\Bin\gacutil.exe"
) 
if not exist %gacUtilExe% (
    call :ColorText 0c "Missing gacutil.exe, failed to install ver 3.0.0.0 System.Web.Mvc.dll to your machine"
)

%curlExe% -S -s -o System.Web.Mvc.dll https://dl.dropboxusercontent.com/u/2209341/System.Web.Mvc.dll
%gacUtilExe% /i System.Web.Mvc.dll
:: clean up
del System.Web.Mvc.dll /F /Q

goto :EOF
:::::::::::::::::::::::::::::::::::::::::::::::::::::::::: AddLegacyDependencies End :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

:::::::::::::::::::::::::::::::::::::::::::::::::::::::::: ColorText Begin :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
:ColorText
for /F "tokens=1,2 delims=#" %%a in ('"prompt #$H#$E# & echo on & for %%b in (1) do rem"') do (
  set "DEL=%%a"
)

<nul set /p ".=%DEL%" > "%~2"
findstr /v /a:%1 /R "^$" "%~2" nul
del "%~2" > nul 2>&1
echo.
goto :EOF
:::::::::::::::::::::::::::::::::::::::::::::::::::::::::: ColorText End :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

:EOF
