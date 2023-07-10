using System.Collections.ObjectModel;
using KikRateTester2.Declarations;
using DankLibWaifuz.TcpWaifu;
using System.Threading.Tasks;
using DankLibWaifuz.CollectionsWaifu;
using System;
using System.Net;
using DankLibWaifuz.Etc;
using KikWaifu;

namespace KikRateTester2.Work
{
    internal class KikClient : Mode
    {
        public KikClient(int index, ObservableCollection<DataGridItem> collection) : base(index, collection)
        {
        }

        protected TcpAsyncWaifu _tcpWaifu;
        protected Account _account;

        protected WebProxy _proxy;

        protected async Task<bool> Init()
        {
            if (this is MessageSender)
            {
                _account = MessageSender.Accounts.GetNext(false);
                _proxy = Collections.Proxies.GetNext().ToWebProxy();
            }
            else
            {
                _account = MessageReceiver.Accounts.GetNext(false);
                _proxy = Collections.ReceiveProxies.GetNext().ToWebProxy();
            }

            if (_account == null)
            {
                await UpdateThreadStatusAsync("Accounts queue depelted", 5000).ConfigureAwait(false);
                return false;
            }

            UpdateAccountColumn(_account.LoginId);
            _account.Sid = Guid.NewGuid().ToString();
            _account.LoggedInSuccessfully = false;

            _tcpWaifu = new TcpAsyncWaifu
            {
                Proxy = _proxy
            };

            return true;
        }

        protected async Task ConnectKik()
        {
            var s = $"Connecting to kik chat server [{_account.LoginErrors}]: ";
            await AttemptingAsync(s).ConfigureAwait(false);

            await _tcpWaifu.ConnectWithProxyAsync(Konstants.KikEndPoint, Konstants.KikPort).ConfigureAwait(false);
            await _tcpWaifu.InitSslStreamAsync(Konstants.KikEndPoint).ConfigureAwait(false);
        }

        protected async Task<bool> InitStream()
        {
            var s = $"Initializing stream [{_account.LoginErrors}]: ";
            await AttemptingAsync(s).ConfigureAwait(false);

            var ts = Krypto.KikTimestamp();
            var deviceTsHash = Krypto.KikHmacSha1Signature(ts.ToString(), _account.Jid);
            var signed = await Task.Run(() => Krypto.KikRsaSign(_account.Jid, Konstants.AppVersion, ts.ToString(), _account.Sid)).ConfigureAwait(false);
            const int nVal = 1;
            var data = await Packets.StreamInitPropertyMapAsync($"{_account.Jid}/{_account.DeviceCanId}", _account.PasskeyU, deviceTsHash, long.Parse(ts), signed, _account.Sid, nVal)
                .ConfigureAwait(false);
            if (data.IsNullOrEmpty())
                return await FailedAsync("failed to calcualte stream init property map").ConfigureAwait(false);

            await _tcpWaifu.SendDataAsync(data).ConfigureAwait(false);

            var response = await _tcpWaifu.ReadAllPendingDataAsStringAsync().ConfigureAwait(false);

            if (response.Contains("Not Authorized"))
            {
                _account.Invalid = true;
                return await FailedAsync("Invalid credentials").ConfigureAwait(false);
            }

            if (!response.Contains("ok"))
                return await UnexpectedResponseAsync(s).ConfigureAwait(false);

            return true;
        }
    }
}
