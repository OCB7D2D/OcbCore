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
using System;
using System.Collections.Generic;

namespace OCBNET
{
    public static class CustomEnums
    {

        // Store two maps for each custom extended enum type.
        // One to map from name to index, the other vice-versa.
        // We could also just use a `NameIdMapping`, but it has
        // some drawbacks in its API, so we just re-implement it.
        // There isn't much rocket science about it anyway ;)
        public static Dictionary<Type, Dictionary<string, int>> Name2Int
            = new Dictionary<Type, Dictionary<string, int>>();
        public static Dictionary<Type, Dictionary<int, string>> Int2Name
            = new Dictionary<Type, Dictionary<int, string>>();

        // Main function for 3rd party to register a new enum field
        // Adds a new enum index for `enumType` by `name` and index `idx`
        // This function should be used directly most of the time, use one of
        // the functions that automatically determines an appropriate index.
        public static void Register(Type enumType, string name, int idx)
        {
            // Make sure the required structures are created if they don't exist yet
            if (!Name2Int.TryGetValue(enumType, out Dictionary<string, int> name2int))
                Name2Int.Add(enumType, name2int = new Dictionary<string, int>());
            if (!Int2Name.TryGetValue(enumType, out Dictionary<int, string> int2name))
                Int2Name.Add(enumType, int2name = new Dictionary<int, string>());
            // Register the mappings
            // name2int.Add(name, idx);
            name2int[name] = idx;
            // int2name.Add(idx, name);
            int2name[idx] = name;
            // Add log message for now (remove on prod)
            Log.Out("Added custom enum {0}.{1} => {2}",
                enumType, name, idx);
            // Also register lower case version
            string lower = name.ToLower();
            if (lower == name) return;
            // Overwrite existing mapping
            // There is a unavoidable edge-case, when supporting case sensitive and
            // non-sensitive matching. As you can have many unique case sensitive names
            // that all match to the same non-sensitive representation. Which one to
            // choose in that situation is not that well defined in vanilla either.
            // Let's assume we only do this to catch modders that can't spell right :)
            name2int[lower] = idx;
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

        // Initializer called
        public static void Init()
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
