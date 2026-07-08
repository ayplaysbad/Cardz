using UnityEngine;

namespace LastFreeCity.Gameplay
{
    public enum MatchControlMode
    {
        Hotseat,
        SeatAssigned
    }

    public enum MatchLaunchMode
    {
        None,
        TurnBased,
        Testing,
        OnlineQuickMatch,
        MultiplayerHost,
        MultiplayerClient,
        DedicatedServer
    }

    public enum ArenaId
    {
        None,
        FreehavenGarden,
        CitadelTrainingGrounds
    }

    public enum OnlineConnectionState
    {
        Offline,
        Authenticating,
        ChoosingCity,
        FindingMatch,
        WaitingForOpponent,
        ChoosingArena,
        InMatch,
        Reconnecting,
        Failed
    }

    public enum MatchSeat
    {
        SeatOne,
        SeatTwo
    }

    public enum TileOwner
    {
        Neutral,
        SeatOne,
        SeatTwo
    }

    public enum TileAreaKind
    {
        Base,
        Freeplay
    }

    public static class MatchPerspectiveUtility
    {
        public static MatchSeat GetOpposingSeat(MatchSeat seat)
        {
            return seat == MatchSeat.SeatOne ? MatchSeat.SeatTwo : MatchSeat.SeatOne;
        }

        public static bool ShouldFlipRows(MatchSeat canonicalTopSeat, MatchSeat localSeat)
        {
            // Canonical layout is stored from a fixed seat perspective.
            // When the local player is the canonical top seat, flip rows so they still render on the bottom.
            return canonicalTopSeat == localSeat;
        }

        public static bool IsLocalOwned(TileOwner owner, MatchSeat localSeat)
        {
            return (owner == TileOwner.SeatOne && localSeat == MatchSeat.SeatOne)
                || (owner == TileOwner.SeatTwo && localSeat == MatchSeat.SeatTwo);
        }

        public static bool IsRemoteOwned(TileOwner owner, MatchSeat localSeat)
        {
            if (owner == TileOwner.Neutral)
            {
                return false;
            }

            return !IsLocalOwned(owner, localSeat);
        }
    }
}
