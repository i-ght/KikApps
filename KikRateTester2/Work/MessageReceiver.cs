using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KikRateTester2.Declarations;
using DankLibWaifuz.CollectionsWaifu;
using DankLibWaifuz;
using DankLibWaifuz.SettingsWaifu;
using System.Threading;
using System.Text.RegularExpressions;
using DankLibWaifuz.Etc;
using System.Net.Sockets;
using System.Security.Authentication;
using System.IO;
using KikWaifu;

namespace KikRateTester2.Work
{
    internal class MessageReceiver : KikClient
    {
        public static List<string> JidsOnline { get; } = new List<string>();
        public static Queue<Account> Accounts { get; } = new Queue<Account>();
        public static SemaphoreSlim ConnectionLock { get; } = new SemaphoreSlim(10, 10);

        public MessageReceiver(int index, ObservableCollection<DataGridItem> collection) : base(index, collection)
        {
        }

        private CancellationTokenSource _cTokenSrc;

        public async Task Base()
        {
            while (true)
            {
                if (MessageSender.WorkComplete)
                    return;

                try
                {

                    await UpdateThreadStatusAsync("Waiting for connection lock...")
                        .ConfigureAwait(false);

                    await ConnectionLock.WaitAsync()
                        .ConfigureAwait(false);

                    try
                    {
                        if (!await Init().ConfigureAwait(false))
                            return;

                        await ConnectKik().ConfigureAwait(false);

                        if (!await InitStream().ConfigureAwait(false))
                            continue;

                    }
                    finally
                    {
                        ConnectionLock.Release();
                    }
                    _account.LoggedInSuccessfully = true;
                    _account.LoginErrors = 0;

                    Interlocked.Increment(ref Stats.RcvAccountsOnline);

                    lock (JidsOnline)
                        JidsOnline.Add(_account.Jid);

                    _cTokenSrc = new CancellationTokenSource();
                    var r = _cTokenSrc.Token.Register(() => _tcpWaifu.Close());
                    await UpdateThreadStatusAsync("Connected").ConfigureAwait(false);

                    try
                    {

                        var ping = PingLoop();
                        var rcv = ReceiveData();

                        await Task.WhenAll(ping, rcv).ConfigureAwait(false);
                    }
                    finally
                    {
                        if (JidsOnline.Contains(_account.Jid))
                            lock (JidsOnline)
                                JidsOnline.Remove(_account.Jid);

                        await UpdateThreadStatusAsync("Disconnected").ConfigureAwait(false);
                        Interlocked.Decrement(ref Stats.RcvAccountsOnline);

                        try
                        {
                            r.Dispose();
                        }
                        catch
                        {
                            /*ignored*/
                        }
                    }
                }
                catch (SocketException)
                {
                    await UpdateThreadStatusAsync("Socket exception").ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    await UpdateThreadStatusAsync("Object disposed exception").ConfigureAwait(false);
                }
                catch (AuthenticationException)
                {
                    await UpdateThreadStatusAsync("SSL authentication exception").ConfigureAwait(false);
                }
                catch (IOException)
                {
                    await UpdateThreadStatusAsync("Socket I/O exception").ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    await ErrorLogger.WriteAsync(e).ConfigureAwait(false);
                }
                finally
                {
                    CleanUp();
                    await AddAcctBackToQueue().ConfigureAwait(false);
                    await UpdateThreadStatusAsync("Sleeping before reconnect", 10000)
                        .ConfigureAwait(false);
                }
            }
        }

        private void CleanUp()
        {
            try
            {
                if (_cTokenSrc != null)
                {
                    _cTokenSrc.Dispose();
                    _cTokenSrc = null;
                }
                _tcpWaifu.Close();
                _tcpWaifu = null;

            }
            catch { /*ignored*/ }
        }

        private async Task PingLoop()
        {
            try
            {
                var sendNextPing = DateTime.Now.AddSeconds(10);

                while (true)
                {
                        if (MessageSender.WorkComplete)
                        {
                            Cancel();
                            return;
                        }

                        if (_cTokenSrc.IsCancellationRequested)
                            return;

                        if (sendNextPing > DateTime.Now)
                        {
                            await Task.Delay(1000).ConfigureAwait(false);
                            continue;
                        }

                        sendNextPing = DateTime.Now.AddSeconds(10);

                        var data = Packets.Ping();
                        await _tcpWaifu.SendDataAsync(data).ConfigureAwait(false);
                }
            }
            catch { /*lmao*/ }
            finally
            {
                Cancel();
            }
        }

