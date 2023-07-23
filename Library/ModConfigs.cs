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
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace OCBNET
{
    public class ModConfigs : SingletonInstance<ModConfigs>, ISingletonInstance
    {
        readonly Dictionary<string, HashSetList<string>> Dependecies;

        readonly Dictionary<string, HashSetList<string>> Conditions;

        // ToDo: config is loaded in original order
        readonly Dictionary<string, HashSetList<string>> Configs;

        public bool DebugLoadOrder = true;

        public List<Mod> LoadOrder;

        public ModConfigs()
        {
            Dependecies = new Dictionary<string, HashSetList<string>>();
            Conditions = new Dictionary<string, HashSetList<string>>();
            Configs = new Dictionary<string, HashSetList<string>>();
            LoadOrder = new List<Mod>(ModManager.GetLoadedMods());
        }

        void ISingletonInstance.Init()
        {
            foreach (var mod in LoadOrder)
            {
                try
                {
                    XmlFile xml = new XmlFile(mod.Path, "ModConfig.xml");
                    ParseModConfig(mod, xml.XmlDoc.Root);
                }
                catch (DirectoryNotFoundException)
                {
                    // Log.Out("Directory not found {0}", er);
                }
                catch (FileNotFoundException)
                {
                    // Log.Out("File not found {0}", er);
                }
                //catch (System.Exception ex)
                //{
                //    Log.Out("Got IO Execption {0}", ex.GetType().Name);
                //}
            }
            // Remove mods which that fail their conditions
            LoadOrder.RemoveAll(mod => !IsModEnabled(mod));
            // Re-sort for load order
            LoadOrder.Sort(delegate (Mod a, Mod b) {
                return HasDependency(a, b) ? 1 :
                       HasDependency(b, a) ? -1 :
                    a.FolderName.CompareTo(b.FolderName);
            });
            // Enable debug for now to check it if needed
            if (DebugLoadOrder)
            {
                Log.Out("Load XML in the following order:");
                foreach (Mod mod in LoadOrder)
                    Log.Out("  {0}", mod.Name);
            }
        }

        public bool IsModEnabled(Mod mod)
        {
            string name = mod.Name;
            if (Conditions.TryGetValue(name, out var conditions))
            {
                foreach (string condition in conditions.list)
                {
                    if (ModConditions.Evaluate(condition)) return true;
                }
                return false;
            }
            return true;
        }

        private void ParseModConfig(Mod mod, XElement node)
        {
            foreach (XElement child in node.Elements())
            {

                string op = null;
                string key = null;
                string name = null;
                string value = null;

                switch (child.NodeType)
                {
                    case XmlNodeType.Element:
                        switch (child.Name.LocalName)
                        {
                            case "Assert":
                                foreach (XAttribute attr in child.Attributes())
                                {
                                    if (attr.Name.LocalName != "condition")
                                    {
                                        Log.Warning(string.Format("Unknown attribute found: {0} (file {1}, line {2})",
                                            attr.Name.LocalName, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                                    }
                                    else
                                    {
                                        if (!ModConditions.Evaluate(attr.Value))
                                        {
                                            Log.Error("Assertion '{0}' failed for {1}",
                                                attr.Value, mod.Name);
                                            Log.Warning("Something with installed mods is wrong!");
                                        }
                                    }
                                }
                                continue;
                            case "Require":
                            case "After":
                                foreach (XAttribute attr in child.Attributes())
                                {
                                    if (attr.Name.LocalName != "mod")
                                    {
                                        Log.Warning(string.Format("Unknown attribute found: {0} (file {1}, line {2})",
                                            attr.Name.LocalName, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                                    }
                                    else if (child.Name.LocalName == "Require" && ModManager.GetMod(attr.Value) == null)
                                    {
                                        Log.Error("Required mod {0} for {1} not loaded",
                                            attr.Value, mod.Name);
                                        // throw new Exception("Required mod not loaded");
                                    }
                                    else
                                    {
                                        AddDependency(mod, attr.Value);
                                    }
                                }
                                continue;
                            case "Config":
                                foreach (XAttribute attr in child.Attributes())
                                {
                                    if (attr.Name.LocalName == "name")
                                    {
                                        if (name != null)
                                        {
                                            Log.Warning(string.Format("Name attribute given twice: {0} (file {1}, line {2})",
                                                child.Name.LocalName, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                                        }
                                        name = attr.Value;
                                    }
                                    else if (attr.Name.LocalName == "value")
                                    {
                                        if (value != null)
                                        {
                                            Log.Warning(string.Format("Value attribute given twice: {0} (file {1}, line {2})",
                                                child.Name.LocalName, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                                        }
                                        value = attr.Value;
                                    }
                                    else
                                    {
                                        Log.Warning(string.Format("Unknown attribute found: {0} (file {1}, line {2})",
                                            attr.Name.LocalName, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                                    }
                                }
                                if (name == null)
                                {
                                    Log.Warning(string.Format("Config is missing name attribute: {0} (file {1}, line {2})",
                                        child.Name.LocalName, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                                }
                                else if (value == null)
                                {
                                    Log.Warning(string.Format("Config is missing value attribute: {0} (file {1}, line {2})",
                                        child.Name.LocalName, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                                }
                                else
                                {
                                    AddConfig(name, value);
                                }
                                continue;
                            case "ConfigOp":
                                foreach (XAttribute attr in child.Attributes())
                                {
                                    if (attr.Name.LocalName == "op")
                                    {
                                        if (op != null)
                                        {
                                            Log.Warning(string.Format("Op attribute given twice: {0} (file {1}, line {2})",
                                                child.Name.LocalName, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                                        }
                                        op = attr.Value;
                                    }
                                    else if (attr.Name.LocalName == "name")
                                    {
                                        if (name != null)
                                        {
                                            Log.Warning(string.Format("Name attribute given twice: {0} (file {1}, line {2})",
                                                child.Name.LocalName, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                                        }
                                        name = attr.Value;
                                    }
                                    else if (attr.Name.LocalName == "key")
                                    {
                                        if (key != null)
                                        {
                                            Log.Warning(string.Format("Key attribute given twice: {0} (file {1}, line {2})",
                                                child.Name.LocalName, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                                        }
                                        key = attr.Value;
                                    }
                                    else if (attr.Name.LocalName == "value")
                                    {
                                        if (value != null)
                                        {
                                            Log.Warning(string.Format("Value attribute given twice: {0} (file {1}, line {2})",
                                                child.Name.LocalName, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                                        }
                                        value = attr.Value;
                                    }
                                    else
                                    {
                                        Log.Warning(string.Format("Unknown attribute found: {0} (file {1}, line {2})",
                                            attr.Name.LocalName, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                                    }
                                }
                                if (op == null)
                                {
                                    Log.Warning(string.Format("Config is missing op attribute: {0} (file {1}, line {2})",
                                        child.Name.LocalName, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                                }
                                else if (name == null)
                                {
                                    Log.Warning(string.Format("Config is missing name attribute: {0} (file {1}, line {2})",
                                        child.Name.LocalName, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                                }
                                else if (key == null)
                                {
                                    Log.Warning(string.Format("Config is missing key attribute: {0} (file {1}, line {2})",
                                        child.Name.LocalName, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                                }
                                else if (value == null)
                                {
                                    Log.Warning(string.Format("Config is missing value attribute: {0} (file {1}, line {2})",
                                        child.Name.LocalName, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                                }
                                else
                                {
                                    OpConfig(op, name, key, value);
                                }
                                continue;
                            case "Enum":
                                ParseModConfigEnum(child);
                                continue;
                            case "Condition":
                                ParseModCondition(mod, child);
                                continue;
                            default:
                                Log.Warning(string.Format("Unknown element found: {0} (file {1}, line {2})",
                                    child.Name, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                                continue;
                        }
                    case XmlNodeType.Comment:
                    case XmlNodeType.Whitespace:
                        continue;
                    default:
                        Log.Error("Unexpected XML node: {0} at line {1}",
                            child.NodeType, ((IXmlLineInfo)child).LineNumber.ToString());
                        continue;
                }
            }
        }

        private void ParseModConfigEnum(XElement child)
        {
            string type = null;
            string name = null;
            bool bitwise = false;
            foreach (XAttribute attr in child.Attributes())
            {
                if (attr.Name.LocalName == "name")
                {
                    if (name != null)
                    {
                        Log.Warning(string.Format("Name attribute given twice: {0} (file {1}, line {2})",
                            child.Name.LocalName, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                    }
                    name = attr.Value;
                }
                else if (attr.Name.LocalName == "type")
                {
                    if (type != null)
                    {
                        Log.Warning(string.Format("Type attribute given twice: {0} (file {1}, line {2})",
                            child.Name.LocalName, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                    }
                    type = attr.Value;
                }
                else if (attr.Name.LocalName == "bitwise")
                {
                    bitwise = bool.Parse(attr.Value);
                }
                else
                {
                    Log.Warning(string.Format("Unknown attribute found: {0} (file {1}, line {2})",
                        attr.Name.LocalName, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                }
            }
            if (name == null)
            {
                Log.Warning(string.Format("Config is missing name attribute: {0} (file {1}, line {2})",
                    child.Name.LocalName, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
            }
            else if (type == null)
            {
                Log.Warning(string.Format("Config is missing type attribute: {0} (file {1}, line {2})",
                    child.Name.LocalName, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
            }
            else
            {
                CustomEnums.Add(type, name, bitwise);
            }
        }

        private void ParseModCondition(Mod mod, XElement child)
        {
            string condition = null;
            foreach (XAttribute attr in child.Attributes())
            {
                if (attr.Name.LocalName == "condition")
                {
                    if (condition != null)
                    {
                        Log.Warning(string.Format("Condition attribute given twice: {0} (file {1}, line {2})",
                            child.Name.LocalName, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                    }
                    condition = attr.Value;
                }
                else
                {
                    Log.Warning(string.Format("Unknown attribute found: {0} (file {1}, line {2})",
                        attr.Name.LocalName, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                }
            }
            if (condition == null)
            {
                Log.Warning(string.Format("Config is missing condition attribute: {0} (file {1}, line {2})",
                    child.Name.LocalName, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
            }
            else
            {
                string name = mod.Name;
                if (!Conditions.TryGetValue(name, out var set))
                    set = new HashSetList<string>();
                Conditions[name] = set;
                set.Add(condition);
            }
        }

        private void AddDependency(Mod mod, string value)
        {
            string name = mod.Name;
            if (!Dependecies.TryGetValue(name, out HashSetList<string> deps))
            {
                deps = new HashSetList<string>();
                Dependecies.Add(name, deps);
            }
            // Check for circular dependency
            if (name == value || HasDependency(value, name))
            {
                Log.Error("Circular Mod Dependency detected");
                Log.Error(" {0} => {1} => {0}", name, value);
                throw new ArgumentException("Circular Mod Dependency");
            }
            Log.Out("Mod {0} requires {1}", name, value);
            deps.Add(value);
        }

        public bool HasDependency(string mod, string dep)
        {
            if (!Dependecies.TryGetValue(mod,
                out HashSetList<string> deps)) return false;
            foreach (string dependency in deps.hashSet)
            {
                if (dependency == dep) return true;
                if (HasDependency(dependency, dep)) return true;
            }
            return false;
        }

        public bool HasDependency(Mod mod, Mod dep)
        {
            return HasDependency(
                mod.Name, dep.Name);
        }

        public HashSetList<string> GetDependencies(string mod)
        {
            return Dependecies.TryGetValue(mod,
                out var deps) ? deps : null;
        }

        public List<string> GetModsWithDependency(string mod, string dep = null)
        {
            return null;
        }

        private void OpConfig(string op, string name, string key, string value)
        {
            Log.Warning("==== Execute OpConfig");
            switch (op)
            {
                case "append":
                    if (Configs.TryGetValue(name, out HashSetList<string> configs))
                    {
                        Log.Out("Extend custom thing");
                    }
                    else if (EnumUtils.Parse<EnumGamePrefs>(name, false) is EnumGamePrefs et)
                    {
                        Log.Out("Extend existing thing");
                    }
                    else
                    {
                        Log.Error("Invalid name {0} for GamePrefs", name);
                    }
                    break;
                default:
                    Log.Error("Invalid operation {0} for GamePrefs", op);
                    break;
            }
        }

        private void AddConfig(string name, string value)
        {
            if (!Configs.TryGetValue(name, out HashSetList<string> configs))
            {
                configs = new HashSetList<string>();
                Configs.Add(name, configs);
            }
            configs.Add(value);
        }

        public HashSetList<string> GetConfigs(string name)
        {
            return Configs.TryGetValue(name,
                out var configs) ? configs : null;
        }

        public string GetLastConfig(string name)
        {
            var configs = GetConfigs(name);
            if (configs == null) return null;
            if (configs.list.Count == 0) return null;
            return configs.list[configs.list.Count - 1];
        }

        public string GetFirstConfig(string name)
        {
            var configs = GetConfigs(name);
            if (configs == null) return null;
            if (configs.list.Count == 0) return null;
            return configs.list[0];
        }

        public bool HasConfig(string name, string value)
        {
            if (GetConfigs(name) is HashSetList<string> configs)
                return configs.hashSet.Contains(value);
            return false;
        }

    }

}
