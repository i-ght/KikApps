using DankLibWaifuz;
using DankLibWaifuz.Etc;
using System;
using System.Collections.Generic;
using System.Threading;

namespace KikBot2.Work
{
    internal static class Maintenance
    {
        public static List<Bot> Bots { get; } = new List<Bot>();

        public static void Base()
        {
            var cnt = Bots.Count;
            
            while (true)
            {
                try
                {
                    var restarts = 0;
                    for (var i = 0; i < cnt; i++)
                    {
                        var cls = Bots[i];
                        if (cls == null)
                            continue;

                        if (cls.Return)
                        {
                            cls.DisposeSocket();
                            Bots[i] = null;
                            continue;
                        }

                        switch (cls.LoginState)
                        {
                            case LoginState.Disconnected:

                                if (restarts >= 5)
                                    break;

                                if (!cls.ShouldConnect)
                                    break;
          
                                cls.Connect()
                                    .BeginTask();
                                restarts++;
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

        public static int OnlineCount
        {
            get
            {
                var ret = 0;
                for (var i = 0; i < Bots.Count; i++)
                {
                    var cls = Bots[i];
                    if (cls == null || cls.LoginState != LoginState.Connected)
                        continue;

                    ret++;
                }

                return ret;
            }
        }
    }
}
