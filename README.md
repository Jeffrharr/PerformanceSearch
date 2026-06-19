# PerformanceSearch

Targeted performance fixes for common GUI slowdowns in heavily modded RimWorld games.

See [About/About.xml](About/About.xml) for the current list of fixes.

## Requirements

- [.NET SDK](https://dotnet.microsoft.com/download) 8.0+
- **Linux**: Mono (`sudo apt install mono-devel` or equivalent) — needed for the `net481` framework references
- **Windows**: .NET Framework 4.8 (ships with Windows; no extra install needed)

## Building

```bash
./build.sh       # Linux / Mac
./build.ps1      # Windows
```

Output lands in `1.6/Assemblies/SearchFix.dll`, which is where RimWorld expects it.

The mod directory should be symlinked into your RimWorld `Mods` folder so the game picks up the freshly built DLL automatically:

```bash
# Linux
ln -s /path/to/PerformanceSearch \
  ~/.local/share/Steam/steamapps/common/RimWorld/Mods/PerformanceSearch

# Windows (run as Administrator)
New-Item -ItemType SymbolicLink `
  -Path "$env:PROGRAMFILES (x86)\Steam\steamapps\common\RimWorld\Mods\PerformanceSearch" `
  -Target (Get-Location)
```

### Why `net481`?

RimWorld runs on Mono, a .NET Framework 4.x implementation. Targeting `net481` ensures the compiled DLL is compatible with the runtime the game provides. The `Krafs.Rimworld.Ref` NuGet package supplies RimWorld's assemblies at compile time so the project builds without needing the game installed.

### Linux: no `FrameworkPathOverride` needed

The `Directory.Build.props` at the repo root sets `FrameworkPathOverride` automatically on Linux, pointing MSBuild to Mono's `net481` framework assemblies. You do not need to prefix `dotnet build` with any environment variable.

## Testing

The test suite uses `Mono.Cecil` to verify that the RimWorld types and methods the mod patches still exist in `Assembly-CSharp.dll`. Run it after every RimWorld update before loading the game:

```bash
./test.sh        # Linux / Mac
./test.ps1       # Windows
```

A test failure means a patched method was renamed or removed in the update and the mod needs attention before it is safe to load.

Tests do **not** require a running game — they inspect the DLL directly.

## Contributing

All changes go through a PR. Direct pushes to `master` are blocked.

When adding a new performance fix:
1. Create a branch from `master`
2. Implement the patch in `Source/`
3. Add API compatibility tests in `Tests/SearchFix.Tests/ApiCompatibilityTests.cs` for every type and method the patch targets
4. Add a plain-language bullet to the `<description>` in `About/About.xml` describing the fix in terms a regular player would understand (see `CLAUDE.md` for the convention)
5. Open a PR
