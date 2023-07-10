using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KikBot3.Declarations;

#if !DEBUG
using System.IO;
using System.Reflection;
#endif

namespace KikBot3.Work
{
    internal class Mode
    {
        private static readonly SemaphoreSlim Semaphore;
        private static readonly object Writelock;

        static Mode()
        {
            Semaphore = new SemaphoreSlim(1, 1);
            Writelock = new object();
        }

        public Mode(int index, ObservableCollection<DataGridItem> collection)
        {
            Index = index;
            Collection = collection;
        }

        public static Random Random { get; } = new Random();

        protected int Index { get; }
        protected ObservableCollection<DataGridItem> Collection { get; }

        protected void UpdateAccountColumn(string acct)
        {
            Collection[Index].Account = acct;
        }

        protected void UpdateThreadStatus(string s, int delay = 0)
        {
            if (Collection[Index].Status != s)
                Collection[Index].Status = s;
            Thread.Sleep(delay);
        }

        protected async Task UpdateThreadStatusAsync(string s, int delay = 0)
        {
            Collection[Index].Status = s;
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

        protected static void AddBlacklist(BlacklistType blacklistType, string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return;

            lock (Writelock)
            {
                Blacklists.Collections[blacklistType].Add(input);
#if !DEBUG
                using (var sw = new StreamWriter($"{Assembly.GetEntryAssembly().GetName().Name.Replace(" ", "_").ToLower()}-{blacklistType.ToString().ToLower()}_blacklist.txt", true))
                    sw.WriteLine(input);
#endif
            }
        }

        protected static void AddBlacklist(BlacklistType blacklistType, ICollection<string> input)
        {
            if (input == null || input.Count == 0)
                return;

            lock (Writelock)
            {
#if DEBUG
                foreach (var item in input)
                    Blacklists.Collections[blacklistType].Add(item);
#else
                using (
                    var sw =
                        new StreamWriter(
                            $"{Assembly.GetEntryAssembly().GetName().Name.Replace(" ", "_").ToLower()}-{blacklistType.ToString().ToLower()}_blacklist.txt",
                                true))
                {

                    foreach (var item in input)
                    {
                        Blacklists.Collections[blacklistType].Add(item);
                        sw.WriteLine(item);
                    }
                }
#endif
            }
        }

        protected static async Task AddBlacklistAsync(BlacklistType blacklistType, string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return;

            await Semaphore.WaitAsync()
                .ConfigureAwait(false);

            try
            {
                Blacklists.Collections[blacklistType].Add(input);
#if !DEBUG
                using (var sw = new StreamWriter($"{Assembly.GetEntryAssembly().GetName().Name.Replace(" ", "_").ToLower()}-{blacklistType.ToString().ToLower()}_blacklist.txt", true))
                    await sw.WriteLineAsync(input)
                        .ConfigureAwait(false);
#endif
            }
            finally
            {
                Semaphore.Release();
            }
        }



        protected static async Task AddBlacklistAsync(BlacklistType blacklistType, ICollection<string> collection)
        {
            if (collection == null || collection.Count == 0)
                return;

            await Semaphore.WaitAsync().ConfigureAwait(false);

            try
            {
#if DEBUG
                foreach (var item in collection)
                    Blacklists.Collections[blacklistType].Add(item);
#else
                using (var sw = new StreamWriter($"{Assembly.GetEntryAssembly().GetName().Name.Replace(" ", "_").ToLower()}-{blacklistType.ToString().ToLower()}_blacklist.txt", true))
                {
                    foreach (var item in collection)
                    {
                        Blacklists.Collections[blacklistType].Add(item);
                        await sw.WriteLineAsync(item).ConfigureAwait(false);
                    }
                }
#endif
            }
            finally
            {
                Semaphore.Release();
            }
        }
    }
}
