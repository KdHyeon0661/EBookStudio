using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EBookStudio.Helpers;
using EBookStudio.Models;

namespace EBookStudio.ViewModels
{
    public class MyPageViewModel : ViewModelBase
    {
        private readonly IAuthService _authService;
        private readonly IDialogService _dialogService;
        private readonly IBookFileSystem _fileSystem;

        private string _username = string.Empty;
        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        private bool _isDarkMode;
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (_isDarkMode != value)
                {
                    _isDarkMode = value;
                    OnPropertyChanged();
                    if (_isDarkMode) SettingsService.Instance.ApplyDarkMode();
                    else SettingsService.Instance.ApplyLightMode();
                }
            }
        }

        public string AppVersion => "v1.0.0 (Build 2025)";
        public event Action? RequestLogout;

        public ObservableCollection<ServerBookItem> ServerDeleteList { get; } = new ObservableCollection<ServerBookItem>();
        public ObservableCollection<ServerBookItem> ServerDownloadList { get; } = new ObservableCollection<ServerBookItem>();

        public ICommand ChangePasswordCommand { get; }
        public ICommand ResetHistoryCommand { get; }
        public ICommand ResetUserDataCommand { get; }
        public ICommand DeleteAccountCommand { get; }
        public ICommand LoadServerDataCommand { get; }
        public ICommand DeleteServerBooksCommand { get; }
        public ICommand DeleteSingleServerBookCommand { get; }
        public ICommand DownloadServerBooksCommand { get; }
        public ICommand DownloadSingleServerBookCommand { get; }

        public MyPageViewModel(string username,
                               IAuthService? authService = null,
                               IDialogService? dialogService = null,
                               IBookFileSystem? fileSystem = null)
        {
            Username = username;
            _authService = authService ?? new AuthService();
            _dialogService = dialogService ?? new DialogService();
            _fileSystem = fileSystem ?? new BookFileSystem();

            ChangePasswordCommand = new RelayCommand(ExecuteChangePassword);
            ResetHistoryCommand = new RelayCommand(ExecuteResetHistory);
            ResetUserDataCommand = new RelayCommand(ExecuteResetUserData);

            DeleteAccountCommand = new AsyncRelayCommand(async o => await ExecuteDeleteAccount());
            LoadServerDataCommand = new AsyncRelayCommand(async o => await LoadServerBooks());

            DeleteServerBooksCommand = new AsyncRelayCommand(async o =>
            {
                var selectedItems = ServerDeleteList.Where(x => x.IsSelected).ToList();
                if (selectedItems.Count > 0) await ExecuteDeleteServerBooks(selectedItems);
            });

            DeleteSingleServerBookCommand = new AsyncRelayCommand(async o =>
            {
                if (o is ServerBookItem item) await ExecuteDeleteServerBooks(new List<ServerBookItem> { item });
            });

            DownloadServerBooksCommand = new AsyncRelayCommand(async o =>
            {
                var selectedItems = ServerDownloadList.Where(x => x.IsSelected).ToList();
                if (selectedItems.Count > 0) await ExecuteDownloadServerBooks(selectedItems);
            });

            DownloadSingleServerBookCommand = new AsyncRelayCommand(async o =>
            {
                if (o is ServerBookItem item) await ExecuteDownloadServerBooks(new List<ServerBookItem> { item });
            });
        }

        private void ExecuteChangePassword(object? parameter)
        {
            var boxes = parameter as object[];
            if (boxes == null || boxes.Length < 2) return;
            var newBox = boxes[0] as PasswordBox;
            var confirmBox = boxes[1] as PasswordBox;

            if (newBox == null || confirmBox == null) return;
            if (string.IsNullOrWhiteSpace(newBox.Password)) { _dialogService.ShowMessage("새 비밀번호를 입력해주세요."); return; }
            if (newBox.Password != confirmBox.Password) { _dialogService.ShowMessage("비밀번호 확인이 일치하지 않습니다."); return; }

            _dialogService.ShowMessage("비밀번호가 변경되었습니다. (서버 연동 필요)");
            newBox.Password = ""; confirmBox.Password = "";
        }

        private void ExecuteResetHistory(object? obj)
        {
            if (_dialogService.ShowConfirm("모든 책의 읽은 기록(진도율)을 초기화하시겠습니까?", "확인"))
            {
                try
                {
                    string userDir = Path.Combine(FileHelper.UsersBasePath, Username);
                    if (_fileSystem.DirectoryExists(userDir))
                    {
                        var bookDirs = _fileSystem.GetDirectories(userDir);
                        foreach (var dir in bookDirs)
                        {
                            string progressPath = Path.Combine(dir, "progress.json");
                            if (_fileSystem.FileExists(progressPath)) _fileSystem.DeleteFile(progressPath);
                        }
                    }
                    _dialogService.ShowMessage("진도율이 초기화되었습니다.");
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage($"초기화 실패: {ex.Message}");
                }
            }
        }

        private void ExecuteResetUserData(object? obj)
        {
            if (_dialogService.ShowConfirm("보관함을 완전히 비우시겠습니까?\n내 컴퓨터의 모든 책 파일이 삭제됩니다.", "경고"))
            {
                _fileSystem.ResetUserData(Username);
                _dialogService.ShowMessage("보관함이 비워졌습니다.");
            }
        }

        private async Task ExecuteDeleteAccount()
        {
            if (_dialogService.ShowConfirm("정말로 탈퇴하시겠습니까?\n계정이 즉시 삭제됩니다.", "탈퇴"))
            {
                _fileSystem.ResetUserData(Username);
                _dialogService.ShowMessage("탈퇴되었습니다.");
                RequestLogout?.Invoke();
            }
        }

        private async Task LoadServerBooks()
        {
            var books = await _authService.GetMyServerBooksAsync(Username);
            ServerDeleteList.Clear();
            ServerDownloadList.Clear();

            foreach (var b in books)
            {
                ServerDeleteList.Add(new ServerBookItem { Title = b.title, CoverUrl = b.cover_url });
                ServerDownloadList.Add(new ServerBookItem { Title = b.title, CoverUrl = b.cover_url });
            }
        }

        private async Task ExecuteDeleteServerBooks(List<ServerBookItem> items)
        {
            if (items.Count == 0) return;
            if (!_dialogService.ShowConfirm($"{items.Count}개의 책을 서버에서 삭제하시겠습니까?\n(음악 파일은 보존됩니다)", "서버 삭제")) return;

            foreach (var item in items)
            {
                bool success = await _authService.DeleteServerBookAsync(item.Title);
                if (success)
                {
                    ServerDeleteList.Remove(item);
                    var dlItem = ServerDownloadList.FirstOrDefault(x => x.Title == item.Title);
                    if (dlItem != null) ServerDownloadList.Remove(dlItem);
                }
            }
            _dialogService.ShowMessage("삭제 처리 완료");
        }

        private async Task ExecuteDownloadServerBooks(List<ServerBookItem> items)
        {
            if (items.Count == 0) return;
            foreach (var item in items)
            {
                await ProcessDownloadBook(item.Title);
            }
            _dialogService.ShowMessage("다운로드가 완료되었습니다.");
        }

        private async Task ProcessDownloadBook(string bookTitle)
        {
            string jsonName = $"{bookTitle}_full.json";
            string serverJsonUrl = $"{ApiService.BaseUrl}/files/{Username}/{bookTitle}/{jsonName}";
            string localJsonPath = FileHelper.GetLocalFilePath(Username, bookTitle, "", jsonName);

            bool jsonOk = await _authService.DownloadFileAsync(serverJsonUrl, localJsonPath);
            if (!jsonOk) return;

            string coverName = $"{bookTitle}.png";
            string serverCoverUrl = $"{ApiService.BaseUrl}/files/{Username}/{bookTitle}/{coverName}";
            string localCoverPath = FileHelper.GetLocalFilePath(Username, bookTitle, "", coverName);
            await _authService.DownloadFileAsync(serverCoverUrl, localCoverPath);

            await DownloadMusicFromList(Username, bookTitle);
        }

        private async Task DownloadMusicFromList(string username, string bookTitle)
        {
            var musicFiles = await _authService.GetMusicFileListAsync(username, bookTitle);
            if (musicFiles == null || musicFiles.Count == 0) return;

            string tempPath = FileHelper.GetLocalFilePath(username, bookTitle, "music", "temp.wav");
            string localMusicFolder = Path.GetDirectoryName(tempPath)!;

            if (!_fileSystem.DirectoryExists(localMusicFolder))
                _fileSystem.CreateDirectory(localMusicFolder);

            foreach (var file in musicFiles)
            {
                string localPath = Path.Combine(localMusicFolder, file);
                if (!_fileSystem.FileExists(localPath))
                {
                    string serverUrl = $"{ApiService.BaseUrl}/files/{username}/{bookTitle}/music/{file}";
                    await _authService.DownloadFileAsync(serverUrl, localPath);
                }
            }
        }
    }
}