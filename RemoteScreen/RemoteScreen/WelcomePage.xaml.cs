using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace RemoteScreen
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class WelcomePage : ContentPage
    {
        public WelcomePage()
        {
            InitializeComponent();
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

                    await DisplayAlert("Device binded", "Device with fingerprint " + fingerprint + " is successfully binded.", "OK");

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