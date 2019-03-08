using System;

namespace ConsentLibrary
{
    public interface ConsentFormListener
    {
        void OnConsentFormLoaded();
        void OnConsentFormError(String reason);
        void OnConsentFormOpened();
        void OnConsentFormClosed(ConsentStatus consentStatus, Boolean userPrefersAdFree);
    }
}
