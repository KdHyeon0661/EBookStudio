using System.IO;
using System.Text;

namespace EBookStudio.Helpers
{
    public static class AtomicFile
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new(false);

        public static void WriteAllText(string path, string contents)
        {
            WriteAllBytes(path, Utf8WithoutBom.GetBytes(contents));
        }

        public static void WriteAllBytes(string path, byte[] contents)
        {
            string tempPath = Prepare(path);
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write,
                           FileShare.None, 81920, FileOptions.WriteThrough))
                {
                    stream.Write(contents);
                    stream.Flush(flushToDisk: true);
                }
                Commit(tempPath, path);
            }
            finally
            {
                TryDelete(tempPath);
            }
        }

        public static Task WriteAllTextAsync(string path, string contents,
                                              CancellationToken cancellationToken = default)
            => WriteAllBytesAsync(path, Utf8WithoutBom.GetBytes(contents), cancellationToken);

        public static async Task WriteAllBytesAsync(string path, byte[] contents,
                                                     CancellationToken cancellationToken = default)
        {
            await using var source = new MemoryStream(contents, writable: false);
            await WriteStreamAsync(path, source, cancellationToken);
        }

        public static async Task WriteStreamAsync(string path, Stream source,
                                                   CancellationToken cancellationToken = default)
        {
            string tempPath = Prepare(path);
            try
            {
                await using (var target = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write,
                                 FileShare.None, 81920,
                                 FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await source.CopyToAsync(target, cancellationToken);
                    await target.FlushAsync(cancellationToken);
                    target.Flush(flushToDisk: true);
                }
                Commit(tempPath, path);
            }
            finally
            {
                TryDelete(tempPath);
            }
        }

        private static string Prepare(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("A destination directory is required.", nameof(path));
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        }

        private static void Commit(string tempPath, string destinationPath)
        {
            File.Move(tempPath, Path.GetFullPath(destinationPath), overwrite: true);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}