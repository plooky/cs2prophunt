# CS2 Prop Hunt

A CounterStrikeSharp Prop Hunt game-mode controller for Counter-Strike 2 Workshop maps.

The plugin starts disabled on every map and must be explicitly enabled with `css_ph start` or the in-game admin menu.

## Requirements

- .NET 10 SDK
- CounterStrikeSharp API 1.0.370 or a compatible newer version

## Build and test

```text
dotnet test PropHunt.Tests/PropHunt.Tests.csproj -c Release
dotnet build PropHunt.Plugin/PropHunt.Plugin.csproj -c Release
```

## Install

Copy these Release build artifacts into:

```text
game/csgo/addons/counterstrikesharp/plugins/PropHunt/
```

Required artifacts:

- `PropHunt.dll`
- `PropHunt.Core.dll`
- `PropHunt.deps.json`
- `PropHunt.pdb`

Restart the server and verify the plugin with `css_plugins list`.

## Administration

The `css_ph` console command is available as `!ph` in public chat and `/ph` in silent chat. In-game use requires the CounterStrikeSharp permission `@ph/admin`; the server console is always allowed.
