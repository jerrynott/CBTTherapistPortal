using Microsoft.Live;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Routing;
using System.Web.Security;
using System.Web.SessionState;

namespace TherapistPortal
{
    public class Global : System.Web.HttpApplication
    {
        protected void Application_Start(object sender, EventArgs e)
        {
            RouteTable.Routes.Ignore("");
        }

        public static string GetIDFromWindowsLive(HttpRequest request)
        {
            var fromAzure = request.Headers["X-MS-CLIENT-PRINCIPAL-ID"];
            if (!String.IsNullOrWhiteSpace(fromAzure))
            {
                return fromAzure;
            }

            var wl_auth = request.Cookies["wl_auth"];
            if (wl_auth == null)
            {
                throw new LiveConnectException(null, "wl_auth cookie not present.");
            }

            var access_token = wl_auth.Values["access_token"];
            if (String.IsNullOrWhiteSpace(access_token))
            {
                throw new LiveConnectException(null, "access_token not present.");
            }

            var client = new LiveConnectClient((LiveConnectSession)Activator.CreateInstance(typeof(LiveConnectSession), true));
            typeof(LiveConnectSession).GetProperty("AccessToken").SetValue(client.Session, access_token);
            var result = client.GetAsync("me").Result;
            var data = result.Result;
            if (!data.ContainsKey("id") || !data.ContainsKey("name"))
            {
                throw new LiveConnectException(null, "Windows Live returned an error: " + result.RawResult);
            }

            return (string)data["id"];
        }
    }
}
