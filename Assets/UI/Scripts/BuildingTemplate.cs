using UnityEngine;

namespace LastFreeCity.UI
{
    [CreateAssetMenu(fileName = "NewBuildingTemplate", menuName = "Last Free City/Building Template")]
    public class BuildingTemplate : ScriptableObject
    {
        [Header("Building Settings")]
        public string buildingName = "Cardboard Outpost";
        public int maxHealth = 35;
        public int passiveCoinIncome = 5;
        public bool isSupport = false;

        [Header("Visuals")]
        public Sprite buildingArt;
    }
}
