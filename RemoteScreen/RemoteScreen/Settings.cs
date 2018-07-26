using System;
using System.Collections.Generic;
using System.Text;
using Xamarin.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xamarin.Auth;
using System.Linq;
using System.Globalization;
using Org.BouncyCastle.Crypto.Digests;

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
            string srpKey = (string)jsonQR["srpKey"];
            string publicKey = (string)jsonQR["publicKey"];

            Application.Current.Properties["guid"] = guid;
            Application.Current.Properties["publicKey"] = publicKey;

            Account account = new Account
            {
                Username = "Secalot RemoteScreen"
            };

            account.Properties.Add("srpKey", srpKey);
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

        public static void GetPublicKey(out string publicKey)
        {
            publicKey = (string)Application.Current.Properties["publicKey"];
        }

        public static void GetSrpKey(out string key)
        {
            var account = AccountStore.Create().FindAccountsForService("Secalot RemoteScreen").FirstOrDefault();

            key = account.Properties["srpKey"];
        }

        public static string PublicKeyToFingerprint(string key)
        {

            byte[] publicKey = key.ConvertHexStringToByteArray();

            Sha256Digest hash = new Sha256Digest();
            hash.BlockUpdate(publicKey, 0, publicKey.Length);
            byte[] digest = new byte[hash.GetDigestSize()];
            hash.DoFinal(digest, 0);

            string fingerprint = BitConverter.ToString(digest, 0, 8).Replace("-", "").ToLower();

            return fingerprint;
        }

    }
}
