using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using KikSessionCreator.Declarations;
using DankLibWaifuz.TcpWaifu;
using DankLibWaifuz.CollectionsWaifu;
using DankLibWaifuz;
using System.Threading;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using DankLibWaifuz.Etc;
using DankLibWaifuz.HttpWaifu;
using KikWaifu;
using DankLibWaifuz.SettingsWaifu;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using System.Web;
using OpenQA.Selenium.Interactions;
using DankLibWaifuz.CaptchaWaifu;

namespace KikSessionCreator.Work
{
    internal class SessionCreator : Mode
    {
        private Account _account;
        private TcpAsyncWaifu _tcpWaifu;
        private FirefoxDriver _driver;
        private WebProxy _driverProxy;

        private static SemaphoreSlim _firefoxLock;

        private static readonly Queue<Point> WindowPositions = new Queue<Point>(new List<Point>
        {
            new Point(0, MainWindow.ScreenHeight - 568),
            new Point(320, MainWindow.ScreenHeight - 568),
            new Point(640, MainWindow.ScreenHeight - 568),
            new Point(960, MainWindow.ScreenHeight - 568),
            new Point(1280, MainWindow.ScreenHeight - 568),
            new Point(1600, MainWindow.ScreenHeight - 568),
            new Point(0, 0),
            new Point(320, 0),
            new Point(640, 0),
            new Point(960, 0),
            new Point(1280, 0),
            new Point(1600, 0)
        });

        public SessionCreator(int index, ObservableCollection<DataGridItem> collection) : base(index, collection)
        {
        }

        public static Queue<Account> Accounts { get; } = new Queue<Account>();

        public async Task Base()
        {
            try
            {
                while ((_account = Accounts.GetNext(false)) != null)
                {
                    //if (string.IsNullOrWhiteSpace(_account.LoginId) ||
                    //    Blacklists.Dict[BlacklistType.Login].Contains(_account.LoginId))
                    //    continue;

                    try
                    {
                        await Init().ConfigureAwait(false);

                        try
                        {
                            await Connect().ConfigureAwait(false);

                            if (!await InitStream().ConfigureAwait(false))
                                continue;

                            LoginResult result;
                            if (Settings.Get<bool>("CreateNewSession"))
                            {
                                result = await CreateSession()
                                    .ConfigureAwait(false);
                            }
                            else
                            {
                                result = await CheckSession()
                                    .ConfigureAwait(false);
                            }

                            switch (result.Result)
                            {
                                case LResult.Success:
                                    await UploadAvatar()
                                        .ConfigureAwait(false);

                                    await SessionCreated()
                                        .ConfigureAwait(false);
                                    break;

                                case LResult.Captcha:
                                    await HandleRotateCaptcha(result.CaptchaVars)
                                        .ConfigureAwait(false);
                                    break;
                            }
                        }
                        finally
                        {
                            _tcpWaifu.Close();
                        }
                    }
                    catch (Exception e)
                    {
                        await ErrorLogger.WriteAsync(e).ConfigureAwait(false);
                    }
                    finally
                    {
                        await AddAcctBackToQueue().ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                await UpdateThreadStatusAsync("Work complete").ConfigureAwait(false);
            }
        }

        public static void InitSempahore()
        {
            var maxLauncherThreads = Settings.Get<int>("MaxFirefoxLauncherThreads");
            if (maxLauncherThreads <= 0)
                maxLauncherThreads = 1;

            if (_firefoxLock != null)
            {
                _firefoxLock.Dispose();
                _firefoxLock = null;
            }

            _firefoxLock = new SemaphoreSlim(maxLauncherThreads, maxLauncherThreads);
        }

        private static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);

        private async Task SessionCreated(string overrideFilename = "")
        {
            await AddBlacklistAsync(BlacklistType.Login, _account.LoginId).ConfigureAwait(false);

            await Semaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                string fileName;
                if (!string.IsNullOrWhiteSpace(overrideFilename))
                    fileName = overrideFilename;
                else
                    fileName = "kik-accounts.txt";

                using (var streamWriter = new StreamWriter(fileName, true))
                    await streamWriter.WriteLineAsync(_account.ToString());
            }
            finally
            {
                Semaphore.Release();
            }

            Interlocked.Increment(ref Stats.SuccessfulLogins);

            _account.LoggedInSuccessfully = true;

            if (overrideFilename != "kik-accounts-captchaed.txt")
                await UpdateThreadStatusAsync("Session created").ConfigureAwait(false);
            else
                await UpdateThreadStatusAsync("Session created (still captchaed)");
        }

