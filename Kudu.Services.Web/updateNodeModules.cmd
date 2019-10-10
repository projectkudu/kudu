@echo off
setlocal enabledelayedexpansion

pushd %1

set attempts=5
set counter=0

:retry
set /a counter+=1
echo Attempt %counter% out of %attempts%

cmd /c npm install https://github.com/projectkudu/KuduScript/tarball/29e2dae68aa224f976a63af33319673a70480d4f
IF %ERRORLEVEL% NEQ 0 goto error

goto end

:error
if %counter% GEQ %attempts% goto :lastError
goto retry

:lastError
popd
echo An error has occurred during npm install.
exit /b 1

:end
popd
echo Finished successfully.
exit /b 0
