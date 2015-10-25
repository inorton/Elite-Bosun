using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

namespace BosunCore
{
    public class BosunConfiguration
    {
        static Dictionary<string, string> configs = null;
        
        public static string GetFileName() 
        {
            var ourdll = typeof(BosunConfiguration).Assembly.Location;
            var ourdir = Path.GetDirectoryName(ourdll);
            return Path.Combine(ourdir, "bosun.json");
        }

        public static void Load()
        {
            var filename = GetFileName();
            if (File.Exists(filename))
            {
                var cfgstr = File.ReadAllText(filename);
                configs = JsonConvert.DeserializeObject<Dictionary<string, string>>(cfgstr);
            }
        }

        public static void Save()
        {
            lock (configs)
            {
                var cfgstr = JsonConvert.SerializeObject(configs);
                File.WriteAllText(GetFileName(), cfgstr);
            }
        }

        public static void Set(string keyname, string value)
        {
            lock (configs)
            {
                configs[keyname] = value;
            }
        }

        public static string Read(string keyname, string defaultvalue)
        {
            if (configs == null)
            {
                Load();
            }
            lock (configs)
            {
                string rv = defaultvalue;
                configs.TryGetValue(keyname, out rv);
                return rv;
            }
        }        
    }
}
