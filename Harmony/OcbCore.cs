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

using HarmonyLib;
using OCBNET;
using System;
using System.Collections.Generic;
using System.Reflection;

public class OcbCore : IModApi
{

    static readonly List<Mod> InitLater = new List<Mod>();

    static readonly HarmonyFieldProxy<DictionaryList<string, Mod>> LoadedMods = new
        HarmonyFieldProxy<DictionaryList<string, Mod>>(typeof(ModManager), "loadedMods");

    static Mod LastModToLoad = null;

    public void InitMod(Mod mod)
    {
        Log.Out(" Loading Patch: " + GetType().ToString());
        // Register `HasCustomEnums` condition
        CustomEnums.Init();
        // Fetch gameprefs sizes
        CustomGamePref.Init();
        // Gather static configs across all mods
        // ToDo: load order not enforced here yet
        var modcfg = ModConfigs.Instance;
        // Create harmony patcher like you'd always do
        Harmony harmony = new Harmony(GetType().ToString());
        // This would be the regular call to harmony to apply patches
        // harmony.PatchAll(Assembly.GetExecutingAssembly());
        // If your mod uses `HarmonyCondition` you need to use this call
        HarmonyCondition.PatchAll(harmony, Assembly.GetExecutingAssembly());

        // This is basically the main implementation for custom game prefs
        // Call could be in any other mod, and doesn't need to be in the core
        CustomGamePref.AddAll(modcfg.GetConfigs("GamePrefs")?.list);
        // Load additional config file for dedicated server to provide default values
        if (GameManager.IsDedicatedServer) CustomGamePrefDedi.ApplyCustomServerConfig();

        // Find last mod loading with regular load order
        // We will receive and queue all until this one
        var mods = ModManager.GetLoadedMods();
        // Should not happen since we exist!?
        if (mods.Count == 0) return;
        LastModToLoad = mods[mods.Count - 1];
    }

    // Hook into `InitModCode` to delay the actual patching
    // Wait until last mod to re-sort and apply in correct order
    [HarmonyPatch(typeof(Mod))]
    [HarmonyPatch("InitModCode")]
    public class ModManager_InitModCode
    {
        static bool Prefix(Mod __instance)
        {
            var mod = __instance;
            if (LastModToLoad == null) return true;
            InitLater.Add(mod);
            if (LastModToLoad == mod)
            {
                LastModToLoad = null;
                var cfg = ModConfigs.Instance;
                Log.Out("=====================================================");
                Log.Out("Detected Last Mod, loading deferred mods in order now");
                Log.Out("Resorting mod list to load mod by their dependencies");
                Log.Out("=====================================================");
                // Remove mods that failed their conditions
                InitLater.RemoveAll(entry => !cfg.IsModEnabled(entry));
                // Sort by dependencies or keep alphanumeric order
                InitLater.Sort(delegate (Mod a, Mod b) {
                    return cfg.HasDependency(a, b) ? 1 :
                          cfg.HasDependency(b, a) ? -1 :
                        a.FolderName.CompareTo(b.FolderName);
                });
                // Enable debug for now to check it if needed
                if (cfg.DebugLoadOrder)
                {
                    Log.Out("Load Mods in the following order:");
                    foreach (Mod load in InitLater)
                        Log.Out("  {0}", load.Name);
                }
                // Load all mods now in new order
                foreach (Mod load in InitLater)
                {
                    try
                    {
                        load.InitModCode();
                    }
                    catch (Exception ex)
                    {
                        Log.Error("[MODS] Failed initializing ModAPI instance on mod {0} from DLL {1}",
                            load.Name, System.IO.Path.GetFileName(load.AllAssemblies[0].Location));
                        Log.Exception(ex);
                    }
                }
            }

            return false;
        }

    }

    // Patching `GetLoadedMods` is all it takes to overload
    // ordering in pretty much all places where it is relevant.
    [HarmonyPatch(typeof(ModManager))]
    [HarmonyPatch("GetLoadedMods")]
    public class ModManager_GetLoadedMods
    {
        // We may be called by creating a `ModConfigs.Instance`
        // This flag ensures we don't get into an endless loop
        static bool nested = false;
        static bool Prefix(
            DictionaryList<string, Mod> ___loadedMods,
            ref List<Mod> __result)
        {
            // Return normal order if we are called nested
            // Call to `ModConfigs.Instance` will cause this
            if (nested) return true;
            nested = true; // enable safe-guard
            __result = ModConfigs.Instance.LoadOrder;
            nested = false; // reset safe-guard
            return false;
        }

    }


}
