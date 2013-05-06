@echo on
setlocal
set webpicmd="%ProgramFiles%\Microsoft\Web Platform Installer\WebPiCmd.exe"
set customFeed="%~dp0KuduSetupCustomWebPiFeed.xml"
set logFile="%~dp0WebPiCmdSetup.log"
%webpicmd% /Install  /SuppressReboot /Products:KuduDevSetup /Feeds:%customFeed% /Log:%logFile%