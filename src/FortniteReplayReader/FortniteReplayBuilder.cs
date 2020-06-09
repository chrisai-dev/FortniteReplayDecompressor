﻿using FortniteReplayReader.Models;
using FortniteReplayReader.Models.NetFieldExports;
using FortniteReplayReader.Models.NetFieldExports.Weapons;
using System.Collections.Generic;
using System.Linq;

namespace FortniteReplayReader
{
    /// <summary>
    /// Responsible for constructing the <see cref="FortniteReplay"/> out of the received exports.
    /// </summary>
    public class FortniteReplayBuilder
    {
        private GameData GameData = new GameData();
        private MapData MapData = new MapData();
        private List<KillFeedEntry> KillFeed = new List<KillFeedEntry>();

        private Dictionary<uint, uint> _actorToChannel = new Dictionary<uint, uint>();
        private Dictionary<uint, uint> _channelToActor = new Dictionary<uint, uint>();
        private Dictionary<uint, uint> _pawnChannelToStateChannel = new Dictionary<uint, uint>();

        /// <summary>
        /// Sometimes we receive a PlayerPawn but we havent received the PlayerState yet, so we dont want to processes these yet.
        /// </summary>
        private Dictionary<uint, List<QueuedPlayerPawn>> _queuedPlayerPawns = new Dictionary<uint, List<QueuedPlayerPawn>>();

        private HashSet<uint> _onlySpectatingPlayers = new HashSet<uint>();
        private Dictionary<uint, PlayerData> _players = new Dictionary<uint, PlayerData>();
        private Dictionary<int?, TeamData> _teams = new Dictionary<int?, TeamData>();
        private Dictionary<uint, Llama> _llamas = new Dictionary<uint, Llama>();
        private Dictionary<int, RebootVan> _rebootVans = new Dictionary<int, RebootVan>();
        private Dictionary<uint, Models.SupplyDrop> _drops = new Dictionary<uint, Models.SupplyDrop>();

        private Dictionary<uint, Inventory> _inventories = new Dictionary<uint, Inventory>();
        private Dictionary<uint, WeaponData> _weapons = new Dictionary<uint, WeaponData>();
        private Dictionary<uint, WeaponData> _unknownWeapons = new Dictionary<uint, WeaponData>();

        private float? ReplicatedWorldTimeSeconds = 0;

        public void AddActorChannel(uint channelIndex, uint guid)
        {
            _actorToChannel[guid] = channelIndex;
            _channelToActor[channelIndex] = guid;
        }

        public void RemoveChannel(uint channelIndex, uint guid)
        {
            _weapons.Remove(channelIndex);
            _unknownWeapons.Remove(channelIndex);
        }

        /// <summary>
        /// Once a replay is fully parsed, add the data build over time to the replay.
        /// </summary>
        /// <param name="replay"></param>
        /// <returns>FortniteReplay</returns>
        public FortniteReplay Build(FortniteReplay replay)
        {
            UpdateTeamData();
            replay.GameData = GameData;
            replay.MapData = MapData;
            replay.KillFeed = KillFeed;
            replay.TeamData = _teams.Values;
            replay.PlayerData = _players.Values;
            return replay;
        }

        private bool TryGetPlayerDataFromActor(uint guid, out PlayerData playerData)
        {
            if (_actorToChannel.TryGetValue(guid, out var pawnChannel))
            {
                if (_pawnChannelToStateChannel.TryGetValue(pawnChannel, out var stateChannel))
                {
                    return _players.TryGetValue(stateChannel, out playerData);
                }
            }
            playerData = null;
            return false;
        }

        private bool TryGetPlayerDataFromPawn(uint pawn, out PlayerData playerData)
        {
            if (_pawnChannelToStateChannel.TryGetValue(pawn, out var stateChannel))
            {
                return _players.TryGetValue(stateChannel, out playerData);
            }
            playerData = null;
            return false;
        }

