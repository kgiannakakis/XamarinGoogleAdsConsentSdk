using System;
using System.Collections.Generic;
using System.IO;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Util;
using Android.Views;
using Android.Webkit;
using Java.Net;
using Newtonsoft.Json;

namespace ConsentLibrary
{
    public class ConsentForm
    {
        private readonly ConsentFormListener listener;
        private readonly Context context;
        private readonly bool personalizedAdsOption;
        private readonly bool nonPersonalizedAdsOption;
        private readonly bool adFreeOption;
        private readonly URL appPrivacyPolicyURL;
        private readonly Dialog dialog;
        private readonly WebView webView;
        private LoadState loadState;

        private enum LoadState
        {
            NOT_READY,
            LOADING,
            LOADED
        }

        class ConsentFormWebViewClient : WebViewClient
        {
            bool isInternalRedirect;

            ConsentForm consentForm;

            internal ConsentFormWebViewClient(ConsentForm consentForm) : base()
            {
                this.consentForm = consentForm;
            }

            private bool isConsentFormUrl(String url)
            {
                return !String.IsNullOrEmpty(url) && url.StartsWith("consent://");
            }

            private void HandleUrl(String url)
            {
                if (!isConsentFormUrl(url))
                {
                    return;
                }

                isInternalRedirect = true;
                Android.Net.Uri uri = Android.Net.Uri.Parse(url);
                String action = uri.GetQueryParameter("action");
                String status = uri.GetQueryParameter("status");
                String browserUrl = uri.GetQueryParameter("url");

                switch (action)
                {
                    case "load_complete":
                        consentForm.HandleLoadComplete(status);
                        break;
                    case "dismiss":
                        isInternalRedirect = false;
                        consentForm.HandleDismiss(status);
                        break;
                    case "browser":
                        consentForm.HandleOpenBrowser(browserUrl);
                        break;
                    default: // fall out
                        break;
                }
            }

            public override void OnLoadResource(WebView view, String url)
            {
                HandleUrl(url);
            }

 
            public override bool ShouldOverrideUrlLoading(WebView view, IWebResourceRequest request)
            {
                String url = request.Url.ToString();
                if (isConsentFormUrl(url))
                {
                    HandleUrl(url);
                    return true;
                }
                return false;
            }

            [Obsolete]
            public override bool ShouldOverrideUrlLoading(WebView view, String url)
            {
                if (isConsentFormUrl(url))
                {
                    HandleUrl(url);
                    return true;
                }
                return false;
            }


            public override void OnPageFinished(WebView view, String url)
            {
                if (!isInternalRedirect)
                {
                    consentForm.UpdateDialogContent(view);
                }
                base.OnPageFinished(view, url);
            }


            public override void OnReceivedError(
                    WebView view, IWebResourceRequest request, WebResourceError error)
            {
                base.OnReceivedError(view, request, error);
                consentForm.loadState = LoadState.NOT_READY;
                consentForm.listener.OnConsentFormError(error.ToString());
            }
        }

        private ConsentForm(Builder builder)
        {
            this.context = builder.context;

            if (builder.listener == null)
            {
                this.listener = null;  //new ConsentFormListener() { };
            }
            else
            {
                this.listener = builder.listener;
            }

            this.personalizedAdsOption = builder.personalizedAdsOption;
            this.nonPersonalizedAdsOption = builder.nonPersonalizedAdsOption;
            this.adFreeOption = builder.adFreeOption;
            this.appPrivacyPolicyURL = builder.appPrivacyPolicyURL;
            this.dialog = new Dialog(context, Android.Resource.Style.ThemeTranslucentNoTitleBar);
            this.loadState = LoadState.NOT_READY;

            this.webView = new WebView(context);
            this.webView.SetBackgroundColor(Color.Transparent);
            this.dialog.SetContentView(webView);
            this.dialog.SetCancelable(false);
            webView.Settings.JavaScriptEnabled = true;
            webView.SetWebViewClient(new ConsentFormWebViewClient(this));
        }

