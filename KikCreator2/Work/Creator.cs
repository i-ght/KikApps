using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using KikCreator2.Declarations;
using System.Threading;
using DankLibWaifuz.SettingsWaifu;
using DankLibWaifuz;
using DankLibWaifuz.CollectionsWaifu;
using DankLibWaifuz.TcpWaifu;
using DankLibWaifuz.HttpWaifu;
using KikWaifu;
using System.Net;
using DankLibWaifuz.Etc;
using KikCreator2.Work.Json;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium;
using System.Collections.Generic;
using System.Web;
using System.Xml;
using OpenQA.Selenium.Interactions;
using DankLibWaifuz.CaptchaWaifu;

namespace KikCreator2.Work
{
    internal class Creator : Mode
    {
        private readonly SemaphoreSlim _sslStreamLock = new SemaphoreSlim(1, 1);

        private Account _account;
        private TcpAsyncWaifu _tcpWaifu;
        private HttpWaifu _httpWaifu;
        private HttpWaifu _metricsClient;
        private WebProxy _proxy;
        private FirefoxDriver _driver;
        private System.Timers.Timer _pingTimer;
        private CancellationTokenSource _cTokenSrc;

        private static SemaphoreSlim _firefoxLock;

        private static JsonSerializerSettings _jsonSerializserSettings;
        private static JsonSerializerSettings JsonSerializerSettings
        {
            get
            {
                return _jsonSerializserSettings ?? (_jsonSerializserSettings = new JsonSerializerSettings()
                {
                    Error = (o, args) =>
                    {
                        ErrorLogger.WriteErrorLog(args.ErrorContext.Error);
                        args.ErrorContext.Handled = true;
                    }
                });
            }
        }

        public Creator(int index, ObservableCollection<DataGridItem> collection) : base(index, collection)
        {
        }

        public static void InitSempahore()
        {
            var maxLauncherThreads = Settings.Get<int>("MaxFirefoxLauncherThreads");

            if (_firefoxLock != null)
            {
                _firefoxLock.Dispose();
                _firefoxLock = null;
            }

            _firefoxLock = new SemaphoreSlim(maxLauncherThreads, maxLauncherThreads);
        }

        public async Task Base()
        {
            while (Stats.Created < Settings.Get<int>("MaxCreates"))
            {
                try
                {
                    if (!await InitAccount().ConfigureAwait(false))
                        continue;

                    try
                    {
                        await MetricsPreReg().ConfigureAwait(false);

                        await ConnectKik().ConfigureAwait(false);

                        if (!await InitStream().ConfigureAwait(false))
                            continue;

                        if (!await FindUniqueUsername().ConfigureAwait(false))
                            continue;

                        var registerResult = await Register().ConfigureAwait(false);
                        await HandleRegisterResult(registerResult).ConfigureAwait(false);
                    }
                    finally
                    {
                        _tcpWaifu.Close();
                    }
                }
                catch (Exception e)
                {
                    await UpdateThreadStatusAsync("Exception occured", 3000).ConfigureAwait(false);
                    await ErrorLogger.WriteAsync(e).ConfigureAwait(false);
                }
            }
        }

        private async Task ConnectKik()
        {
            const string s = "Connecting to kik chat server: ";
            await AttemptingAsync(s).ConfigureAwait(false);

            await _tcpWaifu.ConnectWithProxyAsync(Konstants.KikEndPoint, Konstants.KikPort).ConfigureAwait(false);
            await _tcpWaifu.InitSslStreamAsync("talk.kik.com").ConfigureAwait(false);
        }

