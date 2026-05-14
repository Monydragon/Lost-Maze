using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Microsoft.Xna.Framework;

namespace LostMaze.Pages;

public partial class Index : IAsyncDisposable
{
    private Game _game;
    private DotNetObjectReference<Index> _instanceReference;
    private SafeAreaInsets _pendingSafeAreaInsets;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (!firstRender)
        {
            return;
        }

        _instanceReference = DotNetObjectReference.Create(this);
        await JsRuntime.InvokeVoidAsync("initRenderJS", _instanceReference);
    }

    [JSInvokable]
    public void TickDotNet()
    {
        if (_game == null)
        {
            _game = new LostMazeGame();
            ApplySafeAreaInsets();
            _game.Run();
        }

        _game.Tick();
    }

    [JSInvokable]
    public void UpdateSafeAreaInsets(int left, int top, int right, int bottom)
    {
        _pendingSafeAreaInsets = new SafeAreaInsets(left, top, right, bottom);
        ApplySafeAreaInsets();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await JsRuntime.InvokeVoidAsync("disposeRenderJS");
        }
        catch (JSDisconnectedException)
        {
        }

        _instanceReference?.Dispose();
    }

    private void ApplySafeAreaInsets()
    {
        if (_game is not LostMazeGame lostMazeGame)
        {
            return;
        }

        lostMazeGame.SetSafeAreaInsets(
            _pendingSafeAreaInsets.Left,
            _pendingSafeAreaInsets.Top,
            _pendingSafeAreaInsets.Right,
            _pendingSafeAreaInsets.Bottom);
    }

    private readonly record struct SafeAreaInsets(int Left, int Top, int Right, int Bottom);
}
