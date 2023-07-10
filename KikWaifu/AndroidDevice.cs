using DankLibWaifuz.CollectionsWaifu;
using DankLibWaifuz.Etc;
using System.Collections.Generic;

namespace KikWaifu
{
    public class AndroidDevice
    {
        public string Manufacturer { get; }
        public string SdkApiVersion { get; }
        public string Model { get; }
        public string DalvikUserAgent { get; }
        public string OsVersion { get; }
        public string CarrierCode { get; }
        public string BuildId { get; }
        public bool IsValid { get; }

        public AndroidDevice(string input)
        {
            if (string.IsNullOrWhiteSpace(input) || !input.Contains("|"))
                return;

            var split = input.Split('|');
            if (split.Length < 6)
                return;

            Manufacturer = split[0];
            SdkApiVersion = split[1];
            Model = split[2];
            DalvikUserAgent = split[3];
            OsVersion = split[4];
            BuildId = split[5];

            if (split.Length == 6)
                CarrierCode = RandomCarrierInfo().Value;
            else
                CarrierCode = split[6];

            IsValid =
                !GeneralHelpers.AnyNullOrWhiteSpace(Manufacturer, SdkApiVersion, Model, DalvikUserAgent, OsVersion, CarrierCode,
                    BuildId);
        }

        public override string ToString()
        {
            return $"{Manufacturer}|{SdkApiVersion}|{Model}|{DalvikUserAgent}|{OsVersion}|{BuildId}|{CarrierCode}";
        }

        private static readonly List<KeyValuePair<string, string>> CarrierCodes = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("Verizon Wireless", "310012"),
            new KeyValuePair<string, string>("T-Mobile", "310026"),
            new KeyValuePair<string, string>("AT&T", "310070"),
            new KeyValuePair<string, string>("T-Mobile", "310260"),
            new KeyValuePair<string, string>("Sprint", "311870")
        };
        
        private static KeyValuePair<string, string> RandomCarrierInfo()
        {
            return CarrierCodes.RandomSelection();
        }

    }
}
