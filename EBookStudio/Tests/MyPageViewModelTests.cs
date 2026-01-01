using EBookStudio.Helpers;
using EBookStudio.Models;
using EBookStudio.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using Xunit;
using static EBookStudio.Models.ApiService;

namespace EBookStudio.Tests
{
    public class MyPageViewModelTests
    {
        private readonly MyPageViewModel _viewModel;
        private readonly FakeAuthService _fakeAuth;
        private readonly FakeDialogService _fakeDialog;
        private readonly FakeBookFileSystem _fakeFile;
        private const string TestUser = "TestUser";

        public MyPageViewModelTests()
        {
            _fakeAuth = new FakeAuthService();
            _fakeDialog = new FakeDialogService();
            _fakeFile = new FakeBookFileSystem();

            _viewModel = new MyPageViewModel(TestUser, _fakeAuth, _fakeDialog, _fakeFile);
        }

        // 1. 비밀번호 변경 테스트 (UIFact 필수)
        [UIFact]
        public void ChangePassword_Mismatch_ShouldShowErrorMessage()
        {
            // Arrange
            var newBox = new PasswordBox { Password = "password123" };
            var confirmBox = new PasswordBox { Password = "different_password" };
            var parameter = new object[] { newBox, confirmBox };

            // Act
            _viewModel.ChangePasswordCommand.Execute(parameter);

            // Assert
            Assert.Contains("일치하지 않습니다", _fakeDialog.LastMessage);
        }

        // 2. 로컬 진도율 초기화 테스트
        [Fact]
        public void ResetHistory_UserConfirmed_ShouldDeleteProgressFiles()
        {
            // Arrange
            _fakeDialog.ConfirmResult = true; // 사용자가 '예'를 눌렀다고 가정
            _fakeFile.ExistingDirectories = new[] { "C:\\Book1", "C:\\Book2" };
            _fakeFile.ExistingFiles = new List<string> { "C:\\Book1\\progress.json" };

            // Act
            _viewModel.ResetHistoryCommand.Execute(null);

            // Assert
            Assert.True(_fakeFile.IsDeleteFileCalled);
            Assert.Contains("초기화되었습니다", _fakeDialog.LastMessage);
        }

        // 3. 서버 데이터 로드 테스트
        [Fact]
        public async Task LoadServerData_ShouldFillCollections()
        {
            // Arrange
            _fakeAuth.MockServerBooks = new List<ServerBookDto>
            {
                new ServerBookDto { title = "ServerBook1", cover_url = "url1" },
                new ServerBookDto { title = "ServerBook2", cover_url = "url2" }
            };

            // Act
            if (_viewModel.LoadServerDataCommand is AsyncRelayCommand cmd)
                await cmd.ExecuteAsync(null);

            // Assert
            Assert.Equal(2, _viewModel.ServerDeleteList.Count);
            Assert.Equal("ServerBook1", _viewModel.ServerDeleteList[0].Title);
        }

        // 4. 서버 책 삭제 테스트
        [Fact]
        public async Task DeleteServerBook_Single_ShouldRemoveFromList()
        {
            // Arrange
            var item = new ServerBookItem { Title = "DeleteMe", IsSelected = true };
            _viewModel.ServerDeleteList.Add(item);
            _viewModel.ServerDownloadList.Add(item);

            _fakeDialog.ConfirmResult = true;
            _fakeAuth.DeleteServerBookResult = true;

            // Act
            if (_viewModel.DeleteSingleServerBookCommand is AsyncRelayCommand cmd)
                await cmd.ExecuteAsync(item);

            // Assert
            Assert.Empty(_viewModel.ServerDeleteList);
            Assert.Empty(_viewModel.ServerDownloadList);
            Assert.Contains("삭제 처리 완료", _fakeDialog.LastMessage);
        }

        // 5. 계정 탈퇴 테스트
        [Fact]
        public async Task DeleteAccount_ShouldCallResetAndLogout()
        {
            // Arrange
            _fakeDialog.ConfirmResult = true;
            bool logoutCalled = false;
            _viewModel.RequestLogout += () => logoutCalled = true;

            // Act
            if (_viewModel.DeleteAccountCommand is AsyncRelayCommand cmd)
                await cmd.ExecuteAsync(null);

            // Assert
            Assert.True(_fakeFile.IsResetUserDataCalled);
            Assert.True(logoutCalled);
            Assert.Contains("탈퇴되었습니다", _fakeDialog.LastMessage);
        }
    }
}