using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace EBookStudio.Helpers
{
    // 1. True/False 반전 (Enabled 제어용)
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool booleanValue) return !booleanValue;
            return false;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // 2. True -> Visible, False -> Collapsed (로그인 된 상태용)
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool booleanValue && booleanValue) return Visibility.Visible;
            return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // [신규] 3. True -> Collapsed, False -> Visible (로그인 안 된 상태용)
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 로그인이 되어있으면(True) -> 숨김(Collapsed)
            if (value is bool booleanValue && booleanValue) return Visibility.Collapsed;
            // 로그인이 안되어있으면(False) -> 보임(Visible)
            return Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class IsTitlePageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text && !string.IsNullOrEmpty(text))
            {
                // 텍스트가 === 로 시작하면 제목 페이지로 간주
                return text.TrimStart().StartsWith("===");
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return new SolidColorBrush(Color.FromRgb(0, 122, 255)); // #007AFF (파랑)
            return new SolidColorBrush(Color.FromRgb(221, 221, 221));   // #DDD (회색)
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // 2. 다크모드 토글 버튼 원 위치 (True -> 오른쪽, False -> 왼쪽)
    public class BoolToOffsetConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return 22.0; // 오른쪽으로 이동
            return 0.0;      // 원래 위치
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // 3. 비밀번호 입력창 2개를 하나로 묶어 보내기 (MultiBinding)
    public class PasswordMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values 배열 그대로 뷰모델에 전달
            return values.Clone();
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}