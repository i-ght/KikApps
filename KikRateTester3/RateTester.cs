using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using DankWaifu.Collections;
using DankWaifu.Net;
using DankWaifu.Sys;
using DankWaifu.Tasks;

namespace KikRateTester3
{
    internal class RateTester : Mode
    {
        private static readonly SemaphoreSlim ConnectionLock;


        static RateTester()
        {
            ConnectionLock = new SemaphoreSlim(15);
        }

        private readonly Collections _collections;
        private readonly GlobalStats _globalStats;
        private readonly DataGridItem _senderUI;

        public RateTester(int index, DataGridItem ui, DataGridItem senderUI, Collections collections, GlobalStats stats) : base(index, ui)
        {
            _collections = collections;
            _globalStats = stats;
            _senderUI = senderUI;
        }


        private async Task<WebProxy> NextRecvProxy()
        {
            while (true)
            {
                while (_collections.ReceiveProxies.Count == 0)
                {
                    await WaitingForInputFile("recv proxies")
                        .ConfigureAwait(false);
                }

                var proxyStr = _collections.ReceiveProxies.GetNext();
                if (NetHelpers.TryParseProxy(proxyStr, out var proxy))
                    return proxy;
            }
        }

        private enum SendOrRecv
        {
            Send,
            Recv
        }

        private async Task<Klient> Connect(Account acct, WebProxy proxy, SendOrRecv t)
        {
            var klient = new Klient(acct);

            switch (t)
            {
                case SendOrRecv.Recv:
                    await UpdateThreadStatusAsync("Waiting for connection lock ...", 1000)
                        .ConfigureAwait(false);
                    break;

                case SendOrRecv.Send:
                    await UpdateSendThreadStatusAsync("Waiting for connection lock ...", 1000)
                        .ConfigureAwait(false);
                    break;
            }

            switch (t)
            {
                case SendOrRecv.Recv:
                    await UpdateThreadStatusAsync("Connecting to kik chat server: ...", 1000)
                        .ConfigureAwait(false);
                    break;

                case SendOrRecv.Send:
                    await UpdateSendThreadStatusAsync("Connecting to kik chat server: ...", 1000)
                        .ConfigureAwait(false);
                    break;
            }


            acct.Sid = Guid.NewGuid().ToString();
            await klient.Connect(proxy)
                .ConfigureAwait(false);

            await klient.InitStream()
                .ConfigureAwait(false);

            return klient;
        }

        private async Task<WebProxy> NextSendProxy()
        {
            while (true)
            {
                while (_collections.SendProxies.Count == 0)
                {
                    await WaitingForInputFile("send proxies")
                        .ConfigureAwait(false);
                }

                var proxyStr = _collections.SendProxies.GetNext();
                if (NetHelpers.TryParseProxy(proxyStr, out var proxy))
                    return proxy;
            }
        }

        private async Task<(Klient ReceiveClient, Klient SendClient)> ConnectClients(Account recvAcct,
            Account sendAcct, WebProxy recvProxy, WebProxy sendProxy, CancellationTokenSource c)
        {
            var recvKlient = await Connect(recvAcct, recvProxy, SendOrRecv.Recv)
                .ConfigureAwait(false);
            UpdateThreadStatus("Connected");
            recvAcct.LoginErrors = 0;
            UpdateSendThreadStatus("Connecting ...");
            var sendKlient = await Connect(sendAcct, sendProxy, SendOrRecv.Send)
                .ConfigureAwait(false);
            sendAcct.LoginErrors = 0;
            return (recvKlient, sendKlient);
        }