        private async Task Init()
        {
            UpdateAccountColumn(_account.LoginId);
            _tcpWaifu = new TcpAsyncWaifu
            {
                Proxy = Collections.Proxies.GetNext().ToWebProxy()
            };
            _account.Sid = Guid.NewGuid().ToString();

            await _account.SetPasskeys().ConfigureAwait(false);

            Interlocked.Increment(ref Stats.TotalLoginAttempts);
        }

        private async Task Connect()
        {
            var s = $"Attempting connection [{_account.LoginErrors}]: ";
            await AttemptingAsync(s).ConfigureAwait(false);

            await _tcpWaifu.ConnectWithProxyAsync(Konstants.KikEndPoint, Konstants.KikPort).ConfigureAwait(false);
            await _tcpWaifu.InitSslStreamAsync(Konstants.KikEndPoint).ConfigureAwait(false);
        }

        private async Task<bool> InitStream()
        {
            var s = $"Initializing stream [{_account.LoginErrors}]: ";
            await AttemptingAsync(s).ConfigureAwait(false);

            if (Settings.Get<bool>("CreateNewSession"))
            {
                var ts = Krypto.KikTimestamp();
                var signed =
                    await
                        Task.Run(() => Krypto.KikRsaSign(_account.DeviceId, Konstants.AppVersion, ts, _account.Sid))
                            .ConfigureAwait(false);
                var deviceTsHash = Krypto.KikHmacSha1Signature(ts, _account.DeviceCanId);
                const int nVal = 10;
                var streamInitPropertyMap =
                    await
                        Packets.StreamInitPropertyMapAnonAsync(signed, deviceTsHash, ts, _account.DeviceCanId, _account.Sid, nVal)
                            .ConfigureAwait(false);
                if (streamInitPropertyMap.IsNullOrEmpty())
                    return
                        await
                            FailedAsync(s, "Failed to get stream init property map (is java tunnel running?)")
                                .ConfigureAwait(false);

                await _tcpWaifu.SendDataAsync(streamInitPropertyMap).ConfigureAwait(false);

                var response = await _tcpWaifu.ReadAllPendingDataAsStringAsync().ConfigureAwait(false);
                if (response.Contains("Not Authorized"))
                {
                    _account.InvalidPassword = true;
                    return await FailedAsync(s, "Invalid credentials").ConfigureAwait(false);
                }

                if (!response.Contains("ok"))
                    return await UnexpectedResponseAsync(s).ConfigureAwait(false);

                return true;
            }

            {
                var ts = Krypto.KikTimestamp();
                var deviceTsHash = Krypto.KikHmacSha1Signature(ts, _account.Jid + "@talk.kik.com");
                var signed = await Task.Run(() => Krypto.KikRsaSign(_account.Jid + "@talk.kik.com", Konstants.AppVersion, ts, _account.Sid)).ConfigureAwait(false);
                const int nVal = 10;
                var data = await Packets.StreamInitPropertyMapAsync($"{_account.Jid + "@talk.kik.com"}/{_account.DeviceCanId}", _account.PasskeyU, deviceTsHash, long.Parse(ts), signed, _account.Sid, nVal)
                    .TimeoutAfter(10000)
                    .ConfigureAwait(false);
                if (data.IsNullOrEmpty())
                    return await FailedAsync("Failed to calculate stream init property map").ConfigureAwait(false);

                await _tcpWaifu.SendDataAsync(data).ConfigureAwait(false);
                var response = await _tcpWaifu.ReadAllPendingDataAsStringAsync().ConfigureAwait(false);
                if (!response.Contains("ok=\"1\""))
                    return await UnexpectedResponseAsync(s).ConfigureAwait(false);

                return true;
            }
        }