        private void HandleQueuedPlayerPawns(uint stateChannelIndex)
        {
            if (_channelToActor.TryGetValue(stateChannelIndex, out var actorId))
            {
                if (_queuedPlayerPawns.Remove(actorId, out var playerPawns))
                {
                    foreach (var playerPawn in playerPawns)
                    {
                        UpdatePlayerPawn(playerPawn.ChannelId, playerPawn.PlayerPawn);
                    }
                }
            }
        }

        public void UpdateGameState(GameState state)
        {
            GameData.GameSessionId ??= state?.GameSessionId;
            GameData.UtcTimeStartedMatch ??= state.UtcTimeStartedMatch?.Time;
            GameData.MapInfo ??= state.MapInfo?.Name;

            GameData.IsLargeTeamGame ??= state.bIsLargeTeamGame;
            GameData.TournamentRound ??= state.EventTournamentRound;

            GameData.AdditionalPlaylistLevels ??= state.AdditionalPlaylistLevelsStreamed?.Select(i => i.Name);

            GameData.MaxPlayers ??= state.TeamCount;
            GameData.TeamSize ??= state.TeamSize;
            GameData.TeamSize ??= state.ActiveTeamNums?.Length;
            GameData.TotalBots = state.PlayerBotsLeft > GameData.TotalBots ? state.PlayerBotsLeft : GameData.TotalBots;

            GameData.TotalPlayerStructures ??= state.TotalPlayerStructures;

            GameData.AircraftStartTime ??= state.AircraftStartTime;
            GameData.SafeZonesStartTime ??= state.SafeZonesStartTime;

            MapData.BattleBusFlightPaths ??= state.TeamFlightPaths?.Select(i => new BattleBus(i) { Skin = state.DefaultBattleBus?.Name });

            if (state.ReplicatedWorldTimeSeconds != null)
            {
                ReplicatedWorldTimeSeconds = state.ReplicatedWorldTimeSeconds;
            }

            GameData.WinningPlayerIds ??= state.WinningPlayerList?.Select(i => i);
            GameData.WinningTeam ??= state.WinningTeam;
        }

        public void UpdatePlaylistInfo(PlaylistInfo playlist)
        {
            GameData.CurrentPlaylist ??= playlist.Name;
        }

        public void UpdateGameplayModifiers(ActiveGameplayModifier modifier)
        {
            GameData.ActiveGameplayModifiers.Add(modifier.ModifierDef?.Name);
        }

        public void UpdateTeamData()
        {
            foreach (var playerData in _players.Values)
            {
                if (playerData?.TeamIndex == null)
                {
                    continue;
                }

                if (!_teams.TryGetValue(playerData.TeamIndex, out var teamData))
                {
                    _teams[playerData.TeamIndex] = new TeamData()
                    {
                        TeamIndex = playerData.TeamIndex,
                        PlayerIds = new List<int?>() { playerData.Id },
                        PlayerNames = new List<string>() { playerData.PlayerId },
                        Placement = playerData.Placement,
                        PartyOwnerId = playerData.IsPartyLeader ? playerData.Id : null,
                        TeamKills = playerData.TeamKills
                    };
                    continue;
                }

                teamData.Placement ??= playerData.Placement;
                teamData.TeamKills ??= playerData.TeamKills;

                teamData.PlayerIds.Add(playerData.Id);
                teamData.PlayerNames.Add(playerData.PlayerId);
                if (playerData.IsPartyLeader)
                {
                    teamData.PartyOwnerId = playerData.Id;
                }
            }
        }