        private async Task<bool> InitStream()
        {
            const string s = "Initializing stream: ";
            await AttemptingAsync(s).ConfigureAwait(false);

            var ts = Krypto.KikTimestamp();
            var signed = await Task.Run(() => Krypto.KikRsaSign(_account.DeviceId, Konstants.AppVersion, ts, _account.Sid)).ConfigureAwait(false);
            var deviceTsHash = Krypto.KikHmacSha1Signature(ts, _account.DeviceCanId);
            const int nFirstRun = 1;
            var streamInitPropertyMap = await Packets.StreamInitPropertyMapAnonAsync(signed, deviceTsHash, ts, _account.DeviceCanId, _account.Sid, nFirstRun);
            if (streamInitPropertyMap.IsNullOrEmpty())
                return await FailedAsync(s, "Failed to calculate stream init property map").ConfigureAwait(false);

            await _tcpWaifu.SendDataAsync(streamInitPropertyMap).ConfigureAwait(false);

            var response = await _tcpWaifu.ReadAllPendingDataAsStringAsync().ConfigureAwait(false);
            if (!response.Contains("ok"))
                return await UnexpectedResponseAsync(s).ConfigureAwait(false);

            return true;
        }

        private async Task DelayBefore(string doingWhat, string settingName)
        {
            var seconds = Settings.GetRandom(settingName);

            for (var i = seconds; i > 0; i--)
            {
                var remaining = i > 1 ? $"{i} seconds remain" : "1 second remains";
                await UpdateThreadStatusAsync($"Delaying before {doingWhat}: {remaining}").ConfigureAwait(false);
            }
        }

        private async Task<bool> FindUniqueUsername()
        {
            await DelayBefore("finding unique username", "DelayBeforeCheckingUsername").ConfigureAwait(false);

            const string s = "Finding unique username: ";
            await AttemptingAsync(s).ConfigureAwait(false);

            for (var i = 0; i < 3; i++)
            {
                var data = Packets.CheckUsername(_account.LoginId);
                await _tcpWaifu.SendDataAsync(data).ConfigureAwait(false);

                var response = await _tcpWaifu.ReadAllPendingDataAsStringAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(response))
                    return await UnexpectedResponseAsync(s).ConfigureAwait(false);

                if (response.Contains("username is-unique=\"true\""))
                    return true;

                _account.FailedUsernameLookUpAttempts++;
                _account.LoginId += Random.Next(9);
                UpdateAccountColumn(_account.LoginId);
            }

            return await FailedAsync(s).ConfigureAwait(false);
        }

        private static readonly Regex CaptchaUrlRegex = new Regex("<captcha-url>(.*?)</captcha-url>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CaptchaIdRegex = new Regex("id=3-(.*?)&", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private async Task ReceiveLoop()
        {
            try
            {
                using (var xmlReader = XmlReader.Create(_tcpWaifu.NetworkStream, new XmlReaderSettings
                {
                    Async = true,
                    IgnoreProcessingInstructions = true,
                    IgnoreComments = true,
                    IgnoreWhitespace = true,
                    ConformanceLevel = ConformanceLevel.Fragment
                }))
                {
                    while (await xmlReader.ReadAsync().ConfigureAwait(false))
                    {
                        if (_cTokenSrc.IsCancellationRequested)
                            return;

                        if (xmlReader.NodeType != XmlNodeType.Element)
                            continue;

                        if (xmlReader.Name == "pong")
                        {
#if DEBUG
                            Console.WriteLine(await xmlReader.ReadOuterXmlAsync().ConfigureAwait(false));
#endif
                            continue;
                        }

                        using (var subtree = await Task.Run(() => xmlReader.ReadSubtree()).ConfigureAwait(false))
                        {
                            while (await subtree.ReadAsync().ConfigureAwait(false))
                            {
                                var xml = await subtree.ReadOuterXmlAsync().ConfigureAwait(false);
                                if (!xml.Contains("jabber:iq:register"))
                                    continue;

                                if (!await AccountWasCreated(xml).ConfigureAwait(false))
                                    await UpdateThreadStatusAsync("Registration failed").ConfigureAwait(false);
 
                                return;
                            }

                        }
                    }
                }
            }
            catch (Exception e)
            {
                await ErrorLogger.WriteAsync(e).ConfigureAwait(false);
            }
        }

        private async Task<RegisterResult> Register()
        {
            await DelayBefore("attempting registration", "DelayBeforeRegistration").ConfigureAwait(false);

            const string s = "Registering: ";
            await AttemptingAsync(s).ConfigureAwait(false);

            await _account.SetPasskeys().ConfigureAwait(false);

            var data = Packets.Register(_account.Email, _account.PasskeyE, _account.PasskeyU,
                _account.DeviceId, _account.LoginId, _account.FirstName, _account.LastName, 
                _account.Birthday, _account.AndroidId, _account.AndroidDevice.CarrierCode,
                _account.AndroidDevice.Manufacturer, _account.AndroidDevice.SdkApiVersion, 
                _account.AndroidDevice.Model);

            await _tcpWaifu.SendDataAsync(data).ConfigureAwait(false);

            var response = await _tcpWaifu.ReadAllPendingDataAsStringAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(response))
            {
                await UnexpectedResponseAsync(s).ConfigureAwait(false);
                return new RegisterResult(RegisterStatusCode.UnexpectedResponse);
            }

#if DEBUG
            Console.WriteLine($@"Register response: {response}");
#endif

            if (await AccountWasCreated(response).ConfigureAwait(false))
                return new RegisterResult(RegisterStatusCode.RegisteredSuccessfully);

            string captchaUrl;
            if (!CaptchaUrlRegex.TryGetGroup(response, out captchaUrl))
            {
                await FailedAsync(s, "Failed to parse captcha url from response").ConfigureAwait(false);
                return new RegisterResult(RegisterStatusCode.CouldntParseCaptchaUrl);
            }

            captchaUrl = HttpUtility.HtmlDecode(captchaUrl);

            string captchaId;
            if (!CaptchaIdRegex.TryGetGroup(response, out captchaId))
            {
                await FailedAsync(s, "Failed to parse captcha id from response").ConfigureAwait(false);
                return new RegisterResult(RegisterStatusCode.CouldntParseCaptchaId);
            }

            captchaId = $"3-{captchaId}";
            return new RegisterResult(RegisterStatusCode.Captchad, captchaUrl, captchaId);
        }