        private static readonly Regex JidRegex = new Regex("<node>(.*?)</node>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex CaptchaRegex =
                new Regex("<captcha-url>(.*?)</captcha-url>",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase),
            CaptchaStcRegex = new Regex("<stc id=\"(.*?)\"><stp type=\"ca\">(.*?)</stp>",
                RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline),
            CaptchaIdRegex =
                new Regex("id=(.*?)&",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase),
            UsernameRegex = new Regex("<username>(.*?)</username>", RegexOptions.Compiled);

        private async Task<LoginResult> CheckSession()
        {
            var s = $"Checking for captcha [{_account.LoginErrors}]: ";
            await AttemptingAsync(s)
                .ConfigureAwait(false);

            var data = Packets.GetUnackedMsgs();
            await _tcpWaifu.SendDataAsync(data)
                .ConfigureAwait(false);
            var response = await _tcpWaifu.ReadAllPendingDataAsStringAsync()
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(response))
            {
                await UnexpectedResponseAsync(s)
                    .ConfigureAwait(false);
                return new LoginResult(LResult.Unknown);
            }

            if (response.Contains("kik:iq:QoS"))
                return new LoginResult(LResult.Success);

            if (response.Contains("<stc"))
            {
                await UpdateThreadStatusAsync(s + "Captchaed")
                    .ConfigureAwait(false);

                var captchaUrl = string.Empty;
                var stcPacketId = string.Empty;
                var match = CaptchaStcRegex.Match(response);
                if (match.Success)
                {
                    captchaUrl = match.Groups[2].Value;
                    stcPacketId = match.Groups[1].Value;
                }

                string captchaId;
                CaptchaIdRegex.TryGetGroup(captchaUrl, out captchaId);

                if (GeneralHelpers.AnyNullOrWhiteSpace(captchaUrl, stcPacketId, captchaId))
                {
                    await FailedAsync(s, "Failed to pares captcha vars")
                        .ConfigureAwait(false);

                    return new LoginResult(LResult.Unknown);
                }

                captchaUrl = HttpUtility.HtmlDecode(captchaUrl);

                var captchaVars = new CaptchaVars
                {
                    Url = captchaUrl,
                    Id = captchaId,
                    StcPacketId = stcPacketId
                };

                return new LoginResult(LResult.Captcha, captchaVars);
            }

            return new LoginResult(LResult.Unknown);
        }

        private async Task<LoginResult> CreateSession()
        {
            var s = $"Creating session [{_account.LoginErrors}]: ";
            await AttemptingAsync(s).ConfigureAwait(false);

            byte[] loginData;
            if (string.IsNullOrWhiteSpace(_account.LoginId))
            {
                loginData = Packets.LoginEmail(_account.Email, _account.PasskeyE, _account.DeviceId,
                   _account.AndroidDevice.CarrierCode, _account.InstallDate, _account.AndroidDevice.Manufacturer,
                   _account.AndroidDevice.OsVersion, _account.AndroidId, _account.AndroidDevice.Model);
            }
            else
            {
                loginData = Packets.Login(_account.LoginId, _account.PasskeyU, _account.DeviceId,
                    _account.AndroidDevice.CarrierCode, _account.InstallDate, _account.AndroidDevice.Manufacturer,
                    _account.AndroidDevice.OsVersion, _account.AndroidId, _account.AndroidDevice.Model);
            }

            await _tcpWaifu.SendDataAsync(loginData).ConfigureAwait(false);

            var response = await _tcpWaifu.ReadAllPendingDataAsStringAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(response)) /*|| !response.Contains("<node>")*/
            {
                await UnexpectedResponseAsync(s).ConfigureAwait(false);
                return new LoginResult(LResult.Unknown);
            }

            if (response.Contains("password-mismatch") ||
                response.Contains("<not-registered"))
            {
                _account.InvalidPassword = true;
                await FailedAsync(s, "Invalid credentials").ConfigureAwait(false);
                return new LoginResult(LResult.InvalidPassword);
            }

            if (response.Contains("<captcha-url>"))
            {
                await UpdateThreadStatusAsync(s + "Captchaed")
                    .ConfigureAwait(false);

                var captchaUrl = string.Empty;
                var captchaId = string.Empty;
                var match = CaptchaRegex.Match(response);
                if (match.Success)
                {
                    captchaUrl = match.Groups[1].Value;
                }

                match = CaptchaIdRegex.Match(captchaUrl);
                if (match.Success)
                {
                    captchaId = match.Groups[1].Value;
                }
                if (GeneralHelpers.AnyNullOrWhiteSpace(captchaUrl, captchaId))
                {
                    await FailedAsync(s, "Failed to pares captcha vars")
                        .ConfigureAwait(false);

                    return new LoginResult(LResult.Unknown);
                }

                captchaUrl = HttpUtility.HtmlDecode(captchaUrl);

                var captchaVars = new CaptchaVars
                {
                    Url = captchaUrl,
                    Id = captchaId
                };

                return new LoginResult(LResult.Captcha, captchaVars);
            }

            if (!response.Contains("<node>"))
            {
                await UnexpectedResponseAsync(s).ConfigureAwait(false);
                return new LoginResult(LResult.Unknown);
            }

