@echo off

setlocal enabledelayedexpansion

IF EXIST %POST_DEPLOYMENT_ACTIONS_DIR% (
  FOR /F %%A IN ('dir /s /b %POST_DEPLOYMENT_ACTIONS_DIR%\*.cmd %POST_DEPLOYMENT_ACTIONS_DIR%\*.bat %POST_DEPLOYMENT_ACTIONS_DIR%\*.ps1') DO (
    set CURRENT_SCRIPT_FILE=%%A
    echo Executing - '!CURRENT_SCRIPT_FILE!'
    :: Get last four chars and check if file end with '.ps1' (powershell file)
    IF /I "!CURRENT_SCRIPT_FILE:~-4!"==".ps1" (
        call PowerShell -NoProfile -NoLogo -ExecutionPolicy RemoteSigned "!CURRENT_SCRIPT_FILE!"
    ) else (
        call "!CURRENT_SCRIPT_FILE!"
    )
    IF !ERRORLEVEL! NEQ 0 goto error
    echo '!CURRENT_SCRIPT_FILE!' executed successfully
  )
)

goto end

:error
echo '!CURRENT_SCRIPT_FILE!' failed
exit /b 1

:end
