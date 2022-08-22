using HarmonyLib;
using System;
using System.Collections.Generic;

namespace OCBNET
{
    public static class CustomEnums
    {

        public static Dictionary<Type, Dictionary<string, int>> Name2Int
            = new Dictionary<Type, Dictionary<string, int>>();
        public static Dictionary<Type, Dictionary<int, string>> Int2Name
            = new Dictionary<Type, Dictionary<int, string>>();

        public static void Register(Type enumType, string name, int idx)
        {
            // Make sure the required structures are created if they don't exist yet
            if (!Name2Int.TryGetValue(enumType, out Dictionary<string, int> name2int))
                Name2Int.Add(enumType, name2int = new Dictionary<string, int>());
            if (!Int2Name.TryGetValue(enumType, out Dictionary<int, string> int2name))
                Int2Name.Add(enumType, int2name = new Dictionary<int, string>());
            // Register the mappings
            name2int.Add(name, idx);
            int2name.Add(idx, name);
            // Also register lower case version
            string lower = name.ToLower();
            if (lower == name) return;
            name2int.Add(lower, idx);
            // Add log message for now
            Log.Out("Added custom enum {0}.{1} => {2}",
                enumType, name, idx);
        }

        public static void Add(string type, string name, bool bitwise = false, bool sparse = true)
        {
            Add(AccessTools.TypeByName(type), name, bitwise, sparse);
        }

        public static void Add(Type et, string name, bool bitwise = false, bool sparse = true)
        {
            int max = -1;
            if (et.IsEnum == false)
            {
                Log.Error("Trying to add an enum field to non enum type {0}", et.FullName);
            }
            else
            {
                // Search for the maximum value
                foreach (var field in et.GetEnumValues())
                    max = Math.Max(max, (int)field);
                if (Int2Name.TryGetValue(et, out var map))
                    foreach (var kv in map.Keys)
                        max = Math.Max(max, (int)kv);
                /* For another day
                // We could try to optimize sparse setting for bitwise
                // But determining that is complicated and not worth it
                if (bitwise == true || sparse == false)
                {
                }
                // Re-use any holes in existing enums
                else
                {
                }
                */
            }
            // Register, assign and take over integer value
            Register(et, name, bitwise ? max * 2 : max + 1);
        }

        public static void RegisterCondition()
        {
            ModConditionsAPI.RegisterCondition(
                "HasCustomEnums",
                () => Name2Int.Count > 0);
        }
    }

    [HarmonyCondition("HasCustomEnums")]
    [HarmonyPatch(typeof(System.Enum))]
    [HarmonyPatch("GetName")]
    public class CustomEnums_EnumGetName
    {
        static bool Prefix(
            Type enumType,
            object value,
            ref string __result)
        {
            if (!(value is int idx)) return true;
            if (CustomEnums.Int2Name.TryGetValue(enumType,
                out Dictionary<int, string> map))
            {
                if (map.TryGetValue(idx, out __result))
                    return false;
            }
            return true;
        }
    }

    [HarmonyCondition("HasCustomEnums")]
    [HarmonyPatch(typeof(Enum))]
    [HarmonyPatch("Parse")]
    [HarmonyPatch(new Type[] {
        typeof(Type),
        typeof(string),
        typeof(bool) })]

    public class CustomEnums_EnumParse
    {
        static bool Prefix(
            Type enumType,
            string value,
            bool ignoreCase,
            ref object __result)
        {
            if (CustomEnums.Name2Int.TryGetValue(enumType,
                out Dictionary<string, int> map))
            {
                if (ignoreCase) value = value.ToLower();
                if (map.TryGetValue(value, out int idx))
                {
                    __result = idx;
                    return false;
                }
            }
            return true;
        }
    }

}
