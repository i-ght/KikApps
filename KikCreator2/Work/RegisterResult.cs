namespace KikCreator2.Work
{
    internal enum RegisterStatusCode
    {
        RegisteredSuccessfully,
        Captchad,
        UnexpectedResponse,
        CouldntParseCaptchaUrl,
        CouldntParseCaptchaId,
        UnknownError
    }

    internal class RegisterResult
    {
        public RegisterStatusCode StatusCode { get; }
        public CaptchaVars CaptchaVars { get; }

        public bool IsValid => CaptchaVars != null && CaptchaVars.IsValid;

        public RegisterResult(RegisterStatusCode statusCode)
        {
            StatusCode = statusCode;
        }

        public RegisterResult(RegisterStatusCode statusCode, string captchaUrl, string captchaId) : this(statusCode)
        {
            CaptchaVars = new CaptchaVars(captchaUrl, captchaId);
        }
    }
}
