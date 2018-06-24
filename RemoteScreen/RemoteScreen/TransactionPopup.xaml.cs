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
        string transactionString;

        public string TransactionString
        {
            get { return transactionString; }
            set
            {
                if (transactionString != value)
                {
                    transactionString = value;
                    OnPropertyChanged();
                }
            }
        }

        public TransactionPopup(string transaction)
        {
            BindingContext = this;

            InitializeComponent();

            TransactionString = transaction;
        }

        async void OnCloseButtonClicked(object sender, EventArgs args)
        {
            await PopupNavigation.PopAsync();
        }

    }
}