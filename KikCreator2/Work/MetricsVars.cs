using DankLibWaifuz.Etc;

namespace KikCreator2.Work
{
    internal class MetricsVars
    {
        public double Core50SetupTime { get; }
        public double Core95SetupTime { get; }
        public double Core50LaunchTime { get; }
        public double Core95LaunchTime { get; }

        public MetricsVars()
        {
            //Core50SetupTime
            var setupTimeStr = $"0.{Mode.Random.Next(45, 63)}{GeneralHelpers.RandomString(14, GeneralHelpers.GenType.Num)}";
            var setupTime = double.Parse(setupTimeStr);

            Core50SetupTime = setupTime;
            Core95SetupTime = setupTime;

            var launchTimeStr = $"0.00{GeneralHelpers.RandomString(16, GeneralHelpers.GenType.Num)}";
            var launchTime = double.Parse(launchTimeStr);

            Core50LaunchTime = launchTime;
            Core95LaunchTime = launchTime;
        }
    }
}