# Last Free City - UI Specification Index

This directory contains the complete UI/UX specifications and technical plans for the mobile card game **Last Free City**. These designs are tailored for **Unity UI Toolkit (UITK)** and adhere strictly to the "Bored in Math Class" art direction.

## Specification Documents

1. [01_Architecture.md](file:///c:/Users/actua/UnityGames/Cardz/UIDesignDocs/01_Architecture.md)
   - Dual-canvas layer separation (WorldSpace vs. ScreenSpace).
   - Dynamic visibility (fade/slide transitions on interaction).
   - Data-driven design patterns via ScriptableObjects.
   - UITK-specific layout strategies (UXML and USS structure).

2. [02_TopHUD.md](file:///c:/Users/actua/UnityGames/Cardz/UIDesignDocs/02_TopHUD.md)
   - Enemy Free City status, Stability, Treasury, and Active Status trays.
   - Mirror of the Player HUD structure.

3. [03_GameBoard.md](file:///c:/Users/actua/UnityGames/Cardz/UIDesignDocs/03_GameBoard.md)
   - The strict 4 Columns x 8 Rows grid layout.
   - Division of Enemy Base, FreeSpace, and Player Base.
   - Tile Prefab structure, health mechanics, and transition logic.

4. [04_BottomHUD.md](file:///c:/Users/actua/UnityGames/Cardz/UIDesignDocs/04_BottomHUD.md)
   - Player Control Center: Stability, Treasury, Nameplate.
   - Carousel-based Hand, Deck count, Contextual Action Buttons, and End Turn controls.

5. [05_InspectorOverlay.md](file:///c:/Users/actua/UnityGames/Cardz/UIDesignDocs/05_InspectorOverlay.md)
   - Detailed inspection pop-up panel for Units, Infrastructure, and Cards.
   - Double-tap and long-press triggers.

6. [06_ArtDirection.md](file:///c:/Users/actua/UnityGames/Cardz/UIDesignDocs/06_ArtDirection.md)
   - "Bored in Math Class" Style Guide.
   - Paper backgrounds, sketchy line art, highlighter fills, sticky-note buttons, and flipbook animation principles.

---

## Architectural Principles for UITK Integration

To ensure full editor inspectability and customization:
- **No Hardcoded Values**: Layout properties, margins, paddings, and background textures are exposed in USS or as fields in Inspector-friendly components.
- **Inspectable ScriptableObject Bindings**: Content is bound through MonoBehaviours that map ScriptableObject data directly to the visual elements.
- **UXML-First Development**: Custom elements and templates are authored in `.uxml` files, allowing easy visual layout editing in the Unity UI Builder.
- **Modular Style Sheets**: High-level visual styles (pencil sketches, ruled paper, highlighters) are defined in a shared `.uss` stylesheet for consistency.
