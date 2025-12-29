using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Threading.Tasks;
using EBookStudio.Models; // Models 사용
using EBookStudio.Helpers; // Helpers 사용

namespace EBookStudio.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainVM;
        private string _username = string.Empty;

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        public ICommand LoginCommand { get; }
        public ICommand GoRegisterCommand { get; }

        public ICommand FindAccountCommand { get; }
        public LoginViewModel(MainViewModel mainVM)
        {
            _mainVM = mainVM;
            LoginCommand = new RelayCommand(async (o) => await ExecuteLogin(o));
            GoRegisterCommand = new RelayCommand(o => _mainVM.NavigateToRegister());

            FindAccountCommand = new RelayCommand(o => _mainVM.NavigateToFindAccount());
        }

        private async Task ExecuteLogin(object? parameter)
        {
            var passwordBox = parameter as PasswordBox;
            var password = passwordBox?.Password;

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("아이디와 비밀번호를 입력하세요.");
                return;
            }

            bool success = await ApiService.LoginAsync(Username, password);
            if (success)
            {
                MessageBox.Show($"로그인 성공! 환영합니다 {Username}님.");
                _mainVM.SetLoginSuccess(Username);
            }
            else
            {
                MessageBox.Show("로그인 실패: 아이디 또는 비밀번호가 틀립니다.");
            }
        }

        public void Clear()
        {
            Username = string.Empty;
            // PasswordBox는 View에서 처리
        }
    }
}