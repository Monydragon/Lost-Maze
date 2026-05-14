using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace LostMaze;

public sealed class LostMazeGame : Game
{
    private const int MazeColumns = 11;
    private const int MazeRows = 9;

    private static readonly int[,] Maze =
    {
        { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 },
        { 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1 },
        { 1, 0, 1, 0, 1, 0, 1, 1, 1, 0, 1 },
        { 1, 0, 1, 0, 0, 0, 0, 0, 1, 0, 1 },
        { 1, 0, 1, 1, 1, 1, 1, 0, 1, 0, 1 },
        { 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1 },
        { 1, 1, 1, 1, 1, 0, 1, 1, 1, 0, 1 },
        { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 },
        { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 },
    };

    private static readonly MazePalette Palette = new(
        new Color(18, 32, 45),
        new Color(41, 64, 62),
        new Color(225, 221, 190),
        new Color(46, 68, 82),
        new Color(24, 37, 50),
        new Color(232, 172, 83),
        new Color(247, 242, 216),
        new Color(171, 194, 190));

    private readonly GraphicsDeviceManager _graphics;
    private readonly PixelTextRenderer _text = new();
    private SpriteBatch _spriteBatch;
    private Texture2D _pixel;
    private SafeAreaInsets _safeAreaInsets;
    private float _pulse;

    public LostMazeGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
        };

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "Lost Maze";

#if DESKTOPGL || WINDOWSDX
        Window.AllowUserResizing = true;
