using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace EBookStudio.Helpers
{
    public class RichTextBoxHelper
    {
        // XAML에서 사용할 속성: helpers:RichTextBoxHelper.Content="{Binding ...}"
        public static readonly DependencyProperty ContentProperty =
            DependencyProperty.RegisterAttached(
                "Content",
                typeof(string),
                typeof(RichTextBoxHelper),
                new FrameworkPropertyMetadata(null, OnContentChanged));

        public static string GetContent(DependencyObject obj) => (string)obj.GetValue(ContentProperty);
        public static void SetContent(DependencyObject obj, string value) => obj.SetValue(ContentProperty, value);

        // 내용이 바뀌면(페이지 이동 등) RichTextBox를 업데이트하는 함수
        private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RichTextBox richTextBox)
            {
                string newText = (string)e.NewValue;

                // 기존 문서 비우기
                richTextBox.Document.Blocks.Clear();

                if (!string.IsNullOrEmpty(newText))
                {
                    // 새 텍스트를 문단(Paragraph)으로 만들어 넣기
                    // (RichTextBox는 FlowDocument 구조를 써야 함)
                    var paragraph = new Paragraph();
                    paragraph.Inlines.Add(new Run(newText));

                    // 문서 스타일 초기화 (기본 폰트, 간격 등은 XAML 스타일을 따름)
                    richTextBox.Document.Blocks.Add(paragraph);
                }
            }
        }
    }
}