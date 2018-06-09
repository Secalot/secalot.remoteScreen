using System;
using System.Collections.Generic;
using System.Text;
using Xamarin.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xamarin.Auth;
using System.Linq;

namespace RemoteScreen
{
    class Settings
    {

        public static bool IsControlPanelBinded()
        {
            if (Application.Current.Properties.ContainsKey("controlPanelBinded") == false)
            {
                return false;
            }
            else
            {
                var controlPanelBinded = (bool)Application.Current.Properties["controlPanelBinded"];

                if(controlPanelBinded == true)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public static void BindControlPanel(string qrCode)
        {
            JObject jsonQR = JObject.Parse(qrCode);

            string guid = (string)jsonQR["guid"];
            string key = (string)jsonQR["key"];

            Application.Current.Properties["guid"] = guid;

            Account account = new Account
            {
                Username = "Secalot RemoteScreen"
            };

            account.Properties.Add("key", key);
            AccountStore.Create().Save(account, "Secalot RemoteScreen");

            Application.Current.Properties["controlPanelBinded"] = true;
        }

        public static void UnbindControlPanel()
        {

            Application.Current.Properties["controlPanelBinded"] = false;
            Application.Current.Properties["guid"] = "";

            var account = AccountStore.Create().FindAccountsForService("Secalot RemoteScreen").FirstOrDefault();
            if (account != null)
            {
                AccountStore.Create().Delete(account, "Secalot RemoteScreen");
            }
        }

        public static void GetGuid(out string guid)
        {
            guid = (string)Application.Current.Properties["guid"];
        }

        public static void GetKey(out string key)
        {
            var account = AccountStore.Create().FindAccountsForService("Secalot RemoteScreen").FirstOrDefault();

            key = account.Properties["key"];
        }

    }
}
