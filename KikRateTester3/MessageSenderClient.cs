using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using DankWaifu.Collections;
using DankWaifu.Net;
using DankWaifu.Sys;
using DankWaifu.Tasks;

namespace KikRateTester3
{
    internal class MessageSenderClient : Mode
    {
        private static readonly SemaphoreSlim ConnectionLock;

        static MessageSenderClient()
        {
            ConnectionLock = new SemaphoreSlim(10, 10);
        }

        private readonly Collections _collections;
        private readonly GlobalStats _globalStats;

        public MessageSenderClient(int index, DataGridItem ui, Collections collections, GlobalStats stats)
            : base(index, ui)
        {
            _collections = collections;
            _globalStats = stats;
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

        public async Task BaseAsync()
        {
            while (_collections.SendAccounts.Count > 0)
            {
                var acct = _collections.SendAccounts.GetNext(false);
                UI.Account = acct.LoginId;

                var sent5 = false;
                try
                {
                    var proxy = await NextSendProxy()
                        .ConfigureAwait(false);

                    using (var klient = new Klient(acct))
                    {
                        await UpdateThreadStatusAsync("Waiting for connection lock...", 1000)
                            .ConfigureAwait(false);

                        var writeLock = new SemaphoreSlim(1, 1);
                        var loginState = new LoginState();

                        await ConnectionLock.WaitAsync()
                            .ConfigureAwait(false);

                        try
                        {
                            await UpdateThreadStatusAsync("Connecting to kik chat server: ...", 1000)
                                .ConfigureAwait(false);

                            acct.Sid = Guid.NewGuid().ToString();
                            await klient.Connect(proxy)
                                .ConfigureAwait(false);

                            await klient.InitStream()
                                .ConfigureAwait(false);
                        }
                        finally
                        {
                            ConnectionLock.Release();
                        }

                        using (var c = new CancellationTokenSource())
                        {
                            c.Token.ThrowIfCancellationRequested();

                            var recvLoop = RecvLoop(klient.Client.NetworkStream, loginState, acct, c);
                            var pingLoop = PingLoop(c.Token, writeLock, klient);

                            var sent = 0;
                            while (!_collections.WorkComplete)
                            {
                                while (_collections.AccountsToMessage.Count == 0)
                                {
                                    await UpdateThreadStatusAsync("Waiting for account to message", 1000, c.Token)
                                        .ConfigureAwait(false);
                                }

                                var next = _collections.AccountsToMessage.GetNext(false);
                                if (_collections.SentBlacklist.Contains(next.LoginId))
                                    continue;



                                var msgId = Guid.NewGuid().ToString();

                            }
                        }
                    }
                }
                catch (TaskCanceledException tEx)
                {

                }
                catch (Exception e)
                {
                    await OnExceptionAsync(e)
                        .ConfigureAwait(false);
                    await UpdateThreadStatusAsync($"Sleeping before reconnect... ({++acct.LoginErrors})", 10000)
                        .ConfigureAwait(false);
                }

                if (!sent5)
                    continue;

                _collections.SendAccounts.Enqueue(acct);
                break;

            }
        }

        private async Task PingLoop(CancellationToken c, SemaphoreSlim writeLock, Klient klient)
        {
            var interval = 1000;
            while (!c.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(interval, c)
                       .ConfigureAwait(false);

                    if ((interval = interval * 2) > 30000)
                        interval = 30000;

                    await writeLock.WaitAsync(c)
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
                catch (TaskCanceledException
#if DEBUG
                tEx
#endif
                )
                {/**/}
            }
        }


        private async Task RecvLoop(Stream stream, LoginState loginState, Account acct, CancellationTokenSource c)
        {
            var online = false;
            try
            {
                using (var xmlReader = XmlReader.Create(stream, new XmlReaderSettings
                {
                    Async = true,
                    IgnoreProcessingInstructions = true,
                    IgnoreComments = true,
                    IgnoreWhitespace = true,
                    //ConformanceLevel = ConformanceLevel.Fragment
                }))
                {
                    while (await xmlReader.ReadAsync()
                        .ConfigureAwait(false))
                    {
                        await xmlReader.MoveToContentAsync()
                            .ConfigureAwait(false);

                        if (xmlReader.NodeType != XmlNodeType.Element)
                            continue;

                        using (var subtree = await Task.Run(() => xmlReader.ReadSubtree())
                            .ConfigureAwait(false))
                        {
                            while (await subtree.ReadAsync()
                                .ConfigureAwait(false))
                            {

                                if (subtree.NodeType != XmlNodeType.Element)
                                    continue;

                                switch (subtree.Name)
                                {
                                    case "k":
                                        var ok = subtree.GetAttribute("ok");
                                        if (ok != "1")
                                        {
                                            var tmp = await subtree.ReadOuterXmlAsync()
                                                .TimeoutAfter(10000)
                                                .ConfigureAwait(false);
                                            if (!tmp.Contains("Not Authorized"))
                                                throw new InvalidOperationException(
                                                    "Kik server returned unexpected response after sending stream init prop map");

                                            acct.LoginErrors = 99;
                                            throw new InvalidOperationException(
                                                "Kik server returned Not Authorized for this session");
                                        }

                                        Interlocked.Increment(ref _globalStats.SendOnline);
                                        UpdateThreadStatus("Connected");
                                        loginState.Index++;
                                        online = true;
                                        break;
                                    case "pong":
#if DEBUG
                                        Console.WriteLine($@"<== {await xmlReader.ReadOuterXmlAsync()
                                            .TimeoutAfter(10000)
                                            .ConfigureAwait(false)}");
#endif
                                        break;
                                }

#if DEBUG
                                var xml = await subtree.ReadOuterXmlAsync()
                                    .TimeoutAfter(1000)
                                    .ConfigureAwait(false);
                                Console.WriteLine($@"<== {xml}");
#endif


                            }

                        }
                    }

                    throw new InvalidOperationException("NetworkStream ended unexpectedly");
                }
            }
            catch (Exception)
            {
                c.Cancel();
                throw;
            }
            finally
            {
                if (online)
                    Interlocked.Decrement(ref _globalStats.SendOnline);
            }
        }
    }
}
