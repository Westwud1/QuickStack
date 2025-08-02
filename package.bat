@echo off

powershell -Command "Compress-Archive -Path 'Config','UIAtlases','ModInfo.xml','QuickStack.dll','QuickStackConfig.xml' -DestinationPath 'QuickStack.zip' -Force"

echo Done