# Art Direction - "Bored in Math Class"

This document defines the core aesthetic guidelines for *Last Free City*. The visual concept is "drawn in the back of a spiral notebook during a boring lecture." It must feel tactile, sketchy, charmingly imperfect, and deeply unserious.

---

## 1. Visual Elements

### A. The Canvas (Background)
*   **Aesthetic**: Lined/ruled notebook paper.
*   **Implementation**:
    - The main ScreenSpace background uses a high-resolution, tileable texture of light cream, slightly crinkled notebook paper with blue horizontal lines and a red margin line.
    - **Left Edge**: A vertical UI panel representing spiral notebook rings overlaying the screen.
    - **Margins**: Randomized decorative doodles in the margins (e.g., scribbled cubes, stars, a coffee ring stain in the bottom corner, graphite smudges).
    - Expose these texture assets directly in the `PanelSettings` or background properties of the main container `VisualElement` so they can be swapped or edited in the inspector.

### B. The Line Art
*   **Aesthetic**: Wobbly pen strokes.
*   **Implementation**:
    - No clean vector circles or straight rectangles.
    - Outlines look like black ballpoint pen (slightly faded) or thick black Sharpie.
    - Borders use textures with high frequency noise. In USS, custom borders can be achieved using a **Sliced Sprite (Border Image)** to preserve wobbly hand-drawn edges when UI boxes scale.

### C. Color Palette
Colors should mimic highlighters and cheap colored pencils. The colors do not fill perfectly within the pen outlines; they leak slightly out of the borders.

| Color Name | Hex Code | Medium Mimic | Purpose |
| :--- | :--- | :--- | :--- |
| **Highlighter Yellow** | `#FFE033` | Highlighter pen | Primary highlight, active card selectors |
| **Highlighter Green** | `#33FF57` | Highlighter pen | Deploy button, player buff status |
| **Ink Blue** | `#0033aa` | Standard blue ballpoint | Grid lines, player base tint, friendly card outlines |
| **Scribble Red** | `#EE3333` | Red marker / pencil | Enemy base tint, health hearts, damage text, "BAM!" VFX |
| **Cardboard Brown**| `#C29F74` | Torn shipping box | HUD container background, buttons |
| **Pencil Lead** | `#333333` | Graphite smudge | General text, inactive status, base shadows |

---

## 2. UI Elements & Materials

*   **Buttons**:
    - Shaped like ripped post-it notes, cardboard scraps, or pieces of yellow tape.
    - Border lines have a double stroke or a jagged edge where the paper was "ripped."
*   **Fonts**:
    - Must load a custom handwriting TTF/OTF font (e.g., "Comic Neue", custom "KidHand", or custom messy handwriting font).
    - Capitalization: **ALL CAPS ONLY** for system buttons and names.
    - Kerning: Slightly irregular spacings and varying letter heights.

---

## 3. VFX & Animations (Flipbook Style)

All UI transitions, attacks, and states must reject smooth interpolations in favor of frame-by-frame sketches:

*   **Low Framerate**: Animations run at a simulated low frame rate (e.g., 6 to 10 FPS) to feel like a flipbook.
*   **Wobble Shader**: Static cards have a shader that swaps between 2 or 3 slightly different hand-drawn outlines every 0.2 seconds to simulate "boiling" line art (the lines wiggle slightly even when still).
*   **Action Word Popups (VFX)**:
    - Instead of particle systems, actions (e.g., attacks, base damage) trigger temporary visual text overlays.
    - An attack triggers a massive, jagged, hand-sketched starburst outline containing the word **"BAM!"** or **"POW!"** scribbled in red marker.
    - This starburst flashes on screen for 0.4 seconds (3-4 distinct drawing frames) and then vanishes.