        /**
         * Creates a new {@link Builder} for constructing a {@link ConsentForm}.
         */
        public class Builder
        {
            internal readonly Context context;
            internal ConsentFormListener listener;
            internal bool personalizedAdsOption;
            internal bool nonPersonalizedAdsOption;
            internal bool adFreeOption;
            internal readonly URL appPrivacyPolicyURL;

            public Builder(Context context, String appPrivacyPolicyURL)
            {
                this.context = context;
                this.personalizedAdsOption = false;
                this.nonPersonalizedAdsOption = false;
                this.adFreeOption = false;

                if (appPrivacyPolicyURL == null)
                {
                    throw new Exception("Must provide valid app privacy policy url"
                        + " to create a ConsentForm");
                }

                try
                {
                    this.appPrivacyPolicyURL = new URL(appPrivacyPolicyURL);
                }
                catch (MalformedURLException e)
                {
                    throw new Exception("Must provide valid app privacy policy url"
                     + " to create a ConsentForm");
                }
            }

            public Builder WithListener(ConsentFormListener listener)
            {
                this.listener = listener;
                return this;
            }

            public Builder WithPersonalizedAdsOption()
            {
                this.personalizedAdsOption = true;
                return this;
            }

            public Builder WithNonPersonalizedAdsOption()
            {
                this.nonPersonalizedAdsOption = true;
                return this;
            }

            public Builder WithAdFreeOption()
            {
                this.adFreeOption = true;
                return this;
            }

            public ConsentForm Build()
            {
                return new ConsentForm(this);
            }
        }

        private static String GetApplicationName(Context context)
        {
            return context.ApplicationInfo.LoadLabel(context.PackageManager).ToString();
        }

        private static String GetAppIconURIString(Context context)
        {
            Drawable iconDrawable = context.PackageManager.GetApplicationIcon(context.ApplicationInfo);
            Bitmap bitmap = Bitmap.CreateBitmap(iconDrawable.IntrinsicWidth,
                iconDrawable.IntrinsicHeight, Bitmap.Config.Argb8888);
            Canvas canvas = new Canvas(bitmap);
            iconDrawable.SetBounds(0, 0, canvas.Width, canvas.Height);
            iconDrawable.Draw(canvas);

            byte[] byteArray;
            using (var stream = new MemoryStream())
            {
                bitmap.Compress(Bitmap.CompressFormat.Png, 100, stream);
                byteArray = stream.ToArray();
            }

            return "data:image/png;base64," + Base64.EncodeToString(byteArray, Base64Flags.Default);
        }

        private static String CreateJavascriptCommand(String command, String argumentsJSON)
        {
            var args = new Dictionary<String, Object>();
            args.Add("info", argumentsJSON);
            var wrappedArgs = new Dictionary<String, Object>();
            wrappedArgs.Add("args", args);
            return String.Format("javascript:{0}({1})", command,
                JsonConvert.SerializeObject(wrappedArgs));
        }

        private void UpdateDialogContent(WebView webView)
        {
            var formInfo = new Dictionary<String, Object>();
            formInfo.Add("app_name", GetApplicationName(context));
            formInfo.Add("app_icon", GetAppIconURIString(context));
            formInfo.Add("offer_personalized", this.personalizedAdsOption);
            formInfo.Add("offer_non_personalized", this.nonPersonalizedAdsOption);
            formInfo.Add("offer_ad_free", this.adFreeOption);
            formInfo.Add("is_request_in_eea_or_unknown",
                ConsentInformation.GetInstance(context).IsRequestLocationInEeaOrUnknown());
            formInfo.Add("app_privacy_url", this.appPrivacyPolicyURL);
            ConsentData consentData = ConsentInformation.GetInstance(context).LoadConsentData();

            formInfo.Add("plat", consentData.SdkPlatformString);
            formInfo.Add("consent_info", consentData);

            String argumentsJSON = JsonConvert.SerializeObject(formInfo);
            String javascriptCommand = CreateJavascriptCommand("setUpConsentDialog",
                argumentsJSON);
            webView.LoadUrl(javascriptCommand);
        }

