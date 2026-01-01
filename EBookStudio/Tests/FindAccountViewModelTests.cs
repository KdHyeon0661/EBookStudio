using Xunit;
using EBookStudio.ViewModels;
using EBookStudio.Helpers;
using System.Threading.Tasks;

namespace EBookStudio.Tests
{
    public class FindAccountViewModelTests
    {
        private FindAccountViewModel _viewModel;
        private MainViewModel _dummyMain;
        private FakeAccountService _fakeApi;
        private FakeDialogService _fakeDialog;

        public FindAccountViewModelTests()
        {
            _dummyMain = new MainViewModel();
            _fakeApi = new FakeAccountService();
            _fakeDialog = new FakeDialogService();

            // [핵심] 가짜 서비스들을 주입! (이제 MessageBox 안 뜨고, 서버 요청 안 날아감)
            _viewModel = new FindAccountViewModel(_dummyMain, _fakeApi, _fakeDialog);
        }

        [Fact]
        public void SendCodeCommand_WithEmptyEmail_ShouldShowError()
        {
            // Arrange
            _viewModel.Email = "";

            // Act
            if (_viewModel.SendCodeCommand.CanExecute(null))
                _viewModel.SendCodeCommand.Execute(null);

            // Assert: 다이얼로그가 호출되었는지 확인
            Assert.Contains("이메일을 입력해주세요", _fakeDialog.LastMessage);
            Assert.False(_viewModel.IsCodeSent); // 전송 안 됨 확인
        }

        [Fact]
        public void SendCodeCommand_Success_ShouldUpdateState()
        {
            // Arrange
            _viewModel.Email = "success@test.com"; // 가짜 API가 성공으로 처리할 이메일

            // Act
            _viewModel.SendCodeCommand.Execute(null);

            // Assert
            Assert.True(_viewModel.IsCodeSent);
            Assert.Contains("발송되었습니다", _fakeDialog.LastMessage);
        }

        [Fact]
        public void VerifyCodeCommand_Success_ShouldSetVerified()
        {
            // Arrange
            _viewModel.Email = "success@test.com";
            _viewModel.Code = "123456";

            // Act
            _viewModel.VerifyCodeCommand.Execute(null);

            // Assert
            Assert.True(_viewModel.IsVerified);
            Assert.Equal("TestUser", _viewModel.FoundUsername); // 가짜 API 반환값
        }

        [Fact]
        public void ResetPassword_Success_ShouldNavigateLogin()
        {
            // Arrange
            _viewModel.Email = "success@test.com";
            _viewModel.Code = "123456";
            _viewModel.NewPassword = "newpassword123";

            // Act
            _viewModel.ResetPasswordCommand.Execute(null);

            // Assert
            // MainViewModel의 상태가 Login으로 바뀌었는지 확인 (MainVM 구현에 따라 다름)
            // 여기선 다이얼로그 메시지로 성공 확인
            Assert.Contains("변경되었습니다", _fakeDialog.LastMessage);
        }

        [Fact]
        public void Clear_ShouldResetAllProperties()
        {
            // Arrange
            _viewModel.Email = "test";
            _viewModel.IsCodeSent = true;

            // Act
            _viewModel.Clear();

            // Assert
            Assert.Empty(_viewModel.Email);
            Assert.False(_viewModel.IsCodeSent);
        }
    }
}