using System;
using System.Collections.Generic;
using DankLibWaifuz.Etc;
using DankLibWaifuz.ScriptWaifu;
using DankLibWaifuz.SettingsWaifu;
using KikWaifu;

namespace KikBot3.Work
{
    internal class Account
    {
        private readonly AndroidDevice _androidDevice;

        private string _browserUserAgent;
        private string _kikUserAgent;

        public volatile int Out;

        public Account(string input)
        {
            if (string.IsNullOrWhiteSpace(input) || !input.Contains(":"))
                return;

            var split = input.Split(':');
            if (split.Length < 9)
                return;

            Email = split[0];
            Password = split[1];
            Jid = $"{split[2]}@talk.kik.com";
            LoginId = split[3];
            AndroidId = split[4];
            DeviceId = split[5];
            PasskeyE = split[6];
            PasskeyU = split[7];
            _androidDevice = new AndroidDevice(split[8]);

            PendingMessages = new Queue<OutgoingMessage>();
            PendingGreets = new Queue<OutgoingMessage>();
            Convos = new Dictionary<string, ScriptWaifu>();
            Contacts = new HashSet<string>();

            IsValid = !GeneralHelpers.AnyNullOrWhiteSpace(Password, Jid, LoginId, AndroidId, PasskeyE, PasskeyU) &&
                      _androidDevice.IsValid;
        }

        public string BrowserUserAgent
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_browserUserAgent))
                    return _browserUserAgent;

                _browserUserAgent = $"Mozilla/5.0 (Linux; Android {_androidDevice.OsVersion}; {_androidDevice.Model} " +
                                    $"Build/{_androidDevice.BuildId}) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/" +
                                    "46.0.2490.43 Mobile Safari/537.36";
                return _browserUserAgent;
            }
        }

        public string KikUserAgent
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_kikUserAgent))
                    return _kikUserAgent;

                _kikUserAgent =
                    $"Kik/{Konstants.AppVersion} (Android {_androidDevice.OsVersion}) {_androidDevice.DalvikUserAgent}";
                return _kikUserAgent;
            }
        }

        public int In { get; set; }
        public string LoginId { get; }
        public string Password { get; }
        public string PasskeyE { get; }
        public string PasskeyU { get; }
        public string Email { get; }
        public string AndroidId { get; }
        public string DeviceId { get; }
        public string Jid { get; }
        public int LoginErrors { get; set; }
        public int CaptchaErrors { get; set; }
        public string Sid { get; set; }
        public string DeviceCanId => $"CAN{DeviceId}";
        public DateTime LastMessageRecievedAt { get; set; }

        public Queue<OutgoingMessage> PendingMessages { get; }
        public Queue<OutgoingMessage> PendingGreets { get; }
        public Dictionary<string, ScriptWaifu> Convos { get; }
        public HashSet<string> Contacts { get; }

        public bool TooManyLoginErrors => LoginErrors >= Settings.Get<int>("MaxLoginErrors");
        public bool TooManyCaptchaErrors => CaptchaErrors >= Settings.Get<int>("MaxCaptchaErrors");

        public bool IsValid { get; }

        public override string ToString()
        {
            return $"{Email}:{Password}:{Jid.Split('@')[0]}:{LoginId}:{AndroidId}:{DeviceId}:{PasskeyE}:{PasskeyU}:{_androidDevice}";
        }

    }

}
