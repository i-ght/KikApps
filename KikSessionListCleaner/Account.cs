using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DankLibWaifuz.Etc;
using KikWaifu;

namespace KikSessionListCleaner
{
    internal class Account
    {
        private static readonly Random Random = new Random();

        public string Email { get; }
        public string LoginId { get; }
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

        public Account(string input)
        {
            if (string.IsNullOrWhiteSpace(input) || !input.Contains(":"))
                return;

            var split = input.Split(':');

            switch (split.Length)
            {
                case 9:
                    Email = split[0];
                    Password = split[1];
                    Jid = split[2];
                    LoginId = split[3];
                    AndroidId = split[4];
                    DeviceId = split[5];
                    PasskeyE = split[6];
                    PasskeyU = split[7];
                    AndroidDevice = new AndroidDevice(split[8]);
                    IsValid = !GeneralHelpers.AnyNullOrWhiteSpace(LoginId, Password, Jid, AndroidId, DeviceId);
                    return;

                case 6:
                    Email = split[0];
                    Password = split[1];
                    Jid = split[2];
                    LoginId = LoginIdFromJid(Jid);
                    AndroidId = split[3];
                    IsValid = !GeneralHelpers.AnyNullOrWhiteSpace(LoginId, Password, Jid, AndroidId);
                    break;
            }
        }

        public async Task SetPasskeys()
        {
            if (_setPasskeys ||
                (!string.IsNullOrWhiteSpace(PasskeyE) && !string.IsNullOrWhiteSpace(PasskeyU)))
                return;

            await Task.Run(() => (PasskeyU = Krypto.KikPasskey(LoginId, Password))).ConfigureAwait(false);
            await Task.Run(() => (PasskeyE = Krypto.KikPasskey(Email, Password))).ConfigureAwait(false);

            _setPasskeys = true;
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
            return $"{Email}:{Password}:{Jid}:{LoginId}:{AndroidId}:{DeviceId}:{PasskeyE}:{PasskeyU}:{AndroidDevice}".TrimEnd(':');
        }
    }
}
