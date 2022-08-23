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
using System.IO;
using System.Reflection;

// Class to implement lazy loading of custom game prefs
// Unfortunately dedicated server reads the values before being patched
// Therefore we have no chance to use the enums there directly
public class CustomGamePrefDedi
{

    // Re-use the parser for regular config file
    // Basically lazy parsing another file on mod init
    static readonly MethodInfo FnLoadConfigFile = AccessTools
        .Method(typeof(GameStartupHelper), "LoadConfigFile");

    // Call this function once and as early as possible
    // ToDo: should we add an implicit unique safe-guard?
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

    // Helper function copying mostly how vanilla gets
    // the path for the server config xml. This function
    // will replace it with a ".core.xml" extension.
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
