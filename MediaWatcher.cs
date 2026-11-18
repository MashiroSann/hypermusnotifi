using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace HyperMusNotifi;

public sealed class MediaWatcher : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private string _lastSignature = "";

    // Single-loop detection: poll timeline; a backward jump near the track end
    // (position -> ~0) while the track identity is unchanged means the song restarted.
    private readonly System.Threading.Timer _loopTimer;
    private double _lastPosition = -1.0;
    private bool _loopAnnouncePending;

    public MediaWatcher()
    {
        _loopTimer = new System.Threading.Timer(_ => PollLoop(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Dispose()
    {
        _loopTimer.Dispose();
        if (_manager != null)
        {
            _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
            _manager.SessionsChanged -= OnSessionsChanged;
        }
        if (_currentSession != null)
        {
            _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
        }
    }

    public void Start()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask();

                _manager.CurrentSessionChanged += OnCurrentSessionChanged;
                _manager.SessionsChanged += OnSessionsChanged;
                foreach (var s in _manager.GetSessions())
                {
                    Hook(s);
                }
            }
            catch (Exception ex)
            {
                Log.Write($"start failed: {ex}");
            }
        });
    }

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
    {
        foreach (var s in sender.GetSessions())
        {
            Hook(s);
        }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        var current = sender.GetCurrentSession();
        if (current != null)
        {
            Hook(current);
        }
    }

    private void Hook(GlobalSystemMediaTransportControlsSession session)
    {
        if (ReferenceEquals(session, _currentSession)) return;
        _currentSession = session;
        _lastPosition = -1.0;
        session.MediaPropertiesChanged += OnMediaPropertiesChanged;
        _ = TryAnnounceAsync(session);
        _loopTimer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(250));
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        _ = TryAnnounceAsync(sender);
    }

    private void PollLoop()
    {
        try
        {
            var session = _currentSession;
            if (session == null) return;

            var tl = session.GetTimelineProperties();
            double pos = tl.Position.TotalSeconds;
            double end = tl.EndTime.TotalSeconds;
            double last = _lastPosition;
            _lastPosition = pos;

            if (last < 0 || pos >= last) return;
            if (string.IsNullOrEmpty(_lastSignature)) return;

            // Restart signature: meaningful backward jump from near the end to near the start.
            bool fromEnd = last >= end - 15 && last > 10;
            bool toStart = pos <= 10;
            if (!fromEnd || !toStart) return;

            if (_loopAnnouncePending) return;
            _loopAnnouncePending = true;
            _ = AnnounceLoopRestartAsync(session);
        }
        catch
        {
            // timeline unavailable (e.g. session died); retry on next tick
        }
    }

    private async Task AnnounceLoopRestartAsync(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            var props = await session.TryGetMediaPropertiesAsync().AsTask();
            if (props == null) return;

            string title = props.Title ?? "";
            string artist = props.Artist ?? "";
            if (string.IsNullOrWhiteSpace(title)) return;

            string signature = $"{session.SourceAppUserModelId}|{title}|{artist}|{props.AlbumTitle ?? ""}|{props.TrackNumber}";
            if (signature != _lastSignature)
            {
                // Track identity changed; the normal path handles it.
                _lastSignature = signature;
                await ShowAsync(title, artist);
                return;
            }

            await ShowAsync(title, artist);
        }
        catch (Exception ex)
        {
            Log.Write($"loop announce failed: {ex}");
        }
        finally
        {
            _loopAnnouncePending = false;
        }
    }

    private async Task TryAnnounceAsync(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            var props = await session.TryGetMediaPropertiesAsync().AsTask();
            if (props == null) return;

            string title = props.Title ?? "";
            string artist = props.Artist ?? "";
            if (string.IsNullOrWhiteSpace(title)) return;

            string signature = $"{session.SourceAppUserModelId}|{title}|{artist}|{props.AlbumTitle ?? ""}|{props.TrackNumber}";
            if (signature == _lastSignature) return;
            _lastSignature = signature;
            _lastPosition = -1.0;

            await ShowAsync(title, artist);
        }
        catch (Exception ex)
        {
            Log.Write($"announce failed: {ex}");
        }
    }

    private static async Task ShowAsync(string title, string artist)
    {
        string artistLine = string.IsNullOrWhiteSpace(artist) ? "" : artist;
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            PopupHost.Show(title, artistLine);
        });
    }
}

public static class PopupHost
{
    private static PopupWindow? _window;

    public static void Show(string title, string artist)
    {
        if (_window == null)
        {
            _window = new PopupWindow();
        }
        _window.ShowTrack(title, artist);
    }
}
