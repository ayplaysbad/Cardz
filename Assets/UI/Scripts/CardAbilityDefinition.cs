using UnityEngine;

namespace LastFreeCity.UI
{
    public enum AbilityKeyword
    {
        None,
        Gather,
        Siphon,
        Discount,
        Strike,
        Shatter,
        Breach,
        Intercept,
        Secure,
        Reclaim,
        Sprint,
        Maneuver,
        Provoke,
        Lock,
        Silence,
        Garrison,
        Spawn,
        Burn,
        Salvage
    }

    public enum AbilityTrigger
    {
        None,
        Instant,
        RoundStart,
        DeployPhaseStart,
        AttackPhase,
        BeforeDamage,
        AfterDamage,
        OnMove,
        OnDeath,
        PassiveAura
    }

    public enum AbilityDuration
    {
        Instant,
        ThisPhase,
        ThisRound,
        Turns,
        WhileSourceStands,
        Permanent
    }

    public enum AbilityTargetScope
    {
        None,
        Self,
        FriendlyUnit,
        EnemyUnit,
        FriendlyInfrastructure,
        EnemyInfrastructure,
        FriendlyBaseTile,
        EnemyBaseTile,
        FreespaceTile,
        AdjacentTile,
        Lane,
        Row,
        City,
        CardInHand,
        CardInDiscard
    }

    [System.Serializable]
    public class AbilityEffectData
    {
        [Header("Keyword")]
        public AbilityKeyword keyword = AbilityKeyword.None;
        public int value = 0;

        [Header("Timing")]
        public AbilityTrigger trigger = AbilityTrigger.Instant;
        public AbilityDuration duration = AbilityDuration.Instant;
        public int durationTurns = 0;

        [Header("Targeting")]
        public AbilityTargetScope targetScope = AbilityTargetScope.None;
        public CardType targetCardType = CardType.Unit;
        public UnitTag targetUnitTag = UnitTag.None;
        public InfrastructureKind targetInfrastructureKind = InfrastructureKind.None;
        public int range = 1;

        [Header("Card Keyword Payloads")]
        public CardTemplate spawnedCard;

        [Header("Rules Copy")]
        [TextArea(1, 3)]
        public string shortDescription = string.Empty;
        [TextArea(3, 8)]
        public string detailedDescription = string.Empty;

        public string GetShortDescription()
        {
            if (!string.IsNullOrWhiteSpace(shortDescription))
            {
                return shortDescription.Trim();
            }

            return FormatDefaultDescription();
        }

