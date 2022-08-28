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
            // Remove mods which that fail their conditions
            InitLater.RemoveAll(entry => !cfg.IsModEnabled(entry));
            // Sort by dependencies or keep alphanumeric order
            InitLater.Sort(delegate (Mod a, Mod b) {
                return cfg.HasDependency(b, a) ? -1 :
                    a.FolderName.CompareTo(b.FolderName);
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

        // Fetch the harmony prefix we want to apply to all future mods
        // Didn't find a way to do it statically, so we do it that way
        // You can find that function above: `bool PrefixModInit(Mod mod)`
        if (GetType().GetMethod("PrefixModInit") is MethodInfo fn)
        {
            // Original code also uses `dict`
            // Shouldn't it be a sorted list?
            // We basically replace the original loop
            // This time with correct dependency order
            foreach (var kv in LoadedMods.Get(null)?.dict)
            {
                if (kv.Value == mod) continue; // Do not patch ourself ;)
                if (kv.Value?.ApiInstance == null) continue; // Play safe
                // Get the "InitMod" function of the 3rd party mod
                // We will patch it so we can gather all future mods
                // Once we give control back to vanilla it will continue to
                // call all other mod's "InitMod", but by then we have already
                // patched them all. We also register here which will be the last
                // mod to load, since on that one, we can start to apply dependency
                // ordering and execute the actual "InitMod" calls. Those will then
                // apply their own harmony mods as with vanilla (just in correct oder).
                var rv = AccessTools.Method(kv.Value.ApiInstance.GetType(), "InitMod");
                if (rv == null) continue;
                var patcher = harmony.CreateProcessor(rv);
                if (patcher == null) continue;
                // Doing harmony patching programmatically
                patcher.AddPrefix(fn);
                patcher.Patch();
                // Update last mod to load
                // Last one standing is the one
                LastModToLoad = kv.Value;
            }
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
