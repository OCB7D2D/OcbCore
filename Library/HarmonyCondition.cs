using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace OCBNET
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public class HarmonyCondition : Attribute
    {
        public string Condition = string.Empty;

        public HarmonyCondition(string condition)
        {
            Condition = condition;
        }

        public bool Evaluate()
        {
            if (Condition == null) return false;
            if (Condition.StartsWith("HasConfig(") && Condition.EndsWith(")"))
            {
                OCBNET.ModConfigs mods = OCBNET.ModConfigs.Instance;
                var conditions = Condition.Substring(10, Condition.Length - 11).Split(',');
                if (conditions.Length > 1)
                {
                    if (mods.GetConfigs(conditions[0]) is List<string> configs)
                    {
                        for (int i = 1; i < conditions.Length; i += 1)
                        {
                            if (!configs.Contains(conditions[i])) return false;
                        }
                        return true;
                    }
                    return false;
                }
            }
            Log.Error("Harmony condition invalid {0}", Condition);
            return false;
        }

        // Helper function to apply patches with conditions applied
        public static void PatchAll(Assembly assembly)
        {
            Harmony harmony = new Harmony($"harmony-auto-{Guid.NewGuid()}");
            Type[] types = AccessTools.GetTypesFromAssembly(assembly);
            foreach (Type type in types)
            {
                bool apply = false;
                bool custom = false;
                foreach (var attr in type.GetCustomAttributes())
                {
                    if (attr is HarmonyCondition condition)
                    {
                        custom = true; // remember we had custom condition
                        apply |= condition.Evaluate(); // OR'ing them
                    }
                }
                if (custom && !apply) continue;
                if (custom && apply) Log.Out(
                    "Applying conditional patch {0}",
                    type.FullName);
                harmony.CreateClassProcessor(type).Patch();
            }
        }

    }
}

