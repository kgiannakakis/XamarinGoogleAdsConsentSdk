using System;
using Newtonsoft.Json;

namespace ConsentLibrary
{
    public class AdProvider
    {
        [JsonProperty(PropertyName = "company_id")]
        public String Id { get; set; }

        [JsonProperty(PropertyName = "company_name")]
        public String Name { get; set; }

        [JsonProperty(PropertyName = "policy_url")]
        public String PrivacyPolicyUrlString { get; set; }

        public override bool Equals(Object obj)
        {
            if (this == obj)
            {
                return true;
            }

            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            AdProvider that = (AdProvider)obj;

            return Id.Equals(that.Id);
        }

        public override int GetHashCode()
        {
            int result = Id.GetHashCode();
            return result;
        }
    }
}
