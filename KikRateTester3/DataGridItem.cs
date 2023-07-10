using System.ComponentModel;
using System.Runtime.CompilerServices;
using KikRateTester3.Annotations;

namespace KikRateTester3
{
    public class DataGridItem : INotifyPropertyChanged
    {
        private string _account;
        public string Account
        {
            get
            {
                return _account;
            }
            set
            {
                if (_account == value)
                    return;

                _account = value;
                OnPropertyChanged();
            }
        }

        private string _status;
        public string Status
        {
            get
            {
                return _status;
            }
            set
            {
                if (_status == value)
                    return;

                _status = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
