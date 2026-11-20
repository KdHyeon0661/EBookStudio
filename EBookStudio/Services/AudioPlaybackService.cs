using System.Windows.Media;
using System.Windows.Threading;

namespace EBookStudio.Services
{
    public interface IAudioPlaybackService : IDisposable
    {
        event Action<double, double>? ProgressChanged;
        event Action? PlaybackFailed;

        void Open(string absolutePath);
        void Play();
        void Pause();
        void Stop();
        void Close();
        void Seek(double seconds);
    }

    public sealed class AudioPlaybackService : IAudioPlaybackService
    {
        private readonly MediaPlayer _player = new();
        private readonly DispatcherTimer _progressTimer = new()
        {
            Interval = TimeSpan.FromSeconds(0.5)
        };
        private bool _disposed;
        private bool _playing;

        public event Action<double, double>? ProgressChanged;
        public event Action? PlaybackFailed;

        public AudioPlaybackService()
        {
            _player.MediaEnded += (_, _) =>
            {
                if (_disposed || !_playing) return;
                _player.Position = TimeSpan.Zero;
                _player.Play();
            };
            _player.MediaFailed += (_, _) =>
            {
                if (_disposed) return;
                _playing = false;
                _progressTimer.Stop();
                PlaybackFailed?.Invoke();
            };
            _progressTimer.Tick += (_, _) => PublishProgress();
        }

        public void Open(string absolutePath)
        {
            ThrowIfDisposed();
            _playing = false;
            _progressTimer.Stop();
            _player.Open(new Uri(absolutePath, UriKind.Absolute));
        }

        public void Play()
        {
            ThrowIfDisposed();
            _player.Play();
            _playing = true;
            _progressTimer.Start();
        }

        public void Pause()
        {
            if (_disposed) return;
            _playing = false;
            _player.Pause();
            _progressTimer.Stop();
        }

        public void Stop()
        {
            if (_disposed) return;
            _playing = false;
            _player.Stop();
            _progressTimer.Stop();
        }

        public void Close()
        {
            if (_disposed) return;
            _playing = false;
            _player.Close();
            _progressTimer.Stop();
        }

        public void Seek(double seconds)
        {
            if (_disposed || !double.IsFinite(seconds) || _player.Source == null) return;
            _player.Position = TimeSpan.FromSeconds(Math.Max(0, seconds));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _progressTimer.Stop();
            _player.Stop();
            _player.Close();
            GC.SuppressFinalize(this);
        }

        private void PublishProgress()
        {
            if (_disposed || _player.Source == null || !_player.NaturalDuration.HasTimeSpan) return;
            ProgressChanged?.Invoke(
                _player.Position.TotalSeconds,
                _player.NaturalDuration.TimeSpan.TotalSeconds);
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }
}
