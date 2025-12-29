using System.Windows;
using System.Windows.Controls;
using EBookStudio.ViewModels;

namespace EBookStudio.Views
{
    public partial class RegisterView : UserControl
    {
        public RegisterView()
        {
            InitializeComponent();

            // 화면이 보일 때마다 비밀번호 칸 비우기
            this.Loaded += (s, e) =>
            {
                if (pbReg != null) pbReg.Password = string.Empty;
                if (pbConfirm != null) pbConfirm.Password = string.Empty;
            };
        }

        // [추가] 가입하기 버튼 클릭 시 실행
        private void OnRegisterClick(object sender, RoutedEventArgs e)
        {
            // 1. 현재 연결된 ViewModel 가져오기
            if (DataContext is RegisterViewModel vm)
            {
                // 2. 입력된 비밀번호 두 개 가져오기
                string pw1 = pbReg.Password;
                string pw2 = pbConfirm.Password;

                // 3. 배열로 묶기
                object[] passwords = new object[] { pw1, pw2 };

                // 4. ViewModel의 명령어 실행 (비밀번호 뭉치 전달)
                if (vm.RegisterCommand.CanExecute(passwords))
                {
                    vm.RegisterCommand.Execute(passwords);
                }
            }
        }
    }
}