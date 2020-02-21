﻿using FortniteReplayReader.Exceptions;
using FortniteReplayReader.Extensions;
using FortniteReplayReader.Models;
using FortniteReplayReader.Models.Events;
using FortniteReplayReader.Models.NetFieldExports;
using FortniteReplayReader.Models.NetFieldExports.RPC;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Unreal.Core;
using Unreal.Core.Contracts;
using Unreal.Core.Exceptions;
using Unreal.Core.Models;
using Unreal.Core.Models.Enums;

namespace FortniteReplayReader
{
    public class ReplayReader : Unreal.Core.ReplayReader<FortniteReplay>
    {
        private FortniteReplayBuilder Builder;

        public ReplayReader(ILogger logger = null) : base(logger)
        {
        }

        public FortniteReplay ReadReplay(string fileName, ParseMode mode = ParseMode.Minimal)
        {
            using var stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var archive = new Unreal.Core.BinaryReader(stream);

            Builder = new FortniteReplayBuilder();
            ReadReplay(archive, mode);

            Builder.UpdateTeamData();
            return Replay;
        }

        public FortniteReplay ReadReplay(Stream stream, ParseMode mode = ParseMode.Minimal)
        {
            using var archive = new Unreal.Core.BinaryReader(stream);
            Replay = ReadReplay(archive, mode);

            Builder.UpdateTeamData();
            return Replay;
        }

        private string _branch;
        public int Major { get; set; }
        public int Minor { get; set; }
        public string Branch
        {
            get => _branch;
            set
            {
                var regex = new Regex(@"\+\+Fortnite\+Release\-(?<major>\d+)\.(?<minor>\d*)");
                var result = regex.Match(value);
                if (result.Success)
                {
                    Major = int.Parse(result.Groups["major"]?.Value ?? "0");
                    Minor = int.Parse(result.Groups["minor"]?.Value ?? "0");
                }
                _branch = value;
            }
        }

        protected override void OnChannelOpened(uint channelIndex, NetworkGUID actor)
        {
            if (actor != null)
            {
                Builder.AddActorChannel(channelIndex, actor.Value);
            }
        }

        protected override void OnChannelClosed(uint channelIndex, NetworkGUID actor)
        {
            if (actor != null)
            {
                Builder.RemoveChannel(channelIndex, actor.Value);
            }
        }

        protected override void OnExportRead(uint channelIndex, INetFieldExportGroup exportGroup)
        {
            _logger?.LogDebug($"Received data for group {exportGroup.GetType().Name}");

            switch (exportGroup)
            {
                case GameState state:
                    Builder.UpdateGameState(state);
                    Builder.CreateGameStateEvent(state);
                    break;
                case PlaylistInfo playlist:
                    Builder.UpdatePlaylistInfo(playlist);
                    break;
                case ActiveGameplayModifier modifier:
                    Builder.UpdateGameplayModifiers(modifier);
                    break;
                case FortPlayerState state:
                    Builder.UpdatePlayerState(channelIndex, state);
                    break;
                case PlayerPawn pawn:
                    Builder.UpdatePlayerPawn(channelIndex, pawn);
                    Builder.CreatePawnEvent(channelIndex, pawn);
                    break;
                case FortPickup pickup:
                    //Builder.CreatePickupEvent(channelIndex, pickup);
                    break;
                case FortInventory inventory:
                    Builder.UpdateInventory(channelIndex, inventory);
                    break;
                case BatchedDamageCues damage:
                    Builder.UpdateBatchedDamge(channelIndex, damage);
                    break;
                case HealthSet healthSet:
                    Builder.CreateHealthEvent(channelIndex, healthSet);
                    break;
                //case BroadcastExplosion explosion:
                //    Builder.UpdateExplosion(explosion);
                //    break;
                case SafeZoneIndicator safeZone:
                    Builder.UpdateSafeZones(safeZone);
                    break;
                case SupplyDropLlama llama:
                    Builder.UpdateLlama(channelIndex, llama);
                    Builder.CreateLlamaEvent(channelIndex, llama);
                    break;
                case SpawnMachineRepData spawnMachine:
                    Builder.UpdateRebootVan(channelIndex, spawnMachine);
                    Builder.CreateRebootVanEvent(channelIndex, spawnMachine);
                    break;
                case Models.NetFieldExports.SupplyDrop drop:
                    Builder.UpdateSupplyDrop(channelIndex, drop);
                    Builder.CreateSupplyDropEvent(channelIndex, drop);
                    break;
                case FortPoiManager poimanager:
                    Builder.UpdatePoiManager(poimanager);
                    break;
                    //case GameplayCue gameplayCue:
                    //    Builder.UpdateGameplayCue(channelIndex, gameplayCue);
                    //    break;
            }
        }

        public override void ReadReplayHeader(FArchive archive)
        {
            base.ReadReplayHeader(archive);
            Branch = Replay.Header.Branch;
        }

