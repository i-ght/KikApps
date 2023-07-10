using DankLibWaifuz.CollectionsWaifu;
using DankLibWaifuz.Etc;
using KikWaifu;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KikCreator2.Work
{
    internal class Account
    {
        private static readonly string[] Seperators = { ".", "", "_" };

        private bool _setPasskeys;

        public string LoginId { get; set; }
        public string Password { get; }
        public string PasskeyE { get; set; }
        public string PasskeyU { get; set; }
        public string Email { get; }
        public string FirstName { get; }
        public string LastName { get; }
        public string Birthday { get; }
        public string AndroidId { get; }
        public string DeviceId { get; }
        public string Sid { get; }
        public string Jid { get; set; }
        public string BrowserUserAgent { get; }

        public int FailedUsernameLookUpAttempts { get; set; }

        public string DeviceCanId => $"CAN{DeviceId}";

        public AndroidDevice AndroidDevice { get; }

        public MetricsVars MetricsVars { get; } = new MetricsVars();

        public Queue<string> Images { get; }

        public bool IsValid { get; }

        public Account(string firstName, string lastName, string word1, string word2, string androidDevice, string imageDir)
        {
            if (GeneralHelpers.AnyNullOrWhiteSpace(firstName, lastName, word1, word2, androidDevice, imageDir))
                return;

            AndroidDevice = new AndroidDevice(androidDevice);
            if (!AndroidDevice.IsValid)
                return;

            Images = GeneralHelpers.JpgsFromDir(imageDir);
            if (Images.Count == 0)
                return;

            FirstName = GeneralHelpers.Normalize(firstName).ToLower();
            LastName = GeneralHelpers.Normalize(lastName).ToLower();

            word1 = GeneralHelpers.Normalize(word1).ToLower();
            word2 = GeneralHelpers.Normalize(word2).ToLower();

            LoginId = $"{word1}{Seperators.RandomSelection()}{word2}";
            Email = $"{FirstName}{LastName}{Mode.Random.Next(999)}@{GeneralHelpers.RandomEmailDomain()}".ToLower();
            Password = GeneralHelpers.RandomString(Mode.Random.Next(8, 13), GeneralHelpers.GenType.LowerLetNum);
            AndroidId = AndroidHelpers.GenerateAndroidId();
            Birthday = GeneralHelpers.GenerateDateOfBirth(19, 44).ToString("yyyy-MM-dd");
            DeviceId = Guid.NewGuid().ToString("N");
            Sid = Guid.NewGuid().ToString();

            BrowserUserAgent =
                $"Mozilla/5.0 (Linux; Android {AndroidDevice.OsVersion}; {AndroidDevice.Model} Build/{AndroidDevice.BuildId}) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/46.0.2490.43 Mobile Safari/537.36";

            IsValid = true;
        }

        public async Task SetPasskeys()
        {
            if (_setPasskeys)
                return;

            await Task.Run(() => (PasskeyE = Krypto.KikPasskey(LoginId, Password))).ConfigureAwait(false);
            await Task.Run(() => (PasskeyU = Krypto.KikPasskey(LoginId, Password))).ConfigureAwait(false);

            _setPasskeys = true;
        }

        public override string ToString()
        {
            return $"{Email}:{Password}:{Jid}:{LoginId}:{AndroidId}:{DeviceId}:{PasskeyE}:{PasskeyU}:{AndroidDevice}";
        }
    }
}
