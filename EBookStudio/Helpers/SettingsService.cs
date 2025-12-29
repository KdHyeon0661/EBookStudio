using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace EBookStudio.Helpers
{
    public class SettingsService : INotifyPropertyChanged
    {
        // 싱글톤 인스턴스
        private static SettingsService? _instance;
        public static SettingsService Instance => _instance ??= new SettingsService();

        private SettingsService() { }

        // --- 속성 ---

        // 1. 글꼴
        private FontFamily _fontFamily = new FontFamily("Malgun Gothic");
        public FontFamily FontFamily
        {
            get => _fontFamily;
            set { _fontFamily = value; OnPropertyChanged(); }
        }

        // 2. 줄 간격
        private double _lineHeight = 40; // 기본값
        public double LineHeight
        {
            get => _lineHeight;
            set { _lineHeight = value; OnPropertyChanged(); }
        }

        // 3. 테마 색상 (배경/글자)
        private Brush _background = Brushes.White;
        public Brush Background
        {
            get => _background;
            set { _background = value; OnPropertyChanged(); }
        }

        private Brush _foreground = new SolidColorBrush(Color.FromRgb(34, 34, 34)); // #222
        public Brush Foreground
        {
            get => _foreground;
            set { _foreground = value; OnPropertyChanged(); }
        }

        // --- 테마 적용 메서드 ---

        public void ApplyLightMode()
        {
            Background = Brushes.White;
            Foreground = new SolidColorBrush(Color.FromRgb(34, 34, 34));
        }

        public void ApplySepiaMode()
        {
            Background = new SolidColorBrush(Color.FromRgb(244, 236, 216)); // 종이색
            Foreground = new SolidColorBrush(Color.FromRgb(95, 75, 50));    // 갈색
        }

        public void ApplyDarkMode()
        {
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 45));    // 짙은 회색
            Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)); // 밝은 회색
        }

        // --- INotifyPropertyChanged ---
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
