using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using KikBot2.Declarations;
using DankLibWaifuz;
using DankLibWaifuz.TcpWaifu;
using DankLibWaifuz.CollectionsWaifu;
using DankLibWaifuz.Etc;
using System.Threading;
using KikWaifu;
using System.Net.Sockets;
using System.Security.Authentication;
using System.IO;
using System.Text.RegularExpressions;
using DankLibWaifuz.ScriptWaifu;
using System.Windows.Controls;
using DankLibWaifuz.SettingsWaifu;
using System.Text;
using System.Timers;

namespace KikBot2.Work
{
    internal enum LoginState
    {
        Disconnected,
        Connecting,
        Connected
    }
    
    internal class Bot : Mode
    {
        private readonly Account _account;
        private readonly SemaphoreSlim _sslStreamLock = new SemaphoreSlim(1, 1);
        private readonly TextBox _chatLog;

        public Bot(int index, ObservableCollection<DataGridItem> collection, Account account, TextBox chatLog) : base(index, collection)
        {
            _account = account;
            _chatLog = chatLog;
            UpdateAccountColumn(account.LoginId);
        }

        public LoginState LoginState { get; private set; }
        
        public string LastDisconnectReason { get; private set; }
        public bool Return { get; private set; }

        private TcpAsyncWaifu _tcpWaifu;
        private DateTime _lastConnectionAttempt;
        private System.Timers.Timer _pingTimer;
        private System.Timers.Timer _keepAliveTimer;

        private void InitSocket()
        {
            DisposeSocket();

            var proxy = Collections.Proxies.GetNext().ToWebProxy();
            _tcpWaifu = new TcpAsyncWaifu
            {
                Proxy = proxy
            };
        }

        public void DisposeSocket()
        {
            if (_tcpWaifu == null)
                return;

            _tcpWaifu.Close();
            _tcpWaifu = null;
        }

        private async Task<bool> TooManyLoginErrors()
        {
            if (!_account.TooManyLoginErrors)
                return false;

            await UpdateThreadStatusAsync("Too many login errors").ConfigureAwait(false);
            Return = true;
            return true;
        }

        //private async Task<bool> ShouldConnect()
        //{
        //    if (_lastConnectionAttempt.AddSeconds(60) <= DateTime.Now)
        //        return true;

        //    await UpdateThreadStatusAsync($"Sleeping ({_account.LoginErrors}): d/c reason = {LastDisconnectReason}").ConfigureAwait(false);
        //    return false;
        //}

        public bool ShouldConnect
        {
            get
            {
                if (_lastConnectionAttempt.AddSeconds(60) <= DateTime.Now)
                    return true;

                UpdateThreadStatus($"Sleeping ({_account.LoginErrors}): d/c reason = {LastDisconnectReason}");
                return false;
            }
        }

        private void InitProperties()
        {
            _lastConnectionAttempt = DateTime.Now;
            LoginState = LoginState.Connecting;

            if (_sslStreamLock.CurrentCount != 1)
                _sslStreamLock.Release();

            _account.Sid = Guid.NewGuid().ToString();
        }

        public async Task Connect()
        {
            try
            {
                if (await TooManyLoginErrors().ConfigureAwait(false))
                    return;

                InitSocket();
                InitProperties();

                await UpdateThreadStatusAsync("Connecting: ...", 1000).ConfigureAwait(false);

                await _tcpWaifu.ConnectWithProxyAsync(Konstants.KikEndPoint, Konstants.KikPort).ConfigureAwait(false);
                await _tcpWaifu.InitSslStreamAsync(Konstants.KikEndPoint).ConfigureAwait(false);

                if (!await InitStream())
                    return;

                await OnConnected().ConfigureAwait(false);
                await Task.Run(() => ReceiveData().ConfigureAwait(false)).ConfigureAwait(false);
            }
            catch (SocketException)
            {
                await OnException("Socket exception @ Connect()").ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                await OnException("Object disposed exception @ Connect()").ConfigureAwait(false);
            }
            catch (AuthenticationException)
            {
                await OnException("SSL authentication exception @ Connect()").ConfigureAwait(false);
            }
            catch (IOException)
            {
                await OnException("Socket I/O exception @ Connect()").ConfigureAwait(false);
            }
            catch (Exception e)
            {
                await OnException("Exception occured @ Connect()", e).ConfigureAwait(false);
            }
        }

