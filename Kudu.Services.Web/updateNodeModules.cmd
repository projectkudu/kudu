@echo off

pushd %1

if exist %1\node_modules\azure-cli\bin\azure (
  cmd /c npm update azure-cli
) else (
  cmd /c npm install azure-cli
)

IF %ERRORLEVEL% NEQ 0 goto error

if exist %1\node_modules\kudusync\bin\kudusync (
  cmd /c npm update kudusync
) else (
  cmd /c npm install kudusync
)

popd
