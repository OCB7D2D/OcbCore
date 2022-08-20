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
            return ModConditions.Evaluate(Condition);
        }

        public static void PatchAll(Assembly assembly)
        {
            PatchAll(new Harmony($"harmony-auto-{Guid.NewGuid()}"), assembly);
        }

        // Helper function to apply patches with conditions applied
        public static void PatchAll(Harmony harmony, Assembly assembly)
        {
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

