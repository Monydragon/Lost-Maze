using Microsoft.Xna.Framework;

namespace LostMaze;

public enum LostMazeScreen
{
    Title,
    Maze,
}

public readonly record struct SafeAreaInsets(int Left, int Top, int Right, int Bottom);

public readonly record struct MazePalette(
    Color BackgroundTop,
    Color BackgroundBottom,
    Color Floor,
    Color Wall,
    Color WallEdge,
    Color Accent,
    Color Text,
    Color MutedText);
