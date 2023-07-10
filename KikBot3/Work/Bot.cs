using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Web;
using System.Windows.Controls;
using DankLibWaifuz;
using DankLibWaifuz.CaptchaWaifu;
using DankLibWaifuz.CollectionsWaifu;
using DankLibWaifuz.Etc;
using DankLibWaifuz.HttpWaifu;
using DankLibWaifuz.ScriptWaifu;
using DankLibWaifuz.SettingsWaifu;
using DankLibWaifuz.TcpWaifu;
using KikBot3.Declarations;
using KikWaifu;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Interactions;
using Image = System.Drawing.Image;

#pragma warning disable 420
//                       M
//                      dM
//                      MMr
//                     4MMML                  .
//                     MMMMM.                xf
//     .              "MMMMM               .MM-
//      Mh..          +MMMMMM            .MMMM
//      .MMM.         .MMMMML.          MMMMMh
//       )MMMh.        MMMMMM         MMMMMMM
//        3MMMMx.     'MMMMMMf      xnMMMMMM"
//        '*MMMMM      MMMMMM.     nMMMMMMP"
//          *MMMMMx    "MMMMM\    .MMMMMMM=
//           *MMMMMh   "MMMMM"   JMMMMMMP
//             MMMMMM   3MMMM.  dMMMMMM            .
//              MMMMMM  "MMMM  .MMMMM(        .nnMP"
//  =..          *MMMMx  MMM"  dMMMM"    .nnMMMMM*
//    "MMn...     'MMMMr 'MM   MMM"   .nMMMMMMM*"
//     "4MMMMnn..   *MMM  MM  MMP"  .dMMMMMMM""
//       ^MMMMMMMMx.  *ML "M .M*  .MMMMMM**"
//          *PMMMMMMhn. *x > M  .MMMM**""
//             ""**MMMMhx/.h/ .=*"
//                      .3P"%....
//                    nP"     "*MMnx

namespace KikBot3.Work
{
    internal enum LoginState
    {
        Disconnected,
        Connecting,
        Connected
    }