        public void UpdatePlayerState(uint channelIndex, FortPlayerState state)
        {
            if (state.bOnlySpectator == true)
            {
                _onlySpectatingPlayers.Add(channelIndex);
                return;
            }

            if (_onlySpectatingPlayers.Contains(channelIndex))
            {
                return;
            }

            var isNewPlayer = !_players.TryGetValue(channelIndex, out var playerData);

            if (isNewPlayer)
            {
                playerData = new PlayerData(state);
                _players[channelIndex] = playerData;
            }

            if (state.RebootCounter > 0 && state.RebootCounter > playerData.RebootCounter)
            {
                playerData.RebootCounter = state.RebootCounter;
            }

            if (state.RebootCounter > 0 || state.bDBNO != null || state.DeathCause != null)
            {
                UpdateKillFeed(channelIndex, playerData, state);
            }

            if (state.Ping > 0)
            {
                // workaround
                playerData.IsReplayOwner = true;
            }

            if (state.TeamIndex > 0)
            {
                playerData.TeamIndex = state.TeamIndex;
            }
            playerData.Placement ??= state.Place;
            playerData.TeamKills = state.TeamKillScore ?? playerData.TeamKills;
            playerData.Kills = state.KillScore ?? playerData.Kills;
            playerData.HasThankedBusDriver ??= state.bThankedBusDriver;
            playerData.Disconnected ??= state.bIsDisconnected;

            playerData.DeathCause ??= state.DeathCause;
            playerData.DeathLocation ??= state.DeathLocation;
            playerData.DeathCircumstance ??= state.DeathCircumstance;
            playerData.DeathTags ??= state.DeathTags?.Tags?.Select(i => i.TagName);

            if (state.DeathTags != null)
            {
                playerData.DeathTime = ReplicatedWorldTimeSeconds;
            }

            playerData.Cosmetics.Parts ??= state.Parts?.Name;
            playerData.Cosmetics.VariantRequiredCharacterParts ??= state.VariantRequiredCharacterParts?.Select(i => i.Name);


            if (isNewPlayer)
            {
                HandleQueuedPlayerPawns(channelIndex);
            }
        }

        public void UpdateKillFeed(uint channelIndex, PlayerData data, FortPlayerState state)
        {
            var entry = new KillFeedEntry()
            {
                ReplicatedWorldTimeSeconds = ReplicatedWorldTimeSeconds
            };

            if (state.RebootCounter != null)
            {
                entry.IsRevived = true;
            }

            if (state.bDBNO == true)
            {
                entry.IsDowned = true;
            }

            if (_actorToChannel.TryGetValue(state.FinisherOrDowner.GetValueOrDefault(), out var actorChannelIndex))
            {
                if (_players.TryGetValue(actorChannelIndex, out var finisherOrDownerData))
                {
                    entry.FinisherOrDowner = finisherOrDownerData.Id;
                    entry.FinisherOrDownerName = finisherOrDownerData.PlayerId;
                    entry.FinisherOrDownerIsBot = finisherOrDownerData.IsBot == true;
                }
            }

            entry.PlayerId = data.Id;
            entry.PlayerName = data.PlayerId;
            entry.PlayerIsBot = data.IsBot == true;

            entry.Distance ??= state.Distance;
            entry.DeathCause ??= state.DeathCause;
            entry.DeathLocation ??= state.DeathLocation;
            entry.DeathCircumstance ??= state.DeathCircumstance;
            entry.DeathTags ??= state.DeathTags?.Tags?.Select(i => i.TagName);

            KillFeed.Add(entry);
        }

