using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LastFreeCity.UI
{
    [RequireComponent(typeof(UIDocument))]
    [ExecuteInEditMode]
    public class UIManager : MonoBehaviour
    {
        [Header("Player Stats")]
        public string playerCityName = "FREE HAVEN";
        [Range(0, 100)] public int playerStability = 100;
        public int playerTreasury = 50;
        public int deckRemainingCount = 24;

        [Header("Enemy Stats")]
        public string enemyCityName = "IRON CITADEL";
        [Range(0, 100)] public int enemyStability = 100;
        public int enemyTreasury = 50;

        [Header("Card Hand Data")]
        public List<CardTemplate> cardsInHand = new List<CardTemplate>();
        public VisualTreeAsset cardThumbnailTemplate; // UXML for small thumbnail card

        [Header("Active Selection / Inspector Popup")]
        public CardTemplate detailedCardData;
        public bool isInspectorOverlayOpen = false;

        [Header("Interactive Testing Triggers")]
        [Tooltip("Hide/Show HUD (simulates dragging card or selecting unit)")]
        public bool hideHUD = false;

        private UIDocument _uiDocument;
        private VisualElement _root;

        private int _highlightedCardIndex = -1;
        private CardTemplate[] _boardTileData = new CardTemplate[24];

        private float _tileScale = 1.0f;
        private bool _hudHidden = false;

        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();
            UpdateUI();
        }

        private void Start()
        {
            // Ensure cardsInHand is never null
            if (cardsInHand == null)
            {
                cardsInHand = new List<CardTemplate>();
            }

            // Duplicate first two cards at runtime if the hand has fewer than 5 cards to allow scroll testing
            if (cardsInHand.Count > 0 && cardsInHand.Count < 5)
            {
                CardTemplate c1 = cardsInHand[0];
                CardTemplate c2 = cardsInHand.Count > 1 ? cardsInHand[1] : cardsInHand[0];
                cardsInHand.Add(c1);
                cardsInHand.Add(c2);
            }

            // Pre-deploy enemy units to tiles 1 and 2 at start to show what units look like on board
            if (cardsInHand.Count > 0)
            {
                _boardTileData[1] = cardsInHand[0];
                if (cardsInHand.Count > 1)
                {
                    _boardTileData[2] = cardsInHand[1];
                }
            }

            UpdateUI();
            RegisterEvents();
        }

        private void OnValidate()
        {
            UpdateUI();
        }

        private void Update()
        {
            // Simple polling for runtime visual testing
            if (Application.isPlaying && _root != null)
            {
                UpdateHUDVisibility();
            }
        }

        [ContextMenu("Refresh All UI")]
        public void UpdateUI()
        {
            if (_uiDocument == null)
                _uiDocument = GetComponent<UIDocument>();

            if (_uiDocument == null) return;

            _root = _uiDocument.rootVisualElement;
            if (_root == null) return;

            // Bind Player HUD
            SetText("#player-city-nameplate", playerCityName.ToUpper());
            SetText("#player-stability", playerStability.ToString());
            SetText("#player-treasury", playerTreasury.ToString());
            SetText("#deck-count", deckRemainingCount.ToString());

            // Bind Enemy HUD
            SetText("#enemy-city-nameplate", enemyCityName.ToUpper());
            SetText("#enemy-stability", enemyStability.ToString());
            SetText("#enemy-treasury", enemyTreasury.ToString());

            // Bind Cards Hand
            PopulateHandCarousel();

            // Bind Board Grid Tiles
            PopulateBoard();

            // Bind Inspector Overlay Details
            UpdateInspectorOverlay();

            // Update Visibility States
            UpdateHUDVisibility();

            // Grey out zoom buttons at scale limits
            var zoomInBtn = _root.Q<Button>("zoom-in-button");
            var zoomOutBtn = _root.Q<Button>("zoom-out-button");
            if (zoomInBtn != null) zoomInBtn.SetEnabled(_tileScale < 1.49f);
            if (zoomOutBtn != null) zoomOutBtn.SetEnabled(_tileScale > 0.61f);
        }

        private void SetText(string nameQuery, string textValue)
        {
            var label = _root.Q<Label>(nameQuery);
            if (label != null)
            {
                label.text = textValue;
            }
        }

        private void PopulateHandCarousel()
        {
            var carousel = _root.Q<ScrollView>("hand-carousel");
            if (carousel == null) return;

            carousel.Clear();

            #if UNITY_EDITOR
            // Self-healing check: Automatically load the correct thumbnail layout if null or misconfigured
            if (cardThumbnailTemplate == null || cardThumbnailTemplate.name == "MainHUD")
            {
                cardThumbnailTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/UXML/CardThumbnail.uxml");
            }
            #endif

            if (cardThumbnailTemplate == null)
            {
                // Put a placeholder text if no thumbnail template is set
                if (cardsInHand.Count > 0)
                {
                    var label = new Label("Add Card Thumbnail Template to Inspector!");
                    label.style.color = Color.gray;
                    carousel.Add(label);
                }
                return;
            }

            for (int i = 0; i < cardsInHand.Count; i++)
            {
                var cardData = cardsInHand[i];
                if (cardData == null) continue;

                // Instantiate UXML template
                VisualElement cardInstance = cardThumbnailTemplate.Instantiate();
                
                // Add margins/spacings (offset index 0 to start clear of zoom panel)
                cardInstance.style.marginRight = 10;
                cardInstance.style.marginLeft = (i == 0) ? 100 : 10;

                // Bind thumbnail data (Cost, Name, Small Art)
                var thumbName = cardInstance.Q<Label>("card-name");
                if (thumbName != null) thumbName.text = cardData.cardName.ToUpper();

                var thumbCost = cardInstance.Q<Label>("card-cost");
                if (thumbCost != null) thumbCost.text = cardData.treasuryCost.ToString();

                var thumbArt = cardInstance.Q<VisualElement>("card-art");
                if (thumbArt != null && cardData.customArt != null)
                    thumbArt.style.backgroundImage = new StyleBackground(cardData.customArt);

                // Bind HP and Attack statistics with rich text bold numbers
                var thumbHp = cardInstance.Q<Label>("card-hp");
                if (thumbHp != null) thumbHp.text = $"<b>{cardData.health}</b> HP";

                var thumbAt = cardInstance.Q<Label>("card-at");
                if (thumbAt != null) thumbAt.text = $"<b>{cardData.attack}</b> AT";

                // Apply highlight visual effect if selected
                var cardRoot = cardInstance.Q<VisualElement>(className: "card-thumbnail");
                if (cardRoot != null && i == _highlightedCardIndex)
                {
                    cardRoot.AddToClassList("highlighted");
                }

                // Register click callback
                int index = i;
                cardInstance.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation(); // Prevent click-away deselect from firing

                    if (evt.clickCount == 2)
                    {
                        OpenInspectorOverlay(cardData);
                    }
                    else
                    {
                        // Toggle highlighted index and refresh hand visuals
                        if (_highlightedCardIndex == index)
                        {
                            _highlightedCardIndex = -1;
                        }
                        else
                        {
                            _highlightedCardIndex = index;
                        }
                        UpdateUI();
                    }
                });

                carousel.Add(cardInstance);
            }
        }

        private void ShowContextualDeploy()
        {
            var actionBar = _root.Q<VisualElement>("contextual-action-bar");
            if (actionBar != null)
            {
                actionBar.RemoveFromClassList("hidden");
                // Show Deploy button only, hide move/attack
                var deployBtn = actionBar.Q<Button>("deploy-btn");
                if (deployBtn != null) deployBtn.style.display = DisplayStyle.Flex;
                var moveBtn = actionBar.Q<Button>("move-btn");
                if (moveBtn != null) moveBtn.style.display = DisplayStyle.None;
                var attackBtn = actionBar.Q<Button>("attack-btn");
                if (attackBtn != null) attackBtn.style.display = DisplayStyle.None;
            }
        }

        public void OpenInspectorOverlay(CardTemplate cardData)
        {
            detailedCardData = cardData;
            isInspectorOverlayOpen = true;
            UpdateUI();
        }

        public void CloseInspectorOverlay()
        {
            isInspectorOverlayOpen = false;
            UpdateUI();
        }

        private void UpdateInspectorOverlay()
        {
            var overlay = _root.Q<VisualElement>("overlay-scrim");
            if (overlay == null) return;

            if (isInspectorOverlayOpen && detailedCardData != null)
            {
                overlay.RemoveFromClassList("hidden");

                // Bind detailed popup fields
                var overlayName = overlay.Q<Label>("overlay-card-name");
                if (overlayName != null) overlayName.text = detailedCardData.cardName.ToUpper();

                var overlayCost = overlay.Q<Label>("overlay-card-cost");
                if (overlayCost != null) overlayCost.text = detailedCardData.treasuryCost.ToString();

                var overlayType = overlay.Q<Label>("overlay-card-type");
                if (overlayType != null) overlayType.text = detailedCardData.cardType.ToString().ToUpper();

                var overlayHealth = overlay.Q<Label>("overlay-card-health");
                if (overlayHealth != null) overlayHealth.text = detailedCardData.health.ToString();

                var overlayAttack = overlay.Q<Label>("overlay-card-attack");
                if (overlayAttack != null) overlayAttack.text = detailedCardData.attack.ToString();

                var overlayRange = overlay.Q<Label>("overlay-card-range");
                if (overlayRange != null) overlayRange.text = detailedCardData.range.ToString();

                var overlayAbility = overlay.Q<Label>("overlay-card-ability");
                if (overlayAbility != null) overlayAbility.text = detailedCardData.abilityText;

                var overlayArt = overlay.Q<VisualElement>("overlay-card-art");
                if (overlayArt != null && detailedCardData.customArt != null)
                    overlayArt.style.backgroundImage = new StyleBackground(detailedCardData.customArt);
            }
            else
            {
                overlay.AddToClassList("hidden");
            }
        }

        private void UpdateHUDVisibility()
        {
            var topHUD = _root.Q<VisualElement>("top-hud");
            var bottomHUD = _root.Q<VisualElement>("bottom-hud");
            var controlsRow = _root.Q<VisualElement>(className: "controls-row");

            if (topHUD == null || bottomHUD == null) return;

            // 1. Hide HUDs entirely during test drag flag (hideHUD)
            if (hideHUD)
            {
                topHUD.AddToClassList("hud-hidden");
                bottomHUD.AddToClassList("hud-hidden");
            }
            else
            {
                topHUD.RemoveFromClassList("hud-hidden");
                bottomHUD.RemoveFromClassList("hud-hidden");
            }

            // 2. Hide hand cards & buttons (controls-row) in Focus Mode, pushing stats to bottom
            if (controlsRow != null)
            {
                if (_hudHidden)
                {
                    controlsRow.AddToClassList("hud-hidden");
                }
                else
                {
                    controlsRow.RemoveFromClassList("hud-hidden");
                }
            }
        }

        private void RegisterEvents()
        {
            if (_root == null) return;

            // Bind Zoom Panel Buttons
            var zoomInBtn = _root.Q<Button>("zoom-in-button");
            if (zoomInBtn != null)
            {
                zoomInBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation(); // Stop click-away deselect
                    _tileScale = Mathf.Min(1.5f, _tileScale + 0.15f);
                    UpdateUI();
                });
            }

            var zoomOutBtn = _root.Q<Button>("zoom-out-button");
            if (zoomOutBtn != null)
            {
                zoomOutBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation(); // Stop click-away deselect
                    _tileScale = Mathf.Max(0.6f, _tileScale - 0.15f);
                    UpdateUI();
                });
            }

            var toggleHudBtn = _root.Q<Button>("toggle-hud-button");
            if (toggleHudBtn != null)
            {
                toggleHudBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation(); // Stop click-away deselect
                    _hudHidden = !_hudHidden;
                    UpdateUI();
                });
            }

            // Reset selection when clicking empty space (bubbles up to root visual element)
            _root.RegisterCallback<ClickEvent>(evt =>
            {
                if (_highlightedCardIndex != -1)
                {
                    _highlightedCardIndex = -1;
                    UpdateUI();
                }
            });

            // Close popup when clicking background scrim
            var overlay = _root.Q<VisualElement>("overlay-scrim");
            if (overlay != null)
            {
                overlay.RegisterCallback<ClickEvent>(evt =>
                {
                    // Ensure we clicked the background, not the card itself
                    if (evt.target == overlay)
                    {
                        CloseInspectorOverlay();
                    }
                });

                var closeBtn = overlay.Q<Button>("overlay-close-btn");
                if (closeBtn != null)
                {
                    closeBtn.clicked += CloseInspectorOverlay;
                }
            }

            // Close contextual buttons when clicking anywhere else
            var endTurnBtn = _root.Q<Button>("end-turn-button");
            if (endTurnBtn != null)
            {
                endTurnBtn.clicked += () =>
                {
                    // Clear active layout states on turn end
                    var actionBar = _root.Q<VisualElement>("contextual-action-bar");
                    if (actionBar != null) actionBar.AddToClassList("hidden");
                    hideHUD = false;
                    UpdateUI();
                    Debug.Log("Turn Ended!");
                };
            }
        }

        private void PopulateBoard()
        {
            var boardScroll = _root.Q<ScrollView>("board-scroll-view");
            if (boardScroll == null) return;

            boardScroll.Clear();

            // Generate 6 rows (1 Enemy Base, 4 Freeplay Neutral, 1 Player Base)
            for (int r = 0; r < 6; r++)
            {
                var rowElement = new VisualElement();
                rowElement.AddToClassList("board-row");

                // Generate 4 columns per row
                for (int c = 0; c < 4; c++)
                {
                    int tileIdx = r * 4 + c;
                    CardTemplate playedCard = _boardTileData[tileIdx];

                    var tileElement = new VisualElement();
                    tileElement.AddToClassList("board-tile");

                    // Apply dynamic zoom scale to tile size and margins
                    float scaledSize = 190f * _tileScale;
                    tileElement.style.width = scaledSize;
                    tileElement.style.height = scaledSize;

                    float scaledMargin = 12f * _tileScale;
                    tileElement.style.marginLeft = scaledMargin;
                    tileElement.style.marginRight = scaledMargin;
                    tileElement.style.marginTop = scaledMargin;
                    tileElement.style.marginBottom = scaledMargin;

                    // Categorize tile styles based on rows
                    if (r == 0) // Top row is enemy base
                    {
                        tileElement.AddToClassList("enemy-tile");
                    }
                    else if (r == 5) // Bottom row is player base
                    {
                        tileElement.AddToClassList("player-tile");
                    }
                    else
                    {
                        tileElement.AddToClassList("neutral-tile");
                    }

                    // Render HP/AT stats bar if it's a base tile OR contains a deployed card
                    bool hasBaseStats = (r == 0 || r == 5);
                    if (playedCard != null || hasBaseStats)
                    {
                        bool isEnemySide = (r == 0);
                        var statsBar = new VisualElement();
                        statsBar.AddToClassList(isEnemySide ? "tile-stats-bar-top" : "tile-stats-bar-bottom");

                        var hpLabel = new Label();
                        hpLabel.AddToClassList("tile-stat-text-left");

                        var artPlaceholder = new VisualElement();
                        artPlaceholder.AddToClassList("tile-art-placeholder");

                        if (playedCard != null)
                        {
                            // Deployed unit stats: HP on left, AT on right with rich text bold numbers
                            bool isBaseRow = (r == 0 || r == 5);
                            if (isBaseRow)
                            {
                                // Merges base durability (30) + unit HP (e.g. 30+10 HP) with unit health in blue
                                hpLabel.text = $"<b>30+<color=#3B82F6>{playedCard.health}</color></b> HP";
                            }
                            else
                            {
                                hpLabel.text = $"<b>{playedCard.health}</b> HP";
                            }
                            
                            var atLabel = new Label($"<b>{playedCard.attack}</b> AT");
                            atLabel.AddToClassList("tile-stat-text-right");

                            statsBar.Add(hpLabel);
                            statsBar.Add(atLabel);

                            if (playedCard.customArt != null)
                            {
                                artPlaceholder.style.backgroundImage = new StyleBackground(playedCard.customArt);
                            }

                            // Trigger springy pull-in swoosh animation when card is spawned on grid
                            statsBar.AddToClassList("tile-deployed-swoosh");
                            artPlaceholder.AddToClassList("tile-deployed-swoosh");

                            tileElement.schedule.Execute(() =>
                            {
                                statsBar.AddToClassList("tile-deployed-active");
                                artPlaceholder.AddToClassList("tile-deployed-active");
                            });
                        }
                        else
                        {
                            // Empty base tile: Display HP only (base durability) with rich text bold number, no AT text
                            hpLabel.text = "<b>30</b> HP";
                            statsBar.Add(hpLabel);
                        }

                        if (isEnemySide)
                        {
                            tileElement.Add(statsBar);
                            tileElement.Add(artPlaceholder);
                        }
                        else
                        {
                            tileElement.Add(artPlaceholder);
                            tileElement.Add(statsBar);
                        }
                    }

                    // Click tile to deploy currently highlighted card
                    int currentTileIdx = tileIdx;
                    tileElement.RegisterCallback<ClickEvent>(evt =>
                    {
                        evt.StopPropagation(); // Prevent click-away deselect from firing

                        if (_highlightedCardIndex != -1 && _highlightedCardIndex < cardsInHand.Count)
                        {
                            var cardToPlay = cardsInHand[_highlightedCardIndex];
                            
                            // Find hand card element to get start coordinates
                            var carousel = _root.Q<ScrollView>("hand-carousel");
                            VisualElement handCardElement = null;
                            if (carousel != null)
                            {
                                int idx = 0;
                                foreach (var child in carousel.Children())
                                {
                                    if (idx == _highlightedCardIndex)
                                    {
                                        handCardElement = child;
                                        break;
                                    }
                                    idx++;
                                }
                            }

                            if (handCardElement != null)
                            {
                                Vector2 startPos = handCardElement.worldBound.position;
                                Vector2 targetPos = tileElement.worldBound.position;
                                float targetSize = tileElement.worldBound.width;

                                // Play flying throw transition effect!
                                PlayFlyingCardAnimation(cardToPlay, startPos, targetPos, targetSize, () =>
                                {
                                    _boardTileData[currentTileIdx] = cardToPlay;
                                    cardsInHand.RemoveAt(_highlightedCardIndex);
                                    _highlightedCardIndex = -1;
                                    UpdateUI();
                                    Debug.Log($"Deployed {cardToPlay.cardName} to Tile {currentTileIdx}!");
                                });
                            }
                            else
                            {
                                _boardTileData[currentTileIdx] = cardToPlay;
                                cardsInHand.RemoveAt(_highlightedCardIndex);
                                _highlightedCardIndex = -1;
                                UpdateUI();
                                Debug.Log($"Deployed {cardToPlay.cardName} to Tile {currentTileIdx}!");
                            }
                        }
                    });

                    rowElement.Add(tileElement);
                }

                boardScroll.Add(rowElement);
            }

            // Always scroll to the bottom (player's side) once layout geometry is calculated
            boardScroll.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                boardScroll.scrollOffset = new Vector2(0f, boardScroll.verticalScroller.highValue);
            });
        }

        private void PlayFlyingCardAnimation(CardTemplate cardData, Vector2 startPos, Vector2 targetPos, float targetSize, System.Action onComplete)
        {
            var flyingContainer = new VisualElement();
            flyingContainer.style.position = Position.Absolute;
            flyingContainer.style.left = startPos.x;
            flyingContainer.style.top = startPos.y;
            flyingContainer.style.width = 200; // Hand card width
            flyingContainer.style.height = 290; // Hand card height

            // Clone layout visual content
            VisualElement cardInstance = cardThumbnailTemplate.Instantiate();
            cardInstance.style.width = Length.Percent(100);
            cardInstance.style.height = Length.Percent(100);

            // Bind cloning stats details
            var thumbName = cardInstance.Q<Label>("card-name");
            if (thumbName != null) thumbName.text = cardData.cardName.ToUpper();

            var thumbCost = cardInstance.Q<Label>("card-cost");
            if (thumbCost != null) thumbCost.text = cardData.treasuryCost.ToString();

            var thumbArt = cardInstance.Q<VisualElement>("card-art");
            if (thumbArt != null && cardData.customArt != null)
                thumbArt.style.backgroundImage = new StyleBackground(cardData.customArt);

            var thumbHp = cardInstance.Q<Label>("card-hp");
            if (thumbHp != null) thumbHp.text = $"<b>{cardData.health}</b> HP";

            var thumbAt = cardInstance.Q<Label>("card-at");
            if (thumbAt != null) thumbAt.text = $"<b>{cardData.attack}</b> AT";

            flyingContainer.Add(cardInstance);
            _root.Add(flyingContainer);

            // Configure transitions (we transition layout geometry for 100% version compatibility)
            flyingContainer.style.transitionProperty = new List<StylePropertyName>
            {
                "left", "top", "width", "height", "opacity"
            };
            flyingContainer.style.transitionDuration = new List<TimeValue>
            {
                new TimeValue(0.4f, TimeUnit.Second)
            };
            flyingContainer.style.transitionTimingFunction = new List<EasingFunction>
            {
                new EasingFunction(EasingMode.EaseInOutBack) // springy bounce throw
            };

            // Animate target frame changes on the next cycle
            flyingContainer.schedule.Execute(() =>
            {
                flyingContainer.style.left = targetPos.x;
                flyingContainer.style.top = targetPos.y;
                flyingContainer.style.width = targetSize;
                flyingContainer.style.height = targetSize * (290f / 200f); // preserve aspect ratio
                flyingContainer.style.opacity = 0.5f;
            });

            // Cleanup container clone and populate target tile after 400ms
            flyingContainer.schedule.Execute(() =>
            {
                _root.Remove(flyingContainer);
                onComplete?.Invoke();
            }).StartingIn(400);
        }
    }
}
