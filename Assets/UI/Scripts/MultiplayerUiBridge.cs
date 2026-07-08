using System;

namespace LastFreeCity.Gameplay
{
    public enum MatchUiActionType
    {
        None,
        ToggleHandCard,
        BoardTilePointerUp,
        TargetCity,
        EndTurn,
        ClearSelection,
        SelectWarShopOption,
        ChooseArena,
        BackToMenu
    }

    [Serializable]
    public struct MatchUiAction
    {
        public MatchUiActionType actionType;
        public int handIndex;
        public int tileIndex;
        public MatchSeat targetSeat;
        public int clickCount;
        public ArenaId arenaId;
    }

    public interface IMatchUiCommandSink
    {
        bool TryHandleUiAction(MatchUiAction action);
    }
}
