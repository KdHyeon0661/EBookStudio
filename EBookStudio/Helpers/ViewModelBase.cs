using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EBookStudio.Helpers
{
    public class ViewModelBase : INotifyPropertyChanged
    {
        // 이벤트 핸들러가 null일 수 있음을 ?로 표시
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}