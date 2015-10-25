using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace LogMonitor
{
    public delegate void StarSystemChangedHandler(Int64 id, string name);
    public delegate void StationDockStartHandler();
    public delegate void CommanderInfoFound(string name);
    public delegate void NewLineHandler(string line);

    /// <summary>
    /// Locate your Ship.
    /// </summary>
    public class ShipLocator
    {

        static string WalkDirs(string folder, string find)
        {
            var found = Directory.EnumerateDirectories(folder);
            foreach (var fpath in found)
            {
                if (!System.IO.Directory.Exists(fpath))
                    continue;
                var dname = System.IO.Path.GetFileName(fpath);
                if (dname.StartsWith(find))
                {
                    return fpath;
                }
            }
            foreach (var fpath in found)
            {
                var x = WalkDirs(fpath, find);
                if (!string.IsNullOrEmpty(x))
                {
                    return x;
                }
            }

            return null;
        }

        static string EDFolder = null;

        public static void OverrideEDFolder(string path)
        {
            EDFolder = path;
        }

        static Process EDProcess = null;
        static Process FindEDProcess()
        {
            var procs = Process.GetProcessesByName("EliteDangerous32");
            if (procs.Length == 1)
            {
                var ed = procs[0];
                var edexe = ed.MainModule.FileName;                
                EDProcess = ed;
            }
            return EDProcess;
        }

        
        public static bool CheckEDRunning()
        {
            if (EDProcess == null)
            {
                FindEDProcess();
            }
            if (EDProcess != null)
            {
                return EDProcess.HasExited == false;
            }
            return false;
        }

        public static string FindEDFolder()
        {
            if (EDFolder == null)
            {
                // look for ED running.
                if (CheckEDRunning())
                {
                    var ed = FindEDProcess();
                    if (ed != null)
                    {
                        var edexe = ed.MainModule.FileName;
                        EDFolder = Path.GetDirectoryName(edexe);
                    }
                }
            }
            return EDFolder;
        }

        static string logdir = null;

        public static string GetLogFolder()
        {
            if (logdir == null)
            {
                var fdf = FindEDFolder();
                if (fdf != null)
                {
                    logdir = Path.Combine(fdf, "Logs");
                }
            }
            return logdir;
        }

        bool listedLogs = false;
        public List<string> ListLogs()
        {
            var logs = System.IO.Directory.GetFiles(GetLogFolder(), "netlog*.log");
            int takelogs = 3;
            if (listedLogs)
            {
                takelogs = 1;
            }
            listedLogs = true;
            return new List<string>(logs.Reverse().Take(takelogs));
        }

        public bool RecentlyNearStation { get; private set; }

        public string LastStarSystem { get; private set; }

        public Int64 LastStarSystemID { get; private set; }

        public string Commander { get; private set; }

        public event StarSystemChangedHandler EnterStarSystem;
        public event StationDockStartHandler BeginDocking;
        public event CommanderInfoFound FoundCommander;
        public event NewLineHandler NewLine;

        Dictionary<string, long> positions = new Dictionary<string, long>();

        public bool UpdateAppConfigXml()
        {
            var edf = FindEDFolder();

            if (edf == null) return false;

            var xml = Path.Combine(edf, "appconfig.xml");

            XmlDocument appc = new XmlDocument();
            appc.Load(xml);
            XmlNode network = appc.SelectSingleNode("/AppConfig/Network");
            bool log_enabled = false;
            foreach (XmlAttribute attr in network.Attributes)
            {
                if (attr.Name == "VerboseLogging")
                {
                    if (attr.Value == "1")
                    {
                        log_enabled = true;
                        break;
                    }
                }
            }
            if (!log_enabled)
            {
                var newattr = appc.CreateAttribute("VerboseLogging");
                newattr.Value = "1";
                network.Attributes.Append(newattr);
                appc.Save(xml);
            }
            return log_enabled;
        }


        /// <summary>
        /// Update our StarSystem and Commander information from the logs.
        /// </summary>
        public void Update()
        {
            // read the last lines of the last log.
            var logs = ListLogs();
            var lines = new List<string>();
            logs.Reverse();
            string lastlogfile = null;
            long lastlogpos = 0;
            foreach (var logfile in logs)
            {
                lastlogfile = logfile;
                long start = 0;
                positions.TryGetValue(logfile, out start);

                using (var fs = new FileStream(logfile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    fs.Seek(start, SeekOrigin.Begin);
                    using (var sr = new StreamReader(fs))
                    {
                        string line = null;
                        do
                        {
                            line = sr.ReadLine();
                            if (line != null)
                            {
                                lines.Add(line);
                            }
                        } while (line != null);
                        positions[logfile] = fs.Position;
                        lastlogpos = fs.Position;
                    }
                }
            }
            if (positions.Count > 100)
            {
                // should be rare, we've seen more than 100 logfiles since starting!
                positions.Clear();
                positions[lastlogfile] = lastlogpos;
            }
            Parse(lines);
        }


        public bool ParseCommander(string line)
        {
            if (line.StartsWith("<")) return true;
            if (line.Contains("FindBestIsland"))
            {
                var parts = line.Split(':');
                if (parts.Length > 4)
                {
                    Commander = parts[3];
                    if (FoundCommander != null)
                    {
                        FoundCommander(Commander);
                    }
                    return false;
                }
            }
            return true;
        }

        public bool ParseStartDocking(string line)
        {
            if (line.StartsWith("<")) return true;
            if (line.Contains("Dock Permission Received on"))
            {
                if (BeginDocking != null)
                {
                    BeginDocking();
                }
                RecentlyNearStation = true;
                return false;
            }
            return true;
        }

        Regex psystem = new Regex("System:(\\d+)\\(([^\\)]+)");

        public bool ParseArriveSystem(string line)
        {
            if (line.StartsWith("<")) return true;
            if (line.Contains("System:"))
            {
                var pm = psystem.Match(line);
                if (pm.Success) {
                    long sysid = Int64.Parse(pm.Groups[1].Value);
                    string sysname = pm.Groups[2].Value;
                    if (sysid != LastStarSystemID)
                    {
                        LastStarSystem = sysname;
                        LastStarSystemID = sysid;
                        RecentlyNearStation = false;

                        if (EnterStarSystem != null)
                        {                                    
                            EnterStarSystem(sysid, LastStarSystem);
                        }
                    }                            
                    return false;                
                }
            }
            return true;
        }

 

        /// <summary>
        /// Parse log contents
        /// </summary>
        /// <param name="lines">lines from the netlog (newest first)</param>
        public void Parse(IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                if (NewLine != null)
                    NewLine(line);
                if (ParseArriveSystem(line) ||
                    ParseStartDocking(line) ||
                    ParseCommander(line))
                {
                    // nothing
                }
            }
        }
    }
}
