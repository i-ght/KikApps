using DankLibWaifuz.Etc;
using KikWaifu;
using System;
using System.Text;
using System.Threading.Tasks;
using DankLibWaifuz.SettingsWaifu;

namespace KikSessionCreator.Work
{
    internal class Account
    {
        private static readonly Random Random = new Random();

        public string Email { get; }
        public string LoginId { get; set; }
        public string Password { get; }
        public string Jid { get; set; }
        public string AndroidId { get; }
        public string DeviceId { get; }
        public string PasskeyU { get; private set; }
        public string PasskeyE { get; private set; }
        public AndroidDevice AndroidDevice { get; set; }
        public string DeviceCanId => $"CAN{DeviceId}";

        public bool InvalidPassword { get; set; }

        public bool IsValid { get; }

        public int LoginErrors { get; set; }
        public bool LoggedInSuccessfully { get; set; }
        public string Sid { get; set; }
        public string InstallDate { get; }

        private string _browserUserAgent;

        public string BrowserUserAgent
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_browserUserAgent))
                    return _browserUserAgent;

                _browserUserAgent = $"Mozilla/5.0 (Linux; Android {AndroidDevice.OsVersion}; {AndroidDevice.Model} Build/{AndroidDevice.BuildId}) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/46.0.2490.43 Mobile Safari/537.36"; ;
                return _browserUserAgent;
            }
        }

        private bool _setPasskeys;

        public Account(string input, AndroidDevice androidDevice)
        {
            if (string.IsNullOrWhiteSpace(input) || !input.Contains(":") || androidDevice == null || !androidDevice.IsValid)
                return;

            AndroidDevice = androidDevice;

            var split = input.Split(':');

            switch (split.Length)
            {
                case 2:
                    if (split[0].Contains("@"))
                    {
                        Email = split[0].ToLower();
                        LoginId = "";
                        Password = split[1];
                        AndroidId = AndroidHelpers.GenerateAndroidId();
                        DeviceId = Guid.NewGuid().ToString("N");
                        InstallDate = DateTime.UtcNow.AddMonths(Random.Next(-16, -8)).CurrentTimeMillis().ToString();
                        IsValid = !GeneralHelpers.AnyNullOrWhiteSpace(Email, Password, AndroidId, DeviceId);
                    }
                    else
                    {
                        Email = "null";
                        LoginId = split[0];
                        Password = split[1];
                        AndroidId = AndroidHelpers.GenerateAndroidId();
                        DeviceId = Guid.NewGuid().ToString("N");
                        InstallDate = DateTime.UtcNow.AddMonths(Random.Next(-16, -8)).CurrentTimeMillis().ToString();
                        IsValid = !GeneralHelpers.AnyNullOrWhiteSpace(LoginId, Password, AndroidId, DeviceId);
                    }

                    return;

                case 3:
                    Email = "null";
                    LoginId = split[0];
                    Password = split[1];
                    AndroidId = split[2];
                    DeviceId = Guid.NewGuid().ToString("N");
                    InstallDate = DateTime.UtcNow.AddMonths(Random.Next(-16, -8)).CurrentTimeMillis().ToString();
                    IsValid = !GeneralHelpers.AnyNullOrWhiteSpace(LoginId, Password, AndroidId, DeviceId);
                    return;

                case 5:
                case 6:
                case 7:
                    Email = split[0];
                    Password = split[1];
                    Jid = split[2];
                    LoginId = LoginIdFromJid(Jid);
                    AndroidId = AndroidHelpers.GenerateAndroidId();
                    DeviceId = Guid.NewGuid().ToString("N");
                    InstallDate = DateTime.UtcNow.AddMonths(Random.Next(-16, -8)).CurrentTimeMillis().ToString();
                    IsValid = !GeneralHelpers.AnyNullOrWhiteSpace(LoginId, Password, Jid, AndroidId, DeviceId);
                    break;

                case 9:
                    Email = split[0];
                    Password = split[1];
                    Jid = split[2];
                    LoginId = split[3];
                    if (!Settings.Get<bool>("CreateNewSession"))
                    {
                        AndroidId = split[4];
                        DeviceId = split[5];
                        AndroidDevice = new AndroidDevice(split[8]);
                    }
                    else
                    {
                        AndroidId = AndroidHelpers.GenerateAndroidId();
                        DeviceId = Guid.NewGuid().ToString("N");
                        AndroidDevice = androidDevice;
                    }

                    PasskeyE = split[6];
                    PasskeyU = split[7];

                    InstallDate = DateTime.UtcNow.AddMonths(Random.Next(-16, -8)).CurrentTimeMillis().ToString();
                    IsValid = !GeneralHelpers.AnyNullOrWhiteSpace(LoginId, Password, Jid, AndroidId, DeviceId);
                    return;
            }
        }

        public async Task SetPasskeys()
        {
            await Task.Run(() => (PasskeyU = Krypto.KikPasskey(LoginId, Password))).ConfigureAwait(false);
            await Task.Run(() => (PasskeyE = Krypto.KikPasskey(Email, Password))).ConfigureAwait(false);
        }

        private static string LoginIdFromJid(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var split = input.Split('_');
            if (split.Length == 2)
                return split[0];

            var sb = new StringBuilder();
            for (var i = 0; i < split.Length - 1; i++)
                sb.Append(split[i] + "_");

            var ret = sb.ToString().TrimEnd('_');
            return ret;
        }

        public override string ToString()
        {
            return $"{Email}:{Password}:{Jid}:{LoginId}:{AndroidId}:{DeviceId}:{PasskeyE}:{PasskeyU}:{AndroidDevice}";
        }
    }
}