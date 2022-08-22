using HarmonyLib;
using OCBNET;
using System;
using System.Collections.Generic;

public class CustomGamePref
{

    static bool initialized = false;
    static int CurrentPropIdx = -1; 
    static int CurrentEnumIdx = -1;

    public static readonly Dictionary<Type, Dictionary<string, CustomPrefCfg>> CustomGamePrefs
        = new Dictionary<Type, Dictionary<string, CustomPrefCfg>>();

    private static readonly List<CustomPrefCfg> AllPrefs
        = new List<CustomPrefCfg>();

    public struct CustomPrefCfg
    {
        public bool persist;
        public string name;
        public object def;
        public int prop;
        public EnumGamePrefs idx;
        public GamePrefs.EnumType type;
    }

    private static void Init()
    {
        CurrentPropIdx = GamePrefs.Instance.GetPropertyList().Length;
        CurrentEnumIdx = (int)EnumGamePrefs.Last;
        initialized = true;
    }

    // Can't use original at this point, since new game enums are not know yet
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

    public static CustomPrefCfg Parse(string cfg)
    {
        if (!initialized) Init();
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

    static readonly HarmonyFieldProxy<GamePrefs.PropertyDecl[]> GamePrefsProp = new
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
            propList[cfg.prop] = new GamePrefs.PropertyDecl(
                cfg.idx, cfg.persist, cfg.type, cfg.def, null, null);
            propValues[(int)cfg.idx] = propList[cfg.prop].defaultValue;
            if (!CustomGamePrefs.TryGetValue(typeof(EnumGamePrefs), out Dictionary<string, CustomPrefCfg> map))
                CustomGamePrefs.Add(typeof(EnumGamePrefs), map = new Dictionary<string, CustomPrefCfg>());
            // Add specific mapping
            map.Add(cfg.name, cfg);
            // Create lookup map
            CustomEnums.Register(
                typeof(EnumGamePrefs),
                cfg.name, (int)cfg.idx);
            AllPrefs.Add(cfg);
        }
        GamePrefsProp.Set(GamePrefs.Instance, propList); // not needed?
        GamePrefsPropValues.Set(GamePrefs.Instance, propValues); // needed!?
    }

    public static void AddAll(List<string> list)
    {
        AddAll(list.ConvertAll(cfg => Parse(cfg)));
    }


    [HarmonyCondition("HasCustomEnums")]
    [HarmonyPatch(typeof(GameModeSurvival))]
    [HarmonyPatch("GetSupportedGamePrefsInfo")]

    public class GameModeSurvival_GetSupportedGamePrefsInfo
    {
        static void Postfix(ref GameMode.ModeGamePref[] __result)
        {
            int len = __result.Length;
            Array.Resize(ref __result, len + AllPrefs.Count);
            for (int i = 0; i < AllPrefs.Count; i += 1)
                __result[len + i] = new GameMode.ModeGamePref(
                    AllPrefs[i].idx, AllPrefs[i].type, AllPrefs[i].def);
        }
    }
    

}
