using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using DankWaifu.Net;
using DankWaifu.Sys;
using DankWaifu.Tasks;
using KikWaifu;

namespace KikRateTester3
{
    internal class Klient : IDisposable
    {
        private readonly Account _account;

        public Klient(Account account)
        {
            Client = new TcpClientWrapper
            {
                ReceiveTimeout = TimeSpan.FromSeconds(60),
                SendTimeout = TimeSpan.FromSeconds(60),
                ConnectTimeout = TimeSpan.FromSeconds(60)
            };
            _account = account;
        }

        public TcpClientWrapper Client { get; }

        public async Task Connect(WebProxy proxy)
        {
            if (proxy == null)
                throw new ArgumentNullException(nameof(proxy));


            await Client.ConnectWithProxyAsync(proxy, Konstants.KikEndPoint, Konstants.KikPort)
                .ConfigureAwait(false);
            await Client.InitSslStreamAsync("talk.kik.com")
                .ConfigureAwait(false);
        }

        public async Task Ping()
        {
            await Client.SendDataAsync(Packets.Ping())
                .ConfigureAwait(false);
        }

        public async Task SendMessage(string msgId, string toJid, string body)
        {
            var packet = Packets.SendMessage(msgId, toJid, body);
            await Client.SendDataAsync(packet)
                .ConfigureAwait(false);
        }

        public async Task InitStream()
        {
            var ts = Krypto.KikTimestamp();
            var signed = await Task.Run(
                    () => Krypto.KikRsaSign(_account.Jid, Konstants.AppVersion, ts, _account.Sid))
                .ConfigureAwait(false);
            var deviceTsHash = Krypto.KikHmacSha1Signature(ts, _account.Jid);
            const int nFirstRun = 10;
            var streamInitPropertyMap = await Packets.StreamInitPropertyMapAsync(
                    $"{_account.Jid}/{_account.DeviceCanId}", _account.PasskeyU, deviceTsHash, long.Parse(ts), signed,
                    _account.Sid, nFirstRun)
                .ConfigureAwait(false);
            if (streamInitPropertyMap == null || streamInitPropertyMap.Length == 0)
                throw new InvalidOperationException("StreamInitPropertyMap returned null (check java server)");
            await Client.SendDataAsync(streamInitPropertyMap)
                .ConfigureAwait(false);

            using (var xmlReader = XmlReader.Create(Client.NetworkStream, new XmlReaderSettings
            {
                Async = true,
                IgnoreProcessingInstructions = true,
                IgnoreComments = true,
                IgnoreWhitespace = true,
                //ConformanceLevel = ConformanceLevel.Fragment
            }))
            {
                await xmlReader.ReadAsync()
                    .ConfigureAwait(false);

                await xmlReader.MoveToContentAsync()
                    .ConfigureAwait(false);

                using (var subtree = await Task.Run(() => xmlReader.ReadSubtree())
                    .ConfigureAwait(false))
                {
                    await subtree.ReadAsync()
                        .ConfigureAwait(false);

                    switch (subtree.Name)
                    {
                        case "k":
                            var ok = subtree.GetAttribute("ok");
                            if (ok == "1")
                            {
                                xmlReader.Dispose();
                                return;
                            }
                            var tmp = await subtree.ReadOuterXmlAsync()
                                .TimeoutAfter(10000)
                                .ConfigureAwait(false);
                            if (!tmp.Contains("Not Authorized"))
                                throw new InvalidOperationException(
                                    "Kik server returned unexpected response after sending stream init prop map");

                            _account.LoginErrors = 99;
                            throw new InvalidOperationException(
                                "Kik server returned Not Authorized for this session");
                    }
#if DEBUG
                    var xml = await subtree.ReadOuterXmlAsync()
                        .TimeoutAfter(10000)
                        .ConfigureAwait(false);
                    Console.WriteLine($@"<== {xml}");
#endif

                    throw new InvalidOperationException("InitStream failed");
                }
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Client?.Dispose();
        }
    }
}
