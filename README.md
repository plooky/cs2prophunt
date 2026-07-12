# CS2 Prop Hunt

A CounterStrikeSharp supplement for Steam Workshop Prop Hunt maps. The maps provide prop models and gameplay mechanics; this plugin manages random hunters, teams, round flow, and administration.

## Rules

- Hunters are selected randomly every round.
- One hunter is used by default; a second is added when the configured prop threshold is exceeded.
- A prop who kills a hunter remains a prop for the following round.
- Hunters default to CT, but the side is configurable.
- Bots and spectators are excluded.

## Requirements

- Counter-Strike 2 dedicated server
- CounterStrikeSharp compatible with API `1.0.370`
- .NET 10 SDK to build the plugin

## Build and test

```powershell
dotnet test src/PropHunt/PropHunt.Tests/PropHunt.Tests.csproj -c Release
dotnet build src/PropHunt/PropHunt.Plugin/PropHunt.Plugin.csproj -c Release
```

## Installation

Create this directory on the server:

```text
game/csgo/addons/counterstrikesharp/plugins/PropHunt/
```

Copy these files from `src/PropHunt/PropHunt.Plugin/bin/Release/net10.0/`:

```text
PropHunt.dll
PropHunt.Core.dll
PropHunt.deps.json
```

Restart the server. The plugin creates `ph_config.json` in its module directory on first load.

## Administration

Players require the CounterStrikeSharp permission `@ph/admin`. Use `css_ph` for the admin menu or commands:

```text
css_ph start
css_ph pause
css_ph restart
css_ph reset
css_ph team <ct|t>
css_ph threshold <number>
css_ph force <player> [player]
css_ph status
css_ph reload
```

The server console has access to these commands without a player permission check. `css_ph_reload` reloads `ph_config.json` directly.
