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
    public partial class TransactionPopup : PopupPage
    {
        private string transactionWithDetails;
        private bool cancelled = false;

        public TransactionPopup(string transaction, uint timeout, string transactionWithDetails = null)
        {
            BindingContext = this;

            InitializeComponent();

            transaction = transaction.Replace(' ', '\u00A0');
            transaction = transaction.Replace("\t", "&emsp;");

            TransactionLabel.Text = transaction;



            if (transactionWithDetails != null)
            {
                transactionWithDetails = transactionWithDetails.Replace(' ', '\u00A0');
                transactionWithDetails = transactionWithDetails.Replace("\t", "&nbsp;&nbsp;");
                TransactionDetailsButton.IsVisible = true;
                this.transactionWithDetails = transactionWithDetails;
            }
            else
            {
                TransactionDetailsButton.IsVisible = false;
            }

            if (timeout != 0)
            {
                TimeoutLabel.Text = "<br>Time remaining to confirm: <b>" + (timeout).ToString() + "</b> seconds. <br><br>";

#if __ANDROID__
                Device.StartTimer(TimeSpan.FromSeconds(1), () =>
                {
                    bool retVal;

                    timeout--;

                    if (timeout != 0)
                    {
                        TimeoutLabel.Text = "<br>Time remaining to confirm: <b>" + (timeout).ToString() + "</b> seconds. <br><br>";

                        retVal = !cancelled;
                    }
                    else
                    {
                        TimeoutLabel.Text = "<br><b>Confirmation time expired.</b><br><br>";

                        retVal = false;
                    }

                    return retVal;
                });
#endif
            }
            else
            {
                TimeoutLabel.Text = "<br><b>Confirmation time expired.</b><br><br>";
            }
        }

        async void OnTransactionDetailsButtonClicked(object sender, EventArgs args)
        {
            await PopupNavigation.PopAsync();
            TransactionLabel.Text = transactionWithDetails;
            TransactionDetailsButton.IsVisible = false;
            await PopupNavigation.PushAsync(this);
        }


        async void OnCloseButtonClicked(object sender, EventArgs args)
        {
            cancelled = true;
            await PopupNavigation.PopAsync();
        }

    }
}

