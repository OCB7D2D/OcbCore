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

class ModConditions
{

    // Must be set from outside first, otherwise not much happens
    public static Dictionary<string, Func<bool>> Conditions = null;

    // Evaluates one single condition (can be negated)
    private static bool EvaluateSingle(string condition)
    {
        // Try to get optional condition from global dictionary
        if (Conditions != null && Conditions.TryGetValue(condition, out Func<bool> callback))
        {
            // Just call the function
            // We don't cache anything
            return callback();
        }
        else if (condition.StartsWith("HasConfig(") && condition.EndsWith(")"))
        {
            OCBNET.ModConfigs mods = OCBNET.ModConfigs.Instance;
            var conditions = condition.Substring(10, condition.Length - 11).Split(';');
            if (conditions.Length > 1)
            {
                if (mods.GetConfigs(conditions[0]) is HashSetList<string> configs)
                {
                    for (int i = 1; i < conditions.Length; i += 1)
                    {
                        if (!configs.hashSet.Contains(conditions[i])) return false;
                    }
                    return true;
                }
            }
            return false;

        }
        // Otherwise check if a mod with that name exists
        // ToDo: maybe do something with ModInfo.version?
        else if (ModManager.GetMod(condition) != null)
        {
            return true;
        }
        // Otherwise it's false
        // Unknown tests too
        return false;
    }

    // Evaluate a comma separated list of conditions
    // The results are logically `and'ed` together
    public static bool Evaluate(string conditions)
    {
        // Ignore if condition is empty or null
        if (string.IsNullOrEmpty(conditions)) return false;
        // Split comma separated list (no whitespace allowed yet)
        foreach (string value in conditions.Split(','))
        {
            bool result = true;
            string condition = value.Trim();
            if (string.IsNullOrEmpty(condition)) continue;
            // Try to find version comparator
            int notpos = condition[0] == '!' ? 1 : 0;
            int ltpos = condition.IndexOf("<");
            int gtpos = condition.IndexOf(">");
            int lepos = condition.IndexOf("≤");
            int gepos = condition.IndexOf("≥");
            int letpos = condition.IndexOf("<=");
            int getpos = condition.IndexOf(">=");
            int length = condition.Length - notpos;
            if (ltpos != -1) length = ltpos - notpos;
            else if (gtpos != -1) length = gtpos - notpos;
            else if (lepos != -1) length = lepos - notpos;
            else if (gepos != -1) length = gepos - notpos;
            else if (letpos != -1) length = letpos - notpos;
            else if (getpos != -1) length = getpos - notpos;
            string name = condition.Substring(notpos, length);
            int off = getpos != -1 || letpos != -1 ? 2 : 1;
            if (length != condition.Length - notpos)
            {
                if (ModManager.GetMod(name) is Mod mod)
                {
                    string version = condition.Substring(notpos + length + off);
                    Version having = Version.Parse(mod.ModInfo?.Version?.Value?.Trim());
                    Version testing = Version.Parse(version.Trim());
                    if (ltpos != -1) result = having < testing;
                    if (gtpos != -1) result = having > testing;
                    if (lepos != -1) result = having <= testing;
                    if (gepos != -1) result = having >= testing;
                    if (letpos != -1) result = having <= testing;
                    if (getpos != -1) result = having >= testing;
                }
                else
                {
                    result = false;
                }
            }
            else if (!EvaluateSingle(name))
            {
                result = false;
            }

            if (notpos == 1) result = !result;
            if (result == false) return false;
        }

        // Something was true
        return true;
    }

}
