using DankLibWaifuz.UIWaifu;

namespace KikBot3.Declarations
{
    public class DataGridItem : UiDataGridItem
    {

        private int _inCount;
        private int _outCount;

#if BLASTER
        private int _blastsCount;
#endif

        public string In => _inCount.ToString("N0");
        public string Out => _outCount.ToString("N0");

#if BLASTER
        public string Blasts => _blastsCount.ToString("N0");

        public int BlastsCount
        {
            get { return _blastsCount; }
            set
            {
                _blastsCount = value;
                OnPropertyChanged(nameof(Blasts));
            }
        }

#endif

        public int InCount
        {
            get { return _inCount; }
            set
            {
                _inCount = value;
                OnPropertyChanged(nameof(In));
            }
        }



        public int OutCount
        {
            get { return _outCount; }
            set
            {
                _outCount = value;
                OnPropertyChanged(nameof(Out));
            }
        }
    }
}
