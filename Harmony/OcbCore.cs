using System.Reflection;
using HarmonyLib;
using OCBNET;
using System;
using System.Collections.Generic;

public class OcbCore : IModApi
{
    public void InitMod(Mod mod)
    {
        Log.Out(" Loading Patch: " + GetType().ToString());
        HarmonyCondition.PatchAll(Assembly.GetExecutingAssembly());
    }

    [HarmonyPatch(typeof(ModManager))]
    [HarmonyPatch("GetLoadedMods")]
    public class ModManager_GetLoadedMods
    {
        static bool Prefix(ref List<Mod> __result)
        {
            if (!ModConfigs.HasInstance()) return true;
            __result = ModConfigs.Instance.LoadOrder;
            // Log.Out("==== Returning new load order");
            return false;
        }
    }


}