        private async void PingCallback(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (LoginState != LoginState.Connected)
                    return;

                if ((_pingTimer.Interval = _pingTimer.Interval * 2) >= 30000.0)
                    _pingTimer.Interval = 30000.0;

                await SendData(Packets.Ping()).ConfigureAwait(false);
            }
            catch
            {
                await OnException("Exception occured @ PingCallback()").ConfigureAwait(false);
            }
        }

        private async Task<bool> InitStream()
        {
            const string s = "Initializing stream";
            await AttemptingAsync(s).ConfigureAwait(false);

            var ts = Krypto.KikTimestamp();
            var deviceTsHash = Krypto.KikHmacSha1Signature(ts, _account.Jid);
            var signed = await Task.Run(() => Krypto.KikRsaSign(_account.Jid, Konstants.AppVersion, ts, _account.Sid)).ConfigureAwait(false);
            const int nVal = 10;
            var data = await Packets.StreamInitPropertyMapAsync($"{_account.Jid}/{_account.DeviceCanId}", _account.PasskeyU, deviceTsHash, long.Parse(ts), signed, _account.Sid, nVal).ConfigureAwait(false);
            if (data.IsNullOrEmpty())
            {
                await OnConnectionFailed("Failed to get stream init property map").ConfigureAwait(false);
                return false;
            }

            await SendData(data).ConfigureAwait(false);

            var buffer = new byte[8192];
            var cnt = await _tcpWaifu.ReceiveDataAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            var response = new byte[cnt];
            Array.Copy(buffer, response, cnt);

            if (response.IsNullOrEmpty() || !response.Utf8String().Contains("ok"))
            {
                await OnConnectionFailed("Unexpected response after authorization attempt").ConfigureAwait(false);
                return false;
            }

            return true;
        }

        private async Task SendData(byte[] data)
        {
            await _sslStreamLock.WaitAsync().ConfigureAwait(false);

            try
            {
                //if (_pingTimer != null && !data.SequenceEqual(Packets.Ping()))
                //    _pingTimer.Interval = 1000;

                //Console.WriteLine("Sent: " + data.Utf8String());

#if DEBUG
                Console.WriteLine(@"==> " + data.Utf8String());
#endif

                await _tcpWaifu.SendDataAsync(data).ConfigureAwait(false);
            }
            finally
            {
                _sslStreamLock.Release();
            }
        }

        private async Task OnConnected()
        {
            _account.LoginErrors = 0;
            LoginState = LoginState.Connected;
            await SendData(Packets.GetUnackedMsgs()).ConfigureAwait(false);
            await UpdateThreadStatusAsync("Connected").ConfigureAwait(false);
        }

