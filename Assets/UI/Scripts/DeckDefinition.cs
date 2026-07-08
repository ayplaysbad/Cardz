using System.Collections.Generic;
using LastFreeCity.UI;
using UnityEngine;

namespace LastFreeCity.Gameplay
{
    [CreateAssetMenu(fileName = "NewDeckDefinition", menuName = "Last Free City/Deck Definition")]
    public class DeckDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string deckId = "deck.free_haven";
        public string displayName = "FREE HAVEN DECK";

        [Header("Cards")]
        public List<CardTemplate> cards = new List<CardTemplate>();
    }
}
