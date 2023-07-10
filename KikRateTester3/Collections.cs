using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using DankWindowsWaifu.WPF;

namespace KikRateTester3
{
    internal class Collections : IDisposable
    {
        //public const string SendProxies = "SendProxies";
        //public const string ReceiveProxies = "ReceiveProxies";
        //public const string SendAccounts = "SendAccounts";
        //public const string ReceiveAccounts = "ReceiveAccounts";

        private readonly SettingsDataGrid _settings;

        public Collections(SettingsDataGrid settings, ConcurrentQueue<Account> send, ConcurrentQueue<Account> recv)
        {
            _settings = settings;
            Writelock = new SemaphoreSlim(1, 1);
            SendAccounts = send;
            ReceiveAccounts = recv;
            OnlineRecvAccounts = new List<Account>();
            AccountsToMessage = new ConcurrentQueue<Account>();
            SentBlacklist = new HashSet<string>();
        }

        public SemaphoreSlim Writelock { get; }
        public ConcurrentQueue<Account> SendAccounts { get; }
        public ConcurrentQueue<Account> ReceiveAccounts { get; }
        public ConcurrentQueue<string> SendProxies => _settings.GetConcurrentQueue(Constants.SendProxies);
        public ConcurrentQueue<string> ReceiveProxies => _settings.GetConcurrentQueue(Constants.ReceiveProxies);
        public List<Account> OnlineRecvAccounts { get; }
        public ConcurrentQueue<Account> AccountsToMessage { get; }
        public HashSet<string> SentBlacklist { get; }
        public bool WorkComplete { get; set; }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Writelock?.Dispose();
        }
    }
}