        public void Load()
        {
            if (this.loadState == LoadState.LOADING)
            {
                listener.OnConsentFormError("Cannot simultaneously load multiple consent forms.");
                return;
            }

            if (this.loadState == LoadState.LOADED)
            {
                listener.OnConsentFormLoaded();
                return;
            }

            this.loadState = LoadState.LOADING;
            this.webView.LoadUrl("file:///android_asset/Content/consentform.html"); //Load HTML file instead of html class
        }

        private void HandleLoadComplete(String status)
        {
            if (String.IsNullOrEmpty(status))
            {
                this.loadState = LoadState.NOT_READY;
                listener.OnConsentFormError("No information");
            }
            else if (status.Contains("Error"))
            {
                this.loadState = LoadState.NOT_READY;
                listener.OnConsentFormError(status);
            }
            else
            {
                this.loadState = LoadState.LOADED;
                listener.OnConsentFormLoaded();
            }
        }

        private void HandleOpenBrowser(String urlString)
        {
            if (String.IsNullOrEmpty(urlString))
            {
                listener.OnConsentFormError("No valid URL for browser navigation.");
                return;
            }

            try
            {
                Intent browserIntent = new Intent(Intent.ActionView, 
                                                  Android.Net.Uri.Parse(urlString));
                context.StartActivity(browserIntent);
            }
            catch (ActivityNotFoundException exception)
            {
                Console.WriteLine(exception.Message);
                listener.OnConsentFormError("No Activity found to handle browser intent.");
            }
        }

        private void HandleDismiss(String status)
        {
            this.loadState = LoadState.NOT_READY;
            dialog.Dismiss();

            if (String.IsNullOrEmpty(status))
            {
                listener.OnConsentFormError("No information provided.");
                return;
            }

            if (status.Contains("Error"))
            {
                listener.OnConsentFormError(status);
                return;
            }

            bool userPrefersAdFree = false;
            ConsentStatus consentStatus;
            switch (status)
            {
                case "personalized":
                    consentStatus = ConsentStatus.PERSONALIZED;
                    break;
                case "non_personalized":
                    consentStatus = ConsentStatus.NON_PERSONALIZED;
                    break;
                case "ad_free":
                    userPrefersAdFree = true;
                    consentStatus = ConsentStatus.UNKNOWN;
                    break;
                default:
                    consentStatus = ConsentStatus.UNKNOWN;
                    break;
            }

            ConsentInformation.GetInstance(context).SetConsentStatus(consentStatus, "form");
            listener.OnConsentFormClosed(consentStatus, userPrefersAdFree);
        }

        internal class DialogListener : Java.Lang.Object, IDialogInterfaceOnShowListener
        {
            ConsentFormListener listener;

            public DialogListener(ConsentFormListener listener)
            {
                this.listener = listener;
            }

            public void OnShow(IDialogInterface dialog)
            {
                listener.OnConsentFormOpened();
            }
        }

        public void Show()
        {
            if (this.loadState != LoadState.LOADED)
            {
                listener.OnConsentFormError("Consent form is not ready to be displayed.");
                return;
            }

            if (ConsentInformation.GetInstance(context).IsTaggedForUnderAgeOfConsent())
            {
                listener.OnConsentFormError("Error: tagged for under age of consent");
                return;
            }

            this.dialog.Window.SetLayout(ViewGroup.LayoutParams.MatchParent,
                                         ViewGroup.LayoutParams.MatchParent);
            this.dialog.Window.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));
            this.dialog.SetOnShowListener(new DialogListener(listener));
            this.dialog.Show();

            if (!this.dialog.IsShowing) {
                listener.OnConsentFormError("Consent form could not be displayed.");
            }
        }

        public bool IsShowing()
        {
            return this.dialog.IsShowing;
        }

    }
}