        /// <summary>
        /// see https://github.com/EpicGames/UnrealEngine/blob/70bc980c6361d9a7d23f6d23ffe322a2d6ef16fb/Engine/Source/Runtime/NetworkReplayStreaming/LocalFileNetworkReplayStreaming/Private/LocalFileNetworkReplayStreaming.cpp#L363
        /// </summary>
        /// <param name="archive"></param>
        /// <returns></returns>
        public override void ReadEvent(FArchive archive)
        {
            var info = new EventInfo
            {
                Id = archive.ReadFString(),
                Group = archive.ReadFString(),
                Metadata = archive.ReadFString(),
                StartTime = archive.ReadUInt32(),
                EndTime = archive.ReadUInt32(),
                SizeInBytes = archive.ReadInt32()
            };

            _logger?.LogDebug($"Encountered event {info.Group} ({info.Metadata}) at {info.StartTime} of size {info.SizeInBytes}");

            using var decryptedArchive = DecryptBuffer(archive, info.SizeInBytes);

            // Every event seems to start with some unknown int
            if (info.Group == ReplayEventTypes.PLAYER_ELIMINATION)
            {
                var elimination = ParseElimination(decryptedArchive, info);
                Replay.Eliminations.Add(elimination);
                return;
            }

            else if (info.Metadata == ReplayEventTypes.MATCH_STATS)
            {
                Replay.Stats = ParseMatchStats(decryptedArchive, info);
                return;
            }

            else if (info.Metadata == ReplayEventTypes.TEAM_STATS)
            {
                Replay.TeamStats = ParseTeamStats(decryptedArchive, info);
                return;
            }

            else if (info.Metadata == ReplayEventTypes.ENCRYPTION_KEY)
            {
                ParseEncryptionKeyEvent(decryptedArchive, info);
                return;
            }

            _logger?.LogInformation($"Unknown event {info.Group} ({info.Metadata}) of size {info.SizeInBytes}");
            if (IsDebugMode)
            {
                throw new UnknownEventException($"Unknown event {info.Group} ({info.Metadata}) of size {info.SizeInBytes}");
            }
        }

        public virtual EncryptionKey ParseEncryptionKeyEvent(FArchive archive, EventInfo info)
        {
            return new EncryptionKey()
            {
                Info = info,
                Key = archive.ReadBytesToString(32)
            };
        }

        public virtual TeamStats ParseTeamStats(FArchive archive, EventInfo info)
        {
            return new TeamStats()
            {
                Info = info,
                Unknown = archive.ReadUInt32(),
                Position = archive.ReadUInt32(),
                TotalPlayers = archive.ReadUInt32()
            };
        }

        public virtual Stats ParseMatchStats(FArchive archive, EventInfo info)
        {
            return new Stats()
            {
                Info = info,
                Unknown = archive.ReadUInt32(),
                Accuracy = archive.ReadSingle(),
                Assists = archive.ReadUInt32(),
                Eliminations = archive.ReadUInt32(),
                WeaponDamage = archive.ReadUInt32(),
                OtherDamage = archive.ReadUInt32(),
                Revives = archive.ReadUInt32(),
                DamageTaken = archive.ReadUInt32(),
                DamageToStructures = archive.ReadUInt32(),
                MaterialsGathered = archive.ReadUInt32(),
                MaterialsUsed = archive.ReadUInt32(),
                TotalTraveled = archive.ReadUInt32()
            };
        }

        public virtual PlayerElimination ParseElimination(FArchive archive, EventInfo info)
        {
            try
            {
                var elim = new PlayerElimination
                {
                    Info = info,
                };

                if (archive.EngineNetworkVersion >= EngineNetworkVersionHistory.HISTORY_FAST_ARRAY_DELTA_STRUCT && Major >= 9)
                {
                    archive.SkipBytes(85);
                    elim.Eliminated = ParsePlayer(archive);
                    elim.Eliminator = ParsePlayer(archive);
                }
                else
                {
                    if (Major <= 4 && Minor < 2)
                    {
                        archive.SkipBytes(12);
                    }
                    else if (Major == 4 && Minor <= 2)
                    {
                        archive.SkipBytes(40);
                    }
                    else
                    {
                        archive.SkipBytes(45);
                    }
                    elim.Eliminated = archive.ReadFString();
                    elim.Eliminator = archive.ReadFString();
                }

                elim.GunType = archive.ReadByte();
                elim.Knocked = archive.ReadUInt32AsBoolean();
                elim.Time = info?.StartTime.MillisecondsToTimeStamp();
                return elim;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error while parsing PlayerElimination at timestamp {info.StartTime}");
                throw new PlayerEliminationException($"Error while parsing PlayerElimination at timestamp {info?.StartTime}", ex);
            }
        }

        public virtual string ParsePlayer(FArchive archive)
        {
            // TODO player type enum
            var botIndicator = archive.ReadByte();
            if (botIndicator == 0x03)
            {
                return "Bot";
            }
            else if (botIndicator == 0x10)
            {
                return archive.ReadFString();
            }

            // 0x11
            var size = archive.ReadByte();
            return archive.ReadGUID(size);
        }

        protected override Unreal.Core.BinaryReader DecryptBuffer(FArchive archive, int size)
        {
            if (!Replay.Info.IsEncrypted)
            {
                return archive as Unreal.Core.BinaryReader;
            }

            var key = Replay.Info.EncryptionKey;
            var encryptedBytes = archive.ReadBytes(size);

            using var rDel = new RijndaelManaged
            {
                KeySize = (key.Length * 8),
                Key = key,
                Mode = CipherMode.ECB,
                Padding = PaddingMode.PKCS7
            };

            using var cryptoTransform = rDel.CreateDecryptor();
            var decryptedArray = cryptoTransform.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

            var decrypted = new Unreal.Core.BinaryReader(new MemoryStream(decryptedArray))
            {
                EngineNetworkVersion = archive.EngineNetworkVersion,
                NetworkVersion = archive.NetworkVersion,
                ReplayHeaderFlags = archive.ReplayHeaderFlags,
                ReplayVersion = archive.ReplayVersion
            };

            return decrypted;
        }
    }
}
