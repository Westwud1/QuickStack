@echo off
setlocal enabledelayedexpansion

rem === Package name ===
set "PACK_NAME=QuickStack"

rem === Extract version from ModInfo.xml using PowerShell ===
for /f "usebackq delims=" %%V in (`powershell -NoProfile -Command ^
    "(Select-Xml -Path 'ModInfo.xml' -XPath '//Version').Node.value"`) do (
    set "VERSION=%%V"
)

rem === Remove old folder if it exists ===
if exist "%PACK_NAME%" rmdir /s /q "%PACK_NAME%"
mkdir "%PACK_NAME%"

rem === Copy files/folders into the package folder ===
xcopy Config "%PACK_NAME%\Config" /e /i
xcopy UIAtlases "%PACK_NAME%\UIAtlases" /e /i
copy ModInfo.xml "%PACK_NAME%\" >nul
copy QuickStack.dll "%PACK_NAME%\" >nul
copy QuickStackConfig.xml "%PACK_NAME%\" >nul

rem === Zip the entire package folder ===
7z a -tzip "%PACK_NAME% %VERSION%.zip" "%PACK_NAME%\*" >nul

rem === Remove old folder if it exists ===
if exist "%PACK_NAME%" rmdir /s /q "%PACK_NAME%"

echo Done
endlocal