            if (string.IsNullOrEmpty(_account.Jid))
            {
                string jid;
                if (!JidRegex.TryGetGroup(response, out jid))
                {
                    await FailedAsync(s, "Failed to parse jid")
                        .ConfigureAwait(false);
                    return new LoginResult(LResult.Unknown);
                }

                _account.Jid = jid;
            }

            if (string.IsNullOrWhiteSpace(_account.LoginId))
            {
                if (!UsernameRegex.TryGetGroup(response, out var username))
                {
                    await FailedAsync(s, "Failed to parse username")
                        .ConfigureAwait(false);
                    return new LoginResult(LResult.Unknown);
                }

                _account.LoginId = username.ToLower();
                await _account.SetPasskeys()
                    .ConfigureAwait(false);
            }

            return new LoginResult(LResult.Success);
        }

        private async Task UploadAvatar()
        {
            if (!Settings.Get<bool>("UploadAvatar"))
                return;

            const string s = "Uploading avatar: ";
            await AttemptingAsync(s)
                .ConfigureAwait(false);

            var imageDir = Collections.ImageDirs.GetNext();
            if (!Directory.Exists(imageDir))
            {
                await FailedAsync(s, "Specified images directory does not exist")
                    .ConfigureAwait(false);
                return;
            }

            var jpgs = GeneralHelpers.JpgsFromDir(imageDir);
            if (jpgs.Count == 0)
            {
                await FailedAsync(s, "image directory has 0 jpgs")
                    .ConfigureAwait(false);
                return;
            }

            jpgs.Shuffle();

            var pathToImage = jpgs.GetNext();
            byte[] imageData;
            using (var ms = new MemoryStream())
            {
                var newImage = GeneralHelpers.ScaleImage(Image.FromFile(pathToImage), 300, 300);
                newImage.Save(ms, ImageFormat.Jpeg);
                ms.Position = 0;
                imageData = ms.ToArray();
            }

            var cfg = new HttpWaifuConfig
            {
                UserAgent =
                    $"Kik/{Konstants.AppVersion} (Android {_account.AndroidDevice.OsVersion}) {_account.AndroidDevice.DalvikUserAgent}",
                Proxy = _tcpWaifu.Proxy
            };
            var client = new HttpWaifu(cfg);

            var request = new HttpReq(HttpMethod.Post, "https://profilepicsup.kik.com/profilepics")
            {
                AdditionalHeaders = new WebHeaderCollection
                {
                    ["x-kik-jid"] = _account.Jid + "@talk.kik.com",
                    ["x-kik-password"] = _account.PasskeyU
                },
                ContentData = imageData
            };
            var response = await client.SendRequestAsync(request)
                .ConfigureAwait(false);
            if (!response.IsOK)
                await UnexpectedResponseAsync(s)
                    .ConfigureAwait(false);
        }

        private async Task<FirefoxDriver> LaunchFirefox()
        {
            const string s = "Launching firefox: ";
            await AttemptingAsync(s)
                .ConfigureAwait(false);

            await _firefoxLock.WaitAsync()
                .ConfigureAwait(false);

            try
            {
                var profileId = Guid.NewGuid().ToString();
                var directory = $@"{AppDomain.CurrentDomain.BaseDirectory}{profileId}";

                var tmp = new FirefoxProfile(directory)
                {
                    AcceptUntrustedCertificates = true,
                    EnableNativeEvents = true,
                    DeleteAfterUse = true,
                    AssumeUntrustedCertificateIssuer = false
                };

                _driverProxy = Collections.WebBrowserProxies.GetNext().ToWebProxy() ?? _tcpWaifu.Proxy;
                var proxy = new Proxy
                {
                    HttpProxy = _driverProxy.Address.Authority,
                    SslProxy = _driverProxy.Address.Authority
                };

                tmp.SetProxyPreferences(proxy);
                tmp.SetPreference("general.useragent.override", _account.BrowserUserAgent);
                tmp.AddExtension("modify_headers.xpi");
                tmp.SetPreference("media.peerconnection.enabled", false);

                var headers = new WebHeaderCollection
                {
                    ["X-Requested-With"] = "kik.android"
                };

                SetWebDriverCustomHeaders(headers, tmp);

                FirefoxDriver ret = null;
                await Task.Run(() =>
                    {
                        try
                        {
                            ret = new FirefoxDriver(tmp);
                        }
                        catch
                        {
                            /*ignored*/
                        }
                    })
                    .ConfigureAwait(false);

                if (ret == null)
                    return null;

                ret.Manage().Window.Size = new Size(320, 568);
                ret.Manage().Window.Position = WindowPositions.GetNext();

                return ret;
            }
            finally
            {
                _firefoxLock.Release();
            }
        }

