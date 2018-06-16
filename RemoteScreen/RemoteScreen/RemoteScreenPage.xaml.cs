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

                await CommonTasks.SelectBtcApp(connectionState, commandTokenSource.Token, tls);

                var transactionDetails = await CommonTasks.GetBtcTransactionDetails(connectionState, commandTokenSource.Token, tls);
                var inputAmounts = await CommonTasks.GetBtcInputAmounts(transactionDetails.numberOfInputs, connectionState, commandTokenSource.Token, tls);
                var transaction = await CommonTasks.ReadBtcTransaction(transactionDetails.currentOffset, connectionState, commandTokenSource.Token, tls);

                string parsedTransaction = BtcParser.ParseBitcoinTransaction(transactionDetails, transaction, inputAmounts);

                PageText = parsedTransaction;

                //var transaction = await CommonTasks.ReadEthTransaction(transactionDetails.currentOffset,
                //    connectionState, commandTokenSource.Token, tls);



                //await CommonTasks.SelectEthApp(connectionState, commandTokenSource.Token, tls);

                //var transactionDetails = await CommonTasks.GetEthTransactionDetails(connectionState, commandTokenSource.Token, tls);

                //var transaction = await CommonTasks.ReadEthTransaction(transactionDetails.currentOffset,
                //    connectionState, commandTokenSource.Token, tls);

                //string parsedTransaction = EthParser.ParseEthereumTransaction(transactionDetails, transaction);

                //PageText = parsedTransaction;


                //Transaction tx = new Transaction("01000000010b44f2a1ed462807bded644b03cc08599dd2b30cb5a82cdba6a19156789ce753010000005701ff4c53ff0488b21e03d28401a280000000327d7c137044fa03b87e7090b9be480401ecba9d42027afce68f450c4605195e026dcfd985eb1ac333cd5da4ccdd190a22c8db417301e7ffef1d698a865a1625fe01000300feffffff02a0860100000000001976a914088ad75578a12715fb5dd7034d493831950d64c888ac84d03403000000001976a914274bfbf0e6e9071dd00bb515ef07139d3e62c52988acb3381400");

                //Debug.WriteLine(tx);

                //QBitNinjaClient client = new QBitNinjaClient(Network.TestNet);

                //for(int i=0; i<tx.Outputs.Count; i++)
                //{
                //    var script = tx.Outputs[i].ScriptPubKey;
                //     var address = script.GetDestinationAddress(Network.TestNet);
                //}

                //for(int i=0; i<tx.Inputs.Count; i++)
                //{
                //    QBitNinja.Client.Models.GetTransactionResponse transactionResponse = client.GetTransaction(tx.Inputs[i].PrevOut.Hash).Result;

                //    var script = transactionResponse.Transaction.Outputs[tx.Inputs[i].PrevOut.N].ScriptPubKey;
                //    var address = script.GetDestinationAddress(Network.TestNet);


                //    Debug.WriteLine(transactionResponse);
                //}


            }
            catch (RemoteProtocolException e)
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