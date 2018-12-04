@echo off

echo.
echo    -=-=-=-=-=-=-=[RTC Factory Cleaner]=-=-=-=-=-=-=-
echo.
echo    This will delete all RTC save data, 
echo    BizHawk config, emulator SaveStates and SaveRAM
echo.
echo    BizHawk will restart after the script has finished.
echo.
echo    To abort this procedure, Close the window.
echo.
echo.
echo.
echo.
Pause

rem !!!!!!!!!!!!!!!!!!!!!!!!!!!
rem DO NOT EDIT THIS BATCHFILE
rem !!!!!!!!!!!!!!!!!!!!!!!!!!!

cls
taskkill /F /IM "EmuHawk.exe"
taskkill /F /IM "StandaloneRTC.exe" > nul

del config.ini /F
del backup_config.ini /F
del stockpile_config.ini /F
del CorruptedROM.rom /F
del VinesauceROMCorruptor.txt /F

del RTC/MEMORYDUMPS/*.* /F /Q
del RTC/RENDEROUTPUT/*.* /F /Q
del RTC/TEMP/*.* /F /Q
del RTC/TEMP2/*.* /F /Q
del RTC/TEMP3/*.* /F /Q
del RTC/TEMP4/*.* /F /Q
del RTC/PARAMS/*.* /F /Q
	
del WGH/PARAMS/*.* /F /Q

start RTC/StateClean.bat

rem IF YOU UPDATE THIS UPDATE StateClean

del "Apple II\SaveRAM*.*" /F /Q
del "Atari 2600\SaveRAM*.*" /F /Q
del "Atari 7800\SaveRAM*.*" /F /Q
del "Coleco\SaveRAM*.*" /F /Q
del "Game Gear\SaveRAM*.*" /F /Q
del "Gameboy\SaveRAM*.*" /F /Q
del "GBA\SaveRAM*.*" /F /Q
del "Genesis\SaveRAM*.*" /F /Q
del "Intellivision\SaveRAM*.*" /F /Q
del "Libretro\SaveRAM*.*" /F /Q
del "Lynx\SaveRAM*.*" /F /Q
del "N64\SaveRAM*.*" /F /Q
del "NES\SaveRAM*.*" /F /Q
del "PC Engine\SaveRAM*.*" /F /Q
del "PSX\SaveRAM*.*" /F /Q
del "Saturn\SaveRAM*.*" /F /Q
del "SG-1000\SaveRAM*.*" /F /Q
del "SMS\SaveRAM*.*" /F /Q
del "SNES\SaveRAM*.*" /F /Q
del "VB\SaveRAM*.*" /F /Q
del "WonderSwan\SaveRAM*.*" /F /Q


echo.
echo.