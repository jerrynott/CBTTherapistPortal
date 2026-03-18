using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
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

            var client = new LiveConnectClient((LiveConnectSession) Activator.CreateInstance(typeof(LiveConnectSession), true));
            typeof(LiveConnectSession).GetProperty("AccessToken").SetValue(client.Session, access_token);
            var result = client.GetAsync("me").Result;
            var data = result.Result;
            if (!data.ContainsKey("id") || !data.ContainsKey("name"))
            {
                throw new LiveConnectException(null, "Windows Live returned an error: " + result.RawResult);
            }

            return (string) data["id"];
        }

        public static string GetIDFromAzure(HttpRequest request)
        {
            var fromAzure = request.Headers["X-MS-CLIENT-PRINCIPAL-ID"];
            if (!String.IsNullOrWhiteSpace(fromAzure))
            {
                return fromAzure;
            }

            var idToken = request.Headers["X-MS-TOKEN-AAD-ID-TOKEN"];
            if (!string.IsNullOrWhiteSpace(idToken))
            {
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(idToken))
                {
                    var jwt = ReadJwtToken(idToken);

                    var oid = jwt.Claims.FirstOrDefault(c => c.Type == "oid")?.Value ?? jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

                    if (!string.IsNullOrWhiteSpace(oid))
                    {
                        return oid;
                    }
                }
            }

            throw new UnauthorizedAccessException("Unable to determine the authenticated user's ID. " +
                "Ensure Azure App Service Authentication (Easy Auth v2) is enabled " +
                "and the request has been authenticated before reaching this code.");
        }
    }
}
