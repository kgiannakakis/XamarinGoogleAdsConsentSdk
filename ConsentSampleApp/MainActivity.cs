using System;
using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V7.App;
using Android.Util;
using Android.Views;
using Android.Widget;
using ConsentLibrary;

namespace ConsentSampleApp
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, ConsentFormListener
    {

        private const String TAG = "MainActivity";

        private ConsentForm form;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            Android.Support.V7.Widget.Toolbar toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            FloatingActionButton fab = FindViewById<FloatingActionButton>(Resource.Id.fab);
            fab.Click += FabOnClick;

            InitConsent();
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            int id = item.ItemId;
            if (id == Resource.Id.action_settings)
            {
                return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        private void FabOnClick(object sender, EventArgs eventArgs)
        {
            View view = (View) sender;
            Snackbar.Make(view, "Replace with your own action", Snackbar.LengthLong)
                .SetAction("Action", (Android.Views.View.IOnClickListener)null).Show();
        }

        private async void InitConsent()
        {
            ConsentInformation consentInformation = ConsentInformation.GetInstance(ApplicationContext);
            String[] publisherIds = { BaseContext.GetString(Resource.String.admob_publisher_id) };
            await consentInformation.RequestConsentInfoUpdate(publisherIds,
                (consentStatus) =>
                {
                    Log.Info(TAG, "Status = " + consentStatus.ToString());
                    LoadConsentForm();
                },
                (msg) =>
                {
                    Log.Error(TAG, msg);
                }
                );
        }

        private void LoadConsentForm()
        {
            String privacyUrl = BaseContext.GetString(Resource.String.privacy_url);
            form = new ConsentForm.Builder(this, privacyUrl).
                                        WithListener(this).
                                        WithPersonalizedAdsOption().
                                        WithNonPersonalizedAdsOption().
                                        Build();
            form.Load();
        }

        public void OnConsentFormLoaded()
        {
            Log.Info(TAG, "Loaded");
            form.Show();
        }

        public void OnConsentFormError(string reason)
        {
            Log.Info(TAG, "Error: " + reason);
        }

        public void OnConsentFormOpened()
        {
            Log.Info(TAG, "Opened");
        }

        public void OnConsentFormClosed(ConsentStatus consentStatus, bool userPrefersAdFree)
        {
            Log.Info(TAG, "Closed. Status: " + consentStatus.ToString() + ", user prefers ad free " +
                userPrefersAdFree);
        }
    }
}

