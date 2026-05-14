using System;

namespace LostMaze;

public static class Program
{
    [STAThread]
    private static void Main()
    {
        using var game = new LostMazeGame();
        game.Run();
    }
}
