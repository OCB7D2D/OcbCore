using System.Reflection;
using HarmonyLib;
using OCBNET;

public class UseSettings : IModApi
{
    public void InitMod(Mod mod)
    {
        Log.Out(" Loading Patch: " + GetType().ToString());
        Log.Warning(" Trying to Parse New GamePref");
        EnumGamePrefs pref_a = EnumUtils.Parse<EnumGamePrefs>("LoadVanillaMap", false);
        EnumGamePrefs pref_b = EnumUtils.Parse<EnumGamePrefs>("OtherGamePref", true);
        EnumGamePrefs pref_c = EnumUtils.Parse<EnumGamePrefs>("Last", true);
        Log.Out(" First: {0} => {1}", (int)pref_a, pref_a.ToString());
        Log.Out(" Other: {0} => {1}", (int)pref_b, pref_b);
        Log.Out(" Last: {0} => {1}", (int)pref_c, pref_c);
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
