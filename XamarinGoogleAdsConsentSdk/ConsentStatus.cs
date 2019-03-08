using System;
using System.Runtime.Serialization;

namespace ConsentLibrary
{
    [DataContract]
    public enum ConsentStatus
    {
        [EnumMember(Value = "unknown")]
        UNKNOWN,
        [EnumMember(Value = "non_personalized")]
        NON_PERSONALIZED,
        [EnumMember(Value = "personalized")]
        PERSONALIZED,
    }
}
