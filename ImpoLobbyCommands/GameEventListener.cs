using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Impostor.Api.Net;
using Impostor.Api.Events;
using Impostor.Api.Events.Player;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Api.Innersloth.Customization;
using Microsoft.Extensions.Logging;

namespace Impostor.Plugins.LobbyCommands.Handlers
{
    enum Gamemode { standard, hns };

    /// <summary>
    ///     A class that listens for two events.
    /// </summary>
    class GameEventListener : IEventListener
    {
        private readonly ILogger<LobbyCommandsPlugin> _logger;
        private readonly string[] _mapNames = Enum.GetNames(typeof(MapTypes));
        private Dictionary<string, Api.Innersloth.Customization.ColorType> colors = new Dictionary<string, Api.Innersloth.Customization.ColorType>();
        private Dictionary<string, Api.Innersloth.Customization.HatType> hats = new Dictionary<string, Api.Innersloth.Customization.HatType>();
        private Dictionary<string, Api.Innersloth.Customization.SkinType> skins = new Dictionary<string, Api.Innersloth.Customization.SkinType>();
        private Dictionary<string, Api.Innersloth.Customization.PetType> pets = new Dictionary<string, Api.Innersloth.Customization.PetType>();
        private Dictionary<IGame, Gamemode> mode = new Dictionary<IGame, Gamemode>();
        private Dictionary<IGame, Dictionary<Gamemode, GameOptionsData>> savedOptions = new Dictionary<IGame, Dictionary<Gamemode, GameOptionsData>>();
        private readonly Random _random = new Random();

        public GameEventListener(ILogger<LobbyCommandsPlugin> logger)
        {
            _logger = logger;
            foreach (Api.Innersloth.Customization.ColorType c in Enum.GetValues(typeof(Api.Innersloth.Customization.ColorType)))
            {
                colors[c.ToString().ToLowerInvariant()] = c;
            }
            foreach (Api.Innersloth.Customization.HatType h in Enum.GetValues(typeof(Api.Innersloth.Customization.HatType)))
            {
                hats[h.ToString().ToLowerInvariant()] = h;
            }
            foreach (Api.Innersloth.Customization.SkinType s in Enum.GetValues(typeof(Api.Innersloth.Customization.SkinType)))
            {
                skins[s.ToString().ToLowerInvariant()] = s;
            }
            foreach (Api.Innersloth.Customization.PetType p in Enum.GetValues(typeof(Api.Innersloth.Customization.PetType)))
            {
                pets[p.ToString().ToLowerInvariant()] = p;
            }
        }

        private static GameOptionsData GetHnsOptions()
        {
            GameOptionsData hnsOptions = new GameOptionsData();
            hnsOptions.ImpostorLightMod = 0.25f;
            hnsOptions.CrewLightMod = 0.75f;
            hnsOptions.KillDistance = KillDistances.Short;
            hnsOptions.DiscussionTime = 0;
            hnsOptions.VotingTime = 1;
            hnsOptions.KillCooldown = 10f;
            hnsOptions.NumEmergencyMeetings = 0;
            hnsOptions.NumImpostors = 1;
            hnsOptions.EmergencyCooldown = 0;
            hnsOptions.ConfirmImpostor = true;
            hnsOptions.VisualTasks = false;
            hnsOptions.AnonymousVotes = true;
            hnsOptions.NumCommonTasks = 0;
            hnsOptions.NumShortTasks = 4;
            hnsOptions.NumLongTasks = 0;
            hnsOptions.PlayerSpeedMod = 1.5f;
            hnsOptions.TaskBarUpdate = TaskBarUpdate.Always;
            return hnsOptions;
        }

        [EventListener]
        public void OnGameCreated(IGameCreatedEvent e)
        {
            Dictionary<Gamemode, GameOptionsData> gameSavedOptions = new Dictionary<Gamemode, GameOptionsData>();
            gameSavedOptions.Add(Gamemode.standard, new GameOptionsData());
            gameSavedOptions.Add(Gamemode.hns, GetHnsOptions());
            savedOptions.Add(e.Game, gameSavedOptions);
            mode.Add(e.Game, Gamemode.standard);
            _logger.LogInformation($"Game created with code {e.Game.Code}");
        }

        [EventListener]
        public void OnGameDestroyed(IGameDestroyedEvent e)
        {
            _ = savedOptions.Remove(e.Game);
            _ = mode.Remove(e.Game);
            _logger.LogInformation($"Game with code {e.Game.Code} was destroyed");
        }

        [EventListener]
        public void OnGameStarted(IGameStartedEvent e)
        {
            //_logger.LogInformation("Game is starting.");
            if (mode[e.Game] == Gamemode.hns)
            {
                foreach (var player in e.Game.Players)
                {
                    if (player.Character.PlayerInfo.IsImpostor)
                    {
                        player.Character.SetColorAsync(Api.Innersloth.Customization.ColorType.Red);
                    }
                    else
                    {
                        player.Character.SetColorAsync(Api.Innersloth.Customization.ColorType.Lime);
                    }
                }
            }
        }

        [EventListener]
        public void OnGameEnded(IGameEndedEvent e)
        {
            //_logger.LogInformation("Game has ended.");
        }

