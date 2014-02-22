@echo off
FOR /F "delims=" %%A in ('where deployedJob.html.template') do set "HTML_TEMPLATE_FILE=%%A"
copy /y "%HTML_TEMPLATE_FILE%" "%DEPLOYMENT_TEMP%\hostingstart.html"
