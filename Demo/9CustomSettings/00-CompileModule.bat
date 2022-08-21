@echo off

call MC7D2D CustomSettings.dll ^
  /reference:"%PATH_7D2D_MANAGED%\Assembly-CSharp.dll" ^
  Harmony\*.cs ..\..\Library\HarmonyCondition.cs && ^
echo Successfully compiled CustomSettings.dll
