using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using PropHunt.Core;

namespace PropHunt.Plugin;

public sealed class PropHuntPlugin : BasePlugin
{
    private const string Prefix = "[PH]";
    private static readonly string[] AdminPermission = { "@ph/admin" };
    private readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
    private readonly RandomSeekerSelector selector = new();
    private readonly HashSet<ulong> currentSeekers = new();
    private readonly HashSet<ulong> forcedNextRound = new();
    private readonly Random random = new();
    private PluginConfig config = new();
    private bool enabled;
    private string currentMapName = string.Empty;

    public override string ModuleName => "PropHunt";
    public override string ModuleVersion => "0.1.0";
    public override string ModuleAuthor => "plooky";
    public override string ModuleDescription => "Prop Hunt team and round controller.";

    private string ConfigPath => Path.Combine(ModuleDirectory, "ph_config.json");

    public override void Load(bool hotReload)
    {
        LoadConfig();
        AddCommand("css_ph", "Open the Prop Hunt admin menu.", OnPhCommand);
        AddCommand("css_ph_reload", "Reload Prop Hunt configuration.", OnReloadCommand);
        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        RegisterListener<Listeners.OnMapEnd>(OnMapEnd);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);

        currentMapName = Server.MapName;
        enabled = false;
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (!enabled)
        {
            return HookResult.Continue;
        }

        ApplyServerCvars();
        ApplyTeamsForRound();

        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (!enabled)
        {
            return HookResult.Continue;
        }

        if (!TryGetPlayerId(@event.Userid, out var victimId))
        {
            return HookResult.Continue;
        }

        ulong? attackerId = null;
        if (TryGetPlayerId(@event.Attacker, out var parsedAttackerId))
        {
            attackerId = parsedAttackerId;
        }

