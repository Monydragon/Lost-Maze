using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace LostMaze;

public sealed class LostMazeGame : Game
{
    private const double ManualRepeatDelay = 0.22d;
    private const double ManualRepeatInterval = 0.11d;
    private const double NavigationStepInterval = 0.10d;
    private const int ScareSoundSampleRate = 22050;
    private const float ThumbstickDeadZone = 0.35f;

    private static readonly MazePalette Palette = new(
        new Color(18, 32, 45),
        new Color(41, 64, 62),
        new Color(225, 221, 190),
        new Color(46, 68, 82),
        new Color(24, 37, 50),
        new Color(232, 172, 83),
        new Color(247, 242, 216),
        new Color(171, 194, 190));

    private static readonly Point[] PathDirections =
    [
        new(0, -1),
        new(1, 0),
        new(0, 1),
        new(-1, 0),
    ];

    private readonly int _runSeed;
    private readonly int _scareLevel;
    private readonly ScareSettings _scareSettings;
    private readonly Queue<Point> _navigationPath = new();
    private readonly GraphicsDeviceManager _graphics;
    private readonly PixelTextRenderer _text = new();
    private SpriteBatch _spriteBatch;
    private Texture2D _pixel;
    private SoundEffect _scareSound;
    private MazeLevel _level;
    private Point _playerCell;
    private MouseState _previousMouse;
    private SafeAreaInsets _safeAreaInsets;
    private bool _scareActive;
    private bool _scareTriggered;
    private bool _scareSoundPlayed;
    private Direction _heldDirection;
    private bool _hasHeldDirection;
    private double _holdElapsed;
    private double _repeatElapsed;
    private double _navigationElapsed;
    private double _scareElapsed;
    private int _pendingLevelAfterScare;
    private float _pulse;

    public LostMazeGame() : this(CreateRunSeed(), CreateScareSettings())
    {
    }

    public LostMazeGame(int runSeed) : this(runSeed, CreateScareSettings())
    {
    }

    public LostMazeGame(int runSeed, ScareSettings scareSettings)
    {
        _runSeed = NormalizeSeed(runSeed);
        _scareSettings = scareSettings.Normalize();
        _scareLevel = PickScareLevel(_runSeed, _scareSettings);
        LoadLevel(1);

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

        if (_scareSettings.SoundEnabled)
        {
            _scareSound = new SoundEffect(CreateScareSoundBuffer(), ScareSoundSampleRate, AudioChannels.Mono);
        }
    }

    protected override void UnloadContent()
    {
        _scareSound?.Dispose();
        _pixel?.Dispose();
        _spriteBatch?.Dispose();
        base.UnloadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        var elapsed = gameTime.ElapsedGameTime.TotalSeconds;
        var keyboard = Keyboard.GetState();
        var gamePad = GamePad.GetState(PlayerIndex.One);

        if (keyboard.IsKeyDown(Keys.Escape) ||
            gamePad.Buttons.Back == ButtonState.Pressed)
        {
            Exit();
        }

        if (_scareActive)
        {
            UpdateScare(elapsed);
        }
        else
        {
            var layout = GetMazeLayout(GetPlayArea(GraphicsDevice.Viewport));
            UpdateMovement(elapsed, keyboard, gamePad, layout);
        }

        _pulse += (float)elapsed;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Palette.BackgroundTop);

        var viewport = GraphicsDevice.Viewport;
        var playArea = GetPlayArea(viewport);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawBackground(viewport.Bounds);

        if (_scareActive)
        {
            DrawScareScreen(viewport.Bounds, playArea);
        }
        else
        {
            DrawTitle(playArea);
            DrawMaze(playArea);
            DrawFooter(playArea);
        }

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
        var subtitle = "ENDLESS SEED RUN";
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
        var layout = GetMazeLayout(playArea);
        var board = layout.Board;
        var cell = layout.CellSize;
        var frameThickness = Math.Max(2, Math.Min(5, cell / 8));
        DrawOutline(new Rectangle(board.X - (frameThickness * 2), board.Y - (frameThickness * 2), board.Width + (frameThickness * 4), board.Height + (frameThickness * 4)), Palette.Accent, frameThickness);
        DrawFill(new Rectangle(board.X - frameThickness, board.Y - frameThickness, board.Width + (frameThickness * 2), board.Height + (frameThickness * 2)), new Color(18, 28, 39));

