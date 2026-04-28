using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SANJET.Core.ViewModels
{
    public class LoadingWindowViewModel : INotifyPropertyChanged
    {
        private string _statusText = "正在初始化連線...";

        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
