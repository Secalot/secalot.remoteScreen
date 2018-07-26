using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Xamarin.Forms;

namespace RemoteScreen
{
	public partial class App : Application
	{
		public App ()
		{
			InitializeComponent();

            bool isBound = Settings.IsControlPanelBound();

            if(isBound == true)
            {
                MainPage = new NavigationPage(new RemoteScreenPage());
            }
            else
            {
                MainPage = new NavigationPage(new WelcomePage());
            }
        }

		protected override void OnStart ()
		{
			// Handle when your app starts
		}

		protected override void OnSleep ()
		{
			// Handle when your app sleeps
		}

		protected override void OnResume ()
		{
			// Handle when your app resumes
		}
	}
}
