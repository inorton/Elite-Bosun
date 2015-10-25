using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XR.Server.Http;
using Newtonsoft.Json;
using System.Reflection;

namespace BosunCore
{
    /// <summary>
    /// Marks REST API methods
    /// </summary>
    internal class RestActionAttribute : Attribute {
    }

    /// <summary>
    /// JSON REST web interface.
    /// </summary>
    public class WatchKeeper : HttpServer
    {
        Dictionary<string, MethodInfo> dispatchMethods = new Dictionary<string, MethodInfo>();
        public FirstMate Mate { get; private set; }

        public WatchKeeper(FirstMate mate)
            : base()
        {
            PopulateMethods();
            Localhostonly = false;
            Mate = mate;
            UriRequested += OnUriRequested;            
        }

        void PopulateMethods()
        {
            var found = this.GetType().GetMethods();
            foreach (var meth in found)
            {
                var attrs = meth.GetCustomAttributes<RestActionAttribute>(true);
                if (attrs.Count() > 0)
                {
                    var methname = meth.Name.ToLower();
                    if (methname.StartsWith("handle")){
                        methname = methname.Substring("handle".Length);
                    }
                    dispatchMethods[methname] = meth;                    
                }
            }
        }

        public void OnUriRequested(object sender, UriRequestEventArgs args)
        {
               if ( args.Request.Url.AbsolutePath.StartsWith("/api/") ){
                   Dispatch(args);
                   if (!args.Handled)
                   {                      
                       SendNoSuchMethodError(args);
                   }
               }
        }

        void SetHandled(UriRequestEventArgs args, int status)
        {
            args.Handled = true;
            args.SetResponseState(status);
            args.SetResponseType("application/json");
        }

        public void SendNoSuchMethodError(UriRequestEventArgs args)
        {
            SendError(args, 404, "No such method");
        }

        public void SendError(UriRequestEventArgs args, int status, string err)
        {
            SetHandled(args, status);            
            var rv = new Dictionary<string, string>() {
                { "error", err }
            };
            args.ResponsStream.Write(JsonConvert.SerializeObject(rv));
        }

        MethodInfo GetMethodFromUri(Uri args)
        {
            MethodInfo tocall = null;
            var words = args.AbsolutePath.Split('/');
            if (words.Length > 2)
            {
                var methname = words[2].ToLower();
                if (dispatchMethods.TryGetValue(methname, out tocall))
                {
                    return tocall;
                }
            }
            throw new MissingMethodException();
        }

        public void Dispatch(UriRequestEventArgs args)
        {
            try
            {
                var tocall = GetMethodFromUri(args.Request.Url);
                tocall.Invoke(this, new object[] { args });
            }
            catch (MissingMethodException)
            {
                SendNoSuchMethodError(args);
            }
            catch (Exception ex)
            {
                SendError(args, 500, String.Format("{0}:{1}", 
                    ex.GetType().Name, ex.Message));
            }
        }

        /// <summary>
        /// Get the current system details.
        /// </summary>
        /// <param name="args"></param>
        [RestAction]        
        public void HandleGetSystem(UriRequestEventArgs args)
        {
            Dictionary<string, string> rv = new Dictionary<string, string>();
            var sysname = Mate.LastSystemName;
            if (!string.IsNullOrEmpty(sysname)){
                rv["systemname"] = sysname;
                var sysurl = Mate.GetEDDBSystemUrl(sysname);
                if (!string.IsNullOrEmpty(sysurl))
                {
                    long sysid = 0;
                    Mate.LookupEDDBSystemID(sysname, out sysid);
                    rv["eddbid"] = sysid.ToString();
                    rv["eddburl"] = sysurl;
                }
            }
            var json = JsonConvert.SerializeObject(rv);            
            SetHandled(args, 200);
            args.ResponsStream.Write(json);
        }

        /// <summary>
        /// Wait until we enter a new system and return the details.
        /// </summary>
        /// <param name="args"></param>
        [RestAction]
        public void HandlePollSystem(UriRequestEventArgs args)
        {
            var sysnow = Mate.LastSystemName;
            do
            {
                Mate.Wait();
            } while (sysnow == Mate.LastSystemName);
            HandleGetSystem(args);
        }
    }
}
