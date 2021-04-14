﻿using FortniteReplayReader.Models.Enums;
using Unreal.Core.Models;

namespace FortniteReplayReader.Models.Events
{
    public class PlayerEliminationInfo
    {
        public string Id { get; internal set; }
        public PlayerTypes PlayerType { get; internal set; }
        public FVector Unknown1 { get; internal set; }
        public FVector Unknown2 { get; internal set; }
        public FVector Location { get; internal set; }
        public bool IsBot => PlayerType == PlayerTypes.BOT || PlayerType == PlayerTypes.NAMED_BOT;
    }
}