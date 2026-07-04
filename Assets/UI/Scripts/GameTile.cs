using UnityEngine;
using UnityEngine.UIElements;

namespace LastFreeCity.UI
{
    public enum TileType
    {
        Base,
        FreeSpace
    }

    public enum OwnerSide
    {
        Player,
        Enemy,
        Neutral
    }

    [ExecuteInEditMode]
    public class GameTile : MonoBehaviour
    {
        [Header("State Settings")]
        public TileType tileType = TileType.Base;
        public OwnerSide owner = OwnerSide.Player;

        [Header("Health System")]
        public int maxHealth = 30;
        public int currentHealth = 30;

        [Header("Visual Tints")]
        public Color playerBaseTint = new Color(0.2f, 0.4f, 0.8f, 0.3f); // Translucent blue highlighter
        public Color enemyBaseTint = new Color(0.8f, 0.2f, 0.2f, 0.3f);  // Translucent red highlighter
        public Color freeSpaceTint = new Color(1f, 1f, 1f, 0.1f);       // Blank graph paper overlay

        [Header("Placement Anchors")]
        public Transform unitAnchor;
        public Transform infrastructureAnchor;

        [Header("Scene References")]
        public SpriteRenderer backgroundRenderer;
        public TextMesh floatingHpText; // WorldSpace TextMesh for HP text

        private void Start()
        {
            UpdateTileVisuals();
        }

        private void OnValidate()
        {
            UpdateTileVisuals();
        }

        [ContextMenu("Apply Damage (10)")]
        public void TestDamage()
        {
            TakeDamage(10);
        }

        public void TakeDamage(int amount)
        {
            currentHealth = Mathf.Max(0, currentHealth - amount);
            
            if (currentHealth <= 0 && tileType == TileType.Base)
            {
                // Incursion Breach Transition
                tileType = TileType.FreeSpace;
                owner = OwnerSide.Neutral;
                Debug.Log($"Tile {gameObject.name} depleted! Transitioning to FreeSpace.");
            }

            UpdateTileVisuals();
        }

        public void UpdateTileVisuals()
        {
            // Update Background Tint based on state and ownership
            if (backgroundRenderer != null)
            {
                if (tileType == TileType.FreeSpace || owner == OwnerSide.Neutral)
                {
                    backgroundRenderer.color = freeSpaceTint;
                }
                else if (owner == OwnerSide.Player)
                {
                    backgroundRenderer.color = playerBaseTint;
                }
                else if (owner == OwnerSide.Enemy)
                {
                    backgroundRenderer.color = enemyBaseTint;
                }
            }

            // Update WorldSpace Floating HP TextMesh
            if (floatingHpText != null)
            {
                if (tileType == TileType.Base)
                {
                    floatingHpText.text = $"{currentHealth}/{maxHealth} HP";
                    floatingHpText.gameObject.SetActive(true);
                }
                else
                {
                    // Hide HP text for neutral FreeSpace
                    floatingHpText.gameObject.SetActive(false);
                }
            }
        }
    }
}
