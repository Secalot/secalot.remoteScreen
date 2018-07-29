/*
 * Secalot RemoteScreen.
 * Copyright (c) 2018 Matvey Mukha <matvey.mukha@gmail.com>
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using Rg.Plugins.Popup.Pages;
using Rg.Plugins.Popup.Services;
using System;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace RemoteScreen
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class HelpPopup : PopupPage
    {
        public HelpPopup()
        {
            InitializeComponent();
        }

        async void OnCloseButtonClicked(object sender, EventArgs args)
        {
            await PopupNavigation.PopAsync();
        }

    }
}