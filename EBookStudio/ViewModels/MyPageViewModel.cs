using System;
using System.Collections.Generic; // 추가
using System.Collections.ObjectModel; // 추가
using System.IO;
using System.Linq; // 추가
using System.Threading.Tasks; // 추가
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EBookStudio.Helpers;
using EBookStudio.Models;

namespace EBookStudio.ViewModels
{
    public class MyPageViewModel : ViewModelBase
    {
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

        // [추가] 아코디언 데이터 리스트
        public ObservableCollection<ServerBookItem> ServerDeleteList { get; } = new ObservableCollection<ServerBookItem>();
        public ObservableCollection<ServerBookItem> ServerDownloadList { get; } = new ObservableCollection<ServerBookItem>();

        // Commands
        public ICommand ChangePasswordCommand { get; }
        public ICommand ResetHistoryCommand { get; }
        public ICommand ResetUserDataCommand { get; } // [변경] DeleteAllBooksCommand -> XAML과 이름 통일
        public ICommand DeleteAccountCommand { get; }

        // [추가] 서버 관련 Commands
        public ICommand LoadServerDataCommand { get; }
        public ICommand DeleteServerBooksCommand { get; }
        public ICommand DeleteSingleServerBookCommand { get; }
        public ICommand DownloadServerBooksCommand { get; }
        public ICommand DownloadSingleServerBookCommand { get; }

        public MyPageViewModel(string username)
        {
            Username = username;

            ChangePasswordCommand = new RelayCommand(ExecuteChangePassword);
            ResetHistoryCommand = new RelayCommand(ExecuteResetHistory);
            ResetUserDataCommand = new RelayCommand(ExecuteResetUserData); // 이름 변경 연결
            DeleteAccountCommand = new RelayCommand(ExecuteDeleteAccount);

            // [추가] 서버 커맨드 초기화
            LoadServerDataCommand = new RelayCommand(async o => await LoadServerBooks());

            DeleteServerBooksCommand = new RelayCommand(async o => await ExecuteDeleteServerBooks(ServerDeleteList.Where(x => x.IsSelected).ToList()));
            DeleteSingleServerBookCommand = new RelayCommand(async o => await ExecuteDeleteServerBooks(new List<ServerBookItem> { (ServerBookItem)o }));

            DownloadServerBooksCommand = new RelayCommand(async o => await ExecuteDownloadServerBooks(ServerDownloadList.Where(x => x.IsSelected).ToList()));
            DownloadSingleServerBookCommand = new RelayCommand(async o => await ExecuteDownloadServerBooks(new List<ServerBookItem> { (ServerBookItem)o }));

            Task.Run(() => LoadServerBooks());
        }

        private void ExecuteChangePassword(object? parameter)
        {
            var boxes = parameter as object[];
            if (boxes == null || boxes.Length < 2) return;
            var newBox = boxes[0] as PasswordBox;
            var confirmBox = boxes[1] as PasswordBox;

            if (newBox == null || confirmBox == null) return;
            if (string.IsNullOrWhiteSpace(newBox.Password)) { MessageBox.Show("새 비밀번호를 입력해주세요."); return; }
            if (newBox.Password != confirmBox.Password) { MessageBox.Show("비밀번호 확인이 일치하지 않습니다."); return; }

            MessageBox.Show("비밀번호가 변경되었습니다. (서버 연동 필요)");
            newBox.Password = ""; confirmBox.Password = "";
        }

