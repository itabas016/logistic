@echo off

set FrameworkVersion=v4.0.30319
call :GetMSBuildFullPath > nul 2>&1
if errorlevel 1 set msbuild=C:\WINDOWS\Microsoft.NET\Framework\%FrameworkVersion%\MSBuild.exe
if not exist "%msbuild%" echo Where's MSBuild.exe? && pause

set tf="%VS140COMNTOOLS%..\IDE\tf.exe"
if not exist %tf% set tf="C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\tf.exe"
if not exist %tf% set tf="%VS100COMNTOOLS%..\IDE\tf.exe"
if not exist %tf% echo Where's TF.exe? && pause

goto :eof

:GetMSBuildFullPath
for /f "tokens=1,2*" %%i in ('reg query "HKLM\SOFTWARE\Microsoft\.NETFramework" /v "InstallRoot"') do (
	if "%%i"=="InstallRoot" (
		set "msbuild=%%k\%FrameworkVersion%\MSBuild.exe"
	)
)
if "%msbuild%"=="" exit /b 1
exit /b 0
