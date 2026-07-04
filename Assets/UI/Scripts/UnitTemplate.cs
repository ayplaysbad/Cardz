using UnityEngine;

namespace LastFreeCity.UI
{
    [CreateAssetMenu(fileName = "NewUnitTemplate", menuName = "Last Free City/Unit Template")]
    public class UnitTemplate : ScriptableObject
    {
        [Header("Unit Settings")]
        public string unitName = "Sharpie Soldier";
        public int maxHealth = 20;
        public int attack = 8;
        public int range = 1;
        public int movement = 1;

        [Header("Visuals")]
        public Sprite unitArt;
    }
}