        private void ExecuteResetHistory(object? obj)
        {
            if (MessageBox.Show("모든 책의 읽은 기록(진도율)을 초기화하시겠습니까?", "확인", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    string userDir = Path.Combine(FileHelper.UsersBasePath, Username);
                    if (Directory.Exists(userDir))
                    {
                        var bookDirs = Directory.GetDirectories(userDir);
                        foreach (var dir in bookDirs)
                        {
                            string progressPath = Path.Combine(dir, "progress.json");
                            if (File.Exists(progressPath)) File.Delete(progressPath);
                        }
                    }
                    MessageBox.Show("진도율이 초기화되었습니다.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"초기화 실패: {ex.Message}");
                }
            }
        }

        // [이름 변경] ExecuteDeleteAllBooks -> ExecuteResetUserData (XAML과 통일)
        private void ExecuteResetUserData(object? obj)
        {
            if (MessageBox.Show("보관함을 완전히 비우시겠습니까?\n내 컴퓨터의 모든 책 파일이 삭제됩니다.", "경고", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                // FileHelper 사용 (기존 로직 유지)
                FileHelper.ResetUserData(Username);
                MessageBox.Show("보관함이 비워졌습니다.");
            }
        }

        private void ExecuteDeleteAccount(object? obj)
        {
            if (MessageBox.Show("정말로 탈퇴하시겠습니까?\n계정이 즉시 삭제됩니다.", "탈퇴", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
            {
                // 로컬 데이터 삭제
                FileHelper.ResetUserData(Username);

                // 서버 API 호출 등은 추후 구현
                MessageBox.Show("탈퇴되었습니다.");
                RequestLogout?.Invoke();
            }
        }

        // ==========================================
        // [추가] 서버 데이터 관리 로직
        // ==========================================
        private async Task LoadServerBooks()
        {
            var books = await ApiService.GetMyServerBooksAsync(Username);

            ServerDeleteList.Clear();
            ServerDownloadList.Clear();

            foreach (var b in books)
            {
                // 삭제용과 다운로드용 리스트에 각각 추가
                ServerDeleteList.Add(new ServerBookItem { Title = b.title, CoverUrl = b.cover_url });
                ServerDownloadList.Add(new ServerBookItem { Title = b.title, CoverUrl = b.cover_url });
            }
        }

        private async Task ExecuteDeleteServerBooks(List<ServerBookItem> items)
        {
            if (items.Count == 0) return;
            if (MessageBox.Show($"{items.Count}개의 책을 서버에서 삭제하시겠습니까?\n(음악 파일은 보존됩니다)", "서버 삭제", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No) return;

            foreach (var item in items)
            {
                bool success = await ApiService.DeleteServerBookAsync(item.Title);
                if (success)
                {
                    ServerDeleteList.Remove(item);
                    var dlItem = ServerDownloadList.FirstOrDefault(x => x.Title == item.Title);
                    if (dlItem != null) ServerDownloadList.Remove(dlItem);
                }
            }
            MessageBox.Show("삭제 처리 완료");
        }

        private async Task ExecuteDownloadServerBooks(List<ServerBookItem> items)
        {
            if (items.Count == 0) return;

            foreach (var item in items)
            {
                await ProcessDownloadBook(item.Title);
            }
            MessageBox.Show("다운로드가 완료되었습니다.");
        }

        private async Task ProcessDownloadBook(string bookTitle)
        {
            string username = Username;

            // 1. JSON 다운로드
            string jsonName = $"{bookTitle}_full.json";
            string serverJsonUrl = $"{ApiService.BaseUrl}/files/{username}/{bookTitle}/{jsonName}";
            string localJsonPath = FileHelper.GetLocalFilePath(username, bookTitle, "", jsonName);

            bool jsonOk = await ApiService.DownloadFileAsync(serverJsonUrl, localJsonPath);
            if (!jsonOk) return;

            // 2. 표지 다운로드
            string coverName = $"{bookTitle}.png";
            string serverCoverUrl = $"{ApiService.BaseUrl}/files/{username}/{bookTitle}/{coverName}";
            string localCoverPath = FileHelper.GetLocalFilePath(username, bookTitle, "", coverName);
            await ApiService.DownloadFileAsync(serverCoverUrl, localCoverPath);

            // 3. 음악 파일 다운로드
            await DownloadMusicFromList(username, bookTitle);
        }

        private async Task DownloadMusicFromList(string username, string bookTitle)
        {
            var musicFiles = await ApiService.GetMusicFileListAsync(username, bookTitle);
            if (musicFiles == null || musicFiles.Count == 0) return;

            string tempPath = FileHelper.GetLocalFilePath(username, bookTitle, "music", "temp.wav");
            string localMusicFolder = Path.GetDirectoryName(tempPath)!;
            if (!Directory.Exists(localMusicFolder)) Directory.CreateDirectory(localMusicFolder);

            foreach (var file in musicFiles)
            {
                string localPath = Path.Combine(localMusicFolder, file);
                if (!File.Exists(localPath))
                {
                    string serverUrl = $"{ApiService.BaseUrl}/files/{username}/{bookTitle}/music/{file}";
                    await ApiService.DownloadFileAsync(serverUrl, localPath);
                }
            }
        }
    }
}