@echo off

setlocal enabledelayedexpansion

IF EXIST %POST_DEPLOYMENT_ACTIONS_DIR% (
  FOR /F %%A IN ('dir /s /b %POST_DEPLOYMENT_ACTIONS_DIR%\*.cmd %POST_DEPLOYMENT_ACTIONS_DIR%\*.bat') DO (
    set CURRENT_SCRIPT_FILE=%%A
    echo Executing - '!CURRENT_SCRIPT_FILE!'
    call !CURRENT_SCRIPT_FILE!
    IF !ERRORLEVEL! NEQ 0 goto error
    echo '!CURRENT_SCRIPT_FILE!' executed successfully
  )
)

goto end

:error
echo '!CURRENT_SCRIPT_FILE!' failed
exit /b 1

:end
