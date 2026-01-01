using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EBookStudio.Models
{
    public class LocalBookData
    {
        public LocalBookInfo? book_info { get; set; }
        public List<LocalChapter>? chapters { get; set; }
    }

    public class LocalBookInfo
    {
        public string title { get; set; } = string.Empty;
        public string author { get; set; } = string.Empty;
        public int total_chapters { get; set; }
    }

    public class LocalChapter
    {
        public int chapter_index { get; set; }
        public string title { get; set; } = string.Empty;
        public List<LocalSegment>? segments { get; set; }
    }

    public class LocalSegment
    {
        public int segment_index { get; set; }
        public string emotion { get; set; } = string.Empty;
        public string music_filename { get; set; } = string.Empty;
        public string music_path { get; set; } = string.Empty;
        public List<LocalPage>? pages { get; set; }
    }

    public class LocalPage
    {
        public int page_index { get; set; }
        public string text { get; set; } = string.Empty;
        public bool is_new_segment { get; set; }
    }
}