    internal class Bot : Mode
    {
        private static readonly Regex
            MessageRegex = new Regex("<(message|msg) (.*?)</(message|msg)>",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline),
            MsgTypeRegex = new Regex("type=\"(.*?)\"",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline),
            MsgIdRegex = new Regex("id=\"(.*?)\"",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline),
            MsgFromRegex = new Regex("from=\"(.*?)\"",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline),
            MsgBodyRegex = new Regex("<body>(.*?)</body>",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline),
            ImageMessageIndexRegex = new Regex("%p(\\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            CaptchaId =
                new Regex("id=(.*?)&",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase),
            CaptchaTokensRegex =
                new Regex("<div class=\"captcha-challenge-container\">.*?token=(.*?)&.*?;(.*?)&",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline),
            CaptchaStcRegex = new Regex("<stc id=\"(.*?)\"><stp type=\"ca\">(.*?)</stp>",
                RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly object WriteLock;
        private static readonly SemaphoreSlim FirefoxLock;
        private static readonly Queue<Point> WindowPositions;

        private const string SocketException = "Socket exception";
        private const string ObjDisposedException = "Object disposed exception";
        private const string SSLAuthException = "SSL auth exception";
        private const string IOException = "I/O exception";
        private const string ExceptionOccured = "Exception occured";
        private const string KikTeam = "kikteam@talk.kik.com";
        private const string NullRefEx = "Null ref exception";

        static Bot()
        {
            WriteLock = new object();
            
            const string maxFirefox = "MaxConcurrentFirefoxWindows";
            var max = Settings.Get<int>(maxFirefox);
            if (max <= 0)
                max = 1;

            FirefoxLock = new SemaphoreSlim(max, max);

            WindowPositions = new Queue<Point>(new List<Point>
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
        }

        private readonly Account _account;
        private readonly StringBuilder _receiveBuffer;
        private readonly TextBox _chatLog;
        private readonly SemaphoreSlim _sslStreamLock;
        private readonly SemaphoreSlim _msgLock;
        private readonly object _socketLock;
        private readonly object _driverLock;

        private System.Timers.Timer _pingTimer;
        private System.Timers.Timer _keepAliveTimer;

        private TcpWaifu _client;
        private FirefoxDriver _driver;

        private WebProxy _proxy;
        private WebProxy _driverProxy;

        //private DateTime _lastConnectionAttempt;
        private DateTime _nextConnectionAt;
        private DateTime _beginSendNextMessageAt;

#if BLASTER
        private DateTime _sendNextBlastAt;
        private DateTime _resumeBlastingAt;
        private int _blastsSent;
        private int _maxBlasts;
#endif

        public Bot(int index, ObservableCollection<DataGridItem> collection, Account account, TextBox chatLog) : base(index, collection)
        {
            _account = account;
            _chatLog = chatLog;
            _sslStreamLock = new SemaphoreSlim(1, 1);
            _msgLock = new SemaphoreSlim(1, 1);
            _socketLock = new object();
            _driverLock = new object();
            _receiveBuffer = new StringBuilder();
            Collection[Index].Account = account.LoginId;

#if BLASTER
            const string blastsPerSession = "BlastsPerSession";
            _maxBlasts = Settings.GetRandom(blastsPerSession);
#endif
        }

        public LoginState LoginState { get; private set; }
        public bool Return { get; private set; }
        public string LastDisconnectReason { get; private set; }

        public bool ShouldConnect
        {
            get
            {
                if (_nextConnectionAt <= DateTime.Now)
                    return true;

                UpdateThreadStatus($"Disconnected ({_account.LoginErrors}|{_account.CaptchaErrors}): d/c reason = {LastDisconnectReason} | next connection @ {_nextConnectionAt.ToShortTimeString()}");
                return false;
            }
        }

        private bool TooManyLoginErrors
        {
            get
            {
                if (!_account.TooManyLoginErrors)
                    return false;

                const string s = "Too many login errors";
                OnDisconnect(s);
                Return = true;
                return true;
            }
        }

        private bool TooManyCaptchaErrors
        {
            get
            {
                if (!_account.TooManyCaptchaErrors)
                    return false;

                const string s = "Too many captcha errors";
                OnDisconnect(s);
                Return = true;
                return true;
            }
        }

        public void BeginConnectKik()
        {
            try
            {
                if (TooManyLoginErrors)
                {
                    WriteToFile("too_many_login_errors.txt", _account.ToString());
                    return;
                }

                if (TooManyCaptchaErrors)
                {
                    WriteToFile("too_many_captcha_errors.txt", _account.ToString());
                    return;
                }
                
                DisposeIDisposables();

                InitSocket();
                InitProperties();

                const string connecting = "Connecting: ...";
                UpdateThreadStatus(connecting);

                if (_proxy != null)
                    _client.BeginConnect(_proxy.Address.Host, _proxy.Address.Port, ConnectProxyCallback);
            }
            catch (SocketException)
            {
                OnException(SocketException);
            }
            catch (ObjectDisposedException)
            {
                OnException(ObjDisposedException);
            }
            catch (AuthenticationException)
            {
                OnException(SSLAuthException);
            }
            catch (IOException)
            {
                OnException(IOException);
            }
            catch (NullReferenceException)
            {
                OnException(NullRefEx);
            }
            catch (Exception e)
            {
                OnException(ExceptionOccured, e: e);
            }
        }

        private static void WriteToFile(string fileName, string contents)
        {
            lock (WriteLock)
            {
                using (var sw = new StreamWriter(fileName, true))
                    sw.WriteLine(contents);
            }
        }

        private void ConnectProxyCallback(IAsyncResult ar)
        {
            try
            {
                _client.EndConnect(ar);

                var sb = new StringBuilder();
                sb.Append($"CONNECT {Konstants.KikEndPoint}:{Konstants.KikPort} HTTP/1.1\r\nProxy-Connection: Keep-Alive\r\n");

                var creds = _proxy.Credentials as NetworkCredential;
                if (creds != null)
                {
                    var proxyCreds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{creds.UserName}:{creds.Password}"));
                    sb.Append($"Proxy-Authorization: basic {proxyCreds}\r\n");
                }

                sb.Append(Environment.NewLine);

                var connectStr = sb.ToString();
                var connectData = Encoding.UTF8.GetBytes(connectStr);

#if DEBUG
                Console.WriteLine(@"==> " + connectStr);
#endif

                _client.BeginWrite(connectData, WriteProxyConnectCallback);
            }
            catch (SocketException)
            {
                OnException(SocketException);
            }
            catch (ObjectDisposedException)
            {
                OnException(ObjDisposedException);
            }
            catch (AuthenticationException)
            {
                OnException(SSLAuthException);
            }
            catch (IOException)
            {
                OnException(IOException);
            }
            catch (NullReferenceException)
            {
                OnException(NullRefEx);
            }
            catch (Exception e)
            {
                OnException(ExceptionOccured, e: e);
            }
        }

        private void WriteProxyConnectCallback(IAsyncResult ar)
        {
            try
            {
                _client.EndWrite(ar);

                var buffer = new byte[2048];
                _client.BeginRead(buffer, ReceiveProxyConnectResponseCallback, buffer);
            }
            catch (SocketException)
            {
                OnException(SocketException);
            }
            catch (ObjectDisposedException)
            {
                OnException(ObjDisposedException);
            }
            catch (AuthenticationException)
            {
                OnException(SSLAuthException);
            }
            catch (IOException)
            {
                OnException(IOException);
            }
            catch (NullReferenceException)
            {
                OnException(NullRefEx);
            }
            catch (Exception e)
            {
                OnException(ExceptionOccured, e: e);
            }
        }

        private void ReceiveProxyConnectResponseCallback(IAsyncResult ar)
        {
            try
            {
                var buffer = (byte[])ar.AsyncState;
                var cnt = _client.EndRead(ar);
                if (cnt == 0)
                {
                    const string f = "Recieved 0 from proxy after connection attempt";
                    Disconnect(f);
                    return;
                }

                const string expected = "connection established";
                var response = Encoding.UTF8.GetString(buffer, 0, cnt);
                if (string.IsNullOrWhiteSpace(response) || 
                    !response.ToLower().Contains(expected))
                {
                    const string f = "Unexpected response received from proxy after connection attempt";
                    Disconnect(f);
                    return;
                }

                _client.InitSslStream(Konstants.KikEndPoint);

                var ts = Krypto.KikTimestamp();
                var deviceTsHash = Krypto.KikHmacSha1Signature(ts, _account.Jid);
                var signed = Krypto.KikRsaSign(_account.Jid, Konstants.AppVersion, ts, _account.Sid);
                var streamInitPropertyMap = Packets.StreamInitPropertyMap($"{_account.Jid}/{_account.DeviceCanId}", _account.PasskeyU,
                    deviceTsHash, long.Parse(ts), signed, _account.Sid);

                if (streamInitPropertyMap.IsNullOrEmpty())
                {
                    const string f = "Failed to calculate stream init property map (is java tunnel running?";
                    Disconnect(f);
                    return;
                }

#if DEBUG
                Console.WriteLine(@"==> " + Encoding.UTF8.GetString(streamInitPropertyMap));
#endif

                _client.BeginWrite(streamInitPropertyMap, SendStreamInitPropMapCallback);
            }
            catch (SocketException)
            {
                OnException(SocketException);
            }
            catch (ObjectDisposedException)
            {
                OnException(ObjDisposedException);
            }
            catch (AuthenticationException)
            {
                OnException(SSLAuthException);
            }
            catch (IOException)
            {
                OnException(IOException);
            }
            catch (NullReferenceException)
            {
                OnException(NullRefEx);
            }
            catch (Exception e)
            {
                OnException(ExceptionOccured, e: e);
            }
        }

        private void SendStreamInitPropMapCallback(IAsyncResult ar)
        {
            try
            {
                _client.EndWrite(ar);

                var buffer = new byte[8192];
                _client.BeginRead(buffer, ReadStreamInitPropMapResponseCallback, buffer);
            }
            catch (SocketException)
            {
                OnException(SocketException);
            }
            catch (ObjectDisposedException)
            {
                OnException(ObjDisposedException);
            }
            catch (AuthenticationException)
            {
                OnException(SSLAuthException);
            }
            catch (IOException)
            {
                OnException(IOException);
            }
            catch (Exception e)
            {
                OnException(ExceptionOccured, e: e);
            }
        }

        private void ReadStreamInitPropMapResponseCallback(IAsyncResult ar)
        {
            try
            {
                var buffer = (byte[])ar.AsyncState;
                var cnt = _client.EndRead(ar);
                if (cnt == 0)
                {
                    const string f = "Received 0 after sending stream init property map";
                    Disconnect(f);
                    return;
                }

                var response = Encoding.UTF8.GetString(buffer, 0, cnt);
                if (string.IsNullOrWhiteSpace(response))
                {
                    const string f = "Recieved empty response after sending stream init property map";
                    Disconnect(f);
                    return;
                }

#if DEBUG
                Console.WriteLine(@"<== " + response);
#endif

                const string ok = "ok=\"1\"";
                if (response.Contains(ok))
                {
                    _client.BeginWrite(Packets.GetUnackedMsgs(), SendGetUnackedMessagesCallback);
                    return;
                }

                const string notAuth = "Not Authorized";
                if (response.Contains(notAuth))
                {
                    const string f = "Received not authorized";
                    Disconnect(f);
                    return;
                }

                Disconnect("Unexpected response after sending stream init property map");
            }
            catch (SocketException)
            {
                OnException(SocketException);
            }
            catch (ObjectDisposedException)
            {
                OnException(ObjDisposedException);
            }
            catch (AuthenticationException)
            {
                OnException(SSLAuthException);
            }
            catch (IOException)
            {
                OnException(IOException);
            }
            catch (NullReferenceException)
            {
                OnException(NullRefEx);
            }
            catch (Exception e)
            {
                OnException(ExceptionOccured, e: e);
            }
        }

        private void SendGetUnackedMessagesCallback(IAsyncResult ar)
        {
            try
            {
                _client.EndWrite(ar);

                var tmpState = new TmpState
                {
                    StringBuffer =  new StringBuilder(),
                    Buffer = new byte[8192]
                };
                BeginReceiveGetUnackedMessagesResponse(tmpState);
            }
            catch (SocketException)
            {
                OnException(SocketException);
            }
            catch (ObjectDisposedException)
            {
                OnException(ObjDisposedException);
            }
            catch (AuthenticationException)
            {
                OnException(SSLAuthException);
            }
            catch (IOException)
            {
                OnException(IOException);
            }
            catch (NullReferenceException)
            {
                OnException(NullRefEx);
            }
            catch (Exception e)
            {
                OnException(ExceptionOccured, e: e);
            }
        }

        private void BeginReceiveGetUnackedMessagesResponse(TmpState state)
        {
            try
            {
                _client.BeginRead(state.Buffer, ReceiveGetUnackedMessagesResponse, state);
            }
            catch (SocketException)
            {
                OnException(SocketException);
            }
            catch (ObjectDisposedException)
            {
                OnException(ObjDisposedException);
            }
            catch (AuthenticationException)
            {
                OnException(SSLAuthException);
            }
            catch (IOException)
            {
                OnException(IOException);
            }
            catch (NullReferenceException)
            {
                OnException(NullRefEx);
            }
            catch (Exception e)
            {
                OnException(ExceptionOccured, e: e);
            }
        }

        private void ReceiveGetUnackedMessagesResponse(IAsyncResult ar)
        {
            try
            {
                var state = (TmpState)ar.AsyncState;
                var buffer = state.Buffer;
                var cnt = _client.EndRead(ar);
                if (cnt == 0)
                {
                    Disconnect("Recieved 0 when trying to receive get messages response");
                    return;
                }

                var response = Encoding.UTF8.GetString(buffer, 0, cnt);
                state.StringBuffer.Append(response);

                Thread.Sleep(100);

                if (_client.Available > 0)
                {
                    BeginReceiveGetUnackedMessagesResponse(state);
                    return;
                }

                var data = state.StringBuffer.ToString();

#if DEBUG
                Console.WriteLine(@"<== " + data);
#endif

                var matcherino = CaptchaStcRegex.Match(data);
                if (matcherino.Success)
                {
                    const string solveCaptchas = "SolveCaptchas";
                    if (!Settings.Get<bool>(solveCaptchas))
                    {
                        const string f = "Captchaed";
                        Disconnect(f);
                        _account.CaptchaErrors++;
                        return;
                    }

                    var stcId = matcherino.Groups[1].Value;
                    var captchaUrl = matcherino.Groups[2].Value;
                    if (GeneralHelpers.AnyNullOrWhiteSpace(stcId, captchaUrl))
                    {
                        const string s = "failed to parse captcha vars from stc packet";
                        Disconnect(s);
                        _account.CaptchaErrors++;
                        return;
                    }

                    string captchaId;
                    if (!CaptchaId.TryGetGroup(captchaUrl, out captchaId))
                    {
                        const string f = "Failed to get captcha id from stc packet";
                        Disconnect(f);
                        return;
                    }

                    var captchaVars = new CaptchaVars
                    {
                        Id = captchaId,
                        StcId = stcId,
                        Url = HttpUtility.HtmlDecode(captchaUrl)
                    };

                    Task.Factory.StartNew(() => HandleCaptcha(captchaVars), TaskCreationOptions.LongRunning)
                        .ConfigureAwait(false);
                    return;
                }

                OnConnected();
                HandleReceivedData(data);
            }
            catch (SocketException)
            {
                OnException(SocketException);
            }
            catch (ObjectDisposedException)
            {
                OnException(ObjDisposedException);
            }
            catch (AuthenticationException)
            {
                OnException(SSLAuthException);
            }
            catch (IOException)
            {
                OnException(IOException);
            }
            catch (NullReferenceException)
            {
                OnException(NullRefEx);
            }
            catch (Exception e)
            {
                OnException(ExceptionOccured, e: e);
            }
        }

        private void HandleCaptcha(CaptchaVars captchaVars)
        {
            if (!FirefoxLock.Wait(1000))
            {
                const string f = "No firefox threads available";
                DisconnectBecauseOfCaptcha(f, 60);
                return;
            }

            var success = false;
            try
            {
                try
                {
                    LaunchFirefox();

                    if (!LoadCaptcha(captchaVars.Url))
                    {
                        const string f = "Failed to load captcha webpage";
                        DisconnectBecauseOfCaptcha(f, 120);
                        return;
                    }

                    if (!ParseCaptchaTokens(captchaVars))
                    {
                        const string f = "Failed to parse captcha tokens";
                        DisconnectBecauseOfCaptcha(f, 120);
                        return;
                    }

                    const string use2Captcha = "Use2Captcha";
                    if (Settings.Get<bool>(use2Captcha))
                    {
                        if (!Solve2Captcha())
                            return;
                    }
                    else
                    {
                        if (!WaitForCaptchaToBeSolved())
                        {
                            const string f = "Captcha solve attempt timed out waiting for success indicator";
                            DisconnectBecauseOfCaptcha(f, 120);
                            return;
                        }
                    }
                }
                finally
                {
                    DisposeDriver();
                    FirefoxLock.Release();
                }

                if (!VerifyCaptcha(captchaVars))
                {
                    const string f = "Failed to verify captcha";
                    DisconnectBecauseOfCaptcha(f, 120);
                    return;
                }

                var stcSolData = Packets.StcSolution(captchaVars.StcId, captchaVars.Solution);
                _client.NetworkStream.WriteTimeout = 30000;
                _client.NetworkStream.ReadTimeout = 30000;
                _client.NetworkStream.Write(stcSolData, 0, stcSolData.Length);

                var sb = new StringBuilder();
                while (true)
                {
                    var buffer = new byte[8192];
                    var cnt = _client.NetworkStream.Read(buffer, 0, buffer.Length);
                    if (cnt == 0)
                    {
                        const string f = "Recieved 0 after sending captcha solution";
                        DisconnectBecauseOfCaptcha(f, 120);
                        return;
                    }

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, cnt));

                    var i = 0;
                    while (_client.Available == 0 && i++ < 50)
                        Thread.Sleep(100);

                    if (_client.Available == 0)
                        break;
                }

                var response = sb.ToString();
                if (!response.Contains("kik:iq:QoS"))
                {
                    const string f = "Unexpected response after sending captcha solution";
                    DisconnectBecauseOfCaptcha(f, 120);
                    return;
                }

                OnConnected();
                HandleReceivedData(response);
                success = true;
                _account.CaptchaErrors = 0;
            }
            catch (NullReferenceException)
            {
                OnException(NullRefEx);
            }
            catch (Exception e)
            {
                OnException(ExceptionOccured, e: e);
            }
            finally
            {
                if (!success)
                    _account.CaptchaErrors++;
            }
        }

        private bool Solve2Captcha()
        {
            var canvas = GetCanvasObject();
            if (canvas == null)
            {
                const string f = "Failed to find canvas obj";
                Disconnect(f);
                return false;
            }

            const string s = "Waiting for solution: ";
            Attempting(s);

            Thread.Sleep(5000);

            var solver = new TwoCaptchaWaifu(Settings.Get<string>("2CaptchaAPIKey"));
            for (var j = 0; j < 5; j++)
            {
                var bmp = GetElementScreenShort(_driver, canvas);
                if (bmp == null)
                {
                    const string f = "Failed to get screenshot of canavs";
                    Disconnect(f);
                    return false;
                }

                var imageData = GeneralHelpers.ImageToBytes(bmp);
                bmp.Dispose();

                var solution = solver.SolveRotateCaptcha(imageData);
                if (solution == 0)
                {
                    const string f = "2Captcha returned 0";
                    Disconnect(f);
                    return false;
                }

                var rotations = solution / 40;
                if (rotations > 0)
                {
                    for (var i = 0; i < rotations; i++)
                    {
                        ClickLeftArrow(canvas);
                        Thread.Sleep(500);
                    }
                }
                else
                {
                    var max = rotations * -1;
                    for (var i = 0; i < max; i++)
                    {
                        ClickRightArrow(canvas);
                        Thread.Sleep(500);
                    }
                }

                ClickInCanvas(canvas, 100, 240);

                _driver.SwitchTo().DefaultContent();

                if (LostFirefoxWindow())
                {
                    const string f = "Lost firefox window";
                    Disconnect(f);
                    return false;
                }

                if (_driver.FindElement(By.XPath("//div[@class='app-dialog']"), 5) == null)
                {
                    SwitchToRelevantIFrame();
                    Thread.Sleep(1000);
                    continue;
                }

                return true;
            }

            const string f2 = "Failed to find captcha solved signal";
            Disconnect(f2);
            return false;
        }

        private static Bitmap GetElementScreenShort(IWebDriver driver, IWebElement element)
        {
            try
            {
                var sc = ((ITakesScreenshot)driver).GetScreenshot();
                using (var img = Image.FromStream(new MemoryStream(sc.AsByteArray)) as Bitmap)
                {
                    if (img == null)
                        return null;

                    using (var clone1 = img.Clone(new Rectangle(new Point(0, 50), element.Size), img.PixelFormat))
                    {
                        return clone1.Clone(new Rectangle(90, 100, 125, 125), clone1.PixelFormat);
                    }
                }
                    
            }
            catch { return null; }
        }

        private IWebElement GetCanvasObject()
        {
            const string s = "Getting canvas obj: ";
            Attempting(s);

            if (!SwitchToRelevantIFrame())
                return null;

            var canvas = _driver.FindElement(By.Id("FunCAPTCHA"), 60);
            if (canvas == null)
                return null;

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

        private bool VerifyCaptcha(CaptchaVars captchaVars)
        {
            const string s = "Verifying captcha was solved correctly: ";
            Attempting(s);

            var cfg = new HttpWaifuConfig
            {
                UserAgent = _account.KikUserAgent,
                Proxy = _driverProxy
            };
            var client = new HttpWaifu(cfg);

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
            var response = client.SendRequest(request);
            if (!response.IsOK || string.IsNullOrWhiteSpace(response.ContentBody))
                return false;

            captchaVars.Solution = response.ContentBody;
            return true;
        }

        private bool LoadCaptcha(string captchaUrl)
        {
            const string s = "Loading captcha: ";
            Attempting(s);

            _driver.Navigate().GoToUrl(captchaUrl);

            Thread.Sleep(5000);

            const string expected = "<title>Verify Account";
            return _driver.PageSource.Contains(expected);
        }

        private bool ParseCaptchaTokens(CaptchaVars captchaVars)
        {
            const string s = "Parsing captcha tokens: ";
            Attempting(s);

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
                return false;

            captchaVars.Response = $"{id}|{id2}";
            return true;

        }

        private bool WaitForCaptchaToBeSolved()
        {
            const string s = "Waiting for captcha to be solved: ";
            Attempting(s);

            for (var i = 0; i < 30; i++)
            {
                if (LostFirefoxWindow())
                    return false;

                if (_driver.FindElement(By.XPath("//div[@class='app-dialog']"), 10) == null)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool LostFirefoxWindow()
        {
            if (_driver == null || _driver.WindowHandles == null || _driver.WindowHandles.Count == 0)
                return true;

            return false;
        }

        private void DisposeDriver()
        {
            lock (_driverLock)
            {
                try
                {
                    if (_driver == null)
                        return;

                    _driver.Quit();
                    _driver.Dispose();
                }
                catch { /*ignored*/}
                finally { _driver = null; }
            }
        }

        private static void SetWebDriverCustomHeaders(NameValueCollection webHeaders, FirefoxProfile profile)
        {
            if (webHeaders == null || webHeaders.Count == 0 || profile == null)
                return;

            const string active = "modifyheaders.config.active";
            const string on = "modifyheaders.config.alwaysOn";
            const string start = "modifyheaders.start";
            const string newTab = "modifyheaders.config.openNewTab";
            const string cnt = "modifyheaders.headers.count";

            profile.SetPreference(active, true);
            profile.SetPreference(on, true);
            profile.SetPreference(start, true);
            profile.SetPreference(newTab, true);
            profile.SetPreference(cnt, webHeaders.Count);

            for (var i = 0; i < webHeaders.Count; i++)
            {
                var key = webHeaders.GetKey(i);
                var values = webHeaders.GetValues(i);
                if (values == null || values.Length == 0)
                    continue;

                var value = values[0];

                const string enabled = "modifyheaders.headers.enabled0";

                profile.SetPreference($"modifyheaders.headers.action{i}", "Add");
                profile.SetPreference($"modifyheaders.headers.name{i}", key);
                profile.SetPreference($"modifyheaders.headers.value{i}", value);
                profile.SetPreference(enabled, true);
            }
        }

        private void LaunchFirefox()
        {
            const string s = "Launching firefox: ";
            Attempting(s);

            var profileId = Guid.NewGuid().ToString();
            var directory = $"{AppDomain.CurrentDomain.BaseDirectory}{profileId}";

            var tmp = new FirefoxProfile(directory)
            {
                AcceptUntrustedCertificates = true,
                EnableNativeEvents = true,
                DeleteAfterUse = true,
                AssumeUntrustedCertificateIssuer = false
            };

            _driverProxy = Collections.WebBrowserProxies.GetNext().ToWebProxy() ?? _proxy;
            var proxy = new Proxy
            {
                HttpProxy = _driverProxy.Address.Authority,
                SslProxy = _driverProxy.Address.Authority
            };

            tmp.SetProxyPreferences(proxy);

            const string userAgentOverride = "general.useragent.override";
            tmp.SetPreference(userAgentOverride, _account.BrowserUserAgent);

            const string modifyHeaders = "modify_headers.xpi";
            tmp.AddExtension(modifyHeaders);

            const string peerConnection = "media.peerconnection.enabled";
            tmp.SetPreference(peerConnection, false);

            const string xReqWith = "X-Requested-With";
            const string kikAndroid = "kik.android";
            var headers = new WebHeaderCollection
            {
                [xReqWith] = kikAndroid
            };

            SetWebDriverCustomHeaders(headers, tmp);

            _driver = new FirefoxDriver(tmp);
            _driver.Manage().Window.Size = new Size(320, 568);
            _driver.Manage().Window.Position = WindowPositions.GetNext();
        }

        private void OnException(string reason, [CallerMemberName]string caller = null, Exception e = null)
        {
            reason = $"{reason} @ {caller}()";

            Disconnect(reason);

            if (e == null)
                return;

            ErrorLogger.WriteErrorLog(e);
        }

        private void OnConnected()
        {
            LoginState = LoginState.Connected;
            _account.LoginErrors = 0;
            _account.CaptchaErrors = 0;
            BeginReceive();

            _pingTimer = new System.Timers.Timer(1000.0);
            _pingTimer.Elapsed += PingCallback;
            _pingTimer.Start();

            _keepAliveTimer = new System.Timers.Timer(1200000.0);
            _keepAliveTimer.Elapsed += KeepAliveCallback;
            _keepAliveTimer.Start();

            const string connected = "Connected";
            UpdateThreadStatus(connected);
        }

        private void BeginReceive()
        {
            if (LoginState != LoginState.Connected)
                return;

            try
            {
                var buffer = new byte[8192];
                _client.BeginRead(buffer, ReceivedDataCallback, buffer);
            }
            catch (SocketException)
            {
                OnException(SocketException);
            }
            catch (ObjectDisposedException)
            {
                OnException(ObjDisposedException);
            }
            catch (AuthenticationException)
            {
                OnException(SSLAuthException);
            }
            catch (IOException)
            {
                OnException(IOException);
            }
            catch (NullReferenceException)
            {
                OnException(NullRefEx);
            }
            catch (Exception e)
            {
                OnException(ExceptionOccured, e: e);
            }
        }

        private void ReceivedDataCallback(IAsyncResult ar)
        {
            try
            {
                var buffer = (byte[])ar.AsyncState;
                var cnt = _client.EndRead(ar);
                if (cnt == 0)
                {
                    const string f = "Recieved 0";
                    Disconnect(f);
                    return;
                }

                var data = Encoding.UTF8.GetString(buffer, 0, cnt);
                _receiveBuffer.Append(data);

                Thread.Sleep(100);

                if (_client.Available > 0)
                    return;

                data = _receiveBuffer.ToString();
                _receiveBuffer.Clear();
#if DEBUG
                Console.WriteLine(@"<== " + data);
#endif
                HandleReceivedData(data);
            }
            catch (SocketException)
            {
                OnException(SocketException);
            }
            catch (ObjectDisposedException)
            {
                OnException(ObjDisposedException);
            }
            catch (AuthenticationException)
            {
                OnException(SSLAuthException);
            }
            catch (IOException)
            {
                OnException(IOException);
            }
            catch (NullReferenceException)
            {
                OnException(NullRefEx);
            }
            catch (Exception e)
            {
                OnException(ExceptionOccured, e: e);
            }
            finally
            {
                BeginReceive();
            }
        }

        private void HandleReceivedData(string data)
        {
            var matcherino = CaptchaStcRegex.Match(data);
            if (matcherino.Success)
            {
                const string f = "Captchaed";
                Disconnect(f);
                return;
            }

            var matches = MessageRegex.Matches(data);
            if (matches.Count == 0)
                return;

            foreach (Match match in matches)
            {
                var messageXml = match.Groups[2].Value;
                if (string.IsNullOrWhiteSpace(messageXml))
                    continue;

                string msgType;
                if (!MsgTypeRegex.TryGetGroup(messageXml, out msgType))
                    continue;

                const string receipt = "receipt";
                if (msgType.ToLower() == receipt)
                {
                    HandleMessageReceipt(messageXml);
                    continue;
                }

                if (msgType.ToLower() != "chat")
                    continue;

                string msgId;
                if (!MsgIdRegex.TryGetGroup(messageXml, out msgId))
                    continue;

                string fromJid;
                if (!MsgFromRegex.TryGetGroup(messageXml, out fromJid))
                    continue;

                string body;
                MsgBodyRegex.TryGetGroup(messageXml, out body);

                Task.Run(() => AckRecievedMessage(fromJid, msgId))
                    .ConfigureAwait(false);

                if (fromJid == KikTeam)
                    continue;

                const string msg = "msg";
                var rcvdOffline = match.Groups[1].Value.ToLower() == msg;

                if (rcvdOffline)
                {
                    var msgBlacklistInput = $"{fromJid}:{_account.Jid}:{msgId}";
                    if (Blacklists.Collections[BlacklistType.Message]
                        .Contains(msgBlacklistInput))
                        continue;

                    AddBlacklist(BlacklistType.Message, msgBlacklistInput);
                }

                const string startedChatting = "started chatting with you";
                const string phoneBeenOff = "phone has been off/disconnected for a while.";

                if (messageXml.Contains(startedChatting) ||
                    messageXml.Contains(phoneBeenOff))
                    continue;

                if (Blacklists.Collections[BlacklistType.Chat].Contains(fromJid))
                    continue;

                OnMessageReceived(fromJid, body);
                HandleIncomingMessage(fromJid, body);
            }
        }

        private void HandleMessageReceipt(string messageXml)
        {
            const string igAck = "IgnoreAckedMessages";
            if (Settings.Get<bool>(igAck))
                return;

            string msgId;
            if (!MsgIdRegex.TryGetGroup(messageXml, out msgId))
                return;

            string fromJid;
            if (!MsgFromRegex.TryGetGroup(messageXml, out fromJid))
                return;
            
            if (fromJid == KikTeam || 
                Blacklists.Collections[BlacklistType.Chat].Contains(fromJid))
                return;

            if (_account.Convos.ContainsKey(fromJid))
                return;

            const string body = "offline message";
            OnMessageReceived(fromJid, body);

            HandleIncomingMessage(fromJid, body);
        }

        private void HandleIncomingMessage(string fromJid, string bodyReceived)
        {
            if (!_account.Convos.ContainsKey(fromJid))
            {
                lock (_account.Convos)
                    _account.Convos.Add(fromJid, new ScriptWaifu(Collections.Script));

                Interlocked.Increment(ref Stats.Convos);

                WriteToFile("responders.txt", fromJid);
            }

            _account.Convos[fromJid].LastMessageReceivedAt = DateTime.Now;

            if (ScriptWaifu.HasRestrictedKeyword(bodyReceived, Collections.Restricts))
            {
                AddBlacklist(BlacklistType.Chat, fromJid);

                Interlocked.Increment(ref Stats.Restricts);

                const string sorry = "sorry";
                var apology = ScriptWaifu.Spin(Collections.Apologies.GetNext());
                if (string.IsNullOrWhiteSpace(apology))
                    apology = sorry;

                var msg = new OutgoingMessage(fromJid, apology);
                EnqueueOrSendMessage(msg);
                return;
            }

            if (_account.Convos[fromJid].Pending)
                return;

            _account.Convos[fromJid].Pending = true;

            if (!_account.Convos[fromJid].IsFirstLine)
            {
                string keywordResponse;
                if (_account.Convos[fromJid].TryFindKeywordResponse(bodyReceived, Collections.Keywords, out keywordResponse))
                {
                    var msg = new OutgoingMessage(fromJid, keywordResponse);
                    EnqueueOrSendMessage(msg);
                    return;
                }
            }

            var reply = _account.Convos[fromJid].NextLine();
            if (string.IsNullOrWhiteSpace(reply))
            {
                _account.Convos[fromJid].Pending = false;
                return;
            }

            if (_account.Convos[fromJid].IsComplete)
            {
                AddBlacklist(BlacklistType.Chat, fromJid);
                Interlocked.Increment(ref Stats.Completed);
            }

            {
                var msg = new OutgoingMessage(fromJid, reply);
                EnqueueOrSendMessage(msg);
            }
        }

        private void EnqueueOrSendMessage(OutgoingMessage msg)
        {
            const string useMsgQueue = "UseMessageQueue";
            if (!Settings.Get<bool>(useMsgQueue))
            {
                Task.Factory.StartNew(() => HandleOutgoingMessage(msg),
                        TaskCreationOptions.LongRunning)
                    .ConfigureAwait(false);
            }
            else
            {
#if BLASTER
                if (msg.IsBlast)
                {
                    lock (_account.PendingGreets)
                        _account.PendingGreets.Enqueue(msg);

                    return;
                }
#endif

                lock (_account.PendingMessages)
                    _account.PendingMessages.Enqueue(msg);
            }
        }

        private bool MsgRequeuedBecauseOfDC(OutgoingMessage msg)
        {
            const string useMsgQueue = "UseMessageQueue";
            if (!Settings.Get<bool>(useMsgQueue))
                return false;

            if (LoginState == LoginState.Disconnected)
            {
#if BLASTER
                if (msg.IsBlast)
                {
                    lock (_account.PendingGreets)
                        _account.PendingGreets.Enqueue(msg);

                    return true;
                }
#endif

                lock (_account.PendingMessages)
                    _account.PendingMessages.Enqueue(msg);

                return true;
            }

            return false;
        }

        private void HandleOutgoingMessage(OutgoingMessage msg)
        {
            try
            {
                Thread.Sleep(Random.Next(8000, 16000));

                const string useMsgQueue = "UseMessageQueue";
                if (!Settings.Get<bool>(useMsgQueue))
                {
                    const string sendMsgDelay = "SendMessageDelay";
                    var sendMessageDelaySeconds = Settings.GetRandom(sendMsgDelay) * 1000;
                    Thread.Sleep(sendMessageDelaySeconds);
                }

                if (MsgRequeuedBecauseOfDC(msg))
                    return;

#if BLASTER
                const string enableAdding = "EnableAdding";
                if (msg.IsBlast && Settings.Get<bool>(enableAdding) && !_account.Contacts.Contains(msg.ToJid))
                {
                    lock (_account.Contacts)
                        _account.Contacts.Add(msg.ToJid);

                    BeginSendData(Packets.Add(msg.ToJid));
                }
#endif

                if (msg.IsImage)
                {
                    HandleImageMessage(msg);
                    return;
                }

                const string useKikBrowser = "UseKikBrowser";
                var msgBody = msg.Body;
                if (msg.IsLink && Settings.Get<bool>(useKikBrowser))
                {
                    HandleLinkMessage(msg);
                    return;
                }

                const string percentS = "%s";
                if (msg.IsLink && !Settings.Get<bool>(useKikBrowser))
                    msgBody = msgBody.Replace(percentS, Collections.Links.GetNext());

                BeginSendData(Packets.IsTyping(msg.ToJid));
                GeneralHelpers.TypingDelay(msgBody);

                if (MsgRequeuedBecauseOfDC(msg))
                    return;

                BeginSendData(Packets.SendMessage(Guid.NewGuid().ToString(), msg.ToJid, msgBody));

                OnMessageSent(msg);
            }
            catch (Exception e)
            {
                OnException(ExceptionOccured, e: e);
            }
            finally
            {
                if (_account.Convos.ContainsKey(msg.ToJid))
                    _account.Convos[msg.ToJid].Pending = false;

                ReleaseMsgLock();
            }
        }

        private void HandleImageMessage(OutgoingMessage msg)
        {
            if (Collections.ImageFiles == null || 
                Collections.ImageFiles.Count == 0)
                return;

            string strIndex;
            if (!ImageMessageIndexRegex.TryGetGroup(msg.Body, out strIndex))
                return;

            int index;
            if (!int.TryParse(strIndex, out index))
                return;

            if (index >= Collections.ImageFiles.Count)
                return;

            const string p = "%p\\d+";
            var split = Regex.Split(msg.Body, p);
            if (split.Length < 1)
                return;

            var pathToImageFile = Collections.ImageFiles[index];
            if (!File.Exists(pathToImageFile))
                return;

            var randomziedImageData = GeneralHelpers.RandomizeImage(pathToImageFile);
            if (randomziedImageData.IsNullOrEmpty())
                return;

            var sentMessage = false;

            try
            {
                var firstMessage = Regex.Replace(split[0], p, string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(firstMessage))
                    return;

                BeginSendData(Packets.IsTyping(msg.ToJid));
                GeneralHelpers.TypingDelay(firstMessage);

                if (MsgRequeuedBecauseOfDC(msg))
                    return;

                BeginSendData(Packets.SendMessage(Guid.NewGuid().ToString(), msg.ToJid, firstMessage));

                Thread.Sleep(Random.Next(6000, 9000));

                var imageDataLength = randomziedImageData.Length;
                var base64DImageData = Convert.ToBase64String(randomziedImageData);

                var shareImage = new ShareImage(msg.ToJid, imageDataLength, base64DImageData);
                BeginSendData(Packets.ShareImage(shareImage));

                sentMessage = true;

                if (split.Length < 2 || string.IsNullOrWhiteSpace(split[1]))
                    return;

                Thread.Sleep(Random.Next(6000, 9000));

                var secondMessage = Regex.Replace(split[1], p, string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(secondMessage))
                    return;

                BeginSendData(Packets.IsTyping(msg.ToJid));
                GeneralHelpers.TypingDelay(secondMessage);
                BeginSendData(Packets.SendMessage(Guid.NewGuid().ToString(), msg.ToJid, secondMessage));
            }
            finally
            {
                if (sentMessage)
                    OnMessageSent(msg);
            }

        }


        private void HandleLinkMessage(OutgoingMessage msg)
        {
            const string percentS = "%s";
            var split = Regex.Split(msg.Body, percentS, RegexOptions.Compiled);
            if (split.Length < 1)
                return;

            var sentMessage = false;

            try
            {
                var firstMessage = split[0].Replace(percentS, string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(firstMessage))
                    return;

                BeginSendData(Packets.IsTyping(msg.ToJid));
                GeneralHelpers.TypingDelay(firstMessage);

                if (MsgRequeuedBecauseOfDC(msg))
                    return;

                BeginSendData(Packets.SendMessage(Guid.NewGuid().ToString(), msg.ToJid, firstMessage));

                var link = Collections.Links.GetNext();
                if (string.IsNullOrWhiteSpace(link))
                    return;

                var shareLink = new ShareLink(msg.ToJid, link,
                    "", "", "");

                Thread.Sleep(Random.Next(6000, 9000));

                BeginSendData(Packets.ShareLink(shareLink));

                sentMessage = true;

                if (split.Length < 2 || string.IsNullOrWhiteSpace(split[1]))
                    return;

                var secondMessage = split[1].Replace("%s", string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(secondMessage))
                    return;

                Thread.Sleep(Random.Next(6000, 9000));

                BeginSendData(Packets.IsTyping(msg.ToJid));
                GeneralHelpers.TypingDelay(secondMessage);
                BeginSendData(Packets.SendMessage(Guid.NewGuid().ToString(), 
                    msg.ToJid, secondMessage));
            }
            finally
            {
                if (sentMessage)
                    OnMessageSent(msg);
            }
        }

        private void OnMessageSent(OutgoingMessage message)
        {
#if BLASTER
            if (message.IsBlast)
            {
                Interlocked.Increment(ref Stats.Blasts);
                return;
            }
#endif
            UpdateChatLog(ChatLogType.Outgoing, message.ToJid, message.Body);

            if (message.IsKeepAlive)
            {
                Interlocked.Increment(ref Stats.KeepAlives);
                return;
            }

            Interlocked.Increment(ref Stats.Out);
            Interlocked.Increment(ref _account.Out);
            Collection[Index].OutCount = _account.Out;

            if (message.IsLink)
                Interlocked.Increment(ref Stats.Links);
        }


        private void AckRecievedMessage(string from, string msgId)
        {
            BeginSendData(Packets.MessageDelivered(from, msgId));
            BeginSendData(Packets.MessageRead(from, msgId));
        }

        private void OnMessageReceived(string from, string body)
        {
            _account.LastMessageRecievedAt = DateTime.Now;
            Interlocked.Increment(ref Stats.In);
            Collection[Index].InCount = ++_account.In;

            UpdateChatLog(ChatLogType.Incoming, from, body);
        }

        internal enum ChatLogType
        {
            Incoming,
            Outgoing
        }

        private void UpdateChatLog(ChatLogType chatLogType, string otherUid, string body)
        {
            const string chatLogEnabled = "ChatLogEnabled";
            if (!Settings.Get<bool>(chatLogEnabled))
                return;

            const string left = "<==";
            const string right = "==>";
            var arrow = chatLogType == ChatLogType.Incoming ? left : right;
            var str = $"[{DateTime.Now.ToShortTimeString()}] {_account.LoginId} {arrow} {otherUid}: {body}";
            _chatLog.Dispatcher.BeginInvoke(new Action(() =>
            {
                _chatLog.AppendText($"{str}{Environment.NewLine}");
            }));
        }

        private void KeepAliveCallback(object sender, ElapsedEventArgs e)
        {
            try
            {
                foreach (var jid in _account.Convos.Keys.ToList())
                {
                    if (Blacklists.Collections[BlacklistType.Chat].Contains(jid))
                        continue;

                    if (_account.Convos[jid].IsComplete)
                        continue;

                    const string maxKeepAlivesStr = "MaxKeepAlives";
                    var maxKeepAlives = Settings.Get<int>(maxKeepAlivesStr);
                    if (_account.Convos[jid].KeepAlivesSent >= maxKeepAlives)
                        continue;

                    if (_account.Convos[jid].HaveSentKeepAlive)
                        continue;

                    const string keepAliveDelayStr = "KeepAliveDelay";
                    var keepAliveDelay = Settings.Get<int>(keepAliveDelayStr);

                    var now = DateTime.Now;
                    var l = _account.Convos[jid].LastMessageReceivedAt.AddMinutes(keepAliveDelay);

                    if (l >= now)
                        continue;

                    var keepAliveMessage = ScriptWaifu.Spin(Collections.KeepAlives.GetNext());
                    if (string.IsNullOrWhiteSpace(keepAliveMessage))
                        continue;

                    _account.Convos[jid].OnSentKeepAlive();

                    var msg = new OutgoingMessage(jid, keepAliveMessage)
                    {
                        IsKeepAlive = true
                    };
                    Task.Factory.StartNew(() => HandleOutgoingMessage(msg),
                            TaskCreationOptions.LongRunning)
                        .ConfigureAwait(false);
                }
            }
            catch { /*ignored haha C# fags triggered empty catches for life LUL*/ }
        }

        private void PingCallback(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (LoginState != LoginState.Connected)
                    return;

                if ((_pingTimer.Interval = _pingTimer.Interval * 2) >= 30000.0)
                    _pingTimer.Interval = 30000.0;

                Task.Run(() => BeginSendData(Packets.Ping()))
                    .ConfigureAwait(false);
            }
            catch (NullReferenceException) { /*ignored*/ }
            catch (ObjectDisposedException) { /*ignored*/ }
        }

        public void BeginSendData(byte[] data)
        {
            try
            {
                _sslStreamLock.Wait();

#if DEBUG
                Console.WriteLine(@"==> " + Encoding.UTF8.GetString(data));
#endif

                _client.BeginWrite(data, SendDataCallback);
            }
            catch (SocketException)
            {
                OnException(SocketException);
                ReleaseSslStreamLock();
            }
            catch (ObjectDisposedException)
            {
                OnException(ObjDisposedException);
                ReleaseSslStreamLock();
            }
            catch (AuthenticationException)
            {
                OnException(SSLAuthException);
                ReleaseSslStreamLock();
            }
            catch (IOException)
            {
                OnException(IOException);
                ReleaseSslStreamLock();
            }
            catch (NullReferenceException)
            {
                OnException(NullRefEx);
                ReleaseSslStreamLock();
            }
            catch (Exception e)
            {
                OnException(ExceptionOccured, e: e);
                ReleaseSslStreamLock();
            }
        }

        private void SendDataCallback(IAsyncResult ar)
        {
            try
            {
                _client.EndWrite(ar);
            }
            catch (SocketException)
            {
                OnException(SocketException);
            }
            catch (ObjectDisposedException)
            {
                OnException(ObjDisposedException);
            }
            catch (AuthenticationException)
            {
                OnException(SSLAuthException);
            }
            catch (IOException)
            {
                OnException(IOException);
            }
            catch (NullReferenceException)
            {
                OnException(NullRefEx);
            }
            catch (Exception e)
            {
                OnException(ExceptionOccured, e: e);
            }
            finally
            {
                ReleaseSslStreamLock();
            }
        }

        private void Disconnect(string reason)
        {
            DisposeIDisposables();

            switch (LoginState)
            {
                case LoginState.Connected:
                    OnDisconnect(reason);
                    break;
                case LoginState.Connecting:
                    OnConnectionFailed(reason);
                    break;

                default:
                    OnDisconnect(reason);
                    break;
            }
        }

        public void DisconnectBecauseOfCaptcha(string reason, int secondsTillNextConnect)
        {
            DisposeIDisposables();
            _nextConnectionAt = DateTime.Now.AddSeconds(secondsTillNextConnect);
            LoginState = LoginState.Disconnected;
            LastDisconnectReason = reason;
            UpdateThreadStatus($"Connection failed: {reason}");
        }

        private void OnDisconnect(string reason)
        {
            LastDisconnectReason = reason;
            LoginState = LoginState.Disconnected;
            UpdateThreadStatus($"Disconnected: {reason}");
        }

        private void OnConnectionFailed(string reason)
        {
            LoginState = LoginState.Disconnected;
            _account.LoginErrors++;
            LastDisconnectReason = reason;
            UpdateThreadStatus($"Connection failed: {reason}");
        }

        private void InitProperties()
        {
            _nextConnectionAt = DateTime.Now.AddSeconds(90);
            LoginState = LoginState.Connecting;

            ReleaseSslStreamLock();
            ReleaseMsgLock();

            _account.Sid = Guid.NewGuid().ToString();
        }

        private void ReleaseSslStreamLock()
        {
            if (_sslStreamLock.CurrentCount != 1)
                _sslStreamLock.Release();
        }

        private void ReleaseMsgLock()
        {
            if (_msgLock.CurrentCount != 1)
                _msgLock.Release();
        }

        private void DisposeSocket()
        {
            try
            {
                lock (_socketLock)
                {
                    try
                    {
                        if (_client == null)
                            return;

                        _client.OnConnectionTimedout -= OnClientConnectionTimedout;
                        _client.OnReadTimedout -= OnClientReadTimedout;
                        _client.OnWriteTimedout -= OnClientWriteTimedout;

                        _client.Dispose();
                    }
                    catch { /*ignored*/ }
                    finally
                    {
                        _client = null;
                    }
                }
            }
            catch { /*ignored*/ }
        }

        public void DisposeIDisposables()
        {
            DisposeSocket();
            DisposeTimers();
            DisposeDriver();
        }

        private void DisposeTimers()
        {
            try
            {
                if (_pingTimer != null)
                {
                    if (_pingTimer.Enabled)
                        _pingTimer.Stop();
                    _pingTimer.Dispose();
                    _pingTimer = null;
                }
            }
            catch { /*ignored*/ }
            try
            {
                if (_keepAliveTimer != null)
                {
                    if (_keepAliveTimer.Enabled)
                        _keepAliveTimer.Stop();
                    _keepAliveTimer.Dispose();
                    _keepAliveTimer = null;
                }
            }
            catch { /*ignored*/}
        }

        private void InitSocket()
        {
            _client = new TcpWaifu {ReadTimeout = 90000};
            _client.OnConnectionTimedout += OnClientConnectionTimedout;
            _client.OnReadTimedout += OnClientReadTimedout;
            _client.OnWriteTimedout += OnClientWriteTimedout;

            _proxy = Collections.Proxies.GetNext().ToWebProxy();
        }

        private void OnClientReadTimedout(object sender, EventArgs e)
        {
            const string t = "Timed out when reading data";
            Disconnect(t);
        }

        private void OnClientWriteTimedout(object sender, EventArgs e)
        {
            const string t = "Timed out when writing data";
            Disconnect(t);
        }

        private void OnClientConnectionTimedout(object sender, EventArgs e)
        {
            const string t = "Connection timed out";
            Disconnect(t);
        }

        public void BeginSendNextMessage()
        {
            if (_beginSendNextMessageAt > DateTime.Now)
                return;

            if (!_msgLock.Wait(0))
                return;

            if (_account.PendingMessages.Count == 0 &&
                _account.PendingGreets.Count == 0)
            {
                ReleaseMsgLock();
                return;
            }

            OutgoingMessage msg = null;
            if (_account.PendingMessages.Count > 0)
                msg = _account.PendingMessages.GetNext(false);
            else if (_account.PendingGreets.Count > 0)
                msg = _account.PendingGreets.GetNext(false);

            if (msg == null)
            {
                ReleaseMsgLock();
                return;
            }

            const string sendMsgDelay = "SendMessageDelay";
            var seconds = Settings.GetRandom(sendMsgDelay);
            _beginSendNextMessageAt = DateTime.Now.AddSeconds(seconds);

            Task.Factory.StartNew(() => HandleOutgoingMessage(msg),
                    TaskCreationOptions.LongRunning)
                .ConfigureAwait(false);
        }

#if BLASTER
        public void BeginSendNextBlast()
        {
            const string disableBlasting = "DisableBlasting";
            if (Settings.Get<bool>(disableBlasting))
                return;

            if (_sendNextBlastAt > DateTime.Now)
                return;

            if (_resumeBlastingAt > DateTime.Now)
                return;

            const string blastDelay = "BlastDelay";
            var seconds = Settings.GetRandom(blastDelay);
            _sendNextBlastAt = DateTime.Now.AddSeconds(seconds);

            if (Collections.Contacts.ReachedEOF)
                return;

            Task.Run(() => BeginSendNextBlastWork())
                .ConfigureAwait(false);
        }

        private void BeginSendNextBlastWork()
        {
            var contact = Collections.Contacts.GetNext();
            if (string.IsNullOrWhiteSpace(contact))
                return;

            if (Blacklists.Collections[BlacklistType.Blast].Contains(contact) ||
                Blacklists.Collections[BlacklistType.Chat].Contains(contact))
                return;

            var greet = Collections.Greets.GetNext();
            if (string.IsNullOrWhiteSpace(greet))
                return;

            var msg = new OutgoingMessage(contact, greet)
            {
                IsBlast = true
            };

            AddBlacklist(BlacklistType.Blast, contact);

            EnqueueOrSendMessage(msg);

            if (++_blastsSent < _maxBlasts)
                return;

            const string blastsPerDelay = "BlastsPerSession";
            _maxBlasts = Settings.GetRandom(blastsPerDelay);
            _blastsSent = 0;

            const string blastSessionDelay = "BlastSessionDelay";
            var minutes = Settings.GetRandom(blastSessionDelay);
            _resumeBlastingAt = DateTime.Now.AddMinutes(minutes);
        }
#endif
    }
}
