using System;

namespace ConsentLibrary
{
    public interface ConsentInfoUpdateListener
    {
        void OnConsentInfoUpdated(ConsentStatus consentStatus);
        void OnFailedToUpdateConsentInfo(String reason);
    }
}
