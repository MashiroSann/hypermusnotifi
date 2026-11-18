using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace HyperMusNotifi;

public partial class PopupWindow : Window
{
    private const double DisplaySeconds = 5.0;
    private const double RightMargin = 16;
    private const double BottomMargin = 12;
    private const double SlideInMs = 420;
    private const double SlideOutMs = 300;

    // Fullscreen borderless games can cover ordinary topmost windows,
    // so re-assert HWND_TOPMOST periodically while visible.
    private static readonly TimeSpan TopmostInterval = TimeSpan.FromMilliseconds(800);

    private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromSeconds(DisplaySeconds) };
    private readonly DispatcherTimer _topmostTimer = new() { Interval = TopmostInterval };

    private bool _closing;
    private int _generation;

    public PopupWindow()
    {
        InitializeComponent();
        _hideTimer.Tick += (_, _) => HideAnimated();
        _topmostTimer.Tick += (_, _) => ForceTopmost();
        Closed += (_, _) => { _hideTimer.Stop(); _topmostTimer.Stop(); _closing = true; };
    }

    public void ShowTrack(string title, string artist)
    {
        if (_closing) return;
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => ShowTrack(title, artist));
            return;
        }

        PrefixText.Text = "正在播放：";
        TitleText.Text = title;
        TitleText.Visibility = string.IsNullOrWhiteSpace(title) ? Visibility.Collapsed : Visibility.Visible;
        ArtistText.Text = string.IsNullOrWhiteSpace(artist) ? "" : artist;
        ArtistText.Visibility = string.IsNullOrWhiteSpace(artist) ? Visibility.Collapsed : Visibility.Visible;

        var work = SystemParameters.WorkArea;
        bool wasVisible = IsVisible;

        if (!wasVisible)
        {
            // Keep the HWND fixed; only the content layer animates (smooth, compositor friendly).
            SlideTransform.BeginAnimation(TranslateTransform.XProperty, null);
            Show();
        }

        // Re-layout so the window re-sizes to the new text before positioning.
        UpdateLayout();

        Left = work.Right - ActualWidth - RightMargin;
        Top = work.Bottom - ActualHeight - BottomMargin;

        if (!wasVisible)
        {
            // Content starts exactly outside the window's right edge (window width == content width).
            SlideTransform.X = ActualWidth;
            ForceTopmost();
            SlideIn();
        }
        else if (SlideTransform.X > 0.5)
        {
            // Mid slide-out (or otherwise off target): bring it back in.
            SlideIn();
        }
        // else: already fully on screen — just update content in place (no flicker).

        _hideTimer.Stop();
        _topmostTimer.Stop();
        _generation++;
        _topmostTimer.Start();
        _hideTimer.Start();
    }

    private void SlideIn()
    {
        var anim = new DoubleAnimation(SlideTransform.X, 0, TimeSpan.FromMilliseconds(SlideInMs))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Timeline.SetDesiredFrameRate(anim, 120);
        SlideTransform.BeginAnimation(TranslateTransform.XProperty, anim);
    }

    private void HideAnimated()
    {
        _hideTimer.Stop();
        _topmostTimer.Stop();
        int gen = _generation;
        double offscreen = ActualWidth + 4;

        var anim = new DoubleAnimation(SlideTransform.X, offscreen, TimeSpan.FromMilliseconds(SlideOutMs))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Timeline.SetDesiredFrameRate(anim, 120);
        anim.Completed += (_, _) =>
        {
            // A newer ShowTrack superseded this slide-out; don't hide.
            if (gen != _generation) return;
            SlideTransform.BeginAnimation(TranslateTransform.XProperty, null);
            SlideTransform.X = offscreen;
            Hide();
        };
        SlideTransform.BeginAnimation(TranslateTransform.XProperty, anim);
    }

    private void ForceTopmost()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        HideAnimated();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE);
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
}
