using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ConsentLibrary
{
    internal class ConsentData
    {
        private const String SDK_PLATFORM = "android";

        private const String SDK_VERSION = "1.0.8";

        [JsonProperty(PropertyName = "providers")]
        internal HashSet<AdProvider> AdProviders { get; set; }

        [JsonProperty(PropertyName = "is_request_in_eea_or_unknown")]
        internal  bool IsRequestLocationInEeaOrUnknown { get; set; }

        [JsonProperty(PropertyName = "consented_providers")]
        internal HashSet<AdProvider> ConsentedAdProviders { get; set; }

        [JsonProperty(PropertyName = "tag_for_under_age_of_consent")]
        internal bool UnderAgeOfConsent { get; set; }

        [JsonProperty(PropertyName = "consent_state")]
        [JsonConverter(typeof(StringEnumConverter))]
        internal ConsentStatus ConsentStatus { get; set; }

        [JsonProperty(PropertyName = "pub_ids")]
        internal HashSet<String> PublisherIds { get; set; }

        [JsonProperty(PropertyName = "has_any_npa_pub_id")]
        internal bool HasNonPersonalizedPublisherId { get; set; }

        [JsonProperty(PropertyName = "consent_source")]
        public String ConsentSource { get; set; }

        [JsonProperty(PropertyName = "version")]
        public String SdkVersionString { get; private set; }

        [JsonProperty(PropertyName = "plat")]
        public String SdkPlatformString { get; private set; }

        [JsonProperty(PropertyName = "raw_response")]
        internal String RawResponse { get; set; }

        internal ConsentData()
        {
            this.AdProviders = new HashSet<AdProvider>();
            this.ConsentedAdProviders = new HashSet<AdProvider>();
            this.PublisherIds = new HashSet<String>();
            this.UnderAgeOfConsent = false;
            this.ConsentStatus = ConsentStatus.UNKNOWN;
            this.IsRequestLocationInEeaOrUnknown = false;
            this.HasNonPersonalizedPublisherId = false;
            this.SdkVersionString = SDK_VERSION;
            this.SdkPlatformString = SDK_PLATFORM;
            this.RawResponse = "";
        }
    }
}
