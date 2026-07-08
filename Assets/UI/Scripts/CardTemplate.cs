using System.Collections.Generic;
using System.Text;
using System;
using UnityEngine;

namespace LastFreeCity.UI
{
    public enum CardType
    {
        Unit,
        Infrastructure,
        Ordinance,
        Item
    }

    public enum UnitTag
    {
        None,
        Civilian,
        Military,
        Special
    }

    public enum InfrastructureKind
    {
        None,
        Support,
        Building
    }

    public enum CommandCardKind
    {
        None,
        LockUnit
    }

    [CreateAssetMenu(fileName = "NewCardTemplate", menuName = "Last Free City/Card Template")]
    public class CardTemplate : ScriptableObject
    {
        [Header("Basic Info")]
        public string cardId = "card.scribbled_recruit";
        public string cardName = "Scribbled Recruit";
        public int treasuryCost = 10;
        public CardType cardType = CardType.Unit;

        [Header("Stats")]
        public int health = 15;
        public int attack = 5;
        public int range = 1;
        public int movementRange = 1;

        [Header("Gameplay Tags")]
        public UnitTag unitTag = UnitTag.None;
        public InfrastructureKind infrastructureKind = InfrastructureKind.None;
        public CommandCardKind commandCardKind = CommandCardKind.None;

        [Header("Keyword Effects")]
        public List<AbilityEffectData> keywordEffects = new List<AbilityEffectData>();

        [Header("Runtime Attachments")]
        public CardTemplate attachedItemCard;

        [Header("Item Stat Payloads")]
        public int bonusHealth = 0;
        public int bonusAttack = 0;
        public int bonusRange = 0;
        public int bonusMovementRange = 0;
        public int bonusSiegeAttack = 0;

        [Header("Legacy Ability Copy")]
        [Obsolete("Use keywordEffects. This list is ignored by runtime ability rules.")]
        public List<CardAbilityDefinition> abilities = new List<CardAbilityDefinition>();
        [TextArea(2, 4)]
        public string abilityText = string.Empty;
        [TextArea(3, 8)]
        public string detailedAbilityText = string.Empty;

        [Header("Visuals")]
        public Sprite customArt;

        public string GetAbilitySummaryText()
        {
            if (keywordEffects != null && keywordEffects.Count > 0)
            {
                var parts = new List<string>();
                for (int i = 0; i < keywordEffects.Count; i++)
                {
                    AbilityEffectData effect = keywordEffects[i];
                    if (effect == null)
                    {
                        continue;
                    }

                    string summary = NormalizeAbilitySentence(effect.GetShortDescription());
                    if (!string.IsNullOrWhiteSpace(summary))
                    {
                        parts.Add(summary);
                    }
                }

                if (parts.Count > 0)
                {
                    return string.Join(" ", parts);
                }
            }

            if (!string.IsNullOrWhiteSpace(abilityText))
            {
                return abilityText.Trim();
            }

            return !string.IsNullOrWhiteSpace(detailedAbilityText) ? detailedAbilityText.Trim() : string.Empty;
        }

        public string GetDetailedAbilityText()
        {
            if (keywordEffects != null && keywordEffects.Count > 0)
            {
                var builder = new StringBuilder();
                for (int i = 0; i < keywordEffects.Count; i++)
                {
                    AbilityEffectData effect = keywordEffects[i];
                    if (effect == null)
                    {
                        continue;
                    }

                    string detail = effect.GetDetailedDescription();
                    if (string.IsNullOrWhiteSpace(detail))
                    {
                        detail = effect.GetShortDescription();
                    }

                    detail = detail != null ? detail.Trim() : string.Empty;
                    if (string.IsNullOrWhiteSpace(detail))
                    {
                        continue;
                    }

                    if (builder.Length > 0)
                    {
                        builder.Append("\n\n");
                    }

                    builder.Append(detail);
                }

                if (builder.Length > 0)
                {
                    return builder.ToString();
                }
            }

            if (!string.IsNullOrWhiteSpace(detailedAbilityText))
            {
                return detailedAbilityText.Trim();
            }

            return !string.IsNullOrWhiteSpace(abilityText) ? abilityText.Trim() : string.Empty;
        }

        private static string NormalizeAbilitySentence(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();
            char last = trimmed[trimmed.Length - 1];
            if (last == '.' || last == '!' || last == '?')
            {
                return trimmed;
            }

            return $"{trimmed}.";
        }
    }
}
