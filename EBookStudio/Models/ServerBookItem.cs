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
        public string FolderId { get; set; } = string.Empty;
        public string CoverUrl { get; set; } = string.Empty;

        public string DisplayCoverUrl => string.IsNullOrEmpty(CoverUrl)
            ? "/Images/default_cover.png"
            : $"{ApiConfig.BaseUrl}{CoverUrl}";
    }
}