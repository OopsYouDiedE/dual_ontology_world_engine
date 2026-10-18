@echo off
setlocal
for %%I in ("%~dp0..\..\..\..") do set "REPO_ROOT=%%~fI"
set "SCRIPT=%~dp0consolidate_dv.py"

py -3 --version >nul 2>nul
if not errorlevel 1 goto use_py
where python >nul 2>nul
if not errorlevel 1 goto use_python

echo Error: Python 3 was not found. Install Python 3 and make py -3 or python available.
exit /b 9009

:use_py
py -3 "%SCRIPT%" --root "%REPO_ROOT%" %*
exit /b %errorlevel%

:use_python
python "%SCRIPT%" --root "%REPO_ROOT%" %*
exit /b %errorlevel%
