using DankLibWaifuz.Etc;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using System;
using System.Security.Cryptography;
using System.Text;

namespace KikWaifu
{
    public static class Krypto
    {
        private const string
           KikModulus =
               "d11650420da95d4a346ed889ef4648cf2def966f7537aa4fb90e178d24bc31c8a7f0b7b57d9b70d80b4e7185b3cc80da6da9ee12a814ba3187ae2f671e5f6729";

        private const string
            KikPrivateExponent =
               "4ff842d620b78870db41121d1fa13833d593ef6bcddf6e8a73730a5af793eb4dec50058595a11ae53f3418188ffea5edf4f683a0959c74a1ebed1a83abef27a1";

        private static readonly byte[] HmacSha1SecretKey = Encoding.UTF8.GetBytes("FudK+Te81ysA4xvUSN4p28e+JUc=");

        private static readonly RsaKeyParameters KikPrivateRsaKey = new RsaKeyParameters(true, new BigInteger(KikModulus, 16), new BigInteger(KikPrivateExponent, 16));

        private static string RsaSign(string data, ICipherParameters key)
        {
            var sig = SignerUtilities.GetSigner("SHA256withRSA");
            sig.Init(true, key);
            var bytes = Encoding.UTF8.GetBytes(data);
            sig.BlockUpdate(bytes, 0, bytes.Length);
            var signature = sig.GenerateSignature();
            var signedString = Convert.ToBase64String(signature);
            return signedString;
        }

        public static string KikRsaSign(string identifier, string version, string ts, string sid)
        {
            var ret = RsaSign($"{identifier}:{version}:{ts}:{sid}", KikPrivateRsaKey).TrimEnd('=')
                .Replace("/", "_")
                .Replace("+", "-");
            return ret;
        }

        //ty panda senpai
        public static string KikPasskey(string loginId, string password)
        {
            try
            {
                const int i = 20;
                var j = (-1 + (i + 16)) / i;
                var arrayOfByte1 = new byte[4];
                var arrayOfByte2 = new byte[j * i];
                for (var k = 1; k <= j; k++)
                {
                    arrayOfByte1[0] = ((byte)(k >> 24));
                    arrayOfByte1[1] = ((byte)(k >> 16));
                    arrayOfByte1[2] = ((byte)(k >> 8));
                    arrayOfByte1[3] = ((byte)k);
                    var arrayOfByte3 = KikSha1Bytes(password);
                    var arrayOfByte4 = Encoding.UTF8.GetBytes($"{loginId}niCRwL7isZHny24qgLvy");

                    var n = i * (k - 1);
                    byte[] arrayOfByte5;
                    using (var hmacSha1 = new HMACSHA1(arrayOfByte3))
                    {
                        var block = new byte[arrayOfByte4.Length + arrayOfByte1.Length];
                        Array.Copy(arrayOfByte4, 0, block, 0, arrayOfByte4.Length);
                        Array.Copy(arrayOfByte1, 0, block, arrayOfByte4.Length, arrayOfByte1.Length);
                        arrayOfByte5 = hmacSha1.ComputeHash(block);
                    }
                    Array.Copy(arrayOfByte5, 0, arrayOfByte2, n, arrayOfByte5.Length);

                    for (var i1 = 1; i1 < 8192; i1++)
                    {
                        using (var hmacSha1 = new HMACSHA1(arrayOfByte3))
                            arrayOfByte5 = hmacSha1.ComputeHash(arrayOfByte5);

                        for (var i2 = 0; i2 != arrayOfByte5.Length; i2++)
                        {
                            var i3 = n + i2;
                            arrayOfByte2[i3] = ((byte)(arrayOfByte2[i3] ^ arrayOfByte5[i2]));
                        }
                    }
                }
                return arrayOfByte2.ToHex().Substring(0, 32);
            }
            catch
            {
                //ignored
            }

            return null;
        }

        public static string KikHmacSha1Signature(string timestamp, string identifier)
        {
            var bytes = Encoding.UTF8.GetBytes($"{timestamp}:{identifier}");
            var ret = HmacSha1(HmacSha1SecretKey, bytes);
            return ret;
        }

        private static byte[] KikSha1Bytes(string password)
        {
            using (var sha1 = new SHA1CryptoServiceProvider())
            {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(password));
                var str = hash.ToHex();

                var ret = new byte[str.Length];
                for (var i = 0; i < str.Length; i++)
                    ret[i] = (byte)str[i];
                return ret;
            }
        }

        private static string HmacSha1(byte[] key, byte[] input)
        {
            using (var hmacSha1 = new HMACSHA1(key))
            {
                var hash = hmacSha1.ComputeHash(input);
                var ret = hash.ToHex();
                return ret;
            }
        }

        private const int Roach = 0;

        public static string KikTimestamp()
        {
            var fuckBoy = DateTime.UtcNow.CurrentTimeMillis() + Roach;
            var roachBoy = RoachTime(fuckBoy);
            return roachBoy.ToString();
        }

        private static long RoachTime(long arg12)
        {
            try
            {
                long v4 = 30 & ((65280 & arg12) >> 8 ^ (16711680 & arg12) >> 16 ^ (-16777216 & arg12) >> 24);
                long v0 = (224 & arg12) >> 5;
                long v6 = -255 & arg12;
                long v8 = 4;

                if (v4 % v8 == 0)
                {
                    v0 = v0 / 3 * 3;
                }
                else
                {
                    goto label_38;
                }

                goto label_32;


            label_38:
                v8 = 2;
                v0 = v0 / v8 * 2;

            label_32:
                return v0 << 5 | v6 | v4;
            }
            catch
            {
                return DateTime.UtcNow.CurrentTimeMillis();
            }
        }
    }
}
