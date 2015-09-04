@ECHO OFF

:: 1. Prepare environment
SET DNVM_CMD_PATH_FILE="%USERPROFILE%\.dnx\temp-set-envvars.cmd"

:: 2. Install DNX
IF EXIST global.json (
    CALL PowerShell -NoProfile -NoLogo -ExecutionPolicy unrestricted -Command "[System.Threading.Thread]::CurrentThread.CurrentCulture = ''; [System.Threading.Thread]::CurrentThread.CurrentUICulture = '';$CmdPathFile='%DNVM_CMD_PATH_FILE%';& '%SCM_DNVM_PS_PATH%' " install -File global.json
    IF ERRORLEVEL 1 GOTO ERROR
) ELSE (
    CALL PowerShell -NoProfile -NoLogo -ExecutionPolicy unrestricted -Command "[System.Threading.Thread]::CurrentThread.CurrentCulture = ''; [System.Threading.Thread]::CurrentThread.CurrentUICulture = '';$CmdPathFile='%DNVM_CMD_PATH_FILE%';& '%SCM_DNVM_PS_PATH%' " install latest
    IF ERRORLEVEL 1 GOTO ERROR
)

:: 3. Put DNX on the path
IF EXIST %DNVM_CMD_PATH_FILE% (
    CALL %DNVM_CMD_PATH_FILE%
    DEL %DNVM_CMD_PATH_FILE%
)

:: 4. Run dnu restore
CALL dnu restore
IF ERRORLEVEL 1 GOTO ERROR

:: 5. Run the WebJob
CALL dnx run
IF ERRORLEVEL 1 GOTO ERROR

GOTO END

:ERROR
ENDLOCAL
ECHO An error has occurred during running DNX WebJob.
CALL :EXITSETERRORLEVEL
CALL :EXITFROMFUNCTION 2>NUL

:EXITSETERRORLEVEL
EXIT /b 1

:EXITFROMFUNCTION
()

:END
ECHO DNX WebJob ended
