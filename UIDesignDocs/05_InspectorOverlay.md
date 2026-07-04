# Inspector Overlay (The Pop-up)

The Inspector Overlay is a modal panel that displays full card details and stats when requested, ensuring that the main board remains clean and free of tiny, unreadable text.

---

## 1. Visual Layout & Components

The overlay appears in the exact center of the screen, dimming the background behind it.

```
+-------------------------------------------------------------+
|                      [Overlay Background]                   |
|                        (Dimmed Screen)                      |
|                                                             |
|           +-------------------------------------+           |
|           | [Close Button (X)]                  |           |
|           |                                     |           |
|           |   +--------------+  +-------------+ |           |
|           |   |              |  | Name        | |           |
|           |   |   Card Art   |  | Cost: 10g   | |           |
|           |   | (Pencil Drawing) | HP: 15     | |           |
|           |   |              |  | ATK: 5      | |           |
|           |   +--------------+  | RNG: 2      | |           |
|           |                     +-------------+ |           |
|           |                                     |           |
|           |   +-------------------------------+ |           |
|           |   | Ability Text Description      | |           |
|           |   | "Deals double damage forward" | |           |
|           |   +-------------------------------+ |           |
|           +-------------------------------------+           |
|                                                             |
+-------------------------------------------------------------+
```

### Layout Details
*   **Scrim Container (`#overlay-scrim`)**:
    - A full-screen container set to `justify-content: center; align-items: center;`.
    - **Background Color**: Semitransparent black tint (e.g., `rgba(0, 0, 0, 0.4)`), representing a pencil-shaded overlay or dark smudge.
    - Dismissal: Clicking or tapping anywhere inside this scrim (but outside the details card) closes the overlay.
*   **Details Card (`#inspector-details-card`)**:
    - Centered container styled to look like a large sheet of torn notebook paper with margins and horizontal blue lines.
    - Thick, wobbly pen outline border.

---

## 2. Card Content Mapping & ScriptableObjects

The overlay extracts data dynamically from the associated `CardTemplate` or `UnitTemplate` ScriptableObject:

| Visual Field in UXML | Source Field in ScriptableObject | Type | Visual Style |
| :--- | :--- | :--- | :--- |
| `#card-name` | `cardName` | string | Sloppy All-Caps Marker Text |
| `#card-cost` | `treasuryCost` | int | Highlighted number with "Coins" label |
| `#card-health` | `health` | int | Drawn shield or heart with pencil shading |
| `#card-attack` | `attack` | int | Crossed sword doodles with pencil shading |
| `#card-range` | `range` | int | Target/arrow icon with pencil shading |
| `#card-art` | `largeArtSprite` | Sprite | Full detailed pencil sketch illustration |
| `#card-ability` | `abilityText` | string | Hand-written rule text |

---

## 3. Triggers & Functionality

### A. Hand Triggers
*   **Interaction**: Double-tap a card thumbnail in the hand.
*   **Result**: Displays the full card details.

### B. Board Triggers
*   **Interaction**: Long-press (hold for 0.5 seconds) on a deployed Unit or Infrastructure tile.
*   **Result**: Displays current dynamic stats (including any damage sustained, buffs from Supports, or active Ordinance modifications).

### C. Dismissal Mechanics
*   **Method 1 (Overlay Close Button)**: A visual "X" in the top right corner (looks like a crossed-out pencil scribble).
*   **Method 2 (Outside Tap)**: Direct event listener on `#overlay-scrim`. Tapping outside the card boundaries triggers the transition class `.hidden` on the modal, fading it out.