        public void UpdatePlayerPawn(uint channelIndex, PlayerPawn pawn)
        {
            if (!_pawnChannelToStateChannel.TryGetValue(channelIndex, out var stateChannelIndex))
            {
                if (pawn.PlayerState == null)
                {
                    return;
                }

                var actorId = pawn.PlayerState.Value;
                if (_actorToChannel.TryGetValue(actorId, out stateChannelIndex))
                {
                    _pawnChannelToStateChannel[channelIndex] = stateChannelIndex;
                }
                else
                {
                    if (!_queuedPlayerPawns.TryGetValue(actorId, out var playerPawns))
                    {
                        playerPawns = new List<QueuedPlayerPawn>();
                        _queuedPlayerPawns[actorId] = playerPawns;
                    }

                    playerPawns.Add(new QueuedPlayerPawn
                    {
                        ChannelId = channelIndex,
                        PlayerPawn = pawn
                    });

                    return;
                }
            }

            var playerState = _players[stateChannelIndex];

            playerState.Cosmetics.Character ??= pawn.Character?.Name;
            playerState.Cosmetics.BannerColorId ??= pawn.BannerColorId;
            playerState.Cosmetics.BannerIconId ??= pawn.BannerIconId;
            playerState.Cosmetics.IsDefaultCharacter ??= pawn.bIsDefaultCharacter;
            playerState.Cosmetics.Backpack ??= pawn.Backpack?.Name;
            playerState.Cosmetics.PetSkin ??= pawn.PetSkin?.Name;
            playerState.Cosmetics.Glider ??= pawn.Glider?.Name;
            playerState.Cosmetics.LoadingScreen ??= pawn.LoadingScreen?.Name;
            playerState.Cosmetics.MusicPack ??= pawn.MusicPack?.Name;
            playerState.Cosmetics.Pickaxe ??= pawn.Pickaxe?.Name;
            playerState.Cosmetics.SkyDiveContrail ??= pawn.SkyDiveContrail?.Name;
            playerState.Cosmetics.Dances ??= pawn.Dances?.Select(i => i.Name);
            playerState.Cosmetics.ItemWraps ??= pawn.ItemWraps?.Select(i => i.Name);

            if (pawn.CurrentWeapon != null)
            {
                playerState.CurrentWeapon = pawn.CurrentWeapon;
            }
        }

        public void UpdateInventory(uint channelIndex, FortInventory fortInventory)
        {
            if (!_inventories.TryGetValue(channelIndex, out var inventory))
            {
                // TODO updates for unknown parent inventory !?
                // TODO receive inventory for some random channel without replaypawn...?
                if (!fortInventory.ReplayPawn.HasValue)
                {
                    return;
                }

                inventory = new Inventory()
                {
                    Id = channelIndex,
                    ReplayPawn = fortInventory.ReplayPawn
                };
                _inventories[channelIndex] = inventory;
            }

            if (fortInventory.ReplayPawn > 0)
            {
                inventory.ReplayPawn = fortInventory.ReplayPawn;
            }

            if (!inventory.PlayerId.HasValue)
            {
                if (TryGetPlayerDataFromActor(inventory.ReplayPawn.GetValueOrDefault(), out var playerData))
                {
                    inventory.PlayerId = playerData.Id;
                    inventory.PlayerName = playerData.PlayerId;
                    playerData.InventoryId = inventory.Id;
                }
            }

            if (!fortInventory.A.HasValue)
            {
                return;
            }

            var inventoryItem = new InventoryItem()
            {
                Count = fortInventory.Count,
                ItemDefinition = fortInventory.ItemDefinition?.Name,
                OrderIndex = fortInventory.OrderIndex,
                Durability = fortInventory.Durability,
                Level = fortInventory.Level,
                LoadedAmmo = fortInventory.LoadedAmmo,
                A = fortInventory.A,
                B = fortInventory.B,
                C = fortInventory.C,
                D = fortInventory.D
            };
            inventory.Items.Add(inventoryItem);
        }

        public void UpdateWeapon(uint channelIndex, BaseWeapon weapon)
        {
            if (!_weapons.TryGetValue(channelIndex, out var newWeapon))
            {
                if (!_unknownWeapons.TryGetValue(channelIndex, out newWeapon))
                {
                    newWeapon = new WeaponData();
                    _weapons[channelIndex] = newWeapon;
                }
                else
                {
                    _unknownWeapons.Remove(channelIndex);
                }
            }

            newWeapon.bIsEquippingWeapon ??= weapon.bIsEquippingWeapon;
            newWeapon.bIsReloadingWeapon ??= weapon.bIsReloadingWeapon;
            newWeapon.WeaponLevel ??= weapon.WeaponLevel;
            newWeapon.AmmoCount ??= weapon.AmmoCount;
            newWeapon.LastFireTimeVerified ??= weapon.LastFireTimeVerified;
            newWeapon.A ??= weapon.A;
            newWeapon.B ??= weapon.B;
            newWeapon.C ??= weapon.C;
            newWeapon.D ??= weapon.D;
            newWeapon.WeaponName ??= weapon.WeaponData?.Name;
        }

