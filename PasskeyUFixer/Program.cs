using KikWaifu;
using System.IO;

namespace PasskeyUFixer
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var streamReader = new StreamReader(
                @"Z:\win7_share\src\c#\KikApps\PasskeyUFixer\bin\Debug\accts.txt"
            );

            var outputStream = new StreamWriter("fixed.txt", true);

            using (outputStream)
            using (streamReader)
            {
                while (!streamReader.EndOfStream)
                {
                    var line = streamReader.ReadLine();
                    var split = line.Split(':');
                    if (split.Length != 9)
                        continue;

                    var username = split[3].ToLower();
                    var password = split[1];
                    var oldPasskeyU = split[7];
                    var newPasskeyU = Krypto.KikPasskey(username, password);
                    var newLine = line.Replace(oldPasskeyU, newPasskeyU);

                    outputStream.WriteLine(newLine);
                }
            }
        }

        private static void HandleLine(string line)
        {
        }
    }
}