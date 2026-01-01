using Xunit;
using EBookStudio.ViewModels;
using EBookStudio.Models;
using EBookStudio.Helpers;
using System.Collections.Generic;
using System.Linq;

namespace EBookStudio.Tests
{
    public class NoteViewModelTests
    {
        private NoteViewModel _viewModel;
        private MainViewModel _dummyMain;
        private Book _testBook;
        private FakeNoteService _fakeNote;

        public NoteViewModelTests()
        {
            _dummyMain = new MainViewModel { LoggedInUser = "TestUser" };
            _testBook = new Book { Title = "TestBook" };
            _fakeNote = new FakeNoteService();

            // 초기 데이터 세팅
            _fakeNote.FakeData = (
                new List<NoteItem> { new NoteItem { PageNumber = 10, Content = "Bookmark 1" } },
                new List<NoteItem> { new NoteItem { PageNumber = 20, Content = "Highlight 1" } },
                new List<NoteItem> { new NoteItem { PageNumber = 30, Content = "Memo 1" } }
            );

            _viewModel = new NoteViewModel(_dummyMain, _testBook, 1, _fakeNote);
        }

        [Fact]
        public void LoadData_ShouldFillCollections()
        {
            // Assert: 생성자에서 LoadData가 실행되므로 바로 확인 가능
            Assert.Single(_viewModel.Bookmarks);
            Assert.Equal(10, _viewModel.Bookmarks[0].PageNumber);
            Assert.Equal("총 1개", _viewModel.BookmarkCount);
        }

        [Fact]
        public void SwitchTabCommand_ShouldChangeIndex()
        {
            // Act: 1번 탭(하이라이트)으로 변경 요청
            _viewModel.SwitchTabCommand.Execute("1");

            // Assert
            Assert.Equal(1, _viewModel.SelectedTabIndex);
        }

        [Fact]
        public void DeleteItemCommand_ShouldCallRemoveAndRefresh()
        {
            // Arrange
            var itemToDelete = _viewModel.Bookmarks[0];
            // 삭제 후 LoadNotes가 빈 목록을 주도록 설정
            _fakeNote.FakeData = (new List<NoteItem>(), new List<NoteItem>(), new List<NoteItem>());

            // Act
            _viewModel.DeleteItemCommand.Execute(itemToDelete);

            // Assert
            Assert.True(_fakeNote.IsRemoveCalled);
            Assert.Empty(_viewModel.Bookmarks); // 다시 로드되어 비어있어야 함
            Assert.Equal("총 0개", _viewModel.BookmarkCount);
        }
    }
}