        private static void SetWebDriverCustomHeaders(NameValueCollection webHeaders, FirefoxProfile profile)
        {
            if (webHeaders == null || webHeaders.Count == 0 || profile == null)
                return;

            profile.SetPreference("modifyheaders.config.active", true);
            profile.SetPreference("modifyheaders.config.alwaysOn", true);
            profile.SetPreference("modifyheaders.start", true);
            profile.SetPreference("modifyheaders.config.openNewTab", true);
            profile.SetPreference("modifyheaders.headers.count", webHeaders.Count);

            for (var i = 0; i < webHeaders.Count; i++)
            {
                var key = webHeaders.GetKey(i);
                var values = webHeaders.GetValues(i);
                if (values == null || values.Length == 0)
                    continue;

                var value = values[0];

                profile.SetPreference($"modifyheaders.headers.action{i}", "Add");
                profile.SetPreference($"modifyheaders.headers.name{i}", key);
                profile.SetPreference($"modifyheaders.headers.value{i}", value);
                profile.SetPreference("modifyheaders.headers.enabled0", true);
            }
        }

        private async Task Ping(CancellationTokenSource cToken)
        {
            while (true)
            {
                if (cToken.IsCancellationRequested)
                    return;

                await Task.Delay(1000)
                    .ConfigureAwait(false);

                await _tcpWaifu.SendDataAsync(Packets.Ping());
                while (_tcpWaifu.Available == 0)
                    await Task.Delay(100)
                        .ConfigureAwait(false);

                var rcvd = 0;
                var expected = _tcpWaifu.Available;
                while (rcvd != expected && _tcpWaifu.Available > 0)
                {
                    var buffer = new byte[expected - rcvd];
                    var cnt = await _tcpWaifu.ReceiveDataAsync(buffer, 0, buffer.Length)
                        .ConfigureAwait(false);
                    rcvd += cnt;
                }
            }
        }

        private async Task HandleRotateCaptcha(CaptchaVars captchaVars)
        {
            if (!Settings.Get<bool>("SolveCaptchas"))
            {
                await SessionCreated("kik-accounts-captchaed.txt")
                    .ConfigureAwait(false);
                return;
            }
            using (var c = new CancellationTokenSource())
            {
                var pingTask = Ping(c);

                try
                {
                    using (_driver = await LaunchFirefox()
                        .ConfigureAwait(false))
                    {
                        if (!await LoadCaptcha(captchaVars.Url)
                            .ConfigureAwait(false))
                            return;

                        if (!await FindCaptchaTokens(captchaVars)
                            .ConfigureAwait(false))
                            return;

                        if (Settings.Get<bool>("Use2Captcha"))
                        {
                            if (!await Solve2Captcha()
                                .ConfigureAwait(false))
                                return;
                        }
                        else
                        {
                            if (!await WaitForCaptchaToBeSolved()
                                .ConfigureAwait(false))
                                return;
                        }
                    }
                }
                catch (Exception e)
                {
                    await ErrorLogger.WriteAsync(e)
                        .ConfigureAwait(false);
                }

                if (!await VerifyCaptcha(captchaVars)
                    .ConfigureAwait(false))
                    return;

                c.Cancel();
                await pingTask.ConfigureAwait(false);
            }

            if (Settings.Get<bool>("CreateNewSession"))
            {
                await UpdateThreadStatusAsync("Logging in w/ captcha: ...")
                    .ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(_account.LoginId))
                {
                    await
                        _tcpWaifu.SendDataAsync(Packets.LoginCaptchaEmail(_account.Email, _account.PasskeyE, _account.DeviceId,
                            _account.AndroidDevice.CarrierCode, _account.InstallDate,
                            _account.AndroidDevice.Manufacturer,
                            _account.AndroidDevice.SdkApiVersion, _account.AndroidId, _account.AndroidDevice.Model,
                            captchaVars.Solution))
           .ConfigureAwait(false);
                }
                else
                {
                    await
                        _tcpWaifu.SendDataAsync(Packets.LoginCaptcha(_account.LoginId, _account.PasskeyU, _account.DeviceId,
                                _account.AndroidDevice.CarrierCode, _account.InstallDate,
                                _account.AndroidDevice.Manufacturer,
                                _account.AndroidDevice.SdkApiVersion, _account.AndroidId, _account.AndroidDevice.Model,
                                captchaVars.Solution))
                            .ConfigureAwait(false);
                }

                var response = await _tcpWaifu.ReadAllPendingDataAsStringAsync()
                    .ConfigureAwait(false);
                if (response.Contains("password-mismatch"))
                    _account.InvalidPassword = true;

                if (response.Contains("<captcha-url>") ||
                    response.Contains("password-mismatch") ||
                    !response.Contains("<node>"))
                {
                    await FailedAsync("Logging in w/ captcha: ...", "Unexpected response")
                        .ConfigureAwait(false);

                    return;
                }

                if (string.IsNullOrEmpty(_account.Jid))
                {
                    string jid;
                    if (!JidRegex.TryGetGroup(response, out jid))
                    {
                        await FailedAsync("Logging in w/ captcha: ...", "Failed to parse jid")
                            .ConfigureAwait(false);
                        return;
                    }

                    _account.Jid = jid;
                }

                if (string.IsNullOrWhiteSpace(_account.LoginId))
                {
                    if (!UsernameRegex.TryGetGroup(response, out var username))
                    {
                        await FailedAsync("Logging in w/ captcha: ...", "Failed to parse username")
                            .ConfigureAwait(false);
                        return;
                    }

                    _account.LoginId = username;
                    await _account.SetPasskeys()
                        .ConfigureAwait(false);
                }
            }
            else
            {
                await UpdateThreadStatusAsync("Sending captcha solution: ...")
                    .ConfigureAwait(false);

                var stcSolData = Packets.StcSolution(captchaVars.StcPacketId, captchaVars.Solution);
                await _tcpWaifu.SendDataAsync(stcSolData)
                    .ConfigureAwait(false);
                var response = await _tcpWaifu.ReadAllPendingDataAsStringAsync()
                    .ConfigureAwait(false);
                if (!response.Contains("kik:iq:QoS"))
                {
                    await FailedAsync("unexpected response after sending solution")
                        .ConfigureAwait(false);
                    return;
                }
            }

