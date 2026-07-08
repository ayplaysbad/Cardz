using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastFreeCity.Gameplay
{
    [Serializable]
    public class BoardTileDefinitionData
    {
        public string tileId = "tile_0_0";
        [Min(0)] public int row;
        [Min(0)] public int column;
        public TileOwner owner = TileOwner.Neutral;
        public TileAreaKind areaKind = TileAreaKind.Freeplay;
        [Min(0)] public int maxHealth;
        public bool blocksCityUntilDestroyed = false;
        public bool becomesNeutralWhenDestroyed = true;
    }

    [CreateAssetMenu(fileName = "NewBoardLayout", menuName = "Last Free City/Board Layout")]
    public class BoardLayoutDefinition : ScriptableObject
    {
        [Header("Dimensions")]
        [Min(1)] public int rows = 6;
        [Min(1)] public int columns = 4;

        [Header("Perspective")]
        public MatchSeat canonicalTopSeat = MatchSeat.SeatOne;

        [Header("Tiles")]
        public List<BoardTileDefinitionData> tiles = new List<BoardTileDefinitionData>();

        public int TileCount => rows * columns;
    }
}
