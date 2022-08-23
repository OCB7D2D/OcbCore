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

    // Additional custom annotation for `[HarmonyCondition("condition")]`
    // Allows to call into `ModConditions` to determine if a harmony patch should
    // be applied or not. Your mod may embed this single file in order to enable
    // this feature, IF core mod is installed. If it is not found, you may decide
    // if you want to blindly apply or reject all patches (or extend this file,
    // that you copied into your source tree anyway, to do whatever you want).
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public class HarmonyCondition : Attribute
    {

        // You may change this at your discretion
        const bool ApplyBlindly = true;
        
        // The string condition for this annotation
        public string Condition = string.Empty;

        // Basic constructor for annotation
        public HarmonyCondition(string condition)
        {
            Condition = condition;
        }

        // Helper function to get interface to core mod
        private static MethodInfo GetOcbCoreModConditions()
        {
            // ToDo: replace with `ModManager.GetMod("SanctionedName").MainAssembly`
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.IsNullOrEmpty(assembly?.FullName)) continue;
                if (assembly.FullName.StartsWith("Microsoft.VisualStudio")) continue;
                foreach (Type type in assembly.GetTypes())
                {
                    // Check the type name (ToDo: make more unique)
                    if (type.FullName != "ModConditions") continue;
                    // ToDo: narrow down selection even further (e.g. enforce static)
                    return type.GetMethod("Evaluate", new Type[] { typeof(string) });
                }
            }
            // Is it enough to just emit a warning in that case (or just shut up?)
            // ToDo: at least we should consider the `ApplyBlindly` boolean by now
            Log.Warning("ModConditions not found, will apply all patches blindly");
            return null;
        }

        // Fetch the reference once and reuse it
        static readonly MethodInfo ModConditions = GetOcbCoreModConditions();
 
        // Helper to evaluate the condition
        public bool Evaluate()
        {
            // No condition means false (or raise error?)
            if (Condition == null) return false;
            // Fallback condition if core mod is not loaded
            if (ModConditions == null) return ApplyBlindly;
            // Call into core mode to evaluate condition
            return (bool)ModConditions.Invoke(
                null, new object[] { Condition });
        }

        // Helper function to apply patches with conditions applied
        // This is in fact a full implementation of the conditional patcher
        // The only core mod relevant bit is the call to `ModConditions` to
        // evaluate the actual condition to take a decision.
        // Function is mostly copied directly from harmony itself.
        public static void PatchAll(Harmony harmony, Assembly assembly)
        {
            // Process all types in all assemblies (meaning main classes)
            // Not exactly sure if this covers all cases original harmony
            // patcher does. If not it should be easy to also add them here.
            // Thinking about inner classes and stuff, but I guess we're OK.
            foreach (Type type in AccessTools.GetTypesFromAssembly(assembly))
            {
                // One can give multiple conditions, which are or'ed, meaning
                // only one needs to be true in order for the patch to be applied.
                // So we need to keep track if there were any condition at all,
                // because in case there isn't any, the patch should be applied.
                // In case we have some conditions, we must have at least one true.
                bool apply = false; // flag if one condition is true
                bool custom = false; // flag if we had any condition at all
                foreach (var attr in type.GetCustomAttributes())
                {
                    // Here we also see the other annotation
                    if (attr is HarmonyCondition annotation)
                    {
                        custom = true; // remember we had custom condition
                        apply |= annotation.Evaluate(); // OR'ing them
                    }
                }
                // Skip patch if custom and none true
                if (custom && !apply) continue;
                // Create harmony processor and apply patch
                // This is directly copied from original code
                harmony.CreateClassProcessor(type).Patch();
            }
        }

    }
}

