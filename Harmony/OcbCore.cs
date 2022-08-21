/*
Copyright © 2022 Marcel Greter

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

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
        var modcfg = ModConfigs.Instance;
        Harmony harmony = new Harmony($"harmony-auto-{Guid.NewGuid()}");
        // harmony.PatchAll(Assembly.GetExecutingAssembly());
        HarmonyCondition.PatchAll(harmony, Assembly.GetExecutingAssembly());
        CustomGamePref.AddAll(modcfg.GetConfigs("GamePrefs")?.list);

        if (GetType().GetMethod("PrefixModInit") is MethodInfo fn)
        {
            // Original code also uses `dict`
            // Shouldn't it be a sorted list?
            foreach (var kv in LoadedMods.Get(null)?.dict)
            {
                if (kv.Value == mod) continue; // Do not patch ourself ;)
                var rv = AccessTools.Method(kv.Value.ApiInstance.GetType(), "InitMod");
                if (rv == null) continue;
                var patcher = harmony.CreateProcessor(rv);
                if (patcher == null) continue;
                patcher.AddPrefix(fn);
                patcher.Patch();
                LastModToLoad = kv.Value;
            }
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
