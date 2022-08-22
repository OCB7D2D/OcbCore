using HarmonyLib;
using OCBNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using UnityEngine;

// Class to implement lazy loading of custom game prefs
// Unfortunately dedicated server reads the values before being patched
// Therefore we have no chance to use the enums there directly
public class CustomGamePrefDedi
{

    // Re-use the parser for regular config file
    // Basically lazy parsing another file on mod init
    static readonly MethodInfo FnLoadConfigFile = AccessTools
        .Method(typeof(GameStartupHelper), "LoadConfigFile");

    public static void ApplyCustomServerConfig()
    {
        if (GetCustomServerConfigPath() is string path)
        {
            // Custom File is optional
            if (!File.Exists(path)) return;
            // Invoke original parser again
            FnLoadConfigFile.Invoke(
                GameStartupHelper.Instance,
                new object[] { path });
        }
    }

    public static string GetCustomServerConfigPath()
    {
        foreach (string arg in Environment.GetCommandLineArgs())
        {
            if (!arg.StartsWith("-configfile=")) continue;
            string path = arg.Substring(arg.IndexOf('=') + 1);
            if (!path.Contains("/") && !path.Contains("\\"))
                path = GameIO.GetApplicationPath() + "/" + path;
            path = path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ?
                path.Substring(0, path.Length - 4) + ".core.xml" : path + ".core.xml";
            return path;
        }
        return null;
    }

}
