using System;

namespace FortniteReplayReader.Models.Events
{
    public class PlayerElimination : BaseEvent, IEquatable<PlayerElimination>
    {
        public PlayerEliminationInfo EliminatedInfo { get; internal set; }
        public PlayerEliminationInfo EliminatorInfo { get; internal set; }

        public string Eliminated => EliminatedInfo?.Id;
        public string Eliminator => EliminatedInfo?.Id;
        public byte GunType { get; internal set; }
        public string Time { get; internal set; }
        public bool Knocked { get; internal set; }
        public bool IsSelfElimination => Eliminated == Eliminator;
        public bool IsValidLocation => EliminatorInfo.Location.Size() != 0;
        public double? Distance => IsValidLocation ? EliminatorInfo.Location.DistanceTo(EliminatedInfo.Location) : null;

        public bool Equals(PlayerElimination other)
        {
            if (other.Equals(null))
            {
                return false;
            }

            if (Eliminated == other.Eliminated && Eliminator == other.Eliminator && GunType == other.GunType && Time == other.Time && Knocked == other.Knocked)
            {
                return true;
            }

            return false;
        }
    }
}