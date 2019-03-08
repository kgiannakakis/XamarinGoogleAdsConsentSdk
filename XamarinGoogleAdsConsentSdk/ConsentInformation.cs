using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using Android.Content;
using Android.OS;
using Android.Provider;
using Android.Util;
using Java.Math;
using Java.Security;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace ConsentLibrary
{
    public class ConsentInformation
    {
        private const String MOBILE_ADS_SERVER_URL =
            "https://adservice.google.com/getconfig/pubvendors";
        private const String TAG = "ConsentInformation";
        private const String PREFERENCES_FILE_KEY = "mobileads_consent";
        private const String CONSENT_DATA_KEY = "consent_string";
        private static ConsentInformation instance;

        private readonly Context context;
        private List<String> testDevices;
        private String HashedDeviceId { get; set; }
        public DebugGeography DebugGeography { get; set; }

        private ConsentInformation(Context context)
        {
            this.context = context.ApplicationContext;
            this.DebugGeography = DebugGeography.DEBUG_GEOGRAPHY_DISABLED;
            this.testDevices = new List<String>();
            this.HashedDeviceId = GetHashedDeviceId();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static ConsentInformation GetInstance(Context context)
        {
            if (instance == null)
            {
                instance = new ConsentInformation(context);
            }
            return instance;
        }

        protected String GetHashedDeviceId()
        {
            ContentResolver contentResolver = context.ContentResolver;
            String androidId =
                contentResolver == null
                    ? null
                    : Settings.Secure.GetString(contentResolver, Settings.Secure.AndroidId);
            return md5(((androidId == null) || IsEmulator()) ? "emulator" : androidId);
        }

        /** Return the MD5 hash of a string. */
        private String md5(String str)
        {
            // Old devices have a bug where OpenSSL can leave MessageDigest in a bad state, but trying
            // multiple times seems to clear it.
            for (int i = 0; i < 3 /** max attempts */; ++i)
            {
                try
                {
                    MessageDigest _md5 = MessageDigest.GetInstance("MD5");
                    _md5.Update(Encoding.ASCII.GetBytes(str));
                    return new BigInteger(1, _md5.Digest()).ToString(16)
                        .ToUpper().PadLeft(32, '0');
                }
                catch (NoSuchAlgorithmException e)
                {
                    // Try again.
                    Log.Error(TAG, e.Message);
                }
                catch (ArithmeticException ex)
                {
                    Log.Error(TAG, ex.Message);
                    return null;
                }
            }
            return null;
        }

        private bool IsEmulator()
        {
            return Build.Fingerprint.StartsWith("generic")
                || Build.Fingerprint.StartsWith("unknown")
                || Build.Model.Contains("google_sdk")
                || Build.Model.Contains("Emulator")
                || Build.Model.Contains("Android SDK built for x86")
                || Build.Manufacturer.Contains("Genymotion")
                || (Build.Brand.StartsWith("generic") && Build.Device.StartsWith("generic"))
                || "google_sdk".Equals(Build.Product);
        }

        /** Returns if the current device is a designated debug device. */
        public bool IsTestDevice()
        {
            return IsEmulator() || testDevices.Contains(HashedDeviceId);
        }


        /**
         * Registers a device as a test device. Test devices will respect debug geography settings to
         * enable easier testing. Test devices must be added individually so that debug geography
         * settings won't accidentally get released to all users.
         * <p>You can access the hashedDeviceId from logcat once your app calls
         * requestConsentInfoUpdate.</p>
         *
         * @param hashedDeviceId The hashed device id that should be considered a debug device.
         */
        public void AddTestDevice(String hashedDeviceId)
        {
            this.testDevices.Add(hashedDeviceId);
        }

        internal class AdNetworkLookupResponse
        {
            [JsonProperty(PropertyName = "ad_network_id")]
            internal String Id { get; set; }

            [JsonProperty(PropertyName = "company_ids")]
            internal List<String> CompanyIds { get; set; }

            [JsonProperty(PropertyName = "lookup_failed")]
            internal bool LookupFailed { get; set; }

            [JsonProperty(PropertyName = "not_found")]
            internal bool NotFound { get; set; }

            [JsonProperty(PropertyName = "is_npa")]
            internal bool IsNPA { get; set; }
        }

        internal class ServerResponse
        {
            [JsonProperty(PropertyName = "companies")]
            internal List<AdProvider> Companies { get; set; }

            [JsonProperty(PropertyName = "ad_network_ids")]
            internal List<AdNetworkLookupResponse> AdNetworkLookupResponses { get; set; }

            [JsonProperty(PropertyName = "is_request_in_eea_or_unknown")]
            internal bool? IsRequestLocationInEeaOrUnknown { get; set; }
        }


        public async Task RequestConsentInfoUpdate(String[] publisherIds,
                                                   Action<ConsentStatus> OnConsentInfoUpdated,
                                                   Action<String> OnFailedToUpdateConsentInfo)
        {
            await RequestConsentInfoUpdate(publisherIds, MOBILE_ADS_SERVER_URL, 
                OnConsentInfoUpdated, OnFailedToUpdateConsentInfo);
        }

        protected async Task RequestConsentInfoUpdate(String[] publisherIds, String url,
                                                      Action<ConsentStatus> OnConsentInfoUpdated,
                                                      Action<String> OnFailedToUpdateConsentInfo)
        {
            if (IsTestDevice())
            {
                Log.Info(TAG, "This request is sent from a test device.");
            }
            else
            {
                Log.Info(TAG, "Use ConsentInformation.getInstance(context).addTestDevice(\""
                          + HashedDeviceId
                          + "\") to get test ads on this device.");
            }

            String publisherIdsString = String.Join(',', publisherIds);


            ConsentData consentData = LoadConsentData();

            Android.Net.Uri.Builder uriBuilder =
                Android.Net.Uri.Parse(url)
                    .BuildUpon()
                    .AppendQueryParameter("pubs", publisherIdsString)
                    .AppendQueryParameter("es", "2")
                    .AppendQueryParameter("plat", consentData.SdkPlatformString)
                    .AppendQueryParameter("v", consentData.SdkVersionString);
            if (IsTestDevice() && DebugGeography != DebugGeography.DEBUG_GEOGRAPHY_DISABLED)
            {
                uriBuilder =
                    uriBuilder.AppendQueryParameter(
                        "debug_geo",
                        ((int)DebugGeography).ToString());
            }
            await MakeConsentLookupRequest(uriBuilder.Build().ToString(), publisherIds,
                                           OnConsentInfoUpdated, OnFailedToUpdateConsentInfo);
        }

        internal ConsentData LoadConsentData()
        {
            ISharedPreferences sharedPref = 
                context.GetSharedPreferences(PREFERENCES_FILE_KEY, FileCreationMode.Private);
                
            String consentDataString = sharedPref.GetString(CONSENT_DATA_KEY, "");

            if (String.IsNullOrEmpty(consentDataString))
            {
                return new ConsentData();
            }
            else
            {
                return JsonConvert.DeserializeObject<ConsentData>(consentDataString);
            }
        }

        private async Task MakeConsentLookupRequest(String urlString,
                                                    String[] publisherIds,
                                                    Action<ConsentStatus> OnConsentInfoUpdated,
                                                    Action<String> OnFailedToUpdateConsentInfo)
        {
            try
            {
                HttpClient client = new HttpClient();
                client.MaxResponseContentBufferSize = 256000;

                var uri = new Uri(urlString);

                HttpResponseMessage response  = await client.GetAsync(uri);
                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    UpdateConsentData(responseString, publisherIds.ToList());
                    OnConsentInfoUpdated?.Invoke(GetConsentStatus());
                }
                else
                {
                    OnFailedToUpdateConsentInfo(response.ReasonPhrase);
                }
            }
            catch (Exception e)
            {
                OnFailedToUpdateConsentInfo(e.Message);
            }
        }

        private void SaveConsentData(ConsentData consentData)
        {
            ISharedPreferences sharedPref =
                context.GetSharedPreferences(PREFERENCES_FILE_KEY, FileCreationMode.Private);
            ISharedPreferencesEditor editor = sharedPref.Edit();
            String consentDataString = JsonConvert.SerializeObject(consentData);
            editor.PutString(CONSENT_DATA_KEY, consentDataString);
            editor.Apply();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SetTagForUnderAgeOfConsent(bool underAgeOfConsent)
        {
            ConsentData consentData = LoadConsentData();
            consentData.UnderAgeOfConsent = underAgeOfConsent;
            SaveConsentData(consentData);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool IsTaggedForUnderAgeOfConsent()
        {
            return LoadConsentData().UnderAgeOfConsent;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Reset()
        {
            ISharedPreferences sharedPref =
                context.GetSharedPreferences(PREFERENCES_FILE_KEY, FileCreationMode.Private);
            ISharedPreferencesEditor editor = sharedPref.Edit();
            editor.Clear();
            editor.Apply();
            this.testDevices = new List<String>();
        }

        private void ValidatePublisherIds(ServerResponse response)
        {

            if (response.IsRequestLocationInEeaOrUnknown == null) 
            {
                throw new Exception("Could not parse Event FE preflight response.");
            }

            if (response.Companies == null && response.IsRequestLocationInEeaOrUnknown.Value)
            {
                throw new Exception("Could not parse Event FE preflight response.");
            }

            if (!response.IsRequestLocationInEeaOrUnknown.Value) 
            {
                return;
            }

            var lookupFailedPublisherIds = new HashSet<String>();
            var notFoundPublisherIds = new HashSet<String>();

            foreach (AdNetworkLookupResponse adNetworkLookupResponse in response.AdNetworkLookupResponses)
            {
                if (adNetworkLookupResponse.LookupFailed)
                {
                    lookupFailedPublisherIds.Add(adNetworkLookupResponse.Id);
                }

                if (adNetworkLookupResponse.NotFound)
                {
                    notFoundPublisherIds.Add(adNetworkLookupResponse.Id);
                }
            }

            if (lookupFailedPublisherIds.Count == 0 && notFoundPublisherIds.Count == 0)
            {
                return;
            }

            StringBuilder errorString = new StringBuilder("Response error.");

            if (lookupFailedPublisherIds.Count > 0) 
            {
                String lookupFailedPublisherIdsString = String.Join(',', lookupFailedPublisherIds);
                errorString.Append(
                    String.Format(" Lookup failure for: {0}.", lookupFailedPublisherIdsString));
            }

            if (notFoundPublisherIds.Count > 0) 
            {
                String notFoundPublisherIdsString = String.Join(',', notFoundPublisherIds);
                errorString.Append(
                    String.Format(" Publisher Ids not found: {0}", notFoundPublisherIdsString));
            }

            throw new Exception(errorString.ToString());
        }

        private HashSet<AdProvider> GetNonPersonalizedAdProviders(List<AdProvider> adProviders,
                                      HashSet<String> nonPersonalizedAdProviderIds)
        {
            var nonPersonalizedAdProviders = new List<AdProvider>();
            foreach (AdProvider adProvider in adProviders)
            {
                if (nonPersonalizedAdProviderIds.Contains(adProvider.Id))
                {
                    nonPersonalizedAdProviders.Add(adProvider);
                }
            }

            return new HashSet<AdProvider>(nonPersonalizedAdProviders);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void UpdateConsentData(String responseString,
                                       List<String> publisherIds)
        {
            ServerResponse response = JsonConvert.DeserializeObject<ServerResponse>(responseString);
            ValidatePublisherIds(response);

            bool hasNonPersonalizedPublisherId = false;
            var nonPersonalizedAdProvidersIds = new HashSet<String>();

            if (response.AdNetworkLookupResponses != null)
            {
                foreach (AdNetworkLookupResponse adNetworkLookupResponse in
                            response.AdNetworkLookupResponses)
                {
                    if (!adNetworkLookupResponse.IsNPA)
                    {
                        continue;
                    }

                    hasNonPersonalizedPublisherId = true;
                    List<String> companyIds = adNetworkLookupResponse.CompanyIds;
                    if (companyIds != null)
                    {
                        nonPersonalizedAdProvidersIds.UnionWith(companyIds);
                    }
                }
            }

            HashSet<AdProvider> newAdProviderSet;
            if (response.Companies == null)
            {
                newAdProviderSet = new HashSet<AdProvider>();
            }
            else if (hasNonPersonalizedPublisherId)
            {
                newAdProviderSet =
                    GetNonPersonalizedAdProviders(response.Companies, nonPersonalizedAdProvidersIds);
            }
            else
            {
                newAdProviderSet = new HashSet<AdProvider>(response.Companies);
            }

            ConsentData consentData = LoadConsentData();

            bool hasNonPersonalizedPublisherIdChanged =
                consentData.HasNonPersonalizedPublisherId != hasNonPersonalizedPublisherId;

            consentData.HasNonPersonalizedPublisherId = hasNonPersonalizedPublisherId;
            consentData.RawResponse = responseString;
            consentData.PublisherIds = new HashSet<String>(publisherIds);
            consentData.AdProviders = newAdProviderSet;
            consentData.IsRequestLocationInEeaOrUnknown = response.IsRequestLocationInEeaOrUnknown.HasValue &&
                                                            response.IsRequestLocationInEeaOrUnknown.Value;

            if (!response.IsRequestLocationInEeaOrUnknown.Value)
            {
                SaveConsentData(consentData);
                return;
            }

            if (!consentData.AdProviders.SequenceEqual(consentData.ConsentedAdProviders)
                || hasNonPersonalizedPublisherIdChanged)
            {
                consentData.ConsentSource = "sdk";
                consentData.ConsentStatus = ConsentStatus.UNKNOWN;
                consentData.ConsentedAdProviders = new HashSet<AdProvider>();
            }
            SaveConsentData(consentData);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public List<AdProvider> GetAdProviders() {
            ConsentData consentData = LoadConsentData();
            return consentData.AdProviders.ToList();
        }

        public bool IsRequestLocationInEeaOrUnknown()
        {
            ConsentData consentData = LoadConsentData();
            return consentData.IsRequestLocationInEeaOrUnknown;
        }

        public void SetConsentStatus(ConsentStatus consentStatus)
        {
            SetConsentStatus(consentStatus, "programmatic");
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void SetConsentStatus(ConsentStatus consentStatus, String source)
        {
            ConsentData consentData = LoadConsentData();
            if (consentStatus == ConsentStatus.UNKNOWN)
            {
                consentData.AdProviders = new HashSet<AdProvider>();
            }
            else
            {
                consentData.ConsentedAdProviders = consentData.AdProviders;
            }

            consentData.ConsentSource = source;
            consentData.ConsentStatus = consentStatus;
            SaveConsentData(consentData);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ConsentStatus GetConsentStatus()
        {
            ConsentData consentData = LoadConsentData();
            return consentData.ConsentStatus;
        }
    }
}
