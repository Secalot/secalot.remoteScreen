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