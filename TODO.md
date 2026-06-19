# TODO

## Windows build support

The README documents `build.ps1` and `test.ps1` but neither exists yet. The project
also lacks a `Directory.Build.props` to handle `FrameworkPathOverride` automatically,
so Windows builds currently require a manual environment variable prefix.

Run `/setup-mod-project` in retrofit mode to add:
- `Directory.Build.props` with the Linux-only `FrameworkPathOverride` condition
- `build.ps1` wrapping `dotnet build`
- `test.ps1` wrapping `dotnet test`
