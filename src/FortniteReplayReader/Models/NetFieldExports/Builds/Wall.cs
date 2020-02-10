﻿using Unreal.Core.Attributes;
using Unreal.Core.Models;
using Unreal.Core.Models.Enums;

namespace FortniteReplayReader.Models.NetFieldExports.Vehicles
{
    [NetFieldExportGroup("/Game/Building/ActorBlueprints/Player/Wood/L1/PBWA_W1_Solid.PBWA_W1_Solid_C", minimalParseMode: ParseMode.Debug)]
    public class WoodWall : BaseBuild
    {
    }
    
    [NetFieldExportGroup("/Game/Building/ActorBlueprints/Player/Stone/L1/PBWA_S1_Solid.PBWA_S1_Solid_C", minimalParseMode: ParseMode.Debug)]
    public class StoneWall : BaseBuild
    {
    }

    [NetFieldExportGroup("/Game/Building/ActorBlueprints/Player/Metal/L1/PBWA_M1_Solid.PBWA_M1_Solid_C", minimalParseMode: ParseMode.Debug)]
    public class MetalWall : BaseBuild
    {
    }
}