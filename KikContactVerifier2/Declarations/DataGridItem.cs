using DankLibWaifuz.UIWaifu;

namespace KikContactVerifier2.Declarations
{
    public class DataGridItem : UiDataGridItem
    {
        public string VerifiedSession => _verifiedSessionCount.ToString("N0");

        private int _verifiedSessionCount;
        public int VerifiedSessionCount
        {
            get
            {
                return _verifiedSessionCount;
            }
            set
            {
                _verifiedSessionCount = value;
                OnPropertyChanged(nameof(VerifiedSession));
            }
        }

        public string AttemptsSession => _attemptsSessionCount.ToString("N0");

        private int _attemptsSessionCount;
        public int AttemptsSessionCount
        {
            get
            {
                return _attemptsSessionCount;
            }
            set
            {
                _attemptsSessionCount = value;
                OnPropertyChanged(nameof(AttemptsSession));
            }
        }

        public string Verified => _verifiedCount.ToString("N0");

        private int _verifiedCount;
        public int VerifiedCount
        {
            get
            {
                return _verifiedCount;
            }
            set
            {
                _verifiedCount = value;
                OnPropertyChanged(nameof(Verified));
            }
        }

        public string Attempts => _attemptsCount.ToString("N0");

        private int _attemptsCount;
        public int AttemptsCount
        {
            get
            {
                return _attemptsCount;
            }
            set
            {
                _attemptsCount = value;
                OnPropertyChanged(nameof(Attempts));
            }
        }
    }
}
