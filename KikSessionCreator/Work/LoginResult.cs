using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KikSessionCreator.Work
{
    internal enum LResult
    {
        Success,
        InvalidPassword,
        Captcha,
        Unknown
    }
    class LoginResult
    {
        public CaptchaVars CaptchaVars { get; }
        public LResult Result { get; }

        public LoginResult(LResult result)
        {
            Result = result;
        }

        public LoginResult(LResult result, CaptchaVars captchaVars) : this(result)
        {
            CaptchaVars = captchaVars;
        }
    }
}
