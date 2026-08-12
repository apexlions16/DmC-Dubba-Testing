using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DmC.Qa.Admin;

public partial class BugDetailWindow
{
    private readonly DispatcherTimer _playerTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(250)
    };

    private TimeSpan _mediaDuration = TimeSpan.Zero;
    private bool _isUpdatingSeek;
    private bool _isSeekingWithMouse;
    private bool _isVideoPlaying;
    private double _volumeBeforeMute = 0.85;

    private void InitializePlayerControls()
    {
        _playerTimer.Tick -= PlayerTimer_Tick;
        _playerTimer.Tick += PlayerTimer_Tick;
        VideoPlayer.Volume = VolumeSlider.Value;
        UpdateMuteButton();
        UpdatePlayerTimeText(TimeSpan.Zero);
    }

    private void ShutdownPlayerControls()
    {
        _playerTimer.Stop();
    }

    private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        _mediaDuration = VideoPlayer.NaturalDuration.HasTimeSpan
            ? VideoPlayer.NaturalDuration.TimeSpan
            : TimeSpan.Zero;

        _isUpdatingSeek = true;
        SeekSlider.Maximum = Math.Max(1, _mediaDuration.TotalSeconds);
        SeekSlider.Value = Math.Clamp(VideoPlayer.Position.TotalSeconds, 0, SeekSlider.Maximum);
        _isUpdatingSeek = false;

        DurationText.Text = FormatPlayerTime(_mediaDuration);
        UpdatePlayerTimeText(VideoPlayer.Position);
        SetPlayerPlayingState(true);
        _playerTimer.Start();
    }

    private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        VideoPlayer.Pause();
        VideoPlayer.Position = TimeSpan.Zero;
        SetPlayerPlayingState(false);
        UpdateSeekFromPlayer();
        VideoStatusText.Text = "Video tamamlandı";
    }

    private void VideoPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        _playerTimer.Stop();
        SetPlayerPlayingState(false);
        VideoStatusText.Text = "Video oynatılamadı";
        MessageBox.Show(
            e.ErrorException?.Message ?? "Video oynatıcı dosyayı açamadı.",
            "Video Oynatıcı",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void PlayerTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isSeekingWithMouse)
        {
            UpdateSeekFromPlayer();
        }
    }

    private void UpdateSeekFromPlayer()
    {
        if (VideoPlayer.Source is null)
        {
            return;
        }

        var position = VideoPlayer.Position;
        _isUpdatingSeek = true;
        SeekSlider.Value = Math.Clamp(position.TotalSeconds, SeekSlider.Minimum, SeekSlider.Maximum);
        _isUpdatingSeek = false;
        UpdatePlayerTimeText(position);
    }

    private void SeekSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isSeekingWithMouse = true;
    }

    private void SeekSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (VideoPlayer.Source is null)
        {
            _isSeekingWithMouse = false;
            return;
        }

        VideoPlayer.Position = TimeSpan.FromSeconds(SeekSlider.Value);
        _isSeekingWithMouse = false;
        UpdatePlayerTimeText(VideoPlayer.Position);
    }

    private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingSeek)
        {
            return;
        }

        UpdatePlayerTimeText(TimeSpan.FromSeconds(Math.Max(0, e.NewValue)));
        if (!_isSeekingWithMouse && VideoPlayer?.Source is not null)
        {
            VideoPlayer.Position = TimeSpan.FromSeconds(e.NewValue);
        }
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (VideoPlayer.Source is null)
        {
            VideoStatusText.Text = "Önce aşağıdaki kanıt dosyalarından bir video açın";
            return;
        }

        if (_isVideoPlaying)
        {
            VideoPlayer.Pause();
            SetPlayerPlayingState(false);
            VideoStatusText.Text = "Duraklatıldı";
        }
        else
        {
            VideoPlayer.Play();
            SetPlayerPlayingState(true);
            VideoStatusText.Text = "Oynatılıyor";
        }
    }

    private void Rewind10_Click(object sender, RoutedEventArgs e)
    {
        SeekRelative(TimeSpan.FromSeconds(-10));
    }

    private void Forward10_Click(object sender, RoutedEventArgs e)
    {
        SeekRelative(TimeSpan.FromSeconds(10));
    }

    private void SeekRelative(TimeSpan delta)
    {
        if (VideoPlayer.Source is null)
        {
            return;
        }

        var upper = _mediaDuration > TimeSpan.Zero ? _mediaDuration : TimeSpan.MaxValue;
        var target = VideoPlayer.Position + delta;
        if (target < TimeSpan.Zero)
        {
            target = TimeSpan.Zero;
        }
        if (target > upper)
        {
            target = upper;
        }

        VideoPlayer.Position = target;
        UpdateSeekFromPlayer();
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (VideoPlayer is null)
        {
            return;
        }

        VideoPlayer.Volume = e.NewValue;
        if (e.NewValue > 0)
        {
            _volumeBeforeMute = e.NewValue;
        }
        UpdateMuteButton();
    }

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        if (VideoPlayer.IsMuted || VolumeSlider.Value <= 0.001)
        {
            VideoPlayer.IsMuted = false;
            VolumeSlider.Value = Math.Max(0.05, _volumeBeforeMute);
        }
        else
        {
            _volumeBeforeMute = Math.Max(0.05, VolumeSlider.Value);
            VideoPlayer.IsMuted = true;
        }
        UpdateMuteButton();
    }

    private void UpdateMuteButton()
    {
        if (MuteButton is null || VideoPlayer is null)
        {
            return;
        }

        var silent = VideoPlayer.IsMuted || VolumeSlider?.Value <= 0.001;
        MuteButton.Content = silent ? "🔇" : "🔊";
        MuteButton.ToolTip = silent ? "Sesi aç" : "Sesi kapat";
    }

    private void Fullscreen_Click(object sender, RoutedEventArgs e)
    {
        if (VideoPlayer.Source is null)
        {
            VideoStatusText.Text = "Tam ekran için önce bir video açın";
            return;
        }

        var source = VideoPlayer.Source;
        var position = VideoPlayer.Position;
        var wasPlaying = _isVideoPlaying;
        var volume = VideoPlayer.Volume;
        var muted = VideoPlayer.IsMuted;

        VideoPlayer.Pause();
        SetPlayerPlayingState(false);

        var fullscreenMedia = new MediaElement
        {
            Source = source,
            LoadedBehavior = MediaState.Manual,
            UnloadedBehavior = MediaState.Close,
            Stretch = Stretch.Uniform,
            ScrubbingEnabled = true,
            Volume = volume,
            IsMuted = muted
        };

        var exitButton = new Button
        {
            Content = "✕ Tam Ekrandan Çık",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(18),
            Padding = new Thickness(14, 8, 14, 8),
            Background = new SolidColorBrush(Color.FromArgb(210, 9, 18, 34)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(82, 123, 255)),
            BorderThickness = new Thickness(1)
        };

        var root = new Grid { Background = Brushes.Black };
        root.Children.Add(fullscreenMedia);
        root.Children.Add(exitButton);

        var fullscreen = new Window
        {
            Title = "Video Kanıtı • Tam Ekran",
            Content = root,
            Background = Brushes.Black,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            WindowState = WindowState.Maximized,
            Topmost = true,
            Owner = this
        };

        fullscreenMedia.MediaOpened += (_, _) =>
        {
            fullscreenMedia.Position = position;
            fullscreenMedia.Play();
        };
        exitButton.Click += (_, _) => fullscreen.Close();
        fullscreen.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape || args.Key == Key.F11)
            {
                fullscreen.Close();
            }
        };
        fullscreen.Closed += (_, _) =>
        {
            var returnPosition = fullscreenMedia.Position;
            fullscreenMedia.Close();
            VideoPlayer.Position = returnPosition;
            if (wasPlaying)
            {
                VideoPlayer.Play();
                SetPlayerPlayingState(true);
            }
            UpdateSeekFromPlayer();
        };

        fullscreen.ShowDialog();
    }

    private void SetPlayerPlayingState(bool playing)
    {
        _isVideoPlaying = playing;
        if (PlayPauseButton is not null)
        {
            PlayPauseButton.Content = playing ? "Ⅱ Duraklat" : "▶ Oynat";
        }
    }

    private void UpdatePlayerTimeText(TimeSpan position)
    {
        if (CurrentTimeText is not null)
        {
            CurrentTimeText.Text = FormatPlayerTime(position);
        }
    }

    private static string FormatPlayerTime(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        return value.TotalHours >= 1
            ? value.ToString(@"hh\:mm\:ss")
            : value.ToString(@"mm\:ss");
    }
}
