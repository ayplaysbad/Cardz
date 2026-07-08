using UnityEngine;

namespace LastFreeCity.Gameplay
{
    [CreateAssetMenu(fileName = "NewCityDefinition", menuName = "Last Free City/City Definition")]
    public class CityDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string cityId = "city.free_haven";
        public string displayName = "FREE HAVEN";

        [Header("Starting Economy")]
        public int startingTreasury = 50;

        [Header("Starting Durability")]
        public int startingHealth = 100;

        [Header("Default Loadout")]
        public DeckDefinition defaultDeck;

        [Header("Presentation")]
        public Sprite cityBannerArt;
    }
}
