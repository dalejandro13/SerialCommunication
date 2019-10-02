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

using System;

using System.Collections.Generic;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Hoho.Android.UsbSerial.Driver;
using Hoho.Android.UsbSerial.Util;
using System.Globalization;
using System.Threading;
using System.IO;
using Environment = Android.OS.Environment;
using Android.Content.PM;
using Java.Lang;
using Java.Lang.Reflect;

[assembly: UsesFeature ("android.hardware.usb.host")]

namespace Hoho.Android.UsbSerial.Examples
{
	[Activity (Label = "@string/app_name", MainLauncher = true, Icon = "@drawable/icon")]
	[IntentFilter (new[] { UsbManager.ActionUsbDeviceAttached })]
	[MetaData (UsbManager.ActionUsbDeviceAttached, Resource = "@xml/device_filter")]
	class DeviceListActivity : Activity
	{
		static readonly string TAG = typeof(DeviceListActivity).Name;
		const string ACTION_USB_PERMISSION = "com.hoho.android.usbserial.examples.USB_PERMISSION";
        string archivo = Environment.GetExternalStoragePublicDirectory(Environment.DirectoryDownloads).ToString() + "/archivos/historial.txt";
        UsbManager usbManager;
		ListView listView;
		TextView progressBarTitle;
		ProgressBar progressBar;
        UsbSerialPortAdapter adapter;
        UsbDevice device;
        BroadcastReceiver detachedReceiver;
		IUsbSerialPort selectedPort;
        bool firstTime;
        bool access;
        

        protected override async void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			SetContentView (Resource.Layout.Main);

			usbManager = GetSystemService(Context.UsbService) as UsbManager;
			listView = FindViewById<ListView>(Resource.Id.deviceList);
			progressBar = FindViewById<ProgressBar>(Resource.Id.progressBar);
			progressBarTitle = FindViewById<TextView>(Resource.Id.progressBarTitle); 
            ActionBar.Hide();
            retrieveset();
            //access = grantAutomaticPermission(device);
            //if (access)
            //{
            //    Toast.MakeText(this, "funciona", ToastLength.Long).Show();
            //}
            //else
            //{
            //    Toast.MakeText(this, "no funciona", ToastLength.Long).Show();
            //}
            //await OpenActivity();
        }

        public bool grantAutomaticPermission(UsbDevice usbDevice)
        {
            try
            {
                Context context = Application.Context;
                PackageManager pkgManager = context.PackageManager;
                ApplicationInfo appInfo = pkgManager.GetApplicationInfo(context.PackageName, PackageInfoFlags.MetaData);

                Class serviceManagerClass = Class.ForName("Android.OS.ServiceManager");
                Method getServiceMethod = serviceManagerClass.GetDeclaredMethod("getService", serviceManagerClass);
                getServiceMethod.Accessible = true;
                IBinder binder = (IBinder)getServiceMethod.Invoke(null, Context.UsbService);

                Class iUsbManagerClass = Class.ForName("android.hardware.usb.IUsbManager");
                Class stubClass = Class.ForName("android.hardware.usb.IUsbManager$Stub");
                Method asInterfaceMethod = stubClass.GetDeclaredMethod("asInterface", iUsbManagerClass);
                asInterfaceMethod.Accessible = true;
                object iUsbManager = asInterfaceMethod.Invoke(null, (Java.Lang.Object)binder);

                //System.out.println("UID : " + appInfo.uid + " " + appInfo.processName + " " + appInfo.permission);
                Method grantDevicePermissionMethod = iUsbManagerClass.GetDeclaredMethod("grantDevicePermission", (Java.Lang.Class)UsbDevice.GetDeviceName(0));
                grantDevicePermissionMethod.Accessible = true;
                grantDevicePermissionMethod.Invoke((Java.Lang.Object)iUsbManager, usbDevice, appInfo.Uid);

                //System.out.println("Method OK : " + binder + "  " + iUsbManager);
                return true;
            }
            catch (System.Exception ex)
            {
                Toast.MakeText(Application.Context, ex.Message, ToastLength.Long).Show();
                using (StreamWriter file = new StreamWriter(archivo))
                {
                    file.Write(ex.Message);
                }
                //System.err.println("Error trying to assing automatic usb permission : ");
                //ex.printStackTrace();
                return false;
            }
        }