        private async Task<bool> RegisterCaptcha(string solution)
        {
            const string s = "Registering with captcha: ";
            await AttemptingAsync(s).ConfigureAwait(false);

            var data = Packets.RegisterCaptcha(_account.Email, _account.PasskeyE, _account.PasskeyU,
                        _account.DeviceId, _account.LoginId, _account.FirstName, _account.LastName,
                        _account.Birthday, _account.AndroidId, _account.AndroidDevice.CarrierCode,
                        _account.AndroidDevice.Manufacturer, _account.AndroidDevice.SdkApiVersion,
                        _account.AndroidDevice.Model, solution);
            await SendData(data).ConfigureAwait(false);

            return true;
        }

        private async Task HandleRegisterResult(RegisterResult result)
        {
            if (result.StatusCode == RegisterStatusCode.RegisteredSuccessfully || result.StatusCode != RegisterStatusCode.Captchad)
                return;

            await HandleCaptcha(result).ConfigureAwait(false);
        }

        private async Task HandleCaptcha(RegisterResult result)
        {
            if (_pingTimer != null)
                _pingTimer.Dispose();

            _pingTimer = new System.Timers.Timer(1000.0);
            _pingTimer.Elapsed += PingCallback;
            _pingTimer.Start();

            var cancelAfter = Settings.Get<int>("CancelAfter") * 1000;
            _cTokenSrc = new CancellationTokenSource(cancelAfter);
            var r =_cTokenSrc.Token.Register(() => _tcpWaifu.Close());

            try
            {
                var receive = ReceiveLoop();

                try
                {
                    await LaunchFirefox().ConfigureAwait(false);

                    if (!await LoadCaptcha(result.CaptchaVars.Url).ConfigureAwait(false))
                    {
                        _cTokenSrc.Cancel();
                        return;
                    }

                    if (!await FindCaptchaTokens(result).ConfigureAwait(false))
                    {
                        _cTokenSrc.Cancel();
                        return;
                    }

                    if (!Settings.Get<bool>("Use2Captcha"))
                    {
                        if (!await WaitForCaptchaToBeSolved().ConfigureAwait(false))
                        {
                            _cTokenSrc.Cancel();
                            return;
                        }
                    }
                    else
                    {
                        if (!await HandleCaptcha().ConfigureAwait(false))
                        {
                            _cTokenSrc.Cancel();
                            return;
                        }
                    }
  
                }
                finally
                {
                    await DisposeDriver().ConfigureAwait(false);
                }

                if (!await VerifyCaptcha(result.CaptchaVars).ConfigureAwait(false))
                {
                    _cTokenSrc.Cancel();
                    return;
                }

                if (!await RegisterCaptcha(result.CaptchaVars.Solution).ConfigureAwait(false))
                {
                    _cTokenSrc.Cancel();
                    return;
                }

                await receive.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _cTokenSrc.Cancel();
                await ErrorLogger.WriteAsync(e).ConfigureAwait(false);
                await UpdateThreadStatusAsync("Exception occured").ConfigureAwait(false);
            }
            finally
            {
                r.Dispose();
                _cTokenSrc.Dispose();
                _pingTimer.Stop();
                _pingTimer.Dispose();
                _pingTimer = null;
            }
        }

