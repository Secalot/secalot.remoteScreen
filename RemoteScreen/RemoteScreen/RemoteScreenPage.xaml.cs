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
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

using System.Diagnostics;
using System.Net.Sockets;
using Rg.Plugins.Popup.Services;

namespace RemoteScreen
{

    [XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class RemoteScreenPage : ContentPage
    {
        string serverStatus;

        public string ServerStatus
        {
            get { return serverStatus; }
            set
            {
                if (serverStatus != value)
                {
                    serverStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        string serverColor;

        public string ServerColor
        {
            get { return serverColor; }
            set
            {
                if (serverColor != value)
                {
                    serverColor = value;
                    OnPropertyChanged();
                }
            }
        }

        bool transactionButtonEnabled;

        public bool TransactionButtonEnabled
        {
            get { return transactionButtonEnabled; }
            set
            {
                if (transactionButtonEnabled != value)
                {
                    transactionButtonEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        bool mainLayoutEnabled;

        public bool MainLayoutEnabled
        {
            get { return mainLayoutEnabled; }
            set
            {
                if (mainLayoutEnabled != value)
                {
                    mainLayoutEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        CancellationTokenSource findServerTokenSource;
        CancellationTokenSource getTransactionTokenSource;

        CommonTasks.ServerInfo serverInfo;
        

        public RemoteScreenPage ()
		{
            BindingContext = this;

            ServerStatus = "Searching for Secalot Control Panel";
            ServerColor = "lightYellow";
            TransactionButtonEnabled = false;
            MainLayoutEnabled = true;

            findServerTokenSource = new CancellationTokenSource();
            getTransactionTokenSource = new CancellationTokenSource();

            InitializeComponent ();
        }

        private async Task FindServer(CancellationToken token)
        {
            CommonTasks.ServerInfo serverInfo;
            int failedAttempts = 0;

            try
            {
                while (true)
                {
                    try
                    {
                        serverInfo = await CommonTasks.FindServerAsync(TimeSpan.FromSeconds(1), token);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        failedAttempts++;

                        if(failedAttempts == 5)
                        {
                            ServerStatus = "Searching for Secalot Control Panel";
                            ServerColor = "lightYellow";
                            TransactionButtonEnabled = false;
                            failedAttempts = 0;

                            await Task.Delay(5000, token);
                        }

                        continue;
                    }

                    ServerStatus = "Secalot Control Panel found";
                    ServerColor = "lightGreen";
                    TransactionButtonEnabled = true;
                    this.serverInfo = serverInfo;
                    failedAttempts = 0;

                    await Task.Delay(5000, token);
                }
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }

        protected override void OnAppearing()
        {
            try
            {
                findServerTokenSource = new CancellationTokenSource();
                getTransactionTokenSource = new CancellationTokenSource();

                Task task = Task.Factory.StartNew(() =>
                {
                    FindServer(findServerTokenSource.Token);
                });
            }
            catch (Exception)
            {
            }
        }

        protected override void OnDisappearing()
        {
            try
            {
                findServerTokenSource.Cancel();
                getTransactionTokenSource.Cancel();
            }
            catch (Exception)
            {
            }
        }

        async void OnGetTransactionButtonClicked(object sender, EventArgs args)
        {
            uint timeout = 0;

            MainLayoutEnabled = false;

            try
            {
                CancellationTokenSource timeoutCTS = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCTS.Token, getTransactionTokenSource.Token))
                {
                    CommonTasks.ConnectionState connectionState = new CommonTasks.ConnectionState();

                    using (connectionState.client = new TcpClient())
                    {
                        await CommonTasks.ConnectToServerAsync(serverInfo, connectionState, TimeSpan.FromSeconds(1));

                        await CommonTasks.EstablishSRPChannelAsync(connectionState, linkedCts.Token);

                        var tls = await CommonTasks.PerformSSLHadshakeWithToken(connectionState, linkedCts.Token);

                        try
                        {
                            await CommonTasks.SelectBtcApp(connectionState, linkedCts.Token, tls);

                            var transactionDetails = await CommonTasks.GetBtcTransactionDetails(connectionState, linkedCts.Token, tls);
                            var inputAmounts = await CommonTasks.GetBtcInputAmounts(transactionDetails.numberOfInputs, connectionState, linkedCts.Token, tls);
                            var transaction = await CommonTasks.ReadBtcTransaction(transactionDetails.currentOffset, connectionState, linkedCts.Token, tls);

                            var parsedTransaction = BtcParser.ParseBitcoinTransaction(transactionDetails, transaction, inputAmounts, ref timeout);

                            await PopupNavigation.PushAsync(new TransactionPopup(parsedTransaction, timeout));
                        }
                        catch (TransactionNotActiveException)
                        {
                            try
                            {
                                await CommonTasks.SelectEthApp(connectionState, linkedCts.Token, tls);

                                var transactionDetails = await CommonTasks.GetEthTransactionDetails(connectionState, linkedCts.Token, tls);

                                var transaction = await CommonTasks.ReadEthTransaction(transactionDetails.currentOffset, connectionState, linkedCts.Token, tls);

                                string parsedTransaction = EthParser.ParseEthereumTransaction(transactionDetails, transaction, ref timeout);

                                await PopupNavigation.PushAsync(new TransactionPopup(parsedTransaction, timeout));
                            }
                            catch (TransactionNotActiveException)
                            {
                                try
                                {
                                    await CommonTasks.SelectXrpApp(connectionState, linkedCts.Token, tls);

                                    var transactionDetails = await CommonTasks.GetXrpTransactionDetails(connectionState, linkedCts.Token, tls);

                                    var transaction = await CommonTasks.ReadXrpTransaction(transactionDetails.currentOffset, connectionState, linkedCts.Token, tls);

                                    List<string> parsedTransaction = XrpParser.ParseRippleTransaction(transactionDetails, transaction, ref timeout);

                                    await PopupNavigation.PushAsync(new TransactionPopup(parsedTransaction[0], timeout, parsedTransaction[1]));
                                }
                                catch (TransactionNotActiveException)
                                {
                                    throw new RemoteProtocolException("There are no transactions active");
                                }
                                catch (AppNotPresentException)
                                {
                                    throw new RemoteProtocolException("There are no transactions active");
                                }
                            }
                        }
                    }
                }
            }
            catch (RemoteProtocolException e)
            {
                await DisplayAlert("Error", e.Message, "OK");
            }
            catch (ServerConnectionFailedException e)
            {
                await DisplayAlert("Error", "Failed to connect.", "OK");
            }
            catch (EthParserException e)
            {
                await DisplayAlert("Error", e.Message, "OK");
            }
            catch (BtcParserException e)
            {
                await DisplayAlert("Error", e.Message, "OK");
            }
            catch (XrpParserException e)
            {
                await DisplayAlert("Error", e.Message, "OK");
            }
            catch (Exception e)
            {
                string message = "An error has occurred";
                while (e.InnerException != null)
                {
                    if(e.InnerException.GetType() == typeof(InvalidServerCertificateException) )
                    {
                        message = "This app is bound with a different Secalot device.";
                        break;
                    }

                    e = e.InnerException;
                }

                await DisplayAlert("Error", message, "OK");
            }
            finally
            {
                MainLayoutEnabled = true;
            }
        }

        async void OnUnbindMenuItemClicked(object sender, EventArgs args)
        {
            try
            {
                Settings.UnbindControlPanel();

                Navigation.InsertPageBefore(new WelcomePage(), this);
                await Navigation.PopAsync(true);
            }
            catch (Exception e)
            {
                await DisplayAlert("Error", "Unbinding failed", "OK");
            }

        }

    }
}