        [EventListener]
        public async ValueTask OnPlayerChat(IPlayerChatEvent e)
        {

            if (e.Game.GameState != GameStates.NotStarted || !e.Message.StartsWith("/") || !e.ClientPlayer.IsHost)
                return;

            string[] parts = e.Message.ToLowerInvariant()[1..].Split(" ");

            switch (parts[0])
            {
                case "help":
                    await ServerSendChatAsync("Commands list: /map, /gamemode, /killcd, /vision", e.ClientPlayer.Character);
                    return;
                case "map":
                    if (parts.Length == 1)
                    {
                        await ServerSendChatAsync($"Available Maps: {string.Join(", ", _mapNames)}", e.ClientPlayer.Character);
                        return;
                    }

                    if (!_mapNames.Any(name => name.ToLowerInvariant() == parts[1]))
                    {
                        await ServerSendChatAsync($"Unknown map. Available Maps: {string.Join(", ", _mapNames)}", e.ClientPlayer.Character);
                        return;
                    }

                    MapTypes map = Enum.Parse<MapTypes>(parts[1], true);

                    await ServerSendChatAsync($"Setting map to {map}", e.ClientPlayer.Character);

                    e.Game.Options.Map = map;
                    await e.Game.SyncSettingsAsync();
                    break;
                case "gamemode":
                    if (parts.Length == 1)
                    {
                        await ServerSendChatAsync($"Current Game Mode: {mode[e.Game]}", e.ClientPlayer.Character);
                        await ServerSendChatAsync($"(Available Modes: Normal, HideNSeek)", e.ClientPlayer.Character);
                        return;
                    }
                    if (parts.Length == 2)
                    {
                        switch (parts[1])
                        {
                            case "standard":
                            case "normal":
                                if (mode[e.Game] != Gamemode.standard)
                                {
                                    loadOptions(savedOptions[e.Game][mode[e.Game]], e.Game.Options); //save options
                                    mode[e.Game] = Gamemode.standard; //load options
                                    loadOptions(e.Game.Options, savedOptions[e.Game][mode[e.Game]]);
                                    await e.Game.SyncSettingsAsync();
                                    await ServerSendChatAsync("Setting game mode to Normal", e.ClientPlayer.Character);
                                }
                                return;
                            case "hns":
                            case "hidenseek":
                            case "hideandseek":
                                if (mode[e.Game] != Gamemode.hns)
                                {
                                    loadOptions(savedOptions[e.Game][mode[e.Game]], e.Game.Options); //save options
                                    mode[e.Game] = Gamemode.hns; //load options
                                    loadOptions(e.Game.Options, savedOptions[e.Game][mode[e.Game]]);
                                    await e.Game.SyncSettingsAsync();
                                    await ServerSendChatAsync("Setting game mode to HideNSeek", e.ClientPlayer.Character);
                                }
                                return;
                        }
                    }

                    await ServerSendChatAsync($"Invalid command. Expecting: '/gamemode {{normal|hns}}'", e.ClientPlayer.Character);
                    break;
                case "killcd":
                    if (parts.Length == 2)
                    {
                        if (float.TryParse(parts[1], out float killCooldownNum))
                        {
                            e.Game.Options.KillCooldown = killCooldownNum;
                            await e.Game.SyncSettingsAsync();
                            await ServerSendChatAsync($"Setting kill cooldown to {killCooldownNum}", e.ClientPlayer.Character);
                            break;
                        }
                    }

                    await ServerSendChatAsync($"Invalid command. Expecting: '/killcd VALUE'", e.ClientPlayer.Character);
                    break;
                case "vision":
                    if (parts.Length == 3)
                    {
                        if (float.TryParse(parts[2], out float visionNum))
                        {
                            switch (parts[1])
                            {
                                case "imp":
                                case "impostor":
                                case "impostors":
                                case "imposter":
                                case "imposters":
                                    e.Game.Options.ImpostorLightMod = visionNum;
                                    await e.Game.SyncSettingsAsync();
                                    await ServerSendChatAsync($"Setting vision for Impostors to {visionNum}", e.ClientPlayer.Character);
                                    return;
                                case "crew":
                                case "crewmate":
                                case "crewmates":
                                    e.Game.Options.CrewLightMod = visionNum;
                                    await e.Game.SyncSettingsAsync();
                                    await ServerSendChatAsync($"Setting vision for Crewmates to {visionNum}", e.ClientPlayer.Character);
                                    return;
                            }
                        }
                    }

                    await ServerSendChatAsync($"Invalid command. Expecting: '/vision {{impostor|crewmate}} VALUE'", e.ClientPlayer.Character);
                    break;
                default:
                    _logger.LogInformation($"Unknown command {parts[0]} from {e.PlayerControl.PlayerInfo.PlayerName} on {e.Game.Code.Code}.");
                    break;
            }
        }

        private async ValueTask ServerSendChatAsync(string text, IInnerPlayerControl player, bool toPlayer = false)
        {
            string playername = player.PlayerInfo.PlayerName;
            await player.SetNameAsync($"Server");
            if (toPlayer)
            {
                await player.SendChatToPlayerAsync($"{text}");
            }
            else
            {
                await player.SendChatAsync($"{text}");
            }
            await player.SetNameAsync(playername);
        }

        private void loadOptions(GameOptionsData dest, GameOptionsData source)
        {
            dest.ImpostorLightMod = source.ImpostorLightMod;
            dest.CrewLightMod = source.CrewLightMod;
            dest.DiscussionTime = source.DiscussionTime;
            dest.VotingTime = source.VotingTime;
            dest.NumEmergencyMeetings = source.NumEmergencyMeetings;
            dest.KillDistance = source.KillDistance;
            dest.KillCooldown = source.KillCooldown;
            dest.PlayerSpeedMod = source.PlayerSpeedMod;
            dest.ConfirmImpostor = source.ConfirmImpostor;
            dest.NumImpostors = source.NumImpostors;
            dest.NumCommonTasks = source.NumCommonTasks;
            dest.NumShortTasks = source.NumShortTasks;
            dest.NumLongTasks = source.NumLongTasks;
            dest.TaskBarUpdate = source.TaskBarUpdate;
            dest.VisualTasks = source.VisualTasks;
            dest.EmergencyCooldown = source.EmergencyCooldown;
        }
    }
}
