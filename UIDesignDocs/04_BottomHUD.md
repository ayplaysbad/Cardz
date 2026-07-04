# Bottom HUD: Player Control Center (ScreenSpace Layer)

The Bottom HUD is the primary interface for player actions. It is anchored to the bottom portion of the ScreenSpace overlay.

---

## 1. Visual Layout & Anchors

The Player HUD is divided into three horizontal columns, mirroring the Top HUD while incorporating action mechanics.

```
       +---------------------------------------------+
       | [Deploy] [Move] [Attack] <-- Contextual Btns|
+------+------------------------------+--------------+
| [HP] |   Nameplate: Player City     |  [Treasury]  |
+------+------------------------------+--------------+
| [D]  |  [Card] [Card] [Card] [Card] |  [End Turn]  |
|      |    (Horizontal ScrollRect)   |              |
+------+------------------------------+--------------+
* D = Deck Pile (Remaining count)
```

### Layout Anchors
*   **Stats Bar**: Positioned directly above the Hand Carousel.
    - **Stability**: Mid-Left, floating directly above the Deck.
    - **Nameplate**: Anchored center-aligned above the Hand.
    - **Treasury**: Mid-Right, floating directly above the End Turn Button.
*   **Deck Pile**: Anchored bottom-left. Displays remaining cards.
*   **Hand Carousel**: Anchored bottom-center. A ScrollView scrolling horizontally.
*   **Contextual Buttons**: Hovering above the center of the carousel.
*   **End Turn Button**: Anchored bottom-right. Fixed size.

---

## 2. Element Breakdown & UI Toolkit Binding

All elements are designed to be inspectable, styled using a custom `.uss` stylesheet.

### A. Stats Area (Mirrors Enemy HUD)
*   **Player City Nameplate (`#player-city-nameplate`)**: Text label styled like a ripped sticky note.
*   **Player Stability (`#player-stability`)**: Left side display showing health value + heart doodles.
*   **Player Treasury (`#player-treasury`)**: Right side display showing gold coin counts.

### B. Action & Deployment Zone
*   **The Deck (`#deck-container`)**:
    - Appears as a stacked deck of cards drawn in wobbly pencil.
    - Text overlay displays the exact remaining count (e.g. `24`).
    - **Inspectable Fields**: `int RemainingCards`, `Sprite CardBackArt`.
*   **The Hand Carousel (`#hand-carousel`)**:
    - A UITK `ScrollView` set to horizontal scrolling.
    - Contains smaller card thumbnail elements to conserve vertical space.
    - **Interactive Thumbnails**: Each thumbnail has scale and rotation offsets to mimic a fan of hand-held scribbled sketches.
    - **Inspectable Fields**: `VisualTreeAsset CardThumbnailTemplate`, `float CardSpacing`.
*   **Contextual Action Buttons (`#contextual-action-bar`)**:
    - Three buttons: **Deploy**, **Move**, and **Attack**.
    - Styled to look like bright fluorescent highlighters (Green, Orange, Pink) highlighting text.
    - **Dynamic Toggle Logic**: These buttons start with `display: none;` (or `opacity: 0;`). They fade into view ONLY when a card in the hand is tapped (shows Deploy) or an active board unit is tapped (shows Move and Attack).
*   **End Turn Button (`#end-turn-button`)**:
    - Fixed size, anchored to the bottom-right.
    - Styled like a piece of red cardboard with "DONE!" scribbled in heavy Sharpie.
    - **Inspectable Fields**: `string EndTurnText` (default "DONE!"), `Sprite CustomButtonTexture`.

---

## 3. MonoBehaviour Controller

The Bottom HUD is controlled by a `BottomHUDController` MonoBehaviour that exposes all critical UI elements and properties in the Inspector.

```csharp
public class BottomHUDController : MonoBehaviour
{
    [Header("Deck Settings")]
    public int deckCount = 30;
    public Sprite cardBackTexture;

    [Header("Hand Settings")]
    public float cardSpacing = 15f;
    public List<CardTemplate> cardsInHandData; // List of ScriptableObjects

    [Header("End Turn Settings")]
    public string endTurnButtonText = "DONE!";
    public Color endTurnActiveColor = Color.red;

    [Header("Interactive Elements")]
    public GameObject deployButton;
    public GameObject moveButton;
    public GameObject attackButton;
}
```
Swapping values in this Inspector updates card counts, names, and hand cards automatically.
