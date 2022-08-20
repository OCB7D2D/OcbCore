﻿/* MIT License

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
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;

static class ModXmlPatcher
{

    // We need to call into the private function to proceed with XML patching
    private static readonly MethodInfo MethodSinglePatch = AccessTools.Method(typeof(XmlPatcher), "singlePatch");

    // Function to load another XML file and basically call the same PatchXML function again
    private static bool IncludeAnotherDocument(XmlFile target, XmlFile parent, XmlElement element, string modName)
    {
        bool result = true;
        foreach (XmlAttribute attr in element.Attributes)
        {
            // Skip unknown attributes
            if (attr.Name != "path") continue;
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
    public static bool PatchXml(XmlFile xmlFile, XmlFile patchXml, XmlElement node, string patchName)
    {
        bool result = true;
        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType == XmlNodeType.Element)
            {
                if (!(child is XmlElement element)) continue;
                // Patched to support includes
                if (child.Name == "include")
                {
                    // Will do the magic by calling our functions again
                    IncludeAnotherDocument(xmlFile, patchXml, element, patchName);
                }
                // Otherwise try to apply the patches found in child element
                else if (!ApplyPatchEntry(xmlFile, patchXml, element, patchName))
                {
                    IXmlLineInfo lineInfo = (IXmlLineInfo)element;
                    Log.Warning(string.Format("XML patch for \"{0}\" from mod \"{1}\" did not apply: {2} (line {3} at pos {4})",
                        xmlFile.Filename, patchName, element.GetElementString(), lineInfo.LineNumber, lineInfo.LinePosition));
                    result = false;
                }
            }
        }
        return result;
    }

    // Flags for consecutive mod-if parsing
    private static bool IfClauseParsed = false;
    private static bool PreviousResult = false;

    // Entry point instead of (private) `XmlPatcher.singlePatch`
    // Implements conditional patching and also allows includes
    private static bool ApplyPatchEntry(XmlFile _xmlFile, XmlFile _patchXml, XmlElement _patchElement, string _patchName)
    {

        // Only support root level
        switch (_patchElement.Name)
        {

            case "include":

                // Call out to our include handler
                return IncludeAnotherDocument(_xmlFile, _patchXml,
                    _patchElement, _patchName);

            case "modif":

                // Reset flags first
                IfClauseParsed = true;
                PreviousResult = false;

                // Check if we have true conditions
                foreach (XmlAttribute attr in _patchElement.Attributes)
                {
                    // Ignore unknown attributes for now
                    if (attr.Name != "condition")
                    {
                        Log.Warning("Ignoring unknown attribute {0}", attr.Name);
                        continue;
                    }
                    // Evaluate one or'ed condition
                    if (ModConditions.Evaluate(attr.Value))
                    {
                        PreviousResult = true;
                        return PatchXml(_xmlFile, _patchXml,
                            _patchElement, _patchName);
                    }
                }

                // Nothing failed!?
                return true;

            case "modelsif":

                // Check for correct parser state
                if (!IfClauseParsed)
                {
                    Log.Error("Found <modelsif> clause out of order");
                    return false;
                }

                // Abort else when last result was true
                if (PreviousResult) return true;

                // Reset flags first
                PreviousResult = false;

                // Check if we have true conditions
                foreach (XmlAttribute attr in _patchElement.Attributes)
                {
                    // Ignore unknown attributes for now
                    if (attr.Name != "condition")
                    {
                        Log.Warning("Ignoring unknown attribute {0}", attr.Name);
                        continue;
                    }
                    // Evaluate one or'ed condition
                    if (ModConditions.Evaluate(attr.Value))
                    {
                        PreviousResult = true;
                        return PatchXml(_xmlFile, _patchXml,
                            _patchElement, _patchName);
                    }
                }

                // Nothing failed!?
                return true;

            case "modelse":

                // Abort else when last result was true
                if (PreviousResult) return true;

                // Reset flags first
                IfClauseParsed = false;
                PreviousResult = false;

                return PatchXml(_xmlFile, _patchXml,
                    _patchElement, _patchName);

            default:
                // Reset flags first
                IfClauseParsed = false;
                PreviousResult = true;
                // Dispatch to original function
                return (bool)MethodSinglePatch.Invoke(null,
                    new object[] { _xmlFile, _patchElement, _patchName });
        }
    }

    // Hook into vanilla XML Patcher
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
            XmlElement element = _patchXml.XmlDoc.DocumentElement;
            if (element == null) return false;
            string version = element.GetAttribute("patcher-version");
            if (!string.IsNullOrEmpty(version))
            {
                // Check if version is too new for us
                if (int.Parse(version) > 2) return true;
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