        private async Task<bool> HandleCaptcha()
        {
            var canvas = await GetCanvasObject().ConfigureAwait(false);
            if (canvas == null)
                return false;

            const string s = "Handling captcha: ";
            await AttemptingAsync(s)
                .ConfigureAwait(false);

            await Task.Delay(5000)
                .ConfigureAwait(false);

            var maxAttempts = Settings.Get<int>("MaxCaptchaSolveAttempts");
            for (var j = 0; j < maxAttempts; j++)
            {
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
                    return await FailedAsync(s, "0 is invalid solution")
                        .ConfigureAwait(false);

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

                _driver.SwitchTo().DefaultContent();

                if (LostFirefoxWindow())
                    return await FailedAsync(s, "Lost firefox window").
                        ConfigureAwait(false);

                if (await Task.Run(() =>
                {
                    if (_driver.FindElement(By.XPath("//div[@class='app-dialog']"), 5) == null)
                    {
                        SwitchToRelevantIFrame();
                        Thread.Sleep(1000);
                        return false;
                    }

                    return true;
                }))
                    return true;
                //var ret = await Task.Run(async () =>
                //{
                //    try
                //    {
                //        for (var i = 0; i < 4; i++)
                //        {
                //            if (LostFirefoxWindow())
                //                return await FailedAsync(s, "Lost firefox window").ConfigureAwait(false);

                //            if (_driver.FindElement(By.XPath("//div[@class='app-dialog']"), 5) == null)
                //            {
                //                await Task.Delay(1000).ConfigureAwait(false);
                //                continue;
                //            }

                //            return true;
                //        }

                //        return await FailedAsync(s).ConfigureAwait(false);
                //    }
                //    catch
                //    {
                //        return await FailedAsync(s, "Lost firefox window").ConfigureAwait(false);
                //    }
                //});
                //return ret;
            }

            return await FailedAsync(s)
                .ConfigureAwait(false);

            //            var bmp = await Task.Run(() => GetElementScreenShort(_driver, canvas)).ConfigureAwait(false);
            //            var imageData = GeneralHelpers.ImageToBytes(bmp);
            //#if DEBUG
            //             await WriteBytes("captcha.jpg", imageData)
            //                .ConfigureAwait(false);
            //#endif

            //            var solver = new TwoCaptchaWaifu(Settings.Get<string>("2CaptchaAPIKey"));
            //            var solution = await solver.SolveRotateCaptchaAsync(imageData)
            //                .ConfigureAwait(false);
            //            if (solution == 0)
            //                return await FailedAsync(s, "0 is invalid solution")
            //                    .ConfigureAwait(false);

            //            var rotations = solution / 40;
            //            if (rotations > 0)
            //            {
            //                for (var i = 0; i < rotations; i++)
            //                {
            //                    ClickLeftArrow(canvas);
            //                    await Task.Delay(1300).ConfigureAwait(false);
            //                }
            //            }
            //            else
            //            {
            //                var max = rotations * -1;
            //                for (var i = 0; i < max; i++)
            //                {
            //                    ClickRightArrow(canvas);
            //                    await Task.Delay(1300).ConfigureAwait(false);
            //                }
            //            }

            //            ClickInCanvas(canvas, 100, 240);

            //            _driver.SwitchTo().DefaultContent();

            //            var ret = await Task.Run(async () =>
            //            {
            //                try
            //                {
            //                    for (var i = 0; i < 4; i++)
            //                    {
            //                        if (LostFirefoxWindow())
            //                            return await FailedAsync(s, "Lost firefox window").ConfigureAwait(false);

            //                        if (_driver.FindElement(By.XPath("//div[@class='app-dialog']"), 5) == null)
            //                        {
            //                            await Task.Delay(1000).ConfigureAwait(false);
            //                            continue;
            //                        }

            //                        return true;
            //                    }

            //                    return await FailedAsync(s).ConfigureAwait(false);
            //                }
            //                catch
            //                {
            //                    return await FailedAsync(s, "Lost firefox window").ConfigureAwait(false);
            //                }
            //            });
            //            return ret;
        }

