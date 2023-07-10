using System;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DankWaifu.Sys;

namespace KikRateTester3
{
    internal class Mode
    {
        public Mode(int index, DataGridItem ui)
        {
            Index = index;
            UI = ui;
        }

        protected int Index { get; }
        protected DataGridItem UI { get; }

        protected void UpdateAccountColumn(string acct)
        {
            UI.Account = acct;
        }

        protected void UpdateThreadStatus(string s)
        {
            UI.Status = s;
        }

        protected void UpdateThreadStatus(string s, int delay)
        {
            UpdateThreadStatus(s);
            Thread.Sleep(delay);
        }

        protected async Task UpdateThreadStatusAsync(string s, int delay)
        {
            UpdateThreadStatus(s);
            await Task.Delay(delay)
                .ConfigureAwait(false);
        }

        protected async Task UpdateThreadStatusAsync(string s, int delay, CancellationToken c)
        {
            UpdateThreadStatus(s);
            await Task.Delay(delay, c)
                .ConfigureAwait(false);
        }

        protected async Task WaitingForInputFile(string file)
        {
            await UpdateThreadStatusAsync($"Waiting for {file} file to be loaded", 2000)
                .ConfigureAwait(false);
        }

        protected async Task OnExceptionAsync(Exception e)
        {
            await OnBackgroundExceptionAsync(e)
                .ConfigureAwait(false);
            await UpdateThreadStatusAsync($"{e.GetType().Name}: {e.Message}", 5000)
                .ConfigureAwait
                (false);
        }

        protected async Task OnBackgroundExceptionAsync(Exception e)
        {
            await ErrorLogger.WriteAsync(e, false)
                .ConfigureAwait(false);
        }

        protected static void UnexpectedHTTPResponse(HttpStatusCode statusCode, [CallerMemberName] string caller = null)
        {
            throw new InvalidOperationException($"{caller} returned unexpected http response ({statusCode})");
        }
    }
}
