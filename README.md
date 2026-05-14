# Lost Maze

Lost Maze is a KNI game scaffold that mirrors the project shape used by `ShapeMeUp` and `CountMeUp`.

Included projects:

- `LostMaze.Shared` for shared game logic
- `LostMaze.SDL2.GL` for desktop OpenGL
- `LostMaze.WinForms.DX11` for Windows DirectX
- `LostMaze.Blazor.GL` for WebAssembly
- `LostMaze.Android.GL` for Android
- `LostMazeContent` for shared KNI content pipeline assets

The first screen is a lightweight placeholder maze scene so each platform has a real game entry point ready for future prompts.

Useful commands:

```powershell
dotnet build .\LostMaze.SDL2.GL\LostMaze.SDL2.GL.csproj
```

```powershell
dotnet build .\LostMaze.WinForms.DX11\LostMaze.WinForms.DX11.csproj
```

```powershell
dotnet run --project .\LostMaze.Blazor.GL\LostMaze.Blazor.GL.csproj
```

The working prompt tracker lives at `docs/Lost Maze LLM Prompts Source.txt`.
