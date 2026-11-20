using EBookStudio.Models;
using System.IO;
using System.Text.Json;

namespace EBookStudio.Helpers;

internal static class BookContent
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    internal static LocalBookData Parse(string json)
    {
        var book = JsonSerializer.Deserialize<LocalBookData>(json, JsonOptions);
        var chapters = book?.chapters;
        if (chapters == null || chapters.Count == 0 || chapters.Any(chapter => chapter == null))
            throw new JsonException("책에 읽을 수 있는 장이 없습니다.");
        return book!;
    }

    internal static List<LocalChapter> ParseChapters(string json) => Parse(json).chapters!;

    internal static IEnumerable<string> MusicReferences(IEnumerable<LocalChapter> chapters)
        => chapters.SelectMany(chapter => chapter.segments ?? [])
            .Select(segment => segment?.GetMusicReference() ?? string.Empty)
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    internal static string? FindLocalMusic(string cacheKey, string folder, string reference,
                                           Func<string, bool> fileExists)
    {
        string local = FileHelper.GetLocalFilePath(cacheKey, folder, reference);
        if (fileExists(local)) return local;
        string shared = FileHelper.GetLocalFilePath(cacheKey, folder, "music", reference);
        return fileExists(shared) ? shared : null;
    }
}
