using DankLibWaifuz.UIWaifu;

namespace KikBot2.Declarations
{
    public class DataGridItem : UiDataGridItem
    {
        public string In => _inCount.ToString("N0");

        private int _inCount;
        public int InCount
        {
            get { return _inCount; }
            set
            {
                _inCount = value;
                OnPropertyChanged(nameof(In));
            }
        }

        public string Out => _outCount.ToString("N0");

        private int _outCount;
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
