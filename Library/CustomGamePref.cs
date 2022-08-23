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

public class CustomGamePref
{

    // We will load those values once the core mod is initialized
    // We need access to GameManage Instance, and it might be rude
    // to statically instantiate it on call load (play safe).
    static bool initialized = false;
    static int CurrentPropIdx = -1; 
    static int CurrentEnumIdx = -1;

    // Keep track of custom game preferences
    // E.g. needed to enrich `SupportedGamePrefs`
    public static readonly Dictionary<string, CustomPrefCfg>
        CustomGamePrefs = new Dictionary<string, CustomPrefCfg>();

    // Struct to hold configuration for custom game prefs
    // This defines the base API for what is required for them
    // Not sure if we need an explicit constructor with some defaults?
    public struct CustomPrefCfg
    {
        public bool persist;
        public string name;
        public object def;
        public int prop;
        public EnumGamePrefs idx;
        public GamePrefs.EnumType type;
    }

    // A very simple (POC) parser for custom prefs
    // All info is given by one string with bad encoding
    // Works if no key or value has commas or other stuff
    // For a real implementation we'd want some proper parsing
    // Additionally we can also parse the config from an XML node!
    public static CustomPrefCfg Parse(string cfg)
    {
        var idx = CurrentEnumIdx++;
        var prop = CurrentPropIdx++;
        var dict = Utilities.ParseKeyValueList(cfg);
        CustomPrefCfg pref;
        pref.persist = true;
        pref.idx = (EnumGamePrefs)idx;
        pref.prop = prop;
        pref.name = dict["name"];
        pref.type = EnumUtils.Parse<GamePrefs.EnumType>(dict["type"]);
        pref.def = ParseGamePrefValue(pref.type, dict["default"]);
        return pref;
    }

    // Helper function to make sure all is initialized
    // Some overhead, but need to do it somehow for lazy load
    // Allows us to pick up changes that may be done before our load
    public static void Init()
    {
        if (initialized) return;
        CurrentPropIdx = GamePrefs.Instance.GetPropertyList().Length;
        CurrentEnumIdx = (int)EnumGamePrefs.Last;
        initialized = true;
    }

    // Can't use original at this point, since new game enums are not know yet
    // Weak point; will need to adjust if new types are ever added to the game
    private static object ParseGamePrefValue(GamePrefs.EnumType type, string _val)
    {
        switch (type)
        {
            case GamePrefs.EnumType.Int:
                return (object)int.Parse(_val);
            case GamePrefs.EnumType.Float:
                return (object)StringParsers.ParseFloat(_val);
            case GamePrefs.EnumType.String:
                return (object)_val;
            case GamePrefs.EnumType.Bool:
                return (object)StringParsers.ParseBool(_val);
            case GamePrefs.EnumType.Binary:
                return (object)_val;
            default:
                return (object)null;
        }
    }

    // Get some private methods via reflection to mess with vanilla internals
    static readonly HarmonyFieldProxy<GamePrefs.PropertyDecl[]> GamePrefsPropList = new
        HarmonyFieldProxy<GamePrefs.PropertyDecl[]>(typeof(GamePrefs), "propertyList");
    static readonly HarmonyFieldProxy<object[]> GamePrefsPropValues = new
        HarmonyFieldProxy<object[]>(typeof(GamePrefs), "propertyValues");

    public static void AddAll(List<CustomPrefCfg> cfgs)
    {
        object[] propValues = GamePrefsPropValues.Get(GamePrefs.Instance);
        GamePrefs.PropertyDecl[] propList = GamePrefs.Instance.GetPropertyList();
        Array.Resize(ref propList, propList.Length + cfgs.Count);
        Array.Resize(ref propValues, propValues.Length + cfgs.Count);
        foreach (CustomPrefCfg cfg in cfgs)
        {
            // Extend property list to include new game pref
            // The index is the natural next index in the array
            // Some old game prefs have been removed by now, so
            // this index is lower than the actual "all time" index
            propList[cfg.prop] = new GamePrefs.PropertyDecl(
                cfg.idx, cfg.persist, cfg.type, cfg.def, null, null);
            // Register default value on property values by real enum index
            propValues[(int)cfg.idx] = propList[cfg.prop].defaultValue;
            // Create lookup maps for harmony patched custom enums functions
            // So `Enum.Parse` and `Enum.GetName` return "runtime" values
            CustomEnums.Register(
                typeof(EnumGamePrefs),
                cfg.name, (int)cfg.idx);
            // Keep track of all custom prefs
            CustomGamePrefs[cfg.name] = cfg;
        }
        // Not exactly sure why we need to set the object again
        // Thought that only raw types would be passed by copy?
        // If anyone knows the answer to this, please enlighten me.
        GamePrefsPropList.Set(GamePrefs.Instance, propList);
        GamePrefsPropValues.Set(GamePrefs.Instance, propValues);
    }

    // Helper to add a list of custom prefs
    public static void AddAll(List<string> list)
    {
        if (list == null) return;
        AddAll(list.ConvertAll(cfg => Parse(cfg)));
    }


    // Only apply patch if custom enums are defined
    // Otherwise just skip it and keep original as is
    [HarmonyCondition("HasCustomEnums")]
    // Patch GetSupportedGamePrefsInfo to make option known to game mode
    // Not sure yet what each game mode is, or if some are just deprecated
    [HarmonyPatch(typeof(GameModeSurvival))]
    [HarmonyPatch("GetSupportedGamePrefsInfo")]

    public class GameModeSurvival_GetSupportedGamePrefsInfo
    {
        static void Postfix(ref GameMode.ModeGamePref[] __result)
        {
            int len = __result.Length;
            // Extend array by number of additional game prefs
            Array.Resize(ref __result, len + CustomGamePrefs.Count);
            // Create a `GameMode.ModeGamePref` for each pref
            foreach (var pref in CustomGamePrefs.Values)
                __result[len++] = new GameMode.ModeGamePref(
                    CustomGamePrefs[pref.name].idx, pref.type, pref.def);
        }
    }
    

}
