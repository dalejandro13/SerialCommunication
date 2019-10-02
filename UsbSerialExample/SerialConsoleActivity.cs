//
// Copyright 2014 LusoVU. All rights reserved.
// 
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 3 of the License, or (at your option) any later version.
// 
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301,
// USA.
// 
// Project home page: https://bitbucket.com/lusovu/xamarinusbserial
// 
// PASSWORD XKFG55QR 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Hardware.Usb;
using Android.Net;
using Android.Net.Wifi;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Hoho.Android.UsbSerial.Driver;
using Hoho.Android.UsbSerial.Util;
using Plugin.Connectivity;
using static Android.Net.Wifi.WifiManager;
using static Android.OS.PowerManager;

namespace Hoho.Android.UsbSerial.Examples
{
	[Activity (Label = "@string/app_name", LaunchMode = LaunchMode.SingleTop)]			
	class SerialConsoleActivity : Activity
	{
		static readonly string TAG = typeof(SerialConsoleActivity).Name;

		public const string EXTRA_TAG = "PortInfo";

		IUsbSerialPort port;
        
        public Intent intent, intento; //new line
        bool activateSleep; //new line
        UsbManager usbManager;
		TextView titleTextView;
		TextView dumpTextView;
        ScrollView scrollView;
        //EditText texto;
        PowerManager pm;
        WakeLock wake;
        WifiManager wifi;
        WifiLock wifilock;

        SerialInputOutputManager serialIoManager;

		protected override void OnCreate(Bundle bundle)
		{
			Log.Info (TAG, "OnCreate");

			base.OnCreate (bundle);

			SetContentView (Resource.Layout.serial_console);

			usbManager = GetSystemService(Context.UsbService) as UsbManager;
			titleTextView = FindViewById<TextView>(Resource.Id.demoTitle);
			dumpTextView = FindViewById<TextView>(Resource.Id.consoleText);
            scrollView = FindViewById<ScrollView>(Resource.Id.demoScroller);
            wifi = (WifiManager)GetSystemService(WifiService);
            wifilock = wifi.CreateWifiLock(WifiMode.FullHighPerf, "wifi keep On");
            pm = (PowerManager)GetSystemService(Context.PowerService); //new line
            wake = pm.NewWakeLock(WakeLockFlags.Partial, "stay awake gently"); //new line
            activateSleep = true; //new line
        }

		protected override void OnPause ()
		{
			Log.Info (TAG, "OnPause");

			base.OnPause ();

			if (serialIoManager != null && serialIoManager.IsOpen) {
				Log.Info (TAG, "Stopping IO manager ..");
				try {
					serialIoManager.Close ();
				}
				catch (Java.IO.IOException) {
					// ignore
				}
			}
		}

		protected async override void OnResume ()
		{
			Log.Info (TAG, "OnResume");

			base.OnResume ();

            var portInfo = Intent.GetParcelableExtra(EXTRA_TAG) as UsbSerialPortInfo;
			int vendorId = portInfo.VendorId;
			int deviceId = portInfo.DeviceId;
			int portNumber = portInfo.PortNumber;

			Log.Info (TAG, string.Format("VendorId: {0} DeviceId: {1} PortNumber: {2}", vendorId, deviceId, portNumber));

			var drivers = await DeviceListActivity.FindAllDriversAsync (usbManager);
			var driver = drivers.Where((d) => d.Device.VendorId == vendorId && d.Device.DeviceId == deviceId).FirstOrDefault();
			if(driver == null)
				throw new Exception ("Driver specified in extra tag not found.");

			port = driver.Ports[portNumber];
			if (port == null) {
				titleTextView.Text = "No serial device.";
				return;
			}
            Log.Info(TAG, "port=" + port);

            titleTextView.Text = "Serial device: " + port.GetType().Name;

            serialIoManager = new SerialInputOutputManager (port) {
				BaudRate = 115200,
				DataBits = 8,
				StopBits = StopBits.One,
				Parity = Parity.None,
			};

            serialIoManager.DataReceived += (sender, e) =>
            {
                RunOnUiThread(() =>
                {
                    UpdateReceivedData(e.Data);
                });
            };

			serialIoManager.ErrorReceived += (sender, e) => {
				RunOnUiThread (() => {
					var intent = new Intent(this, typeof(DeviceListActivity));
					StartActivity(intent);
				});
			};

			Log.Info (TAG, "Starting IO manager ..");
			try {
				serialIoManager.Open (usbManager);
			}
			catch (Java.IO.IOException e) {
				titleTextView.Text = "Error opening device: " + e.Message;
				return;
			}
		}

		void UpdateReceivedData(byte[] data)
		{
            try
            {
                ConnectivityManager ConnectivityManager = (ConnectivityManager)Application.Context.GetSystemService(Context.ConnectivityService);
                NetworkInfo connection = ConnectivityManager.ActiveNetworkInfo; 
                if (connection != null && connection.IsConnected && CrossConnectivity.Current.IsConnected)
                {
                    var message = HexDump.DumpHexString(data) + "\n\n";
                    dumpTextView.Append(message);
                    scrollView.SmoothScrollTo(0, dumpTextView.Bottom);
                }
                else
                {
                    if (wake.IsHeld && wifilock.IsHeld)
                    {
                        wake.Release(); //libera wakeLock
                        wifilock.Release(); //libera wifiLock
                    }
                    activateSleep = false;
                    PowerManager pwm = (PowerManager)GetSystemService(Context.PowerService);
                    WakeLock wkl = pwm.NewWakeLock(WakeLockFlags.Full | WakeLockFlags.AcquireCausesWakeup | WakeLockFlags.OnAfterRelease, "wakeup device");
                    wkl.Acquire();
                    wkl.Release();

                    Finish();
                    intent = PackageManager.GetLaunchIntentForPackage("com.flexolumens.MonitorInteligente");
                    StartActivity(intent);
                    //OnDestroy();
                }

                if (activateSleep == true && (connection != null && connection.IsConnected) && CrossConnectivity.Current.IsConnected)
                {
                    wake.Acquire();
                    wifilock.Acquire();
                    activateSleep = false;
                    intento = PackageManager.GetLaunchIntentForPackage("com.ssaurel.lockdevice");
                    StartActivity(intento);
                    //if (!wifi.IsWifiEnabled)
                    //{
                    //wifi.SetWifiEnabled(true);
                    //}
                }
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, ex.Message, ToastLength.Long).Show();
            }
		}
	}
}

