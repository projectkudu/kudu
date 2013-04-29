@echo off
setlocal enabledelayedexpansion 

pushd %1

set attempts=5
set counter=0

:retry
set /a counter+=1
echo Attempt %counter% out of %attempts%

if exist node_modules\azure-cli\bin\azure (
  cmd /c npm update git://github.com/amitapl/azure-sdk-tools-xplat.git#kudu_s21_5
) else (
  cmd /c npm install git://github.com/amitapl/azure-sdk-tools-xplat.git#kudu_s21_5
)

IF %ERRORLEVEL% NEQ 0 goto error

pushd node_modules

for /r %%X IN (test) DO (
  rmdir /s /q %%X 2>nul
)

popd

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
