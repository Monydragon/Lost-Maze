using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Java.Lang;
using System.Runtime.Versioning;

namespace LostMaze;

[Activity(
    Label = "Lost Maze",
    MainLauncher = true,
    AlwaysRetainTaskState = true,
    LaunchMode = LaunchMode.SingleInstance,
    ConfigurationChanges = ConfigChanges.Orientation |
                           ConfigChanges.Keyboard |
                           ConfigChanges.KeyboardHidden |
                           ConfigChanges.ScreenSize |
                           ConfigChanges.ScreenLayout |
                           ConfigChanges.UiMode |
                           ConfigChanges.SmallestScreenSize,
    ScreenOrientation = ScreenOrientation.FullSensor)]
public class LostMazeActivity : Microsoft.Xna.Framework.AndroidGameActivity
{
    private View _gameView;
    private WindowInsetListener _windowInsetListener;

    protected override void OnCreate(Bundle bundle)
    {
        base.OnCreate(bundle);
        var game = new LostMazeGame();
        _gameView = (View)game.Services.GetService(typeof(View));
        _windowInsetListener = new WindowInsetListener(game);
        _gameView.SetOnApplyWindowInsetsListener(_windowInsetListener);
        SetContentView(_gameView);
        _gameView.RequestApplyInsets();
        game.Run();
    }

    protected override void OnDestroy()
    {
        if (_gameView is not null)
        {
            _gameView.SetOnApplyWindowInsetsListener(null);
        }

        _windowInsetListener?.Dispose();
        _windowInsetListener = null;
        _gameView = null;
        base.OnDestroy();
    }

    private sealed class WindowInsetListener : Object, View.IOnApplyWindowInsetsListener
    {
        private readonly LostMazeGame _game;

        public WindowInsetListener(LostMazeGame game)
        {
            _game = game;
        }

        public WindowInsets OnApplyWindowInsets(View v, WindowInsets insets)
        {
            if (System.OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                ApplyModernInsets(insets);
                return insets;
            }

            ApplyLegacyInsets(insets);
            return insets;
        }

        [SupportedOSPlatform("android30.0")]
        private void ApplyModernInsets(WindowInsets insets)
        {
            var typeMask =
                WindowInsets.Type.SystemBars() |
                WindowInsets.Type.DisplayCutout() |
                WindowInsets.Type.SystemGestures() |
                WindowInsets.Type.TappableElement();
            var safeInsets = insets.GetInsets(typeMask);
            _game.SetSafeAreaInsets(safeInsets.Left, safeInsets.Top, safeInsets.Right, safeInsets.Bottom);
        }

#pragma warning disable CA1422
        private void ApplyLegacyInsets(WindowInsets insets)
        {
            _game.SetSafeAreaInsets(
                insets.SystemWindowInsetLeft,
                insets.SystemWindowInsetTop,
                insets.SystemWindowInsetRight,
                insets.SystemWindowInsetBottom);
        }
#pragma warning restore CA1422
    }
}
