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

using System;
using System.Collections.Generic;
using System.Reflection;

static class ModConditionsAPI
{

    private static FieldInfo GetOcbCoreModConditions()
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (string.IsNullOrEmpty(assembly?.FullName)) continue;
            if (assembly.FullName.StartsWith("Microsoft.VisualStudio")) continue;
            foreach (Type type in assembly.GetTypes())
            {
                if (type.FullName != "ModConditions") continue;
                return type.GetField("Conditions");
            }
        }
        Log.Warning("ModConditions not found, CoreMod not loaded!?");
        return null;
    }

    static FieldInfo ModConditions = GetOcbCoreModConditions();

    public static void RegisterCondition(string name, Func<bool> fn)
    {
        object value = ModConditions?.GetValue(null);
        if (value is Dictionary<string, Func<bool>> conditions)
        {
            conditions[name] = fn; // create or overwrite
        }
    }

}

