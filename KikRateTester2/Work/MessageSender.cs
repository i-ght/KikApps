using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using KikRateTester2.Declarations;
using DankLibWaifuz.CollectionsWaifu;
using DankLibWaifuz;
using DankLibWaifuz.SettingsWaifu;
using System.Threading;
using DankLibWaifuz.ScriptWaifu;
using KikWaifu;
using DankLibWaifuz.Etc;
using System.IO;
using System.Security.Authentication;
using System.Net.Sockets;

namespace KikRateTester2.Work
{
    internal class MessageSender : KikClient
    {
        public static Queue<Account> Accounts { get; } = new Queue<Account>();
        public static Dictionary<string, Account> SentMessages { get; } = new Dictionary<string, Account>();
        public static bool WorkComplete { get; set; }

        public MessageSender(int index, ObservableCollection<DataGridItem> collection) : base(index, collection)
        {
        }

        public async Task Base()
        {
            try
            {
                while (true)
                {
                    try
                    {
                        if (WorkComplete)
                            return;

                        if (!await Init().ConfigureAwait(false))
                            return;

                        await WaitForReceiveAccounts().ConfigureAwait(false);

                        await ConnectKik().ConfigureAwait(false);

                        if (!await InitStream().ConfigureAwait(false))
                            continue;

                        Interlocked.Increment(ref Stats.SendAccountsOnline);
                        _account.LoginErrors = 0;
                        _account.LoggedInSuccessfully = true;

                        try
                        {
                            var seconds = Settings.GetRandom("SendMsgDelay");
                            await UpdateThreadStatusAsync($"Delaying for {seconds} seconds...", seconds * 1000).ConfigureAwait(false);

                            await SendMessage().ConfigureAwait(false);

                            _account.SentMessage = true;
                        }
                        finally
                        {
                            Interlocked.Decrement(ref Stats.SendAccountsOnline);
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
                        _tcpWaifu.Close();
                        await AddAcctBackToQueue().ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                await UpdateThreadStatusAsync("Work complete").ConfigureAwait(false);
            }
        }

        private async Task WaitForReceiveAccounts()
        {
            while (MessageReceiver.JidsOnline.Count < Settings.Get<int>("MinReceiveAcctsOnline"))
            {
                await UpdateThreadStatusAsync("Waiting for recieve accounts to be online...", 1000).ConfigureAwait(false);
            }
        }

        private async Task SendMessage()
        {
            var toJid = MessageReceiver.JidsOnline.RandomSelection();
            var message = ScriptWaifu.Spin(Collections.MessagesToSend.GetNext());
            var msgId = Guid.NewGuid().ToString();

            var data = Packets.SendMessage(msgId, toJid, message);
            await _tcpWaifu.SendDataAsync(data).ConfigureAwait(false);

            lock (SentMessages)
                SentMessages.Add(msgId, _account);
        }

        private async Task AddAcctBackToQueue()
        {
            if (_account == null)
                return;

            if (_account.SentMessage)
                return;

            if (!_account.LoggedInSuccessfully)
                _account.LoginErrors++;

            if (_account.LoginErrors >= Settings.Get<int>("MaxLoginErrors") || _account.Invalid)
            {
                await MessageReceiver.AppendInvalidAccount(_account.ToString()).ConfigureAwait(false);
                await AddBlacklistAsync(BlacklistType.Login, _account.LoginId).ConfigureAwait(false);
                return;
            }

            await UpdateThreadStatusAsync($"Adding {_account.LoginId} back to queue. Login errors = {_account.LoginErrors}").ConfigureAwait(false);

            lock (Accounts)
                Accounts.Enqueue(_account);
        }

        public static void SaveRatedAccounts()
        {
            foreach (var kvp in SentMessages)
                GeneralHelpers.AppendFile("kik-rated_accounts.txt", kvp.Value.ToString());
        }
    }
}
