using System;
using System.Collections.Generic;
using System.Threading;
using DankLibWaifuz;

namespace KikBot3.Work
{
    internal static class Maintenance
    {
        static Maintenance()
        {
            Bots = new List<Bot>();
        }

        public static List<Bot> Bots { get; }

        public static void Base()
        {
            while (true)
            {
                try
                {
                    var restarts = 0;
                    for (var i = 0; i < Bots.Count; i++)
                    {
                        var bot = Bots[i];
                        if (bot == null)
                            continue;

                        if (bot.Return)
                        {
                            bot.DisposeIDisposables();
                            Bots[i] = null;
                            continue;
                        }

                        switch (bot.LoginState)
                        {
                            case LoginState.Disconnected:
                                if (restarts >= 5)
                                    break;

                                if (!bot.ShouldConnect)
                                    break;

                                bot.BeginConnectKik();
                                restarts++;
                                break;
                            case LoginState.Connected:
#if BLASTER
                                bot.BeginSendNextBlast();
#endif
                                bot.BeginSendNextMessage();
                                break;
                        }
                    }
                }
                catch (Exception e)
                {
                    ErrorLogger.WriteErrorLog(e);
                }
                finally
                {
                    Thread.Sleep(1000);
                }
            }
        }

        public static int OnlineCount()
        {
            var ret = 0;
            for (var i = 0; i < Bots.Count; i++)
            {
                var bot = Bots[i];
                if (bot == null)
                    continue;

                if (bot.LoginState == LoginState.Connected)
                    ret++;
            }

            return ret;
        }
    }
}