        /* public boolean grantAutomaticPermission(UsbDevice usbDevice)
           {
           try
           {
               Context context=YourActivityOrApplication;
               PackageManager pkgManager=context.getPackageManager();
               ApplicationInfo appInfo=pkgManager.getApplicationInfo(context.getPackageName(), PackageManager.GET_META_DATA);

               Class serviceManagerClass=Class.forName("android.os.ServiceManager");
               Method getServiceMethod=serviceManagerClass.getDeclaredMethod("getService",String.class);
               getServiceMethod.setAccessible(true);
               android.os.IBinder binder=(android.os.IBinder)getServiceMethod.invoke(null, Context.USB_SERVICE);

               Class iUsbManagerClass=Class.forName("android.hardware.usb.IUsbManager");
               Class stubClass=Class.forName("android.hardware.usb.IUsbManager$Stub");
               Method asInterfaceMethod=stubClass.getDeclaredMethod("asInterface", android.os.IBinder.class);
               asInterfaceMethod.setAccessible(true);
               Object iUsbManager=asInterfaceMethod.invoke(null, binder);


               System.out.println("UID : " + appInfo.uid + " " + appInfo.processName + " " + appInfo.permission);
               final Method grantDevicePermissionMethod = iUsbManagerClass.getDeclaredMethod("grantDevicePermission", UsbDevice.class,int.class);
               grantDevicePermissionMethod.setAccessible(true);
               grantDevicePermissionMethod.invoke(iUsbManager, usbDevice,appInfo.uid);


               System.out.println("Method OK : " + binder + "  " + iUsbManager);
               return true;
           }
           catch(Exception e)
           {
               System.err.println("Error trying to assing automatic usb permission : ");
               e.printStackTrace();
               return false;
           }
           }*/

        protected override async void OnResume()
		{
			base.OnResume ();
            firstTime = true;
            adapter = new UsbSerialPortAdapter(this);
            listView.Adapter = adapter;

            listView.ItemClick += async (sender, e) =>
            {
                await OnItemClick(sender, e);
            };

            await PopulateListAsync();
            await OpenActivity(); //nuevo
            //register the broadcast receivers
            detachedReceiver = new UsbDeviceDetachedReceiver(this);
            RegisterReceiver(detachedReceiver, new IntentFilter(UsbManager.ActionUsbDeviceDetached));
        }

		protected override void OnPause ()
		{
			base.OnPause ();
            // unregister the broadcast receivers
            var temp = detachedReceiver; // copy reference for thread safety
			if(temp != null)
				UnregisterReceiver (temp);
		}

		protected override void OnDestroy ()
		{
            
			base.OnDestroy ();
            saveset();
        }

        async Task OpenActivity()
        {
            ////////////////////////////////////new line///////////////////////////////////////
            try
            {
                if (firstTime == true)
                {
                    await Task.Delay(3000);
                    if (adapter.Count != 0)
                    {
                        selectedPort = adapter.GetItem(0);
                        //var permissionGranted = true/*await usbManager.RequestPermissionAsync(selectedPort.Driver.Device, this)*/;
                        //if (!File.Exists(System.IO.Path.Combine((string)Environment.GetExternalStoragePublicDirectory(Environment.DirectoryDownloads), "granted.flx")))
                        //{
                        var permissionGranted = await usbManager.RequestPermissionAsync(selectedPort.Driver.Device, this);
                            //if (permissionGranted)
                            //{
                            //    Toast.MakeText(this, System.IO.Path.Combine((string)Environment.GetExternalStoragePublicDirectory(Environment.DirectoryDownloads), "granted.flx"), ToastLength.Long).Show();
                            //    File.Create(System.IO.Path.Combine((string)Environment.GetExternalStoragePublicDirectory(Environment.DirectoryDownloads), "granted.flx"));
                            //}
                            
                        //}
                        if (permissionGranted)
                        {
                            // start the SerialConsoleActivity for this device
                            var newIntent = new Intent(this, typeof(SerialConsoleActivity));
                            newIntent.SetFlags(newIntent.Flags | ActivityFlags.NoHistory);
                            newIntent.PutExtra(SerialConsoleActivity.EXTRA_TAG, new UsbSerialPortInfo(selectedPort));
                            StartActivity(newIntent);
                        }
                    }
                    firstTime = false;
                }
            }
            catch (System.Exception ex)
            {
                //Toast.MakeText(this, ex.Message, ToastLength.Long).Show();
                //OpenActivity();
            }
            //////////////////////////////////////////////////////////////////////////////////
        }

        internal static Task<IList<IUsbSerialDriver>> FindAllDriversAsync(UsbManager usbManager)
		{
			// using the default probe table
			// return UsbSerialProber.DefaultProber.FindAllDriversAsync (usbManager);

			// adding a custom driver to the default probe table
			var table = UsbSerialProber.DefaultProbeTable;
			table.AddProduct(0x1b4f, 0x0008, Java.Lang.Class.FromType(typeof(CdcAcmSerialDriver))); // IOIO OTG
			var prober = new UsbSerialProber (table);
			return prober.FindAllDriversAsync (usbManager);
		}

