:::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
:: Setup the console worker application/web site
:::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
::
:: A console worker consists of the console worker site and the binaries to run.
:: Tree looks like so:
:: site\
::   wwwroot\
::     global.asax     (this contains the worker site's logic)
::     web.config
::   bin\
::     run_worker.cmd  (the startup script for the worker process, this will contain the command to run as the worker)
::     %worker files%  (these are the deployed user files to use for the worker process)
::
:::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

@if "%SCM_TRACE_LEVEL%" NEQ "4" @echo off

setlocal enabledelayedexpansion

set BIN_DIR=%DEPLOYMENT_TEMP%\bin
set RUN_WORKER_COMMAND=%BIN_DIR%\run_worker.cmd

echo Setting up console worker site

:: Copy console worker web site's files
:::::::::::::::::::::::::::::::::::::::

copy /y "%~dp0Global.asax.template" "%DEPLOYMENT_TEMP%\Global.asax"
IF !ERRORLEVEL! NEQ 0 goto error

copy /y "%~dp0Web.config.template" "%DEPLOYMENT_TEMP%\Web.config"
IF !ERRORLEVEL! NEQ 0 goto error

:: Prepare run worker script
::::::::::::::::::::::::::::

mkdir "%BIN_DIR%" 2>nul

echo @echo off > "%RUN_WORKER_COMMAND%"
IF !ERRORLEVEL! NEQ 0 goto error

echo "%WORKER_COMMAND%" >> "%RUN_WORKER_COMMAND%"
IF !ERRORLEVEL! NEQ 0 goto error

goto end

:error
echo An error has occurred while setting up console worker
exit /b 1

:end
