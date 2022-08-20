using ICSharpCode.WpfDesign.XamlDom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace OCBNET
{
    public class ModConfigs : SingletonInstance<ModConfigs>
    {

        Dictionary<string, HashSetList<string>> Dependecies;

        Dictionary<string, HashSetList<string>> Configs;

        bool DebugLoadOrder = true;

        public List<Mod> LoadOrder;

        public ModConfigs()
        {
            Dependecies = new Dictionary<string, HashSetList<string>>();
            Configs = new Dictionary<string, HashSetList<string>>();
            LoadOrder = new List<Mod>(ModManager.GetLoadedMods());
            foreach (var mod in LoadOrder)
            {
                try
                {
                    XmlFile xml = new XmlFile(mod.Path, "ModConfig.xml");
                    foreach (XmlNode child in xml.XmlDoc.DocumentElement.ChildNodes)
                    {
                        switch (child.NodeType)
                        {
                            case XmlNodeType.Element:
                                switch (child.Name)
                                {
                                    case "ModConfig":
                                        ParseModConfig(mod, (PositionXmlElement)child);
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
            // Re-sort for load order
            LoadOrder.Sort(delegate (Mod a, Mod b) {
                return HasDependency(b, a) ? -1 : 1;
            });
            // Enable debug for now to check it if needed
            if (DebugLoadOrder)
            {
                Log.Out("Load XML in the following order:");
                foreach (Mod mod in LoadOrder)
                {
                    Log.Out("  {0}", mod.ModInfo.Name.Value);
                }
            }
        }

        private void ParseModConfig(Mod mod, PositionXmlElement node)
        {
            foreach (XmlNode child in node.ChildNodes)
            {
                switch (child.NodeType)
                {
                    case XmlNodeType.Element:
                        switch (child.Name)
                        {
                            case "Require":
                            case "After":
                                foreach (XmlAttribute attr in child.Attributes)
                                {
                                    if (attr.Name != "mod")
                                    {
                                        Log.Warning(string.Format("Unknown attribute found: {0} (file {1}, line {2})",
                                            child.Name, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                                    }
                                    else if (child.Name == "Require" && ModManager.GetMod(attr.Value) == null)
                                    {
                                        Log.Error("Required mod {0} for {1} not loaded",
                                            attr.Value, mod.ModInfo.Name.Value);
                                        // throw new Exception("Required mod not loaded");
                                    }
                                    else
                                    {
                                        AddDependency(mod, attr.Value);
                                    }
                                }
                                continue;
                            case "Config":
                                string name = null;
                                string value = null;
                                foreach (XmlAttribute attr in child.Attributes)
                                {
                                    if (attr.Name == "name")
                                    {
                                        if (name != null)
                                        {
                                            Log.Warning(string.Format("Name attribute given twice: {0} (file {1}, line {2})",
                                                child.Name, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                                        }
                                        name = attr.Value;
                                    }
                                    else if (attr.Name == "value")
                                    {
                                        if (value != null)
                                        {
                                            Log.Warning(string.Format("Value attribute given twice: {0} (file {1}, line {2})",
                                                child.Name, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                                        }
                                        value = attr.Value;
                                    }
                                    else
                                    {
                                        Log.Warning(string.Format("Unknown attribute found: {0} (file {1}, line {2})",
                                            child.Name, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                                    }
                                }
                                if (name == null)
                                {
                                    Log.Warning(string.Format("Config is missing name attribute: {0} (file {1}, line {2})",
                                        child.Name, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                                }
                                else if (value == null)
                                {
                                    Log.Warning(string.Format("Config is missing value attribute: {0} (file {1}, line {2})",
                                        child.Name, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
                                }
                                else
                                {
                                    AddConfig(name, value);
                                }
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

        private void AddDependency(Mod mod, string value)
        {
            string name = mod.ModInfo.Name.Value;
            if (!Dependecies.TryGetValue(name, out HashSetList<string> deps))
            {
                deps = new HashSetList<string>();
                Dependecies.Add(name, deps);
            }
            // Check for circular dependency
            if (HasDependency(value, name))
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
                mod.ModInfo.Name.Value,
                dep.ModInfo.Name.Value);
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
