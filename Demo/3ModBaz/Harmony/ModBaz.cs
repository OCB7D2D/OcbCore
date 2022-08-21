using System.Reflection;
using HarmonyLib;
using OCBNET;

public class ModBaz : IModApi
{
    public void InitMod(Mod mod)
    {
        Log.Out(" Loading Patch: " + GetType().ToString());
        var harmony = new Harmony(GetType().ToString());
        HarmonyCondition.PatchAll(harmony, Assembly.GetExecutingAssembly());
    }

    [HarmonyCondition("OcbCore>=0.0.0.0,OcbCore<=0.0.0.1,HasConfig(SCore;Fire),HasConfig(SCore;Base)")]
    [HarmonyPatch(typeof(EntityPlayerLocal))]
    [HarmonyPatch("Update")]
    public class EntityPlayerLocalUpdate
    {
        static void Prefix()
        {
            Log.Out("Update Inside Baz");
        }
    }

}
