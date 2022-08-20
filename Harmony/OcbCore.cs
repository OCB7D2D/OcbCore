using System.Reflection;
using HarmonyLib;
using OCBNET;
using System;
using System.Collections.Generic;
using System.IO;

public class OcbCore : IModApi
{

    static readonly List<Mod> InitLater = new List<Mod>();

    static readonly HarmonyFieldProxy<DictionaryList<string, Mod>> LoadedMods = new
        HarmonyFieldProxy<DictionaryList<string, Mod>>(typeof(ModManager), "loadedMods");

    static Mod LastModToLoad = null;

    // Patch Init code for every mod to be loaded
    // Defer loading if we are waiting for last mod
    // This patch is applied programmatically only!
    static public bool PrefixModInit(Mod mod)
    {
        if (LastModToLoad == null) return true;
        InitLater.Add(mod);
        if (LastModToLoad == mod)
        {
            LastModToLoad = null;
            var cfg = ModConfigs.Instance;
            Log.Out("Detected Last Mod, loading deferred mods in order now");
            Log.Out("Resorting mod list to load mod by their dependencies");
            InitLater.Sort(delegate (Mod a, Mod b) {
                return cfg.HasDependency(b, a) ? -1 : 1;
            });
            foreach (Mod load in InitLater)
            {
                if (load.ApiInstance != null)
                {
                    try
                    {
                        load.ApiInstance.InitMod(load);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("[MODS] Failed initializing ModAPI instance on mod {0} from DLL {1}",
                            load.ModInfo.Name.Value, Path.GetFileName(load.MainAssembly.Location));
                        Log.Exception(ex);
                    }
                }

            }
        }

        return false;
    }
    
    public void InitMod(Mod mod)
    {
        Log.Out(" Loading Patch: " + GetType().ToString());
        Harmony harmony = new Harmony($"harmony-auto-{Guid.NewGuid()}");
        HarmonyCondition.PatchAll(harmony, Assembly.GetExecutingAssembly());
        // Original code also uses `dict`
        // Shouldn't it be a sorted list?
        foreach (var kv in LoadedMods.Get(null)?.dict)
        {
            var rv = AccessTools.Method(kv.Value.ApiInstance.GetType(), "InitMod");
            if (rv == null) continue;
            var patcher = harmony.CreateProcessor(rv);
            if (patcher == null) continue;
            var fn = this.GetType().GetMethod("PrefixModInit");
            if (fn == null) continue;
            patcher.AddPrefix(fn);
            patcher.Patch();
            LastModToLoad = kv.Value;
        }
    }

    [HarmonyPatch(typeof(ModManager))]
    [HarmonyPatch("GetLoadedMods")]
    public class ModManager_GetLoadedMods
    {

        static bool nested = false;

        static bool Prefix(
            DictionaryList<string, Mod> ___loadedMods,
            ref List<Mod> __result)
        {
            if (nested) return true;
            nested = true;
            __result = ModConfigs.Instance.LoadOrder;
            nested = false;
            return false;
        }

    }


}
