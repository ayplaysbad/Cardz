using UnityEngine;

namespace LastFreeCity.UI
{
    public enum CardType
    {
        Unit,
        Infrastructure,
        Ordinance
    }

    [CreateAssetMenu(fileName = "NewCardTemplate", menuName = "Last Free City/Card Template")]
    public class CardTemplate : ScriptableObject
    {
        [Header("Basic Info")]
        public string cardName = "Scribbled Recruit";
        public int treasuryCost = 10;
        public CardType cardType = CardType.Unit;

        [Header("Stats")]
        public int health = 15;
        public int attack = 5;
        public int range = 1;

        [Header("Visuals & Lore")]
        public Sprite customArt;
        [TextArea(3, 5)]
        public string abilityText = "Attacks Forward. Smells like graphite.";
    }
}
