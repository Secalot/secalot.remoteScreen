using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace RemoteScreen
{

    [XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class RemoteScreenPage : ContentPage
    {
        string pageText;

        public string PageText
        {
            get { return pageText; }
            set
            {
                if (pageText != value)
                {
                    pageText = value;
                    OnPropertyChanged();
                }

            }
        }


        CommonTasks.ConnectionState connectionState;

        CancellationTokenSource connectionTokenSource;
        CancellationTokenSource commandTokenSource;

        public RemoteScreenPage ()
		{
            BindingContext = this;

            connectionTokenSource = new CancellationTokenSource();
            commandTokenSource = new CancellationTokenSource();

            InitializeComponent ();
        }

        private async Task ConnectToServer(CancellationToken token)
        {
            CommonTasks.ServerInfo serverInfo;

            while (true)
            {
                try
                {
                    PageText = "Finding server...";

                    serverInfo = await CommonTasks.FindServerAsync(TimeSpan.FromDays(1), token);

                    PageText = "Connecting to server...";

                    connectionState = await CommonTasks.ConnectToServerAsync(serverInfo, TimeSpan.FromSeconds(1));

                    if (token.IsCancellationRequested)
                    {
                        token.ThrowIfCancellationRequested();
                    }

                    PageText = "Authenticating...";

                    await CommonTasks.EstablishPSKChannelAsync(connectionState, token);

                    PageText = "Connected";

                    break;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    PageText += " Failed.";
                    await Task.Delay(3000);
                    continue;
                }
            }
        }

        protected async override void OnAppearing()
        {
            connectionTokenSource = new CancellationTokenSource();

            await ConnectToServer(connectionTokenSource.Token);
        }

        protected override void OnDisappearing()
        {
            try
            {
                connectionTokenSource.Cancel();
                commandTokenSource.Cancel();

                CommonTasks.DisconnectFromServer(ref connectionState);
            }
            catch (Exception)
            {
            }
        }

        async void OnGetTransactionButtonClicked(object sender, EventArgs args)
        {

            try
            {
                commandTokenSource = new CancellationTokenSource();

                var tls = await CommonTasks.PerformSSLHadshakeWithToken(connectionState, commandTokenSource.Token);

                await CommonTasks.SelectEthApp(connectionState, commandTokenSource.Token, tls);

                var transactionDetails = await CommonTasks.GetEthTransactionDetails(connectionState, commandTokenSource.Token, tls);

                var transaction = await CommonTasks.ReadEthTransaction(transactionDetails.currentOffset,
                    connectionState, commandTokenSource.Token, tls);

                string parsedTransaction = EthParser.ParseEthereumTransaction(transactionDetails, transaction);

                PageText = parsedTransaction;
            }
            catch(RemoteProtocolException e)
            {
                await DisplayAlert("Error", e.Message, "OK");
            }
            catch(Exception e)
            {
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