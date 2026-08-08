@echo off
setlocal
set "script_dir=%~dp0"
pushd "%script_dir%..\src\Hardware Store Simulator"
dotnet "%script_dir%Jenny\Jenny.Generator.Cli.dll" gen "%script_dir%JennyRoslyn.properties" -v
set exit_code=%errorlevel%
popd
exit /b %exit_code%