        for (var row = 0; row < _level.Rows; row++)
        {
            for (var column = 0; column < _level.Columns; column++)
            {
                var cellBounds = new Rectangle(board.X + (column * cell), board.Y + (row * cell), cell, cell);

                if (_level.IsWall(new Point(column, row)))
                {
                    var inset = Math.Max(1, cell / 10);
                    DrawFill(cellBounds, Palette.WallEdge);
                    DrawFill(new Rectangle(cellBounds.X + inset, cellBounds.Y + inset, cellBounds.Width - (inset * 2), cellBounds.Height - (inset * 2)), Palette.Wall);
                    continue;
                }

                DrawFill(cellBounds, Palette.Floor);
                DrawOutline(cellBounds, new Color(166, 157, 120), 1);
            }
        }

        DrawEndpoint(board, cell, _level.Start.X, _level.Start.Y, "S", new Color(76, 148, 121));
        DrawEndpoint(board, cell, _level.Exit.X, _level.Exit.Y, "E", Palette.Accent);
        DrawFogOfWar(board, cell);
        DrawPlayer(board, cell);
    }

    private void DrawEndpoint(Rectangle board, int cell, int column, int row, string label, Color color)
    {
        var padding = Math.Max(2, cell / 7);
        var bounds = new Rectangle(board.X + (column * cell) + padding, board.Y + (row * cell) + padding, cell - (padding * 2), cell - (padding * 2));
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
        var padding = Math.Max(2, cell / 5);
        var size = Math.Max(6, cell - (padding * 2));
        var x = board.X + (_playerCell.X * cell) + ((cell - size) / 2);
        var y = board.Y + (_playerCell.Y * cell) + ((cell - size) / 2) + bob;
        var bounds = new Rectangle(x, y, size, size);

        DrawFill(new Rectangle(bounds.X - 3, bounds.Y + 5, bounds.Width + 6, bounds.Height + 2), new Color(41, 45, 36, 95));
        DrawFill(bounds, new Color(93, 198, 212));
        DrawFill(new Rectangle(bounds.X + size / 4, bounds.Y + size / 4, size / 5, size / 5), new Color(15, 44, 58));
        DrawFill(new Rectangle(bounds.Right - size / 3, bounds.Y + size / 4, size / 5, size / 5), new Color(15, 44, 58));
    }

    private void DrawFogOfWar(Rectangle board, int cell)
    {
        for (var row = 0; row < _level.Rows; row++)
        {
            for (var column = 0; column < _level.Columns; column++)
            {
                var distance = Math.Abs(column - _playerCell.X) + Math.Abs(row - _playerCell.Y);

                if (distance <= _level.RevealRadius)
                {
                    continue;
                }

                var overflow = distance - _level.RevealRadius;
                var alpha = Math.Min(225, 80 + (overflow * 26) + (_level.Difficulty * 5));
                var cellBounds = new Rectangle(board.X + (column * cell), board.Y + (row * cell), cell, cell);
                DrawFill(cellBounds, new Color(4, 7, 12, alpha));
            }
        }
    }

    private void DrawFooter(Rectangle playArea)
    {
        var scale = Math.Max(1, Math.Min(3, playArea.Width / 360));
        var message = $"LEVEL {_level.Number} DANGER {_level.Difficulty}";
        var hint = _scareSettings.Enabled && !_scareTriggered
            ? $"SEED {_runSeed} FEAR {_level.Number}/{_scareLevel}"
            : $"SEED {_runSeed} KEEP GOING";
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

    private void DrawScareScreen(Rectangle bounds, Rectangle playArea)
    {
        var background = new Color(48, 0, 0);
        var face = new Color(216, 203, 176);
        var shadow = new Color(9, 0, 0);
        var eyeAndMouth = new Color(12, 0, 0);

        DrawFill(bounds, background);

        var size = Math.Min(bounds.Width, bounds.Height);
        var faceWidth = Math.Max(160, (int)(size * 0.48f));
        var faceHeight = Math.Max(180, (int)(size * 0.58f));
        var faceBounds = new Rectangle(
            bounds.X + ((bounds.Width - faceWidth) / 2),
            bounds.Y + Math.Max(24, (bounds.Height - faceHeight) / 2),
            faceWidth,
            faceHeight);

        DrawFill(new Rectangle(faceBounds.X + 12, faceBounds.Y + 18, faceBounds.Width, faceBounds.Height), shadow);
        DrawFill(faceBounds, face);

        var eyeWidth = Math.Max(24, faceBounds.Width / 5);
        var eyeHeight = Math.Max(34, faceBounds.Height / 5);
        var eyeY = faceBounds.Y + (faceBounds.Height / 4);
        DrawFill(new Rectangle(faceBounds.X + (faceBounds.Width / 5), eyeY, eyeWidth, eyeHeight), eyeAndMouth);
        DrawFill(new Rectangle(faceBounds.Right - (faceBounds.Width / 5) - eyeWidth, eyeY, eyeWidth, eyeHeight), eyeAndMouth);

        var mouthWidth = faceBounds.Width / 2;
        var mouthHeight = faceBounds.Height / 4;
        var mouthX = faceBounds.X + ((faceBounds.Width - mouthWidth) / 2);
        var mouthY = faceBounds.Y + (faceBounds.Height * 3 / 5);
        DrawFill(new Rectangle(mouthX, mouthY, mouthWidth, mouthHeight), eyeAndMouth);

        var toothWidth = Math.Max(6, mouthWidth / 9);
        for (var i = 0; i < 5; i++)
        {
            var x = mouthX + (i * toothWidth * 2);
            DrawFill(new Rectangle(x, mouthY, toothWidth, mouthHeight / 2), face);
            DrawFill(new Rectangle(x + toothWidth, mouthY + (mouthHeight / 2), toothWidth, mouthHeight / 2), face);
        }

        DrawCenteredText("RUN", playArea, playArea.Y + Math.Max(20, playArea.Height / 14), Math.Max(4, Math.Min(12, playArea.Width / 95)), face);
        DrawCenteredText("THE MAZE FOUND YOU", playArea, playArea.Bottom - Math.Max(74, playArea.Height / 9), Math.Max(1, Math.Min(3, playArea.Width / 300)), face);
    }

    private void DrawCenteredText(string text, Rectangle area, int y, int scale, Color color)
    {
        var textSize = _text.MeasureText(text, scale, scale);
        _text.DrawString(
            _spriteBatch,
            _pixel,
            text,
            new Vector2(area.X + ((area.Width - textSize.X) / 2f), y),
            color,
            scale,
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

    private MazeLayout GetMazeLayout(Rectangle playArea)
    {
        var reservedHeight = Math.Max(174, playArea.Height / 4);
        var availableWidth = Math.Max(1, playArea.Width - 48);
        var availableHeight = Math.Max(1, playArea.Height - reservedHeight);
        var cell = Math.Max(8, Math.Min(64, Math.Min(availableWidth / _level.Columns, availableHeight / _level.Rows)));
        var boardWidth = cell * _level.Columns;
        var boardHeight = cell * _level.Rows;
        var boardX = playArea.X + ((playArea.Width - boardWidth) / 2);
        var boardY = playArea.Y + Math.Max(112, ((playArea.Height - boardHeight) / 2) + 38);

        if (boardY + boardHeight > playArea.Bottom - 72)
        {
            boardY = playArea.Bottom - boardHeight - 72;
        }

        return new MazeLayout(new Rectangle(boardX, boardY, boardWidth, boardHeight), cell);
    }

    private void UpdateMovement(double elapsed, KeyboardState keyboard, GamePadState gamePad, MazeLayout layout)
    {
        var mouse = Mouse.GetState();
        HandleMouseNavigation(mouse, layout);
        HandleTouchNavigation(layout);

        var manualDirection = GetManualDirection(keyboard, gamePad);

        if (manualDirection.HasValue)
        {
            _navigationPath.Clear();
            UpdateManualMovement(manualDirection.Value, elapsed);
        }
        else
        {
            ResetManualMovement();
            UpdateQueuedNavigation(elapsed);
        }

        _previousMouse = mouse;
    }

    private void HandleMouseNavigation(MouseState mouse, MazeLayout layout)
    {
        if (mouse.LeftButton != ButtonState.Pressed ||
            _previousMouse.LeftButton == ButtonState.Pressed)
        {
            return;
        }

        TrySetNavigationTarget(new Vector2(mouse.X, mouse.Y), layout);
    }

    private void HandleTouchNavigation(MazeLayout layout)
    {
        var touches = TouchPanel.GetState();

        foreach (var touch in touches)
        {
            if (touch.State != TouchLocationState.Pressed)
            {
                continue;
            }

            TrySetNavigationTarget(touch.Position, layout);
            break;
        }
    }

    private Direction? GetManualDirection(KeyboardState keyboard, GamePadState gamePad)
    {
        var x = 0;
        var y = 0;

        if (keyboard.IsKeyDown(Keys.A) ||
            keyboard.IsKeyDown(Keys.Left) ||
            gamePad.DPad.Left == ButtonState.Pressed)
        {
            x--;
        }

        if (keyboard.IsKeyDown(Keys.D) ||
            keyboard.IsKeyDown(Keys.Right) ||
            gamePad.DPad.Right == ButtonState.Pressed)
        {
            x++;
        }

        if (keyboard.IsKeyDown(Keys.W) ||
            keyboard.IsKeyDown(Keys.Up) ||
            gamePad.DPad.Up == ButtonState.Pressed)
        {
            y--;
        }

        if (keyboard.IsKeyDown(Keys.S) ||
            keyboard.IsKeyDown(Keys.Down) ||
            gamePad.DPad.Down == ButtonState.Pressed)
        {
            y++;
        }

        if (x != 0 || y != 0)
        {
            return GetDirectionFromVector(x, y);
        }

        var thumbstick = gamePad.ThumbSticks.Left;
        var thumbX = MathF.Abs(thumbstick.X);
        var thumbY = MathF.Abs(thumbstick.Y);

        if (thumbX < ThumbstickDeadZone &&
            thumbY < ThumbstickDeadZone)
        {
            return null;
        }

        if (thumbX > thumbY)
        {
            return thumbstick.X < 0 ? Direction.Left : Direction.Right;
        }

        return thumbstick.Y > 0 ? Direction.Up : Direction.Down;
    }

    private static Direction GetDirectionFromVector(int x, int y)
    {
        if (y < 0)
        {
            return Direction.Up;
        }

        if (y > 0)
        {
            return Direction.Down;
        }

        return x < 0 ? Direction.Left : Direction.Right;
    }

    private void UpdateManualMovement(Direction direction, double elapsed)
    {
        if (!_hasHeldDirection ||
            _heldDirection != direction)
        {
            _hasHeldDirection = true;
            _heldDirection = direction;
            _holdElapsed = 0d;
            _repeatElapsed = 0d;
            TryMovePlayer(direction);
            return;
        }

        _holdElapsed += elapsed;

        if (_holdElapsed < ManualRepeatDelay)
        {
            return;
        }

        _repeatElapsed += elapsed;

        if (_repeatElapsed < ManualRepeatInterval)
        {
            return;
        }

        _repeatElapsed = 0d;
        TryMovePlayer(direction);
    }

    private void ResetManualMovement()
    {
        _hasHeldDirection = false;
        _holdElapsed = 0d;
        _repeatElapsed = 0d;
    }

    private void UpdateQueuedNavigation(double elapsed)
    {
        if (_navigationPath.Count == 0)
        {
            _navigationElapsed = 0d;
            return;
        }

        _navigationElapsed += elapsed;

        if (_navigationElapsed < NavigationStepInterval)
        {
            return;
        }

        _navigationElapsed = 0d;
        var next = _navigationPath.Dequeue();

        if (!_level.IsOpen(next) ||
            !IsAdjacent(_playerCell, next))
        {
            _navigationPath.Clear();
            return;
        }

        MovePlayerTo(next);
    }

    private bool TryMovePlayer(Direction direction)
    {
        var offset = GetOffset(direction);
        var next = new Point(_playerCell.X + offset.X, _playerCell.Y + offset.Y);

        if (!_level.IsOpen(next))
        {
            return false;
        }

        MovePlayerTo(next);
        return true;
    }

    private void MovePlayerTo(Point cell)
    {
        _playerCell = cell;

        if (IsSameCell(_playerCell, _level.Exit))
        {
            var nextLevel = _level.Number + 1;

            if (ShouldTriggerScare(_level.Number))
            {
                TriggerScare(nextLevel);
                return;
            }

            LoadLevel(nextLevel);
        }
    }

    private bool ShouldTriggerScare(int completedLevel)
    {
        return _scareSettings.Enabled &&
               !_scareTriggered &&
               completedLevel >= _scareLevel;
    }

    private void TriggerScare(int nextLevel)
    {
        _scareActive = true;
        _scareTriggered = true;
        _scareSoundPlayed = false;
        _scareElapsed = 0d;
        _pendingLevelAfterScare = nextLevel;
        _navigationPath.Clear();
        ResetManualMovement();
    }

    private void UpdateScare(double elapsed)
    {
        _scareElapsed += elapsed;

        if (!_scareSoundPlayed)
        {
            PlayScareSound();
            _scareSoundPlayed = true;
        }

        if (_scareElapsed < _scareSettings.DurationSeconds)
        {
            return;
        }

        _scareActive = false;
        LoadLevel(Math.Max(1, _pendingLevelAfterScare));
        _pendingLevelAfterScare = 0;
    }

    private void PlayScareSound()
    {
        if (!_scareSettings.SoundEnabled ||
            _scareSound is null)
        {
            return;
        }

        try
        {
            _scareSound.Play(1f, 0f, 0f);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void TrySetNavigationTarget(Vector2 position, MazeLayout layout)
    {
        if (!layout.Board.Contains((int)position.X, (int)position.Y))
        {
            return;
        }

        var target = new Point(
            (int)((position.X - layout.Board.X) / layout.CellSize),
            (int)((position.Y - layout.Board.Y) / layout.CellSize));

        target = FindNearestOpenCell(target, 2);

        if (!_level.IsOpen(target) ||
            IsSameCell(target, _playerCell))
        {
            return;
        }

        var path = FindPath(_playerCell, target);

        if (path.Count == 0)
        {
            return;
        }

        _navigationPath.Clear();

        foreach (var step in path)
        {
            _navigationPath.Enqueue(step);
        }

        _navigationElapsed = NavigationStepInterval;
    }

    private Point FindNearestOpenCell(Point origin, int maxDistance)
    {
        if (_level.IsOpen(origin))
        {
            return origin;
        }

        for (var distance = 1; distance <= maxDistance; distance++)
        {
            for (var row = origin.Y - distance; row <= origin.Y + distance; row++)
            {
                for (var column = origin.X - distance; column <= origin.X + distance; column++)
                {
                    if (Math.Abs(column - origin.X) + Math.Abs(row - origin.Y) != distance)
                    {
                        continue;
                    }

                    var candidate = new Point(column, row);

                    if (_level.IsOpen(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        return origin;
    }

    private List<Point> FindPath(Point start, Point target)
    {
        var visited = new bool[_level.Columns, _level.Rows];
        var previous = new Point[_level.Columns, _level.Rows];
        var queue = new Queue<Point>();

        visited[start.X, start.Y] = true;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (IsSameCell(current, target))
            {
                break;
            }

            foreach (var direction in PathDirections)
            {
                var next = new Point(current.X + direction.X, current.Y + direction.Y);

                if (!_level.IsOpen(next) ||
                    visited[next.X, next.Y])
                {
                    continue;
                }

                visited[next.X, next.Y] = true;
                previous[next.X, next.Y] = current;
                queue.Enqueue(next);
            }
        }

        var path = new List<Point>();

        if (!visited[target.X, target.Y])
        {
            return path;
        }

        var pathCell = target;

        while (!IsSameCell(pathCell, start))
        {
            path.Add(pathCell);
            pathCell = previous[pathCell.X, pathCell.Y];
        }

        path.Reverse();
        return path;
    }

    private void LoadLevel(int levelNumber)
    {
        _level = MazeLevelGenerator.Create(_runSeed, levelNumber);
        _playerCell = _level.Start;
        _navigationPath.Clear();
        _navigationElapsed = 0d;
    }

    private static Point GetOffset(Direction direction)
    {
        return direction switch
        {
            Direction.Up => new Point(0, -1),
            Direction.Right => new Point(1, 0),
            Direction.Down => new Point(0, 1),
            _ => new Point(-1, 0),
        };
    }

    private static bool IsAdjacent(Point a, Point b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1;
    }

    private static bool IsSameCell(Point a, Point b)
    {
        return a.X == b.X && a.Y == b.Y;
    }

    private static int CreateRunSeed()
    {
        var seedText = Environment.GetEnvironmentVariable("LOST_MAZE_SEED");

        if (int.TryParse(seedText, out var seed))
        {
            return NormalizeSeed(seed);
        }

        return Random.Shared.Next(1, int.MaxValue);
    }

    private static ScareSettings CreateScareSettings()
    {
        var defaults = ScareSettings.Default;
        return new ScareSettings(
            ReadBooleanEnvironment("LOST_MAZE_SCARE_ENABLED", defaults.Enabled),
            ReadBooleanEnvironment("LOST_MAZE_SCARE_SOUND", defaults.SoundEnabled),
            ReadIntegerEnvironment("LOST_MAZE_SCARE_MIN_LEVEL", defaults.MinLevel),
            ReadIntegerEnvironment("LOST_MAZE_SCARE_MAX_LEVEL", defaults.MaxLevel),
            ReadDoubleEnvironment("LOST_MAZE_SCARE_DURATION", defaults.DurationSeconds)).Normalize();
    }

    private static int PickScareLevel(int runSeed, ScareSettings settings)
    {
        if (!settings.Enabled)
        {
            return int.MaxValue;
        }

        var normalized = settings.Normalize();
        var random = new Random(MixSeed(runSeed, 0x5CA1E));
        return random.Next(normalized.MinLevel, normalized.MaxLevel + 1);
    }

    private static bool ReadBooleanEnvironment(string name, bool fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);

        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "1" or "TRUE" or "YES" or "ON" => true,
            "0" or "FALSE" or "NO" or "OFF" => false,
            _ => fallback,
        };
    }

    private static int ReadIntegerEnvironment(string name, int fallback)
    {
        return int.TryParse(Environment.GetEnvironmentVariable(name), out var value)
            ? value
            : fallback;
    }

    private static double ReadDoubleEnvironment(string name, double fallback)
    {
        return double.TryParse(Environment.GetEnvironmentVariable(name), out var value)
            ? value
            : fallback;
    }

    private static byte[] CreateScareSoundBuffer()
    {
        const double duration = 1.15d;
        var random = new Random(7417);
        var sampleCount = (int)(ScareSoundSampleRate * duration);
        var buffer = new byte[sampleCount * 2];
        var phase = 0d;

        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (double)ScareSoundSampleRate;
            var attack = Math.Min(1d, t / 0.035d);
            var release = Math.Min(1d, (duration - t) / 0.30d);
            var envelope = attack * release;
            var sweep = 1080d - (t * 470d) + (Math.Sin(t * 82d) * 190d);
            phase += Math.Tau * sweep / ScareSoundSampleRate;

            var noise = (random.NextDouble() * 2d) - 1d;
            var sample = ((Math.Sin(phase) * 0.68d) + (Math.Sin(phase * 0.52d) * 0.28d) + (noise * 0.38d)) * envelope;
            var value = (short)Math.Clamp(sample * short.MaxValue, short.MinValue, short.MaxValue);
            var index = i * 2;
            buffer[index] = (byte)(value & 0xFF);
            buffer[index + 1] = (byte)((value >> 8) & 0xFF);
        }

        return buffer;
    }

    private static int MixSeed(int seed, int salt)
    {
        unchecked
        {
            var hash = (uint)NormalizeSeed(seed);
            hash ^= (uint)salt + 0x9E3779B9u + (hash << 6) + (hash >> 2);
            hash ^= hash >> 16;
            hash *= 0x85EBCA6Bu;
            hash ^= hash >> 13;
            hash *= 0xC2B2AE35u;
            hash ^= hash >> 16;
            return (int)(hash & 0x7FFFFFFF);
        }
    }

    private static int NormalizeSeed(int seed)
    {
        if (seed == int.MinValue)
        {
            return int.MaxValue;
        }

        var normalized = Math.Abs(seed);
        return normalized == 0 ? 1 : normalized;
    }

    private readonly record struct MazeLayout(Rectangle Board, int CellSize);

    private enum Direction
    {
        Up,
        Right,
        Down,
        Left,
    }
}