        public async Task BaseAsync()
        {
            try
            {
                while (_collections.ReceiveAccounts.Count > 0 && _collections.SendAccounts.Count > 0)
                {

                    Account recvAcct = null;
                    Account sendAcct = null;

                    try
                    {
                        recvAcct = _collections.ReceiveAccounts.GetNext(false);
                        UI.Account = recvAcct.LoginId;

                        sendAcct = _collections.SendAccounts.GetNext(false);
                        _senderUI.Account = sendAcct.LoginId;
                        UpdateSendThreadStatus("Waiting for recv acct to be online...");

                        var recvProxy = await NextRecvProxy()
                            .ConfigureAwait(false);
                        var sendProxy = await NextSendProxy()
                            .ConfigureAwait(false);

                        var sendWriteLock = new SemaphoreSlim(1, 1);
                        var recvWriteLock = new SemaphoreSlim(1, 1);

                        UpdateThreadStatus("Connecting ...");
                        using (var c = new CancellationTokenSource(40000))
                        {
                            await ConnectionLock.WaitAsync()
                                .ConfigureAwait(false);
                            (Klient ReceiveClient, Klient SendClient) clients;
                            try
                            {
                                clients = await ConnectClients(recvAcct, sendAcct, recvProxy, sendProxy, c)
                                    .ConfigureAwait(false);
                            }
                            finally
                            {
                                ConnectionLock.Release();
                            }

                            using (var recvKlient = clients.ReceiveClient)
                            using (var sendKlient = clients.SendClient)
                            {
                                var recvRecvLoop = RecvLoop(c.Token, recvKlient.Client, recvAcct, sendAcct.Jid);
                                var recvPingLoop = PingLoop(c.Token, recvWriteLock, recvKlient);

                                var sendRecvLoop = RecvLoop(c.Token, sendKlient.Client, sendAcct,
                                    string.Empty);
                                var sendPingLoop = PingLoop(c.Token, sendWriteLock, sendKlient);

                                UpdateSendThreadStatus("Connected");

#if !DEBUG
                                var delay = RandomHelpers.RandomInt(5000, 8000);
#else
                                    var delay = RandomHelpers.RandomInt(2222, 5555);
#endif

                                await Task.Delay(delay, c.Token)
                                    .ConfigureAwait(false);

                                var msgId = Guid.NewGuid().ToString();
                                var msg = StringHelpers.RandomString(RandomHelpers.RandomInt(8, 14),
                                    StringDefinition.DigitsAndLowerLetters);
                                await sendKlient.SendMessage(msgId, recvAcct.Jid, msg)
                                    .ConfigureAwait(false);
                                await recvRecvLoop;
                                c.Cancel();
                                await sendRecvLoop;
                                await recvPingLoop;
                                await sendPingLoop;
                            }
                        }

                    }
                    catch (TaskCanceledException)
                    {
                        UpdateSendThreadStatus("Timed out");
                        await UpdateThreadStatusAsync("Timed out",
                                10000)
                            .ConfigureAwait(false);

                        UpdateSendThreadStatus("Sleeping before reconnect");
                        await UpdateThreadStatusAsync("Sleeping before reconnect...",
                                10000)
                            .ConfigureAwait(false);

                    }
                    catch (Exception e)
                    {
                        UpdateSendThreadStatus($"{e.GetType().Name}: {e.Message}");
                        await OnExceptionAsync(e)
                            .ConfigureAwait(false);
                        UpdateSendThreadStatus("Sleeping before reconnect");
                        await UpdateThreadStatusAsync("Sleeping before reconnect...",
                                10000)
                            .ConfigureAwait(false);

                        if (recvAcct != null)
                            recvAcct.LoginErrors++;
                        if (sendAcct != null)
                            sendAcct.LoginErrors++;
                    }
                    finally
                    {
                        if (recvAcct != null)
                        {
                            if (recvAcct.LoginErrors < Settings.Get<int>(Constants.MaxLoginErrors) && !recvAcct.MsgReceived)
                                _collections.ReceiveAccounts.Enqueue(recvAcct);
                        }

                        if (sendAcct != null)
                        {
                            if (sendAcct.LoginErrors < Settings.Get<int>(Constants.MaxLoginErrors))
                                _collections.SendAccounts.Enqueue(sendAcct);
                        }

                    }
                }
            }
            finally
            {
                if (!_collections.WorkComplete)
                    _collections.WorkComplete = true;
                UI.Account = string.Empty;
                UI.Status = string.Empty;
                _senderUI.Account = string.Empty;
                _senderUI.Status = string.Empty;
            }
        }
        //public async Task BaseAsync()
        //{
        //    try
        //    {
        //        while (_collections.ReceiveAccounts.Count > 0)
        //        {
        //            var acct = _collections.ReceiveAccounts.GetNext(false);
        //            UI.Account = acct.LoginId;

        //            try
        //            {
        //                var proxy = await NextRecvProxy()
        //                    .ConfigureAwait(false);

        //                using (var klient = new Klient(acct))
        //                {
        //                    await UpdateThreadStatusAsync("Waiting for connection lock...", 1000)
        //                        .ConfigureAwait(false);

        //                    await ConnectionLock.WaitAsync()
        //                        .ConfigureAwait(false);

        //                    var loginState = new LoginState();
        //                    var writeLock = new SemaphoreSlim(1, 1);

        //                    try
        //                    {
        //                        await UpdateThreadStatusAsync("Connecting to kik chat server: ...", 1000)
        //                            .ConfigureAwait(false);

        //                        acct.Sid = Guid.NewGuid().ToString();
        //                        await klient.Connect(proxy)
        //                            .ConfigureAwait(false);

        //                        await klient.InitStream()
        //                            .ConfigureAwait(false);
        //                    }
        //                    finally
        //                    {
        //                        ConnectionLock.Release();
        //                    }

