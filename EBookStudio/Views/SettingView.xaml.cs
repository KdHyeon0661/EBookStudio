using System.Windows.Controls;
using EBookStudio.ViewModels;
using EBookStudio.Helpers;

namespace EBookStudio.Views
{
    public partial class SettingView : UserControl
    {
        public SettingView()
        {
            InitializeComponent();

            // XAML 대신 여기서 DataContext를 설정합니다.
            // 이렇게 하면 생성자 주입 로직이 안전하게 실행됩니다.
            if (this.DataContext == null)
            {
                this.DataContext = new SettingViewModel();
            }
        }
    }
}