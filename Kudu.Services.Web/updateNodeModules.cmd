@echo off
setlocal enabledelayedexpansion 

pushd %1

set attempts=5
set counter=0

:retry
set /a counter+=1
echo Attempt %counter% out of %attempts%

cmd /c npm install https://github.com/projectkudu/KuduScript/tarball/eddc60f7017a05a02bd9b19b3162ce58a10de81a
IF %ERRORLEVEL% NEQ 0 goto error

goto end

:error
if %counter% GEQ %attempts% goto :lastError
goto retry

:lastError
popd
echo An error has occured during npm install.
exit /b 1

:end
popd
echo Finished successfully.
exit /b 0
