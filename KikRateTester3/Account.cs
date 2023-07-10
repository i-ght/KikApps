using DankWaifu.Sys;
using KikWaifu;

namespace KikRateTester3
{
    internal class Account
    {
        public string LoginId { get; }
        public string Password { get; }
        public string PasskeyE { get; }
        public string PasskeyU { get; }
        public string Email { get; }
        public string AndroidId { get; }
        public string DeviceId { get; }
        public string Sid { get; set; }

        public bool Invalid { get; set; }

        public int LoginErrors { get; set; }

        public AndroidDevice AndroidDevice { get; set; }

        public bool LoggedInSuccessfully { get; set; }
        public bool MsgReceived { get; set; }

        public string DeviceCanId => $"CAN{DeviceId}";

        public string Jid
        {
            get { return $"{JidNoDomain}@talk.kik.com".ToLower(); }
        }

        public bool TooManyLoginErrors
        {
            get { return LoginErrors >= Settings.Get<int>(Constants.MaxLoginErrors); }
        }

        public string JidNoDomain { get; }

        public bool IsValid { get; }

        public Account(string input)
        {
            if (string.IsNullOrWhiteSpace(input) || !input.Contains(":"))
                return;

            var split = input.Split(':');
            if (split.Length < 9)
                return;

            Email = split[0];
            Password = split[1];
            JidNoDomain = split[2];
            LoginId = split[3];
            AndroidId = split[4];
            DeviceId = split[5];
            PasskeyE = split[6];
            PasskeyU = split[7];
            AndroidDevice = new AndroidDevice(split[8]);

            IsValid = !StringHelpers.AnyNullOrWhitespace(Email, Password, JidNoDomain,
                LoginId, AndroidId, DeviceId, PasskeyE, PasskeyU) &&
                AndroidDevice.IsValid;

        }

        public override string ToString()
        {
            return
                $"{Email}:{Password}:{JidNoDomain}:{LoginId}:{AndroidId}:{DeviceId}:{PasskeyE}:{PasskeyU}:{AndroidDevice}";
        }
    }
}
