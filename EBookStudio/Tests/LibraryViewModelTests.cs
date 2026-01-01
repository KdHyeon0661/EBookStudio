using EBookStudio.Helpers;
using EBookStudio.Models;
using EBookStudio.ViewModels;
using System.Reflection;
using System.Windows;
using Xunit;
using System.IO;

namespace EBookStudio.Tests
{
    public class LibraryViewModelTests
    {
        private LibraryViewModel _viewModel;
        private MainViewModel _dummyMain;

        // 가짜 서비스들
        private FakeLibraryService _fakeLib;
        private FakeDialogService _fakeDialog;
        private FakeFilePickerService _fakePicker;

        public LibraryViewModelTests()
        {
            _dummyMain = new MainViewModel { LoggedInUser = "TestUser", IsNetworkAvailable = true };
            _fakeLib = new FakeLibraryService();
            _fakeDialog = new FakeDialogService();
            _fakePicker = new FakeFilePickerService();

            // [핵심] 모든 외부 의존성을 가짜로 주입
            _viewModel = new LibraryViewModel(_dummyMain, _fakeLib, _fakeDialog, _fakePicker);
        }

        [Fact]
        public void SearchText_ShouldFilterBooks()
        {
            // Arrange: Reflection을 사용하여 private List<Book> _allBooks에 데이터 강제 주입
            // (ViewModel의 LoadLibrary가 파일 시스템을 쓰므로, 테스트에선 이렇게 데이터를 넣는게 깔끔합니다)
            var allBooksField = typeof(LibraryViewModel)
                .GetField("_allBooks", BindingFlags.NonPublic | BindingFlags.Instance);

            var testData = new List<Book>
            {
                new Book { Title = "Apple", Author = "User1", CreatedAt = System.DateTime.Now },
                new Book { Title = "Banana", Author = "User1", CreatedAt = System.DateTime.Now },
                new Book { Title = "Cherry", Author = "User1", CreatedAt = System.DateTime.Now }
            };

            // private 필드에 값 설정
            allBooksField.SetValue(_viewModel, testData);

            // 초기화 (DisplayBooks 갱신)
            _viewModel.SearchText = "";

            // Act: 검색어 입력 ("Ban")
            _viewModel.SearchText = "Ban";

            // Assert: "Banana"만 남았는지 확인
            // (첫 번째 아이템은 '책 추가 버튼'일 수 있으므로 필터링해서 확인)
            var resultBook = _viewModel.DisplayBooks.FirstOrDefault(b => !b.IsAddButton);

            Assert.NotNull(resultBook);
            Assert.Equal("Banana", resultBook.Title);
            Assert.Single(_viewModel.DisplayBooks.Where(b => !b.IsAddButton)); // 검색 결과는 1개여야 함
        }

        [Fact]
        public void AddBook_NotLoggedIn_ShouldShowAlert()
        {
            // Arrange
            _dummyMain.LoggedInUser = null; // 로그아웃 상태

            // Act
            if (_viewModel.AddBookCommand.CanExecute(null))
            {
                _viewModel.AddBookCommand.Execute(null);
            }

            // Assert
            Assert.Contains("로그인", _fakeDialog.LastMessage);
        }

        [Fact]
        public async Task AddBook_UploadSuccess_ShouldAddBookToList()
        {
            // 1. Arrange: 환경 설정
            _dummyMain.LoggedInUser = "User1";
            _dummyMain.IsLoggedIn = true;
            _dummyMain.IsNetworkAvailable = true;

            // [핵심] 가짜 PDF 파일 생성 (CheckCopyrightAndDRM 통과용)
            // 테스트 환경의 임시 폴더에 파일을 만듭니다.
            string tempFilePath = Path.Combine(Path.GetTempPath(), "MyTestBook.pdf");

            // CheckCopyrightAndDRM이 검사하는 "%PDF" 헤더를 파일 시작 부분에 씁니다.
            await File.WriteAllBytesAsync(tempFilePath, System.Text.Encoding.ASCII.GetBytes("%PDF-1.7 Test Content"));

            _fakePicker.FilePathToReturn = tempFilePath; // 생성한 임시 파일 경로를 전달
            _fakeLib.ShouldSucceed = true;

            // 2. Act
            // 커맨드 실행 (내부적으로 CheckCopyrightAndDRM이 실행됨)
            _viewModel.AddBookCommand.Execute(null);

            // 분석 및 업로드 로직이 비동기로 흐르므로 넉넉히 대기
            await Task.Delay(2000);

            // 3. Assert
            // 원본 리스트(_allBooks)를 가져와서 확인
            var allBooksField = typeof(LibraryViewModel).GetField("_allBooks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var allBooks = (List<Book>)allBooksField.GetValue(_viewModel);

            Assert.NotNull(allBooks);
            Assert.NotEmpty(allBooks); // 이제 파일 체크를 통과했으므로 비어있지 않음!

            var addedBook = allBooks.FirstOrDefault(b => b.Title == "MyTestBook");
            Assert.NotNull(addedBook);
            Assert.Equal("MyTestBook", addedBook.Title);

            // [Clean up] 사용한 임시 파일 삭제
            if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
        }

        [Fact]
        public async Task AddBook_UploadFail_ShouldRemoveBookAndShowError()
        {
            // Arrange
            _dummyMain.LoggedInUser = "User1";

            // [해결책] IsLoggedIn이 public이니까 그냥 대놓고 true로 설정하면 됩니다!
            _dummyMain.IsLoggedIn = true;

            // (혹시 모를 안전장치로 토큰도 넣어두면 더 좋음, 필수는 아닐 수 있음)
            typeof(ApiService).GetProperty("CurrentToken").SetValue(null, "TEST_TOKEN");

            _fakePicker.FilePathToReturn = "C:\\Fail.pdf";
            _fakeLib.ShouldSucceed = false;

            _fakeDialog.ShowMessage("");

            // Act
            _viewModel.AddBookCommand.Execute(null);

            // Assert
            await Task.Delay(2000);

            var addedBook = _viewModel.DisplayBooks.FirstOrDefault(b => !b.IsAddButton);
            Assert.Null(addedBook);

            Assert.Contains("올바른 PDF 파일이 아닙니다.", _fakeDialog.LastMessage);

            // [청소]
            typeof(ApiService).GetProperty("CurrentToken").SetValue(null, null);
        }
    }
}