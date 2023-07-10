using DankLibWaifuz.Etc;
using DankLibWaifuz.ScriptWaifu;
using DankLibWaifuz.SettingsWaifu;
using System.Collections.Generic;

namespace KikBot2.Work
{
    internal class Account
    {
        public int In;
        public int Out;

        public string LoginId { get; }
        public string Password { get; }
        public string PasskeyE { get; }
        public string PasskeyU { get; }
        public string Email { get; }
        public string AndroidId { get; }
        public string DeviceId { get; }
        public string Jid { get; }
        public int LoginErrors { get; set; }
        public string Sid { get; set; }
        public string DeviceCanId => $"CAN{DeviceId}";

        //public Queue<OutgoingMessage> PendingMessages { get; } = new Queue<OutgoingMessage>();

        public Dictionary<string, ScriptWaifu> Convos { get; } = new Dictionary<string, ScriptWaifu>();

        public bool TooManyLoginErrors => LoginErrors > Settings.Get<int>("MaxLoginErrors");

        public bool IsValid { get; }

        public Account(string input)
        {
            if (string.IsNullOrWhiteSpace(input) || !input.Contains(":"))
                return;

            var split = input.Split(':');
            if (split.Length < 8)
                return;

            Email = split[0];
            Password = split[1];
            Jid = $"{split[2]}@talk.kik.com";
            LoginId = split[3];
            AndroidId = split[4];
            DeviceId = split[5];
            PasskeyE = split[6];
            PasskeyU = split[7];

            IsValid = !GeneralHelpers.AnyNullOrWhiteSpace(Password, Jid, LoginId, AndroidId, PasskeyE, PasskeyU);
        }
    }
}