		async Task OnItemClick(object sender, AdapterView.ItemClickEventArgs e)
		{
			Log.Info(TAG, "Pressed item " + e.Position);
			if (e.Position >= adapter.Count) {
				Log.Info(TAG, "Illegal position.");
				return;
			}
            // request user permisssion to connect to device
            // NOTE: no request is shown to user if permission already granted
            await OpenActivity(); //nuevo
            
        }

		async Task PopulateListAsync ()
		{
			ShowProgressBar ();
            Log.Info (TAG, "Refreshing device list ...");

			var drivers = await FindAllDriversAsync (usbManager);

			adapter.Clear ();
			foreach (var driver in drivers) {
				var ports = driver.Ports;
				Log.Info (TAG, string.Format ("+ {0}: {1} port{2}", driver, ports.Count, ports.Count == 1 ? string.Empty : "s"));
				foreach(var port in ports)
					adapter.Add (port);
			}

			adapter.NotifyDataSetChanged();
			progressBarTitle.Text = string.Format("{0} device{1} found", adapter.Count, adapter.Count == 1 ? string.Empty : "s");
			HideProgressBar();
			Log.Info(TAG, "Done refreshing, " + adapter.Count + " entries found.");
            await OpenActivity(); //nuevo
        }

        protected void saveset()
        {
            string accessories = string.Empty;
            for (int i = 0; i < usbManager.GetAccessoryList().Length; i++)
            {
                accessories += usbManager.GetAccessoryList()[i];
                accessories += System.Environment.NewLine;
            }
            //store
            var prefs = Application.Context.GetSharedPreferences("MyApp", FileCreationMode.Private);
            var prefEditor = prefs.Edit();
            prefEditor.PutString("PrefName", accessories);
            prefEditor.Commit();

        }

        // Function called from OnCreate
        protected void retrieveset()
        {
            //retreive 
            var prefs = Application.Context.GetSharedPreferences("MyApp", FileCreationMode.Private);
            var somePref = prefs.GetString("PrefName", null);

            //Show a toast
            //RunOnUiThread(() => Toast.MakeText(this, $"{somePref}", ToastLength.Long).Show());

        }

        void ShowProgressBar()
		{
			progressBar.Visibility = ViewStates.Visible;
			progressBarTitle.Text = GetString(Resource.String.refreshing);
		}

		void HideProgressBar()
		{
			progressBar.Visibility = ViewStates.Invisible;
		}

		#region UsbSerialPortAdapter implementation

		class UsbSerialPortAdapter : ArrayAdapter<IUsbSerialPort>
		{
            public UsbSerialPortAdapter(Context context) : base(context, global::Android.Resource.Layout.SimpleExpandableListItem2)
			{
			}

			public override View GetView(int position, View convertView, ViewGroup parent)
			{
				var row = convertView;
				if (row == null) {
					var inflater = Context.GetSystemService(Context.LayoutInflaterService) as LayoutInflater;
					row = inflater.Inflate(global::Android.Resource.Layout.SimpleListItem2, null);
				}

				var port = this.GetItem(position);
				var driver = port.Driver;
				var device = driver.Device;

				var title = string.Format ("Vendor {0} Product {1}", HexDump.ToHexString ((short)device.VendorId), HexDump.ToHexString ((short)device.ProductId));
				row.FindViewById<TextView> (global::Android.Resource.Id.Text1).Text = title;
                
				var subtitle = device.Class.SimpleName;
				row.FindViewById<TextView> (global::Android.Resource.Id.Text2).Text = subtitle;

                return row;
			}

            // Function called from OnDestroy
            
        }

		#endregion

		#region UsbDeviceDetachedReceiver implementation

		class UsbDeviceDetachedReceiver
			: BroadcastReceiver
		{
			readonly string TAG = typeof(UsbDeviceDetachedReceiver).Name;
			readonly DeviceListActivity activity;

			public UsbDeviceDetachedReceiver(DeviceListActivity activity)
			{
				this.activity = activity;
			}

			public override void OnReceive (Context context, Intent intent)
			{
				var device = intent.GetParcelableExtra(UsbManager.ExtraDevice) as UsbDevice;

				Log.Info (TAG, "USB device detached: " + device.DeviceName);

				activity.PopulateListAsync();
			}
		}

		#endregion
	}
}


