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
using System.Reflection;

namespace OCBNET
{

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public class HarmonyCondition : Attribute
    {

        public string Condition = string.Empty;

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

