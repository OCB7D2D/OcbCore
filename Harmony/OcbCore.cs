using System.Reflection;
using HarmonyLib;
using OCBNET;
using System;

public class OcbCore : IModApi
{
    public void InitMod(Mod mod)
    {
        Log.Out(" Loading Patch: " + GetType().ToString());
        HarmonyCondition.PatchAll(Assembly.GetExecutingAssembly());
    }


}
