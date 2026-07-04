# Top HUD: Enemy Free City (ScreenSpace Layer)

The Top HUD represents the status of the enemy's defensive core (the Free City) and is anchored to the top of the ScreenSpace Overlay.

---

## 1. Visual Layout & Anchors

The HUD is layed out horizontally across the top section of the screen, using a flexbox layout configuration within UITK.

```
+-------------------------------------------------------------+
| [Stability (HP)]            [Nameplate]          [Treasury] |
| (Hearts + "100")          "Enemy City"           (Gold Icon) |
|                                                             |
|                          [Active Status]                    |
|                         [Icon] [Icon] [Icon]                |
+-------------------------------------------------------------+
```

### Layout Properties
*   **Anchoring**: Anchored to the Top-Center, with padding on the left and right edges (e.g., 20px).
*   **Aesthetics**: Renders as a ripped strip of notepad paper or cardboard taped to the top of the notebook page (using USS background textures).
*   **Mirroring**: This layout is a 1:1 mirrored layout of the player’s Bottom HUD stats header, sharing the same UXML template structure to maintain structural unity.

---

## 2. Element Breakdown & Inspector Fields

Every element below is fully exposed in UXML and can be bound, scaled, or visually edited in the Unity UI Builder.

### A. Enemy City Nameplate
*   **UXML Element ID**: `#enemy-city-nameplate`
*   **Visual Style**: Renders text in a scribbled, shaky font. Background looks like a scrap of cardboard.
*   **Inspectable Properties**:
    - `Label NameText`: String (e.g., "FORT GRIT").
    - `Font Asset`: Messy handwriting font.
    - `Background Sprite`: Pencil-shaded block sprite.

### B. Enemy Stability (Health)
*   **UXML Element ID**: `#enemy-stability`
*   **Visual Style**: Left-aligned panel. Displays a numerical value alongside heart icons.
*   **Stability Hearts Container (`#enemy-hearts-container`)**:
    - Holds visual heart icons representing stability thresholds.
    - Hearts look like sloppy red marker drawings (like a kid drawing three-second hearts).
*   **Inspectable Properties**:
    - `int MaxStability`: Total health (e.g., 100).
    - `int CurrentStability`: Current health text value.
    - `Sprite HeartActiveSprite`: Hand-drawn heart filled in red.
    - `Sprite HeartEmptySprite`: Hand-drawn heart outline only.

### C. Enemy Treasury
*   **UXML Element ID**: `#enemy-treasury`
*   **Visual Style**: Right-aligned panel. Displays current available coins.
*   **Treasury Icon (`#enemy-treasury-icon`)**:
    - Looks like a coin sketch drawn in yellow highlighter.
*   **Inspectable Properties**:
    - `int CurrentTreasury`: Coins available (starts at 50 or dynamic).
    - `Sprite CoinSprite`: Hand-drawn coin doodle.

### D. Active Status Tray
*   **UXML Element ID**: `#enemy-status-tray`
*   **Visual Style**: A horizontal wrap-layout box anchored directly below the Nameplate.
*   **Contents**: Dynamic list of active city-wide ordinances or debuffs (e.g., "EMP", "Inflation", "Siege").
*   **Inspectable Properties**:
    - `List<StatusEffectTemplate>`: A list of ScriptableObject debuffs to map.
    - `VisualTreeAsset StatusIconTemplate`: The visual representation prefab for a status icon (wobbly border + scribbled symbol).

---

## 3. Inspector Representation

The top HUD is managed by a `TopHUDController` script. This script exposes the following properties directly to the Unity Inspector:

```csharp
public class TopHUDController : MonoBehaviour
{
    [Header("Visual Assets")]
    public Font fontStyle;
    public Sprite defaultHeartSprite;
    public Sprite emptyHeartSprite;
    public Sprite coinSprite;

    [Header("Data Bindings")]
    public string enemyCityName = "The Iron Citadel";
    public int currentStability = 100;
    public int currentTreasury = 50;
    
    // Dynamic status effects to display on the board
    public List<Sprite> activeStatusIcons;
}
```
All of these fields dynamically sync to the UI Document elements so you can edit names, coins, or stability in real-time in the editor.
