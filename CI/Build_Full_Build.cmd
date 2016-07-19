@echo off

set script_path=%~dp0
call %script_path%setup.cmd


%msbuild% /t:ReleaseWithNoMail /p:Configuration=Debug %script_path%build.msbuild

if %errorlevel% neq 0 goto failed


:failed

pause
exit /b 1