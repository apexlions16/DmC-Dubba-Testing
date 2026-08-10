@echo off
setlocal
cd /d "%~dp0"
set "ROOT=%~dp0"
set "GAME_QA_PACKAGED_TEST=1"
set "GAME_QA_API=http://127.0.0.1:7860/"
set "GAME_QA_PORT=7860"

echo ==========================================================
echo   OYUN QA PLATFORMU - YEREL TEST ORTAMI
echo ==========================================================
echo.
echo Sunucu baslatiliyor...
start "Game QA Sunucusu" /min "%ROOT%Server\GameQaServer\GameQaServer.exe"

echo Sunucunun hazir olmasi bekleniyor...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ok=$false; for($i=0;$i -lt 60;$i++){ try { $r=Invoke-RestMethod -Uri 'http://127.0.0.1:7860/health' -TimeoutSec 2; if($r.status -eq 'ok'){ $ok=$true; break } } catch {}; Start-Sleep -Milliseconds 500 }; if(-not $ok){ exit 1 }"
if errorlevel 1 (
  echo.
  echo [HATA] QA sunucusu baslatilamadi.
  echo Bu pencereyi kapatmadan once TEST-BILGILERI.txt dosyasini kontrol edin.
  pause
  exit /b 1
)

echo Sunucu hazir.
echo Yonetim Merkezi ve Tester uygulamasi aciliyor...
start "QA Yonetim Merkezi" "%ROOT%Admin\DmC.Qa.Admin.exe"
timeout /t 1 /nobreak >nul
start "QA Tester" "%ROOT%Tester\DmC.Qa.Tester.exe"

echo.
echo Test ortami acildi.
echo Yonetici girisi: Yonetici
echo Tester girisi: Test Kullanicisi
echo.
echo Bu pencereyi kapatabilirsiniz; sunucu arka planda calismaya devam eder.
endlocal
