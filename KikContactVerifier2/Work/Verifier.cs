using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using KikContactVerifier2.Declarations;
using DankLibWaifuz.TcpWaifu;
using System.Net;
using DankLibWaifuz;
using DankLibWaifuz.CollectionsWaifu;
using DankLibWaifuz.Etc;
using KikWaifu;
using DankLibWaifuz.SettingsWaifu;
using System.Threading;
using System.Text.RegularExpressions;
using System.IO;
using System.Net.Sockets;
using System.Security.Authentication;

namespace KikContactVerifier2.Work
{
    internal class Verifier : Mode
    {
        public static Queue<Account> Accounts { get; } = new Queue<Account>();

        private static readonly SemaphoreSlim Writelock = new SemaphoreSlim(1, 1);

        private readonly SemaphoreSlim ReceiveLock = new SemaphoreSlim(1, 1);

        public Verifier(int index, ObservableCollection<DataGridItem> collection) : base(index, collection)
        {
        }

        private Account _account;
        private WebProxy _proxy;
        private TcpAsyncWaifu _tcpWaifu;
        private CancellationTokenSource _cTokenSrc;

        public async Task Base()
        {
            try
            {
                while (true)
                {
                    try
                    {
                        if (Collections.Contacts.ReachedEOF)
                        {
                            await OutOfContacts().ConfigureAwait(false);
                            return;
                        }

                        if (!await GetNextAccount().ConfigureAwait(false))
                            return;

                        try
                        {
                            if (!await OkToLogin().ConfigureAwait(false))
                                continue;

                            await ConnectKik().ConfigureAwait(false);

                            if (!await InitStream().ConfigureAwait(false))
                                continue;

                            _account.LoggedInSuccessfully = true;

                            //if (await AccountIsRated().ConfigureAwait(false))
                            //    continue;

                            if (await RateCheck().ConfigureAwait(false))
                                continue;

                            await Verify().ConfigureAwait(false);
                        }
                        finally
                        {
                            _tcpWaifu.Close();
                            await AddAcctBackToQueue().ConfigureAwait(false);
                        }
                    }
                    catch (Exception e)
                    {
                        await UpdateThreadStatusAsync($"{e.GetType().Name} ~ {e.Message}").ConfigureAwait(false);
                        await ErrorLogger.WriteAsync(e).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                await UpdateThreadStatusAsync("Work complete").ConfigureAwait(false);
            }
        }

        private async Task<bool> GetNextAccount()
        {
            await ResetUiStats().ConfigureAwait(false);

            await UpdateThreadStatusAsync("Getting next account: ...").ConfigureAwait(false);

            _account = Accounts.GetNext(false);
            if (_account == null)
            {
                await UpdateThreadStatusAsync("Accounts queue depleted.", 3000).ConfigureAwait(false);
                return false;
            }

            _account.LoggedInSuccessfully = false;
            _account.Sid = Guid.NewGuid().ToString();
            _account.VerifySession = new VerifySession();

            await UpdateUiStatsFromAccount().ConfigureAwait(false);

            _proxy = Collections.Proxies.GetNext().ToWebProxy();
            _tcpWaifu = new TcpAsyncWaifu
            {
                Proxy = _proxy
            };

            return true;
        }

        private async Task ConnectKik()
        {
            var s = $"Connecting to kik chat server [{_account.LoginErrors}]: ";
            await AttemptingAsync(s).ConfigureAwait(false);

            await _tcpWaifu.ConnectWithProxyAsync(Konstants.KikEndPoint, Konstants.KikPort).ConfigureAwait(false);
            await _tcpWaifu.InitSslStreamAsync(Konstants.KikEndPoint).ConfigureAwait(false);
        }

        private async Task<bool> InitStream()
        {
            var s = $"Initializing stream [{_account.LoginErrors}]: ";
            await AttemptingAsync(s).ConfigureAwait(false);

            var ts = Krypto.KikTimestamp();
            var deviceTsHash = Krypto.KikHmacSha1Signature(ts, _account.Jid);
            var signed = await Task.Run(() => Krypto.KikRsaSign(_account.Jid, Konstants.AppVersion, ts.ToString(), _account.Sid)).ConfigureAwait(false);
            const int nVal = 1;
            var data = await Packets.StreamInitPropertyMapAsync($"{_account.Jid}/{_account.DeviceCanId}", _account.PasskeyU, deviceTsHash, long.Parse(ts), signed, _account.Sid, nVal)
                .TimeoutAfter(10000)
                .ConfigureAwait(false);
            if (data.IsNullOrEmpty())
                return await FailedAsync("Failed to calculate stream init property map").ConfigureAwait(false);

            await _tcpWaifu.SendDataAsync(data).ConfigureAwait(false);

            var response = await _tcpWaifu.ReadAllPendingDataAsStringAsync().ConfigureAwait(false);
            if (!response.Contains("ok"))
                return await UnexpectedResponseAsync(s).ConfigureAwait(false);

            _account.LoginErrors = 0;
            return true;
        }

        private async Task<bool> OkToLogin()
        {
            if (_account.NextLogin > DateTime.Now)
            {
                await UpdateThreadStatusAsync($"Next login @ {_account.NextLogin.ToLongTimeString()}", 10000).ConfigureAwait(false);
                _account.LoggedInSuccessfully = true;
                return false;
            }

            return true;
        }

        private async Task<bool> RateCheck()
        {
            var checkRatedSetting = Settings.Get<int>("CheckIfRatedAt");
            if (checkRatedSetting > _account.RateIndex)
                return false;

            _account.RateIndex = 0;

            return await AccountIsRated().ConfigureAwait(false);
        }

        private async Task<bool> AccountIsRated()
        {
            const string s = "Checking if account is rated: ";
            await AttemptingAsync(s).ConfigureAwait(false);

            var contact = Collections.VerifiedContacts.GetNext();
            if (string.IsNullOrWhiteSpace(contact))
                return false;

            var data = Packets.SearchForUsername(contact);
            await _tcpWaifu.SendDataAsync(data).ConfigureAwait(false);

            var response = await _tcpWaifu.ReadAllPendingDataAsStringAsync().ConfigureAwait(false);
            if (!response.Contains("<item jid"))
            {
                _account.IsRated = true;
                await WriteRatedAccount(_account.ToString()).ConfigureAwait(false);
                await AddBlacklistAsync(BlacklistType.Rated, _account.LoginId).ConfigureAwait(false);
                await UpdateThreadStatusAsync(s + "true");
                return true;
            }

            return false;
        }

        private static readonly SemaphoreSlim AccountsRatedLock = new SemaphoreSlim(1, 1);

        private static async Task WriteRatedAccount(string str)
        {
            await AccountsRatedLock.WaitAsync().ConfigureAwait(false);

            try
            {
                using (var sw = new StreamWriter("contact_verifier-accts-rated.txt", true))
                    await sw.WriteLineAsync(str).ConfigureAwait(false);
            }
            finally
            {
                AccountsRatedLock.Release();
            }
        }

        private async Task Verify()
        {
            var maxAttempts = Settings.GetRandom("AttemptsPerSession");

            Interlocked.Increment(ref Stats.Online);

            _cTokenSrc = new CancellationTokenSource();
            var r = _cTokenSrc.Token.Register(() => _tcpWaifu.Close());
            var rcvTask = ReceiveData();

            try
            {
                while (true)
                {
                    if (_account.VerifySession.Attempts > maxAttempts)
                    {
                        _account.VerifySession = new VerifySession();

                        var minutes = Settings.GetRandom("DelayBetweenSessions");
                        _account.NextLogin = DateTime.Now.AddMinutes(minutes);

                        await UpdateThreadStatusAsync($@"Session complete.  Next login @ {_account.NextLogin.ToShortTimeString()}", 5000).ConfigureAwait(false);
                        return;
                    }

                    if (Collections.Contacts.ReachedEOF)
                    {
                        await OutOfContacts().ConfigureAwait(false);
                        return;
                    }

                    var mode = Settings.Get<string>("Mode");
                    if (string.IsNullOrWhiteSpace(mode))
                    {
                        await UpdateThreadStatusAsync("Invalid mode", 30000).ConfigureAwait(false);
                        continue;
                    }
                    switch (mode.ToLower())
                    {
                        default:
                            await UpdateThreadStatusAsync("Invalid mode", 30000).ConfigureAwait(false);
                            break;

                        case "username":
                            var contact = await Collections.Contacts.GetNextAsync().ConfigureAwait(false);
                            if (string.IsNullOrEmpty(contact))
                            {
                                await OutOfContacts().ConfigureAwait(false);
                                return;
                            }

                            if (Blacklists.Dict[BlacklistType.Verify].Contains(contact))
                                continue;

                            Interlocked.Increment(ref Stats.Attempts);
                            UpdateSessionAttempts(++_account.VerifySession.Attempts);
                            UpdateAttempts(++_account.Attempts);

                            await SendVerifyRequest(contact).ConfigureAwait(false);

                            _account.RateIndex++;

                            await AddBlacklistAsync(BlacklistType.Verify, contact).ConfigureAwait(false);

                            break;

                        case "email":

                            var emails = await Emails().ConfigureAwait(false);
                            if (emails.Count == 0)
                            {
                                await OutOfContacts().ConfigureAwait(false);
                                return;
                            }

                            Interlocked.Add(ref Stats.Attempts, emails.Count);
                            UpdateSessionAttempts(_account.VerifySession.Attempts += emails.Count);
                            UpdateAttempts(_account.Attempts += emails.Count);

                            await SendVerifyEmailsRequest(emails).ConfigureAwait(false);
                            await AddBlacklistAsync(BlacklistType.Verify, emails).ConfigureAwait(false);

                            break;
                    }

                    await DelayBeforeNextVerifyAttempt().ConfigureAwait(false);
                }
            }
            finally
            {
                Interlocked.Decrement(ref Stats.Online);
                _cTokenSrc.Cancel();
                try { await rcvTask.ConfigureAwait(false); } catch { /*ignored*/ }
                _cTokenSrc.Dispose();
                r.Dispose();
            }
        }

        private static async Task<List<string>> Emails()
        {
            var ret = new List<string>();
            var max = Random.Next(8, 23);
            for (var i = 0; i < max; i++)
            {
                var email = await Collections.Contacts.GetNextAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(email))
                    continue;

                if (Blacklists.Dict[BlacklistType.Verify].Contains(email))
                    continue;

                ret.Add(email);
            }

            return ret;
        }

        private async Task DelayBeforeNextVerifyAttempt()
        {
            var seconds = Settings.GetRandom("DelayBetweenAttempts");
            for (var i = seconds; i > 0; i--)
            {
                var s = i > 1 ? $"Delaying before next attempt [{i} seconds remain]" : $"Delaying before next attempt [{i} second remains]";
                await UpdateThreadStatusAsync(s).ConfigureAwait(false);
            }
        }

        private async Task SendVerifyEmailsRequest(List<string> emails)
        {
            var s = $"Trying {emails.Count} emails: ";
            await UpdateThreadStatusAsync(s, 100).ConfigureAwait(false);

            var data = Packets.ImportContacts(emails);
#if DEBUG
            Console.WriteLine("Sending => " + data.Utf8String());
#endif
            await _tcpWaifu.SendDataAsync(data).ConfigureAwait(false);
        }

        private async Task SendVerifyRequest(string username)
        {
            username = username.Replace("\"", string.Empty);
            var s = $"Trying {username}: ";
            await UpdateThreadStatusAsync(s + "...", 100).ConfigureAwait(false);

            var data = Packets.SearchForUsername(username);
            await _tcpWaifu.SendDataAsync(data).ConfigureAwait(false);
        }

        private async Task ReceiveData()
        {
            try
            {
                var sb = new StringBuilder();
                while (true)
                {
                    if (_cTokenSrc.IsCancellationRequested)
                        return;

                    await ReceiveLock.WaitAsync()
                        .ConfigureAwait(false);

                    try
                    {
                        var buffer = new byte[8192];
                        var cnt = await _tcpWaifu.ReceiveDataAsync(buffer, 0, buffer.Length, _cTokenSrc.Token);

                        var dataReceived = Encoding.UTF8.GetString(buffer, 0, cnt);
                        sb.Append(dataReceived);

                        await Task.Delay(100).ConfigureAwait(false);

                        if (_tcpWaifu.Available > 0)
                            continue;

                        var data = sb.ToString();

#if DEBUG
                        Console.WriteLine(@"Received: " + data);
#endif

                        sb.Clear();
                        await ParseJids(data).ConfigureAwait(false);
                    }
                    finally
                    {
                        ReceiveLock.Release();
                    }
                }
            }
            catch { /*ignored*/ }
        }

        private static readonly Regex JidRegex = new Regex("<item jid=\"(.*?)\">",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private async Task ParseJids(string data)
        {
            var tmp = new HashSet<string>();
            foreach (Match match in JidRegex.Matches(data))
            {
                var jid = match.Groups[1].Value;
                if (string.IsNullOrWhiteSpace(jid))
                    continue;

                tmp.Add(jid);
            }

            if (tmp.Count == 0)
                return;

            await Writelock.WaitAsync().ConfigureAwait(false);

            try
            {
                using (var sw = new StreamWriter("kik-jids_scraped.txt", true))
                    foreach (var jid in tmp)
                        await sw.WriteLineAsync(jid).ConfigureAwait(false);
            }
            finally
            {
                Writelock.Release();
            }

            Interlocked.Add(ref Stats.Verified, tmp.Count);

            UpdateVerified(_account.Verified += tmp.Count);
            UpdateSessionVerified(_account.VerifySession.Verified += tmp.Count);
        }

        private async Task OutOfContacts()
        {
            await UpdateThreadStatusAsync("Out of contacts", 3000).ConfigureAwait(false);
        }

        private void UpdateAttempts(int val)
        {
            _collection[_index].AttemptsCount = val;
        }

        public void UpdateVerified(int val)
        {
            _collection[_index].VerifiedCount = val;
        }

        private void UpdateSessionAttempts(int val)
        {
            _collection[_index].AttemptsSessionCount = val;
        }

        private void UpdateSessionVerified(int val)
        {
            _collection[_index].VerifiedSessionCount = val;
        }

        private async Task UpdateUiStatsFromAccount()
        {
            UpdateAccountColumn(_account.LoginId);
            await UpdateThreadStatusAsync("Idle", 0).ConfigureAwait(false);
            UpdateAttempts(_account.Attempts);
            UpdateVerified(_account.Verified);
            UpdateSessionAttempts(_account.VerifySession.Attempts);
            UpdateSessionVerified(_account.VerifySession.Verified);
        }

        private new async Task ResetUiStats()
        {
            UpdateAccountColumn(string.Empty);
            await UpdateThreadStatusAsync(string.Empty, 0).ConfigureAwait(false);
            UpdateAttempts(0);
            UpdateVerified(0);
            UpdateSessionAttempts(0);
            UpdateSessionVerified(0);
        }

        private async Task AddAcctBackToQueue()
        {
            if (_account == null)
                return;

            if (!_account.LoggedInSuccessfully)
                _account.LoginErrors++;

            if (_account.LoginErrors >= Settings.Get<int>("MaxLoginErrors") || _account.IsRated)
                return;

            await UpdateThreadStatusAsync($"Adding {_account.LoginId} back to queue. Login errors = {_account.LoginErrors}").ConfigureAwait(false);

            lock (Accounts)
                Accounts.Enqueue(_account);
        }
    }
}