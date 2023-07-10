using Newtonsoft.Json;
using System;

namespace KikCreator2.Work.Json
{
    internal class PreRegistrationPicturePrompt
    {

        [JsonProperty("variant")]
        public string Variant { get; set; }
    }

    internal class EventDataExperiments
    {

        [JsonProperty("pre_registration_picture_prompt")]
        public PreRegistrationPicturePrompt PreRegistrationPicturePrompt { get; set; }
    }

    internal class MetricsPreRegSelected
    {

        [JsonProperty("event:origin")]
        public string EventOrigin { get; set; }

        [JsonProperty("devicePrefix")]
        public string DevicePrefix { get; set; }

        [JsonProperty("commonData:Received New People in Last 7 Days")]
        public int CommonDataReceivedNewPeopleInLast7Days { get; set; }

        [JsonProperty("commonData:Messages Received in Last 7 Days")]
        public int CommonDataMessagesReceivedInLast7Days { get; set; }

        [JsonProperty("commonData:Chat List Size")]
        public int CommonDataChatListSize { get; set; }

        [JsonProperty("event:name")]
        public string EventName { get; set; }

        [JsonProperty("commonData:OS Architecture")]
        public string CommonDataOSArchitecture { get; set; }

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("commonData:ABM Opt In")]
        public bool CommonDataABMOptIn { get; set; }

        [JsonProperty("commonData:Bubble Colour")]
        public string CommonDataBubbleColour { get; set; }

        [JsonProperty("commonData:Notify For New People")]
        public bool CommonDataNotifyForNewPeople { get; set; }

        [JsonProperty("clientVersion")]
        public string ClientVersion { get; set; }

        [JsonProperty("deviceId")]
        public string DeviceId { get; set; }

        [JsonProperty("commonData:Current Device Orientation")]
        public string CommonDataCurrentDeviceOrientation { get; set; }

        [JsonProperty("commonData:50% Core Setup Time")]
        public double CommonData50CoreSetupTime { get; set; }

        [JsonProperty("commonData:Logins Since Install")]
        public int CommonDataLoginsSinceInstall { get; set; }

        [JsonProperty("instanceId")]
        public string InstanceId { get; set; }

        [JsonProperty("eventData:experiments")]
        public EventDataExperiments EventDataExperiments { get; set; }

        [JsonProperty("commonData:New Chat List Size")]
        public int CommonDataNewChatListSize { get; set; }

        [JsonProperty("commonData:Messaging Partners in Last 7 Days")]
        public int CommonDataMessagingPartnersInLast7Days { get; set; }

        [JsonProperty("commonData:OS Version")]
        public string CommonDataOSVersion { get; set; }

        [JsonProperty("commonData:Registrations Since Install")]
        public int CommonDataRegistrationsSinceInstall { get; set; }

        [JsonProperty("commonData:Block List Size")]
        public int CommonDataBlockListSize { get; set; }

        [JsonProperty("commonData:Is Wear Installed")]
        public bool CommonDataIsWearInstalled { get; set; }

        [JsonProperty("commonData:Android Id")]
        public string CommonDataAndroidId { get; set; }

        [JsonProperty("commonData:95% Core Setup Time")]
        public double CommonData95CoreSetupTime { get; set; }
    }


}
