using DankLibWaifuz.Etc;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace KikWaifu
{
    public static class Packets
    {
        public static async Task<byte[]> StreamInitPropertyMapAnonAsync(string signed, string deviceTsHash, string ts, string deviceCanId, string sid, int? n)
        {
            var model = new StreamInitPropertyMapAnon
            {
                Anon = 1,
                Conn = "WIFI",
                Cv = deviceTsHash,
                Sid = sid,
                Lang = "en_US",
                Ts = long.Parse(ts),
                Dev = deviceCanId,
                N = n,
                Signed = signed,
                V = Konstants.AppVersion,
            };
            var jsonStr =
                await Task.Run(() => JsonConvert.SerializeObject(model, Formatting.None) + Environment.NewLine).ConfigureAwait(false);
            var data = Encoding.UTF8.GetBytes(jsonStr);

            var host = "127.0.0.1"; //Debugger.IsAttached ? "192.168.0.22" : "127.0.0.1";

            using (var client = new TcpClient())
            {
                await client.ConnectAsync(host, 30592).ConfigureAwait(false);

                using (var stream = client.GetStream())
                {
                    await stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);

                    var buffer = new byte[8192];
                    var read = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                    var response = Encoding.UTF8.GetString(buffer, 0, read);
                    var ret = response.TrimEnd(Environment.NewLine.ToCharArray());
                    return Encoding.UTF8.GetBytes(ret);
                }
            }

            //var int_ = smethod_240(dict);
            //var source = smethod_246(dict);
            //var text2 = Smethod_231(" ", int_) + "<k";
            //var arg15A0 = source.Cast<DictionaryEntry>();
            //var arg15A1 = text2;
            //var arg15A2 = new Func<string, DictionaryEntry, string>(Method_3);
            //text2 = arg15A0.Aggregate(arg15A1, arg15A2);
            //text2 += ">";

            //return Encoding.UTF8.GetBytes(text2);
        }

        internal static string Method_3(string string0, DictionaryEntry dictionaryEntry0)
        {
            return string.Concat(new string[]
            {
                string0,
                " ",
                dictionaryEntry0.Key.ToString(),
                "=\"",
                dictionaryEntry0.Value.ToString(),
                "\""
            });
        }

        private static string Smethod_231(string string0, int int0)
        {
            return new StringBuilder(string0.Length * int0).Insert(0, string0, int0).ToString();
        }

        private static OrderedDictionary smethod_246(OrderedDictionary orderedDictionary0)
        {
            var orderedDictionary = new OrderedDictionary();
            var num = 2330827939u;
            var num2 = 7;
            while (orderedDictionary0.Count > 0)
            {
                var num3 = smethod_163(0, smethod_36(orderedDictionary0));
                var num4 = smethod_163(2, smethod_176(orderedDictionary0, true));
                var num5 = smethod_163(1, smethod_36(orderedDictionary0));
                var num6 = num ^ num3 << num2 ^ num4 << num2 * 2 ^ num5 << num2 ^ num3;
                int num7;
                if ((num6 & 2147483648u) > 0u)
                {
                    num7 = (int)((2147483648u - (num6 - 2147483648u)) * 4294967295u);
                }
                else
                {
                    num7 = (int)num6;
                }
                num7 %= orderedDictionary0.Count;
                if (num7 < 0)
                {
                    num7 = orderedDictionary0.Count + num7;
                }
                var num8 = 0;
                foreach (DictionaryEntry dictionaryEntry in orderedDictionary0)
                {
                    if (num8 == num7)
                    {
                        orderedDictionary.Add(dictionaryEntry.Key, dictionaryEntry.Value);
                        break;
                    }
                    num8++;
                }
                orderedDictionary0.RemoveAt(num7);
            }
            return orderedDictionary;
        }

        private static string smethod_176(OrderedDictionary orderedDictionary0, bool bool0)
        {
            if (bool0)
            {
                var num = orderedDictionary0.Count;
                var orderedDictionary = new OrderedDictionary();
                foreach (DictionaryEntry dictionaryEntry in orderedDictionary0)
                {
                    orderedDictionary.Insert(0, dictionaryEntry.Key, dictionaryEntry.Value);
                    num--;
                }
                orderedDictionary0 = orderedDictionary;
            }
            return smethod_36(orderedDictionary0);
        }

        private static string smethod_36(OrderedDictionary orderedDictionary0)
        {
            var text = "";
            foreach (DictionaryEntry dictionaryEntry in orderedDictionary0)
            {
                text = text + dictionaryEntry.Key.ToString() + dictionaryEntry.Value.ToString();
            }
            return text;
        }

        private static int smethod_240(OrderedDictionary orderedDictionary0)
        {
            var num = smethod_163(0, smethod_36(orderedDictionary0));
            var num2 = smethod_163(2, smethod_176(orderedDictionary0, true));
            var num3 = smethod_163(1, smethod_36(orderedDictionary0));
            var num4 = 3984710317u ^ num << 13 ^ num2 << 26 ^ num3 << 13 ^ num;
            int num5;
            if ((num4 & 2147483648u) > 0u)
            {
                num5 = (int)((2147483648u - (num4 - 2147483648u)) * 4294967295u);
            }
            else
            {
                num5 = (int)num4;
            }
            num5 %= 29;
            if (num5 < 0)
            {
                num5 += 29;
            }
            return num5;
        }

        private static uint smethod_74(byte byte0, int int0)
        {
            var num = (uint)byte0;
            if ((num & 128u) > 0u)
            {
                num |= 4294967040u;
            }
            return num << int0;
        }

        private static uint smethod_163(int int0, string string0)
        {
            HashAlgorithm hashAlgorithm = null;
            try
            {
                if (int0 == 0)
                {
                    hashAlgorithm = SHA256.Create();
                }
                else if (int0 == 1)
                {
                    hashAlgorithm = SHA1.Create();
                }
                else
                {
                    hashAlgorithm = MD5.Create();
                }
                var array = hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(string0));
                var num = array.Length;
                var num2 = 0u;
                for (var i = 0; i < num; i += 4)
                {
                    var num3 = smethod_74(array[i + 3], 24);
                    var num4 = smethod_74(array[i + 2], 16);
                    var num5 = smethod_74(array[i + 1], 8);
                    var num6 = smethod_74(array[i], 0);
                    var num7 = num6 | num5 | num4 | num3;
                    num2 ^= num7;
                }
                return num2;
            }
            finally
            {
                hashAlgorithm?.Dispose();
            }
        }

        public static byte[] IsTyping(string toJid, bool val = true)
        {
            var ts = Krypto.KikTimestamp();
            return
                Encoding.UTF8.GetBytes(
                    $"<message type=\"is-typing\" to=\"{toJid}\" id=\"{Guid.NewGuid()}\"><kik push=\"false\" qos=\"false\" timestamp=\"{ts}\" /><is-typing val=\"{val.ToString().ToLower()}\" /></message>");
        }

        public static async Task<byte[]> StreamInitPropertyMapAsync(string from, string passkeyU, string deviceTsHash, long timestamp, string signed, string sid, int? n)
        {
            var model = new StreamInitPropertyMap
            {
                From = from,
                P = passkeyU,
                Cv = deviceTsHash,
                Ts = timestamp,
                Signed = signed,
                V = Konstants.AppVersion,
                Sid = sid,
                Lang = "en_US",
                To = "talk.kik.com",
                N = n,
                Conn = "WIFI"
            };

            var jsonData = await Task.Run(() => JsonConvert.SerializeObject(model, Formatting.None)).ConfigureAwait(false);
            var data = Encoding.UTF8.GetBytes(jsonData);

            var host = "127.0.0.1"; //Debugger.IsAttached ? "192.168.0.22" : "127.0.0.1";

            using (var client = new TcpClient())
            {
                await client.ConnectAsync(host, 30592).ConfigureAwait(false);

                using (var stream = client.GetStream())
                {
                    await stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);

                    var buffer = new byte[8192];
                    var read = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                    var response = Encoding.UTF8.GetString(buffer, 0, read);
                    var ret = response.TrimEnd(Environment.NewLine.ToCharArray());
                    //Console.WriteLine(@"Resposne from java tunnel: " + ret);
                    return Encoding.UTF8.GetBytes(ret);
                }
            }
        }

        public static byte[] StreamInitPropertyMap(string from, string passkeyU, string deviceTsHash, long timestamp, string signed, string sid)
        {
            var model = new StreamInitPropertyMap
            {
                From = from,
                P = passkeyU,
                Cv = deviceTsHash,
                Ts = timestamp,
                Signed = signed,
                V = Konstants.AppVersion,
                Sid = sid,
                Lang = "en_US",
                To = "talk.kik.com",
                N = 1,
                Conn = "WIFI"
            };

            var jsonData = JsonConvert.SerializeObject(model, Formatting.None);
            var data = Encoding.UTF8.GetBytes(jsonData);

#if DEBUG
            const string host = "192.168.0.22";
#else
            const string host = "127.0.0.1";
#endif

            const int port = 30592;
            using (var client = new TcpClient())
            {
                client.Connect(host, port);

                using (var stream = client.GetStream())
                {
                    stream.Write(data, 0, data.Length);

                    var buffer = new byte[8192];
                    var read = stream.Read(buffer, 0, buffer.Length);
                    var response = Encoding.UTF8.GetString(buffer, 0, read);
                    var ret = response.TrimEnd(Environment.NewLine.ToCharArray());
                    return Encoding.UTF8.GetBytes(ret);
                }
            }
        }

        public static byte[] Ping()
        {
            return Encoding.UTF8.GetBytes("<ping/>");
        }

        public static byte[] CheckUsername(string username)
        {
            var id = Guid.NewGuid().ToString();
            var str =
                $"<iq type=\"get\" id=\"{id}\"><query xmlns=\"kik:iq:check-unique\"><username>{username}</username></query></iq>";
            return str.Utf8Bytes();
        }

        public static byte[] Register(string email, string passkeyE, string passkeyU, string deviceId, string username,
            string firstName, string lastName, string birthday, string androidId, string carrierCode,
            string manufacturer, string sdkVersion, string model)
        {
            var id = Guid.NewGuid().ToString();
            var str =
                $"<iq type=\"set\" id=\"{id}\"><query xmlns=\"jabber:iq:register\"><email>{email}</email><passkey-e>{passkeyE}</passkey-e><passkey-u>{passkeyU}</passkey-u><device-id>{deviceId}</device-id><username>{username}</username><first>{firstName}</first><last>{lastName}</last><birthday>{birthday}</birthday><device-type>android</device-type><logins-since-install>0</logins-since-install><android-id>{androidId}</android-id><operator>{carrierCode}</operator><install-date>unknown</install-date><brand>{manufacturer}</brand><lang>en_US</lang><version>{Konstants.AppVersion}</version><android-sdk>{sdkVersion}</android-sdk><registrations-since-install>0</registrations-since-install><prefix>CAN</prefix><model>{model}</model></query></iq>";
            return str.Utf8Bytes();
        }

        public static byte[] RegisterCaptcha(string email, string passkeyE, string passkeyU, string deviceId,
            string username, string firstName, string lastName, string birthday, string androidId, string carrierCode,
            string manufacturer, string sdkVersion, string model, string challengeResponse)
        {
            var id = Guid.NewGuid().ToString();
            var str =
                $"<iq type=\"set\" id=\"{id}\"><query xmlns=\"jabber:iq:register\"><email>{email}</email><passkey-e>{passkeyE}</passkey-e><passkey-u>{passkeyU}</passkey-u><device-id>{deviceId}</device-id><username>{username}</username><first>{firstName}</first><last>{lastName}</last><birthday>{birthday}</birthday><challenge><response>{challengeResponse}</response></challenge><device-type>android</device-type><logins-since-install>0</logins-since-install><android-id>{androidId}</android-id><operator>{carrierCode}</operator><install-date>unknown</install-date><brand>{manufacturer}</brand><lang>en_US</lang><version>{Konstants.AppVersion}</version><android-sdk>{sdkVersion}</android-sdk><registrations-since-install>0</registrations-since-install><prefix>CAN</prefix><model>{model}</model></query></iq>";
            return str.Utf8Bytes();
        }

        public static byte[] Login(string loginId, string passkeyU, string deviceId, string op, string installDate, string manufacturer, string sdkVersion, string androidId, string model)
        {
            var str = $"<iq type=\"set\" id=\"{Guid.NewGuid()}\"><query xmlns=\"jabber:iq:register\"><username>{loginId}</username><passkey-u>{passkeyU}</passkey-u><device-id>{deviceId}</device-id><install-referrer>utm_source=google-play&amp;utm_medium=organic</install-referrer><operator>{op}</operator><install-date>{installDate}</install-date><device-type>android</device-type><brand>{manufacturer}</brand><logins-since-install>0</logins-since-install><version>{Konstants.AppVersion}</version><lang>en_US</lang><android-sdk>{sdkVersion}</android-sdk><registrations-since-install>0</registrations-since-install><prefix>CAN</prefix><android-id>{androidId}</android-id><model>{model}</model></query></iq>";
            return str.Utf8Bytes();
        }

        public static byte[] LoginEmail(string email, string passkeyE, string deviceId, string op, string installDate, string manufacturer, string sdkVersion, string androidId, string model)
        {
            var str = $"<iq type=\"set\" id=\"{Guid.NewGuid()}\"><query xmlns=\"jabber:iq:register\"><email>{email}</email><passkey-e>{passkeyE}</passkey-e><device-id>{deviceId}</device-id><install-referrer>utm_source=google-play&amp;utm_medium=organic</install-referrer><operator>{op}</operator><install-date>{installDate}</install-date><device-type>android</device-type><brand>{manufacturer}</brand><logins-since-install>0</logins-since-install><version>{Konstants.AppVersion}</version><lang>en_US</lang><android-sdk>{sdkVersion}</android-sdk><registrations-since-install>0</registrations-since-install><prefix>CAN</prefix><android-id>{androidId}</android-id><model>{model}</model></query></iq>";
            return str.Utf8Bytes();
        }

        public static byte[] LoginCaptcha(string loginId, string passkeyU, string deviceId, string op,
            string installDate, string manufacturer, string sdkVersion, string androidId, string model,
            string challengeResponse)
        {
            var str = $"<iq type=\"set\" id=\"{Guid.NewGuid()}\"><query xmlns=\"jabber:iq:register\"><username>{loginId}</username><passkey-u>{passkeyU}</passkey-u><device-id>{deviceId}</device-id><challenge><response>{challengeResponse}</response></challenge><install-referrer>utm_source=google-play&amp;utm_medium=organic</install-referrer><operator>{op}</operator><install-date>{installDate}</install-date><device-type>android</device-type><brand>{manufacturer}</brand><logins-since-install>0</logins-since-install><version>{Konstants.AppVersion}</version><lang>en_US</lang><android-sdk>{sdkVersion}</android-sdk><registrations-since-install>0</registrations-since-install><prefix>CAN</prefix><android-id>{androidId}</android-id><model>{model}</model></query></iq>";
            return str.Utf8Bytes();
        }

        public static byte[] LoginCaptchaEmail(string email, string passkeyE, string deviceId, string op,
            string installDate, string manufacturer, string sdkVersion, string androidId, string model,
            string challengeResponse)
        {
            var str = $"<iq type=\"set\" id=\"{Guid.NewGuid()}\"><query xmlns=\"jabber:iq:register\"><email>{email}</email><passkey-e>{androidId}</passkey-u><device-id>{deviceId}</device-id><challenge><response>{challengeResponse}</response></challenge><install-referrer>utm_source=google-play&amp;utm_medium=organic</install-referrer><operator>{op}</operator><install-date>{installDate}</install-date><device-type>android</device-type><brand>{manufacturer}</brand><logins-since-install>0</logins-since-install><version>{Konstants.AppVersion}</version><lang>en_US</lang><android-sdk>{sdkVersion}</android-sdk><registrations-since-install>0</registrations-since-install><prefix>CAN</prefix><android-id>{androidId}</android-id><model>{model}</model></query></iq>";
            return str.Utf8Bytes();
        }

        public static byte[] SearchForUsername(string username)
        {
            var str =
                $"<iq type=\"get\" id=\"{Guid.NewGuid()}\"><query xmlns=\"kik:iq:friend\"><item username=\"{username}\" /></query></iq>";
            return str.Utf8Bytes();
        }

        public static byte[] SendMessage(string guid, string toJid, string body)
        {
            var ts = Krypto.KikTimestamp();
            return $"<message type=\"chat\" to=\"{toJid}\" id=\"{guid}\" cts=\"{ts}\"><body>{body}</body><preview>{body}</preview><kik push=\"true\" qos=\"true\" timestamp=\"{ts}\" /><request xmlns=\"kik:message:receipt\" r=\"true\" d=\"true\" /><ri></ri></message>"
                .Utf8Bytes();
        }

        public static byte[] GetUnackedMsgs()
        {
            var str =
                $"<iq type=\"set\" id=\"{Guid.NewGuid()}\"><query xmlns=\"kik:iq:QoS\" cts=\"{Krypto.KikTimestamp()}\"><msg-acks></msg-acks><history attach=\"true\"></history></query></iq>";
            return str.Utf8Bytes();
        }

        public static byte[] MessageDelivered(string fromJid, string msgId)
        {
            var str =
                $"<iq type=\"set\" id=\"{Guid.NewGuid()}\"><query xmlns=\"kik:iq:QoS\" cts=\"{Krypto.KikTimestamp()}\"><msg-acks><sender jid=\"{fromJid}\" convo=\"{fromJid}\"><ack-id receipt=\"true\">{msgId}</ack-id></sender></msg-acks><history attach=\"false\"></history></query></iq>";
            return Encoding.UTF8.GetBytes(str);
        }

        public static byte[] MessageRead(string receivedMsgFromJid, string receivedMsgId)
        {
            var ts = Krypto.KikTimestamp();
            return
                Encoding.UTF8.GetBytes(
                    $"<message type=\"receipt\" id=\"{Guid.NewGuid()}\" to=\"{receivedMsgFromJid}\" cts=\"{ts}\"><kik push=\"false\" qos=\"true\" timestamp=\"{ts}\" /><receipt xmlns=\"kik:message:receipt\" type=\"read\"><msgid id=\"{receivedMsgId}\" /></receipt></message>");
        }

        public static byte[] Add(string jid)
        {
            var str = $"<iq type=\"set\" id=\"{Guid.NewGuid()}\"><query xmlns=\"kik:iq:friend\"><add jid=\"{jid}\" /><context type=\"inline-username-search\" reply=\"false\" /></query></iq>";
            return Encoding.UTF8.GetBytes(str);
        }

        public static byte[] StcSolution(string id, string solution)
        {
            var str = $"<stc id=\"{id}\"><sts>{solution}</sts></stc>";
            return Encoding.UTF8.GetBytes(str);
        }

        public static byte[] ShareLink(ShareLink shareLink)
        {
            var guid = Guid.NewGuid().ToString();

            var ts = Krypto.KikTimestamp();

            var packetStr =
                $"<message type=\"chat\" to=\"{shareLink.ToJid}\" id=\"{guid}\" cts=\"{ts}\"><kik push=\"true\" qos=\"true\" timestamp=\"{ts}\" /><request xmlns=\"kik:message:receipt\" r=\"true\" d=\"true\" /><content id=\"{Guid.NewGuid()}\" app-id=\"com.kik.cards\" v=\"2\"><strings><app-name>Web History</app-name><card-icon>https://home.kik.com/img/icon.png</card-icon><layout>article</layout><title>Web History</title><text>{shareLink.Link}</text><allow-forward>true</allow-forward></strings><extras /><hashes /><images><icon>{IconBytes}</icon></images><uris><uri platform=\"cards\">{shareLink.Link}</uri><uri></uri><uri>http://cdn.kik.com/cards/unsupported.html</uri></uris></content></message>";
            return packetStr.Utf8Bytes();
        }

        private const string IconBytes = "iVBORw0KGgoAAAANSUhEUgAAADIAAAAyCAIAAACRXR/mAAAAA3NCSVQICAjb4U/gAAAFYUlEQVRYhc1ZW2xURRj+Zrplaelld9ltibfYlkJbetGA0UcNrSakXhKSmvCgJkLpBqFB4osvxsQHA71Ba7sVQr0HEhJDwMRLgiaKASJgW6UVq8ZosN3upZVya8uOD3PO2TnnzJndtQ/dLyeb8833n3/++ec/M7O7ZOORWWQfXIwBYEILyQZKzRqyhLpMySJmg+WjLlOUWZEpgMHF2xhAhNlddspLnjeZYl5e6sKS8WlL4YOeHLGl5/yt94fvLMUnaRiM/4/CXO+nx7cWpdPBQ4MzGXnmlDSE4hk90dGU31i+gpPuczwrcuMfd3qF4OIZhUbqQzHHkdowrPfUMBh3kiwGR5oLHrk3F8CmwzMLCcsr5wjzAuGM+4vo6W3FAHh2h9u8ABhjc/Os0E31lKMhFHdRXGz11gZyfgrfBbD91By3/2GHZzHBNr47k053pG4gRnTCI7TTNx/Pf67KDaA+FB/e6SGEwIb6UPxyqyeHEiPu+lBcdPX1C8Wr8ykP3akjg1I+aMbA9KXCQu8rIkZM3Gn9gHUG6wdiABYT4AExxrix6OqJD2a/mLgD4JsXi6UdiZRCa2VcsdPPtnm0ji2qgJGgb6TNO3ObGVTq6rWvbiQY8+ZRqSpSygDFNRL0AagbiNkb7SgtoPzGYi9efIpHgj51v1Qhdj25CkBdf8wqpcJIm1fhtq4/BmA06FPYuBgYQPRtSSs+Tpsq3ND2KZOqjqmuP2Z3ZaOQejYo5XPJkrOr0arVFEBtf9SuqsOyu7LT2v4ogKFnC52MqTgpTJijE897ksOwqU74eWpBaiyhAF9mpaqp5GG+7/j+hlRVoOXEv3ZXUlr7ThTOqljyzCIOXbotVXecNH0r2dAX3dAX2dAXFdybXckoT5h3JZGqlGmLBk+kdn9yWzEEalHP/rVQ0xfhMdX0RQxVvzcZKyiA7172SVXzAqEPd61PP4epZwJornSDoYmfKRjObfcV5JK0ZlF8IW0qNbUwsz2DVOVXdW8EwP6nCq/s9h/cUsTVRw/Hrs8zu7GUJgduU6mlhDOi1b2R6t7IrtOzAMZ2+5fiykL5wUZsFBY8xtL5znnm9/nqQ9NjewL+PBK5mQDwVmPB1po8ADfmE5tCUednjV6sqos5x22e0hSDrDo0zW/G9wSMxlUr6NiegK7Kn2VmyiEv+TfOXE8aq8vWTmW4HPRLS/7v2UV5yduWDDDg2OhtCNSiKqgT8nLJeHuAX6Lx5vfiUldUMfxf2gOZ5mq8PWCPScT4NN+d2MgufzJpNlfaJPJjIdOl5AvsoDrRoxdvqsN65uM4N3a7iDYhMlfUUg8GXdcTBnCsxSNVnejb384pYvpzZpHfBPKp0YXUFYW+4ENMlW718D0rHFUHuq47DAc0DmknyrOtfp4qJ1eU6fVqXAat7J4C8OveEqmqoPxBCyq7p7h6dW+JSKWuqCLnAGoOhgFcaEtRyClhBFrhzQEwFl5Q21MwU8wWunCXLSaYN49qu146BwPxjQEArO2aMtTPX/IDePqjmNpV6j2xqicMYMv6vKt7S1IacxS7k+f9tV1anqoCOROvlhotalekomOSgfENiYAAkNILwYAvPwdARedkSuOJfWsAvP7l7PHRW1z9bd8aLcrOSfWznJLyjn+QNgzvFZ2TajPDwJ9PzwdLAIyFF5o/jCqeEpHZz24VnZM8Mv7pFNz8XQbgSnspXzMVlk4gZQcyyJaBT1q8jz3gTseyvCOzgDhI2YFryt9OUlDPSnrplVKL0z9ii5uPTmfqSqSkbP814y3Imp/l+d8F5pizgerfqpnZZLmp8eeKcYoj2UBd2TR1Sbgs+1eWIMUJYrnwH9OvdDKab//KAAAAAElFTkSuQmCC";

        public static byte[] ShareImage(ShareImage shareImage)
        {
            var fileName = Guid.NewGuid();
            var ts = Krypto.KikTimestamp();
            var str = $"<message type=\"chat\" to=\"{shareImage.ToJid}\" id=\"{Guid.NewGuid()}\" cts=\"{ts}\"><kik push=\"true\" qos=\"true\" timestamp=\"{ts}\" /><request xmlns=\"kik:message:receipt\" r=\"true\" d=\"true\" /><content id=\"{fileName}\" app-id=\"com.kik.ext.gallery\" v=\"2\"><strings><app-name>Gallery</app-name><file-size>{shareImage.ImageLength}</file-size><file-name>{fileName}.jpg</file-name><allow-forward>true</allow-forward></strings><extras /><images><preview>{shareImage.Base64ImageData}</preview><icon>{IconBytes}</icon></images><uris /></content></message>";
            return str.Utf8Bytes();
        }

        private static string ContactsStr(List<string> input)
        {
            var sb = new StringBuilder();
            foreach (var c in input)
                sb.Append($"<email>{c}</email>");

            return sb.ToString();
        }

        public static byte[] ImportContacts(List<string> emails)
        {
            var contacts = ContactsStr(emails);
            var text =
                $"<iq type=\"set\" id=\"{Guid.NewGuid()}\"><match xmlns=\"kik:iq:matching\"><my /><contacts>{contacts}</contacts></match></iq>";

            return text.Utf8Bytes();
        }
    }
}