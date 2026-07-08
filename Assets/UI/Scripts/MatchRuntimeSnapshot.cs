using System;
using System.Collections.Generic;
using LastFreeCity.UI;
using UnityEngine;

namespace LastFreeCity.Gameplay
{
    public enum MatchRoundPhaseSnapshot
    {
        DeployPlanning,
        CombatPlanning,
        DisplayResolution
    }

    public enum DisplayResolutionModeSnapshot
    {
        Movement,
        Attack
    }

    [Serializable]
    public class CardRuntimeSnapshot
    {
        public string cardId;
        public string cardName;
        public int treasuryCost;
        public CardType cardType;
        public int health;
        public int attack;
        public int range;
        public int movementRange;
        public UnitTag unitTag;
        public InfrastructureKind infrastructureKind;
        public CommandCardKind commandCardKind;
        public string abilityText;
        public string detailedAbilityText;
        public List<AbilityEffectRuntimeSnapshot> keywordEffects = new List<AbilityEffectRuntimeSnapshot>();
        public CardRuntimeSnapshot attachedItemCard;
        public int bonusHealth;
        public int bonusAttack;
        public int bonusRange;
        public int bonusMovementRange;
        public int bonusSiegeAttack;
    }

    [Serializable]
    public class AbilityEffectRuntimeSnapshot
    {
        public AbilityKeyword keyword = AbilityKeyword.None;
        public int value;
        public AbilityTrigger trigger = AbilityTrigger.Instant;
        public AbilityDuration duration = AbilityDuration.Instant;
        public int durationTurns;
        public AbilityTargetScope targetScope = AbilityTargetScope.None;
        public CardType targetCardType = CardType.Unit;
        public UnitTag targetUnitTag = UnitTag.None;
        public InfrastructureKind targetInfrastructureKind = InfrastructureKind.None;
        public int range = 1;
        public string shortDescription;
        public string detailedDescription;
    }

    [Serializable]
    public class ParticipantRuntimeSnapshot
    {
        public MatchSeat seat;
        public string cityName;
        public int health;
        public int treasury;
        public int turnStartDrawCount;
        public int maxHandSize = 6;
        public int baseTreasuryIncome = 6;
        public int deployTurnsTaken;
        public List<CardRuntimeSnapshot> hand = new List<CardRuntimeSnapshot>();
        public List<CardRuntimeSnapshot> drawPile = new List<CardRuntimeSnapshot>();
        public List<CardRuntimeSnapshot> discardPile = new List<CardRuntimeSnapshot>();
        public List<CardRuntimeSnapshot> burnPile = new List<CardRuntimeSnapshot>();
    }

    [Serializable]
    public class TileRuntimeSnapshot
    {
        public TileOwner owner;
        public TileAreaKind areaKind;
        public int currentHealth;
        public int maxHealth;
        public bool blocksCity;
        public bool locked;
        public int secureHoldTurns;
        public int silenceTurns;
        public int spawnChargeTurns;
        public int attackTargetTileIndex = -1;
        public int moveTargetTileIndex = -1;
        public bool hasOccupant;
        public MatchSeat occupantSeat = MatchSeat.SeatOne;
        public int occupantCurrentHealth;
        public CardRuntimeSnapshot occupantCard;
    }

    [Serializable]
    public class FloatingBoardTextSnapshot
    {
        public int tileIndex = -1;
        public string text;
        public string cssClass = "tile-floating-damage";
        public float secondsRemaining;
    }

    [Serializable]
    public class MatchRuntimeSnapshot
    {
        public int rows;
        public int columns;
        public MatchSeat canonicalTopSeat;
        public MatchSeat localSeat;
        public MatchControlMode controlMode;
        public MatchSeat activeTurnSeat;
        public MatchSeat roundInitiativeSeat;
        public int roundNumber;
        public bool arenaSelectionActive;
        public ArenaId selectedArena = ArenaId.None;
        public ArenaId seatOneArenaVote = ArenaId.None;
        public ArenaId seatTwoArenaVote = ArenaId.None;
        public float arenaSelectionCountdownRemaining = -1f;
        public bool matchEnded;
        public MatchSeat winningSeat = MatchSeat.SeatOne;
        public string matchEndMessage;
        public MatchRoundPhaseSnapshot roundPhase;
        public float phaseSecondsRemaining = -1f;
        public bool hotseatTestMode;
        public int highlightedCardIndex = -1;
        public int selectedBoardTileIndex = -1;
        public int selectedAttackerTileIndex = -1;
        public int selectedWarShopOption = -1;
        public bool activeTurnWarShopPurchaseUsed;
        public DisplayResolutionModeSnapshot displayResolutionMode = DisplayResolutionModeSnapshot.Attack;
        public string displayStageLabel;
        public bool hasDisplayStageSeat;
        public MatchSeat displayStageSeat = MatchSeat.SeatOne;
        public string displayNarrationText;
        public string awarenessOverrideText;
        public float awarenessOverrideSecondsRemaining = -1f;
        public ParticipantRuntimeSnapshot seatOne;
        public ParticipantRuntimeSnapshot seatTwo;
        public List<TileRuntimeSnapshot> tiles = new List<TileRuntimeSnapshot>();
        public List<FloatingBoardTextSnapshot> floatingBoardTexts = new List<FloatingBoardTextSnapshot>();
    }

    [Serializable]
    public class MatchTimerSyncSnapshot
    {
        public int roundNumber;
        public MatchSeat activeTurnSeat;
        public MatchRoundPhaseSnapshot roundPhase;
        public DisplayResolutionModeSnapshot displayResolutionMode = DisplayResolutionModeSnapshot.Attack;
        public double serverTimeSeconds = -1d;
        public double phaseEndsAtServerTime = -1d;
        public float phaseSecondsRemaining = -1f;
        public bool arenaSelectionActive;
        public ArenaId seatOneArenaVote = ArenaId.None;
        public ArenaId seatTwoArenaVote = ArenaId.None;
        public double arenaResolveAtServerTime = -1d;
        public float arenaSelectionCountdownRemaining = -1f;
    }
}
