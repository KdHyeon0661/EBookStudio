using System.Windows.Controls;
using System.Windows.Input;

namespace EBookStudio.Views
{
    public partial class LibraryView : UserControl
    {
        public LibraryView()
        {
            InitializeComponent();
        }

        // [추가] 마우스 휠을 돌리면 -> 가로(Horizontal)로 이동하게 만듦
        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer != null)
            {
                if (e.Delta > 0)
                    scrollViewer.LineLeft(); // 휠 올리면 왼쪽으로
                else
                    scrollViewer.LineRight(); // 휠 내리면 오른쪽으로

                e.Handled = true; // 기본 세로 스크롤 막기
            }
        }
    }
}