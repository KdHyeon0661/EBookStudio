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
        public string Folder { get; set; } = string.Empty;
        public string CoverUrl { get; set; } = string.Empty;
        public string CoverFile { get; set; } = string.Empty;
        public string TextFile { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;

        public string DisplayCoverUrl => string.IsNullOrEmpty(CoverUrl)
            ? "/Images/default_cover.png"
            : $"{ApiConfig.BaseUrl}{CoverUrl}";
    }
}