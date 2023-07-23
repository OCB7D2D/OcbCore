/* MIT License

Copyright (c) 2022 OCB7D2D

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using HarmonyLib;
using OCBNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

static class ModXmlPatcher
{

    // Must be set from outside first, otherwise not much happens
    public static Dictionary<string, Func<bool>> Conditions = null;

    // Evaluates one single condition (can be negated)
    private static bool EvaluateCondition(string condition)
    {
        // Try to get optional condition from global dictionary
        if (Conditions != null && Conditions.TryGetValue(condition, out Func<bool> callback))
        {
            // Just call the function
            // We don't cache anything
            return callback();
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
    private static bool EvaluateConditions(string conditions, XmlFile xml)
    {
        // Ignore if condition is empty or null
        if (string.IsNullOrEmpty(conditions)) return false;
        // Split comma separated list (no whitespace allowed yet)

        if (conditions.StartsWith("xpath:"))
        {
            conditions = conditions.Substring(6);
            foreach (string xpath in conditions.Split(','))
            {
                bool negate = false;
                List<System.Xml.Linq.XElement> xmlNodeList;
                if (xpath.StartsWith("!"))
                {
                    negate = true;
                    xmlNodeList = xml.XmlDoc.XPathSelectElements(
                        xpath.Substring(1)).ToList();
                }
                else
                {
                    xmlNodeList = xml.XmlDoc.XPathSelectElements(xpath).ToList();
                }
                bool result = true;
                if (xmlNodeList == null) result = false;
                if (xmlNodeList.Count == 0) result = false;
                if (negate) result = !result;
                if (!result) return false;
            }
        }
        else
        {
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
                        Version having = mod.Version;
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
                else if (!EvaluateCondition(name))
                {
                    result = false;
                }

                if (notpos == 1) result = !result;
                if (result == false) return false;
            }
        }

        // Something was true
        return true;
    }

    // We need to call into the private function to proceed with XML patching
    private static readonly MethodInfo MethodSinglePatch = AccessTools.Method(typeof(XmlPatcher), "singlePatch");

    // Function to load another XML file and basically call the same PatchXML function again
    private static bool IncludeAnotherDocument(XmlFile target, XmlFile parent, XElement element, string modName)
    {
        bool result = true;
        foreach (XAttribute attr in element.Attributes())
        {
            // Skip unknown attributes
            if (attr.Name.LocalName != "path") continue;
            // Load path relative to previous XML include
            string prev = Path.Combine(parent.Directory, parent.Filename);
            string path = Path.Combine(Path.GetDirectoryName(prev), attr.Value);
            if (File.Exists(path))
            {
                try
                {
                    string _text = File.ReadAllText(path, Encoding.UTF8)
                        .Replace("@modfolder:", "@modfolder(" + modName + "):");
                    XmlFile _patchXml;
                    try
                    {
                        _patchXml = new XmlFile(_text,
                            Path.GetDirectoryName(path),
                            Path.GetFileName(path),
                            true);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("XML loader: Loading XML patch include '{0}' from mod '{1}' failed.", path, modName);
                        Log.Exception(ex);
                        result = false;
                        continue;
                    }
                    result &= XmlPatcher.PatchXml(
                        target, _patchXml, modName);
                }
                catch (Exception ex)
                {
                    Log.Error("XML loader: Patching '" + target.Filename + "' from mod '" + modName + "' failed.");
                    Log.Exception(ex);
                    result = false;
                }
            }
            else
            {
                Log.Error("XML loader: Can't find XML include '{0}' from mod '{1}'.", path, modName);
            }
        }
        return result;
    }

    // Basically the same function as `XmlPatcher.PatchXml`
    // Patched to support `include` and `modif` XML elements

    static int count = 0;

    public static bool PatchXml(XmlFile xmlFile, XmlFile patchXml, XElement node, string patchName)
    {
        bool result = true;
        count++;
        ParserStack stack = new ParserStack();
        stack.count = count;
        foreach (XElement child in node.Elements())
        {
            if (child.NodeType == XmlNodeType.Element)
            {
                if (!(child is XElement element)) continue;
                // Patched to support includes
                if (child.Name == "include")
                {
                    // Will do the magic by calling our functions again
                    IncludeAnotherDocument(xmlFile, patchXml, element, patchName);
                }
                else if (child.Name == "echo")
                {
                    // Log.Out(System.Environment.StackTrace);
                    foreach (XAttribute attr in child.Attributes())
                    {
                        if (attr.Name.LocalName == "log") Log.Out("{1}: {0}", attr.Value, xmlFile.Filename);
                        if (attr.Name.LocalName == "warn") Log.Warning("{1}: {0}", attr.Value, xmlFile.Filename);
                        if (attr.Name.LocalName == "error") Log.Error("{1}: {0}", attr.Value, xmlFile.Filename);
                        if (attr.Name.LocalName != "log" && attr.Name.LocalName != "warn" && attr.Name.LocalName != "error")
                            Log.Warning("Echo has no valid name (log, warn or error)");
                    }
                }
                // Otherwise try to apply the patches found in child element
                else if (!ApplyPatchEntry(xmlFile, patchXml, element, patchName, ref stack))
                {
                    IXmlLineInfo lineInfo = (IXmlLineInfo)element;
                    Log.Warning(string.Format("XML patch for \"{0}\" from mod \"{1}\" did not apply: {2} (line {3} at pos {4})",
                        xmlFile.Filename, patchName, element.ToString(), lineInfo.LineNumber, lineInfo.LinePosition));
                    result = false;
                }
            }
        }
        return result;
    }

    // Flags for consecutive mod-if parsing
    public struct ParserStack
    {
        public int count;
        public bool IfClauseParsed;
        public bool PreviousResult;
    }

    public static string EvaluateTemplate(Match match, int i, Dictionary<string, string> cfg)
    {
        if (match.Groups.Count < 2) return match.Value;
        string str = match.Groups[1].Value.Trim();
        if (str.StartsWith("Mul(") && str.EndsWith(")"))
        {
            int multiplier = int.Parse(str.Substring(4, str.Length - 5));
            return (multiplier * i).ToString();
        }
        else if (str.StartsWith("Val(") && str.EndsWith(")"))
        {
            string name = str.Substring(4, str.Length - 5);
            if (cfg.TryGetValue(name, out string value)) return value;
            return string.Empty; // Value should always be replaced
        }
        return match.Value;
    }

    // Entry point instead of (private) `XmlPatcher.singlePatch`
    // Implements conditional patching and also allows includes
    private static bool ApplyPatchEntry(XmlFile _xmlFile, XmlFile _patchXml, XElement _patchElement, string _patchName, ref ParserStack stack)
    {

        // Only support root level
        switch (_patchElement.Name.ToString())
        {

            case "include":

                // Call out to our include handler
                return IncludeAnotherDocument(_xmlFile, _patchXml,
                    _patchElement, _patchName);

            case "modif":

                // Reset flags first
                stack.IfClauseParsed = true;
                stack.PreviousResult = false;

                // Check if we have true conditions
                foreach (XAttribute attr in _patchElement.Attributes())
                {
                    // Ignore unknown attributes for now
                    if (attr.Name.LocalName != "condition")
                    {
                        Log.Warning("Ignoring unknown attribute {0}", attr.Name.LocalName);
                        continue;
                    }
                    // Evaluate one or'ed condition
                    if (ModConditions.Evaluate(attr.Value))
                    {
                        stack.PreviousResult = true;
                        return PatchXml(_xmlFile, _patchXml,
                            _patchElement, _patchName);
                    }
                }

                // Nothing failed!?
                return true;

            case "modelsif":

                // Check for correct parser state
                if (!stack.IfClauseParsed)
                {
                    Log.Error("Found <modelsif> clause out of order");
                    return false;
                }

                // Abort else when last result was true
                if (stack.PreviousResult) return true;

                // Check if we have true conditions
                foreach (XAttribute attr in _patchElement.Attributes())
                {
                    // Ignore unknown attributes for now
                    if (attr.Name.LocalName != "condition")
                    {
                        Log.Warning("Ignoring unknown attribute {0}", attr.Name.LocalName);
                        continue;
                    }
                    // Evaluate one or'ed condition
                    if (ModConditions.Evaluate(attr.Value))
                    {
                        stack.PreviousResult = true;
                        return PatchXml(_xmlFile, _patchXml,
                            _patchElement, _patchName);
                    }
                }

                // Nothing failed!?
                return true;

            case "modelse":

                // Reset flags first
                stack.IfClauseParsed = false;
                // Abort else when last result was true
                if (stack.PreviousResult) return true;
                return PatchXml(_xmlFile, _patchXml,
                    _patchElement, _patchName);

            case "foreach":

                string template = "<xml>";

                // Check if we have true conditions
                foreach (var node in _patchElement.Nodes())
                {
                    if (node is XCData data)
                    {
                        if (!string.IsNullOrEmpty(template))
                            template += " "; // Add space between
                        template += data.Value;
                    }
                    else
                    {
                        Log.Error("foreach must only contain CDATA nodes", node);
                    }
                }

                string ConfigName = null;

                // Check if we have true conditions
                foreach (var attr in _patchElement.Attributes())
                {
                    // Ignore unknown attributes for now
                    if (attr.Name.LocalName == "config")
                    {
                        ConfigName = attr.Value;
                    }
                }

                if (ConfigName == null)
                {
                    Log.Error("Foreach must name a config");
                    return false;
                }

                var cfgs = ModConfigs.Instance.GetConfigs(ConfigName);

                if (cfgs == null)
                {
                    return true;
                }

                template += "</xml>";

                string pattern = @"{{([^\}]+)}}";
                bool rv = true;

                for (int it = 0; it < cfgs.list.Count; it++)
                {
                    string qwe = cfgs.list[it];
                    var cfg = ParseKeyValueList(cfgs.list[it]);
                    var evaluated = Regex.Replace(template, pattern,
                            match => EvaluateTemplate(match, it, cfg));
                    Log.Out("APPEND {0}", evaluated);

                    XmlFile xml = new XmlFile(
                        evaluated,
                        _xmlFile.Directory,
                        _xmlFile.Filename);
                    rv &= PatchXml(_xmlFile, _patchXml,
                        xml.XmlDoc.Root,
                        _patchName);
                    Log.Out("PAtched {0}", rv);
                }


                return rv;


            default:
                // Reset flags first
                stack.IfClauseParsed = false;
                stack.PreviousResult = true;
                // Dispatch to original function
                return (bool)MethodSinglePatch.Invoke(null,
                    new object[] { _xmlFile, _patchElement, _patchName });
        }
    }

    private static Dictionary<string, string> ParseKeyValueList(string cfg)
    {
        var props = new Dictionary<string, string>();
        foreach (var kv in cfg.Split(';'))
        {
            var kvpair = kv.Trim();
            if (string.IsNullOrEmpty(kvpair)) continue;
            var parts = kvpair.Split(new char[] { '=' }, 2);
            props[parts[0]] = parts.Length == 2 ? parts[1] : null;
        }
        return props;
    }

    // Hook into vanilla XML Patcher
    [HarmonyCondition("HasConfig(OcbCore;XmlPatcher)")]
    [HarmonyPatch(typeof(XmlPatcher))]
    [HarmonyPatch("PatchXml")]
    public class XmlPatcher_PatchXml
    {
        static bool Prefix(
            ref XmlFile _xmlFile,
            ref XmlFile _patchXml,
            ref string _patchName,
            ref bool __result)
        {
            // According to Harmony docs, returning false on a prefix
            // should skip the original and all other prefixers, but
            // it seems that it only skips the original. The other
            // prefixers are still called. The reason for this is
            // unknown, but could be because the game uses HarmonyX.
            // Might also be something solved with latest versions,
            // as the game uses a rather old HarmonyX version (2.2).
            // To address this we simply "consume" one of the args.
            if (_patchXml == null) return false;
            XElement element = _patchXml.XmlDoc.Root;
            if (element == null) return false;
            string version = element.GetAttribute("patcher-version");
            if (!string.IsNullOrEmpty(version))
            {
                // Check if version is too new for us
                if (version != "ocb-core-test") return true;
            }
            // Call out to static helper function
            __result = PatchXml(
                _xmlFile, _patchXml,
                element, _patchName);
            // First one wins
            _patchXml = null;
            return false;
        }
    }

}
