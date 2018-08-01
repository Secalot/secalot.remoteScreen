/*
 * Secalot RemoteScreen.
 * Copyright (c) 2018 Matvey Mukha <matvey.mukha@gmail.com>
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using Android.App;
using Android.OS;
using Android.Content;
using Android.Support.V7.App;
using System.Threading.Tasks;
using Android.Content.PM;

[Activity(Label = "RemoteScreen", Icon = "@drawable/icon", Theme = "@style/Splash", MainLauncher = true, NoHistory = true, ScreenOrientation = ScreenOrientation.Portrait)]
public class SplashActivity : AppCompatActivity
{
    static readonly string TAG = "X:" + typeof(SplashActivity).Name;

    public override void OnCreate(Bundle savedInstanceState, PersistableBundle persistentState)
    {
        base.OnCreate(savedInstanceState, persistentState);
    }

    protected override void OnResume()
    {
        base.OnResume();

        StartActivity(typeof(RemoteScreen.Droid.MainActivity));
    }
}

