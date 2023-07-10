using KikContactVerifier2.Declarations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KikContactVerifier2.Work
{
    internal class Mode
    {
        public static Random Random { get; } = new Random();

        protected readonly int _index;
        protected readonly ObservableCollection<DataGridItem> _collection;

        public Mode(int index, ObservableCollection<DataGridItem> collection)
        {
            _index = index;
            _collection = collection;
        }

        protected void UpdateAccountColumn(string acct)
        {
            _collection[_index].Account = acct;
        }

        protected void UpdateThreadStatus(string s, int delay = 1000)
        {
            _collection[_index].Status = s;
            Thread.Sleep(delay);
        }

        protected async Task UpdateThreadStatusAsync(string s, int delay = 1000)
        {
            _collection[_index].Status = s;
            await Task.Delay(delay).ConfigureAwait(false);
        }

        protected bool UnexpectedResponse(string s)
        {
            const string unexR = "Unexpected response";
            return Failed(s, unexR);
        }

        protected async Task<bool> UnexpectedResponseAsync(string s)
        {
            const string unexR = "Unexpected response";
            return await FailedAsync(s, unexR).ConfigureAwait(false);
        }

        protected void Attempting(string s, string subMsg = null)
        {
            var sb = new StringBuilder($"{s}...");
            if (!string.IsNullOrWhiteSpace(subMsg))
                sb.Append($" [{subMsg}]");

            UpdateThreadStatus(sb.ToString());
        }

        protected async Task AttemptingAsync(string s, int delay = 0)
        {
            var sb = new StringBuilder($"{s}...");
            await UpdateThreadStatusAsync(sb.ToString(), delay).ConfigureAwait(false);
        }

        protected async Task<bool> SuccessAsync(string s, string subMsg = null)
        {
            var sb = new StringBuilder($"{s}OK");
            if (!string.IsNullOrWhiteSpace(subMsg))
                sb.Append($" [{subMsg}]");

            await UpdateThreadStatusAsync(sb.ToString()).ConfigureAwait(false);
            return true;
        }

        protected async Task<bool> FailedAsync(string s, string subMsg = null)
        {
            var sb = new StringBuilder($"{s}FAILED");
            if (!string.IsNullOrWhiteSpace(subMsg))
                sb.Append($" [{subMsg}]");

            await UpdateThreadStatusAsync(sb.ToString(), 2000).ConfigureAwait(false);
            return false;
        }

        protected bool Success(string s, string subMsg = null)
        {
            var sb = new StringBuilder($"{s}OK");
            if (!string.IsNullOrWhiteSpace(subMsg))
                sb.Append($" [{subMsg}]");

            UpdateThreadStatus(sb.ToString());
            return true;
        }

        protected bool Failed(string s, string subMsg = null)
        {
            var sb = new StringBuilder($"{s}FAILED");
            if (!string.IsNullOrWhiteSpace(subMsg))
                sb.Append($" [{subMsg}]");

            UpdateThreadStatus(sb.ToString(), 2000);
            return false;
        }

        protected void ResetUiStats()
        {
            UpdateAccountColumn(string.Empty);
            UpdateThreadStatus(string.Empty, 0);
        }

        private static readonly object _writelock = new object();
        protected static void AddBlacklist(BlacklistType blacklistType, string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return;

            lock (_writelock)
            {
                Blacklists.Dict[blacklistType].Add(input);
                using (var sw = new StreamWriter($"{Assembly.GetEntryAssembly().GetName().Name.Replace(" ", "_").ToLower()}-{blacklistType.ToString().ToLower()}_blacklist.txt", true))
                    sw.WriteLine(input);
            }

        }

        private static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);
        protected static async Task AddBlacklistAsync(BlacklistType blacklistType, string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return;

            await Semaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                Blacklists.Dict[blacklistType].Add(input);

                using (var sw = new StreamWriter($"{Assembly.GetEntryAssembly().GetName().Name.Replace(" ", "_").ToLower()}-{blacklistType.ToString().ToLower()}_blacklist.txt", true))
                    await sw.WriteLineAsync(input).ConfigureAwait(false);
            }
            finally
            {
                Semaphore.Release();
            }
        }


        protected static async Task AddBlacklistAsync(BlacklistType blacklistType, ICollection<string> collection)
        {
            if (collection?.Count == 0)
                return;

            await Semaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                using (var sw = new StreamWriter($"{Assembly.GetEntryAssembly().GetName().Name.Replace(" ", "_").ToLower()}-{blacklistType.ToString().ToLower()}_blacklist.txt", true))
                {
                    foreach (var item in collection)
                    {
                        Blacklists.Dict[blacklistType].Add(item);
                        await sw.WriteLineAsync(item).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                Semaphore.Release();
            }
        }
    }
}
