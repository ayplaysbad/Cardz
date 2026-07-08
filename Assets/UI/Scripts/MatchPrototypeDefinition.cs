using System;
using System.Collections.Generic;
using LastFreeCity.UI;
using UnityEngine;

namespace LastFreeCity.Gameplay
{
    [Serializable]
    public class MatchParticipantDefinition
    {
        public MatchSeat seat = MatchSeat.SeatOne;
        public CityDefinition city;
        public DeckDefinition deck;
        public int startingHealthOverride = -1;
        public int startingTreasuryOverride = -1;
        public int openingHandSize = 1;
        public int turnStartDrawCount = 1;
        public int maxHandSize = 6;
        public int baseTreasuryIncome = 6;
        public List<CardTemplate> openingHand = new List<CardTemplate>();
    }

    [Serializable]
    public class StartingCardPlacement
    {
        public int row;
        public int column;
        public CardTemplate card;
    }

    [CreateAssetMenu(fileName = "NewMatchPrototype", menuName = "Last Free City/Match Prototype")]
    public class MatchPrototypeDefinition : ScriptableObject
    {
        [Header("Perspective")]
        public MatchSeat localSeat = MatchSeat.SeatOne;
        public bool hotseatTestMode = true;
        public MatchControlMode defaultControlMode = MatchControlMode.Hotseat;
        public MatchSeat startingTurn = MatchSeat.SeatOne;

        [Header("Board")]
        public BoardLayoutDefinition boardLayout;

        [Header("Participants")]
        public MatchParticipantDefinition seatOne = new MatchParticipantDefinition { seat = MatchSeat.SeatOne };
        public MatchParticipantDefinition seatTwo = new MatchParticipantDefinition { seat = MatchSeat.SeatTwo };

        [Header("Preview Placements")]
        public List<StartingCardPlacement> startingCardPlacements = new List<StartingCardPlacement>();

        public MatchParticipantDefinition GetParticipant(MatchSeat seat)
        {
            return seat == MatchSeat.SeatOne ? seatOne : seatTwo;
        }
    }
}
