import os

uss_path = r"c:\Users\actua\UnityGames\Cardz\Assets\UI\USS\LastFreeCityStyles.uss"

with open(uss_path, "r", encoding="utf-8") as f:
    content = f.read()

# 1. Make main canvas HUD background transparent
old_canvas = """/* Background & Margin Graph Grid */
.main-canvas {
    width: 100%;
    height: 100%;
    flex-direction: column;
    justify-content: space-between;
    background-image: url("project://database/Assets/UI/Sprites/notebook_ruled_paper_bg.png");
    -unity-background-scale-mode: stretch-to-fill;
    padding: 40px;
}"""

new_canvas = """/* Background & Margin Graph Grid */
.main-canvas {
    width: 100%;
    height: 100%;
    flex-direction: column;
    justify-content: space-between;
    background-color: transparent; /* Transparent background to see the 2D camera board */
    padding: 40px;
}"""
content = content.replace(old_canvas, new_canvas)

# 2. Make card title font larger (22px)
old_title = """.thumbnail-title {
    -unity-font: url("project://database/Assets/UI/Fonts/PermanentMarker-Regular.ttf");
    -unity-font-definition: initial;
    font-size: 16px; /* Bigger card title text */
    color: #333333;
    -unity-text-align: center;
    -unity-font-style: bold;
    white-space: normal;
    height: 50px;
    justify-content: center;
}"""

new_title = """.thumbnail-title {
    -unity-font: url("project://database/Assets/UI/Fonts/PermanentMarker-Regular.ttf");
    -unity-font-definition: initial;
    font-size: 22px; /* Much bigger card title text */
    color: #333333;
    -unity-text-align: center;
    -unity-font-style: bold;
    white-space: normal;
    height: 50px;
    justify-content: center;
}"""
content = content.replace(old_title, new_title)

# 3. Enlarge card cost badge container
old_badge = """.thumbnail-cost-badge {
    position: absolute;
    top: 4px;
    right: 4px;
    background-color: #FFE033;
    border-width: 2px;
    border-color: #333333;
    border-radius: 50%;
    width: 36px;
    height: 36px;
    justify-content: center;
    align-items: center;
    z-index: 10;
}"""

new_badge = """.thumbnail-cost-badge {
    position: absolute;
    top: 4px;
    right: 4px;
    background-color: #FFE033;
    border-width: 2px;
    border-color: #333333;
    border-radius: 50%;
    width: 46px; /* Larger Cost Badge Width */
    height: 46px; /* Larger Cost Badge Height */
    justify-content: center;
    align-items: center;
    z-index: 10;
}"""
content = content.replace(old_badge, new_badge)

# 4. Enlarge card cost badge font size
old_cost_text = """.thumbnail-cost-text {
    -unity-font: url("project://database/Assets/UI/Fonts/ArchitectsDaughter-Regular.ttf");
    -unity-font-definition: initial;
    font-size: 20px;
    color: #333333;
    -unity-font-style: bold;
}"""

new_cost_text = """.thumbnail-cost-text {
    -unity-font: url("project://database/Assets/UI/Fonts/ArchitectsDaughter-Regular.ttf");
    -unity-font-definition: initial;
    font-size: 26px; /* Larger Cost text */
    color: #333333;
    -unity-font-style: bold;
}"""
content = content.replace(old_cost_text, new_cost_text)

# 5. Add scroller hide rule at the bottom
scroller_hide_rule = """
/* Hide horizontal scroller bars completely for gesture swipe or scroll wheel usage */
.hand-carousel-scroll .unity-scroller {
    display: none;
    visibility: hidden;
    opacity: 0;
}
"""

if scroller_hide_rule not in content:
    content += scroller_hide_rule

with open(uss_path, "w", encoding="utf-8") as f:
    f.write(content)

print("Styles successfully updated for transparency, scrollbar hiding, and card title resizing!")