        private static readonly Regex
          MessageRegex = new Regex("<(message|msg) (.*?)</(message|msg)>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline),
          MsgTypeRegex = new Regex("type=\"(.*?)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline),
          MsgIdRegex = new Regex("id=\"(.*?)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline),
          MsgFromRegex = new Regex("from=\"(.*?)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline),
          MsgBodyRegex = new Regex("<body>(.*?)</body>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private async Task ReceiveData()
        {
            _pingTimer = new System.Timers.Timer(1000.0);
            _pingTimer.Elapsed += PingCallback;
            _pingTimer.Start();

            _keepAliveTimer = new System.Timers.Timer(1200000.0);
            _keepAliveTimer.Elapsed += KeepAliveCallback;
            _keepAliveTimer.Start();

            try
            {
                var sb = new StringBuilder();
                while (true)
                {
                    var buffer = new byte[8192];

                    var cnt = await _tcpWaifu.ReceiveDataAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                    var data = new byte[cnt];
                    Array.Copy(buffer, data, cnt);

                    if (data.IsNullOrEmpty())
                    {
                        await OnDisconnect("Received 0").ConfigureAwait(false);
                        return;
                    }

                    sb.Append(data.Utf8String());

                    await Task.Delay(100).ConfigureAwait(false);

                    if (_tcpWaifu.Available > 0)
                        continue;

                    var dataReceived = sb.ToString();
                    sb.Clear();

#if DEBUG
                    Console.WriteLine(@"<== " + dataReceived);
#endif

                    await HandleReceivedData(dataReceived).ConfigureAwait(false);
                }

                
            }
            catch (Exception e)
            {
                await OnException("Exception occured @ ReceiveData()", e).ConfigureAwait(false);
            }
            finally
            {
                if (_pingTimer != null)
                {
                    _pingTimer.Stop();
                    _pingTimer.Dispose();
                    _pingTimer = null;
                }

                if (_keepAliveTimer != null)
                {
                    _keepAliveTimer.Stop();
                    _keepAliveTimer.Dispose();
                    _keepAliveTimer = null;
                }
            }
                
        }

        private async void KeepAliveCallback(object sender, ElapsedEventArgs e)
        {
            try
            {
                foreach (var jid in _account.Convos.Keys.ToList())
                {
                    if (Blacklists.Dict[BlacklistType.Chat].Contains(jid))
                        continue;

                    if (_account.Convos[jid].IsComplete)
                        continue;

                    var maxKeepAlives = Settings.Get<int>("MaxKeepAlives");
                    if (_account.Convos[jid].KeepAlivesSent >= maxKeepAlives)
                        continue;

                    if (_account.Convos[jid].HaveSentKeepAlive)
                        continue;

                    var keepAliveDelay = Settings.Get<int>("KeepAliveDelay");

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
                    await Task.Run(() => HandleOutgoingMessage(msg).ConfigureAwait(false)).ConfigureAwait(false);
                }
            } catch { /*ignored haha C# fags triggered empty catches for life lul*/ }
        }

        private async Task HandleReceivedData(string xml)
        {
            //Console.WriteLine("Received: " + xml);

            var matches = MessageRegex.Matches(xml);
            if (matches.Count == 0)
                return;

            foreach (Match match in matches)
            {
                var messageXml = match.Groups[2].Value;
                if (string.IsNullOrWhiteSpace(messageXml))
                    continue;

                string msgType;
                if (!MsgTypeRegex.TryGetGroup(messageXml, out msgType) || msgType.ToLower() != "chat")
                    continue;

                string msgId;
                if (!MsgIdRegex.TryGetGroup(messageXml, out msgId))
                    continue;

                string fromJid;
                if (!MsgFromRegex.TryGetGroup(messageXml, out fromJid))
                    continue;

                string body;
                MsgBodyRegex.TryGetGroup(messageXml, out body);


                await SendData(Packets.MessageDelivered(fromJid, msgId)).ConfigureAwait(false);
                await SendData(Packets.MessageRead(fromJid, msgId)).ConfigureAwait(false);

                var rcvdOffline = match.Groups[1].Value.ToLower() == "msg";

                if (rcvdOffline)
                {
                    var msgBlacklistInput = $"{fromJid}:{_account.Jid}:{msgId}";
                    if (Blacklists.Dict[BlacklistType.Message].Contains(msgBlacklistInput) || fromJid == "kikteam@talk.kik.com")
                        continue;

                    await AddBlacklistAsync(BlacklistType.Message, msgBlacklistInput).ConfigureAwait(false);
                }

                if (messageXml.Contains("started chatting with you") ||
                    messageXml.Contains("phone has been off/disconnected for a while."))
                    continue;

                if (Blacklists.Dict[BlacklistType.Chat].Contains(fromJid))
                    continue;

                OnMessageReceived(fromJid, body);

                await HandleIncomingMessage(fromJid, body).ConfigureAwait(false);
            }
        }

        private void OnMessageReceived(string from, string body)
        {
            Interlocked.Increment(ref Stats.In);
            UpdateInStat(++_account.In);

            UpdateChatLog(ChatLogType.Incoming, from, body);
        }

        internal enum ChatLogType
        {
            Incoming,
            Outgoing
        }

        private void UpdateChatLog(ChatLogType chatLogType, string otherUid, string body)
        {
            if (!Settings.Get<bool>("ChatLogEnabled"))
                return;

            var arrow = chatLogType == ChatLogType.Incoming ? "<==" : "==>";
            var str = $"[{DateTime.Now.ToShortTimeString()}] {_account.LoginId} {arrow} {otherUid}: {body}";
            _chatLog.Dispatcher.BeginInvoke(new Action(() =>
            {
                _chatLog.AppendText($"{str}{Environment.NewLine}");
            }));
        }

        private void UpdateInStat(int val)
        {
            Collection[Index].InCount = val;
        }

        private void UpdateOutStat(int val)
        {
            Collection[Index].OutCount = val;
        }

        private async Task HandleIncomingMessage(string fromJid, string bodyReceived)
        {
            if (!_account.Convos.ContainsKey(fromJid))
            {
                lock (_account.Convos)
                    _account.Convos.Add(fromJid, new ScriptWaifu(Collections.Script));

                Interlocked.Increment(ref Stats.Convos);
            }

            _account.Convos[fromJid].LastMessageReceivedAt = DateTime.Now;

            if (ScriptWaifu.HasRestrictedKeyword(bodyReceived, Collections.Restricts))
            {
                await AddBlacklistAsync(BlacklistType.Chat, fromJid);

                Interlocked.Increment(ref Stats.Restricts);

                var apology = ScriptWaifu.Spin(Collections.Apologies.GetNext());
                if (string.IsNullOrWhiteSpace(apology))
                    apology = "Sorry";

                var msg = new OutgoingMessage(fromJid, apology);
                await Task.Run(() => HandleOutgoingMessage(msg).ConfigureAwait(false)).ConfigureAwait(false);
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
                    await Task.Run(() => HandleOutgoingMessage(msg).ConfigureAwait(false)).ConfigureAwait(false);
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
                await AddBlacklistAsync(BlacklistType.Chat, fromJid).ConfigureAwait(false);
                Interlocked.Increment(ref Stats.Completed);
            }

            {
                var msg = new OutgoingMessage(fromJid, reply);
                await Task.Run(() => HandleOutgoingMessage(msg).ConfigureAwait(false)).ConfigureAwait(false);
            }
        }

        private async Task HandleOutgoingMessage(OutgoingMessage message)
        {
            try
            {
                await Task.Delay(Random.Next(5000, 11000)).ConfigureAwait(false);

                var sendMessageDelaySeconds = Settings.GetRandom("SendMessageDelay") * 1000;
                await Task.Delay(sendMessageDelaySeconds).ConfigureAwait(false);

                if (message.IsImage)
                {
                    await HandleImageMessage(message).ConfigureAwait(false);
                    return;
                }

                var messageBody = message.Body;
                if (message.IsLink && Settings.Get<bool>("UseKikBrowser"))
                {
                    await HandleLinkMessage(message).ConfigureAwait(false);
                    return;
                }
                else if (message.IsLink && !Settings.Get<bool>("UseKikBrowser"))
                    messageBody = message.Body.Replace("%s", Collections.Links.GetNext());

                await SendData(Packets.IsTyping(message.ToJid)).ConfigureAwait(false);
                await GeneralHelpers.TypingDelayAsync(messageBody).ConfigureAwait(false);
                await SendData(Packets.SendMessage(Guid.NewGuid().ToString(), message.ToJid, messageBody)).ConfigureAwait(false);

                OnMessageSent(message);
            }
            catch (Exception e)
            {
                await OnException("Exception occured @ HandleOutgoingMessage()", e).ConfigureAwait(false);
            }
            finally
            {
                if (_account.Convos.ContainsKey(message.ToJid))
                    _account.Convos[message.ToJid].Pending = false;
            }
        }

        private async Task HandleLinkMessage(OutgoingMessage message)
        {
            var split = Regex.Split(message.Body, "%s", RegexOptions.Compiled);
            if (split.Length < 1)
                return;

            SpoofedLinkInfo spoofedLinkInfo;
            if (!TryGetNextSpoofedLinkInfo(out spoofedLinkInfo))
                return;

            var sentMessage = false;

            try
            {
                var firstMessage = split[0].Replace("%s", string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(firstMessage))
                    return;

                await SendData(Packets.IsTyping(message.ToJid)).ConfigureAwait(false);
                await GeneralHelpers.TypingDelayAsync(firstMessage).ConfigureAwait(false);
                await SendData(Packets.SendMessage(Guid.NewGuid().ToString(), message.ToJid, firstMessage)).ConfigureAwait(false);

                var link = Collections.Links.GetNext();
                if (string.IsNullOrWhiteSpace(link))
                    return;

                var shareLink = new ShareLink(message.ToJid, link,
                    spoofedLinkInfo.SpoofedDomain, spoofedLinkInfo.LinkTitle,
                    spoofedLinkInfo.AppName);

                await Task.Delay(Random.Next(6000, 9000)).ConfigureAwait(false);

                await SendData(Packets.ShareLink(shareLink)).ConfigureAwait(false);

                sentMessage = true;

                if (split.Length < 2 || string.IsNullOrWhiteSpace(split[1]))
                    return;

                var secondMessage = split[1].Replace("%s", string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(secondMessage))
                    return;

                await Task.Delay(Random.Next(6000, 9000)).ConfigureAwait(false);

                await SendData(Packets.IsTyping(message.ToJid)).ConfigureAwait(false);
                await GeneralHelpers.TypingDelayAsync(secondMessage).ConfigureAwait(false);
                await SendData(Packets.SendMessage(Guid.NewGuid().ToString(), message.ToJid, secondMessage)).ConfigureAwait(false);
            }
            finally
            {
                if (sentMessage)
                    OnMessageSent(message);
            }
        }
        
        private static readonly Regex ImageMessageIndexRegex = new Regex("%p(\\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private async Task HandleImageMessage(OutgoingMessage message)
        {
            if (Collections.ImageFiles == null || Collections.ImageFiles.Count == 0)
                return;

            string strIndex;
            if (!ImageMessageIndexRegex.TryGetGroup(message.Body, out strIndex))
                return;

            int index;
            if (!int.TryParse(strIndex, out index))
                return;

            if (index >= Collections.ImageFiles.Count)
                return;

            var split = Regex.Split(message.Body, "%p\\d+");
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
                var firstMessage = Regex.Replace(split[0], "%p\\d+", string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(firstMessage))
                    return;

                await SendData(Packets.IsTyping(message.ToJid)).ConfigureAwait(false);
                await GeneralHelpers.TypingDelayAsync(firstMessage).ConfigureAwait(false);
                await SendData(Packets.SendMessage(Guid.NewGuid().ToString(), message.ToJid, firstMessage)).ConfigureAwait(false);

                await Task.Delay(Random.Next(6000, 9000)).ConfigureAwait(false);

                var imageDataLength = randomziedImageData.Length;
                var base64DImageData = Convert.ToBase64String(randomziedImageData);

                var shareImage = new ShareImage(message.ToJid, imageDataLength, base64DImageData);
                await SendData(Packets.ShareImage(shareImage)).ConfigureAwait(false);

                sentMessage = true;

                if (split.Length < 2 || string.IsNullOrWhiteSpace(split[1]))
                    return;

                await Task.Delay(Random.Next(6000, 9000)).ConfigureAwait(false);

                var secondMessage = Regex.Replace(split[1], "%p\\d+", string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(secondMessage))
                    return;

                await SendData(Packets.IsTyping(message.ToJid)).ConfigureAwait(false);
                await GeneralHelpers.TypingDelayAsync(secondMessage).ConfigureAwait(false);
                await SendData(Packets.SendMessage(Guid.NewGuid().ToString(), message.ToJid, secondMessage)).ConfigureAwait(false);
            }
            finally
            {
                if (sentMessage)
                    OnMessageSent(message);
            }

        }

        private static bool TryGetNextSpoofedLinkInfo(out SpoofedLinkInfo spoofedLinkInfo)
        {
            spoofedLinkInfo = null;

            for (var i = 0; i < 5; i++)
            {
                var spoofedLinkInfoStr = Collections.SpoofedLinkInfo.GetNext();
                if (string.IsNullOrWhiteSpace(spoofedLinkInfoStr))
                    continue;

                spoofedLinkInfo = new SpoofedLinkInfo(spoofedLinkInfoStr);
                if (spoofedLinkInfo.IsValid)
                    return true;
            }

            return false;
        }

        private void OnMessageSent(OutgoingMessage message)
        {
            UpdateChatLog(ChatLogType.Outgoing, message.ToJid, message.Body);

            if (message.IsKeepAlive)
            {
                Interlocked.Increment(ref Stats.KeepAlives);
                return;
            }

            Interlocked.Increment(ref Stats.Out);
            Interlocked.Increment(ref _account.Out);
            UpdateOutStat(_account.Out);

            if (message.IsLink)
                Interlocked.Increment(ref Stats.Links);
        }

        private async Task OnException(string reason, Exception e = null)
        {
            switch (LoginState)
            {
                case LoginState.Connecting:
                    await OnConnectionFailed(reason).ConfigureAwait(false);
                    break;

                default:
                    await OnDisconnect(reason).ConfigureAwait(false);
                    break;
            }

            if (e == null)
                return;

            await ErrorLogger.WriteAsync(e).ConfigureAwait(false);
        }

        private async Task OnDisconnect(string reason)
        {
            LastDisconnectReason = reason;
            LoginState = LoginState.Disconnected;

            await UpdateThreadStatusAsync($"Disconnected: {reason}").ConfigureAwait(false);
        }

        private async Task OnConnectionFailed(string reason)
        {
            LastDisconnectReason = reason;
            LoginState = LoginState.Disconnected;
            _account.LoginErrors++;
            await UpdateThreadStatusAsync($"Connection failed: {reason})").ConfigureAwait(false);
        }
    }
}
