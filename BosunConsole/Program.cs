﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using BosunCore;
using LogMonitor;

namespace BosunConsole
{
    class Program
    {
        static void LogMessage(string fmp, params object[] args)
        {
            Console.Out.WriteLine("{0}: {1}", DateTime.Now.ToString("s"),
                String.Format(fmp, args));
        }

        static void Main(string[] args)
        {
            if ((args.Length > 0) && Directory.Exists(args[0]))
            {
                ShipLocator.OverrideEDFolder(args[0]);
                var sl = new ShipLocator();

                var bc = new BosunCore.FirstMate(sl);

                if (bc.CommanderName != null)
                {
                    bc_FoundCommander(bc.CommanderName);
                }
                if (bc.LastSystemName != null)
                {
                    long sysid;
                    LogMessage("Bosun Ready, last recorded system is {0}", bc.LastSystemName);
                    if (bc.LookupEDDBSystemID(bc.LastSystemName, out sysid))
                    {
                        LogMessage("EDDB: http://eddb.io/system/{0} - {1}", sysid, bc.LastSystemName);
                    }
                }

                bc.FoundCommander += bc_FoundCommander;
                bc.StarSystemEntered += sl_EnterStarSystem;
                bc.DockingRequestGranted += bc_DockingRequestGranted;

                bc.Start();

                while (true)
                {
                    bc.Wait();
                }

            }
            else
            {
                Console.WriteLine("Usage: bosunconsole PATH_TO_ED_FOLDER");
                Console.ReadKey();
            }

        }

        static void bc_FoundCommander(string name)
        {
            LogMessage("Hello CMDR {0}", name);
        }

        static void bc_DockingRequestGranted()
        {
            LogMessage("Docking");
        }

        static void sl_EnterStarSystem(string name, long id, string url)
        {
            LogMessage("Arrived at {0}", name);
            LogMessage("EDDB: {0}", url);
        }

        
    }
}