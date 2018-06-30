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

        CancellationTokenSource findServerTokenSource;
        CancellationTokenSource getTransactionTokenSource;

        CommonTasks.ServerInfo serverInfo;
        

        public RemoteScreenPage ()
		{
            BindingContext = this;

            ServerStatus = "Searching for Secalot Control Panel";
            ServerColor = "lightYellow";
            TransactionButtonEnabled = false;

            findServerTokenSource = new CancellationTokenSource();
            getTransactionTokenSource = new CancellationTokenSource();

            InitializeComponent ();
        }

        private async Task FindServer(CancellationToken token)
        {
            CommonTasks.ServerInfo serverInfo;

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
                        ServerStatus = "Searching for Secalot Control Panel";
                        ServerColor = "lightYellow";
                        TransactionButtonEnabled = false;

                        await Task.Delay(5000, token);

                        continue;
                    }

                    ServerStatus = "Secalot Control Panel found";
                    ServerColor = "lightGreen";
                    TransactionButtonEnabled = true;
                    this.serverInfo = serverInfo;

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
            try
            {
                CancellationTokenSource timeoutCTS = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCTS.Token, getTransactionTokenSource.Token))
                {
                    CommonTasks.ConnectionState connectionState = new CommonTasks.ConnectionState();

                    using (connectionState.client = new TcpClient())
                    {
                        await CommonTasks.ConnectToServerAsync(serverInfo, connectionState, TimeSpan.FromSeconds(1));

                        await CommonTasks.EstablishPSKChannelAsync(connectionState, linkedCts.Token);

                        var tls = await CommonTasks.PerformSSLHadshakeWithToken(connectionState, linkedCts.Token);

                        try
                        {
                            await CommonTasks.SelectBtcApp(connectionState, linkedCts.Token, tls);

                            var transactionDetails = await CommonTasks.GetBtcTransactionDetails(connectionState, linkedCts.Token, tls);
                            var inputAmounts = await CommonTasks.GetBtcInputAmounts(transactionDetails.numberOfInputs, connectionState, linkedCts.Token, tls);
                            var transaction = await CommonTasks.ReadBtcTransaction(transactionDetails.currentOffset, connectionState, linkedCts.Token, tls);

                            var parsedTransaction = BtcParser.ParseBitcoinTransaction(transactionDetails, transaction, inputAmounts);

                            await PopupNavigation.PushAsync(new TransactionPopup(parsedTransaction));
                        }
                        catch (TransactionNotActiveException)
                        {
                            try
                            {
                                await CommonTasks.SelectEthApp(connectionState, linkedCts.Token, tls);

                                var transactionDetails = await CommonTasks.GetEthTransactionDetails(connectionState, linkedCts.Token, tls);

                                var transaction = await CommonTasks.ReadEthTransaction(transactionDetails.currentOffset, connectionState, linkedCts.Token, tls);

                                string parsedTransaction = EthParser.ParseEthereumTransaction(transactionDetails, transaction);

                                await PopupNavigation.PushAsync(new TransactionPopup(parsedTransaction));
                            }
                            catch (TransactionNotActiveException)
                            {
                                throw new RemoteProtocolException("There are no transactions active");
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
            catch (Exception e)
            {
                string message = "An error has occured";
                while (e.InnerException != null)
                {
                    if(e.InnerException.GetType() == typeof(InvalidServerCertificateException) )
                    {
                        message = "This app is binded with a different Secalot device.";
                        break;
                    }

                    e = e.InnerException;
                }

                await DisplayAlert("Error", message, "OK");
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