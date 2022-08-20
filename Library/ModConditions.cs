using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                if (mods.GetConfigs(conditions[0]) is List<string> configs)
                {
                    for (int i = 1; i < conditions.Length; i += 1)
                    {
                        if (!configs.Contains(conditions[i])) return false;
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
        foreach (string condition in conditions.Split(','))
        {
            bool result = true;
            // Try to find version comparator
            int notpos = condition[0] == '!' ? 1 : 0;
            int ltpos = condition.IndexOf("<");
            int gtpos = condition.IndexOf(">");
            int lepos = condition.IndexOf("≤");
            int gepos = condition.IndexOf("≥");
            int length = condition.Length - notpos;
            if (ltpos != -1) length = ltpos - notpos;
            else if (gtpos != -1) length = gtpos - notpos;
            else if (lepos != -1) length = lepos - notpos;
            else if (gepos != -1) length = gepos - notpos;
            string name = condition.Substring(notpos, length);
            if (length != condition.Length - notpos)
            {
                if (ModManager.GetMod(name) is Mod mod)
                {
                    string version = condition.Substring(notpos + length + 1);
                    Version having = Version.Parse(mod.ModInfo?.Version?.Value);
                    Version testing = Version.Parse(version);
                    if (ltpos != -1) result = having < testing;
                    if (gtpos != -1) result = having > testing;
                    if (lepos != -1) result = having <= testing;
                    if (gepos != -1) result = having >= testing;
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