        private void ClickLeftArrow(IWebElement canvas)
        {
            ClickInCanvas(canvas, 45, 143);
        }

        private void ClickRightArrow(IWebElement canvas)
        {
            ClickInCanvas(canvas, 255, 145);
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
            catch { return null; }
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
                catch { return false; }
            }).ConfigureAwait(false);

            if (!foundCanvas)
            {
                await FailedAsync(s).ConfigureAwait(false);
                return null;
            }

            ClickInCanvas(canvas, 100, 240);
            return canvas;
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

        private async void PingCallback(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                if (!_pingTimer.Enabled)
                    return;

                if ((_pingTimer.Interval = _pingTimer.Interval * 2) >= 8000.0)
                    _pingTimer.Interval = 8000.0;

                await SendData(Packets.Ping()).ConfigureAwait(false);
#if DEBUG
                Console.WriteLine(@"<ping/>");
#endif
            }
            catch { /*ignored*/ }
        }

        private async Task SendData(byte[] data)
        {
            await _sslStreamLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await _tcpWaifu.SendDataAsync(data).ConfigureAwait(false);
            }
            finally
            {
                _sslStreamLock.Release();
            }
        }


        public static Queue<Point> WindowPositions { get; set; }

        private async Task LaunchFirefox()
        {
            const string s = "Launching firefox: ";
            await AttemptingAsync(s).ConfigureAwait(false);

            await _firefoxLock.WaitAsync().ConfigureAwait(false);

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


                var webProxy = Collections.WebBrowserProxies.GetNext().ToWebProxy() ?? _proxy;
                var proxy = new Proxy
                {
                    HttpProxy = webProxy.Address.Authority,
                    SslProxy = webProxy.Address.Authority
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

                await Task.Run(() => _driver = new FirefoxDriver(tmp)).ConfigureAwait(false);
                _driver.Manage().Window.Size = new Size(320, 568);
                _driver.Manage().Window.Position = WindowPositions.GetNext();
            }
            finally
            {
                _firefoxLock.Release();
            }
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
                catch { /*ignored*/ }
            });

            await Task.Delay(5000).ConfigureAwait(false);

            return _driver.PageSource.Contains("<title>Verify Account");
        }

        private static readonly Regex CaptchaTokensRegex = new Regex("<div class=\"captcha-challenge-container\">.*?token=(.*?)&.*?;(.*?)&",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private async Task<bool> FindCaptchaTokens(RegisterResult result)
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

            result.CaptchaVars.Response = $"{id}|{id2}";
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

            var oldProxy = _httpWaifu.Config.Proxy;
            try
            {
                for (var i = 0; i < 3; i++)
                {
                    _httpWaifu.Config.Proxy = Collections.SubmitCaptchaProxies.GetNext().ToWebProxy();

                    var request = new HttpReq(HttpMethod.Post, "https://captcha.kik.com/verify")
                    {
                        Accept = "text/plain, */*; q=0.01",
                        Origin = "https://captcha.kik.com",
                        Referer = captchaVars.Url,
                        ContentType = "application/json",
                        OverrideUserAgent = _account.BrowserUserAgent,
                        AcceptEncoding = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                        AdditionalHeaders = new WebHeaderCollection
                        {
                            ["X-Requested-With"] = "XMLHttpRequest"
                        },
                        ContentBody = $"{{\"id\":\"{captchaVars.Id}\",\"response\":\"{captchaVars.Response}\"}}"
                    };
                    var response = await _httpWaifu.SendRequestAsync(request).ConfigureAwait(false);
                    if (response.IsOK && !string.IsNullOrWhiteSpace(response.ContentBody))
                    {
                        captchaVars.Solution = response.ContentBody;
                        return true;
                    }

                    await Task.Delay(1000).ConfigureAwait(false);
                }

                return await FailedAsync(s).ConfigureAwait(false);
            }
            finally
            {
                _httpWaifu.Config.Proxy = oldProxy;
            }
        }

        private bool LostFirefoxWindow()
        {
            if (_driver == null || _driver.WindowHandles == null || _driver.WindowHandles.Count == 0)
                return true;

            return false;
        }

        private async Task DisposeDriver()
        {
            await Task.Run(() =>
            {
                try
                {
                    _driver.Quit();
                    _driver = null;
                }
                catch { /*ignored*/ }
               
            });
        }

        private void SetWebDriverCustomHeaders(WebHeaderCollection webHeaders, FirefoxProfile profile)
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

        private static readonly Regex
            JidRegex = new Regex("<node>(.*?)</node>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private async Task<bool> AccountWasCreated(string data)
        {
            string jid;
            if (JidRegex.TryGetGroup(data, out jid))
            {
                _account.Jid = jid;
                var isAvatarUploaded = await UploadAvatar().ConfigureAwait(false);
                await AccountCreated(isAvatarUploaded).ConfigureAwait(false);
                return true;
            }

            return false;
        }

        private static readonly SemaphoreSlim Writelock = new SemaphoreSlim(1, 1);
        private async Task AccountCreated(bool isAvatarUploaded)
        {
            string fileName;
            if (isAvatarUploaded)
                fileName = $"{Assembly.GetEntryAssembly().GetName().Name.Replace(" ", "_").ToLower()}-accts_created.txt";
            else
                fileName = $"{Assembly.GetEntryAssembly().GetName().Name.Replace(" ", "_").ToLower()}-accts_created_noavatar.txt";

            Interlocked.Increment(ref Stats.Created);

            await Writelock.WaitAsync().ConfigureAwait(false);
            try
            {
                using (var sw = new StreamWriter(fileName, true))
                    await sw.WriteLineAsync(_account.ToString()).ConfigureAwait(false);

                using (var sw = new StreamWriter("good_devices.txt", true))
                    await sw.WriteLineAsync(_account.AndroidDevice.ToString()).ConfigureAwait(false);
            }
            finally
            {
                Writelock.Release();
            }

            await UpdateThreadStatusAsync("Account created").ConfigureAwait(false);
        }

        private async Task<bool> UploadAvatar()
        {
            const string s = "Uploading avatar: ";
            await AttemptingAsync(s).ConfigureAwait(false);

            _account.Images.Shuffle();
            var pathToAvatarImage = _account.Images.GetNext(false);
            if (string.IsNullOrWhiteSpace(pathToAvatarImage) || !File.Exists(pathToAvatarImage))
                return await FailedAsync(s, "Avatar image file does not exist").ConfigureAwait(false);

            byte[] imageData;
            using (var ms = new MemoryStream())
            {
                var newImage = GeneralHelpers.ScaleImage(Image.FromFile(pathToAvatarImage), 300, 300);
                newImage.Save(ms, ImageFormat.Jpeg);
                ms.Position = 0;
                imageData = ms.ToArray();
            }

            var request = new HttpReq(HttpMethod.Post, "https://profilepicsup.kik.com/profilepics")
            {
                AdditionalHeaders = new WebHeaderCollection
                {
                    ["x-kik-jid"] = _account.Jid + "@talk.kik.com",
                    ["x-kik-password"] = _account.PasskeyU
                },
                ContentData = imageData
            };
            var response = await _httpWaifu.SendRequestAsync(request).ConfigureAwait(false);
            return response.IsOK;
        }

        private async Task<bool> InitAccount()
        {
            var word1 = Collections.Words1.GetNext();
            var word2 = Collections.Words2.GetNext();
            var firstName = Collections.FirstNames.GetNext();
            var lastName = Collections.LastNames.GetNext();
            var imageDir = Collections.ImageDirs.GetNext();
            var androidDevice = Collections.AndroidDevices.GetNext();

            if (string.IsNullOrWhiteSpace(firstName))
                firstName = word1;

            if (string.IsNullOrWhiteSpace(lastName))
                lastName = word2;

            _account = new Account(firstName, lastName, word1, word2, androidDevice, imageDir);
            if (!_account.IsValid)
            {
                await UpdateThreadStatusAsync("Failed to init account, missing required files", 2000).ConfigureAwait(false);
                return false;
            }

            UpdateAccountColumn(_account.LoginId);

            _proxy = Collections.Proxies.GetNext().ToWebProxy();

            var cfg = new HttpWaifuConfig
            {
                UserAgent = $"Kik/{Konstants.AppVersion} (Android {_account.AndroidDevice.OsVersion}) {_account.AndroidDevice.DalvikUserAgent}",
                Proxy = _proxy
            };
            _httpWaifu = new HttpWaifu(cfg);

            var metricsCfg = new HttpWaifuConfig
            {
                UserAgent = _account.AndroidDevice.DalvikUserAgent,
                Proxy = _proxy
            };
            _metricsClient = new HttpWaifu(metricsCfg);

            _tcpWaifu = new TcpAsyncWaifu
            {
                Proxy = _proxy
            };

            Interlocked.Increment(ref Stats.Attempts);
            return true;
        }

        private async Task<bool> MetricsPreReg()
        {
            const string s = "Sending pre reg metrics data: ";
            await AttemptingAsync(s).ConfigureAwait(false);

            var preReg = new MetricsPreRegSelected
            {
                EventOrigin = "mobile",
                DevicePrefix = "CAN",
                CommonDataReceivedNewPeopleInLast7Days = 0,
                CommonDataMessagesReceivedInLast7Days = 0,
                CommonDataChatListSize = 0,
                EventName = "AB PreRegistration Selected",
                CommonDataOSArchitecture = "armv7l",
                Timestamp = DateTime.UtcNow,
                CommonDataABMOptIn = false,
                CommonDataBubbleColour = "Bright Kik Green",
                CommonDataNotifyForNewPeople = true,
                ClientVersion = Konstants.AppVersion,
                DeviceId = _account.DeviceId,
                CommonDataCurrentDeviceOrientation = "Portrait",
                CommonData50CoreSetupTime = _account.MetricsVars.Core50SetupTime,
                CommonDataLoginsSinceInstall = 0,
                InstanceId = Guid.NewGuid().ToString(),
                EventDataExperiments = new EventDataExperiments()
                {
                    PreRegistrationPicturePrompt = new PreRegistrationPicturePrompt
                    {
                        Variant = "show"
                    }
                },
                CommonDataNewChatListSize = 0,
                CommonDataMessagingPartnersInLast7Days = 0,
                CommonDataOSVersion = _account.AndroidDevice.OsVersion,
                CommonDataRegistrationsSinceInstall = 0,
                CommonDataBlockListSize = 0,
                CommonDataIsWearInstalled = false,
                CommonDataAndroidId = _account.AndroidId,
                CommonData95CoreSetupTime = _account.MetricsVars.Core95SetupTime
            };

            return await SendMetricsRequest(preReg).ConfigureAwait(false);
        }

        private async Task<bool> SendMetricsRequest(object obj)
        {
            var jsonData =
                await Task.Run(() => JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.None, JsonSerializerSettings));

            var request = new HttpReq(HttpMethod.Post,
                "https://clientmetrics-augmentum.kik.com/clientmetrics/augmentum/v1/data?flattened=true")
            {
                ContentType = "application/json",
                ContentBody = jsonData,
                Timeout = 10000
            };
            var response = await _metricsClient.SendRequestAsync(request).ConfigureAwait(false);
            return response.IsOK;
        }

    }
}
