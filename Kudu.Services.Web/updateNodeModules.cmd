@echo off

pushd %1

set attempts=5
set counter=0

:retry
set /a counter+=1
echo Attempt %counter% out of %attempts%

if exist node_modules\azure-cli\bin\azure (
  cmd /c npm update https://github.com/amitapl/azure-sdk-tools-xplat/tarball/kudu_s21_5
) else (
  cmd /c npm install https://github.com/amitapl/azure-sdk-tools-xplat/tarball/kudu_s21_5
)

IF %ERRORLEVEL% NEQ 0 goto error

set long_unrequired_directory=node_modules\azure-cli\node_modules\azure\node_modules\request\node_modules\form-data\node_modules\combined-stream\node_modules\delayed-stream\test
if exist %long_unrequired_directory% (
  rmdir /s /q %long_unrequired_directory%
  IF %ERRORLEVEL% NEQ 0 goto error
)

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
