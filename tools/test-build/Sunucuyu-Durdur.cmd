@echo off
setlocal
echo Oyun QA yerel test sunucusu kapatiliyor...
taskkill /IM GameQaServer.exe /F >nul 2>&1
if errorlevel 1 (
  echo Calisan GameQaServer.exe bulunamadi.
) else (
  echo Sunucu kapatildi.
)
endlocal
