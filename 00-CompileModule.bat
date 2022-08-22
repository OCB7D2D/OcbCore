@echo off

call MC7D2D OcbCore.dll ^
  /reference:"%PATH_7D2D_MANAGED%\Assembly-CSharp.dll" ^
  Harmony\*.cs Library\*.cs API\*.cs && ^
echo Successfully compiled OcbCore.dll

cd Demo\1ModBar
call 00-CompileModule.bat
cd ..\..

cd Demo\2ModFoo
call 00-CompileModule.bat
cd ..\..

cd Demo\3ModBaz
call 00-CompileModule.bat
cd ..\..

cd Demo\8UseSettings
call 00-CompileModule.bat
cd ..\..

cd Demo\9CustomSettings
call 00-CompileModule.bat
cd ..\..

pause