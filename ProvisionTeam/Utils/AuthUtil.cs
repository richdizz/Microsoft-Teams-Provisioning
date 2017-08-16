using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ProvisionTeam.Utils
{
    public class AuthUtil
    {
		public static string CLIENT_ID = "f52e6f98-3adf-488c-8ff5-72af1950f0b9";
		public static string CLIENT_SECRET = "26Px0SQW/d9NOM48zZOeeDAezhahBEra1reBg8fxhcc=";
        public static string REDIRECT = "http://localhost:51266";
        public static string AUTHORITY = "https://login.microsoftonline.com/common";

        public AuthUtil()
        {
        }

        public static string GetAuthorizationRedirect() {
            return $"https://login.microsoftonline.com/common/oauth2/authorize?client_id={CLIENT_ID}&resource=https://api.spaces.skype.com&redirect_uri={REDIRECT}&response_type=code";
        }

        public static async Task<AuthResult> GetTokenWithAuthorizationCode(string code) {
            HttpClient client = new HttpClient();
            string payloadString = $"grant_type=authorization_code&redirect_uri={REDIRECT}&client_id={CLIENT_ID}&client_secret={CLIENT_SECRET}&code={code}&resource=https://api.spaces.skype.com";
            StringContent payload = new StringContent(payloadString, Encoding.UTF8, "application/x-www-form-urlencoded");
            using (var resp = await client.PostAsync("https://login.microsoftonline.com/common/oauth2/token", payload)) {
                if (resp.IsSuccessStatusCode) {
                    var json = await resp.Content.ReadAsStringAsync();
                    AuthResult result = JsonConvert.DeserializeObject<AuthResult>(json);
                    return result;
                }
                else
                    return null;
            }
        }

        public static async Task<AuthResult> GetTokenForResourceWithRefreshToken(string resource, string refresh_token) {
			HttpClient client = new HttpClient();
            string payloadString = $"grant_type=refresh_token&client_id={CLIENT_ID}&client_secret={CLIENT_SECRET}&refresh_token={refresh_token}&resource={resource}";
			StringContent payload = new StringContent(payloadString, Encoding.UTF8, "application/x-www-form-urlencoded");
			using (var resp = await client.PostAsync("https://login.microsoftonline.com/common/oauth2/token", payload))
			{
				if (resp.IsSuccessStatusCode)
				{
					var json = await resp.Content.ReadAsStringAsync();
					AuthResult result = JsonConvert.DeserializeObject<AuthResult>(json);
					return result;
				}
				else
					return null;
			}
        }
    }

    public class AuthResult
    {
        public string access_token
        {
            get;
            set;
        }
		public string refresh_token
		{
			get;
			set;
		}
    }
}
