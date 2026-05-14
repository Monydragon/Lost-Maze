using System;
using System.Collections.Generic;
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

public sealed class MazeLevel
{
    private readonly bool[,] _walls;

    public MazeLevel(
        int number,
        int seed,
        int difficulty,
        int revealRadius,
        bool[,] walls,
        Point start,
        Point exit)
    {
        Number = number;
        Seed = seed;
        Difficulty = difficulty;
        RevealRadius = revealRadius;
        _walls = walls;
        Start = start;
        Exit = exit;
    }

    public int Number { get; }

    public int Seed { get; }

    public int Difficulty { get; }

    public int RevealRadius { get; }

    public int Columns => _walls.GetLength(0);

    public int Rows => _walls.GetLength(1);

    public Point Start { get; }

    public Point Exit { get; }

    public bool Contains(Point cell)
    {
        return cell.X >= 0 &&
               cell.Y >= 0 &&
               cell.X < Columns &&
               cell.Y < Rows;
    }

    public bool IsOpen(Point cell)
    {
        return Contains(cell) && !_walls[cell.X, cell.Y];
    }

    public bool IsWall(Point cell)
    {
        return !Contains(cell) || _walls[cell.X, cell.Y];
    }
}

public readonly record struct ScareSettings(
    bool Enabled,
    bool SoundEnabled,
    int MinLevel,
    int MaxLevel,
    double DurationSeconds)
{
    public static ScareSettings Default => new(true, true, 4, 8, 2.25d);

    public ScareSettings Normalize()
    {
        var minLevel = Math.Max(2, MinLevel);
        var maxLevel = Math.Max(minLevel, MaxLevel);
        return new ScareSettings(
            Enabled,
            SoundEnabled,
            minLevel,
            maxLevel,
            Math.Clamp(DurationSeconds, 0.75d, 8d));
    }
}

internal static class MazeLevelGenerator
{
    private static readonly Point[] CarveDirections =
    [
        new(0, -2),
        new(2, 0),
        new(0, 2),
        new(-2, 0),
    ];

    private static readonly Point[] StepDirections =
    [
        new(0, -1),
        new(1, 0),
        new(0, 1),
        new(-1, 0),
    ];

    public static MazeLevel Create(int runSeed, int levelNumber)
    {
        var number = Math.Max(1, levelNumber);
        var difficulty = Math.Min(10, 1 + ((number - 1) / 2));
        var growth = Math.Min(8, (number - 1) / 2);
        var revealRadius = Math.Max(4, 9 - ((number - 1) / 2));
        var columns = 11 + (growth * 2);
        var rows = 9 + (growth * 2);
        var levelSeed = MixSeed(runSeed, number);
        var random = new Random(levelSeed);
        var walls = new bool[columns, rows];

        for (var column = 0; column < columns; column++)
        {
            for (var row = 0; row < rows; row++)
            {
                walls[column, row] = true;
            }
        }

        var start = new Point(1, 1);
        var stack = new Stack<Point>();
        walls[start.X, start.Y] = false;
        stack.Push(start);

        while (stack.Count > 0)
        {
            var current = stack.Peek();
            var candidates = GetUncarvedNeighbors(current, walls);

            if (candidates.Count == 0)
            {
                stack.Pop();
                continue;
            }

            var next = candidates[random.Next(candidates.Count)];
            var passage = new Point(
                current.X + ((next.X - current.X) / 2),
                current.Y + ((next.Y - current.Y) / 2));

            walls[passage.X, passage.Y] = false;
            walls[next.X, next.Y] = false;
            stack.Push(next);
        }

        var exit = FindFarthestOpenCell(walls, start);
        return new MazeLevel(number, levelSeed, difficulty, revealRadius, walls, start, exit);
    }

    private static List<Point> GetUncarvedNeighbors(Point current, bool[,] walls)
    {
        var neighbors = new List<Point>(CarveDirections.Length);
        var columns = walls.GetLength(0);
        var rows = walls.GetLength(1);

        foreach (var direction in CarveDirections)
        {
            var candidate = new Point(current.X + direction.X, current.Y + direction.Y);

            if (candidate.X <= 0 ||
                candidate.Y <= 0 ||
                candidate.X >= columns - 1 ||
                candidate.Y >= rows - 1 ||
                !walls[candidate.X, candidate.Y])
            {
                continue;
            }

            neighbors.Add(candidate);
        }

        return neighbors;
    }

    private static Point FindFarthestOpenCell(bool[,] walls, Point start)
    {
        var columns = walls.GetLength(0);
        var rows = walls.GetLength(1);
        var visited = new bool[columns, rows];
        var distances = new int[columns, rows];
        var queue = new Queue<Point>();
        var farthest = start;

        visited[start.X, start.Y] = true;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (distances[current.X, current.Y] > distances[farthest.X, farthest.Y])
            {
                farthest = current;
            }

            foreach (var direction in StepDirections)
            {
                var next = new Point(current.X + direction.X, current.Y + direction.Y);

                if (next.X <= 0 ||
                    next.Y <= 0 ||
                    next.X >= columns - 1 ||
                    next.Y >= rows - 1 ||
                    visited[next.X, next.Y] ||
                    walls[next.X, next.Y])
                {
                    continue;
                }

                visited[next.X, next.Y] = true;
                distances[next.X, next.Y] = distances[current.X, current.Y] + 1;
                queue.Enqueue(next);
            }
        }

        return farthest;
    }

    private static int MixSeed(int runSeed, int levelNumber)
    {
        unchecked
        {
            var hash = (uint)runSeed;
            hash ^= (uint)levelNumber + 0x9E3779B9u + (hash << 6) + (hash >> 2);
            hash ^= hash >> 16;
            hash *= 0x85EBCA6Bu;
            hash ^= hash >> 13;
            hash *= 0xC2B2AE35u;
            hash ^= hash >> 16;
            return (int)(hash & 0x7FFFFFFF);
        }
    }
}
