using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using ProvisionTeam.Utils;

namespace ProvisionTeam.Controllers
{
    public class HomeController : Controller
    {
        public async Task<IActionResult> Index()
        {
            // check for cached refresh token
            if (!Request.Cookies.ContainsKey("RefreshToken")) {
                // no cached token...check for authorization code in uri
                if (Request.Query.ContainsKey("code"))
                {
                    // authorization code on request...finish the code authorization flow
                    var token = await AuthUtil.GetTokenWithAuthorizationCode(Request.Query["code"]);

                    if (token == null)
                        return RedirectToAction("Error", "Home", new { msg = "Error completing code authorization flow" });
                    else
                    {
                        // save refresh token and teams access token in cookie
                        Response.Cookies.Append("RefreshToken", token.refresh_token);
                        Response.Cookies.Append("TeamsAccessToken", token.access_token);

                        // get skype token
                        HttpClient client = new HttpClient();
                        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token.access_token);
                        client.DefaultRequestHeaders.Add("Accept", "application/json");
                        var payloadString = "";
                        StringContent payload = new StringContent(payloadString, Encoding.UTF8, "application/json");
                        using (var resp = await client.PostAsync("https://api.teams.skype.com/beta/auth/skypetoken", payload))
                        {
                            if (resp.IsSuccessStatusCode)
                            {
                                // save skype access token in cookie
                                var json = JObject.Parse(await resp.Content.ReadAsStringAsync());
                                Response.Cookies.Append("SkypeAccessToken", json.SelectToken("tokens.skypeToken").Value<string>());
                                return View();
                            }
                            else
                                return RedirectToAction("Error", "Home", new { msg = "Failed to secure skype token" });
                        }
                    }
                }
                else
                    return Redirect(AuthUtil.GetAuthorizationRedirect());
            }
            else
                return View();
        }

        [HttpPost]
        public async Task<ActionResult> Create()
        {
            // get the team name from the form
            var name = Request.Form["txtName"][0].ToString();

            // First get a token for graph to create the O365 Group
            var token = await AuthUtil.GetTokenForResourceWithRefreshToken("https://graph.microsoft.com", Request.Cookies["RefreshToken"].ToString());
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token.access_token);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
			var groupPayloadString = "{\"description\":\"A auto-provisioned team named " + name + "\",\"displayName\":\"" + name + "\",\"groupTypes\":[\"Unified\"],\"mailEnabled\":true,\"mailNickname\":\"" + name + "\",\"securityEnabled\":false}";
			StringContent groupPayload = new StringContent(groupPayloadString, Encoding.UTF8, "application/json");
            using (var resp = await client.PostAsync("https://graph.microsoft.com/beta/groups", groupPayload))
            {
                if (resp.IsSuccessStatusCode)
                {
                    var json = JObject.Parse(await resp.Content.ReadAsStringAsync());
                    var groupid = json.SelectToken("id").Value<string>();

                    // now call into https://api.spaces.skype.com to convert the group to a team
                    client = new HttpClient();
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + Request.Cookies["TeamsAccessToken"].ToString());
                    client.DefaultRequestHeaders.Add("Accept", "application/json");
                    client.DefaultRequestHeaders.Add("X-Skypetoken", Request.Cookies["SkypeAccessToken"].ToString());
                    var migratePayloadString = "{description: \"" + name + "\", displayName: \"" + name + "\", smtpAddress: \"" + name + "@richdizz.com\"}";
                    StringContent migratePayload = new StringContent(migratePayloadString, Encoding.UTF8, "application/json");
                    var uri = $"https://api.teams.skype.com/amer/beta/teams/migrateGroup/{groupid}";
                    using (var resp2 = await client.PutAsync(uri, migratePayload))
                    {
                        if (resp2.IsSuccessStatusCode) {
                            ViewData["Team"] = name;
                            return View();
                        }
                        else {
                            return RedirectToAction("Error", "Home", new { msg = "Failed to migrate group to team" });
                        }
                    }
                }
                else
                    return RedirectToAction("Error", "Home", new { msg = "Failed to create o365 group" });
            }
        }

		public IActionResult Error(string msg)
		{
			ViewData["Message"] = msg;

			return View();
		}
    }
}
