@echo off
setlocal enabledelayedexpansion

pushd %1

set attempts=5
set counter=0

:retry
set /a counter+=1
echo Attempt %counter% out of %attempts%

cmd /c npm install https://github.com/projectkudu/KuduScript/tarball/dea230f29169cadd1990e7b9ab9da1375d446ca2
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
