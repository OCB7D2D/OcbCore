@echo off

call MC7D2D UseSettings.dll ^
  /reference:"%PATH_7D2D_MANAGED%\Assembly-CSharp.dll" ^
  Harmony\*.cs ..\..\API\HarmonyCondition.cs && ^
echo Successfully compiled UseSettings.dll
