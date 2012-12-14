@echo off

pushd %1

if exist %1\node_modules\azure-cli\bin\azure (
  call npm update azure-cli
) else (
  call npm install azure-cli
)

if exist %1\node_modules\kudusync\bin\kudusync (
  call npm update kudusync
) else (
  call npm install kudusync
)

popd
