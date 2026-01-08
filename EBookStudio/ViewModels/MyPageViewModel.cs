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
    // [추가] 화면 목록용 아이템 클래스에 Folder 속성 추가
    public class ServerBookItem : ViewModelBase
    {
        public string Title { get; set; } = string.Empty;
        public string Folder { get; set; } = string.Empty; // [중요] 실제 UUID 폴더명
        public string CoverUrl { get; set; } = string.Empty;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
    }

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
                // [수정] Folder(UUID) 정보도 같이 저장해야 함
                var item = new ServerBookItem
                {
                    Title = b.title,
                    Folder = b.folder, // 서버에서 받은 진짜 폴더명 (예: test_pdf_81c13072)
                    CoverUrl = b.cover_url
                };

                ServerDeleteList.Add(item);

                // 다운로드 리스트에는 객체를 새로 만들어야 UI 상태(IsSelected 등)가 꼬이지 않음
                ServerDownloadList.Add(new ServerBookItem
                {
                    Title = b.title,
                    Folder = b.folder,
                    CoverUrl = b.cover_url
                });
            }
        }

        private async Task ExecuteDeleteServerBooks(List<ServerBookItem> items)
        {
            if (items.Count == 0) return;
            if (!_dialogService.ShowConfirm($"{items.Count}개의 책을 서버에서 삭제하시겠습니까?\n(음악 파일은 보존됩니다)", "서버 삭제")) return;

            foreach (var item in items)
            {
                // [중요 수정] item.Title 대신 item.Folder(UUID)를 보내야 삭제됨
                bool success = await _authService.DeleteServerBookAsync(item.Folder);
                if (success)
                {
                    ServerDeleteList.Remove(item);
                    var dlItem = ServerDownloadList.FirstOrDefault(x => x.Folder == item.Folder); // Folder로 비교
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
                // [중요 수정] Title 대신 Folder(UUID) 전달
                await ProcessDownloadBook(item.Folder, item.Title);
            }
            _dialogService.ShowMessage("다운로드가 완료되었습니다.");
        }

        private async Task ProcessDownloadBook(string bookFolder, string displayTitle)
        {
            // [중요] 파일명도 이제 UUID 기반입니다. (예: test_pdf_81c13072_full.json)
            string jsonName = $"{bookFolder}_full.json";
            string serverJsonUrl = $"{ApiConfig.BaseUrl}/files/{Username}/{bookFolder}/{jsonName}";

            // 로컬에 저장할 때도 UUID 폴더 안에 UUID 파일명으로 저장
            string localJsonPath = FileHelper.GetLocalFilePath(Username, bookFolder, "", jsonName);

            bool jsonOk = await _authService.DownloadFileAsync(serverJsonUrl, localJsonPath);
            if (!jsonOk) return;

            // 커버 이미지 다운로드 (파일명 = UUID.png)
            string coverName = $"{bookFolder}.png";
            string localCoverPath = FileHelper.GetLocalFilePath(Username, bookFolder, "", coverName);
            string serverCoverUrl = $"{ApiConfig.BaseUrl}/files/{Username}/{bookFolder}/{coverName}";
            await _authService.DownloadFileAsync(serverCoverUrl, localCoverPath);

            await DownloadMusicFromList(Username, bookFolder);
        }

        private async Task DownloadMusicFromList(string username, string bookFolder)
        {
            var musicFiles = await _authService.GetMusicFileListAsync(username, bookFolder);
            if (musicFiles == null || musicFiles.Count == 0) return;

            string tempPath = FileHelper.GetLocalFilePath(username, bookFolder, "music", "temp.wav");
            string localMusicFolder = Path.GetDirectoryName(tempPath)!;

            if (!_fileSystem.DirectoryExists(localMusicFolder))
                _fileSystem.CreateDirectory(localMusicFolder);

            foreach (var file in musicFiles)
            {
                string localPath = Path.Combine(localMusicFolder, file);
                if (!_fileSystem.FileExists(localPath))
                {
                    string serverUrl = $"{ApiConfig.BaseUrl}/files/{username}/{bookFolder}/music/{file}";
                    await _authService.DownloadFileAsync(serverUrl, localPath);
                }
            }
        }
    }
}