        public string GetDetailedDescription(bool appliedToCurrentCard = false)
        {
            string detail = !string.IsNullOrWhiteSpace(detailedDescription)
                ? detailedDescription.Trim()
                : GetShortDescription();

            if (string.IsNullOrWhiteSpace(detail))
            {
                return detail;
            }

            if (appliedToCurrentCard)
            {
                return LooksLikeTransferInstruction(detail)
                    ? FormatAppliedDescription()
                    : detail;
            }

            if (LooksLikeTransferInstruction(detail))
            {
                string glossary = GetKeywordGlossDescription();
                if (!string.IsNullOrWhiteSpace(glossary)
                    && detail.IndexOf(glossary, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return $"{glossary} {detail}".Trim();
                }
            }

            return detail;
        }

        private static bool LooksLikeTransferInstruction(string detail)
        {
            if (string.IsNullOrWhiteSpace(detail))
            {
                return false;
            }

            string trimmed = detail.Trim();
            return trimmed.StartsWith("Give ", System.StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("Attach ", System.StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("Grant ", System.StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("Apply ", System.StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("Target ", System.StringComparison.OrdinalIgnoreCase);
        }

        private string FormatAppliedDescription()
        {
            int appliedValue = Mathf.Max(1, value);
            switch (keyword)
            {
                case AbilityKeyword.Gather:
                    return $"This card has Gather {appliedValue}. At the start of each friendly Deploy phase, gain +{appliedValue} coins while it remains in play.";
                case AbilityKeyword.Siphon:
                    return $"This card has Siphon {appliedValue}. When its Siphon effect resolves, steal {appliedValue} coins from the enemy treasury.";
                case AbilityKeyword.Discount:
                    return $"This card has Discount {appliedValue}. Its matching deployment discount is increased by {appliedValue} while it remains in play.";
                case AbilityKeyword.Strike:
                    return $"This card has Strike {appliedValue}. Its attacks deal +{appliedValue} flat damage.";
                case AbilityKeyword.Shatter:
                    return "This card has Shatter. It deals double damage to buildings and base tiles.";
                case AbilityKeyword.Breach:
                    return "This card has Breach. Excess unit-kill damage carries forward in its attack direction.";
                case AbilityKeyword.Intercept:
                    return $"This card has Intercept {appliedValue}. It blocks the first {appliedValue} incoming damage event(s) each round.";
                case AbilityKeyword.Secure:
                    return $"This card has Secure {appliedValue}. If it remains on the same connected Freespace tile for 2 friendly Deploy starts, that tile becomes a weakened friendly base tile.";
                case AbilityKeyword.Reclaim:
                    return $"This card has Reclaim {appliedValue}. The next {appliedValue} enemy base tile(s) it destroys become friendly base instead of neutral Freespace.";
                case AbilityKeyword.Sprint:
                    return $"This card has Sprint {appliedValue}. Its movement range is increased by {appliedValue}.";
                case AbilityKeyword.Maneuver:
                    return "This card has Maneuver. It may move orthogonally in any direction during Deploy.";
                case AbilityKeyword.Provoke:
                    return "This card has Provoke. Enemies that can attack it must target this card if able.";
                case AbilityKeyword.Lock:
                    return $"This card is Locked for {appliedValue} turn(s). It cannot move, attack, or use abilities.";
                case AbilityKeyword.Silence:
                    return $"This card is Silenced for {appliedValue} turn(s). Its abilities are disabled.";
                case AbilityKeyword.Garrison:
                    return "This card has Garrison. It projects its local stat aura while it remains in play.";
                case AbilityKeyword.Spawn:
                    return spawnedCard != null
                        ? $"This card has Spawn. When its Spawn effect resolves, it creates {spawnedCard.cardName} directly onto the board instead of into your hand."
                        : "This card has Spawn. When its Spawn effect resolves, it creates its listed token or card directly onto the board instead of into your hand.";
                case AbilityKeyword.Burn:
                    return "This card has Burn. It is removed from the match instead of returning to discard.";
                case AbilityKeyword.Salvage:
                    return $"This card has Salvage {appliedValue}. At the start of each friendly Deploy phase, return {appliedValue} random discard card(s) to the deck while it remains in play.";
                default:
                    return !string.IsNullOrWhiteSpace(detailedDescription)
                        ? detailedDescription.Trim()
                        : GetShortDescription();
            }
        }

        public string GetKeywordGlossDescription()
        {
            int appliedValue = Mathf.Max(1, value);
            switch (keyword)
            {
                case AbilityKeyword.Intercept:
                    return $"Intercept {appliedValue} blocks the first {appliedValue} incoming damage event(s) each round.";
                case AbilityKeyword.Sprint:
                    return $"Sprint {appliedValue} increases movement range by {appliedValue}.";
                case AbilityKeyword.Gather:
                    return $"Gather {appliedValue} adds +{appliedValue} coins at the start of each friendly Deploy phase while the card remains in play.";
                case AbilityKeyword.Strike:
                    return $"Strike {appliedValue} adds +{appliedValue} flat damage to attacks.";
                case AbilityKeyword.Secure:
                    return $"Secure {appliedValue} converts the current connected Freespace tile under this card into a weakened friendly base tile after 2 friendly Deploy starts on that same tile.";
                case AbilityKeyword.Reclaim:
                    return $"Reclaim {appliedValue} makes the next {appliedValue} destroyed enemy base tile(s) become friendly base instead of neutral Freespace.";
                case AbilityKeyword.Maneuver:
                    return "Maneuver allows orthogonal movement in any direction during Deploy.";
                case AbilityKeyword.Provoke:
                    return "Provoke forces enemies that can attack this card to target it.";
                case AbilityKeyword.Salvage:
                    return $"Salvage {appliedValue} returns {appliedValue} random discard card(s) to the deck at the start of each friendly Deploy phase while the card remains in play.";
                case AbilityKeyword.Shatter:
                    return "Shatter deals double damage to buildings and base tiles.";
                case AbilityKeyword.Breach:
                    return "Breach carries excess unit-kill damage forward in the attack direction.";
                case AbilityKeyword.Spawn:
                    return "Spawn creates the listed token or card directly onto the board instead of putting it into your hand.";
                default:
                    return string.Empty;
            }
        }

        private string FormatDefaultDescription()
        {
            switch (keyword)
            {
                case AbilityKeyword.Gather:
                    return $"Gather {value}: gain {value} coins.";
                case AbilityKeyword.Siphon:
                    return $"Siphon {value}: steal {value} coins.";
                case AbilityKeyword.Discount:
                    return $"Discount {value}: reduce a deployment cost by {value}.";
                case AbilityKeyword.Strike:
                    return $"Strike {value}: deal {value} damage.";
                case AbilityKeyword.Shatter:
                    return "Shatter: double damage to buildings and base tiles.";
                case AbilityKeyword.Breach:
                    return "Breach: excess unit damage carries into the tile behind.";
                case AbilityKeyword.Intercept:
                    return $"Intercept {Mathf.Max(1, value)}: block incoming damage.";
                case AbilityKeyword.Secure:
                    return $"Secure {value}: hold the same connected Freespace tile for 2 friendly Deploy starts to turn it into a weakened base tile.";
                case AbilityKeyword.Reclaim:
                    return $"Reclaim {value}: destroyed enemy base tiles become yours.";
                case AbilityKeyword.Sprint:
                    return $"Sprint {value}: increase movement range by {value}.";
                case AbilityKeyword.Maneuver:
                    return "Maneuver: move orthogonally in any direction during Deploy.";
                case AbilityKeyword.Provoke:
                    return "Provoke: nearby enemies must attack this if able.";
                case AbilityKeyword.Lock:
                    return $"Lock {Mathf.Max(1, value)}: stop movement, attacks, and abilities.";
                case AbilityKeyword.Silence:
                    return $"Silence {Mathf.Max(1, value)}: disable abilities.";
                case AbilityKeyword.Garrison:
                    return "Garrison: grant a local stat aura.";
                case AbilityKeyword.Spawn:
                    return spawnedCard != null
                        ? $"Spawn: create {spawnedCard.cardName} directly onto the board."
                        : "Spawn: create the listed token or card directly onto the board.";
                case AbilityKeyword.Burn:
                    return "Burn: remove this card from the match.";
                case AbilityKeyword.Salvage:
                    return $"Salvage {value}: return cards from discard.";
                default:
                    return string.Empty;
            }
        }
    }

    [CreateAssetMenu(fileName = "NewKeywordAbility", menuName = "Last Free City/Keyword Ability")]
    public class CardAbilityDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string abilityId = "keyword.blank";
        public string displayName = "New Keyword Ability";

        [Header("Effect")]
        public AbilityEffectData effect = new AbilityEffectData();

        public string GetShortDescription()
        {
            return effect != null ? effect.GetShortDescription() : string.Empty;
        }

        public string GetDetailedDescription()
        {
            return effect != null ? effect.GetDetailedDescription() : GetShortDescription();
        }
    }
}
