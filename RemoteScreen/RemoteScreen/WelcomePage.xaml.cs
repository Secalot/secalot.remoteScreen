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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

using Rg.Plugins.Popup.Extensions;
using Rg.Plugins.Popup.Services;

namespace RemoteScreen
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class WelcomePage : ContentPage
    {
        public WelcomePage()
        {
            InitializeComponent();
        }

        async void OnHelpButtonClicked(object sender, EventArgs args)
        {
            await PopupNavigation.PushAsync(new HelpPopup());
        }

        async void OnBindButtonClicked(object sender, EventArgs args)
        {
            try
            {
                string qrCode = await CommonTasks.GetQRCodeAsync();

                if (qrCode == null)
                {
                    return;
                }
                else
                {
                    string publicKey;

                    Settings.BindControlPanel(qrCode);

                    Settings.GetPublicKey(out publicKey);

                    string fingerprint = Settings.PublicKeyToFingerprint(publicKey);

                    await DisplayAlert("Device bound", "Device with fingerprint " + fingerprint + " is successfully bound.", "OK");

                    Navigation.InsertPageBefore(new RemoteScreenPage(), this);
                    await Navigation.PopAsync(true);
                }
            }
            catch (Exception e)
            {
                await DisplayAlert("Error", "Binding failed", "OK");
            }
        }

    }
}