        selector.RecordKill(
            victimId,
            attackerId,
            currentSeekers.Contains(victimId),
            attackerId.HasValue && currentSeekers.Contains(attackerId.Value));
        return HookResult.Continue;
    }

    private void OnPhCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (!HasAdmin(player))
        {
            command.ReplyToCommand($"{Prefix} You do not have access to Prop Hunt admin controls.");
            return;
        }

        var action = command.ArgCount > 1 ? command.GetArg(1).ToLowerInvariant() : string.Empty;
        if (command.ArgCount <= 1)
        {
            if (player == null)
            {
                command.ReplyToCommand($"{Prefix} Usage: css_ph start|stop|restart|reset|team <ct|t>|threshold <number>|force <player> [player]|status|reload");
                return;
            }

            OpenMenu(player);
            return;
        }

        switch (action)
        {
            case "start":
                enabled = true;
                StartOrRestart("Prop Hunt started by admin");
                break;
            case "stop":
                Stop("Prop Hunt stopped by admin");
                break;
            case "restart":
                if (!RequireEnabled(command)) break;
                StartOrRestart("Prop Hunt round restarted by admin");
                break;
            case "reset":
                ResetSelection();
                if (enabled)
                {
                    StartOrRestart("Prop Hunt selection reset by admin");
                }
                else
                {
                    command.ReplyToCommand($"{Prefix} Selection reset; Prop Hunt remains disabled.");
                }
                break;
            case "team":
                SetTeamCommand(command);
                break;
            case "threshold":
                SetThresholdCommand(command);
                break;
            case "force":
                ForceSeekersCommand(command);
                break;
            case "status":
                PrintStatus(command);
                break;
            case "reload":
                Reload(command);
                break;
            default:
                command.ReplyToCommand($"{Prefix} Unknown action.");
                break;
        }
    }

    private void OnReloadCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (!HasAdmin(player))
        {
            command.ReplyToCommand($"{Prefix} You do not have access to reload Prop Hunt.");
            return;
        }

        Reload(command);
    }

    private void Reload(CommandInfo command)
    {
        LoadConfig();
        command.ReplyToCommand($"{Prefix} Configuration reloaded.");
    }

    private void OnMapStart(string mapName)
    {
        currentMapName = mapName ?? string.Empty;
        enabled = false;
        ResetSelection();
        Logger.LogInformation("Prop Hunt is disabled for map {MapName} until explicitly started.", currentMapName);
    }

    private void OnMapEnd()
    {
        enabled = false;
        ResetSelection();
    }

    private void OpenMenu(CCSPlayerController player)
    {
        var menu = new ChatMenu("Prop Hunt Admin");
        menu.AddMenuOption(enabled ? "Restart round" : "Start Prop Hunt", (admin, _) =>
        {
            var wasEnabled = enabled;
            enabled = true;
            StartOrRestart(wasEnabled ? "Prop Hunt restarted from menu" : "Prop Hunt started from menu");
        });
        menu.AddMenuOption(enabled ? "Stop Prop Hunt" : "Prop Hunt is stopped", (admin, _) =>
        {
            if (enabled)
            {
                Stop("Prop Hunt stopped from menu");
            }
            else
            {
                admin.PrintToChat($"{Prefix} Prop Hunt is already stopped.");
            }
        });
        menu.AddMenuOption("Reset random selection", (admin, _) =>
        {
            ResetSelection();
            if (enabled)
            {
                StartOrRestart("Prop Hunt selection reset from menu");
            }
            else
            {
                admin.PrintToChat($"{Prefix} Selection reset; Prop Hunt remains disabled.");
            }
        });
        menu.AddMenuOption($"Seeker side: {NormalizeTeamName(config.SeekerTeam)}", (admin, _) =>
        {
            config.SeekerTeam = NormalizeTeamName(config.SeekerTeam) == "CT" ? "T" : "CT";
            SaveConfig();
            ResetSelection();
            if (enabled)
            {
                StartOrRestart("Prop Hunt seeker side changed");
            }
        });
        menu.AddMenuOption($"Threshold +1 ({config.TwoSeekerHiderThreshold})", (admin, _) =>
        {
            config.TwoSeekerHiderThreshold++;
            SaveConfig();
            admin.PrintToChat($"{Prefix} Two-seeker threshold is now {config.TwoSeekerHiderThreshold} hiders.");
        });
        menu.AddMenuOption($"Threshold -1 ({config.TwoSeekerHiderThreshold})", (admin, _) =>
        {
            config.TwoSeekerHiderThreshold = Math.Max(2, config.TwoSeekerHiderThreshold - 1);
            SaveConfig();
            admin.PrintToChat($"{Prefix} Two-seeker threshold is now {config.TwoSeekerHiderThreshold} hiders.");
        });
        menu.AddMenuOption("Force seeker", (admin, _) => OpenForceSeekerMenu(admin));
        MenuManager.OpenChatMenu(player, menu);
    }

    private void OpenForceSeekerMenu(CCSPlayerController admin)
    {
        if (!enabled)
        {
            admin.PrintToChat($"{Prefix} Start Prop Hunt before forcing a seeker.");
            return;
        }

        var menu = new ChatMenu("Force seeker");
        foreach (var player in ActivePlayers())
        {
            var target = player;
            menu.AddMenuOption(target.PlayerName, (caller, _) =>
            {
                if (TryGetPlayerId(target, out var id))
                {
                    ForceSeekers(new[] { id });
                    caller.PrintToChat($"{Prefix} Forced {target.PlayerName} as seeker.");
                }
            });
        }

        MenuManager.OpenChatMenu(admin, menu);
    }

    private void SetTeamCommand(CommandInfo command)
    {
        if (command.ArgCount <= 2)
        {
            command.ReplyToCommand($"{Prefix} Usage: !ph team <ct|t>");
            return;
        }

        var token = command.GetArg(2).Trim();
        if (!token.Equals("ct", StringComparison.OrdinalIgnoreCase) &&
            !token.Equals("t", StringComparison.OrdinalIgnoreCase))
        {
            command.ReplyToCommand($"{Prefix} Usage: !ph team <ct|t>");
            return;
        }

        config.SeekerTeam = NormalizeTeamName(token);
        SaveConfig();
        ResetSelection();
        if (enabled)
        {
            StartOrRestart("Prop Hunt seeker side changed by admin");
        }
    }

    private void SetThresholdCommand(CommandInfo command)
    {
        if (command.ArgCount <= 2 || !int.TryParse(command.GetArg(2), out var threshold) || threshold < 2)
        {
            command.ReplyToCommand($"{Prefix} Usage: !ph threshold <number greater than 1>");
            return;
        }

        config.TwoSeekerHiderThreshold = threshold;
        SaveConfig();
        command.ReplyToCommand($"{Prefix} Two-seeker threshold set to {threshold} hiders.");
    }

    private void ForceSeekersCommand(CommandInfo command)
    {
        if (!RequireEnabled(command))
        {
            return;
        }

        if (command.ArgCount <= 2)
        {
            command.ReplyToCommand($"{Prefix} Usage: !ph force <player name, slot, or steamid64> [second player]");
            return;
        }

        var forced = new List<ulong>();
        for (var i = 2; i < command.ArgCount; i++)
        {
            var player = FindPlayer(command.GetArg(i));
            if (player == null || !TryGetPlayerId(player, out var id))
            {
                command.ReplyToCommand($"{Prefix} Could not find player: {command.GetArg(i)}");
                return;
            }

            forced.Add(id);
        }

        ForceSeekers(forced);
        command.ReplyToCommand($"{Prefix} Forced seekers and overrode next-round protection.");
    }

    private void ForceSeekers(IEnumerable<ulong> seekerIds)
    {
        var players = ActivePlayers();
        var seekerCount = DesiredSeekerCount(players.Count);
        var forced = seekerIds.Where(id => id != 0).Distinct().Take(seekerCount).ToList();
        forced.AddRange(players.Select(PlayerIdOrZero).Where(id => id != 0 && !forced.Contains(id))
            .Take(Math.Max(0, seekerCount - forced.Count)));
        selector.OverrideProtection();
        forcedNextRound.Clear();
        forcedNextRound.UnionWith(forced);
        Server.PrintToChatAll($"{Prefix} Prop Hunt seekers forced by admin.");
        Server.ExecuteCommand("mp_restartgame 1");
    }

    private void PrintStatus(CommandInfo command)
    {
        var players = ActivePlayers();
        var desired = DesiredSeekerCount(players.Count);
        command.ReplyToCommand($"{Prefix} enabled={enabled}, map={currentMapName}, players={players.Count}, desired_seekers={desired}, protected_next_round={selector.ProtectedNextRound.Count}, threshold={config.TwoSeekerHiderThreshold}, seeker_team={NormalizeTeamName(config.SeekerTeam)}");
    }

    private bool RequireEnabled(CommandInfo command)
    {
        if (enabled)
        {
            return true;
        }

        command.ReplyToCommand($"{Prefix} Prop Hunt is disabled. Use css_ph start first.");
        return false;
    }

    private void Stop(string reason)
    {
        enabled = false;
        ResetSelection();
        Server.PrintToChatAll($"{Prefix} {reason}.");
    }

    private void StartOrRestart(string reason)
    {
        ApplyServerCvars();
        Server.PrintToChatAll($"{Prefix} {reason}.");
        Server.ExecuteCommand("mp_restartgame 1");
    }

    private void ResetSelection()
    {
        selector.Reset();
        currentSeekers.Clear();
        forcedNextRound.Clear();
    }

    private bool ApplyTeamsForRound()
    {
        var players = ActivePlayers();
        var seekerCount = DesiredSeekerCount(players.Count);
        if (!PropHuntRules.HasValidBalance(players.Count - seekerCount, seekerCount))
        {
            Server.PrintToChatAll($"{Prefix} Waiting for more props than seekers.");
            return false;
        }

        var ids = players.Select(PlayerIdOrZero).Where(id => id != 0).ToList();
        List<ulong> nextSeekers;
        if (forcedNextRound.Count > 0)
        {
            nextSeekers = forcedNextRound.Where(ids.Contains).Take(seekerCount).ToList();
            nextSeekers.AddRange(ids.Where(id => !nextSeekers.Contains(id))
                .Take(Math.Max(0, seekerCount - nextSeekers.Count)));
            forcedNextRound.Clear();
        }
        else
        {
            nextSeekers = selector.ChooseNextSeekers(ids, seekerCount, random.Next).ToList();
        }
        if (nextSeekers.Count < seekerCount)
        {
            Server.PrintToChatAll($"{Prefix} Waiting for enough unprotected players to select seekers.");
            return false;
        }

        currentSeekers.Clear();
        currentSeekers.UnionWith(nextSeekers);
        ApplyAssignedTeams(players, nextSeekers);
        return true;
    }

    private void ApplyAssignedTeams(IEnumerable<CCSPlayerController> players, IReadOnlyCollection<ulong> seekers)
    {
        var seekerTeam = SeekerCsTeam();
        var propTeam = seekerTeam == CsTeam.CounterTerrorist ? CsTeam.Terrorist : CsTeam.CounterTerrorist;
        foreach (var player in players)
        {
            var target = seekers.Contains(PlayerIdOrZero(player)) ? seekerTeam : propTeam;
            if ((CsTeam)player.TeamNum != target)
            {
                player.SwitchTeam(target);
            }
        }
    }

    private List<CCSPlayerController> ActivePlayers() => Utilities.GetPlayers()
        .Where(player => player.IsValid && !player.IsBot)
        .Where(player => (CsTeam)player.TeamNum == CsTeam.Terrorist || (CsTeam)player.TeamNum == CsTeam.CounterTerrorist)
        .ToList();

    private int DesiredSeekerCount(int playerCount) =>
        PropHuntRules.GetSeekerCountForTotalPlayers(playerCount, config.TwoSeekerHiderThreshold);

    private bool HasAdmin(CCSPlayerController? player) =>
        player == null || AdminManager.PlayerHasPermissions(player, AdminPermission);

    private CCSPlayerController? FindPlayer(string token)
    {
        var players = ActivePlayers();
        if (int.TryParse(token, out var slot))
        {
            var bySlot = players.FirstOrDefault(player => player.Slot == slot);
            if (bySlot != null) return bySlot;
        }

        if (ulong.TryParse(token, out var steamId))
        {
            var byId = players.FirstOrDefault(player => player.AuthorizedSteamID?.SteamId64 == steamId);
            if (byId != null) return byId;
        }

        return players.FirstOrDefault(player => player.PlayerName.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryGetPlayerId(CCSPlayerController? player, out ulong id)
    {
        id = PlayerIdOrZero(player);
        return id != 0;
    }

    private static ulong PlayerIdOrZero(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.IsBot) return 0;
        return player.AuthorizedSteamID?.SteamId64 ?? (ulong)(player.Slot + 1);
    }

    private CsTeam SeekerCsTeam() => NormalizeTeamName(config.SeekerTeam) == "T"
        ? CsTeam.Terrorist
        : CsTeam.CounterTerrorist;

    private static string NormalizeTeamName(string team) =>
        team.Trim().Equals("T", StringComparison.OrdinalIgnoreCase) ||
        team.Trim().Equals("TERRORIST", StringComparison.OrdinalIgnoreCase) ? "T" : "CT";

    private static void ApplyServerCvars()
    {
        Server.ExecuteCommand("bot_quota 0");
        Server.ExecuteCommand("mp_autokick 0");
        Server.ExecuteCommand("mp_autoteambalance 0");
        Server.ExecuteCommand("mp_limitteams 0");
        Server.ExecuteCommand("mp_warmuptime 5");
        Server.ExecuteCommand("sv_alltalk 1");
        Server.ExecuteCommand("sv_deadtalk 1");
        Server.ExecuteCommand("sv_full_alltalk 1");
        Server.ExecuteCommand("sv_talk_enemy_dead 1");
        Server.ExecuteCommand("sv_talk_enemy_living 1");
    }

    private void LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                config = JsonSerializer.Deserialize<PluginConfig>(File.ReadAllText(ConfigPath), jsonOptions) ?? new PluginConfig();
            }
            else
            {
                config = new PluginConfig();
                SaveConfig();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load Prop Hunt config. Using defaults.");
            config = new PluginConfig();
        }

        config.SeekerTeam = NormalizeTeamName(config.SeekerTeam);
        config.TwoSeekerHiderThreshold = Math.Max(2, config.TwoSeekerHiderThreshold);
    }

    private void SaveConfig()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath) ?? ".");
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, jsonOptions) + Environment.NewLine);
    }
}
