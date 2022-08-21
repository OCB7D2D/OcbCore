using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OCBNET
{

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public class HarmonyCondition : Attribute
    {

        public string Condition = string.Empty;

        // static MethodInfo = AccessTools.

        private static MethodInfo GetOcbCoreModConditions()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.IsNullOrEmpty(assembly?.FullName)) continue;
                if (assembly.FullName.StartsWith("Microsoft.VisualStudio")) continue;
                foreach (Type type in assembly.GetTypes())
                {
                    if (type.FullName != "ModConditions") continue;
                    // ToDo: narrow down selection even further (e.g. enforce static)
                    return type.GetMethod("Evaluate", new Type[] { typeof(string) });
                }
            }
            Log.Warning("ModConditions not found, will apply all patches blindly");
            return null;
        }

        static MethodInfo ModConditions = GetOcbCoreModConditions();

        public HarmonyCondition(string condition)
        {
            Condition = condition;
        }

        public bool Evaluate()
        {
            if (Condition == null) return false;
            if (ModConditions == null) return true;
            return (bool)ModConditions.Invoke(
                null, new object[] { Condition });
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

