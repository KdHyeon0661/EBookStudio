using EBookStudio.Helpers;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;

namespace EBookStudio.ViewModels
{
    public class SettingViewModel : ViewModelBase
    {
        private readonly ISettingsService _settings;
        public ISettingsService Settings => _settings;

        public ObservableCollection<FontFamily> FontList { get; } = new ObservableCollection<FontFamily>
        {
            new FontFamily("Malgun Gothic"),
            new FontFamily("KoPubBatang"),
            new FontFamily("KoPubDotum"),
            new FontFamily("Segoe UI"),
            new FontFamily("Gulim")
        };

        public ICommand SetThemeCommand { get; }

        public SettingViewModel(ISettingsService? settings = null)
        {
            // SettingsService가 ISettingsService를 상속받았으므로 이제 안전하게 캐스팅됩니다.
            _settings = settings ?? (ISettingsService)SettingsService.Instance;

            SetThemeCommand = new RelayCommand(o =>
            {
                if (o is string theme)
                {
                    if (theme == "Light") _settings.ApplyLightMode();
                    else if (theme == "Sepia") _settings.ApplySepiaMode();
                    else if (theme == "Dark") _settings.ApplyDarkMode();
                }
            });
        }
    }
}
