@if "%_echo%" neq "1" @echo off

setlocal
set _SCRIPT=%~nx0
set _KUDUZIP=%~dp0Kudu.zip
set _SCMURI=%~1
set _CURLEXE=%ProgramFiles(x86)%\git\bin\curl.exe

REM first parameter is the deploy uri with embedded cred
if "%_SCMURI%" equ "" (
  call :USAGE 
  goto :EOF
)

REM remove any after .net
set _SCMURI=%_SCMURI:.net=&rem.%
set _SCMURI=%_SCMURI%.net

if NOT EXIST "%_KUDUZIP%" (
  @echo "%_KUDUZIP%" does not exists! 
  goto :EOF
)

if NOT EXIST "%_CURLEXE%" (
  @echo "%_CURLEXE%" does not exists! 
  goto :EOF
)

@echo.
@echo "%_CURLEXE%" -k -v -T "%_KUDUZIP%" "%_SCMURI%/zip"
@echo.
@call "%_CURLEXE%" -k -v -T "%_KUDUZIP%" "%_SCMURI%/zip"
@echo.
@call "%_CURLEXE%" -k -X DELETE "%_SCMURI%/diagnostics/processes/0" >nul 2>&1
if "%ERRORLEVEL%" equ "0" (
  @echo - w3wp.exe restarted 
)

exit /b 0

:USAGE
@echo usage: %_SCRIPT% "<scm_uri>"
exit /b 0
 
REM testing