            await UploadAvatar()
                .ConfigureAwait(false);

            await SessionCreated()
                .ConfigureAwait(false);
        }

        private async Task<IWebElement> GetCanvasObject()
        {
            const string s = "Getting canvas obj: ";
            await AttemptingAsync(s).ConfigureAwait(false);

            IWebElement canvas = null;
            var foundCanvas = await Task.Run(() =>
            {
                try
                {
                    if (!SwitchToRelevantIFrame())
                        return true;

                    canvas = _driver.FindElement(By.Id("FunCAPTCHA"), 60);
                    return true;
                }
                catch
                {
                    return false;
                }
            }).ConfigureAwait(false);

            if (!foundCanvas)
            {
                await FailedAsync(s).ConfigureAwait(false);
                return null;
            }

            ClickInCanvas(canvas, 100, 240);
            return canvas;
        }

        private void ClickLeftArrow(IWebElement canvas)
        {
            ClickInCanvas(canvas, 45, 143);
        }

        private void ClickRightArrow(IWebElement canvas)
        {
            ClickInCanvas(canvas, 255, 145);
        }

        private static Bitmap GetElementScreenShort(IWebDriver driver, IWebElement element)
        {
            try
            {
                var sc = ((ITakesScreenshot)driver).GetScreenshot();
                var img = Image.FromStream(new MemoryStream(sc.AsByteArray)) as Bitmap;
                if (img == null)
                    return null;

                var clone1 = img.Clone(new Rectangle(new Point(0, 50), element.Size), img.PixelFormat);

#if DEBUG
                var data = GeneralHelpers.ImageToBytes(clone1);
                Task.Run(async () =>
                {
                    await WriteBytes("fullss.jpg", data).ConfigureAwait(false);
                }).Wait();
#endif

                return clone1.Clone(new Rectangle(90, 100, 125, 125), clone1.PixelFormat);
            }
            catch
            {
                return null;
            }
        }

        private void ClickInCanvas(IWebElement canvas, int x, int y)
        {
            var act = new Actions(_driver);
            act.MoveToElement(canvas, x, y);
            act.Click();
            act.Perform();
        }

        private bool SwitchToRelevantIFrame()
        {
            var captchaFrame = _driver.SwitchTo()
                .Frame("fc-iframe-wrap")
                .SwitchTo()
                .Frame("CaptchaFrame");
            return captchaFrame != null;
        }

        private async Task<bool> Solve2Captcha()
        {
            var canvas = await GetCanvasObject().ConfigureAwait(false);
            if (canvas == null)
                return false;

            await Task.Delay(5000)
                .ConfigureAwait(false);

            const string s = "Waiting for solution: ";

            var maxAttempts = Settings.Get<int>("MaxCaptchaSolveAttempts");
            for (var j = 0; j < maxAttempts; j++)
            {
                await AttemptingAsync(s)
                    .ConfigureAwait(false);

                if (await Task.Run(() => CaptchaSuccess()))
                    return true;

                var bmp = await Task.Run(() => GetElementScreenShort(_driver, canvas)).ConfigureAwait(false);
                var imageData = GeneralHelpers.ImageToBytes(bmp);
#if DEBUG
                await WriteBytes("captcha.jpg", imageData)
                    .ConfigureAwait(false);
#endif

                var solver = new TwoCaptchaWaifu(Settings.Get<string>("2CaptchaAPIKey"));
                var solution = await solver.SolveRotateCaptchaAsync(imageData, Settings.Get<int>("SolveCaptchaTimeout"))
                    .ConfigureAwait(false);
                if (solution == 0)
                {
                    if (await Task.Run(() => CaptchaSuccess()))
                        return true;

                    if (Settings.Get<bool>("RestartWorkerOn2CaptchaError"))
                        return await FailedAsync(s, "2captcha returned 0")
                            .ConfigureAwait(false);

                    await UpdateThreadStatusAsync("2Captcha returned 0")
                        .ConfigureAwait(false);
                    continue;
                }

                if (await Task.Run(() => CaptchaSuccess()))
                    return true;

                var rotations = solution / 40;
                if (rotations > 0)
                {
                    for (var i = 0; i < rotations; i++)
                    {
                        ClickLeftArrow(canvas);
                        await Task.Delay(1300).ConfigureAwait(false);
                    }
                }
                else
                {
                    var max = rotations * -1;
                    for (var i = 0; i < max; i++)
                    {
                        ClickRightArrow(canvas);
                        await Task.Delay(1300).ConfigureAwait(false);
                    }
                }

                ClickInCanvas(canvas, 100, 240);

                if (LostFirefoxWindow())
                    return await FailedAsync(s, "Lost firefox window").
                        ConfigureAwait(false);

                if (await Task.Run(() => CaptchaSuccess()))
                    return true;
            }

            return await FailedAsync(s)
                .ConfigureAwait(false);
        }

        private bool CaptchaSuccess()
        {
            _driver.SwitchTo().DefaultContent();

            if (_driver.FindElement(By.XPath("//div[@class='app-dialog']"), 5) == null)
            {
                SwitchToRelevantIFrame();
                Thread.Sleep(1000);
                return false;
            }

            return true;
        }

        private async Task<bool> LoadCaptcha(string captchaUrl)
        {
            const string s = "Loading captcha: ";
            await AttemptingAsync(s).ConfigureAwait(false);

            await Task.Run(() =>
            {
                try
                {
                    _driver.Navigate().GoToUrl(captchaUrl);
                }
                catch
                {
                    /*ignored*/
                }
            });

            await Task.Delay(5000).ConfigureAwait(false);

            return _driver.PageSource.Contains("<title>Verify Account");
        }

        private static readonly Regex CaptchaTokensRegex =
            new Regex("<div class=\"captcha-challenge-container\">.*?token=(.*?)&.*?;(.*?)&",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private async Task<bool> FindCaptchaTokens(CaptchaVars captchaVars)
        {
            const string s = "Finding captcha tokens: ";
            await AttemptingAsync(s).ConfigureAwait(false);

            var id = string.Empty;
            var id2 = string.Empty;

            var src = _driver.PageSource;
            var match = CaptchaTokensRegex.Match(src);
            if (match.Success)
            {
                id = match.Groups[1].Value;
                id2 = match.Groups[2].Value;
            }

            if (GeneralHelpers.AnyNullOrWhiteSpace(id, id2))
                return await FailedAsync(s).ConfigureAwait(false);

            captchaVars.Response = $"{id}|{id2}";
            return true;
        }

        private async Task<bool> WaitForCaptchaToBeSolved()
        {
            const string s = "Waiting for captcha to be solved: ";
            await AttemptingAsync(s).ConfigureAwait(false);

            if (!await Task.Run(async () =>
            {
                try
                {
                    for (var i = 0; i < 30; i++)
                    {
                        if (LostFirefoxWindow())
                            return await FailedAsync(s, "Lost firefox window").ConfigureAwait(false);

                        if (_driver.FindElement(By.XPath("//div[@class='app-dialog']"), 10) == null)
                        {
                            await Task.Delay(1000).ConfigureAwait(false);
                            continue;
                        }

                        return true;
                    }

                    return await FailedAsync(s).ConfigureAwait(false);
                }
                catch
                {
                    return await FailedAsync(s, "Lost firefox window").ConfigureAwait(false);
                }
            }))
                return false;

            return true;
        }

        private async Task<bool> VerifyCaptcha(CaptchaVars captchaVars)
        {
            const string s = "Verifying captcha was solved correctly: ";
            await AttemptingAsync(s).ConfigureAwait(false);

            var cfg = new HttpWaifuConfig
            {
                UserAgent =
                    $"Kik/{Konstants.AppVersion} (Android {_account.AndroidDevice.OsVersion}) {_account.AndroidDevice.DalvikUserAgent}"
            };
            var client = new HttpWaifu(cfg);

            for (var i = 0; i < 3; i++)
            {
                var proxy = Collections.SubmitCaptchaProxies.GetNext().ToWebProxy();
                if (proxy == null)
                    proxy = _driverProxy;
                client.Config.Proxy = proxy;

                var request = new HttpReq(HttpMethod.Post, "https://captcha.kik.com/verify")
                {
                    Accept = "text/plain, */*; q=0.01",
                    Origin = "https://captcha.kik.com",
                    Referer = captchaVars.Url,
                    ContentType = "application/json",
                    OverrideUserAgent = _account.BrowserUserAgent,
                    Timeout = 10000,
                    AcceptEncoding = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    AdditionalHeaders = new WebHeaderCollection
                    {
                        ["X-Requested-With"] = "XMLHttpRequest"
                    },
                    ContentBody = $"{{\"id\":\"{captchaVars.Id}\",\"response\":\"{captchaVars.Response}\"}}"
                };
                var response = await client.SendRequestAsync(request).ConfigureAwait(false);
                if (response.IsOK && !string.IsNullOrWhiteSpace(response.ContentBody))
                {
                    captchaVars.Solution = response.ContentBody;
                    return true;
                }

                await Task.Delay(1000).ConfigureAwait(false);
            }

            return await FailedAsync(s).ConfigureAwait(false);
        }

        private bool LostFirefoxWindow()
        {
            if (_driver == null || _driver.WindowHandles == null || _driver.WindowHandles.Count == 0)
                return true;

            return false;
        }

        private async Task AddAcctBackToQueue()
        {
            if (_account == null)
                return;

            if (_account.LoggedInSuccessfully)
                return;

            if (_account.LoginErrors++ >= Settings.Get<int>("MaxLoginErrors") || _account.InvalidPassword)
            {
                await AppendInvalidAccount(_account.ToString()).ConfigureAwait(false);
                await AddBlacklistAsync(BlacklistType.Login, _account.LoginId).ConfigureAwait(false);
                return;
            }

            await UpdateThreadStatusAsync($"Adding {_account.LoginId} back to queue. Login errors = {_account.LoginErrors}").ConfigureAwait(false);

            for (var i = 0; i < 5; i++)
            {
                var device = new AndroidDevice(Collections.AndroidDevices.GetNext());
                if (!device.IsValid)
                    continue;

                _account.AndroidDevice = device;
                break;
            }

            lock (Accounts)
                Accounts.Enqueue(_account);
        }

        private static readonly SemaphoreSlim InvalidAcctLock = new SemaphoreSlim(1, 1);

        private static async Task AppendInvalidAccount(string str)
        {
            await InvalidAcctLock.WaitAsync().ConfigureAwait(false);

            try
            {
                using (var sw = new StreamWriter("invalid-accounts.txt", true))
                    await sw.WriteLineAsync(str).ConfigureAwait(false);
            }
            finally
            {
                InvalidAcctLock.Release();
            }
        }

        private static readonly SemaphoreSlim Writelock2 = new SemaphoreSlim(1, 1);

        private static async Task WriteBytes(string fileName, byte[] data)
        {
            await Writelock2.WaitAsync()
                .ConfigureAwait(false);

            try
            {
                using (var sw = new FileStream(fileName, FileMode.OpenOrCreate))
                    await sw.WriteAsync(data, 0, data.Length)
                        .ConfigureAwait(false);
            }
            finally
            {
                Writelock2.Release();
            }
        }
    }
}