#endif
    }

    public void SetSafeAreaInsets(int left, int top, int right, int bottom)
    {
        _safeAreaInsets = new SafeAreaInsets(
            Math.Max(0, left),
            Math.Max(0, top),
            Math.Max(0, right),
            Math.Max(0, bottom));
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    protected override void UnloadContent()
    {
        _pixel?.Dispose();
        _spriteBatch?.Dispose();
        base.UnloadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape) ||
            GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
        {
            Exit();
        }

        _pulse += (float)gameTime.ElapsedGameTime.TotalSeconds;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Palette.BackgroundTop);

        var viewport = GraphicsDevice.Viewport;
        var playArea = GetPlayArea(viewport);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawBackground(viewport.Bounds);
        DrawTitle(playArea);
        DrawMaze(playArea);
        DrawFooter(playArea);
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private Rectangle GetPlayArea(Viewport viewport)
    {
        var width = Math.Max(1, viewport.Width - _safeAreaInsets.Left - _safeAreaInsets.Right);
        var height = Math.Max(1, viewport.Height - _safeAreaInsets.Top - _safeAreaInsets.Bottom);
        return new Rectangle(_safeAreaInsets.Left, _safeAreaInsets.Top, width, height);
    }

    private void DrawBackground(Rectangle bounds)
    {
        var bands = Math.Max(1, bounds.Height / 8);

        for (var y = 0; y < bounds.Height; y += bands)
        {
            var amount = bounds.Height <= bands ? 0f : y / (float)(bounds.Height - bands);
            var color = Color.Lerp(Palette.BackgroundTop, Palette.BackgroundBottom, amount);
            DrawFill(new Rectangle(bounds.X, bounds.Y + y, bounds.Width, Math.Min(bands + 1, bounds.Height - y)), color);
        }

        DrawFill(new Rectangle(bounds.X, bounds.Y + bounds.Height - 86, bounds.Width, 86), new Color(14, 23, 34, 110));
    }

    private void DrawTitle(Rectangle playArea)
    {
        var titleScale = Math.Max(3, Math.Min(8, playArea.Width / 140));
        var subtitleScale = Math.Max(1, Math.Min(3, playArea.Width / 330));
        var title = "LOST MAZE";
        var subtitle = "KNI STARTER SCAFFOLD";
        var titleSize = _text.MeasureText(title, titleScale, titleScale);
        var subtitleSize = _text.MeasureText(subtitle, subtitleScale, subtitleScale);
        var titleY = playArea.Y + Math.Max(22, playArea.Height / 22);
        var subtitleY = titleY + (int)titleSize.Y + Math.Max(12, titleScale);

        _text.DrawString(
            _spriteBatch,
            _pixel,
            title,
            new Vector2(playArea.X + ((playArea.Width - titleSize.X) / 2f), titleY),
            Palette.Text,
            titleScale,
            titleScale);

        _text.DrawString(
            _spriteBatch,
            _pixel,
            subtitle,
            new Vector2(playArea.X + ((playArea.Width - subtitleSize.X) / 2f), subtitleY),
            Palette.MutedText,
            subtitleScale,
            subtitleScale);
    }

    private void DrawMaze(Rectangle playArea)
    {
        var reservedHeight = Math.Max(174, playArea.Height / 4);
        var cell = Math.Max(18, Math.Min((playArea.Width - 48) / MazeColumns, (playArea.Height - reservedHeight) / MazeRows));
        var boardWidth = cell * MazeColumns;
        var boardHeight = cell * MazeRows;
        var boardX = playArea.X + ((playArea.Width - boardWidth) / 2);
        var boardY = playArea.Y + Math.Max(112, (playArea.Height - boardHeight) / 2 + 38);

        if (boardY + boardHeight > playArea.Bottom - 72)
        {
            boardY = playArea.Bottom - boardHeight - 72;
        }

        var board = new Rectangle(boardX, boardY, boardWidth, boardHeight);
        DrawOutline(new Rectangle(board.X - 10, board.Y - 10, board.Width + 20, board.Height + 20), Palette.Accent, 5);
        DrawFill(new Rectangle(board.X - 5, board.Y - 5, board.Width + 10, board.Height + 10), new Color(18, 28, 39));

        for (var row = 0; row < MazeRows; row++)
        {
            for (var column = 0; column < MazeColumns; column++)
            {
                var cellBounds = new Rectangle(board.X + (column * cell), board.Y + (row * cell), cell, cell);

                if (Maze[row, column] == 1)
                {
                    DrawFill(cellBounds, Palette.WallEdge);
                    DrawFill(new Rectangle(cellBounds.X + 3, cellBounds.Y + 3, cellBounds.Width - 6, cellBounds.Height - 6), Palette.Wall);
                    continue;
                }

                DrawFill(cellBounds, Palette.Floor);
                DrawOutline(cellBounds, new Color(166, 157, 120), 1);
            }
        }

        DrawEndpoint(board, cell, 1, 1, "S", new Color(76, 148, 121));
        DrawEndpoint(board, cell, 9, 7, "E", Palette.Accent);
        DrawPlayer(board, cell);
    }

    private void DrawEndpoint(Rectangle board, int cell, int column, int row, string label, Color color)
    {
        var bounds = new Rectangle(board.X + (column * cell) + 5, board.Y + (row * cell) + 5, cell - 10, cell - 10);
        var scale = Math.Max(1, cell / 13);
        var labelSize = _text.MeasureText(label, scale, scale);

        DrawFill(bounds, color);
        _text.DrawString(
            _spriteBatch,
            _pixel,
            label,
            new Vector2(bounds.X + ((bounds.Width - labelSize.X) / 2f), bounds.Y + ((bounds.Height - labelSize.Y) / 2f)),
            Color.White,
            scale,
            scale);
    }

    private void DrawPlayer(Rectangle board, int cell)
    {
        var bob = (int)MathF.Round(MathF.Sin(_pulse * 3.2f) * Math.Max(2, cell * 0.05f));
        var size = Math.Max(12, cell - 18);
        var x = board.X + cell + ((cell - size) / 2);
        var y = board.Y + cell + ((cell - size) / 2) + bob;
        var bounds = new Rectangle(x, y, size, size);

        DrawFill(new Rectangle(bounds.X - 3, bounds.Y + 5, bounds.Width + 6, bounds.Height + 2), new Color(41, 45, 36, 95));
        DrawFill(bounds, new Color(93, 198, 212));
        DrawFill(new Rectangle(bounds.X + size / 4, bounds.Y + size / 4, size / 5, size / 5), new Color(15, 44, 58));
        DrawFill(new Rectangle(bounds.Right - size / 3, bounds.Y + size / 4, size / 5, size / 5), new Color(15, 44, 58));
    }

    private void DrawFooter(Rectangle playArea)
    {
        var scale = Math.Max(1, Math.Min(3, playArea.Width / 360));
        var message = "ALL PLATFORMS READY";
        var hint = "NEXT PROMPT CAN SHAPE THE GAME";
        var messageSize = _text.MeasureText(message, scale, scale);
        var hintSize = _text.MeasureText(hint, Math.Max(1, scale - 1), scale);
        var messageY = playArea.Bottom - Math.Max(54, 24 + (int)messageSize.Y + (int)hintSize.Y);

        _text.DrawString(
            _spriteBatch,
            _pixel,
            message,
            new Vector2(playArea.X + ((playArea.Width - messageSize.X) / 2f), messageY),
            Palette.Text,
            scale,
            scale);

        _text.DrawString(
            _spriteBatch,
            _pixel,
            hint,
            new Vector2(playArea.X + ((playArea.Width - hintSize.X) / 2f), messageY + messageSize.Y + 12),
            Palette.MutedText,
            Math.Max(1, scale - 1),
            scale);
    }

    private void DrawFill(Rectangle bounds, Color color)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        _spriteBatch.Draw(_pixel, bounds, color);
    }

    private void DrawOutline(Rectangle bounds, Color color, int thickness)
    {
        DrawFill(new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
        DrawFill(new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
        DrawFill(new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
        DrawFill(new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
    }
}
