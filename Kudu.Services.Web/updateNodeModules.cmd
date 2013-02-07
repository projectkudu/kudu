@echo off

pushd %1

set attempts=5
set counter=0

:retry
set /a counter+=1
echo Attempt %counter% out of %attempts%

if exist %1\node_modules\azure-cli\bin\azure (
  cmd /c npm update https://github.com/amitapl/azure-sdk-tools-xplat/tarball/kudu_s20
) else (
  cmd /c npm install https://github.com/amitapl/azure-sdk-tools-xplat/tarball/kudu_s20
)

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