        private void Cancel()
        {
            try
            {
                if (_cTokenSrc.Token.CanBeCanceled)
                    _cTokenSrc.Cancel();
            }
            catch { /*ignored*/ }
        }

        private async Task ReceiveData()
        {
            try
            {
                var sb = new StringBuilder();
                while (true)
                {

                    if (MessageSender.WorkComplete)
                    {
                        Cancel();
                        return;
                    }

                    if (_cTokenSrc.IsCancellationRequested)
                        return;

                    var buffer = new byte[8192];
                    var cnt =
                        await
                            _tcpWaifu.ReceiveDataAsync(buffer, 0, buffer.Length, _cTokenSrc.Token).ConfigureAwait(false);
                    var dataReceived = new byte[cnt];
                    Array.Copy(buffer, dataReceived, cnt);

                    if (dataReceived.IsNullOrEmpty())
                    {
                        await UpdateThreadStatusAsync("Socket I/O error").ConfigureAwait(false);
                        return;
                    }
                    sb.Append(dataReceived.Utf8String());

                    await Task.Delay(2000).ConfigureAwait(false);

                    if (_tcpWaifu.Available > 0)
                        continue;

                    var dataStr = sb.ToString();
                    sb.Clear();

#if DEBUG
                    Console.WriteLine("Received: " + dataStr);
#endif
                    //var dataStr = Encoding.UTF8.GetString(dataReceived);
                    //if (string.IsNullOrWhiteSpace(dataStr))
                    //{
                    //    await UpdateThreadStatusAsync("Socket I/O error").ConfigureAwait(false);
                    //    return;
                    //}

                    await HandleIncomingData(dataStr).ConfigureAwait(false);
                }
            }
            catch
            {
                /*lmao*/
            }
            finally
            {
                Cancel();
            }
        }

        private static readonly HashSet<string> Blacklist = new HashSet<string>();
        private static readonly Regex MsgIdRegex = new Regex("id=\"(.*?)\"", RegexOptions.IgnoreCase),
            MsgRegex = new Regex("<message(.*?)>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private async Task HandleIncomingData(string data)
        {
            foreach (Match match in MsgRegex.Matches(data))
            {
                var msgXml = match.Groups[1].Value;

                string msgId;
                if (!MsgIdRegex.TryGetGroup(msgXml, out msgId))
                    continue;

                if (!MessageSender.SentMessages.ContainsKey(msgId))
                    continue;

                var sender = MessageSender.SentMessages[msgId];
                if (Blacklist.Contains(sender.LoginId))
                    continue;

                lock (MessageSender.SentMessages)
                    MessageSender.SentMessages.Remove(msgId);

                await UpdateThreadStatusAsync($"Received message from {sender.LoginId}", 0).ConfigureAwait(false);


                await GeneralHelpers.AppendFileAsync("kik-unrated_accounts.txt", _account.ToString()).ConfigureAwait(false);
                Interlocked.Increment(ref Stats.Unrated);

                Blacklist.Add(sender.LoginId);
            }
        }


        private async Task AddAcctBackToQueue()
        {
            try
            {
                if (MessageSender.WorkComplete)
                    return;

                if (_account == null)
                    return;

                if (!_account.LoggedInSuccessfully)
                    _account.LoginErrors++;

                if (_account.LoginErrors >= Settings.Get<int>("MaxLoginErrors") || _account.Invalid)
                {
                    await AppendInvalidAccount(_account.ToString()).ConfigureAwait(false);
                    await AddBlacklistAsync(BlacklistType.Login, _account.LoginId).ConfigureAwait(false);
                    return;
                }

                await
                    UpdateThreadStatusAsync(
                            $"Adding {_account.LoginId} back to queue. Login errors = {_account.LoginErrors}")
                        .ConfigureAwait(false);

                lock (Accounts)
                    Accounts.Enqueue(_account);
            }
            catch (Exception e)
            {
                await ErrorLogger.WriteAsync(e)
                    .ConfigureAwait(false);
            }
        }

        private static readonly SemaphoreSlim InvalidAcctLock = new SemaphoreSlim(1, 1);
        public static async Task AppendInvalidAccount(string str)
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
    }
}
