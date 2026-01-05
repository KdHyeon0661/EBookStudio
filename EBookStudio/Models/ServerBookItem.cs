using EBookStudio.Helpers;

namespace EBookStudio.Models
{
    public class ServerBookItem : ViewModelBase
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public string Title { get; set; } = string.Empty;
        public string CoverUrl { get; set; } = string.Empty; // 서버 URL (표시용)

        // 화면 표시용 (로컬에 있는지 여부 등을 체크해서 버튼 활성화를 할 수도 있음)
        public string DisplayCoverUrl => string.IsNullOrEmpty(CoverUrl)
            ? "/Images/default_cover.png" // 기본 이미지 경로
            : $"{ApiConfig.BaseUrl}{CoverUrl}";
    }
}