        //                    using (var c = new CancellationTokenSource())
        //                    {
        //                        c.Token.ThrowIfCancellationRequested();

        //                        var recvLoop = RecvLoop(klient.Client.NetworkStream, loginState, acct);
        //                        var pingLoop = PingLoop(c.Token, writeLock, klient);

        //                        await Task.WhenAll(recvLoop, pingLoop)
        //                            .ConfigureAwait(false);
        //                    }
        //                }
        //            }
        //            catch (TaskCanceledException tEx)
        //            {

        //            }
        //            catch (Exception e)
        //            {
        //                await OnExceptionAsync(e)
        //                    .ConfigureAwait(false);
        //                await UpdateThreadStatusAsync($"Sleeping before reconnect... ({++acct.LoginErrors})",
        //                        10000)
        //                    .ConfigureAwait(false);
        //            }
        //            finally
        //            {
        //                if (acct.LoginErrors < Settings.Get<int>(Constants.MaxLoginErrors))
        //                    _collections.ReceiveAccounts.Enqueue(acct);
        //            }
        //        }
        //    }
        //    finally
        //    {
        //        if (!_collections.WorkComplete)
        //            _collections.WorkComplete = true;
        //    }
        //}

        private async Task PingLoop(CancellationToken c, SemaphoreSlim writeLock, Klient klient)
        {
            var interval = 1000;
            while (!c.IsCancellationRequested)
            {
                await Task.Delay(interval)
                    .ConfigureAwait(false);

                if ((interval = interval * 2) > 30000)
                    interval = 30000;

                await writeLock.WaitAsync()
                    .ConfigureAwait(false);

                try
                {
                    await klient.Ping()
                        .ConfigureAwait(false);
                }
                finally
                {
                    writeLock.Release();
                }
            }
        }
        //ayyyyyyye lol this is really good
        private static readonly Regex
            MessageRegex = new Regex("<(message|msg) (.*?)</(message|msg)>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline),
            MsgTypeRegex = new Regex("type=\"(.*?)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline),
            MsgIdRegex = new Regex("id=\"(.*?)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline),
            MsgFromRegex = new Regex("from=\"(.*?)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline),
            MsgBodyRegex = new Regex("<body>(.*?)</body>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);


        private async Task RecvLoop(CancellationToken c, TcpClientWrapper client, Account acct, string senderJid)
        {
            var sb = new StringBuilder();
            while (!c.IsCancellationRequested)
            {
                var buffer = new byte[4096];
                var cnt = await client.ReceiveDataAsync(buffer, 0, buffer.Length)
                    .ConfigureAwait(false);
                if (cnt == 0)
                {
                    throw new InvalidOperationException("Send account received 0");
                }

                var str = Encoding.UTF8.GetString(buffer, 0, cnt);
                sb.Append(str);

                await Task.Delay(100)
                    .ConfigureAwait(false);
                if (client.Available > 0)
                    continue;

                str = sb.ToString();

                if (MessageRegex.TryGetGroup(str, out var msgXml, 2))
                {
                    if (MsgFromRegex.TryGetGroup(msgXml, out var fromJid))
                    {
                        if (fromJid == senderJid)
                        {

                            await _collections.Writelock.WaitAsync()
                                .ConfigureAwait(false);

                            try
                            {
                                using (var streamWriter = new StreamWriter("unrated-recv.txt", true))
                                {
                                    await streamWriter.WriteLineAsync(acct.ToString())
                                        .ConfigureAwait(false);
                                }
                            }
                            finally
                            {
                                _collections.Writelock.Release();
                            }

                            acct.MsgReceived = true;
                            Interlocked.Increment(ref _globalStats.Unrated);
                            return;
                        }
                    }
                }
#if DEBUG
                Console.WriteLine($@"{acct.LoginId} received: {str}");
#endif
                sb.Clear();
            }
        }

        protected void UpdateSendAccountColumn(string acct)
        {
            _senderUI.Account = acct;
        }

        protected void UpdateSendThreadStatus(string s)
        {
            _senderUI.Status = s;
        }

        protected void UpdateSendThreadStatus(string s, int delay)
        {
            UpdateSendThreadStatus(s);
            Thread.Sleep(delay);
        }

        protected async Task UpdateSendThreadStatusAsync(string s, int delay)
        {
            UpdateSendThreadStatus(s);
            await Task.Delay(delay)
                .ConfigureAwait(false);
        }

        protected async Task UpdateSendThreadStatusAsync(string s, int delay, CancellationToken c)
        {
            UpdateSendThreadStatus(s);
            await Task.Delay(delay, c)
                .ConfigureAwait(false);
        }

    }
}
