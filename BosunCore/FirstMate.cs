using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LogMonitor;
using System.Threading;
using System.Net;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace BosunCore
{
    public delegate void SystemArrivalHandler(string name, long eddbid, string eddburl);

    /// <summary>
    /// Collects useful things. Such as our Commander's Name and Current Start System Name.
    /// 
    /// Also attempts to give you an EDDB Url for the system.
    /// </summary>
    public class FirstMate
    {
        ShipLocator logmon { get; set; }
        bool stop = false;
        Thread monThread = null;

        ManualResetEvent waiter = new ManualResetEvent(false);
        
        WatchKeeper server = null;
        Dictionary<string, long> eddbSystems = new Dictionary<string, long>();

        public FirstMate(ShipLocator lm)
        {
            logmon = lm;

            lm.EnterStarSystem += OnEnterSystem;
            lm.BeginDocking += OnDockingRequestGranted;
            lm.FoundCommander += OnFoundCommander;            
            logmon.Update();
        }

        public int WatchKeeperPort
        {
            get
            {
                return server.Port;
            }
        }

        public event SystemArrivalHandler StarSystemEntered;
        public event LogMonitor.StationDockStartHandler DockingRequestGranted;
        public event LogMonitor.CommanderInfoFound FoundCommander;

        public string CommanderName
        {
            get
            {
                return logmon.Commander;
            }
        }

        public string LastSystemName
        {
            get
            {
                return logmon.LastStarSystem;
            }
        }

        public bool Docking
        {
            get
            {
                return logmon.RecentlyNearStation;
            }
        }

        void NotifyChanged()
        {
            waiter.Set();
            waiter.Reset();
        }

        void OnFoundCommander(string name)
        {
            if (FoundCommander != null)
            {
                FoundCommander(name);
            }
            NotifyChanged();
        }

        void OnEnterSystem(long id, string name)
        {
            if (StarSystemEntered != null)
            {
                long sysid;
                if (LookupEDDBSystemID(name, out sysid))
                {
                    StarSystemEntered(name, sysid, GetEDDBSystemUrl(name));
                }
            }

            NotifyChanged();
        }

        void OnDockingRequestGranted()
        {
            if (DockingRequestGranted != null)
            {
                DockingRequestGranted();
            }
            NotifyChanged();
        }

        void LogMonitorThread(object o)
        {
            while (!stop)
            {
                Thread.Sleep(1000);
                lock (logmon)
                {
                    logmon.Update();
                }
            }
        }

        public void Start()
        {
            if (server == null)
            {
                server = new WatchKeeper(this);
                server.BeginListen();
            }        
            if (monThread == null)
            {
                lock (logmon)
                {
                    stop = false;
                    var mt = new Thread(LogMonitorThread);
                    mt.Start();
                    monThread = mt;
                }
            }
        }

        public void Stop()
        {
            lock (logmon)
            {
                stop = true;
                try
                {
                    server.StopServer();
                }
                finally
                {
                    server = null;
                }
            }
        }

        public void Wait()
        {
            waiter.WaitOne();
        }

        public string GetEDDBSystemUrl(string name)
        {
            long sysid;
            if (LookupEDDBSystemID(name, out sysid))
            {
                return String.Format("http://eddb.io/system/{0}", sysid);
            }
            return null;
        }

        public bool LookupEDDBSystemID(string name, out long sysid)
        {            
            sysid = -1;
            if (Monitor.TryEnter(eddbSystems)) {
                try
                {
                    if (!eddbSystems.ContainsKey(name))
                    {
                        var eddburl = String.Format("http://eddb.io/system/search?system%5Bmultiname%5D={0}", name);
                        var jsonstr = GetJsonString(eddburl);

                        dynamic data = JsonConvert.DeserializeObject(jsonstr);

                        foreach (var item in data) {
                            try
                            {
                                sysid = long.Parse(item.id.ToString());
                                eddbSystems[item.name.ToString()] = sysid;
                            }
                            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { }
                            catch (FormatException) { }                            
                        }
                    }
                    return eddbSystems.TryGetValue(name, out sysid);
                }
                finally
                {
                    Monitor.Exit(eddbSystems);
                }
            }
            return false;
        }

        string GetJsonString(string url)
        {
            // Shamelessly stolen from
            // http://stackoverflow.com/questions/8270464/best-way-to-call-a-json-webservice-from-a-net-console
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            try
            {
                WebResponse response = request.GetResponse();
                using (Stream responseStream = response.GetResponseStream())
                {
                    StreamReader reader = new StreamReader(responseStream, Encoding.UTF8);
                    return reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                WebResponse errorResponse = ex.Response;
                using (Stream responseStream = errorResponse.GetResponseStream())
                {
                    StreamReader reader = new StreamReader(responseStream, Encoding.GetEncoding("utf-8"));
                    String errorText = reader.ReadToEnd();
                    Console.Error.WriteLine(errorText);
                }
                throw;
            }
        }
    }
}
