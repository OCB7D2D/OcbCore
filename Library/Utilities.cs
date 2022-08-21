using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

class Utilities
{
    public static Dictionary<string, string> ParseKeyValueList(string cfg)
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

}
