using DankLibWaifuz.Etc;
using KikWaifu;
using System;

namespace KikContactVerifier2.Work
{
    internal class Account
    {
        public int Attempts;
        public int Verified;
        public int RateIndex;

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
        public AndroidDevice AndroidDevice { get; }

        public bool IsRated { get; set; }

        public bool LoggedInSuccessfully { get; set; }

        public VerifySession VerifySession { get; set; } = new VerifySession();

        public DateTime NextLogin { get; set; }

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
            AndroidDevice = new AndroidDevice(split[8]);

            IsValid = !GeneralHelpers.AnyNullOrWhiteSpace(Email, Password, Jid, LoginId, AndroidId, PasskeyE, PasskeyU);
        }

        public override string ToString()
        {
            return $"{Email}:{Password}:{Jid}:{LoginId}:{AndroidId}:{DeviceId}:{PasskeyE}:{PasskeyU}:{AndroidDevice}";
        }
    }
}
