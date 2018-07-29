/*
 * Secalot RemoteScreen.
 * Copyright (c) 2018 Matvey Mukha <matvey.mukha@gmail.com>
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

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

        public static bool IsControlPanelBound()
        {
            if (Application.Current.Properties.ContainsKey("controlPanelBound") == false)
            {
                return false;
            }
            else
            {
                var controlPanelBound = (bool)Application.Current.Properties["controlPanelBound"];

                if(controlPanelBound == true)
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

            Application.Current.Properties["controlPanelBound"] = true;
        }

        public static void UnbindControlPanel()
        {

            Application.Current.Properties["controlPanelBound"] = false;
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
