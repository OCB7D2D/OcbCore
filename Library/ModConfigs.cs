using ICSharpCode.WpfDesign.XamlDom;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace OCBNET
{
    public class ModConfigs : SingletonInstance<ModConfigs>
    {

        Dictionary<string, List<string>> Dependecies;

        Dictionary<string, List<string>> Configs;

        public ModConfigs()
        {
            Dependecies = new Dictionary<string, List<string>>();
            Configs = new Dictionary<string, List<string>>();
            foreach (var mod in ModManager.GetLoadedMods())
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
                            case "Dependency":
                                foreach (XmlAttribute attr in child.Attributes)
                                {
                                    if (attr.Name != "value")
                                    {
                                        Log.Warning(string.Format("Unknown attribute found: {0} (file {1}, line {2})",
                                            child.Name, "ModConfig.xml", ((IXmlLineInfo)child).LineNumber));
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

        private void AddConfig(string name, string value)
        {
            List<string> configs = null;
            if (!Configs.TryGetValue(name, out configs))
            {
                configs = new List<string>();
                Configs.Add(name, configs);
            }
            configs.Add(value);
        }

        private void AddDependency(Mod mod, string value)
        {
            List<string> deps = null;
            if (!Dependecies.TryGetValue(mod.ModInfo.Name.Value, out deps))
            {
                deps = new List<string>();
                Dependecies.Add(mod.ModInfo.Name.Value, deps);
            }
            deps.Add(value);
        }

        public List<string> GetDependencies(string mod)
        {
            return Dependecies.TryGetValue(mod,
                out var deps) ? deps : null;
        }

        public List<string> GetConfigs(string mod)
        {
            return Configs.TryGetValue(mod,
                out var configs) ? configs : null;
        }

        public string GetLastConfig(string mod)
        {
            var configs = GetConfigs(mod);
            if (configs == null) return null;
            if (configs.Count == 0) return null;
            return configs[configs.Count - 1];
        }

        public string GetFirstConfig(string mod)
        {
            var configs = GetConfigs(mod);
            if (configs == null) return null;
            if (configs.Count == 0) return null;
            return configs[0];
        }

    }

}
