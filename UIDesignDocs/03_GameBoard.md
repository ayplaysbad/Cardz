# The Game Board (WorldSpace Layer)

The Game Board is the centerpiece of the game, structured as a strict 4 Columns x 8 Rows grid. It scales to fit between the Top and Bottom HUD elements.

---

## 1. Grid Layout & Visual Zones

The grid is divided into three functional zones running from top to bottom (representing Enemy Base, neutral combat territory, and Player Base):

```
+-----------------------------+
| Col 1   Col 2   Col 3   Col 4|
+-----------------------------+
| [R1] [R1] [R1] [R1]         | -- Row 1 (Enemy Base: Red Tint)
| [R2] [R2] [R2] [R2]         | -- Row 2 (Enemy Base: Red Tint)
+-----------------------------+
| [R3] [R3] [R3] [R3]         | -- Row 3 (FreeSpace: Lined Paper Style)
| [R4] [R4] [R4] [R4]         | -- Row 4 (FreeSpace: Lined Paper Style)
| [R5] [R5] [R5] [R5]         | -- Row 5 (FreeSpace: Lined Paper Style)
| [R6] [R6] [R6] [R6]         | -- Row 6 (FreeSpace: Lined Paper Style)
+-----------------------------+
| [R7] [R7] [R7] [R7]         | -- Row 7 (Player Base: Blue Tint)
| [R8] [R8] [R8] [R8]         | -- Row 8 (Player Base: Blue Tint)
+-----------------------------+
```

### Visual Specifications
*   **Grid Dimensions**: 4x8 cells. Centered on the screen.
*   **Enemy Base (Rows 1 & 2)**:
    - 8 Tiles total.
    - Visually styled with red pencil/highlighter tinting.
    - Represents the enemy defensive outer shell.
*   **FreeSpace (Rows 3, 4, 5, & 6)**:
    - 16 Tiles total.
    - Neutral combat zone.
    - Styled to look like plain graph paper or a blank notebook grid.
*   **Player Base (Rows 7 & 8)**:
    - 8 Tiles total.
    - Visually styled with blue pencil/highlighter tinting.
    - Zone where the player can deploy units and infrastructure during Setup.

---

## 2. Tile Prefab Structure & Logic

Each tile is a highly modular, self-contained Prefab that handles its own states, health, and anchors.

### Tile Object Hierarchy (Inspector Visible)
Every slot is an explicit child GameObject with a transform anchor that can be adjusted in the Editor.

```
TilePrefab (GameObject)
├── SpriteRenderer / MeshRenderer (Base background look)
├── TileUI (WorldSpace UIDocument for floating HP text)
├── InfrastructureSlot (Transform Anchor for Building / Support)
└── UnitSlot (Transform Anchor for Unit placement)
```

### Inspector Exposed Fields
```csharp
public enum TileType { Base, FreeSpace }
public enum OwnerSide { Player, Enemy, Neutral }

public class GameTile : MonoBehaviour
{
    [Header("State Settings")]
    public TileType tileType = TileType.Base;
    public OwnerSide owner = OwnerSide.Player;
    
    [Header("Health Pool")]
    public int maxHealth = 30;
    public int currentHealth = 30;
    
    [Header("Visual Tinting Colors")]
    public Color playerBaseTint = new Color(0.2f, 0.4f, 0.8f, 0.3f); // Translucent Blue Pencil
    public Color enemyBaseTint = new Color(0.8f, 0.2f, 0.2f, 0.3f);  // Translucent Red Pencil
    public Color freeSpaceTint = new Color(1f, 1f, 1f, 0.1f);       // Blank paper
    
    [Header("Placement Anchors")]
    public Transform unitAnchor;
    public Transform infrastructureAnchor;
    
    [Header("References")]
    public SpriteRenderer backgroundRenderer;
    public UIDocument floatingHpTextDocument;
}
```

---

## 3. Dynamic Tile State Transitions (Incursion Logic)

The transition of base tiles to neutral space is fully dynamic and changes based on health changes.

### Trigger Logic
1.  **Damage Event**: When units attack a Base Tile (Rows 1-2 for enemy, 7-8 for player), its `currentHealth` is depleted.
2.  **Destruction State**: Once `currentHealth <= 0`:
    - The Tile's state automatically changes: `tileType` is set to `TileType.FreeSpace`.
    - The `owner` is set to `OwnerSide.Neutral`.
    - The background sprite/material changes its color tint dynamically (e.g. shifts from blue/red to blank notebook grid paper with wobbly edges).
    - The grid slot properties are updated to allow free movement, making it a combat lane zone.
3.  **Core Exposure**: Once all 8 Base Tiles for a side are destroyed, units are flagged as permitted to cross the final threshold into the Core (Free City health).