        public void UpdateSafeZones(SafeZoneIndicator safeZone)
        {
            if (safeZone.SafeZoneStartShrinkTime <= 0 && safeZone.SafeZoneFinishShrinkTime <= 0)
            {
                return;
            }

            MapData.SafeZones.Add(new SafeZone(safeZone));
        }

        public void UpdateLlama(uint channelIndex, SupplyDropLlama supplyDropLlama)
        {
            if (!_llamas.TryGetValue(channelIndex, out var llama))
            {
                llama = new Llama(channelIndex, supplyDropLlama);
                MapData.Llamas.Add(llama);
                _llamas.Add(channelIndex, llama);
                return;
            }

            if (supplyDropLlama.Looted)
            {
                llama.Looted = true;
                llama.LootedTime = ReplicatedWorldTimeSeconds;
            }

            if (supplyDropLlama.bHasSpawnedPickups)
            {
                llama.HasSpawnedPickups = true;
            }
        }

        public void UpdateSupplyDrop(uint channelIndex, Models.NetFieldExports.SupplyDrop supplyDrop)
        {
            if (!_drops.TryGetValue(channelIndex, out var drop))
            {
                drop = new Models.SupplyDrop(channelIndex, supplyDrop);
                MapData.SupplyDrops.Add(drop);
                _drops.Add(channelIndex, drop);
                return;
            }

            if (supplyDrop.Opened)
            {
                drop.Looted = true;
                drop.LootedTime = ReplicatedWorldTimeSeconds;
            }

            if (supplyDrop.BalloonPopped)
            {
                drop.BalloonPopped = true;
                drop.BalloonPoppedTime = ReplicatedWorldTimeSeconds;
            }

            if (supplyDrop.bHasSpawnedPickups)
            {
                drop.HasSpawnedPickups = true;
            }

            if (supplyDrop.LandingLocation != null)
            {
                drop.LandingLocation = supplyDrop.LandingLocation;
            }
        }

        public void UpdateRebootVan(uint channelIndex, SpawnMachineRepData spawnMachine)
        {
            if (!_rebootVans.TryGetValue(spawnMachine.SpawnMachineRepDataHandle, out var rebootVan))
            {
                rebootVan = new RebootVan(spawnMachine);
                MapData.RebootVans.Add(rebootVan);
                _rebootVans.Add(spawnMachine.SpawnMachineRepDataHandle, rebootVan);
                return;
            }
        }

        //public void UpdateExplosion(BroadcastExplosion explosion)
        //{
        //    // ¯\_(ツ)_/¯
        //}

        public void UpdatePoiManager(FortPoiManager poiManager)
        {
            MapData.GridCountX ??= poiManager.GridCountX;
            MapData.GridCountY ??= poiManager.GridCountY;
            MapData.WorldGridStart ??= poiManager.WorldGridStart;
            MapData.WorldGridEnd ??= poiManager.WorldGridEnd;
            MapData.WorldGridSpacing ??= poiManager.WorldGridSpacing;
            MapData.WorldGridTotalSize ??= poiManager.WorldGridTotalSize;

            // ignore PoiTagContainerTable since it is just a list of all POI...
        }

        //public void UpdateGameplayCue(uint channelIndex, GameplayCue gameplayCue)
        //{
        //    // ¯\_(ツ)_/¯
        //}
    }
}
