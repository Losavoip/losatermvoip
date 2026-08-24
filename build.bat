@echo off
setlocal

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set OUT=LosaTermVoip.exe

echo.
echo ============================================================
echo   LosaTermVoip — Build Script
echo ============================================================
echo.

:: Compila
echo [1/3] Compilazione...
"%CSC%" /target:winexe /win32icon:app.ico /out:"%OUT%" ^
  /r:System.Windows.Forms.dll ^
  /r:System.Drawing.dll ^
  /r:System.Runtime.Serialization.dll ^
  /r:System.Net.dll ^
  /r:System.Security.dll ^
  /r:System.Core.dll ^
  NetTerm.cs Analyzer.cs FtpSftpServer.cs Localization.cs AdvancedFeatures.cs Enhancements.cs SbcHealth.cs VoipCodes.cs NetTools.cs TftpServer.cs DhcpServer.cs SerialConsole.cs VoipCalc.cs StunTester.cs OptionsMonitor.cs RtpPlayer.cs DnsVoip.cs DnsQuery.cs HealthCheck.cs FirewallCheck.cs LiveCapture.cs SipRegister.cs TrafficGen.cs NetPath.cs Srtp.cs Provisioning.cs RawSip.cs WebRtc.cs SipValidator.cs

if errorlevel 1 (
  echo.
  echo [ERRORE] Compilazione fallita!
  pause
  exit /b 1
)

echo [OK] Compilato: %OUT%
echo.

:: Copia il manuale
echo [2/3] Copia file...
if not exist "dist" mkdir dist
copy /Y "%OUT%"                        dist\
copy /Y "LosaTermVoip_Manuale.html"   dist\
if exist "putty.exe" copy /Y "putty.exe" dist\

echo [OK] File in dist\
echo.

:: Lancia l'app per test
echo [3/3] Avvio per test...
start "" "%OUT%"

echo.
echo ============================================================
echo   Build completata con successo!
echo   Output: %OUT%
echo   Distribuzione: dist\
echo ============================================================
echo.
echo Per creare il setup.exe:
echo   1. Installa NSIS da https://nsis.sourceforge.io/
echo   2. Copia LosaTermVoip.exe e putty.exe in installer\
echo   3. Clicca destro su installer\LosaTermVoip_Setup.nsi
echo   4. Scegli "Compile NSIS Script"
echo.
pause
