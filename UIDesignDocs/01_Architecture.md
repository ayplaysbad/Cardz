# UI Architecture - Last Free City

This document outlines the global architecture requirements for the UI systems in *Last Free City*. The architecture is split between WorldSpace (game board interaction) and ScreenSpace (HUD overlays and menus) using Unity's UI Toolkit (UITK).

---

## 1. Canvas Layers

The visual layer structure of the game is split into two distinct rendering layers to separate tactile, physical board elements from persistent info-overlays:

```
[Layer 2: ScreenSpace Overlay] <-- HUD, Hand, Deck, Pop-up Overlay (UITK Canvas)
      ^
      | (Dynamic Visibility - Fade / Slide Off)
      v
[Layer 1: WorldSpace Game Board] <-- 4x8 Grid, Physical Tiles, Units, Infrastructure (3D/2.5D)
```

### Layer 1: WorldSpace (The Game Board)
*   **Purpose**: Renders the physical gameplay elements (the board grid, unit cards, buildings, active support structures).
*   **Technique**: Rendered in 3D/2.5D space aligned with the camera frustum. Fits centered between the top and bottom HUD limits.
*   **Inspectability**: Individual Board Tiles are standard game objects with their own `UIDocument` components (using WorldSpace rendering) or standard Unity Mesh Renderers displaying hand-drawn card graphics. This allows every tile to be dragged, dropped, or modified in the inspector.

### Layer 2: ScreenSpace Overlay (HUD and Menus)
*   **Purpose**: Renders the persistent player stats, enemy stats, player hand carousel, and full-screen contextual menus (like the Inspector Overlay).
*   **Technique**: Uses a single main `UIDocument` component set to `PanelSettings` that target ScreenSpace-Overlay.
*   **Inspectability**: Built using a nested hierarchy of UXML templates. Every text field, coin counter, and button is bound to visual queries so they can be modified visually in the UI Builder or tweaked in the inspector of the main UI Manager.

---

## 2. Dynamic Visibility (HUD Hiding)

To maximize screen real estate during combat planning and card deployment, the ScreenSpace Overlay (Layer 2) reacts dynamically to player actions.

### Triggers & Behavior
*   **Drag Card / Select Unit**:
    - The Moment the player initiates a drag action on a card in their hand, or taps a deployed unit on the board:
    - Layer 2 transitions out.
    - **Transition Method**: In UITK, the main container's `.uss` class is toggled to add a `.hud-hidden` class. This class drops the `opacity` style parameter to `0` and changes the layout offset (`translate`) to slide the top HUD up and bottom HUD down off-screen.
*   **Release Card / Deselect**:
    - Once the card is deployed, returned to hand, or the selection is cleared:
    - The `.hud-hidden` class is removed, smoothly fading and sliding the HUD back into view.

### USS Transition Setup
```css
/* Shared HUD transition setup */
.hud-container {
    transition-property: opacity, translate;
    transition-duration: 0.25s;
    transition-timing-function: ease-out-sine;
    opacity: 1;
}

.hud-container.hud-hidden {
    opacity: 0;
    /* Slide top HUD up and bottom HUD down */
    translate: 0 -150px; /* For Top HUD */
}
```

---

## 3. Data-Driven UI System

To avoid hardcoded properties and ensure full editor-level inspectability, the entire UI is driven by standard Unity **ScriptableObjects** and modular UI templates.

### ScriptableObject Structures
*   **CardTemplate**: Stores Card data (cost, name, type: Unit/Infrastructure/Ordinance, art texture, rules text).
*   **UnitTemplate**: Stores Unit base stats (health, attack, range, movement range, graphics).
*   **BuildingTemplate**: Stores infrastructure health, passive income generation, support buffs.

### UI Binding Pattern
Instead of hiding content binding inside complex black-box scripts, content is injected using a modular **UI Linker** pattern:
1.  **UI Elements as Templates**: The UXML files represent the skeleton structure (e.g., `CardTemplate.uxml`).
2.  **Unity Inspector Fields**: A MonoBehaviour script (`UIDocumentBinder`) is attached to UI prefabs.
    - This script exposes fields for `Visual Tree Asset` (UXML), `Stylesheet` (USS), and references to standard ScriptableObjects.
    - You can swap the ScriptableObject reference in the Unity Inspector, and the script will automatically update the label contents, textures, and stats in both Edit Mode (using `ExecuteInEditMode`) and Play Mode.

```csharp
// Example Binder Class for Inspector Configuration
public class CardUIDocumentBinder : MonoBehaviour
{
    [Header("Data Source")]
    public CardTemplate cardData;

    [Header("UI Templates")]
    public VisualTreeAsset cardUxml;
    
    // Allows the user to edit values on the fly in the inspector and see instant updates
    private void OnValidate()
    {
        UpdateUI();
    }
}
```
