using EBookStudio.Helpers;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;

namespace EBookStudio.ViewModels
{
    public class SettingViewModel : ViewModelBase
    {
        public SettingsService Settings => SettingsService.Instance;

        // 사용 가능한 폰트 목록
        public ObservableCollection<FontFamily> FontList { get; } = new ObservableCollection<FontFamily>
        {
            new FontFamily("Malgun Gothic"),
            new FontFamily("KoPubBatang"), // 코펍바탕
            new FontFamily("KoPubDotum"),  // 코펍돋움
            new FontFamily("Segoe UI"),
            new FontFamily("Gulim")
        };

        public ICommand SetThemeCommand { get; }

        public SettingViewModel()
        {
            SetThemeCommand = new RelayCommand(o =>
            {
                string theme = o as string;
                if (theme == "Light") Settings.ApplyLightMode();
                else if (theme == "Sepia") Settings.ApplySepiaMode();
                else if (theme == "Dark") Settings.ApplyDarkMode();
            });
        }
    }
}