using System.Reflection;
using HarmonyLib;
using OCBNET;

public class UseSettings : IModApi
{
    public void InitMod(Mod mod)
    {
        Log.Out(" Loading Patch: " + GetType().ToString());
        var harmony = new Harmony(GetType().ToString());
        HarmonyCondition.PatchAll(harmony, Assembly.GetExecutingAssembly());
    }

/*
    [HarmonyCondition("HasConfig(SCore;Fire),HasConfig(SCore;Base)")]
    [HarmonyPatch(typeof(EntityPlayerLocal))]
    [HarmonyPatch("Update")]
    public class EntityPlayerLocalUpdate
    {
        static void Prefix()
        {
            Log.Out("Update Inside Baz");
        }
    }
*/

}
