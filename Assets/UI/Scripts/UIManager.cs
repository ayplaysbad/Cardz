using System;
using System.Collections.Generic;
using System.Linq;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using LastFreeCity.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace LastFreeCity.UI
{
    [RequireComponent(typeof(UIDocument))]
    [ExecuteInEditMode]
    public class UIManager : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void CardzRequestWebAppInstall();
#endif

        public event Action<MatchLaunchMode> LaunchModeSelected;
        public event Action<MatchSeat> OnlineQuickMatchRequested;
        public event Action ReconnectBackToMenuRequested;
        public event Action MatchBackToMenuRequested;

        private sealed class ParticipantRuntimeState
        {
            public MatchSeat seat;
            public string cityName;
            public int health;
            public int treasury;
            public int turnStartDrawCount;
            public int maxHandSize = 6;
            public int baseTreasuryIncome = DefaultBaseTreasuryIncome;
            public int deployTurnsTaken;
            public bool lastDrawRepooledDiscard;
            public int lastDeckRefillRealHandIndex = -1;
            public readonly List<CardTemplate> hand = new List<CardTemplate>();
            public readonly List<CardTemplate> drawPile = new List<CardTemplate>();
            public readonly List<CardTemplate> discardPile = new List<CardTemplate>();
            public readonly List<CardTemplate> burnPile = new List<CardTemplate>();
        }

        private sealed class SeatTransientUiState
        {
            public int highlightedCardIndex = -1;
            public int selectedBoardTileIndex = -1;
            public int selectedAttackerTileIndex = -1;
            public int selectedWarShopOption = -1;
            public bool placementFocusActive;
            public CardTemplate abilityPreviewCard;
            public string abilityPreviewText = string.Empty;
        }

        private sealed class FloatingBoardTextRuntime
        {
            public int tileIndex = -1;
            public string text = string.Empty;
            public float expiresAt;
            public string cssClass = "tile-floating-damage";
        }

        private enum MatchRoundPhase
        {
            DeployPlanning,
            CombatPlanning,
            DisplayResolution
        }

        private enum DisplayResolutionMode
        {
            Movement,
            Attack
        }

        private enum PileViewerKind
        {
            None,
            Deck,
            Discard
        }

        private enum WarShopOption
        {
            None = -1,
            FieldMedic = 0,
            BombDrop = 1,
            FrontierClaim = 2,
            RebuildOrder = 3
        }

        private enum RemovedCardFateOverride
        {
            None,
            Discard,
            Burn
        }

        private sealed class DisplayAttackStepRuntime
        {
            public int sourceTileIndex = -1;
            public MatchSeat seat;
        }

        private sealed class DisplayStruggleStepRuntime
        {
            public int winnerSourceTileIndex = -1;
            public int loserSourceTileIndex = -1;
            public int contestedTileIndex = -1;
            public MatchSeat winnerSeat;
        }

        private sealed class DisplayMoveStepRuntime
        {
            public int sourceTileIndex = -1;
            public int targetTileIndex = -1;
            public MatchSeat seat;
        }

        private sealed class EncyclopediaSectionData
        {
            public string TabLabel;
            public string Title;
            public string Body;
        }

        private const int DefaultBoardRows = 6;
        private const int DefaultBoardColumns = 4;
        private const float MinTileScale = 0.42f;
        private const float DesktopMinTileScale = 0.2f;
        private const float DesktopBoardFitScaleFactor = 1.0f;
        private const float MaxTileScale = 2.4f;
        private const float ZoomStep = 0.08f;
        private const float DesktopDockCenterStageWidth = 1080f;
        private const float DesktopStageDesignWidth = 1080f;
        private const float DesktopStageDesignHeight = 1360f;
        private const float DesktopDockMinWidth = 180f;
        private const float DesktopDockMaxWidth = 205f;
        private const float DesktopDockGapWidth = 6f;
        private const float DesktopDockMinViewportWidth = 1440f;
        private const float DesktopDockMinAspectRatio = 1.2f;
        private const float DesktopStageViewportPadding = 4f;
        private const float TileBaseWidth = 380f;
        private const float TileBaseHeight = 320f;
        private const float TileBaseMargin = 8f;
        private const float BoardPanDragThreshold = 8f;
        private const float BoardViewportPaddingLeft = 0f;
        private const float BoardViewportPaddingTop = 140f;
        private const float BoardViewportPaddingBottom = 250f;
        private const float BoardFitPaddingX = 0f;
        private const float BoardFitPaddingY = 32f;
        private const int LocksPerTurn = 2;
        private const int ManualCityAttackTargetToken = -2;
        private const int WarShopFieldMedicCost = 35;
        private const int WarShopBombDropCost = 40;
        private const int WarShopFrontierClaimCost = 70;
        private const int WarShopRebuildOrderCost = 55;
        private const int WarShopFrontierClaimHealth = 10;
        private const int BelfryDeploysPerSpawn = 3;
        private const int BelfryTokenAttack = 5;
        private const float FloatingTextDurationSeconds = 2.2f;
        private const float DeployPhaseDurationSeconds = 60f;
        private const float AttackPhaseDurationSeconds = 30f;
        private const float DisplayAttackStepSeconds = 1.7f;
        private const float DisplayMovementDelaySeconds = 1.55f;
        private const float CityFlashDurationSeconds = 0.9f;
        private const float AbilityMarqueeSpeed = 32f;
        private const float AbilityMarqueePauseSeconds = 0.9f;
        private const float RoundAnnouncementLeadSeconds = 2.6f;
        private const float RoundAnnouncementDisplaySeconds = 3.3f;
        private const float ArenaMismatchCountdownSeconds = 3f;
        private const int DefaultMaxRealHandSize = 6;
        private const int DefaultBaseTreasuryIncome = 6;
        private const float CardHoldDetailSeconds = 0.5f;
        private static readonly string[] SpecialGeneratedArtClasses =
        {
            "generated-art-warshop-field-medic",
            "generated-art-warshop-bomb-drop",
            "generated-art-warshop-frontier-claim",
            "generated-art-warshop-rebuild-order"
        };
        [Header("Player Stats")]
        public string playerCityName = "FREE HAVEN";
        [Range(0, 100)] public int playerStability = 100;
        public int playerTreasury = 50;
        public int deckRemainingCount = 24;
        public int discardPileCount = 0;

        [Header("Enemy Stats")]
        public string enemyCityName = "IRON CITADEL";
        [Range(0, 100)] public int enemyStability = 100;
        public int enemyTreasury = 50;

        [Header("Card Hand Data")]
        public List<CardTemplate> cardsInHand = new List<CardTemplate>();
        public VisualTreeAsset cardThumbnailTemplate; // UXML for small thumbnail card

        [Header("Real Match Definitions")]
        public MatchPrototypeDefinition prototypeMatch;

        [Header("Round Indicator Sprites")]
        [SerializeField] private List<Sprite> roundIndicatorSprites = new List<Sprite>();

        [Header("Active Selection / Inspector Popup")]
        public CardTemplate detailedCardData;
        public bool isInspectorOverlayOpen = false;

        [Header("Interactive Testing Triggers")]
        [Tooltip("Hide/Show HUD (simulates dragging card or selecting unit)")]
        public bool hideHUD = false;

        private UIDocument _uiDocument;
        private VisualElement _root;

        private int _highlightedCardIndex = -1;
        private int _selectedBoardTileIndex = -1;
        private int _selectedAttackerTileIndex = -1;
        private int _selectedWarShopOption = -1;
        private CardTemplate _abilityPreviewCard;
        private string _abilityPreviewText = string.Empty;
        private CardTemplate[] _boardTileData = new CardTemplate[DefaultBoardRows * DefaultBoardColumns];
        private MatchSeat?[] _tileOccupantSeats = new MatchSeat?[DefaultBoardRows * DefaultBoardColumns];
        private int[] _occupantCurrentHealth = new int[DefaultBoardRows * DefaultBoardColumns];
        private int[] _tileCurrentHealth = new int[DefaultBoardRows * DefaultBoardColumns];
        private int[] _tileMaxHealth = new int[DefaultBoardRows * DefaultBoardColumns];
        private TileAreaKind[] _tileAreaKinds = new TileAreaKind[DefaultBoardRows * DefaultBoardColumns];
        private TileOwner[] _tileOwners = new TileOwner[DefaultBoardRows * DefaultBoardColumns];
        private bool[] _tileBlocksCity = new bool[DefaultBoardRows * DefaultBoardColumns];
        private bool[] _tileLocked = new bool[DefaultBoardRows * DefaultBoardColumns];
        private int[] _attackTargetTileBySource = new int[DefaultBoardRows * DefaultBoardColumns];
        private int[] _moveTargetTileBySource = new int[DefaultBoardRows * DefaultBoardColumns];
        private readonly List<FloatingBoardTextRuntime> _floatingBoardTexts = new List<FloatingBoardTextRuntime>();
        private int[] _previewOccupantHealth = new int[DefaultBoardRows * DefaultBoardColumns];
        private int[] _previewTileHealth = new int[DefaultBoardRows * DefaultBoardColumns];
        private int _previewSeatOneCityHealth;
        private int _previewSeatTwoCityHealth;
        private int[] _previewAttackDamageBySource = new int[DefaultBoardRows * DefaultBoardColumns];
        private int[] _displayAutoTargetTileBySource = new int[DefaultBoardRows * DefaultBoardColumns];
        private int[] _previewMoveTargetTileBySource = new int[DefaultBoardRows * DefaultBoardColumns];
        private int[] _previewResolvedMoveTargetBySource = new int[DefaultBoardRows * DefaultBoardColumns];
        private int[] _previewMovementOccupantHealth = new int[DefaultBoardRows * DefaultBoardColumns];
        private bool[] _previewMoveTargetContestedBySource = new bool[DefaultBoardRows * DefaultBoardColumns];
        private int[] _secureHoldTurnsByTile = new int[DefaultBoardRows * DefaultBoardColumns];
        private int[] _silenceTurnsByTile = new int[DefaultBoardRows * DefaultBoardColumns];
        private int[] _spawnChargeTurnsByTile = new int[DefaultBoardRows * DefaultBoardColumns];
        private bool[] _displayMovementConsumedByTile = new bool[DefaultBoardRows * DefaultBoardColumns];
        private int[] _interceptConsumedByTile = new int[DefaultBoardRows * DefaultBoardColumns];
        private int[] _previewInterceptConsumedByTile = new int[DefaultBoardRows * DefaultBoardColumns];
        private bool[] _movementPhaseStartingLocks = new bool[DefaultBoardRows * DefaultBoardColumns];
        private readonly bool[] _warShopPurchaseUsedBySeat = new bool[2];
        private readonly List<DisplayAttackStepRuntime> _displayAttackQueue = new List<DisplayAttackStepRuntime>();
        private readonly List<DisplayStruggleStepRuntime> _displayStruggleQueue = new List<DisplayStruggleStepRuntime>();
        private readonly List<DisplayMoveStepRuntime> _displayMoveQueue = new List<DisplayMoveStepRuntime>();
        private ScrollView _boardScrollView;
        private VisualElement _boardSurfaceElement;
        private VisualElement _boardOwnershipFrameElement;
        private VisualElement _boardOwnershipTimerLayerElement;
        private VisualElement _boardOwnershipTimerTopElement;
        private VisualElement _boardOwnershipTimerRightElement;
        private VisualElement _boardOwnershipTimerBottomElement;
        private VisualElement _boardOwnershipTimerLeftElement;
        private VisualElement _boardGridLayerElement;
        private VisualElement _boardEffectsLayerElement;
        private VisualElement _boardMotionLayerElement;
        private VisualElement[] _boardRowElements;
        private VisualElement[] _boardTileElements;
        private VisualElement[] _boardTileTextureLayers;
        private VisualElement[] _boardTileAreaOverlays;
        private VisualElement[] _boardTileOwnershipFrames;
        private VisualElement[] _boardTileSelectionGlows;
        private VisualElement[] _boardTileStatsBars;
        private Label[] _boardTileHpLabels;
        private VisualElement[] _boardTileCardContents;
        private VisualElement[] _boardTileArtPlaceholders;
        private Label[] _boardTileNameLabels;
        private Label[] _boardTileAttackLabels;
        private VisualElement[] _boardTileRightStatClusters;
        private Label[] _boardTileLockLabels;
        private Label[] _boardTileAbilityLabels;
        private Label[] _boardTileItemLabels;
        private Label[] _boardTileIntentBadges;
        private Label[] _boardTileInvalidMarkers;
        private Label[] _boardTileDoomMarkers;
        private int _boardVisualTileCount = -1;
        private string _lastHandCarouselSignature = string.Empty;
        private int _lastHandCarouselHighlightIndex = int.MinValue;
        private MatchRoundPhase _lastHandCarouselPhase = (MatchRoundPhase)(-1);
        private PileViewerKind _pileViewerKind = PileViewerKind.None;
        private string _lastPileViewerSignature = string.Empty;
        private int _lastRenderedDeckCount = -1;
        private int _lastRenderedDiscardCount = -1;
        private int _nextHandEntryRepoolRealIndex = -1;
        private int _nextHandEntryRepoolDelayMs = 0;
        private bool _suppressNextDeckCountBounce = false;
        private bool _suppressNextDiscardCountBounce = false;
        private bool _cardHoldDetailOpened = false;

        private float _tileScale = 1.0f;
        private bool _hudHidden = false;
        private bool _placementFocusActive = false;
        private bool _cardDeployInFlight = false;
        private bool _eventsRegistered = false;
        private bool _matchInitialized = false;
        private bool _boardViewNeedsReset = true;
        private bool _boardPanActive = false;
        private bool _boardPanMoved = false;
        private bool _suppressNextBoardClick = false;
        private Rect _lastSafeArea = new Rect(-1f, -1f, -1f, -1f);
        private Vector2Int _lastScreenSize = new Vector2Int(-1, -1);
        private int _boardRows = DefaultBoardRows;
        private int _boardColumns = DefaultBoardColumns;
        private int _boardPanPointerId = -1;
        private int _boardViewResetAttempts = 0;
        private MatchSeat _localSeat = MatchSeat.SeatOne;
        private MatchSeat _perspectiveSeat = MatchSeat.SeatOne;
        private MatchSeat _canonicalTopSeat = MatchSeat.SeatTwo;
        private MatchSeat _activeTurnSeat = MatchSeat.SeatOne;
        private MatchSeat _roundInitiativeSeat = MatchSeat.SeatOne;
        private MatchControlMode _controlMode = MatchControlMode.Hotseat;
        private MatchControlMode? _startupControlModeOverride;
        private MatchLaunchMode _selectedLaunchMode = MatchLaunchMode.None;
        private MatchSeat _selectedOnlineSeat = MatchSeat.SeatOne;
        private int _roundNumber = 0;
        private MatchRoundPhase _roundPhase = MatchRoundPhase.DeployPlanning;
        private bool _hotseatTestMode = false;
        private bool _awaitingLaunchModeSelection = true;
        private bool _displayMovementResolved = false;
        private Vector2 _boardPanPointerStart;
        private Vector2 _boardPanScrollStart;
        private float _phaseEndsAtUnscaledTime = -1f;
        private float _nextDisplayActionAtUnscaledTime = -1f;
        private int _displayAttackQueueIndex = 0;
        private int _displayStruggleQueueIndex = 0;
        private int _displayMoveQueueIndex = 0;
        private bool _displayStrugglePrepared = false;
        private bool _displayAttackPrepared = false;
        private bool _displayMovePrepared = false;
        private DisplayResolutionMode _displayResolutionMode = DisplayResolutionMode.Attack;
        private bool _phaseAdvanceDelayInProgress = false;
        private bool _phaseAdvanceContinuationRunning = false;
        private string _displayStageLabel = string.Empty;
        private MatchSeat? _displayStageSeat;
        private float _seatOneCityFlashExpiresAt = -1f;
        private float _seatTwoCityFlashExpiresAt = -1f;
        private string _displayNarrationText = string.Empty;
        private string _awarenessOverrideText = string.Empty;
        private float _awarenessOverrideExpiresAt = -1f;
        private string _lastAbilityPreviewMarkup = string.Empty;
        private float _abilityPreviewMarqueeStartTime = -1f;
        private float _autoAdvanceAtUnscaledTime = -1f;
        private MatchRoundPhase _autoAdvancePhase = MatchRoundPhase.DeployPlanning;
        private MatchSeat _autoAdvanceSeat = MatchSeat.SeatOne;
        private ParticipantRuntimeState _seatOneState;
        private ParticipantRuntimeState _seatTwoState;
        private readonly SeatTransientUiState _seatOneTransientUiState = new SeatTransientUiState();
        private readonly SeatTransientUiState _seatTwoTransientUiState = new SeatTransientUiState();
        private bool _isInitializingMatchRuntime = false;
        private bool _skipAttackDisplayThisRound = false;
        private bool _isRefreshingMovementPreview = false;
        private bool _isApplyingRemoteSeatAction = false;
        private bool _preserveCanonicalBoardView = false;
        private readonly HashSet<int> _resolveAnimationHiddenTiles = new HashSet<int>();
        private VisualElement _activeResolveMotionProxy;
        private int _resolveMotionAnimationSerial = 0;
        private IMatchUiCommandSink _externalCommandSink;
        private string _launchModeStatusText = "Start with the field guide, a local test, or online play.";
        private bool _reconnectOverlayVisible = false;
        private string _reconnectMessageText = string.Empty;
        private int _reconnectSecondsRemaining = 30;
        private bool _arenaSelectionActive = false;
        private bool _arenaMismatchCountdownActive = false;
        private ArenaId _selectedArena = ArenaId.None;
        private ArenaId _seatOneArenaVote = ArenaId.None;
        private ArenaId _seatTwoArenaVote = ArenaId.None;
        private float _arenaResolveAtUnscaledTime = -1f;
        private bool _matchEnded = false;
        private MatchSeat _winningSeat = MatchSeat.SeatOne;
        private string _matchEndMessage = string.Empty;
        private bool _warShopOverlayOpen = false;
        private bool _encyclopediaOpen = false;
        private int _encyclopediaTabIndex = 0;
        private bool _launchModeOnlineCityStepActive = false;
        private bool _webAdminMode = false;
        private bool _desktopDockLayoutActive = false;
        private bool _mobileLeftDockOpen = false;
        private bool _mobileRightDockOpen = false;

        public bool IsDisplayResolutionActive => _roundPhase == MatchRoundPhase.DisplayResolution;

        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();
            RefreshWebLaunchContext();
            UpdateUI();
        }

        private void Start()
        {
            RefreshWebLaunchContext();
            UpdateUI();
            RegisterEvents();

            var boardScroll = _root?.Q<ScrollView>("board-scroll-view");
            if (boardScroll != null && !_awaitingLaunchModeSelection)
            {
                boardScroll.schedule.Execute(() => RequestBoardFitAndCenter(true)).StartingIn(32);
            }
        }

        private void OnValidate()
        {
            EnsureRoundIndicatorSpritesLoaded();
            _matchInitialized = false;
            _boardViewNeedsReset = true;
            UpdateUI();
        }

        private void Update()
        {
            // Simple polling for runtime visual testing
            if (Application.isPlaying && _root != null)
            {
                ApplySafeAreaIfNeeded();
                if (_awaitingLaunchModeSelection || _reconnectOverlayVisible)
                {
                    return;
                }

                if (_arenaSelectionActive)
                {
                    bool wasArenaSelectionActive = _arenaSelectionActive;
                    TickArenaSelection();
                    if (wasArenaSelectionActive && !_arenaSelectionActive)
                    {
                        UpdateUI();
                        return;
                    }

                    UpdateArenaSelectionOverlay();
                    return;
                }

                UpdateHUDVisibility();
                UpdatePhaseTimerUI();
                UpdateCityDamageFlashUI();
                UpdateAbilityPreviewMarquee();

                bool removedExpiredFloatingText = false;
                bool awarenessChanged = false;
                float now = Time.unscaledTime;
                for (int i = _floatingBoardTexts.Count - 1; i >= 0; i--)
                {
                    if (_floatingBoardTexts[i].expiresAt > now)
                    {
                        continue;
                    }

                    _floatingBoardTexts.RemoveAt(i);
                    removedExpiredFloatingText = true;
                }

                if (removedExpiredFloatingText)
                {
                    UpdateUI();
                }

                if (!string.IsNullOrWhiteSpace(_awarenessOverrideText)
                    && _awarenessOverrideExpiresAt > 0f
                    && now >= _awarenessOverrideExpiresAt)
                {
                    _awarenessOverrideText = string.Empty;
                    _awarenessOverrideExpiresAt = -1f;
                    awarenessChanged = true;
                }

                bool isRemoteReplica = _externalCommandSink != null;

                if (!isRemoteReplica && _autoAdvanceAtUnscaledTime > 0f)
                {
                    if (_roundPhase != _autoAdvancePhase || _activeTurnSeat != _autoAdvanceSeat)
                    {
                        _autoAdvanceAtUnscaledTime = -1f;
                    }
                    else if (now >= _autoAdvanceAtUnscaledTime)
                    {
                        _autoAdvanceAtUnscaledTime = -1f;
                        AdvancePhaseFromReadyOrTimeout();
                        return;
                    }
                }

                if (awarenessChanged)
                {
                    UpdateUI();
                }

                if (!_matchEnded && (!isRemoteReplica || _roundPhase == MatchRoundPhase.DisplayResolution))
                {
                    TickRoundPhaseTimersAndDisplay();
                }
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

            if (_isApplyingRemoteSeatAction)
            {
                return;
            }

            UpdateArenaBackgroundClass();
            UpdateLaunchModeOverlay();
            UpdateReconnectOverlay();
            UpdateArenaSelectionOverlay();
            UpdateMatchEndOverlay();
            UpdateGameplayVisibilityForLaunchState();
            UpdateEncyclopediaOverlay();

            if (_awaitingLaunchModeSelection || _reconnectOverlayVisible || _arenaSelectionActive)
            {
                return;
            }

            InitializeMatchRuntimeIfNeeded();
            if (!_matchInitialized)
            {
                return;
            }

            bool shouldCaptureLocalTransientState = _externalCommandSink == null
                || (_roundPhase != MatchRoundPhase.DisplayResolution && _activeTurnSeat == _localSeat);
            if (shouldCaptureLocalTransientState)
            {
                CaptureCurrentTransientUiState(_localSeat);
            }

            SyncVisibleStateFromPerspective();
            RepairBoardOccupantSeatData();
            ApplySafeAreaIfNeeded();
            UpdateDesktopDockLayout();
            RefreshCombatPreviewState();
            RefreshMovementPreviewState();
            EnsureRoundIndicatorSpritesLoaded();

            // Bind Player HUD
            SetText("player-city-nameplate", playerCityName.ToUpper());
            SetText("player-stability", GetRenderedCityHealth(_perspectiveSeat).ToString());
            SetText("player-treasury", playerTreasury.ToString());
            SetText("player-treasury-income", FormatTreasuryIncome(GetTreasuryIncomeForSeat(_perspectiveSeat)));
            SetText("deck-count", deckRemainingCount.ToString());
            SetText("discard-count", discardPileCount.ToString());
            UpdatePileButtonBounce("deck-container", deckRemainingCount, ref _lastRenderedDeckCount, ref _suppressNextDeckCountBounce);
            UpdatePileButtonBounce("discard-container", discardPileCount, ref _lastRenderedDiscardCount, ref _suppressNextDiscardCountBounce);

            // Bind Enemy HUD
            SetText("enemy-city-nameplate", enemyCityName.ToUpper());
            SetText("enemy-stability", GetRenderedCityHealth(MatchPerspectiveUtility.GetOpposingSeat(_perspectiveSeat)).ToString());
            SetText("enemy-treasury", enemyTreasury.ToString());
            SetText("enemy-treasury-income", FormatTreasuryIncome(GetTreasuryIncomeForSeat(MatchPerspectiveUtility.GetOpposingSeat(_perspectiveSeat))));
            UpdateDominantRoundIndicators();

            // Bind Cards Hand
            PopulateHandCarousel();

            // Bind Board Grid Tiles
            PopulateBoard();

            // Bind contextual actions
            UpdateContextualActionBar();

            // Bind Inspector Overlay Details
            UpdateInspectorOverlay();

            // Bind ability preview message
            UpdateAbilityPreview();

            // Bind city phase badges
            UpdateCityPhaseIndicators();

            // Update phase timer and action button
            UpdatePhaseTimerUI();
            UpdatePrimaryActionButton();
            UpdateCityDamageFlashUI();
            UpdatePileViewer();
            UpdateWarShopUi();

            // Update Visibility States
            UpdateHUDVisibility();

            // Re-apply board fit/centering after the full HUD layout has settled.
            ResetBoardViewToPlayerAnchorIfNeeded();

            // Grey out zoom buttons at scale limits or when hitting screen width constraints
            var zoomInBtn = _root.Q<Button>("zoom-in-button");
            var zoomOutBtn = _root.Q<Button>("zoom-out-button");
            if (zoomInBtn != null)
            {
                zoomInBtn.SetEnabled(_tileScale < MaxTileScale - 0.01f);
            }
            if (zoomOutBtn != null)
            {
                zoomOutBtn.SetEnabled(_tileScale > GetCurrentMinTileScale() + 0.01f);
            }
        }

        private void SetText(string nameQuery, string textValue)
        {
            var label = _root.Q<Label>(nameQuery);
            if (label != null)
            {
                label.text = textValue;
            }
        }

        public void ShowLaunchModePicker(string statusText = null)
        {
            _awaitingLaunchModeSelection = true;
            _reconnectOverlayVisible = false;
            _encyclopediaOpen = false;
            _selectedLaunchMode = MatchLaunchMode.None;
            _launchModeOnlineCityStepActive = false;
            _arenaSelectionActive = false;
            _arenaMismatchCountdownActive = false;
            _selectedArena = ArenaId.None;
            _seatOneArenaVote = ArenaId.None;
            _seatTwoArenaVote = ArenaId.None;
            _arenaResolveAtUnscaledTime = -1f;
            _matchEnded = false;
            _matchEndMessage = string.Empty;
            _launchModeStatusText = string.IsNullOrWhiteSpace(statusText)
                ? "Start with the field guide, a local test, or online play."
                : statusText;
            UpdateUI();
        }

        public void ShowReconnectWait(string message, int secondsRemaining)
        {
            _reconnectOverlayVisible = true;
            _encyclopediaOpen = false;
            _reconnectMessageText = string.IsNullOrWhiteSpace(message)
                ? "Trying to keep this match alive."
                : message;
            _reconnectSecondsRemaining = Mathf.Max(0, secondsRemaining);
            isInspectorOverlayOpen = false;
            UpdateUI();
        }

        public void HideReconnectWait()
        {
            if (!_reconnectOverlayVisible)
            {
                return;
            }

            _reconnectOverlayVisible = false;
            UpdateUI();
        }

        public void SetLaunchModeStatus(string statusText)
        {
            _launchModeStatusText = string.IsNullOrWhiteSpace(statusText)
                ? "Start with the field guide, a local test, or online play."
                : statusText;
            UpdateUI();
        }

        public void StartTurnBasedSession()
        {
            _selectedLaunchMode = MatchLaunchMode.TurnBased;
            _startupControlModeOverride = MatchControlMode.Hotseat;
            _controlMode = MatchControlMode.Hotseat;
            _preserveCanonicalBoardView = false;
            _externalCommandSink = null;
            if (prototypeMatch != null)
            {
                prototypeMatch.hotseatTestMode = true;
                prototypeMatch.defaultControlMode = MatchControlMode.Hotseat;
            }
            CompleteLaunchModeSelection();
            BeginArenaSelection();
        }

        public void StartTestingSession()
        {
            _selectedLaunchMode = MatchLaunchMode.Testing;
            _startupControlModeOverride = MatchControlMode.Hotseat;
            _controlMode = MatchControlMode.Hotseat;
            _preserveCanonicalBoardView = false;
            _externalCommandSink = null;
            if (prototypeMatch != null)
            {
                prototypeMatch.hotseatTestMode = true;
                prototypeMatch.defaultControlMode = MatchControlMode.Hotseat;
            }
            CompleteLaunchModeSelection();
            BeginArenaSelection();
        }

        public void BeginSeatAssignedSession(MatchSeat localSeat)
        {
            if (_selectedLaunchMode == MatchLaunchMode.None)
            {
                _selectedLaunchMode = MatchLaunchMode.MultiplayerHost;
            }

            _startupControlModeOverride = MatchControlMode.SeatAssigned;
            _controlMode = MatchControlMode.SeatAssigned;
            _preserveCanonicalBoardView = false;
            _localSeat = localSeat;
            _perspectiveSeat = localSeat;
            if (prototypeMatch != null)
            {
                prototypeMatch.localSeat = localSeat;
                prototypeMatch.hotseatTestMode = false;
                prototypeMatch.defaultControlMode = MatchControlMode.SeatAssigned;
            }
            CompleteLaunchModeSelection();
            BeginArenaSelection();
        }

        public void BeginArenaSelection()
        {
            _arenaSelectionActive = true;
            _arenaMismatchCountdownActive = false;
            _seatOneArenaVote = ArenaId.None;
            _seatTwoArenaVote = ArenaId.None;
            _arenaResolveAtUnscaledTime = -1f;
            isInspectorOverlayOpen = false;
            UpdateUI();
        }

        private void CompleteLaunchModeSelection()
        {
            _awaitingLaunchModeSelection = false;
            _launchModeStatusText = string.Empty;
            ResetRuntimeForNewSession();
            UpdateUI();

            if (Application.isPlaying)
            {
                var boardScroll = _root?.Q<ScrollView>("board-scroll-view");
                if (boardScroll != null)
                {
                    boardScroll.schedule.Execute(() => RequestBoardFitAndCenter(true)).StartingIn(32);
                }
            }
        }

        private void UpdateLaunchModeOverlay()
        {
            var overlay = _root.Q<VisualElement>("game-mode-overlay");
            if (overlay == null)
            {
                return;
            }

            bool showModeOverlay = _awaitingLaunchModeSelection
                && !_reconnectOverlayVisible
                && !_encyclopediaOpen;
            overlay.style.display = showModeOverlay ? DisplayStyle.Flex : DisplayStyle.None;
            overlay.pickingMode = showModeOverlay ? PickingMode.Position : PickingMode.Ignore;

            var statusLabel = overlay.Q<Label>("game-mode-status");
            if (statusLabel != null)
            {
                statusLabel.text = _launchModeStatusText;
            }

            var testingButton = overlay.Q<Button>("mode-testing-button");
            if (testingButton != null)
            {
                bool showTesting = ShouldExposeTestingMode();
                testingButton.style.display = showTesting ? DisplayStyle.Flex : DisplayStyle.None;
                testingButton.pickingMode = showTesting ? PickingMode.Position : PickingMode.Ignore;
            }

            var citySection = overlay.Q<VisualElement>("mode-city-section");
            if (citySection != null)
            {
                citySection.style.display = _launchModeOnlineCityStepActive ? DisplayStyle.Flex : DisplayStyle.None;
                citySection.pickingMode = _launchModeOnlineCityStepActive ? PickingMode.Position : PickingMode.Ignore;
            }

            UpdateCitySelectionButtons(overlay);

#if UNITY_WEBGL && !UNITY_EDITOR
            var installButton = overlay.Q<Button>("mode-install-app-button");
            if (installButton != null)
            {
                installButton.style.display = DisplayStyle.Flex;
            }
#else
            var installButton = overlay.Q<Button>("mode-install-app-button");
            if (installButton != null)
            {
                installButton.style.display = DisplayStyle.None;
            }
#endif
        }

        private void RefreshWebLaunchContext()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            _webAdminMode = UrlFlagEnabled(Application.absoluteURL, "admin");
#else
            _webAdminMode = true;
#endif
        }

        private bool ShouldExposeTestingMode()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return _webAdminMode;
#else
            return true;
#endif
        }

        private static bool UrlFlagEnabled(string url, string key)
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            int queryIndex = url.IndexOf('?');
            if (queryIndex < 0 || queryIndex >= url.Length - 1)
            {
                return false;
            }

            string query = url.Substring(queryIndex + 1);
            int fragmentIndex = query.IndexOf('#');
            if (fragmentIndex >= 0)
            {
                query = query.Substring(0, fragmentIndex);
            }

            string[] parts = query.Split('&');
            foreach (string part in parts)
            {
                if (string.IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                string[] pair = part.Split(new[] { '=' }, 2);
                string paramName = Uri.UnescapeDataString(pair[0]);
                if (!string.Equals(paramName, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string value = pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : "1";
                return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private void UpdateCitySelectionButtons(VisualElement overlay)
        {
            var freehavenButton = overlay.Q<Button>("mode-city-freehaven-button");
            var citadelButton = overlay.Q<Button>("mode-city-citadel-button");
            freehavenButton?.EnableInClassList("city-select-active", _selectedOnlineSeat == MatchSeat.SeatOne);
            citadelButton?.EnableInClassList("city-select-active", _selectedOnlineSeat == MatchSeat.SeatTwo);
        }

        private void UpdateReconnectOverlay()
        {
            var overlay = _root.Q<VisualElement>("reconnect-overlay");
            if (overlay == null)
            {
                return;
            }

            overlay.style.display = _reconnectOverlayVisible ? DisplayStyle.Flex : DisplayStyle.None;
            overlay.pickingMode = _reconnectOverlayVisible ? PickingMode.Position : PickingMode.Ignore;

            var messageLabel = overlay.Q<Label>("reconnect-message");
            if (messageLabel != null)
            {
                messageLabel.text = _reconnectMessageText;
            }

            var countdownLabel = overlay.Q<Label>("reconnect-countdown");
            if (countdownLabel != null)
            {
                countdownLabel.text = _reconnectSecondsRemaining.ToString();
            }
        }

        private void UpdateMatchEndOverlay()
        {
            var overlay = _root.Q<VisualElement>("match-end-overlay");
            if (overlay == null)
            {
                return;
            }

            bool showOverlay = _matchEnded && !_awaitingLaunchModeSelection && !_reconnectOverlayVisible;
            overlay.style.display = showOverlay ? DisplayStyle.Flex : DisplayStyle.None;
            overlay.pickingMode = showOverlay ? PickingMode.Position : PickingMode.Ignore;

            var winnerLabel = overlay.Q<Label>("match-end-winner");
            if (winnerLabel != null)
            {
                winnerLabel.text = $"{GetSeatDisplayName(_winningSeat).ToUpper()} WINS";
            }

            var messageLabel = overlay.Q<Label>("match-end-message");
            if (messageLabel != null)
            {
                messageLabel.text = string.IsNullOrWhiteSpace(_matchEndMessage)
                    ? "A city has fallen. Return to menu to start the next test."
                    : _matchEndMessage;
            }
        }

        private void UpdateArenaSelectionOverlay()
        {
            var overlay = _root.Q<VisualElement>("arena-selection-overlay");
            if (overlay == null)
            {
                return;
            }

            bool showArenaOverlay = !_awaitingLaunchModeSelection && !_reconnectOverlayVisible && _arenaSelectionActive;
            overlay.style.display = showArenaOverlay ? DisplayStyle.Flex : DisplayStyle.None;
            overlay.pickingMode = showArenaOverlay ? PickingMode.Position : PickingMode.Ignore;

            var statusLabel = overlay.Q<Label>("arena-selection-status");
            if (statusLabel != null)
            {
                statusLabel.text = GetArenaSelectionStatusText();
            }

            UpdateArenaChoiceButton(overlay, "arena-freehaven-garden-button", ArenaId.FreehavenGarden);
            UpdateArenaChoiceButton(overlay, "arena-citadel-training-button", ArenaId.CitadelTrainingGrounds);
        }

        private void UpdateArenaChoiceButton(VisualElement overlay, string buttonName, ArenaId arenaId)
        {
            var button = overlay.Q<Button>(buttonName);
            if (button == null)
            {
                return;
            }

            bool localVote = GetArenaVoteForSeat(_localSeat) == arenaId;
            bool enemyVote = GetArenaVoteForSeat(MatchPerspectiveUtility.GetOpposingSeat(_localSeat)) == arenaId;
            button.EnableInClassList("arena-choice-local-vote", localVote);
            button.EnableInClassList("arena-choice-enemy-vote", enemyVote);
            button.SetEnabled(_arenaSelectionActive && !_arenaMismatchCountdownActive);
        }

        private string GetArenaSelectionStatusText()
        {
            if (_controlMode == MatchControlMode.Hotseat)
            {
                return "Pick the arena for this test match.";
            }

            if (_arenaMismatchCountdownActive)
            {
                int seconds = Mathf.Max(0, Mathf.CeilToInt(GetArenaCountdownRemainingSeconds()));
                return $"Different arenas picked. Randomising in {seconds}...";
            }

            if (GetArenaVoteForSeat(_localSeat) != ArenaId.None)
            {
                return "Waiting for the other city to choose...";
            }

            return "Choose where this fight is happening.";
        }

        private void UpdateArenaBackgroundClass()
        {
            var canvas = _root.Q<VisualElement>("main-canvas");
            if (canvas == null)
            {
                return;
            }

            canvas.EnableInClassList("arena-bg-freehaven-garden", _selectedArena == ArenaId.FreehavenGarden);
            canvas.EnableInClassList("arena-bg-citadel-training", _selectedArena == ArenaId.CitadelTrainingGrounds);
        }

        private void UpdateGameplayVisibilityForLaunchState()
        {
            bool showGameplay = !_awaitingLaunchModeSelection && !_reconnectOverlayVisible && !_arenaSelectionActive;
            SetDisplayForRootElement("top-hud", showGameplay);
            SetDisplayForRootElement("play-area-container", showGameplay);
            SetDisplayForRootElement("bottom-hud", showGameplay);
            SetDisplayForRootElement("overlay-scrim", showGameplay && isInspectorOverlayOpen && !_matchEnded);
        }

        private void SetDisplayForRootElement(string elementName, bool visible)
        {
            var element = _root.Q<VisualElement>(elementName);
            if (element != null)
            {
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void HandleArenaChoice(ArenaId arenaId)
        {
            if (!_arenaSelectionActive || arenaId == ArenaId.None || _arenaMismatchCountdownActive)
            {
                return;
            }

            SetArenaVoteForSeat(_localSeat, arenaId);
            EvaluateArenaSelectionState();
            UpdateUI();
        }

        private void PreviewLocalArenaChoice(ArenaId arenaId)
        {
            if (!_arenaSelectionActive || arenaId == ArenaId.None || _arenaMismatchCountdownActive)
            {
                return;
            }

            SetArenaVoteForSeat(_localSeat, arenaId);
            UpdateUI();
        }

        private void EvaluateArenaSelectionState()
        {
            if (_controlMode == MatchControlMode.Hotseat)
            {
                ArenaId soloArena = GetArenaVoteForSeat(_localSeat);
                if (soloArena != ArenaId.None)
                {
                    ResolveArenaSelection(soloArena);
                }

                return;
            }

            if (_seatOneArenaVote == ArenaId.None || _seatTwoArenaVote == ArenaId.None)
            {
                return;
            }

            if (_seatOneArenaVote == _seatTwoArenaVote)
            {
                ResolveArenaSelection(_seatOneArenaVote);
                return;
            }

            if (!_arenaMismatchCountdownActive)
            {
                _arenaMismatchCountdownActive = true;
                _arenaResolveAtUnscaledTime = Application.isPlaying
                    ? Time.unscaledTime + ArenaMismatchCountdownSeconds
                    : -1f;
            }
        }

        private void TickArenaSelection()
        {
            if (_externalCommandSink != null || !_arenaSelectionActive || !_arenaMismatchCountdownActive || !Application.isPlaying)
            {
                return;
            }

            if (Time.unscaledTime < _arenaResolveAtUnscaledTime)
            {
                return;
            }

            ArenaId resolvedArena = UnityEngine.Random.Range(0, 2) == 0
                ? _seatOneArenaVote
                : _seatTwoArenaVote;
            ResolveArenaSelection(resolvedArena);
        }

        private float GetArenaCountdownRemainingSeconds()
        {
            if (!_arenaMismatchCountdownActive || !Application.isPlaying || _arenaResolveAtUnscaledTime < 0f)
            {
                return ArenaMismatchCountdownSeconds;
            }

            return Mathf.Max(0f, _arenaResolveAtUnscaledTime - Time.unscaledTime);
        }

        private void ResolveArenaSelection(ArenaId arenaId)
        {
            _selectedArena = arenaId == ArenaId.None ? ArenaId.FreehavenGarden : arenaId;
            _arenaSelectionActive = false;
            _arenaMismatchCountdownActive = false;
            _arenaResolveAtUnscaledTime = -1f;
            _boardViewNeedsReset = true;
        }

        private ArenaId GetArenaVoteForSeat(MatchSeat seat)
        {
            return seat == MatchSeat.SeatOne ? _seatOneArenaVote : _seatTwoArenaVote;
        }

        private void SetArenaVoteForSeat(MatchSeat seat, ArenaId arenaId)
        {
            if (seat == MatchSeat.SeatOne)
            {
                _seatOneArenaVote = arenaId;
            }
            else
            {
                _seatTwoArenaVote = arenaId;
            }
        }

        private void ResetRuntimeForNewSession()
        {
            _matchInitialized = false;
            _highlightedCardIndex = -1;
            _selectedBoardTileIndex = -1;
            _selectedAttackerTileIndex = -1;
            _selectedWarShopOption = -1;
            _placementFocusActive = false;
            _cardDeployInFlight = false;
            _boardViewNeedsReset = true;
            _boardViewResetAttempts = 0;
            _roundNumber = 0;
            _phaseEndsAtUnscaledTime = -1f;
            _nextDisplayActionAtUnscaledTime = -1f;
            _autoAdvanceAtUnscaledTime = -1f;
            _awarenessOverrideText = string.Empty;
            _awarenessOverrideExpiresAt = -1f;
            _displayNarrationText = string.Empty;
            _abilityPreviewCard = null;
            _abilityPreviewText = string.Empty;
            _lastAbilityPreviewMarkup = string.Empty;
            _reconnectOverlayVisible = false;
            _reconnectMessageText = string.Empty;
            _reconnectSecondsRemaining = 30;
            _arenaSelectionActive = false;
            _arenaMismatchCountdownActive = false;
            _selectedArena = ArenaId.None;
            _seatOneArenaVote = ArenaId.None;
            _seatTwoArenaVote = ArenaId.None;
            _arenaResolveAtUnscaledTime = -1f;
            _matchEnded = false;
            _winningSeat = MatchSeat.SeatOne;
            _matchEndMessage = string.Empty;
            _warShopOverlayOpen = false;
            _mobileLeftDockOpen = false;
            _mobileRightDockOpen = false;
            _floatingBoardTexts.Clear();
            isInspectorOverlayOpen = false;
            detailedCardData = null;
            _seatOneTransientUiState.highlightedCardIndex = -1;
            _seatOneTransientUiState.selectedBoardTileIndex = -1;
            _seatOneTransientUiState.selectedAttackerTileIndex = -1;
            _seatOneTransientUiState.selectedWarShopOption = -1;
            _seatOneTransientUiState.placementFocusActive = false;
            _seatTwoTransientUiState.highlightedCardIndex = -1;
            _seatTwoTransientUiState.selectedBoardTileIndex = -1;
            _seatTwoTransientUiState.selectedAttackerTileIndex = -1;
            _seatTwoTransientUiState.selectedWarShopOption = -1;
            _seatTwoTransientUiState.placementFocusActive = false;
            Array.Clear(_warShopPurchaseUsedBySeat, 0, _warShopPurchaseUsedBySeat.Length);
            InvalidateBoardVisualTree();
        }

        private bool UsesHotseatControlMode()
        {
            return _controlMode == MatchControlMode.Hotseat;
        }

        private static string GetSeatThemeClass(MatchSeat seat)
        {
            return seat == MatchSeat.SeatOne ? "seat-theme-one" : "seat-theme-two";
        }

        private static string GetSeatStatsClass(MatchSeat seat)
        {
            return seat == MatchSeat.SeatOne ? "tile-stats-theme-seat-one" : "tile-stats-theme-seat-two";
        }

        private static string GetSeatOwnershipFrameClass(MatchSeat seat)
        {
            return seat == MatchSeat.SeatOne ? "tile-ownership-frame-seat-one" : "tile-ownership-frame-seat-two";
        }

        private static string GetBaseTileThemeClass(MatchSeat seat)
        {
            return seat == MatchSeat.SeatOne ? "seat-one-base-tile" : "seat-two-base-tile";
        }

        private static string GetCardArtThemeClass(MatchSeat seat)
        {
            return seat == MatchSeat.SeatOne ? "tile-card-art-seat-one" : "tile-card-art-seat-two";
        }

        private static string GetBoardActiveFrameClass(MatchSeat seat)
        {
            return seat == MatchSeat.SeatOne ? "board-ownership-frame-seat-one" : "board-ownership-frame-seat-two";
        }

        private bool TryGetSelectedActionSeat(out MatchSeat seat)
        {
            seat = _localSeat;
            if (_selectedAttackerTileIndex >= 0
                && _selectedAttackerTileIndex < _tileOccupantSeats.Length
                && _tileOccupantSeats[_selectedAttackerTileIndex].HasValue)
            {
                seat = _tileOccupantSeats[_selectedAttackerTileIndex].Value;
                return true;
            }

            if (_highlightedCardIndex >= 0 && _activeTurnSeat == _localSeat)
            {
                seat = _localSeat;
                return true;
            }

            return false;
        }

        private void InitializeMatchRuntimeIfNeeded()
        {
            if (_matchInitialized || _isInitializingMatchRuntime)
            {
                return;
            }

            _isInitializingMatchRuntime = true;

            if (cardsInHand == null)
            {
                cardsInHand = new List<CardTemplate>();
            }

            try
            {
                if (prototypeMatch != null)
                {
                    InitializeFromPrototypeMatch();
                }
                else
                {
                    InitializeLegacyPreviewMatch();
                }

                _matchInitialized = true;
            }
            finally
            {
                _isInitializingMatchRuntime = false;
            }
        }

        private void InitializeFromPrototypeMatch()
        {
            var layout = prototypeMatch.boardLayout;
            _boardRows = layout != null ? Mathf.Max(1, layout.rows) : DefaultBoardRows;
            _boardColumns = layout != null ? Mathf.Max(1, layout.columns) : DefaultBoardColumns;
            MatchControlMode prototypeControlMode = prototypeMatch.hotseatTestMode
                ? MatchControlMode.Hotseat
                : prototypeMatch.defaultControlMode;
            _controlMode = _startupControlModeOverride ?? prototypeControlMode;
            _perspectiveSeat = prototypeMatch.localSeat;
            _localSeat = _perspectiveSeat;
            _hotseatTestMode = UsesHotseatControlMode();
            _activeTurnSeat = prototypeMatch.startingTurn;
            if (UsesHotseatControlMode())
            {
                _localSeat = _activeTurnSeat;
            }
            _canonicalTopSeat = layout != null ? layout.canonicalTopSeat : MatchPerspectiveUtility.GetOpposingSeat(_perspectiveSeat);
            _boardViewNeedsReset = true;
            _boardViewResetAttempts = 0;

            EnsureBoardRuntimeCapacity(_boardRows * _boardColumns);
            ResetBoardRuntimeToDefaults();

            if (layout != null)
            {
                foreach (BoardTileDefinitionData tile in layout.tiles)
                {
                    if (!IsInBounds(tile.row, tile.column))
                    {
                        continue;
                    }

                    int tileIndex = ToTileIndex(tile.row, tile.column);
                    _tileOwners[tileIndex] = tile.owner;
                    _tileAreaKinds[tileIndex] = tile.areaKind;
                    _tileMaxHealth[tileIndex] = tile.maxHealth;
                    _tileCurrentHealth[tileIndex] = tile.maxHealth;
                    _tileBlocksCity[tileIndex] = tile.blocksCityUntilDestroyed;
                }
            }

            _seatOneState = BuildParticipantRuntimeState(prototypeMatch.seatOne);
            _seatTwoState = BuildParticipantRuntimeState(prototypeMatch.seatTwo);

            SyncVisibleStateFromPerspective();

            if (prototypeMatch.startingCardPlacements != null)
            {
                foreach (StartingCardPlacement placement in prototypeMatch.startingCardPlacements)
                {
                    if (!IsInBounds(placement.row, placement.column))
                    {
                        continue;
                    }

                    int placementIndex = ToTileIndex(placement.row, placement.column);
                    _boardTileData[placementIndex] = placement.card;
                    _tileOccupantSeats[placementIndex] = GetSeatFromTileOwner(_tileOwners[placementIndex]);
                    if (placement.card != null && IsInfrastructureCard(placement.card))
                    {
                        int mergedHealth = Mathf.Max(0, placement.card.health);
                        _tileMaxHealth[placementIndex] = Mathf.Max(0, _tileMaxHealth[placementIndex]) + mergedHealth;
                        _tileCurrentHealth[placementIndex] = Mathf.Max(0, _tileCurrentHealth[placementIndex]) + mergedHealth;
                        _occupantCurrentHealth[placementIndex] = _tileCurrentHealth[placementIndex];
                    }
                    else
                    {
                        _occupantCurrentHealth[placementIndex] = placement.card != null ? placement.card.health : 0;
                    }
                }
            }

            BeginRound(true);
        }

        private void InitializeLegacyPreviewMatch()
        {
            _boardRows = DefaultBoardRows;
            _boardColumns = DefaultBoardColumns;
            _controlMode = _startupControlModeOverride ?? MatchControlMode.Hotseat;
            _perspectiveSeat = MatchSeat.SeatOne;
            _localSeat = MatchSeat.SeatOne;
            _activeTurnSeat = MatchSeat.SeatOne;
            _hotseatTestMode = UsesHotseatControlMode();
            _canonicalTopSeat = MatchSeat.SeatTwo;
            _boardViewNeedsReset = true;
            _boardViewResetAttempts = 0;

            EnsureBoardRuntimeCapacity(_boardRows * _boardColumns);
            ResetBoardRuntimeToDefaults();

            _seatOneState = new ParticipantRuntimeState
            {
                seat = MatchSeat.SeatOne,
                cityName = playerCityName,
                health = playerStability,
                treasury = playerTreasury,
                turnStartDrawCount = 1,
                maxHandSize = DefaultMaxRealHandSize,
                baseTreasuryIncome = DefaultBaseTreasuryIncome
            };
            _seatTwoState = new ParticipantRuntimeState
            {
                seat = MatchSeat.SeatTwo,
                cityName = enemyCityName,
                health = enemyStability,
                treasury = enemyTreasury,
                turnStartDrawCount = 1,
                maxHandSize = DefaultMaxRealHandSize,
                baseTreasuryIncome = DefaultBaseTreasuryIncome
            };

            if (cardsInHand.Count > 0 && cardsInHand.Count < 5)
            {
                CardTemplate cardOne = cardsInHand[0];
                CardTemplate cardTwo = cardsInHand.Count > 1 ? cardsInHand[1] : cardsInHand[0];
                cardsInHand.Add(cardOne);
                cardsInHand.Add(cardTwo);
            }

            _seatOneState.hand.Clear();
            foreach (CardTemplate card in cardsInHand)
            {
                if (card != null)
                {
                    _seatOneState.hand.Add(card);
                }
            }

            if (cardsInHand.Count > 0)
            {
                int firstIndex = ToTileIndex(0, 1);
                _boardTileData[firstIndex] = cardsInHand[0];
                _occupantCurrentHealth[firstIndex] = cardsInHand[0].health;
                if (cardsInHand.Count > 1)
                {
                    int secondIndex = ToTileIndex(0, 2);
                    _boardTileData[secondIndex] = cardsInHand[1];
                    _occupantCurrentHealth[secondIndex] = cardsInHand[1].health;
                }
            }

            BeginRound(true);
        }

        private ParticipantRuntimeState BuildParticipantRuntimeState(MatchParticipantDefinition participant)
        {
            MatchParticipantDefinition source = participant ?? new MatchParticipantDefinition();
            DeckDefinition deck = source.deck != null ? source.deck : source.city != null ? source.city.defaultDeck : null;
            bool isTestingMode = _selectedLaunchMode == MatchLaunchMode.Testing;

            ParticipantRuntimeState state = new ParticipantRuntimeState
            {
                seat = source.seat,
                cityName = source.city != null ? source.city.displayName : source.seat == MatchSeat.SeatOne ? playerCityName : enemyCityName,
                health = source.startingHealthOverride >= 0
                    ? source.startingHealthOverride
                    : source.city != null ? source.city.startingHealth : source.seat == MatchSeat.SeatOne ? playerStability : enemyStability,
                treasury = source.startingTreasuryOverride >= 0
                    ? source.startingTreasuryOverride
                    : source.city != null ? source.city.startingTreasury : source.seat == MatchSeat.SeatOne ? playerTreasury : enemyTreasury,
                turnStartDrawCount = isTestingMode ? 0 : Mathf.Max(0, source.turnStartDrawCount),
                maxHandSize = isTestingMode ? 0 : Mathf.Max(0, source.maxHandSize),
                baseTreasuryIncome = source.baseTreasuryIncome,
                deployTurnsTaken = 0
            };

            state.drawPile.Clear();
            if (isTestingMode)
            {
                foreach (CardTemplate card in BuildTestingModeCardPool())
                {
                    if (card != null)
                    {
                        state.drawPile.Add(CloneRuntimeCard(card));
                    }
                }
            }
            else if (deck != null && deck.cards != null)
            {
                foreach (CardTemplate card in deck.cards)
                {
                    if (card != null)
                    {
                        state.drawPile.Add(CloneRuntimeCard(card));
                    }
                }
            }

            ShufflePile(state.drawPile);

            state.hand.Clear();
            if (isTestingMode)
            {
                state.maxHandSize = state.drawPile.Count;
                while (state.drawPile.Count > 0)
                {
                    CardTemplate nextCard = state.drawPile[0];
                    state.drawPile.RemoveAt(0);
                    state.hand.Add(nextCard);
                }
            }
            else if (source.openingHand != null && source.openingHand.Count > 0)
            {
                foreach (CardTemplate card in source.openingHand)
                {
                    if (card != null)
                    {
                        state.hand.Add(CloneRuntimeCard(card));
                        RemoveFirstCardById(state.drawPile, card.cardId);
                    }
                }
            }
            else
            {
                DrawOpeningChampionAndUnits(state);
            }

            return state;
        }

        private List<CardTemplate> BuildTestingModeCardPool()
        {
            var pool = new List<CardTemplate>();
            var seenCardIds = new HashSet<string>(StringComparer.Ordinal);

            void AddDeckCards(DeckDefinition sourceDeck)
            {
                if (sourceDeck == null || sourceDeck.cards == null)
                {
                    return;
                }

                foreach (CardTemplate card in sourceDeck.cards)
                {
                    if (card == null || string.IsNullOrWhiteSpace(card.cardId) || !seenCardIds.Add(card.cardId))
                    {
                        continue;
                    }

                    pool.Add(card);
                }
            }

            if (prototypeMatch != null)
            {
                DeckDefinition seatOneDeck = prototypeMatch.seatOne != null
                    ? (prototypeMatch.seatOne.deck != null ? prototypeMatch.seatOne.deck : prototypeMatch.seatOne.city != null ? prototypeMatch.seatOne.city.defaultDeck : null)
                    : null;
                DeckDefinition seatTwoDeck = prototypeMatch.seatTwo != null
                    ? (prototypeMatch.seatTwo.deck != null ? prototypeMatch.seatTwo.deck : prototypeMatch.seatTwo.city != null ? prototypeMatch.seatTwo.city.defaultDeck : null)
                    : null;

                AddDeckCards(seatOneDeck);
                AddDeckCards(seatTwoDeck);
            }

            return pool;
        }

        private void DrawOpeningChampionAndUnits(ParticipantRuntimeState state)
        {
            if (state == null)
            {
                return;
            }

            TryDrawFirstMatchingCard(state, IsSpecialUnitCard);
            for (int i = CountOpeningCivilianMilitaryCards(state); i < 5; i++)
            {
                if (!TryDrawFirstMatchingCard(state, IsCivilianOrMilitaryUnitCard))
                {
                    break;
                }
            }
        }

        private int CountOpeningCivilianMilitaryCards(ParticipantRuntimeState state)
        {
            if (state == null)
            {
                return 0;
            }

            int count = 0;
            foreach (CardTemplate card in state.hand)
            {
                if (IsCivilianOrMilitaryUnitCard(card))
                {
                    count++;
                }
            }

            return count;
        }

        private bool TryDrawFirstMatchingCard(ParticipantRuntimeState state, Predicate<CardTemplate> predicate)
        {
            if (state == null || predicate == null)
            {
                return false;
            }

            for (int i = 0; i < state.drawPile.Count; i++)
            {
                CardTemplate card = state.drawPile[i];
                if (!predicate(card))
                {
                    continue;
                }

                state.drawPile.RemoveAt(i);
                state.hand.Add(card);
                return true;
            }

            return false;
        }

        private bool IsSpecialUnitCard(CardTemplate card)
        {
            return card != null && card.cardType == CardType.Unit && card.unitTag == UnitTag.Special;
        }

        private bool IsCivilianOrMilitaryUnitCard(CardTemplate card)
        {
            return card != null
                && card.cardType == CardType.Unit
                && (card.unitTag == UnitTag.Civilian || card.unitTag == UnitTag.Military);
        }

        private void DrawCards(ParticipantRuntimeState state, int count)
        {
            if (state == null || count <= 0)
            {
                return;
            }

            for (int i = 0; i < count && state.drawPile.Count > 0; i++)
            {
                CardTemplate nextCard = state.drawPile[0];
                state.drawPile.RemoveAt(0);
                state.hand.Add(nextCard);
            }
        }

        private void DrawToHandLimit(ParticipantRuntimeState state)
        {
            if (state == null)
            {
                return;
            }

            RemoveTemporaryCommandCards(state);
            state.lastDrawRepooledDiscard = false;
            state.lastDeckRefillRealHandIndex = -1;
            int handLimit = Mathf.Max(0, state.maxHandSize);
            while (CountRealHandCards(state) < handLimit)
            {
                if (state.drawPile.Count <= 0)
                {
                    if (state.discardPile.Count <= 0)
                    {
                        break;
                    }

                    state.lastDrawRepooledDiscard = true;
                    state.lastDeckRefillRealHandIndex = CountRealHandCards(state);
                    RefillDrawPileFromShuffledDiscard(state);
                    QueueDeckRefillVisualSequence(state);
                }

                if (state.drawPile.Count <= 0)
                {
                    break;
                }

                CardTemplate nextCard = state.drawPile[0];
                state.drawPile.RemoveAt(0);
                if (IsRealDeckCard(nextCard))
                {
                    state.hand.Add(nextCard);
                }
            }
        }

        private int CountRealHandCards(ParticipantRuntimeState state)
        {
            if (state == null)
            {
                return 0;
            }

            int count = 0;
            foreach (CardTemplate card in state.hand)
            {
                if (IsRealDeckCard(card))
                {
                    count++;
                }
            }

            return count;
        }

        private bool IsRealDeckCard(CardTemplate card)
        {
            return card != null
                && !IsLockCommandCard(card)
                && !IsSystemRuntimeCard(card);
        }

        private static bool IsSystemRuntimeCard(CardTemplate card)
        {
            return card != null
                && !string.IsNullOrWhiteSpace(card.cardId)
                && card.cardId.StartsWith("card.system.", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBelfryTokenCard(CardTemplate card)
        {
            return card != null
                && string.Equals(card.cardId, "card.system.belfry_token", StringComparison.OrdinalIgnoreCase);
        }

        private void RefillDrawPileFromShuffledDiscard(ParticipantRuntimeState state)
        {
            if (state == null || state.discardPile.Count <= 0)
            {
                return;
            }

            ShufflePile(state.discardPile);
            state.drawPile.AddRange(state.discardPile);
            state.discardPile.Clear();
            ShufflePile(state.drawPile);
        }

        private void ShufflePile(List<CardTemplate> pile)
        {
            if (pile == null)
            {
                return;
            }

            for (int i = pile.Count - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                CardTemplate temp = pile[i];
                pile[i] = pile[swapIndex];
                pile[swapIndex] = temp;
            }
        }

        private bool RemoveFirstCardById(List<CardTemplate> pile, string cardId)
        {
            if (pile == null || string.IsNullOrWhiteSpace(cardId))
            {
                return false;
            }

            for (int i = 0; i < pile.Count; i++)
            {
                if (pile[i] != null && pile[i].cardId == cardId)
                {
                    pile.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        private CardTemplate CloneRuntimeCard(CardTemplate source)
        {
            if (source == null)
            {
                return null;
            }

            CardTemplate clone = ScriptableObject.CreateInstance<CardTemplate>();
            clone.cardId = source.cardId;
            clone.cardName = source.cardName;
            clone.treasuryCost = source.treasuryCost;
            clone.cardType = source.cardType;
            clone.health = source.health;
            clone.attack = source.attack;
            clone.range = source.range;
            clone.movementRange = source.movementRange;
            clone.unitTag = source.unitTag;
            clone.infrastructureKind = source.infrastructureKind;
            clone.commandCardKind = source.commandCardKind;
            clone.keywordEffects = CloneAbilityEffects(source.keywordEffects);
            clone.abilityText = source.abilityText;
            clone.detailedAbilityText = source.detailedAbilityText;
            clone.customArt = source.customArt;
            clone.attachedItemCard = source.attachedItemCard != null ? CloneRuntimeCard(source.attachedItemCard) : null;
            clone.bonusHealth = source.bonusHealth;
            clone.bonusAttack = source.bonusAttack;
            clone.bonusRange = source.bonusRange;
            clone.bonusMovementRange = source.bonusMovementRange;
            clone.bonusSiegeAttack = source.bonusSiegeAttack;
            return clone;
        }

        private bool IsSilencedAtTile(int tileIndex)
        {
            return tileIndex >= 0
                && tileIndex < _silenceTurnsByTile.Length
                && _silenceTurnsByTile[tileIndex] > 0
                && _boardTileData[tileIndex] != null
                && _occupantCurrentHealth[tileIndex] > 0;
        }

        private void TickTimedTileStatusesAtRoundStart()
        {
            for (int tileIndex = 0; tileIndex < _silenceTurnsByTile.Length; tileIndex++)
            {
                if (_silenceTurnsByTile[tileIndex] > 0)
                {
                    _silenceTurnsByTile[tileIndex] = Mathf.Max(0, _silenceTurnsByTile[tileIndex] - 1);
                }
            }
        }

        private void ApplySilenceToTile(int tileIndex, int turns)
        {
            if (tileIndex < 0
                || tileIndex >= _boardTileData.Length
                || _boardTileData[tileIndex] == null
                || _occupantCurrentHealth[tileIndex] <= 0)
            {
                return;
            }

            _silenceTurnsByTile[tileIndex] = Mathf.Max(_silenceTurnsByTile[tileIndex], Mathf.Max(1, turns) + 1);
            AddFloatingBoardText(tileIndex, "SILENCE", "tile-floating-status");
        }

        private int GetTreasuryIncomeForSeat(MatchSeat seat)
        {
            ParticipantRuntimeState state = GetRuntimeState(seat);
            int income = (state != null ? state.baseTreasuryIncome : DefaultBaseTreasuryIncome) + GetRoundIncomeScalingBonus();
            for (int tileIndex = 0; tileIndex < _boardTileData.Length; tileIndex++)
            {
                if (_tileOccupantSeats[tileIndex].HasValue
                    && _tileOccupantSeats[tileIndex].Value == seat
                    && _boardTileData[tileIndex] != null
                    && _occupantCurrentHealth[tileIndex] > 0)
                {
                    income += IsSilencedAtTile(tileIndex)
                        ? 0
                        : GetKeywordValue(_boardTileData[tileIndex], AbilityKeyword.Gather);
                }
            }

            return income;
        }

        private int GetRoundIncomeScalingBonus()
        {
            int round = Mathf.Max(1, _roundNumber);
            return Mathf.Max(0, ((round - 1) / 2) * 2);
        }

        private string FormatTreasuryIncome(int income)
        {
            return income >= 0 ? $"+{income}" : income.ToString();
        }

        private void BeginDeployTurnEconomyAndDraw(MatchSeat seat)
        {
            ParticipantRuntimeState state = GetRuntimeState(seat);
            if (state == null)
            {
                return;
            }

            state.treasury = Mathf.Max(0, state.treasury + GetTreasuryIncomeForSeat(seat));

            ApplyDeployStartKeywordEffects(seat);
            DrawToHandLimit(state);
            RefreshTurnCommandCardsForSeat(seat);
            if (state.lastDrawRepooledDiscard && IsVisibleControlSeat(seat))
            {
                _nextHandEntryRepoolRealIndex = Mathf.Max(0, state.lastDeckRefillRealHandIndex);
                _nextHandEntryRepoolDelayMs = 430;
            }
            state.deployTurnsTaken++;
        }

        private void ApplyDeployStartKeywordEffects(MatchSeat seat)
        {
            for (int tileIndex = 0; tileIndex < _boardTileData.Length; tileIndex++)
            {
                CardTemplate cardData = _boardTileData[tileIndex];
                if (!_tileOccupantSeats[tileIndex].HasValue
                    || _tileOccupantSeats[tileIndex].Value != seat
                    || cardData == null
                    || _occupantCurrentHealth[tileIndex] <= 0)
                {
                    continue;
                }

                if (IsSilencedAtTile(tileIndex))
                {
                    continue;
                }

                if (CardHasKeyword(cardData, AbilityKeyword.Secure))
                {
                    ParticipantRuntimeState state = GetRuntimeState(seat);
                    bool usesCooldown = cardData.cardId == "card.iron_citadel.marshal";
                    if (!usesCooldown || (state != null && state.deployTurnsTaken > 0 && state.deployTurnsTaken % 5 == 0))
                    {
                        TrySecureOccupiedTile(tileIndex, seat, Mathf.Max(1, GetKeywordValue(cardData, AbilityKeyword.Secure)));
                    }
                }

                if (CardHasKeyword(cardData, AbilityKeyword.Salvage))
                {
                    SalvageCardsFromDiscardToDeck(seat, Mathf.Max(1, GetKeywordValue(cardData, AbilityKeyword.Salvage)));
                }

                if (cardData.cardId == "card.iron_citadel.tax_office")
                {
                    ApplyTaxOfficeSiphon(tileIndex, seat);
                }

                if (cardData.cardId == "card.free_haven.belfry")
                {
                    AdvanceBelfrySpawnCharge(tileIndex, seat);
                }
            }
        }

        private void ApplyTaxOfficeSiphon(int tileIndex, MatchSeat seat)
        {
            int siphonValue = Mathf.Max(1, GetKeywordValue(_boardTileData[tileIndex], AbilityKeyword.Siphon));
            ParticipantRuntimeState friendlyState = GetRuntimeState(seat);
            ParticipantRuntimeState enemyState = GetRuntimeState(MatchPerspectiveUtility.GetOpposingSeat(seat));
            if (friendlyState == null || enemyState == null || siphonValue <= 0)
            {
                return;
            }

            int stolen = Mathf.Min(siphonValue, Mathf.Max(0, enemyState.treasury));
            if (stolen <= 0)
            {
                return;
            }

            enemyState.treasury = Mathf.Max(0, enemyState.treasury - stolen);
            friendlyState.treasury += stolen;
            AddFloatingBoardText(tileIndex, $"+{stolen}", "tile-floating-status");
            ShowAwarenessMessage($"{_boardTileData[tileIndex].cardName} siphoned {stolen} coin{(stolen == 1 ? string.Empty : "s")}.", 1.8f);
        }

        private void AdvanceBelfrySpawnCharge(int tileIndex, MatchSeat seat)
        {
            if (tileIndex < 0 || tileIndex >= _spawnChargeTurnsByTile.Length)
            {
                return;
            }

            _spawnChargeTurnsByTile[tileIndex] = Mathf.Max(0, _spawnChargeTurnsByTile[tileIndex]) + 1;
            if (_spawnChargeTurnsByTile[tileIndex] < BelfryDeploysPerSpawn || HasBelfryTokenForSeat(seat))
            {
                return;
            }

            int spawnTileIndex = GetBelfrySpawnTileIndex(tileIndex, seat);
            if (spawnTileIndex < 0)
            {
                return;
            }

            CardTemplate token = CreateBelfryTokenCard();
            _boardTileData[spawnTileIndex] = token;
            _tileOccupantSeats[spawnTileIndex] = seat;
            _occupantCurrentHealth[spawnTileIndex] = token.health;
            _spawnChargeTurnsByTile[tileIndex] = 0;
            AddFloatingBoardText(spawnTileIndex, "SPAWN", "tile-floating-status");
            ShowAwarenessMessage($"Belfry spawned a Belfry Token.", 1.9f);
        }

        private bool HasBelfryTokenForSeat(MatchSeat seat)
        {
            for (int tileIndex = 0; tileIndex < _boardTileData.Length; tileIndex++)
            {
                if (_tileOccupantSeats[tileIndex].HasValue
                    && _tileOccupantSeats[tileIndex].Value == seat
                    && IsBelfryTokenCard(_boardTileData[tileIndex])
                    && _occupantCurrentHealth[tileIndex] > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private int GetBelfrySpawnTileIndex(int belfryTileIndex, MatchSeat seat)
        {
            if (!TryGetRowColumnFromTileIndex(belfryTileIndex, out int sourceRow, out int sourceColumn))
            {
                return -1;
            }

            int rowStep = GetForwardRowStepForSeat(seat);
            int[,] offsets =
            {
                { rowStep, 0 },
                { 0, -1 },
                { 0, 1 },
                { -rowStep, 0 }
            };

            for (int i = 0; i < offsets.GetLength(0); i++)
            {
                int targetRow = sourceRow + offsets[i, 0];
                int targetColumn = sourceColumn + offsets[i, 1];
                if (!IsInBounds(targetRow, targetColumn))
                {
                    continue;
                }

                int targetTileIndex = ToTileIndex(targetRow, targetColumn);
                if (_boardTileData[targetTileIndex] != null)
                {
                    continue;
                }

                MatchSeat? baseSeat = GetSeatFromTileOwner(_tileOwners[targetTileIndex]);
                if (_tileAreaKinds[targetTileIndex] == TileAreaKind.Base
                    && baseSeat.HasValue
                    && baseSeat.Value != seat
                    && _tileCurrentHealth[targetTileIndex] > 0)
                {
                    continue;
                }

                return targetTileIndex;
            }

            return -1;
        }

        private CardTemplate CreateBelfryTokenCard()
        {
            CardTemplate token = ScriptableObject.CreateInstance<CardTemplate>();
            token.cardId = "card.system.belfry_token";
            token.cardName = "Belfry Token";
            token.cardType = CardType.Unit;
            token.unitTag = UnitTag.Special;
            token.infrastructureKind = InfrastructureKind.None;
            token.commandCardKind = CommandCardKind.None;
            token.health = 1;
            token.attack = BelfryTokenAttack;
            token.range = 1;
            token.movementRange = 0;
            token.treasuryCost = 0;
            token.abilityText = $"1 HP. {BelfryTokenAttack} AT. Cannot be attacked. Burns after its attack.";
            token.detailedAbilityText = $"This token has 1 HP, {BelfryTokenAttack} AT, attack range 1, and movement range 0. It cannot be targeted by attacks, and it burns away immediately after making its attack.";
            return token;
        }

        private void SalvageCardsFromDiscardToDeck(MatchSeat seat, int count)
        {
            ParticipantRuntimeState state = GetRuntimeState(seat);
            if (state == null || count <= 0 || state.discardPile.Count <= 0)
            {
                return;
            }

            int moved = Mathf.Min(count, state.discardPile.Count);
            for (int i = 0; i < moved; i++)
            {
                int sourceIndex = UnityEngine.Random.Range(0, state.discardPile.Count);
                CardTemplate card = state.discardPile[sourceIndex];
                state.discardPile.RemoveAt(sourceIndex);
                state.drawPile.Add(card);
            }

            ShufflePile(state.drawPile);
            if (IsVisibleControlSeat(seat))
            {
                QueueDeckRefillVisualSequence(state);
            }
        }

        private void TrySecureOccupiedTile(int tileIndex, MatchSeat seat, int count)
        {
            if (count <= 0
                || tileIndex < 0
                || tileIndex >= _boardTileData.Length
                || _boardTileData[tileIndex] == null
                || _tileAreaKinds[tileIndex] != TileAreaKind.Freeplay)
            {
                ResetSecureHoldProgress(tileIndex);
                return;
            }

            TileOwner friendlyOwner = seat == MatchSeat.SeatOne ? TileOwner.SeatOne : TileOwner.SeatTwo;
            if (!HasOrthogonalAdjacentFriendlyBase(tileIndex, friendlyOwner))
            {
                ResetSecureHoldProgress(tileIndex);
                return;
            }

            _secureHoldTurnsByTile[tileIndex] = Mathf.Max(0, _secureHoldTurnsByTile[tileIndex]) + 1;
            if (_secureHoldTurnsByTile[tileIndex] < 2)
            {
                AddFloatingBoardText(tileIndex, $"HOLD {_secureHoldTurnsByTile[tileIndex]}/2", "tile-floating-status");
                return;
            }

            ApplySecureBaseTile(tileIndex, seat);
        }

        private void ApplySecureBaseTile(int tileIndex, MatchSeat seat)
        {
            const int weakenedSecureBaseHealth = 10;
            _tileAreaKinds[tileIndex] = TileAreaKind.Base;
            _tileOwners[tileIndex] = seat == MatchSeat.SeatOne ? TileOwner.SeatOne : TileOwner.SeatTwo;
            _tileMaxHealth[tileIndex] = weakenedSecureBaseHealth;
            _tileCurrentHealth[tileIndex] = weakenedSecureBaseHealth;
            _tileBlocksCity[tileIndex] = true;
            _secureHoldTurnsByTile[tileIndex] = 0;
            AddFloatingBoardText(tileIndex, "SECURE", "tile-floating-status");
        }

        private void ResetSecureHoldProgress(int tileIndex)
        {
            if (tileIndex < 0 || tileIndex >= _secureHoldTurnsByTile.Length)
            {
                return;
            }

            _secureHoldTurnsByTile[tileIndex] = 0;
        }

        private bool HasOrthogonalAdjacentFriendlyBase(int tileIndex, TileOwner friendlyOwner)
        {
            if (!TryGetRowColumnFromTileIndex(tileIndex, out int row, out int column))
            {
                return false;
            }

            int[,] offsets = { { -1, 0 }, { 1, 0 }, { 0, -1 }, { 0, 1 } };
            for (int i = 0; i < offsets.GetLength(0); i++)
            {
                int adjacentRow = row + offsets[i, 0];
                int adjacentColumn = column + offsets[i, 1];
                if (!IsInBounds(adjacentRow, adjacentColumn))
                {
                    continue;
                }

                int adjacentIndex = ToTileIndex(adjacentRow, adjacentColumn);
                if (_tileAreaKinds[adjacentIndex] == TileAreaKind.Base && _tileOwners[adjacentIndex] == friendlyOwner)
                {
                    return true;
                }
            }

            return false;
        }

        private void DiscardRemainingHandForDeployEnd(MatchSeat seat)
        {
            ParticipantRuntimeState state = GetRuntimeState(seat);
            if (state == null)
            {
                return;
            }

            for (int i = state.hand.Count - 1; i >= 0; i--)
            {
                CardTemplate card = state.hand[i];
                if (IsRealDeckCard(card))
                {
                    CardTemplate pileCard = GetPileReadyCard(card);
                    if (pileCard != null)
                    {
                        state.discardPile.Add(pileCard);
                    }
                }

                state.hand.RemoveAt(i);
            }
        }

        private void DiscardCardForSeat(MatchSeat seat, CardTemplate card)
        {
            ParticipantRuntimeState state = GetRuntimeState(seat);
            if (state == null || !IsRealDeckCard(card))
            {
                return;
            }

            CardTemplate pileCard = GetPileReadyCard(card);
            if (pileCard != null)
            {
                state.discardPile.Add(pileCard);
            }
        }

        private void UpdatePileButtonBounce(string elementName, int currentCount, ref int lastRenderedCount, ref bool suppressNextBounce)
        {
            if (lastRenderedCount >= 0 && lastRenderedCount != currentCount)
            {
                if (suppressNextBounce)
                {
                    suppressNextBounce = false;
                }
                else
                {
                    AnimatePileButtonBounce(elementName);
                }
            }

            lastRenderedCount = currentCount;
        }

        private void AnimatePileButtonBounce(string elementName)
        {
            var element = _root?.Q<VisualElement>(elementName);
            if (element == null)
            {
                return;
            }

            element.RemoveFromClassList("stack-btn-bounce");
            element.schedule.Execute(() => element.AddToClassList("stack-btn-bounce")).StartingIn(0);
            element.schedule.Execute(() => element.RemoveFromClassList("stack-btn-bounce")).StartingIn(260);
        }

        private bool IsVisibleControlSeat(MatchSeat seat)
        {
            MatchSeat visibleSeat = UsesHotseatControlMode() ? _activeTurnSeat : _localSeat;
            return seat == visibleSeat;
        }

        private void QueueDeckRefillVisualSequence(ParticipantRuntimeState state)
        {
            if (!Application.isPlaying || _root == null || state == null || !IsVisibleControlSeat(state.seat))
            {
                return;
            }

            AnimatePileButtonBounce("discard-container");
            _suppressNextDiscardCountBounce = true;
            _suppressNextDeckCountBounce = true;
            _root.schedule.Execute(() => AnimatePileButtonBounce("deck-container")).StartingIn(180);
            _root.schedule.Execute(() =>
            {
                _suppressNextDiscardCountBounce = false;
                _suppressNextDeckCountBounce = false;
            }).StartingIn(520);
        }

        private void BurnCardForSeat(MatchSeat seat, CardTemplate card)
        {
            ParticipantRuntimeState state = GetRuntimeState(seat);
            if (state == null || !IsRealDeckCard(card))
            {
                return;
            }

            CardTemplate pileCard = GetPileReadyCard(card);
            if (pileCard != null)
            {
                state.burnPile.Add(pileCard);
            }
        }

        private CardTemplate GetPileReadyCard(CardTemplate card)
        {
            if (card == null)
            {
                return null;
            }

            CardTemplate canonicalCard = GetCanonicalCardTemplate(card.cardId);
            if (canonicalCard != null)
            {
                return canonicalCard;
            }

            CardTemplate stripped = CloneRuntimeCard(card);
            stripped.keywordEffects = CloneAbilityEffects(GetBaseKeywordEffectsForCard(card.cardId));
            stripped.attachedItemCard = null;
            stripped.bonusHealth = 0;
            stripped.bonusAttack = 0;
            stripped.bonusRange = 0;
            stripped.bonusMovementRange = 0;
            stripped.bonusSiegeAttack = 0;
            return stripped;
        }

        private CardTemplate GetCanonicalCardTemplate(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return null;
            }

            void TryRegister(CardTemplate candidate, ref CardTemplate match)
            {
                if (match == null && candidate != null && string.Equals(candidate.cardId, cardId, StringComparison.Ordinal))
                {
                    match = candidate;
                }
            }

            CardTemplate match = null;

            void ScanParticipant(MatchParticipantDefinition participant)
            {
                if (participant == null || match != null)
                {
                    return;
                }

                if (participant.deck != null)
                {
                    foreach (CardTemplate candidate in participant.deck.cards)
                    {
                        TryRegister(candidate, ref match);
                        if (match != null) return;
                    }
                }

                if (participant.city != null && participant.city.defaultDeck != null)
                {
                    foreach (CardTemplate candidate in participant.city.defaultDeck.cards)
                    {
                        TryRegister(candidate, ref match);
                        if (match != null) return;
                    }
                }

                foreach (CardTemplate candidate in participant.openingHand)
                {
                    TryRegister(candidate, ref match);
                    if (match != null) return;
                }
            }

            if (prototypeMatch != null)
            {
                ScanParticipant(prototypeMatch.seatOne);
                ScanParticipant(prototypeMatch.seatTwo);
                if (match == null)
                {
                    foreach (StartingCardPlacement placement in prototypeMatch.startingCardPlacements)
                    {
                        TryRegister(placement.card, ref match);
                        if (match != null)
                        {
                            break;
                        }
                    }
                }
            }

            return match;
        }

        private List<AbilityEffectData> GetBaseKeywordEffectsForCard(string cardId)
        {
            CardTemplate canonicalCard = GetCanonicalCardTemplate(cardId);
            return canonicalCard != null ? canonicalCard.keywordEffects : null;
        }

        private void ResolveRemovedBoardCardFate(MatchSeat seat, CardTemplate card, RemovedCardFateOverride fateOverride = RemovedCardFateOverride.None)
        {
            if (!IsRealDeckCard(card))
            {
                return;
            }

            if (card.attachedItemCard != null)
            {
                RemoveAttachedItemPayloadFromCarrier(card);
                DiscardCardForSeat(seat, card.attachedItemCard);
                card.attachedItemCard = null;
            }

            if (fateOverride == RemovedCardFateOverride.Burn)
            {
                BurnCardForSeat(seat, card);
                return;
            }

            if (fateOverride == RemovedCardFateOverride.Discard)
            {
                DiscardCardForSeat(seat, card);
                return;
            }

            if (IsInfrastructureCard(card) || card.unitTag == UnitTag.Special)
            {
                BurnCardForSeat(seat, card);
                return;
            }

            DiscardCardForSeat(seat, card);
        }

        private void RemoveAttachedItemPayloadFromCarrier(CardTemplate carrier)
        {
            if (carrier == null || carrier.attachedItemCard == null)
            {
                return;
            }

            CardTemplate item = carrier.attachedItemCard;
            if (item.bonusHealth > 0)
            {
                carrier.health = Mathf.Max(0, carrier.health - item.bonusHealth);
            }

            carrier.bonusHealth = Mathf.Max(0, carrier.bonusHealth - Mathf.Max(0, item.bonusHealth));
            carrier.bonusAttack = Mathf.Max(0, carrier.bonusAttack - Mathf.Max(0, item.bonusAttack));
            carrier.bonusRange = Mathf.Max(0, carrier.bonusRange - Mathf.Max(0, item.bonusRange));
            carrier.bonusMovementRange = Mathf.Max(0, carrier.bonusMovementRange - Mathf.Max(0, item.bonusMovementRange));
            carrier.bonusSiegeAttack = Mathf.Max(0, carrier.bonusSiegeAttack - Mathf.Max(0, item.bonusSiegeAttack));

            AbilityEffectData itemEffect = GetPrimaryKeywordEffect(item);
            if (itemEffect != null && itemEffect.keyword != AbilityKeyword.None && IsStackableKeyword(itemEffect.keyword))
            {
                TryConsumeKeywordValue(carrier, itemEffect.keyword, Mathf.Max(1, itemEffect.value));
            }
        }

        private ParticipantRuntimeState GetRuntimeState(MatchSeat seat)
        {
            return seat == MatchSeat.SeatOne ? _seatOneState : _seatTwoState;
        }

        private void SyncVisibleStateFromPerspective()
        {
            ParticipantRuntimeState playerFacingState = GetRuntimeState(_perspectiveSeat);
            ParticipantRuntimeState enemyFacingState = GetRuntimeState(MatchPerspectiveUtility.GetOpposingSeat(_perspectiveSeat));
            ParticipantRuntimeState controlState = GetRuntimeState(_localSeat);

            if (playerFacingState != null)
            {
                playerCityName = playerFacingState.cityName;
                playerStability = playerFacingState.health;
                playerTreasury = playerFacingState.treasury;
            }

            if (enemyFacingState != null)
            {
                enemyCityName = enemyFacingState.cityName;
                enemyStability = enemyFacingState.health;
                enemyTreasury = enemyFacingState.treasury;
            }

            if (controlState != null)
            {
                cardsInHand = controlState.hand;
                deckRemainingCount = controlState.drawPile.Count;
                discardPileCount = controlState.discardPile.Count;
            }
        }

        public void SetExternalCommandSink(IMatchUiCommandSink commandSink)
        {
            _externalCommandSink = commandSink;
        }

        public void ConfigureNetworkPerspective(MatchSeat localSeat)
        {
            bool shouldFlipBefore = ShouldFlipBoardRowsForCurrentView();
            _startupControlModeOverride = MatchControlMode.SeatAssigned;
            _controlMode = MatchControlMode.SeatAssigned;
            _hotseatTestMode = false;
            _preserveCanonicalBoardView = false;
            _localSeat = localSeat;
            _perspectiveSeat = localSeat;
            if (prototypeMatch != null)
            {
                prototypeMatch.localSeat = localSeat;
                prototypeMatch.hotseatTestMode = false;
                prototypeMatch.defaultControlMode = MatchControlMode.SeatAssigned;
            }

            bool shouldFlipAfter = ShouldFlipBoardRowsForCurrentView();
            if (shouldFlipBefore != shouldFlipAfter)
            {
                InvalidateBoardVisualTree();
                _boardViewNeedsReset = true;
            }

            ApplyTransientUiState(localSeat);
        }

        private bool TryDispatchUiAction(MatchUiAction action)
        {
            return _externalCommandSink != null && _externalCommandSink.TryHandleUiAction(action);
        }

        private bool IsRemoteReplica()
        {
            return _externalCommandSink != null;
        }

        private bool ShouldDispatchHandCardClick(int handIndex)
        {
            return _externalCommandSink != null
                && _roundPhase == MatchRoundPhase.DeployPlanning
                && _activeTurnSeat == _localSeat
                && handIndex >= 0
                && handIndex < cardsInHand.Count;
        }

        private bool ShouldDispatchBoardTileClick(int tileIndex)
        {
            if (_externalCommandSink == null
                || _roundPhase == MatchRoundPhase.DisplayResolution
                || _activeTurnSeat != _localSeat
                || tileIndex < 0
                || tileIndex >= _boardTileData.Length)
            {
                return false;
            }

            if (_highlightedCardIndex >= 0 && _highlightedCardIndex < cardsInHand.Count)
            {
                return true;
            }

            if (_selectedAttackerTileIndex >= 0)
            {
                return true;
            }

            return _tileOccupantSeats[tileIndex].HasValue
                && _tileOccupantSeats[tileIndex].Value == _localSeat
                && IsUnitCard(_boardTileData[tileIndex])
                && (_roundPhase == MatchRoundPhase.DeployPlanning || _roundPhase == MatchRoundPhase.CombatPlanning);
        }

        private bool WouldBoardTileClickChangePlanningState(int tileIndex)
        {
            if (_roundPhase == MatchRoundPhase.DisplayResolution
                || _activeTurnSeat != _localSeat
                || tileIndex < 0
                || tileIndex >= _boardTileData.Length)
            {
                return false;
            }

            if (HasSelectedWarShopOption())
            {
                return true;
            }

            if (_highlightedCardIndex >= 0 && _highlightedCardIndex < cardsInHand.Count)
            {
                return true;
            }

            if (HasSelectedWarShopOption())
            {
                return true;
            }

            if (_selectedAttackerTileIndex >= 0)
            {
                return true;
            }

            return _tileOccupantSeats[tileIndex].HasValue
                && _tileOccupantSeats[tileIndex].Value == _localSeat
                && IsUnitCard(_boardTileData[tileIndex])
                && (_roundPhase == MatchRoundPhase.DeployPlanning || _roundPhase == MatchRoundPhase.CombatPlanning);
        }

        public void ApplyRemoteUiAction(MatchUiAction action)
        {
            switch (action.actionType)
            {
                case MatchUiActionType.ToggleHandCard:
                    HandleHandCardClicked(action.handIndex);
                    break;
                case MatchUiActionType.BoardTilePointerUp:
                    HandleBoardTilePointerUp(action.tileIndex);
                    break;
                case MatchUiActionType.TargetCity:
                    HandleCityAttackClicked(action.targetSeat);
                    break;
                case MatchUiActionType.EndTurn:
                    if (!_cardDeployInFlight && _roundPhase != MatchRoundPhase.DisplayResolution)
                    {
                        AdvancePhaseFromReadyOrTimeout();
                    }
                    break;
                case MatchUiActionType.ClearSelection:
                    ClearSelectionsAndRefresh();
                    break;
                case MatchUiActionType.SelectWarShopOption:
                    HandleWarShopOptionSelected(NormalizeWarShopOption(action.handIndex));
                    break;
                case MatchUiActionType.ChooseArena:
                    HandleArenaChoice(action.arenaId);
                    break;
                case MatchUiActionType.BackToMenu:
                    RequestBackToMenuAfterMatch();
                    break;
            }
        }

        public void ApplyRemoteUiActionForSeat(MatchSeat actingSeat, MatchUiAction action)
        {
            if (!CanApplyRemoteUiActionForSeat(actingSeat, action))
            {
                Debug.LogWarning($"Ignored {action.actionType} from {actingSeat}; active={_activeTurnSeat}, phase={_roundPhase}.");
                return;
            }

            MatchSeat originalLocalSeat = _localSeat;
            CaptureCurrentTransientUiState(originalLocalSeat);
            ApplyTransientUiState(actingSeat);
            _isApplyingRemoteSeatAction = true;
            try
            {
                _localSeat = actingSeat;
                SyncVisibleStateFromPerspective();
                ApplyRemoteUiAction(action);
                CaptureCurrentTransientUiState(actingSeat);
            }
            finally
            {
                _isApplyingRemoteSeatAction = false;
                _localSeat = originalLocalSeat;
                ApplyTransientUiState(originalLocalSeat);
                SyncVisibleStateFromPerspective();
                UpdateUI();
            }
        }

        private bool CanApplyRemoteUiActionForSeat(MatchSeat actingSeat, MatchUiAction action)
        {
            if (action.actionType == MatchUiActionType.ChooseArena
                || action.actionType == MatchUiActionType.BackToMenu)
            {
                return true;
            }

            if (_matchEnded || _roundPhase == MatchRoundPhase.DisplayResolution)
            {
                return false;
            }

            return actingSeat == _activeTurnSeat;
        }

        public string ExportRuntimeSnapshotJson()
        {
            return JsonUtility.ToJson(ExportRuntimeSnapshot());
        }

        public string ExportRuntimeSnapshotStableJson()
        {
            MatchRuntimeSnapshot snapshot = ExportRuntimeSnapshot();
            snapshot.arenaSelectionCountdownRemaining = -1f;
            snapshot.phaseSecondsRemaining = -1f;
            snapshot.awarenessOverrideSecondsRemaining = -1f;
            return JsonUtility.ToJson(snapshot);
        }

        public string ExportTimerSyncSnapshotJson()
        {
            return ExportTimerSyncSnapshotJson(Time.unscaledTime);
        }

        public string ExportTimerSyncSnapshotJson(double serverTimeSeconds)
        {
            return JsonUtility.ToJson(ExportTimerSyncSnapshot(serverTimeSeconds));
        }

        public MatchTimerSyncSnapshot ExportTimerSyncSnapshot()
        {
            return ExportTimerSyncSnapshot(Time.unscaledTime);
        }

        public MatchTimerSyncSnapshot ExportTimerSyncSnapshot(double serverTimeSeconds)
        {
            float phaseSecondsRemaining = GetPhaseSecondsRemainingForSnapshot();
            float arenaCountdownRemaining = _arenaMismatchCountdownActive ? GetArenaCountdownRemainingSeconds() : -1f;
            return new MatchTimerSyncSnapshot
            {
                roundNumber = _roundNumber,
                activeTurnSeat = _activeTurnSeat,
                roundPhase = ToSnapshotPhase(_roundPhase),
                displayResolutionMode = ToSnapshotDisplayResolutionMode(_displayResolutionMode),
                serverTimeSeconds = serverTimeSeconds,
                phaseEndsAtServerTime = phaseSecondsRemaining >= 0f ? serverTimeSeconds + phaseSecondsRemaining : -1d,
                phaseSecondsRemaining = phaseSecondsRemaining,
                arenaSelectionActive = _arenaSelectionActive,
                seatOneArenaVote = _seatOneArenaVote,
                seatTwoArenaVote = _seatTwoArenaVote,
                arenaResolveAtServerTime = arenaCountdownRemaining >= 0f ? serverTimeSeconds + arenaCountdownRemaining : -1d,
                arenaSelectionCountdownRemaining = arenaCountdownRemaining
            };
        }

        public MatchRuntimeSnapshot ExportRuntimeSnapshot()
        {
            SeatTransientUiState exportedUiState = _roundPhase == MatchRoundPhase.DisplayResolution
                ? null
                : GetTransientUiState(_activeTurnSeat);
            var snapshot = new MatchRuntimeSnapshot
            {
                rows = _boardRows,
                columns = _boardColumns,
                canonicalTopSeat = _canonicalTopSeat,
                localSeat = _localSeat,
                controlMode = _controlMode,
                activeTurnSeat = _activeTurnSeat,
                roundInitiativeSeat = _roundInitiativeSeat,
                roundNumber = _roundNumber,
                arenaSelectionActive = _arenaSelectionActive,
                selectedArena = _selectedArena,
                seatOneArenaVote = _seatOneArenaVote,
                seatTwoArenaVote = _seatTwoArenaVote,
                arenaSelectionCountdownRemaining = _arenaMismatchCountdownActive ? GetArenaCountdownRemainingSeconds() : -1f,
                matchEnded = _matchEnded,
                winningSeat = _winningSeat,
                matchEndMessage = _matchEndMessage ?? string.Empty,
                roundPhase = ToSnapshotPhase(_roundPhase),
                phaseSecondsRemaining = GetPhaseSecondsRemainingForSnapshot(),
                hotseatTestMode = _hotseatTestMode,
                highlightedCardIndex = exportedUiState != null ? exportedUiState.highlightedCardIndex : -1,
                selectedBoardTileIndex = _roundPhase == MatchRoundPhase.DisplayResolution
                    ? _selectedBoardTileIndex
                    : exportedUiState != null ? exportedUiState.selectedBoardTileIndex : -1,
                selectedAttackerTileIndex = _roundPhase == MatchRoundPhase.DisplayResolution
                    ? _selectedAttackerTileIndex
                    : exportedUiState != null ? exportedUiState.selectedAttackerTileIndex : -1,
                selectedWarShopOption = _roundPhase == MatchRoundPhase.DisplayResolution
                    ? -1
                    : exportedUiState != null ? exportedUiState.selectedWarShopOption : -1,
                activeTurnWarShopPurchaseUsed = _activeTurnSeat == MatchSeat.SeatOne
                    ? _warShopPurchaseUsedBySeat[0]
                    : _warShopPurchaseUsedBySeat[1],
                displayResolutionMode = ToSnapshotDisplayResolutionMode(_displayResolutionMode),
                displayStageLabel = _displayStageLabel ?? string.Empty,
                hasDisplayStageSeat = _displayStageSeat.HasValue,
                displayStageSeat = _displayStageSeat ?? MatchSeat.SeatOne,
                displayNarrationText = _displayNarrationText ?? string.Empty,
                awarenessOverrideText = _awarenessOverrideText ?? string.Empty,
                awarenessOverrideSecondsRemaining = GetAwarenessSecondsRemainingForSnapshot(),
                seatOne = CreateParticipantSnapshot(_seatOneState),
                seatTwo = CreateParticipantSnapshot(_seatTwoState)
            };

            int tileCount = _boardRows * _boardColumns;
            for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
            {
                snapshot.tiles.Add(new TileRuntimeSnapshot
                {
                    owner = _tileOwners[tileIndex],
                    areaKind = _tileAreaKinds[tileIndex],
                    currentHealth = _tileCurrentHealth[tileIndex],
                    maxHealth = _tileMaxHealth[tileIndex],
                    blocksCity = _tileBlocksCity[tileIndex],
                    locked = _tileLocked[tileIndex],
                    secureHoldTurns = _secureHoldTurnsByTile[tileIndex],
                    silenceTurns = _silenceTurnsByTile[tileIndex],
                    spawnChargeTurns = _spawnChargeTurnsByTile[tileIndex],
                    attackTargetTileIndex = _attackTargetTileBySource[tileIndex],
                    moveTargetTileIndex = _moveTargetTileBySource[tileIndex],
                    hasOccupant = _boardTileData[tileIndex] != null,
                    occupantSeat = _tileOccupantSeats[tileIndex] ?? MatchSeat.SeatOne,
                    occupantCurrentHealth = _occupantCurrentHealth[tileIndex],
                    occupantCard = CreateCardSnapshot(_boardTileData[tileIndex])
                });
            }

            for (int i = 0; i < _floatingBoardTexts.Count; i++)
            {
                FloatingBoardTextSnapshot floatingTextSnapshot = CreateFloatingBoardTextSnapshot(_floatingBoardTexts[i]);
                if (floatingTextSnapshot != null)
                {
                    snapshot.floatingBoardTexts.Add(floatingTextSnapshot);
                }
            }

            return snapshot;
        }

        public void ImportRuntimeSnapshotJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            MatchRuntimeSnapshot snapshot = JsonUtility.FromJson<MatchRuntimeSnapshot>(json);
            ImportRuntimeSnapshot(snapshot);
        }

        public void ImportTimerSyncSnapshotJson(string json)
        {
            ImportTimerSyncSnapshotJson(json, Time.unscaledTime);
        }

        public void ImportTimerSyncSnapshotJson(string json, double serverTimeSeconds)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            MatchTimerSyncSnapshot snapshot = JsonUtility.FromJson<MatchTimerSyncSnapshot>(json);
            ImportTimerSyncSnapshot(snapshot, serverTimeSeconds);
        }

        public void ImportTimerSyncSnapshot(MatchTimerSyncSnapshot snapshot)
        {
            ImportTimerSyncSnapshot(snapshot, Time.unscaledTime);
        }

        public void ImportTimerSyncSnapshot(MatchTimerSyncSnapshot snapshot, double serverTimeSeconds)
        {
            if (snapshot == null || !Application.isPlaying)
            {
                return;
            }

            MatchRoundPhase importedPhase = FromSnapshotPhase(snapshot.roundPhase);
            if (snapshot.roundNumber != _roundNumber
                || snapshot.activeTurnSeat != _activeTurnSeat
                || importedPhase != _roundPhase
                || _matchEnded)
            {
                return;
            }

            float phaseSecondsRemaining = snapshot.phaseEndsAtServerTime >= 0d
                ? Mathf.Max(0f, (float)(snapshot.phaseEndsAtServerTime - serverTimeSeconds))
                : snapshot.phaseSecondsRemaining;
            if (_roundPhase != MatchRoundPhase.DisplayResolution && phaseSecondsRemaining >= 0f)
            {
                _phaseEndsAtUnscaledTime = Time.unscaledTime + phaseSecondsRemaining;
                UpdatePhaseTimerUI();
            }

            if (snapshot.arenaSelectionActive
                && _arenaSelectionActive
                && snapshot.seatOneArenaVote == _seatOneArenaVote
                && snapshot.seatTwoArenaVote == _seatTwoArenaVote
                && (snapshot.arenaResolveAtServerTime >= 0d || snapshot.arenaSelectionCountdownRemaining >= 0f))
            {
                float arenaCountdownRemaining = snapshot.arenaResolveAtServerTime >= 0d
                    ? Mathf.Max(0f, (float)(snapshot.arenaResolveAtServerTime - serverTimeSeconds))
                    : snapshot.arenaSelectionCountdownRemaining;
                _arenaResolveAtUnscaledTime = Time.unscaledTime + arenaCountdownRemaining;
            }
        }

        public void ImportRuntimeSnapshot(MatchRuntimeSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            bool isRemoteReplica = _externalCommandSink != null;
            if (isRemoteReplica)
            {
                CaptureCurrentTransientUiState(_localSeat);
            }

            ClearResolveMotionVisuals();

            string[] previousOccupantSignatures = CaptureTileOccupantSignatures();
            bool shouldFlipBefore = ShouldFlipBoardRowsForCurrentView();
            int previousRows = _boardRows;
            int previousColumns = _boardColumns;
            MatchRoundPhase previousRoundPhase = _roundPhase;
            DisplayResolutionMode previousDisplayResolutionMode = _displayResolutionMode;
            MatchRoundPhase importedRoundPhase = FromSnapshotPhase(snapshot.roundPhase);
            DisplayResolutionMode importedDisplayResolutionMode = FromSnapshotDisplayResolutionMode(snapshot.displayResolutionMode);
            _boardRows = Mathf.Max(1, snapshot.rows);
            _boardColumns = Mathf.Max(1, snapshot.columns);
            _canonicalTopSeat = snapshot.canonicalTopSeat;
            MatchSeat importedLocalSeat = _externalCommandSink != null ? _localSeat : snapshot.localSeat;
            _localSeat = importedLocalSeat;
            _perspectiveSeat = importedLocalSeat;
            bool shouldFlipAfter = ShouldFlipBoardRowsForCurrentView();
            _controlMode = snapshot.controlMode;
            _activeTurnSeat = snapshot.activeTurnSeat;
            _roundInitiativeSeat = snapshot.roundInitiativeSeat;
            _roundNumber = snapshot.roundNumber;
            _arenaSelectionActive = snapshot.arenaSelectionActive;
            _selectedArena = snapshot.selectedArena;
            _seatOneArenaVote = snapshot.seatOneArenaVote;
            _seatTwoArenaVote = snapshot.seatTwoArenaVote;
            _arenaMismatchCountdownActive = _arenaSelectionActive
                && _seatOneArenaVote != ArenaId.None
                && _seatTwoArenaVote != ArenaId.None
                && _seatOneArenaVote != _seatTwoArenaVote;
            _arenaResolveAtUnscaledTime = Application.isPlaying
                && snapshot.arenaSelectionCountdownRemaining > 0f
                ? Time.unscaledTime + snapshot.arenaSelectionCountdownRemaining
                : -1f;
            _matchEnded = snapshot.matchEnded;
            _winningSeat = snapshot.winningSeat;
            _matchEndMessage = snapshot.matchEndMessage ?? string.Empty;
            _roundPhase = importedRoundPhase;
            _displayResolutionMode = importedDisplayResolutionMode;
            _phaseEndsAtUnscaledTime = Application.isPlaying && snapshot.phaseSecondsRemaining > 0f
                ? Time.unscaledTime + snapshot.phaseSecondsRemaining
                : -1f;
            _hotseatTestMode = snapshot.hotseatTestMode || _controlMode == MatchControlMode.Hotseat;
            _displayStageLabel = snapshot.displayStageLabel ?? string.Empty;
            _displayStageSeat = snapshot.hasDisplayStageSeat ? snapshot.displayStageSeat : (MatchSeat?)null;
            _displayNarrationText = snapshot.displayNarrationText ?? string.Empty;
            _awarenessOverrideText = snapshot.awarenessOverrideText ?? string.Empty;
            _awarenessOverrideExpiresAt = Application.isPlaying && snapshot.awarenessOverrideSecondsRemaining > 0f
                ? Time.unscaledTime + snapshot.awarenessOverrideSecondsRemaining
                : -1f;
            _autoAdvanceAtUnscaledTime = -1f;
            bool importedDisplayResolutionStarted = importedRoundPhase == MatchRoundPhase.DisplayResolution
                && (previousRoundPhase != MatchRoundPhase.DisplayResolution
                    || previousDisplayResolutionMode != importedDisplayResolutionMode);
            if (importedDisplayResolutionStarted)
            {
                _displayMovementResolved = false;
                _displayStruggleQueue.Clear();
                _displayMoveQueue.Clear();
                _displayAttackQueue.Clear();
                _displayStruggleQueueIndex = 0;
                _displayMoveQueueIndex = 0;
                _displayAttackQueueIndex = 0;
                _displayStrugglePrepared = false;
                _displayAttackPrepared = false;
                _displayMovePrepared = false;
                _skipAttackDisplayThisRound = false;

                for (int i = 0; i < _displayAutoTargetTileBySource.Length; i++)
                {
                    _displayAutoTargetTileBySource[i] = -1;
                    _displayMovementConsumedByTile[i] = false;
                }
            }

            bool preserveDisplayActionTimer = isRemoteReplica
                && previousRoundPhase == MatchRoundPhase.DisplayResolution
                && importedRoundPhase == MatchRoundPhase.DisplayResolution
                && previousDisplayResolutionMode == importedDisplayResolutionMode;
            if (!preserveDisplayActionTimer)
            {
                _nextDisplayActionAtUnscaledTime = Application.isPlaying && importedRoundPhase == MatchRoundPhase.DisplayResolution
                    ? Time.unscaledTime + DisplayAttackStepSeconds
                    : -1f;
            }
            else if (_nextDisplayActionAtUnscaledTime < 0f)
            {
                _nextDisplayActionAtUnscaledTime = Time.unscaledTime + DisplayAttackStepSeconds;
            }

            if (previousRows != _boardRows || previousColumns != _boardColumns || shouldFlipBefore != shouldFlipAfter)
            {
                InvalidateBoardVisualTree();
            }

            EnsureBoardRuntimeCapacity(_boardRows * _boardColumns);
            ResetBoardRuntimeToDefaults();

            Dictionary<string, CardTemplate> cardLookup = BuildKnownCardLookup();
            _seatOneState = CreateParticipantStateFromSnapshot(snapshot.seatOne, cardLookup);
            _seatTwoState = CreateParticipantStateFromSnapshot(snapshot.seatTwo, cardLookup);

            int tileCount = Mathf.Min(snapshot.tiles != null ? snapshot.tiles.Count : 0, _boardRows * _boardColumns);
            for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
            {
                TileRuntimeSnapshot tileSnapshot = snapshot.tiles[tileIndex];
                if (tileSnapshot == null)
                {
                    continue;
                }

                _tileOwners[tileIndex] = tileSnapshot.owner;
                _tileAreaKinds[tileIndex] = tileSnapshot.areaKind;
                _tileCurrentHealth[tileIndex] = tileSnapshot.currentHealth;
                _tileMaxHealth[tileIndex] = tileSnapshot.maxHealth;
                    _tileBlocksCity[tileIndex] = tileSnapshot.blocksCity;
                    _tileLocked[tileIndex] = tileSnapshot.locked;
                    _secureHoldTurnsByTile[tileIndex] = tileSnapshot.secureHoldTurns;
                    _silenceTurnsByTile[tileIndex] = tileSnapshot.silenceTurns;
                    _spawnChargeTurnsByTile[tileIndex] = tileSnapshot.spawnChargeTurns;
                    _attackTargetTileBySource[tileIndex] = tileSnapshot.attackTargetTileIndex;
                    _moveTargetTileBySource[tileIndex] = tileSnapshot.moveTargetTileIndex;
                _occupantCurrentHealth[tileIndex] = tileSnapshot.occupantCurrentHealth;
                _tileOccupantSeats[tileIndex] = tileSnapshot.hasOccupant ? tileSnapshot.occupantSeat : (MatchSeat?)null;
                _boardTileData[tileIndex] = tileSnapshot.hasOccupant
                    ? CreateCardTemplateFromSnapshot(tileSnapshot.occupantCard, cardLookup)
                    : null;
            }

            SanitizeBoardOccupancyState("snapshot import");

            if (importedDisplayResolutionStarted && importedDisplayResolutionMode == DisplayResolutionMode.Movement)
            {
                CaptureMovementPhaseStartingLocks();
            }

            SyncFloatingBoardTextsFromSnapshot(snapshot.floatingBoardTexts);
            _matchInitialized = true;
            _boardViewNeedsReset = true;
            SyncVisibleStateFromPerspective();

            if (!isRemoteReplica || _activeTurnSeat != importedLocalSeat)
            {
                SeatTransientUiState activeSeatState = GetTransientUiState(_activeTurnSeat);
                activeSeatState.highlightedCardIndex = snapshot.highlightedCardIndex;
                activeSeatState.selectedBoardTileIndex = snapshot.selectedBoardTileIndex;
                activeSeatState.selectedAttackerTileIndex = snapshot.selectedAttackerTileIndex;
                activeSeatState.selectedWarShopOption = snapshot.selectedWarShopOption;
                activeSeatState.placementFocusActive = false;
            }

            _highlightedCardIndex = _roundPhase != MatchRoundPhase.DisplayResolution && _activeTurnSeat == importedLocalSeat
                ? snapshot.highlightedCardIndex
                : -1;
            _selectedBoardTileIndex = snapshot.selectedBoardTileIndex;
            _selectedAttackerTileIndex = snapshot.selectedAttackerTileIndex;
            _selectedWarShopOption = snapshot.selectedWarShopOption;
            _placementFocusActive = false;
            _warShopPurchaseUsedBySeat[GetSeatIndex(_activeTurnSeat)] = snapshot.activeTurnWarShopPurchaseUsed;

            if (isRemoteReplica)
            {
                ApplyTransientUiState(importedLocalSeat);
            }

            if (_highlightedCardIndex < 0 || _highlightedCardIndex >= cardsInHand.Count)
            {
                _highlightedCardIndex = -1;
                if (_abilityPreviewCard != null && !IsCardVisibleToLocalUi(_abilityPreviewCard))
                {
                    _abilityPreviewCard = null;
                }
            }

            UpdateUI();
            PlayImportedDeploymentAnimations(previousOccupantSignatures);
        }

        private void ClearSelectionsAndRefresh()
        {
            if (_highlightedCardIndex != -1)
            {
                _highlightedCardIndex = -1;
                _placementFocusActive = false;
            }

            SetSelectedWarShopOption(WarShopOption.None);
            _selectedBoardTileIndex = -1;
            _selectedAttackerTileIndex = -1;
            SetAbilityPreviewCard(null);
            UpdateUI();
        }

        private void ClearActiveSelectionState()
        {
            _highlightedCardIndex = -1;
            SetSelectedWarShopOption(WarShopOption.None);
            _selectedBoardTileIndex = -1;
            _selectedAttackerTileIndex = -1;
            _placementFocusActive = false;
            SetAbilityPreviewCard(null);
            CaptureCurrentTransientUiState(_localSeat);
        }

        private void ShowInvalidActionAndClearSelection(string message)
        {
            ClearActiveSelectionState();
            ShowAwarenessMessage(message);
        }

        private void InvalidateBoardVisualTree()
        {
            ClearResolveMotionVisuals();
            _boardScrollView = null;
            _boardSurfaceElement = null;
            _boardOwnershipFrameElement = null;
            _boardOwnershipTimerLayerElement = null;
            _boardOwnershipTimerTopElement = null;
            _boardOwnershipTimerRightElement = null;
            _boardOwnershipTimerBottomElement = null;
            _boardOwnershipTimerLeftElement = null;
            _boardGridLayerElement = null;
            _boardEffectsLayerElement = null;
            _boardMotionLayerElement = null;
            _boardRowElements = null;
            _boardTileElements = null;
            _boardTileTextureLayers = null;
            _boardTileAreaOverlays = null;
            _boardTileOwnershipFrames = null;
            _boardTileSelectionGlows = null;
            _boardTileStatsBars = null;
            _boardTileHpLabels = null;
            _boardTileCardContents = null;
            _boardTileArtPlaceholders = null;
            _boardTileNameLabels = null;
            _boardTileAttackLabels = null;
            _boardTileRightStatClusters = null;
            _boardTileLockLabels = null;
            _boardTileAbilityLabels = null;
            _boardTileItemLabels = null;
            _boardTileIntentBadges = null;
            _boardTileInvalidMarkers = null;
            _boardTileDoomMarkers = null;
            _boardVisualTileCount = -1;
        }

        private SeatTransientUiState GetTransientUiState(MatchSeat seat)
        {
            return seat == MatchSeat.SeatOne ? _seatOneTransientUiState : _seatTwoTransientUiState;
        }

        private static int GetSeatIndex(MatchSeat seat)
        {
            return seat == MatchSeat.SeatOne ? 0 : 1;
        }

        private static WarShopOption NormalizeWarShopOption(int rawValue)
        {
            return Enum.IsDefined(typeof(WarShopOption), rawValue)
                ? (WarShopOption)rawValue
                : WarShopOption.None;
        }

        private void CaptureCurrentTransientUiState(MatchSeat seat)
        {
            SeatTransientUiState state = GetTransientUiState(seat);
            state.highlightedCardIndex = _highlightedCardIndex;
            state.selectedBoardTileIndex = _selectedBoardTileIndex;
            state.selectedAttackerTileIndex = _selectedAttackerTileIndex;
            state.selectedWarShopOption = _selectedWarShopOption;
            state.placementFocusActive = _placementFocusActive;
            state.abilityPreviewCard = _abilityPreviewCard;
            state.abilityPreviewText = _abilityPreviewText;
        }

        private void ApplyTransientUiState(MatchSeat seat)
        {
            SeatTransientUiState state = GetTransientUiState(seat);
            _highlightedCardIndex = state.highlightedCardIndex;
            _selectedBoardTileIndex = state.selectedBoardTileIndex;
            _selectedAttackerTileIndex = state.selectedAttackerTileIndex;
            _selectedWarShopOption = state.selectedWarShopOption;
            _placementFocusActive = state.placementFocusActive;
            _abilityPreviewCard = state.abilityPreviewCard;
            _abilityPreviewText = state.abilityPreviewText ?? string.Empty;
        }

        private WarShopOption GetSelectedWarShopOption()
        {
            return NormalizeWarShopOption(_selectedWarShopOption);
        }

        private void SetSelectedWarShopOption(WarShopOption option)
        {
            _selectedWarShopOption = (int)option;
        }

        private bool HasSelectedWarShopOption()
        {
            return GetSelectedWarShopOption() != WarShopOption.None;
        }

        private bool HasUsedWarShopPurchase(MatchSeat seat)
        {
            return _warShopPurchaseUsedBySeat[GetSeatIndex(seat)];
        }

        private void SetWarShopPurchaseUsed(MatchSeat seat, bool used)
        {
            _warShopPurchaseUsedBySeat[GetSeatIndex(seat)] = used;
        }

        private string BuildHandCarouselSignature()
        {
            if (_roundPhase != MatchRoundPhase.DeployPlanning || cardsInHand == null || cardsInHand.Count == 0)
            {
                return $"{_roundPhase}|empty";
            }

            var parts = new System.Text.StringBuilder();
            parts.Append((int)_roundPhase).Append('|');
            for (int i = 0; i < cardsInHand.Count; i++)
            {
                CardTemplate cardData = cardsInHand[i];
                if (cardData == null)
                {
                    parts.Append("null;");
                    continue;
                }

                parts.Append(cardData.cardId)
                    .Append(':')
                    .Append(cardData.cardName)
                    .Append(':')
                    .Append(GetEffectiveDeploymentCost(cardData, _localSeat))
                    .Append(':')
                    .Append(cardData.health)
                    .Append(':')
                    .Append(cardData.attack)
                    .Append(':')
                    .Append(cardData.attachedItemCard != null ? cardData.attachedItemCard.cardId : "noitem")
                    .Append(':')
                    .Append(GetAbilitySignature(cardData))
                    .Append(';');
            }

            return parts.ToString();
        }

        private static string GetAbilitySignature(CardTemplate cardData)
        {
            if (cardData == null || cardData.keywordEffects == null || cardData.keywordEffects.Count == 0)
            {
                return "none";
            }

            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < cardData.keywordEffects.Count; i++)
            {
                AbilityEffectData effect = cardData.keywordEffects[i];
                if (effect == null || effect.keyword == AbilityKeyword.None)
                {
                    continue;
                }

                builder.Append(effect.keyword).Append(effect.value).Append(',');
            }

            return builder.Length > 0 ? builder.ToString() : "none";
        }

        private MatchRoundPhaseSnapshot ToSnapshotPhase(MatchRoundPhase phase)
        {
            switch (phase)
            {
                case MatchRoundPhase.CombatPlanning:
                    return MatchRoundPhaseSnapshot.CombatPlanning;
                case MatchRoundPhase.DisplayResolution:
                    return MatchRoundPhaseSnapshot.DisplayResolution;
                default:
                    return MatchRoundPhaseSnapshot.DeployPlanning;
            }
        }

        private DisplayResolutionModeSnapshot ToSnapshotDisplayResolutionMode(DisplayResolutionMode mode)
        {
            return mode == DisplayResolutionMode.Movement
                ? DisplayResolutionModeSnapshot.Movement
                : DisplayResolutionModeSnapshot.Attack;
        }

        private float GetPhaseSecondsRemainingForSnapshot()
        {
            if (!Application.isPlaying || _phaseEndsAtUnscaledTime < 0f)
            {
                return -1f;
            }

            return Mathf.Max(0f, _phaseEndsAtUnscaledTime - Time.unscaledTime);
        }

        private MatchRoundPhase FromSnapshotPhase(MatchRoundPhaseSnapshot phase)
        {
            switch (phase)
            {
                case MatchRoundPhaseSnapshot.CombatPlanning:
                    return MatchRoundPhase.CombatPlanning;
                case MatchRoundPhaseSnapshot.DisplayResolution:
                    return MatchRoundPhase.DisplayResolution;
                default:
                    return MatchRoundPhase.DeployPlanning;
            }
        }

        private DisplayResolutionMode FromSnapshotDisplayResolutionMode(DisplayResolutionModeSnapshot mode)
        {
            return mode == DisplayResolutionModeSnapshot.Movement
                ? DisplayResolutionMode.Movement
                : DisplayResolutionMode.Attack;
        }

        private float GetAwarenessSecondsRemainingForSnapshot()
        {
            if (!Application.isPlaying || _awarenessOverrideExpiresAt < 0f || string.IsNullOrWhiteSpace(_awarenessOverrideText))
            {
                return -1f;
            }

            return Mathf.Max(0f, _awarenessOverrideExpiresAt - Time.unscaledTime);
        }

        private FloatingBoardTextSnapshot CreateFloatingBoardTextSnapshot(FloatingBoardTextRuntime floatingText)
        {
            if (floatingText == null || floatingText.tileIndex < 0 || string.IsNullOrWhiteSpace(floatingText.text))
            {
                return null;
            }

            float secondsRemaining = Application.isPlaying
                ? Mathf.Max(0f, floatingText.expiresAt - Time.unscaledTime)
                : FloatingTextDurationSeconds;
            if (Application.isPlaying && secondsRemaining <= 0f)
            {
                return null;
            }

            return new FloatingBoardTextSnapshot
            {
                tileIndex = floatingText.tileIndex,
                text = floatingText.text,
                cssClass = string.IsNullOrWhiteSpace(floatingText.cssClass) ? "tile-floating-damage" : floatingText.cssClass,
                secondsRemaining = secondsRemaining
            };
        }

        private void SyncFloatingBoardTextsFromSnapshot(List<FloatingBoardTextSnapshot> snapshots)
        {
            _floatingBoardTexts.Clear();
            if (snapshots == null || snapshots.Count == 0)
            {
                return;
            }

            float now = Application.isPlaying ? Time.unscaledTime : 0f;
            for (int i = 0; i < snapshots.Count; i++)
            {
                FloatingBoardTextSnapshot snapshot = snapshots[i];
                if (snapshot == null || snapshot.tileIndex < 0 || string.IsNullOrWhiteSpace(snapshot.text))
                {
                    continue;
                }

                _floatingBoardTexts.Add(new FloatingBoardTextRuntime
                {
                    tileIndex = snapshot.tileIndex,
                    text = snapshot.text,
                    cssClass = string.IsNullOrWhiteSpace(snapshot.cssClass) ? "tile-floating-damage" : snapshot.cssClass,
                    expiresAt = now + Mathf.Max(0.01f, snapshot.secondsRemaining)
                });
            }
        }

        private string[] CaptureTileOccupantSignatures()
        {
            if (_boardTileData == null || _boardTileData.Length == 0)
            {
                return Array.Empty<string>();
            }

            var signatures = new string[_boardTileData.Length];
            for (int i = 0; i < _boardTileData.Length; i++)
            {
                signatures[i] = BuildTileOccupantSignature(i);
            }

            return signatures;
        }

        private string BuildTileOccupantSignature(int tileIndex)
        {
            if (_boardTileData == null
                || tileIndex < 0
                || tileIndex >= _boardTileData.Length
                || _boardTileData[tileIndex] == null
                || !_tileOccupantSeats[tileIndex].HasValue)
            {
                return string.Empty;
            }

            CardTemplate cardData = _boardTileData[tileIndex];
            string cardKey = !string.IsNullOrWhiteSpace(cardData.cardId)
                ? cardData.cardId
                : $"{cardData.cardName}|{cardData.cardType}|{cardData.health}|{cardData.attack}";
            return $"{_tileOccupantSeats[tileIndex].Value}|{cardKey}";
        }

        private void PlayImportedDeploymentAnimations(string[] previousOccupantSignatures)
        {
            if (_roundPhase != MatchRoundPhase.DeployPlanning
                || previousOccupantSignatures == null
                || previousOccupantSignatures.Length == 0)
            {
                return;
            }

            int tileCount = Mathf.Min(previousOccupantSignatures.Length, _boardTileData != null ? _boardTileData.Length : 0);
            for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
            {
                string previousSignature = previousOccupantSignatures[tileIndex];
                string currentSignature = BuildTileOccupantSignature(tileIndex);
                if (!string.IsNullOrEmpty(previousSignature)
                    || string.IsNullOrEmpty(currentSignature))
                {
                    continue;
                }

                AnimateBoardTileDeployment(tileIndex);
            }
        }

        private void AnimateBoardTileDeployment(int tileIndex)
        {
            if (_boardTileCardContents == null
                || tileIndex < 0
                || tileIndex >= _boardTileCardContents.Length
                || _boardTileCardContents[tileIndex] == null)
            {
                _root?.schedule.Execute(() => AnimateBoardTileDeployment(tileIndex)).StartingIn(16);
                return;
            }

            VisualElement tileCardContent = _boardTileCardContents[tileIndex];
            tileCardContent.RemoveFromClassList("tile-deployed-active");
            tileCardContent.RemoveFromClassList("tile-deployed-swoosh");
            tileCardContent.AddToClassList("tile-deployed-swoosh");
            tileCardContent.schedule.Execute(() =>
            {
                tileCardContent.AddToClassList("tile-deployed-active");
            }).StartingIn(0);
            tileCardContent.schedule.Execute(() =>
            {
                tileCardContent.RemoveFromClassList("tile-deployed-active");
                tileCardContent.RemoveFromClassList("tile-deployed-swoosh");
            }).StartingIn(480);
        }

        private ParticipantRuntimeSnapshot CreateParticipantSnapshot(ParticipantRuntimeState state)
        {
            if (state == null)
            {
                return null;
            }

            var snapshot = new ParticipantRuntimeSnapshot
            {
                seat = state.seat,
                cityName = state.cityName,
                health = state.health,
                treasury = state.treasury,
                turnStartDrawCount = state.turnStartDrawCount,
                maxHandSize = state.maxHandSize,
                baseTreasuryIncome = state.baseTreasuryIncome,
                deployTurnsTaken = state.deployTurnsTaken
            };

            foreach (CardTemplate card in state.hand)
            {
                snapshot.hand.Add(CreateCardSnapshot(card));
            }

            foreach (CardTemplate card in state.drawPile)
            {
                snapshot.drawPile.Add(CreateCardSnapshot(card));
            }

            foreach (CardTemplate card in state.discardPile)
            {
                snapshot.discardPile.Add(CreateCardSnapshot(card));
            }

            foreach (CardTemplate card in state.burnPile)
            {
                snapshot.burnPile.Add(CreateCardSnapshot(card));
            }

            return snapshot;
        }

        private ParticipantRuntimeState CreateParticipantStateFromSnapshot(ParticipantRuntimeSnapshot snapshot, Dictionary<string, CardTemplate> cardLookup)
        {
            if (snapshot == null)
            {
                return new ParticipantRuntimeState();
            }

            var state = new ParticipantRuntimeState
            {
                seat = snapshot.seat,
                cityName = snapshot.cityName,
                health = snapshot.health,
                treasury = snapshot.treasury,
                turnStartDrawCount = snapshot.turnStartDrawCount,
                maxHandSize = snapshot.maxHandSize > 0 ? snapshot.maxHandSize : DefaultMaxRealHandSize,
                baseTreasuryIncome = snapshot.baseTreasuryIncome,
                deployTurnsTaken = snapshot.deployTurnsTaken
            };

            foreach (CardRuntimeSnapshot cardSnapshot in snapshot.hand)
            {
                state.hand.Add(CreateCardTemplateFromSnapshot(cardSnapshot, cardLookup));
            }

            foreach (CardRuntimeSnapshot cardSnapshot in snapshot.drawPile)
            {
                state.drawPile.Add(CreateCardTemplateFromSnapshot(cardSnapshot, cardLookup));
            }

            foreach (CardRuntimeSnapshot cardSnapshot in snapshot.discardPile)
            {
                state.discardPile.Add(CreateCardTemplateFromSnapshot(cardSnapshot, cardLookup));
            }

            foreach (CardRuntimeSnapshot cardSnapshot in snapshot.burnPile)
            {
                state.burnPile.Add(CreateCardTemplateFromSnapshot(cardSnapshot, cardLookup));
            }

            return state;
        }

        private CardRuntimeSnapshot CreateCardSnapshot(CardTemplate cardData)
        {
            if (cardData == null)
            {
                return null;
            }

            return new CardRuntimeSnapshot
            {
                cardId = cardData.cardId,
                cardName = cardData.cardName,
                treasuryCost = cardData.treasuryCost,
                cardType = cardData.cardType,
                health = cardData.health,
                attack = cardData.attack,
                range = cardData.range,
                movementRange = cardData.movementRange,
                unitTag = cardData.unitTag,
                infrastructureKind = cardData.infrastructureKind,
                commandCardKind = cardData.commandCardKind,
                abilityText = cardData.GetAbilitySummaryText(),
                detailedAbilityText = cardData.GetDetailedAbilityText(),
                keywordEffects = CreateAbilityEffectSnapshots(cardData.keywordEffects),
                attachedItemCard = cardData.attachedItemCard != null ? CreateCardSnapshot(cardData.attachedItemCard) : null,
                bonusHealth = cardData.bonusHealth,
                bonusAttack = cardData.bonusAttack,
                bonusRange = cardData.bonusRange,
                bonusMovementRange = cardData.bonusMovementRange,
                bonusSiegeAttack = cardData.bonusSiegeAttack
            };
        }

        private List<AbilityEffectRuntimeSnapshot> CreateAbilityEffectSnapshots(List<AbilityEffectData> effects)
        {
            var snapshots = new List<AbilityEffectRuntimeSnapshot>();
            if (effects == null)
            {
                return snapshots;
            }

            for (int i = 0; i < effects.Count; i++)
            {
                AbilityEffectData effect = effects[i];
                if (effect == null || effect.keyword == AbilityKeyword.None)
                {
                    continue;
                }

                snapshots.Add(new AbilityEffectRuntimeSnapshot
                {
                    keyword = effect.keyword,
                    value = effect.value,
                    trigger = effect.trigger,
                    duration = effect.duration,
                    durationTurns = effect.durationTurns,
                    targetScope = effect.targetScope,
                    targetCardType = effect.targetCardType,
                    targetUnitTag = effect.targetUnitTag,
                    targetInfrastructureKind = effect.targetInfrastructureKind,
                    range = effect.range,
                    shortDescription = effect.shortDescription,
                    detailedDescription = effect.detailedDescription
                });
            }

            return snapshots;
        }

        private Dictionary<string, CardTemplate> BuildKnownCardLookup()
        {
            var lookup = new Dictionary<string, CardTemplate>();

            void RegisterCard(CardTemplate cardData)
            {
                if (cardData == null || string.IsNullOrWhiteSpace(cardData.cardId) || lookup.ContainsKey(cardData.cardId))
                {
                    return;
                }

                lookup[cardData.cardId] = cardData;
            }

            void RegisterParticipantCards(MatchParticipantDefinition participant)
            {
                if (participant == null)
                {
                    return;
                }

                if (participant.deck != null)
                {
                    foreach (CardTemplate cardData in participant.deck.cards)
                    {
                        RegisterCard(cardData);
                    }
                }

                if (participant.city != null && participant.city.defaultDeck != null)
                {
                    foreach (CardTemplate cardData in participant.city.defaultDeck.cards)
                    {
                        RegisterCard(cardData);
                    }
                }

                foreach (CardTemplate cardData in participant.openingHand)
                {
                    RegisterCard(cardData);
                }
            }

            if (prototypeMatch != null)
            {
                RegisterParticipantCards(prototypeMatch.seatOne);
                RegisterParticipantCards(prototypeMatch.seatTwo);

                foreach (StartingCardPlacement placement in prototypeMatch.startingCardPlacements)
                {
                    RegisterCard(placement.card);
                }
            }

            if (_seatOneState != null)
            {
                foreach (CardTemplate cardData in _seatOneState.hand)
                {
                    RegisterCard(cardData);
                }

                foreach (CardTemplate cardData in _seatOneState.drawPile)
                {
                    RegisterCard(cardData);
                }

                foreach (CardTemplate cardData in _seatOneState.discardPile)
                {
                    RegisterCard(cardData);
                }

                foreach (CardTemplate cardData in _seatOneState.burnPile)
                {
                    RegisterCard(cardData);
                }
            }

            if (_seatTwoState != null)
            {
                foreach (CardTemplate cardData in _seatTwoState.hand)
                {
                    RegisterCard(cardData);
                }

                foreach (CardTemplate cardData in _seatTwoState.drawPile)
                {
                    RegisterCard(cardData);
                }

                foreach (CardTemplate cardData in _seatTwoState.discardPile)
                {
                    RegisterCard(cardData);
                }

                foreach (CardTemplate cardData in _seatTwoState.burnPile)
                {
                    RegisterCard(cardData);
                }
            }

            if (_boardTileData != null)
            {
                foreach (CardTemplate cardData in _boardTileData)
                {
                    RegisterCard(cardData);
                }
            }

            return lookup;
        }

        private CardTemplate CreateCardTemplateFromSnapshot(CardRuntimeSnapshot snapshot, Dictionary<string, CardTemplate> cardLookup)
        {
            if (snapshot == null)
            {
                return null;
            }

            CardTemplate sourceTemplate = null;
            if (cardLookup != null && !string.IsNullOrWhiteSpace(snapshot.cardId))
            {
                cardLookup.TryGetValue(snapshot.cardId, out sourceTemplate);
            }

            CardTemplate cardData = ScriptableObject.CreateInstance<CardTemplate>();
            cardData.cardId = string.IsNullOrWhiteSpace(snapshot.cardId) ? $"runtime.{System.Guid.NewGuid():N}" : snapshot.cardId;
            cardData.cardName = snapshot.cardName;
            cardData.treasuryCost = snapshot.treasuryCost;
            cardData.cardType = snapshot.cardType;
            cardData.health = snapshot.health;
            cardData.attack = snapshot.attack;
            cardData.range = snapshot.range;
            cardData.movementRange = snapshot.movementRange;
            cardData.unitTag = snapshot.unitTag;
            cardData.infrastructureKind = snapshot.infrastructureKind;
            cardData.commandCardKind = snapshot.commandCardKind;
            cardData.abilityText = snapshot.abilityText;
            cardData.detailedAbilityText = snapshot.detailedAbilityText;
            cardData.keywordEffects = snapshot.keywordEffects != null && snapshot.keywordEffects.Count > 0
                ? CreateAbilityEffectsFromSnapshots(snapshot.keywordEffects)
                : sourceTemplate != null && sourceTemplate.keywordEffects != null
                    ? CloneAbilityEffects(sourceTemplate.keywordEffects)
                    : new List<AbilityEffectData>();
            cardData.attachedItemCard = snapshot.attachedItemCard != null
                ? CreateCardTemplateFromSnapshot(snapshot.attachedItemCard, cardLookup)
                : null;
            cardData.bonusHealth = snapshot.bonusHealth;
            cardData.bonusAttack = snapshot.bonusAttack;
            cardData.bonusRange = snapshot.bonusRange;
            cardData.bonusMovementRange = snapshot.bonusMovementRange;
            cardData.bonusSiegeAttack = snapshot.bonusSiegeAttack;
            cardData.customArt = sourceTemplate != null ? sourceTemplate.customArt : null;
            return cardData;
        }

        private List<AbilityEffectData> CreateAbilityEffectsFromSnapshots(List<AbilityEffectRuntimeSnapshot> snapshots)
        {
            var effects = new List<AbilityEffectData>();
            if (snapshots == null)
            {
                return effects;
            }

            for (int i = 0; i < snapshots.Count; i++)
            {
                AbilityEffectRuntimeSnapshot snapshot = snapshots[i];
                if (snapshot == null || snapshot.keyword == AbilityKeyword.None)
                {
                    continue;
                }

                effects.Add(new AbilityEffectData
                {
                    keyword = snapshot.keyword,
                    value = snapshot.value,
                    trigger = snapshot.trigger,
                    duration = snapshot.duration,
                    durationTurns = snapshot.durationTurns,
                    targetScope = snapshot.targetScope,
                    targetCardType = snapshot.targetCardType,
                    targetUnitTag = snapshot.targetUnitTag,
                    targetInfrastructureKind = snapshot.targetInfrastructureKind,
                    range = snapshot.range,
                    shortDescription = snapshot.shortDescription,
                    detailedDescription = snapshot.detailedDescription
                });
            }

            return effects;
        }

        private static List<AbilityEffectData> CloneAbilityEffects(List<AbilityEffectData> sourceEffects)
        {
            var effects = new List<AbilityEffectData>();
            if (sourceEffects == null)
            {
                return effects;
            }

            for (int i = 0; i < sourceEffects.Count; i++)
            {
                AbilityEffectData source = sourceEffects[i];
                if (source == null)
                {
                    continue;
                }

                effects.Add(new AbilityEffectData
                {
                    keyword = source.keyword,
                    value = source.value,
                    trigger = source.trigger,
                    duration = source.duration,
                    durationTurns = source.durationTurns,
                    targetScope = source.targetScope,
                    targetCardType = source.targetCardType,
                    targetUnitTag = source.targetUnitTag,
                    targetInfrastructureKind = source.targetInfrastructureKind,
                    range = source.range,
                    spawnedCard = source.spawnedCard,
                    shortDescription = source.shortDescription,
                    detailedDescription = source.detailedDescription
                });
            }

            return effects;
        }

        private static bool CardHasKeyword(CardTemplate cardData, AbilityKeyword keyword)
        {
            if (cardData == null || keyword == AbilityKeyword.None || cardData.keywordEffects == null)
            {
                return false;
            }

            for (int i = 0; i < cardData.keywordEffects.Count; i++)
            {
                AbilityEffectData effect = cardData.keywordEffects[i];
                if (effect != null && effect.keyword == keyword)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsStackableKeyword(AbilityKeyword keyword)
        {
            switch (keyword)
            {
                case AbilityKeyword.Gather:
                case AbilityKeyword.Siphon:
                case AbilityKeyword.Discount:
                case AbilityKeyword.Strike:
                case AbilityKeyword.Intercept:
                case AbilityKeyword.Secure:
                case AbilityKeyword.Reclaim:
                case AbilityKeyword.Sprint:
                case AbilityKeyword.Lock:
                case AbilityKeyword.Silence:
                case AbilityKeyword.Salvage:
                    return true;
                default:
                    return false;
            }
        }

        private static int GetKeywordValue(CardTemplate cardData, AbilityKeyword keyword)
        {
            if (cardData == null || keyword == AbilityKeyword.None || cardData.keywordEffects == null)
            {
                return 0;
            }

            int total = 0;
            for (int i = 0; i < cardData.keywordEffects.Count; i++)
            {
                AbilityEffectData effect = cardData.keywordEffects[i];
                if (effect != null && effect.keyword == keyword)
                {
                    total += Mathf.Max(1, effect.value);
                }
            }

            return total;
        }

        private static bool CardHasAnyKeyword(CardTemplate cardData)
        {
            if (cardData == null || cardData.keywordEffects == null)
            {
                return false;
            }

            for (int i = 0; i < cardData.keywordEffects.Count; i++)
            {
                AbilityEffectData effect = cardData.keywordEffects[i];
                if (effect != null && effect.keyword != AbilityKeyword.None)
                {
                    return true;
                }
            }

            return false;
        }

        private static AbilityEffectData GetPrimaryKeywordEffect(CardTemplate cardData)
        {
            if (cardData == null || cardData.keywordEffects == null)
            {
                return null;
            }

            for (int i = 0; i < cardData.keywordEffects.Count; i++)
            {
                AbilityEffectData effect = cardData.keywordEffects[i];
                if (effect != null && effect.keyword != AbilityKeyword.None)
                {
                    return effect;
                }
            }

            return null;
        }

        private bool TryAddOrStackKeyword(CardTemplate targetCard, AbilityEffectData sourceEffect)
        {
            if (targetCard == null || sourceEffect == null || sourceEffect.keyword == AbilityKeyword.None)
            {
                return false;
            }

            if (targetCard.keywordEffects == null)
            {
                targetCard.keywordEffects = new List<AbilityEffectData>();
            }

            for (int i = 0; i < targetCard.keywordEffects.Count; i++)
            {
                AbilityEffectData existing = targetCard.keywordEffects[i];
                if (existing == null || existing.keyword != sourceEffect.keyword)
                {
                    continue;
                }

                if (!IsStackableKeyword(sourceEffect.keyword))
                {
                    return false;
                }

                existing.value = Mathf.Max(1, existing.value) + Mathf.Max(1, sourceEffect.value);
                if (!string.IsNullOrWhiteSpace(sourceEffect.shortDescription))
                {
                    existing.shortDescription = sourceEffect.shortDescription;
                }
                if (!string.IsNullOrWhiteSpace(sourceEffect.detailedDescription))
                {
                    existing.detailedDescription = sourceEffect.detailedDescription;
                }
                return true;
            }

            if (CardHasAnyKeyword(targetCard) && !IsStackableKeyword(sourceEffect.keyword))
            {
                return false;
            }

            targetCard.keywordEffects.Add(new AbilityEffectData
            {
                keyword = sourceEffect.keyword,
                value = sourceEffect.value,
                trigger = sourceEffect.trigger,
                duration = sourceEffect.duration,
                durationTurns = sourceEffect.durationTurns,
                targetScope = sourceEffect.targetScope,
                targetCardType = sourceEffect.targetCardType,
                targetUnitTag = sourceEffect.targetUnitTag,
                targetInfrastructureKind = sourceEffect.targetInfrastructureKind,
                range = sourceEffect.range,
                spawnedCard = sourceEffect.spawnedCard,
                shortDescription = sourceEffect.shortDescription,
                detailedDescription = sourceEffect.detailedDescription
            });
            return true;
        }

        private static bool TryConsumeKeywordValue(CardTemplate cardData, AbilityKeyword keyword, int amount = 1)
        {
            if (cardData == null || keyword == AbilityKeyword.None || amount <= 0 || cardData.keywordEffects == null)
            {
                return false;
            }

            for (int i = 0; i < cardData.keywordEffects.Count; i++)
            {
                AbilityEffectData effect = cardData.keywordEffects[i];
                if (effect == null || effect.keyword != keyword)
                {
                    continue;
                }

                effect.value = Mathf.Max(0, effect.value - amount);
                if (effect.value <= 0)
                {
                    cardData.keywordEffects.RemoveAt(i);
                }

                return true;
            }

            return false;
        }

        private bool CardHasKeywordAtTile(int tileIndex, AbilityKeyword keyword)
        {
            CardTemplate cardData = tileIndex >= 0 && tileIndex < _boardTileData.Length ? _boardTileData[tileIndex] : null;
            bool suppressIntrinsicKeywords = IsSilencedAtTile(tileIndex) && keyword != AbilityKeyword.Silence;
            if (!suppressIntrinsicKeywords && CardHasKeyword(cardData, keyword))
            {
                return true;
            }

            if (!IsUnitCard(cardData) || !_tileOccupantSeats[tileIndex].HasValue)
            {
                return false;
            }

            MatchSeat seat = _tileOccupantSeats[tileIndex].Value;
            switch (keyword)
            {
                case AbilityKeyword.Intercept:
                    return IsAffectedByInfrastructureKeyword(tileIndex, seat, "card.free_haven.gatehouse");
                case AbilityKeyword.Maneuver:
                    return IsAffectedByInfrastructureKeyword(tileIndex, seat, "card.free_haven.beacon");
                case AbilityKeyword.Shatter:
                    return IsAffectedByInfrastructureKeyword(tileIndex, seat, "card.iron_citadel.smelter");
                default:
                    return false;
            }
        }

        private int GetEffectiveKeywordValueAtTile(int tileIndex, AbilityKeyword keyword)
        {
            CardTemplate cardData = tileIndex >= 0 && tileIndex < _boardTileData.Length ? _boardTileData[tileIndex] : null;
            int value = IsSilencedAtTile(tileIndex) && keyword != AbilityKeyword.Silence
                ? 0
                : GetKeywordValue(cardData, keyword);
            if (!IsUnitCard(cardData) || !_tileOccupantSeats[tileIndex].HasValue)
            {
                return value;
            }

            MatchSeat seat = _tileOccupantSeats[tileIndex].Value;
            if (keyword == AbilityKeyword.Intercept && IsAffectedByInfrastructureKeyword(tileIndex, seat, "card.free_haven.gatehouse"))
            {
                value += 1;
            }

            if (value <= 0 && CardHasKeywordAtTile(tileIndex, keyword))
            {
                value = 1;
            }

            return value;
        }

        private bool IsFriendlyCardWithKeywordAtTile(int tileIndex, MatchSeat seat, AbilityKeyword keyword)
        {
            return tileIndex >= 0
                && tileIndex < _boardTileData.Length
                && _tileOccupantSeats[tileIndex].HasValue
                && _tileOccupantSeats[tileIndex].Value == seat
                && _occupantCurrentHealth[tileIndex] > 0
                && CardHasKeywordAtTile(tileIndex, keyword);
        }

        private bool IsAffectedByInfrastructureKeyword(int unitTileIndex, MatchSeat unitSeat, string infrastructureCardId)
        {
            if (unitTileIndex < 0 || unitTileIndex >= _boardTileData.Length || string.IsNullOrWhiteSpace(infrastructureCardId))
            {
                return false;
            }

            for (int tileIndex = 0; tileIndex < _boardTileData.Length; tileIndex++)
            {
                CardTemplate infrastructure = _boardTileData[tileIndex];
                if (!IsInfrastructureCard(infrastructure)
                    || infrastructure.cardId != infrastructureCardId
                    || !_tileOccupantSeats[tileIndex].HasValue
                    || _tileOccupantSeats[tileIndex].Value != unitSeat
                    || _occupantCurrentHealth[tileIndex] <= 0
                    || IsSilencedAtTile(tileIndex))
                {
                    continue;
                }

                if (InfrastructureAffectsTile(infrastructureCardId, tileIndex, unitTileIndex, unitSeat))
                {
                    return true;
                }
            }

            return false;
        }

        private bool InfrastructureAffectsTile(string infrastructureCardId, int infrastructureTileIndex, int targetTileIndex, MatchSeat ownerSeat)
        {
            if (!TryGetRowColumnFromTileIndex(infrastructureTileIndex, out int sourceRow, out int sourceColumn)
                || !TryGetRowColumnFromTileIndex(targetTileIndex, out int targetRow, out int targetColumn))
            {
                return false;
            }

            int rowDelta = Mathf.Abs(targetRow - sourceRow);
            int columnDelta = Mathf.Abs(targetColumn - sourceColumn);
            bool orthogonalAdjacent = rowDelta + columnDelta == 1;
            bool diagonalAdjacent = rowDelta == 1 && columnDelta == 1;
            bool sameColumn = sourceColumn == targetColumn;

            switch (infrastructureCardId)
            {
                case "card.free_haven.gatehouse":
                case "card.iron_citadel.outpost":
                    return orthogonalAdjacent;
                case "card.iron_citadel.smelter":
                    return sameColumn;
                case "card.free_haven.beacon":
                    return true;
                default:
                    return false;
            }
        }

        private int GetInfrastructureAttackBonusForTile(int unitTileIndex, MatchSeat unitSeat)
        {
            int bonus = 0;
            for (int tileIndex = 0; tileIndex < _boardTileData.Length; tileIndex++)
            {
                CardTemplate infrastructure = _boardTileData[tileIndex];
                if (!IsInfrastructureCard(infrastructure)
                    || !_tileOccupantSeats[tileIndex].HasValue
                    || _tileOccupantSeats[tileIndex].Value != unitSeat
                    || _occupantCurrentHealth[tileIndex] <= 0
                    || IsSilencedAtTile(tileIndex))
                {
                    continue;
                }

                if (infrastructure.cardId == "card.iron_citadel.outpost"
                    && InfrastructureAffectsTile(infrastructure.cardId, tileIndex, unitTileIndex, unitSeat))
                {
                    bonus += 2;
                }
            }

            return bonus;
        }

        private int GetInfrastructureMovementBonusForTile(int unitTileIndex, MatchSeat unitSeat)
        {
            return 0;
        }

        private int CountFriendlyCardsWithKeywordOnBoard(MatchSeat seat, AbilityKeyword keyword)
        {
            int count = 0;
            for (int i = 0; i < _boardTileData.Length; i++)
            {
                if (IsFriendlyCardWithKeywordAtTile(i, seat, keyword))
                {
                    count++;
                }
            }

            return count;
        }

        private bool TryGetTileSeat(int tileIndex, out MatchSeat seat)
        {
            seat = MatchSeat.SeatOne;
            MatchSeat? tileSeat = tileIndex >= 0 && tileIndex < _boardTileData.Length
                ? _tileOccupantSeats[tileIndex]
                : null;
            if (!tileSeat.HasValue)
            {
                return false;
            }

            seat = tileSeat.Value;
            return true;
        }

        private bool IsStandardCivilianCard(CardTemplate cardData)
        {
            return cardData != null
                && IsUnitCard(cardData)
                && cardData.unitTag == UnitTag.Civilian;
        }

        private int GetEffectiveDeploymentCost(CardTemplate cardData, MatchSeat seat)
        {
            if (cardData == null)
            {
                return 0;
            }

            int discount = GetKeywordValue(cardData, AbilityKeyword.Discount);
            if (cardData.unitTag == UnitTag.Military && HasStandingInfrastructure(seat, "card.iron_citadel.warforge"))
            {
                discount += 2;
            }

            if ((cardData.cardType == CardType.Ordinance || cardData.cardType == CardType.Item)
                && HasStandingInfrastructure(seat, "card.free_haven.workshop"))
            {
                discount += 1;
            }

            return Mathf.Max(0, cardData.treasuryCost - discount);
        }

        private bool CanAffordCard(CardTemplate cardData, MatchSeat seat)
        {
            if (cardData == null)
            {
                return false;
            }

            ParticipantRuntimeState state = GetRuntimeState(seat);
            if (state == null)
            {
                return true;
            }

            return GetEffectiveDeploymentCost(cardData, seat) <= state.treasury;
        }

        private static int GetPrintedHealth(CardTemplate cardData)
        {
            return cardData != null ? Mathf.Max(0, cardData.health) : 0;
        }

        private static int GetPrintedAttack(CardTemplate cardData)
        {
            return cardData != null ? Mathf.Max(0, cardData.attack + cardData.bonusAttack) : 0;
        }

        private static int GetPrintedRange(CardTemplate cardData)
        {
            return cardData != null ? Mathf.Max(0, cardData.range + cardData.bonusRange) : 0;
        }

        private static int GetPrintedMovementRange(CardTemplate cardData)
        {
            return cardData != null ? Mathf.Max(0, cardData.movementRange + cardData.bonusMovementRange) : 0;
        }

        private int GetCurrentAttackValueForTile(int tileIndex, bool includeAttackPhaseBonuses)
        {
            CardTemplate cardData = tileIndex >= 0 && tileIndex < _boardTileData.Length ? _boardTileData[tileIndex] : null;
            int attackValue = GetPrintedAttack(cardData);
            if (!includeAttackPhaseBonuses || cardData == null || !IsUnitCard(cardData) || !TryGetTileSeat(tileIndex, out MatchSeat seat))
            {
                return attackValue;
            }

            return attackValue + GetKeywordValue(cardData, AbilityKeyword.Strike) + GetInfrastructureAttackBonusForTile(tileIndex, seat);
        }

        private bool HasStandingInfrastructure(MatchSeat seat, string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return false;
            }

            for (int tileIndex = 0; tileIndex < _boardTileData.Length; tileIndex++)
            {
                if (_tileOccupantSeats[tileIndex].HasValue
                    && _tileOccupantSeats[tileIndex].Value == seat
                    && IsInfrastructureCard(_boardTileData[tileIndex])
                    && _boardTileData[tileIndex].cardId == cardId
                    && _occupantCurrentHealth[tileIndex] > 0
                    && !IsSilencedAtTile(tileIndex))
                {
                    return true;
                }
            }

            return false;
        }

        private int GetRenderedCityHealth(MatchSeat seat)
        {
            if (_roundPhase == MatchRoundPhase.CombatPlanning)
            {
                return seat == MatchSeat.SeatOne
                    ? _previewSeatOneCityHealth
                    : _previewSeatTwoCityHealth;
            }

            ParticipantRuntimeState state = GetRuntimeState(seat);
            return state != null ? state.health : 0;
        }

        private void BeginRound(bool initialRound)
        {
            if (initialRound)
            {
                _roundNumber = 1;
                _roundInitiativeSeat = GetInitialRoundInitiativeSeat();
            }
            else
            {
                _roundNumber = Mathf.Max(1, _roundNumber + 1);
                _roundInitiativeSeat = MatchPerspectiveUtility.GetOpposingSeat(_roundInitiativeSeat);
            }

            _displayStruggleQueue.Clear();
            _displayMoveQueue.Clear();
            _displayAttackQueue.Clear();
            _displayStruggleQueueIndex = 0;
            _displayMoveQueueIndex = 0;
            _displayAttackQueueIndex = 0;
            _displayStrugglePrepared = false;
            _displayAttackPrepared = false;
            _displayMovePrepared = false;
            _displayResolutionMode = DisplayResolutionMode.Attack;
            _displayMovementResolved = false;
            _skipAttackDisplayThisRound = false;
            _nextDisplayActionAtUnscaledTime = -1f;
            _highlightedCardIndex = -1;
            _selectedAttackerTileIndex = -1;
            _selectedBoardTileIndex = -1;
            _displayStageLabel = string.Empty;
            _displayStageSeat = null;
            _displayNarrationText = string.Empty;
            _awarenessOverrideText = string.Empty;
            _awarenessOverrideExpiresAt = -1f;
            _autoAdvanceAtUnscaledTime = -1f;
            _warShopOverlayOpen = false;
            SetSelectedWarShopOption(WarShopOption.None);
            SetAbilityPreviewCard(null);
            ClearFloatingBoardTexts();

            for (int i = 0; i < _displayAutoTargetTileBySource.Length; i++)
            {
                _displayAutoTargetTileBySource[i] = -1;
                _moveTargetTileBySource[i] = -1;
                _displayMovementConsumedByTile[i] = false;
            }

            RemoveTemporaryCommandCards(_seatOneState);
            RemoveTemporaryCommandCards(_seatTwoState);
            ResetRoundAbilityState();
            TickTimedTileStatusesAtRoundStart();
            ApplyStartOfRoundAbilityEffects();
            BeginPlanningPhase(MatchRoundPhase.DeployPlanning, _roundInitiativeSeat, false);
            if (_phaseEndsAtUnscaledTime > 0f)
            {
                _phaseEndsAtUnscaledTime += RoundAnnouncementLeadSeconds;
            }

            ShowAwarenessMessage($"<b>Round {_roundNumber} - {GetSeatDisplayName(_roundInitiativeSeat)} starts</b>", RoundAnnouncementDisplaySeconds);
            EvaluateAutoAdvanceForPlanningPhase(MatchRoundPhase.DeployPlanning, _roundInitiativeSeat);
        }

        private void ResetRoundAbilityState()
        {
            Array.Clear(_interceptConsumedByTile, 0, _interceptConsumedByTile.Length);
            Array.Clear(_previewInterceptConsumedByTile, 0, _previewInterceptConsumedByTile.Length);
            Array.Clear(_movementPhaseStartingLocks, 0, _movementPhaseStartingLocks.Length);
        }

        private void ApplyStartOfRoundAbilityEffects()
        {
            ApplySeatStartOfRoundEffects(MatchSeat.SeatOne);
            ApplySeatStartOfRoundEffects(MatchSeat.SeatTwo);
        }

        private void ApplySeatStartOfRoundEffects(MatchSeat seat)
        {
            ParticipantRuntimeState state = GetRuntimeState(seat);
            if (state == null)
            {
                return;
            }

            // Keyword effects now resolve through ordinances/statuses. Plain cards have no start-of-round passives.
        }

        private void BeginPlanningPhase(MatchRoundPhase phase, MatchSeat seat, bool evaluateAutoAdvance = true)
        {
            _roundPhase = phase;
            _activeTurnSeat = seat;
            float planningDuration = GetPlanningPhaseDurationSeconds(phase);
            _phaseEndsAtUnscaledTime = Application.isPlaying && planningDuration > 0f
                ? Time.unscaledTime + planningDuration
                : -1f;
            _displayStageSeat = seat;
            _displayStageLabel = phase == MatchRoundPhase.DeployPlanning ? "DEPLOY" : "ATTACK";
            _displayNarrationText = string.Empty;
            _awarenessOverrideText = string.Empty;
            _awarenessOverrideExpiresAt = -1f;
            _autoAdvanceAtUnscaledTime = -1f;
            _selectedAttackerTileIndex = -1;
            _selectedBoardTileIndex = -1;
            _warShopOverlayOpen = false;
            SetSelectedWarShopOption(WarShopOption.None);

            if (phase == MatchRoundPhase.DeployPlanning)
            {
                BeginDeployTurnEconomyAndDraw(seat);
            }
            else if (phase == MatchRoundPhase.CombatPlanning)
            {
                RemoveTemporaryCommandCards(GetRuntimeState(seat));
                ApplyAttackStartKeywordEffects(seat);
                SetWarShopPurchaseUsed(seat, false);
            }

            if (UsesHotseatControlMode())
            {
                _localSeat = seat;
            }

            SyncVisibleStateFromPerspective();
            if (evaluateAutoAdvance)
            {
                EvaluateAutoAdvanceForPlanningPhase(phase, seat);
            }
        }

        private void BeginCombatPlanningSeat(MatchSeat seat)
        {
            BeginPlanningPhase(MatchRoundPhase.CombatPlanning, seat);
        }

        private void ApplyAttackStartKeywordEffects(MatchSeat seat)
        {
            for (int sourceTileIndex = 0; sourceTileIndex < _boardTileData.Length; sourceTileIndex++)
            {
                CardTemplate sourceCard = _boardTileData[sourceTileIndex];
                if (!_tileOccupantSeats[sourceTileIndex].HasValue
                    || _tileOccupantSeats[sourceTileIndex].Value != seat
                    || !IsInfrastructureCard(sourceCard)
                    || sourceCard.cardId != "card.iron_citadel.ballista"
                    || _occupantCurrentHealth[sourceTileIndex] <= 0
                    || IsSilencedAtTile(sourceTileIndex))
                {
                    continue;
                }

                int strikeDamage = Mathf.Max(1, GetKeywordValue(sourceCard, AbilityKeyword.Strike));
                if (!TryGetRowColumnFromTileIndex(sourceTileIndex, out _, out int sourceColumn))
                {
                    continue;
                }

                for (int targetTileIndex = 0; targetTileIndex < _boardTileData.Length; targetTileIndex++)
                {
                    if (!TryGetRowColumnFromTileIndex(targetTileIndex, out _, out int targetColumn)
                        || targetColumn != sourceColumn
                        || !_tileOccupantSeats[targetTileIndex].HasValue
                        || _tileOccupantSeats[targetTileIndex].Value == seat
                        || !IsUnitCard(_boardTileData[targetTileIndex])
                        || _occupantCurrentHealth[targetTileIndex] <= 0)
                    {
                        continue;
                    }

                    TryApplyDamageToOccupantActual(targetTileIndex, strikeDamage, seat, sourceTileIndex, out bool prevented, out _);
                    AddFloatingBoardText(targetTileIndex, prevented ? "BLOCK" : "PING", "tile-floating-status");
                }
            }
        }

        private void BeginDisplayResolution(DisplayResolutionMode resolutionMode)
        {
            _roundPhase = MatchRoundPhase.DisplayResolution;
            _displayResolutionMode = resolutionMode;
            _phaseEndsAtUnscaledTime = -1f;
            _displayMovementResolved = false;
            _displayStruggleQueue.Clear();
            _displayMoveQueue.Clear();
            _displayAttackQueue.Clear();
            _displayStruggleQueueIndex = 0;
            _displayMoveQueueIndex = 0;
            _displayAttackQueueIndex = 0;
            _displayStrugglePrepared = false;
            _displayAttackPrepared = false;
            _displayMovePrepared = false;
            _skipAttackDisplayThisRound = false;
            _highlightedCardIndex = -1;
            _selectedAttackerTileIndex = -1;
            _selectedBoardTileIndex = -1;
            _displayStageLabel = string.Empty;
            _displayStageSeat = null;
            _displayNarrationText = resolutionMode == DisplayResolutionMode.Movement
                ? "Movement is resolving."
                : "Attacks are resolving.";
            _awarenessOverrideText = string.Empty;
            _awarenessOverrideExpiresAt = -1f;
            _autoAdvanceAtUnscaledTime = -1f;
            SetAbilityPreviewCard(null);
            ClearFloatingBoardTexts();

            for (int i = 0; i < _displayAutoTargetTileBySource.Length; i++)
            {
                _displayAutoTargetTileBySource[i] = -1;
                _displayMovementConsumedByTile[i] = false;
            }

            if (UsesHotseatControlMode())
            {
                _localSeat = _roundInitiativeSeat;
            }

            if (resolutionMode == DisplayResolutionMode.Movement)
            {
                CaptureMovementPhaseStartingLocks();
            }

            SyncVisibleStateFromPerspective();
            if (resolutionMode == DisplayResolutionMode.Attack)
            {
                MatchSeat secondSeat = MatchPerspectiveUtility.GetOpposingSeat(_roundInitiativeSeat);
                _skipAttackDisplayThisRound = !HasAnyAttackActionForSeat(_roundInitiativeSeat) && !HasAnyAttackActionForSeat(secondSeat);
                if (_skipAttackDisplayThisRound)
                {
                    _displayNarrationText = "No valid attacks this round.";
                }
            }

            _nextDisplayActionAtUnscaledTime = Application.isPlaying ? Time.unscaledTime + DisplayAttackStepSeconds : -1f;
        }

        private void CaptureMovementPhaseStartingLocks()
        {
            if (_movementPhaseStartingLocks == null || _movementPhaseStartingLocks.Length != _tileLocked.Length)
            {
                _movementPhaseStartingLocks = new bool[_tileLocked.Length];
            }

            Array.Copy(_tileLocked, _movementPhaseStartingLocks, _tileLocked.Length);
        }

        private void ConsumeMovementPhaseStartingLocks()
        {
            if (_movementPhaseStartingLocks == null || _movementPhaseStartingLocks.Length != _tileLocked.Length)
            {
                return;
            }

            for (int i = 0; i < _movementPhaseStartingLocks.Length; i++)
            {
                if (_movementPhaseStartingLocks[i])
                {
                    _tileLocked[i] = false;
                }
            }
        }

        private void AppendDisplayAttackStepsForSeat(MatchSeat seat)
        {
            List<int> orderedTiles = GetOrderedFriendlyUnitTilesForSeat(seat);
            for (int i = 0; i < orderedTiles.Count; i++)
            {
                _displayAttackQueue.Add(new DisplayAttackStepRuntime
                {
                    seat = seat,
                    sourceTileIndex = orderedTiles[i]
                });
            }
        }

        private void BuildDisplayStruggleQueue()
        {
            _displayStruggleQueue.Clear();
            _displayStruggleQueueIndex = 0;
            RefreshMovementPreviewState();

            Dictionary<int, List<int>> contendersByTarget = new Dictionary<int, List<int>>();
            for (int sourceTileIndex = 0; sourceTileIndex < _previewMoveTargetTileBySource.Length; sourceTileIndex++)
            {
                int targetTileIndex = _previewMoveTargetTileBySource[sourceTileIndex];
                if (targetTileIndex < 0 || !_previewMoveTargetContestedBySource[sourceTileIndex])
                {
                    continue;
                }

                if (!contendersByTarget.TryGetValue(targetTileIndex, out List<int> contenders))
                {
                    contenders = new List<int>();
                    contendersByTarget[targetTileIndex] = contenders;
                }

                contenders.Add(sourceTileIndex);
            }

            foreach (KeyValuePair<int, List<int>> pair in contendersByTarget)
            {
                List<int> contenders = pair.Value;
                if (contenders.Count < 2)
                {
                    continue;
                }

                contenders.Sort(CompareStruggleCandidates);
                int winnerSourceTileIndex = contenders[0];
                int loserSourceTileIndex = contenders[1];
                MatchSeat? winnerSeat = _tileOccupantSeats[winnerSourceTileIndex];
                if (!winnerSeat.HasValue)
                {
                    continue;
                }

                MatchSeat? loserSeat = _tileOccupantSeats[loserSourceTileIndex];
                if (!loserSeat.HasValue || loserSeat.Value == winnerSeat.Value)
                {
                    continue;
                }

                _displayStruggleQueue.Add(new DisplayStruggleStepRuntime
                {
                    winnerSourceTileIndex = winnerSourceTileIndex,
                    loserSourceTileIndex = loserSourceTileIndex,
                    contestedTileIndex = pair.Key,
                    winnerSeat = winnerSeat.Value
                });
            }
        }

        private int CompareStruggleCandidates(int leftSourceTileIndex, int rightSourceTileIndex)
        {
            bool leftManual = HasManualMoveAssignment(leftSourceTileIndex);
            bool rightManual = HasManualMoveAssignment(rightSourceTileIndex);
            if (leftManual != rightManual)
            {
                return leftManual ? -1 : 1;
            }

            CardTemplate leftCard = _boardTileData[leftSourceTileIndex];
            CardTemplate rightCard = _boardTileData[rightSourceTileIndex];
            int leftAttack = leftCard != null ? leftCard.attack : 0;
            int rightAttack = rightCard != null ? rightCard.attack : 0;
            int attackCompare = rightAttack.CompareTo(leftAttack);
            if (attackCompare != 0)
            {
                return attackCompare;
            }

            MatchSeat? leftSeat = _tileOccupantSeats[leftSourceTileIndex];
            MatchSeat? rightSeat = _tileOccupantSeats[rightSourceTileIndex];
            if (leftSeat.HasValue && rightSeat.HasValue && leftSeat.Value != rightSeat.Value)
            {
                if (leftSeat.Value == _roundInitiativeSeat)
                {
                    return -1;
                }

                if (rightSeat.Value == _roundInitiativeSeat)
                {
                    return 1;
                }
            }

            int leftProgress = leftSeat.HasValue ? GetForwardProgressForSeat(leftSeat.Value, leftSourceTileIndex) : int.MinValue;
            int rightProgress = rightSeat.HasValue ? GetForwardProgressForSeat(rightSeat.Value, rightSourceTileIndex) : int.MinValue;
            int progressCompare = rightProgress.CompareTo(leftProgress);
            if (progressCompare != 0)
            {
                return progressCompare;
            }

            return leftSourceTileIndex.CompareTo(rightSourceTileIndex);
        }

        private bool HasManualMoveAssignment(int sourceTileIndex)
        {
            if (sourceTileIndex < 0 || sourceTileIndex >= _moveTargetTileBySource.Length || !_tileOccupantSeats[sourceTileIndex].HasValue)
            {
                return false;
            }

            int targetTileIndex = _moveTargetTileBySource[sourceTileIndex];
            return targetTileIndex >= 0 && CanSourceUnitMoveToTile(sourceTileIndex, targetTileIndex, _tileOccupantSeats[sourceTileIndex].Value);
        }

        private void PopulateDisplayAutoTargetPreview()
        {
            for (int i = 0; i < _displayAttackQueue.Count; i++)
            {
                DisplayAttackStepRuntime step = _displayAttackQueue[i];
                if (step.sourceTileIndex < 0 || step.sourceTileIndex >= _boardTileData.Length)
                {
                    continue;
                }

                CardTemplate cardData = _boardTileData[step.sourceTileIndex];
                if (!IsUnitCard(cardData) || _occupantCurrentHealth[step.sourceTileIndex] <= 0)
                {
                    continue;
                }

                if (_attackTargetTileBySource[step.sourceTileIndex] >= 0)
                {
                    _displayAutoTargetTileBySource[step.sourceTileIndex] = _attackTargetTileBySource[step.sourceTileIndex];
                    continue;
                }

                if (TryGetSpecialAttackTarget(step.sourceTileIndex, step.seat, out int specialTileTargetIndex, out _))
                {
                    _displayAutoTargetTileBySource[step.sourceTileIndex] = specialTileTargetIndex != step.sourceTileIndex
                        ? specialTileTargetIndex
                        : -1;
                    continue;
                }

                _displayAutoTargetTileBySource[step.sourceTileIndex] = GetAutoAttackTargetTile(step.sourceTileIndex, step.seat, GetCardAttackRangeAtTile(step.sourceTileIndex));
            }
        }

        private void TickRoundPhaseTimersAndDisplay()
        {
            if (_roundPhase == MatchRoundPhase.DisplayResolution)
            {
                TickDisplayResolution();
                return;
            }

            if (_phaseEndsAtUnscaledTime > 0f && Time.unscaledTime >= _phaseEndsAtUnscaledTime)
            {
                AdvancePhaseFromReadyOrTimeout();
            }
        }

        private void TickDisplayResolution()
        {
            if (_nextDisplayActionAtUnscaledTime < 0f || Time.unscaledTime < _nextDisplayActionAtUnscaledTime)
            {
                return;
            }

            if (_displayResolutionMode == DisplayResolutionMode.Movement)
            {
                if (!_displayStrugglePrepared)
                {
                    BuildDisplayStruggleQueue();
                    _displayStrugglePrepared = true;
                }

                if (_displayStruggleQueueIndex < _displayStruggleQueue.Count)
                {
                    ResolveDisplayStruggleStep(_displayStruggleQueue[_displayStruggleQueueIndex]);
                    _displayStruggleQueueIndex++;
                    _nextDisplayActionAtUnscaledTime = Time.unscaledTime + DisplayAttackStepSeconds;
                    UpdateUI();
                    return;
                }

                if (!_displayMovePrepared)
                {
                    BuildDisplayMoveQueue();
                    _displayMovePrepared = true;
                }

                if (_displayMoveQueueIndex < _displayMoveQueue.Count)
                {
                    ResolveDisplayMoveStep(_displayMoveQueue[_displayMoveQueueIndex]);
                    _displayMoveQueueIndex++;
                    _nextDisplayActionAtUnscaledTime = Time.unscaledTime + DisplayMovementDelaySeconds;
                    UpdateUI();
                    return;
                }

                if (!_displayMovementResolved)
                {
                    MatchSeat secondSeat = MatchPerspectiveUtility.GetOpposingSeat(_roundInitiativeSeat);
                    ConsumeMovementPhaseStartingLocks();
                    ClearMovementAssignmentsForSeat(_roundInitiativeSeat);
                    ClearMovementAssignmentsForSeat(secondSeat);
                    _displayMovementResolved = true;
                    _selectedAttackerTileIndex = -1;
                    _selectedBoardTileIndex = -1;
                    _displayNarrationText = "Movement resolved.";
                    _nextDisplayActionAtUnscaledTime = Time.unscaledTime + DisplayMovementDelaySeconds;
                    UpdateUI();
                    return;
                }

                if (_externalCommandSink != null)
                {
                    _nextDisplayActionAtUnscaledTime = -1f;
                    return;
                }

                BeginCombatPlanningSeat(_roundInitiativeSeat);
                UpdateUI();
                return;
            }

            if (_skipAttackDisplayThisRound)
            {
                if (_externalCommandSink != null)
                {
                    _skipAttackDisplayThisRound = false;
                    _displayMovementResolved = true;
                    _displayNarrationText = "Round resolved.";
                    _nextDisplayActionAtUnscaledTime = -1f;
                    UpdateUI();
                    return;
                }

                MatchSeat secondSeat = MatchPerspectiveUtility.GetOpposingSeat(_roundInitiativeSeat);
                CleanupResolvedTurnStateForSeat(_roundInitiativeSeat);
                CleanupResolvedTurnStateForSeat(secondSeat);
                _skipAttackDisplayThisRound = false;
                BeginRound(false);
                UpdateUI();
                return;
            }

            if (!_displayAttackPrepared)
            {
                MatchSeat secondSeat = MatchPerspectiveUtility.GetOpposingSeat(_roundInitiativeSeat);
                _displayAttackQueue.Clear();
                _displayAttackQueueIndex = 0;
                AppendDisplayAttackStepsForSeat(_roundInitiativeSeat);
                AppendDisplayAttackStepsForSeat(secondSeat);
                PopulateDisplayAutoTargetPreview();
                _displayAttackPrepared = true;
            }

            if (_displayAttackQueueIndex < _displayAttackQueue.Count)
            {
                ResolveDisplayAttackStep(_displayAttackQueue[_displayAttackQueueIndex]);
                _displayAttackQueueIndex++;
                _nextDisplayActionAtUnscaledTime = Time.unscaledTime + DisplayAttackStepSeconds;
                UpdateUI();
                return;
            }

            if (!_displayMovementResolved)
            {
                MatchSeat secondSeat = MatchPerspectiveUtility.GetOpposingSeat(_roundInitiativeSeat);
                CleanupResolvedTurnStateForSeat(_roundInitiativeSeat);
                CleanupResolvedTurnStateForSeat(secondSeat);
                _displayMovementResolved = true;
                _selectedAttackerTileIndex = -1;
                _selectedBoardTileIndex = -1;
                _displayNarrationText = "Round resolved.";
                _nextDisplayActionAtUnscaledTime = Time.unscaledTime + DisplayMovementDelaySeconds;
                UpdateUI();
                return;
            }

            if (_externalCommandSink != null)
            {
                _nextDisplayActionAtUnscaledTime = -1f;
                return;
            }

            BeginRound(false);
            UpdateUI();
        }

        private void ResolveDisplayAttackStep(DisplayAttackStepRuntime step)
        {
            if (step == null || step.sourceTileIndex < 0 || step.sourceTileIndex >= _boardTileData.Length)
            {
                return;
            }

            CardTemplate attackerCard = _boardTileData[step.sourceTileIndex];
            if (!IsUnitCard(attackerCard) || _occupantCurrentHealth[step.sourceTileIndex] <= 0)
            {
                return;
            }

            _displayStageSeat = step.seat;
            _displayStageLabel = "ATTACK";
            _selectedAttackerTileIndex = step.sourceTileIndex;
            string attackerName = attackerCard.cardName;
            if (!TryResolveDisplayAttackTarget(step.sourceTileIndex, step.seat, attackerCard, out int targetTileIndex, out MatchSeat? cityTargetSeat))
            {
                _selectedBoardTileIndex = -1;
                AddFloatingBoardText(step.sourceTileIndex, "MISS", "tile-floating-status");
                _displayNarrationText = $"{attackerName} missed.";
                TryConsumeEphemeralAttackerAfterAttack(step.sourceTileIndex);
                return;
            }

            int attackDamage = GetCurrentAttackValueForTile(step.sourceTileIndex, true);
            Rect sourceRect = GetBoardSurfaceTileRect(step.sourceTileIndex);
            Vector2 lungeDelta;
            if (cityTargetSeat.HasValue)
            {
                float cityAttackTileHeight = (TileBaseHeight + (TileBaseMargin * 2f)) * _tileScale;
                float forwardY = GetForwardRowStepForSeat(step.seat) * cityAttackTileHeight * 0.74f;
                lungeDelta = new Vector2(0f, forwardY);
                BeginResolveAttackMotion(step.sourceTileIndex, lungeDelta, "CITY!");
                _selectedBoardTileIndex = -1;
                ApplyCityDamage(cityTargetSeat.Value, attackDamage);
                _displayNarrationText = $"{attackerName} dealt {attackDamage}AT to {GetSeatDisplayName(cityTargetSeat.Value)}.";
                TryResolveGuerrillaRetreat(step.sourceTileIndex, step.seat);
                TryConsumeEphemeralAttackerAfterAttack(step.sourceTileIndex);
                return;
            }

            Rect targetRect = GetBoardSurfaceTileRect(targetTileIndex);
            Vector2 deltaToTarget = targetRect.center - sourceRect.center;
            Vector2 direction = deltaToTarget.sqrMagnitude > 0.01f ? deltaToTarget.normalized : Vector2.up;
            float tileFootprintWidth = (TileBaseWidth + (TileBaseMargin * 2f)) * _tileScale;
            float tileFootprintHeight = (TileBaseHeight + (TileBaseMargin * 2f)) * _tileScale;
            Vector2 sourceEndpoint = GetIntentEndpoint(sourceRect.center, direction, tileFootprintWidth, tileFootprintHeight, step.seat, true, false);
            Vector2 targetEndpoint = GetIntentEndpoint(targetRect.center, direction, tileFootprintWidth, tileFootprintHeight, step.seat, false, false);
            lungeDelta = (targetEndpoint - sourceEndpoint) * 0.92f;
            _selectedBoardTileIndex = targetTileIndex;
            if (IsSiegeableEnemyBaseTileForSeat(targetTileIndex, step.seat))
            {
                BeginResolveAttackMotion(step.sourceTileIndex, lungeDelta, "SIEGE");
                int siegeDamage = GetAttackDamageAgainstTarget(step.sourceTileIndex, step.seat, targetTileIndex, attackDamage);
                ApplyBaseTileDamage(targetTileIndex, siegeDamage, step.seat, step.sourceTileIndex);
                _displayNarrationText = $"{attackerName} is sieging {GetBaseTileDisplayName(targetTileIndex)} for {siegeDamage}AT.";
                TryResolveGuerrillaRetreat(step.sourceTileIndex, step.seat);
                TryConsumeEphemeralAttackerAfterAttack(step.sourceTileIndex);
                return;
            }

            BeginResolveAttackMotion(step.sourceTileIndex, lungeDelta, "ATTACK!");
            string targetName = GetCombatTargetDisplayName(targetTileIndex, step.seat);
            bool targetHadOccupant = _boardTileData[targetTileIndex] != null;
            VisualElement deathProxy = targetHadOccupant ? CreateResolveMotionProxy(targetTileIndex) : null;
            int targetDamage = GetAttackDamageAgainstTarget(step.sourceTileIndex, step.seat, targetTileIndex, attackDamage);
            bool prevented;
            TryApplyDamageToOccupantActual(targetTileIndex, targetDamage, step.seat, step.sourceTileIndex, out prevented, out bool unitKilled);
            if (!targetHadOccupant && _boardTileData[targetTileIndex] == null && _tileCurrentHealth[targetTileIndex] > 0)
            {
                ApplyTileDamage(targetTileIndex, targetDamage, step.seat, step.sourceTileIndex);
            }
            else if (deathProxy != null && unitKilled && !prevented && _boardTileData[targetTileIndex] == null)
            {
                BeginResolveDeathMotion(deathProxy, targetTileIndex);
            }

            _displayNarrationText = prevented
                ? $"{attackerName}'s attack was intercepted by {targetName}."
                : $"{attackerName} dealt {targetDamage}AT to {targetName}.";
            TryResolveGuerrillaRetreat(step.sourceTileIndex, step.seat);
            TryConsumeEphemeralAttackerAfterAttack(step.sourceTileIndex);
            SanitizeBoardOccupancyState("display attack resolve");
        }

        private void TryResolveGuerrillaRetreat(int attackerTileIndex, MatchSeat seat)
        {
            // Retained as a hook for future keyword-driven after-combat movement.
            // The old card-specific Guerrilla Strike behavior has been removed.
        }

        private void ResolveDisplayStruggleStep(DisplayStruggleStepRuntime step)
        {
            if (step == null
                || step.winnerSourceTileIndex < 0
                || step.loserSourceTileIndex < 0
                || step.contestedTileIndex < 0
                || step.winnerSourceTileIndex >= _boardTileData.Length
                || step.loserSourceTileIndex >= _boardTileData.Length
                || step.contestedTileIndex >= _boardTileData.Length)
            {
                return;
            }

            CardTemplate winnerCard = _boardTileData[step.winnerSourceTileIndex];
            if (!IsUnitCard(winnerCard) || _occupantCurrentHealth[step.winnerSourceTileIndex] <= 0)
            {
                return;
            }

            _displayStageSeat = step.winnerSeat;
            _displayStageLabel = "STRUGGLE";
            _selectedAttackerTileIndex = step.winnerSourceTileIndex;
            _selectedBoardTileIndex = step.contestedTileIndex;
            string winnerName = winnerCard.cardName;
            string loserName = _boardTileData[step.loserSourceTileIndex] != null ? _boardTileData[step.loserSourceTileIndex].cardName : "the opposing unit";
            _displayNarrationText = $"{winnerName} won the struggle against {loserName}.";

            if (_boardTileData[step.loserSourceTileIndex] != null && _occupantCurrentHealth[step.loserSourceTileIndex] > 0)
            {
                VisualElement loserDeathProxy = CreateResolveMotionProxy(step.loserSourceTileIndex);
                bool prevented;
                TryApplyDamageToOccupantActual(step.loserSourceTileIndex, Mathf.Max(0, winnerCard.attack), step.winnerSeat, step.winnerSourceTileIndex, out prevented, out bool unitKilled);
                if (prevented)
                {
                    _displayNarrationText = $"{winnerName} reached {loserName}, but the first hit was intercepted.";
                }
                else if (loserDeathProxy != null && unitKilled && _boardTileData[step.loserSourceTileIndex] == null)
                {
                    BeginResolveDeathMotion(loserDeathProxy, step.loserSourceTileIndex);
                }
            }

            if (_boardTileData[step.winnerSourceTileIndex] != null
                && _occupantCurrentHealth[step.winnerSourceTileIndex] > 0
                && _boardTileData[step.contestedTileIndex] == null)
            {
                BeginResolveMoveMotion(step.winnerSourceTileIndex, step.contestedTileIndex, "STRUGGLE");
                MoveOccupant(step.winnerSourceTileIndex, step.contestedTileIndex);
                _displayMovementConsumedByTile[step.contestedTileIndex] = true;
                _selectedAttackerTileIndex = step.contestedTileIndex;
            }

            SanitizeBoardOccupancyState("display struggle resolve");
        }

        private void AdvancePhaseFromReadyOrTimeout()
        {
            if (_phaseAdvanceDelayInProgress && !_phaseAdvanceContinuationRunning)
            {
                return;
            }

            if (_roundPhase == MatchRoundPhase.DeployPlanning
                && !_phaseAdvanceDelayInProgress
                && TryAnimateVisibleHandExitBeforeDeployEnd())
            {
                _phaseAdvanceDelayInProgress = true;
                _root.schedule.Execute(() =>
                {
                    _phaseAdvanceContinuationRunning = true;
                    AdvancePhaseFromReadyOrTimeout();
                    _phaseAdvanceContinuationRunning = false;
                    _phaseAdvanceDelayInProgress = false;
                }).StartingIn(560);
                return;
            }

            _placementFocusActive = false;
            hideHUD = false;
            _highlightedCardIndex = -1;
            _selectedAttackerTileIndex = -1;
            _selectedBoardTileIndex = -1;
            SetAbilityPreviewCard(null);

            MatchSeat secondSeat = MatchPerspectiveUtility.GetOpposingSeat(_roundInitiativeSeat);
            if (_roundPhase == MatchRoundPhase.DeployPlanning)
            {
                DiscardRemainingHandForDeployEnd(_activeTurnSeat);

                if (_activeTurnSeat == _roundInitiativeSeat)
                {
                    BeginPlanningPhase(MatchRoundPhase.DeployPlanning, secondSeat);
                    UpdateUI();
                    return;
                }

                BeginDisplayResolution(DisplayResolutionMode.Movement);
                UpdateUI();
                return;
            }

            if (_roundPhase == MatchRoundPhase.CombatPlanning)
            {
                if (_activeTurnSeat == _roundInitiativeSeat)
                {
                    BeginCombatPlanningSeat(secondSeat);
                    UpdateUI();
                    return;
                }

                BeginDisplayResolution(DisplayResolutionMode.Attack);
                UpdateUI();
            }
        }

        private string GetPhaseButtonText()
        {
            if (_roundPhase == MatchRoundPhase.DeployPlanning)
            {
                return "READY!";
            }

            if (_roundPhase == MatchRoundPhase.CombatPlanning)
            {
                return "DONE!";
            }

            return "WAIT";
        }

        private string GetPhaseTimerLabelText()
        {
            string phaseLabel = _roundPhase == MatchRoundPhase.DeployPlanning
                ? "DEPLOY"
                : _roundPhase == MatchRoundPhase.CombatPlanning
                    ? "COMBAT"
                    : "DISPLAY";
            int timeLeft = Mathf.Max(0, Mathf.CeilToInt(GetPhaseTimeRemainingSeconds()));
            return _roundPhase == MatchRoundPhase.DisplayResolution ? phaseLabel : $"{phaseLabel} {timeLeft}s";
        }

        private float GetPhaseTimeRemainingSeconds()
        {
            if (_roundPhase == MatchRoundPhase.DisplayResolution || _phaseEndsAtUnscaledTime < 0f || !Application.isPlaying)
            {
                return GetPlanningPhaseDurationSeconds(_roundPhase);
            }

            return Mathf.Max(0f, _phaseEndsAtUnscaledTime - Time.unscaledTime);
        }

        private float GetPlanningPhaseDurationSeconds(MatchRoundPhase phase)
        {
            if (_selectedLaunchMode == MatchLaunchMode.Testing)
            {
                return 0f;
            }

            switch (phase)
            {
                case MatchRoundPhase.CombatPlanning:
                    return AttackPhaseDurationSeconds;
                case MatchRoundPhase.DeployPlanning:
                    return DeployPhaseDurationSeconds;
                default:
                    return 1f;
            }
        }

        private void UpdatePhaseTimerUI()
        {
            var timerGroup = _root.Q<VisualElement>("phase-timer-group");
            if (timerGroup != null)
            {
                timerGroup.style.display = DisplayStyle.Flex;
            }

            if (_boardSurfaceElement != null)
            {
                Vector2 boardSize = GetCurrentBoardSurfaceSize();
                float boardWidth = boardSize.x;
                float boardHeight = boardSize.y;
                if (boardWidth > 1f && boardHeight > 1f)
                {
                    bool showActiveGridFrame = _roundPhase != MatchRoundPhase.DisplayResolution;
                    UpdateBoardPlanningCountdownFrame(showActiveGridFrame, boardWidth, boardHeight);
                }
            }
        }

        private Vector2 GetCurrentBoardSurfaceSize()
        {
            float tileFootprintWidth = (TileBaseWidth + (TileBaseMargin * 2f)) * _tileScale;
            float tileFootprintHeight = (TileBaseHeight + (TileBaseMargin * 2f)) * _tileScale;
            return new Vector2(_boardColumns * tileFootprintWidth, _boardRows * tileFootprintHeight);
        }

        private void UpdateBoardPlanningCountdownFrame(bool visible, float boardWidth, float boardHeight)
        {
            if (_boardOwnershipTimerLayerElement == null
                || _boardOwnershipTimerTopElement == null
                || _boardOwnershipTimerRightElement == null
                || _boardOwnershipTimerBottomElement == null
                || _boardOwnershipTimerLeftElement == null)
            {
                return;
            }

            _boardOwnershipTimerLayerElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
            {
                return;
            }

            _boardOwnershipTimerLayerElement.EnableInClassList("board-ownership-timer-seat-one", _activeTurnSeat == MatchSeat.SeatOne);
            _boardOwnershipTimerLayerElement.EnableInClassList("board-ownership-timer-seat-two", _activeTurnSeat == MatchSeat.SeatTwo);

            const float frameInset = 14f;
            const float thickness = 10f;
            float outerWidth = boardWidth + (frameInset * 2f);
            float outerHeight = boardHeight + (frameInset * 2f);
            float totalPerimeter = (outerWidth * 2f) + (outerHeight * 2f);
            float targetRatio = 1f;
            if (_selectedLaunchMode != MatchLaunchMode.Testing && _roundPhase != MatchRoundPhase.DisplayResolution)
            {
                float planningDuration = GetPlanningPhaseDurationSeconds(_roundPhase);
                targetRatio = planningDuration <= 0.01f
                    ? 1f
                    : Mathf.Clamp01(GetPhaseTimeRemainingSeconds() / planningDuration);
            }

            float remaining = totalPerimeter * targetRatio;
            float topLength = Mathf.Clamp(remaining, 0f, outerWidth);
            remaining = Mathf.Max(0f, remaining - topLength);
            float rightLength = Mathf.Clamp(remaining, 0f, outerHeight);
            remaining = Mathf.Max(0f, remaining - rightLength);
            float bottomLength = Mathf.Clamp(remaining, 0f, outerWidth);
            remaining = Mathf.Max(0f, remaining - bottomLength);
            float leftLength = Mathf.Clamp(remaining, 0f, outerHeight);

            SetBoardTimerEdge(_boardOwnershipTimerTopElement, topLength > 0.5f, 0f, 0f, topLength, thickness);
            SetBoardTimerEdge(_boardOwnershipTimerRightElement, rightLength > 0.5f, outerWidth - thickness, 0f, thickness, rightLength);
            SetBoardTimerEdge(_boardOwnershipTimerBottomElement, bottomLength > 0.5f, outerWidth - bottomLength, outerHeight - thickness, bottomLength, thickness);
            SetBoardTimerEdge(_boardOwnershipTimerLeftElement, leftLength > 0.5f, 0f, outerHeight - leftLength, thickness, leftLength);
        }

        private static void SetBoardTimerEdge(VisualElement edge, bool visible, float left, float top, float width, float height)
        {
            if (edge == null)
            {
                return;
            }

            edge.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
            {
                return;
            }

            edge.style.left = left;
            edge.style.top = top;
            edge.style.width = width;
            edge.style.height = height;
        }

        private void UpdatePrimaryActionButton()
        {
            var endTurnBtn = _root.Q<Button>("end-turn-button");
            var buttonLabel = _root.Q<Label>("end-turn-button-text");
            if (endTurnBtn == null || buttonLabel == null)
            {
                return;
            }

            bool interactable = !_awaitingLaunchModeSelection
                && !_matchEnded
                && _roundPhase != MatchRoundPhase.DisplayResolution
                && !_cardDeployInFlight
                && _activeTurnSeat == _localSeat;
            endTurnBtn.SetEnabled(interactable);
            buttonLabel.text = GetPhaseButtonText();
            endTurnBtn.EnableInClassList("primary-action-ready", _roundPhase == MatchRoundPhase.DeployPlanning);
            endTurnBtn.EnableInClassList("primary-action-attack", _roundPhase == MatchRoundPhase.CombatPlanning);
        }

        private string GetCityStateTextForSeat(MatchSeat seat)
        {
            if (_roundPhase == MatchRoundPhase.DeployPlanning)
            {
                return seat == _activeTurnSeat ? "DEPLOY" : string.Empty;
            }

            if (_roundPhase == MatchRoundPhase.CombatPlanning)
            {
                return seat == _activeTurnSeat ? "ATTACK" : string.Empty;
            }

            if (_roundPhase == MatchRoundPhase.DisplayResolution
                && !string.IsNullOrWhiteSpace(_displayStageLabel))
            {
                return _displayStageLabel;
            }

            if (_displayStageSeat.HasValue && seat == _displayStageSeat.Value)
            {
                return _displayStageLabel ?? string.Empty;
            }

            return string.Empty;
        }

        private void UpdateCityPhaseIndicators()
        {
            UpdateCityPhaseIndicator(
                "player-city-state-shell",
                "player-city-state",
                GetCityStateTextForSeat(_perspectiveSeat));

            UpdateCityPhaseIndicator(
                "enemy-city-state-shell",
                "enemy-city-state",
                GetCityStateTextForSeat(MatchPerspectiveUtility.GetOpposingSeat(_perspectiveSeat)));

            UpdateCityPhaseHighlight(
                "player-city-header-group",
                _perspectiveSeat == _activeTurnSeat && _roundPhase != MatchRoundPhase.DisplayResolution ? _activeTurnSeat : (MatchSeat?)null);

            UpdateCityPhaseHighlight(
                "enemy-city-header-group",
                MatchPerspectiveUtility.GetOpposingSeat(_perspectiveSeat) == _activeTurnSeat && _roundPhase != MatchRoundPhase.DisplayResolution ? _activeTurnSeat : (MatchSeat?)null);
        }

        private void UpdateDominantRoundIndicators()
        {
            UpdateRoundIndicatorForSeat("player-round-badge-shell", "player-round-badge", _perspectiveSeat);
            UpdateRoundIndicatorForSeat("enemy-round-badge-shell", "enemy-round-badge", MatchPerspectiveUtility.GetOpposingSeat(_perspectiveSeat));
        }

        private void UpdateRoundIndicatorForSeat(string shellName, string imageName, MatchSeat displayedSeat)
        {
            var shell = _root.Q<VisualElement>(shellName);
            var indicator = _root.Q<VisualElement>(imageName);
            if (shell == null || indicator == null)
            {
                return;
            }

            bool shouldShow = displayedSeat == _roundInitiativeSeat && _roundNumber > 0;
            shell.EnableInClassList("round-badge-hidden", !shouldShow);
            if (!shouldShow)
            {
                indicator.style.backgroundImage = StyleKeyword.Null;
                return;
            }

            Sprite roundSprite = GetRoundIndicatorSprite(_roundNumber);
            if (roundSprite != null)
            {
                indicator.style.backgroundImage = new StyleBackground(roundSprite);
            }
            else
            {
                indicator.style.backgroundImage = StyleKeyword.Null;
            }
        }

        private Sprite GetRoundIndicatorSprite(int roundNumber)
        {
            if (roundIndicatorSprites == null || roundIndicatorSprites.Count == 0)
            {
                return null;
            }

            int clampedRound = Mathf.Clamp(roundNumber, 1, 100);
            int index = clampedRound - 1;
            if (index >= 0 && index < roundIndicatorSprites.Count && roundIndicatorSprites[index] != null)
            {
                return roundIndicatorSprites[index];
            }

            return roundIndicatorSprites.FirstOrDefault(sprite => sprite != null && sprite.name == $"round_{clampedRound}");
        }

        private void EnsureRoundIndicatorSpritesLoaded()
        {
#if UNITY_EDITOR
            const string folderPath = "Assets/UI/Sprites/rounds";
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
            if (guids == null || guids.Length == 0)
            {
                return;
            }

            var loadedSprites = guids
                .Select(guid => UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid)))
                .Where(sprite => sprite != null)
                .OrderBy(sprite =>
                {
                    string suffix = sprite.name.Replace("round_", string.Empty);
                    return int.TryParse(suffix, out int parsed) ? parsed : int.MaxValue;
                })
                .ToList();

            bool changed = roundIndicatorSprites == null
                || roundIndicatorSprites.Count != loadedSprites.Count
                || !roundIndicatorSprites.SequenceEqual(loadedSprites);

            if (changed)
            {
                roundIndicatorSprites = loadedSprites;
            }
#endif
        }

        private void UpdateCityPhaseIndicator(string shellName, string labelName, string stateText)
        {
            var shell = _root.Q<VisualElement>(shellName);
            var label = _root.Q<Label>(labelName);
            if (shell == null || label == null)
            {
                return;
            }

            bool isVisible = !string.IsNullOrWhiteSpace(stateText);
            label.text = stateText;
            shell.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            shell.EnableInClassList("city-state-hidden", !isVisible);
            shell.EnableInClassList("city-state-visible", isVisible);
            shell.EnableInClassList("city-state-deploy", stateText == "DEPLOY");
            shell.EnableInClassList("city-state-attack", stateText == "ATTACK");
            shell.EnableInClassList("city-state-move", stateText == "MOVE");
            shell.EnableInClassList("city-state-struggle", stateText == "STRUGGLE");
        }

        private void UpdateCityPhaseHighlight(string headerGroupName, MatchSeat? activeSeat)
        {
            var headerGroup = _root.Q<VisualElement>(headerGroupName);
            if (headerGroup == null)
            {
                return;
            }

            headerGroup.EnableInClassList("city-header-active-seat-one", activeSeat.HasValue && activeSeat.Value == MatchSeat.SeatOne);
            headerGroup.EnableInClassList("city-header-active-seat-two", activeSeat.HasValue && activeSeat.Value == MatchSeat.SeatTwo);
        }

        private void UpdateCityDamageFlashUI()
        {
            if (_root == null)
            {
                return;
            }

            UpdateCityDamageFlashForSeat(
                _perspectiveSeat,
                "player-city-nameplate",
                "player-stability");

            UpdateCityDamageFlashForSeat(
                MatchPerspectiveUtility.GetOpposingSeat(_perspectiveSeat),
                "enemy-city-nameplate",
                "enemy-stability");
        }

        private void UpdateCityDamageFlashForSeat(MatchSeat seat, string titleLabelName, string healthLabelName)
        {
            var titleLabel = _root.Q<Label>(titleLabelName);
            var healthLabel = _root.Q<Label>(healthLabelName);
            if (titleLabel == null || healthLabel == null)
            {
                return;
            }

            float flashExpiresAt = seat == MatchSeat.SeatOne ? _seatOneCityFlashExpiresAt : _seatTwoCityFlashExpiresAt;
            bool isFlashing = Application.isPlaying && flashExpiresAt > Time.unscaledTime;
            ParticipantRuntimeState seatState = GetRuntimeState(seat);
            int currentHealth = seatState != null ? seatState.health : GetRenderedCityHealth(seat);
            bool hasPreviewDamage = _roundPhase == MatchRoundPhase.CombatPlanning && GetRenderedCityHealth(seat) < currentHealth;
            titleLabel.EnableInClassList("city-under-attack", isFlashing);
            healthLabel.EnableInClassList("city-health-under-attack", isFlashing || hasPreviewDamage);
        }

        private void AdvanceTurn()
        {
            _activeTurnSeat = MatchPerspectiveUtility.GetOpposingSeat(_activeTurnSeat);

            ParticipantRuntimeState activeState = GetRuntimeState(_activeTurnSeat);
            if (activeState != null)
            {
                BeginDeployTurnEconomyAndDraw(_activeTurnSeat);
            }

            if (UsesHotseatControlMode())
            {
                _localSeat = _activeTurnSeat;
            }

            _boardViewNeedsReset = true;
            _boardViewResetAttempts = 0;
            SyncVisibleStateFromPerspective();
        }

        private void EnsureBoardRuntimeCapacity(int tileCount)
        {
            if (_boardTileData == null || _boardTileData.Length != tileCount)
            {
                _boardTileData = new CardTemplate[tileCount];
                _tileOccupantSeats = new MatchSeat?[tileCount];
                _occupantCurrentHealth = new int[tileCount];
                _tileCurrentHealth = new int[tileCount];
                _tileMaxHealth = new int[tileCount];
                _tileAreaKinds = new TileAreaKind[tileCount];
                _tileOwners = new TileOwner[tileCount];
                _tileBlocksCity = new bool[tileCount];
                _tileLocked = new bool[tileCount];
                _attackTargetTileBySource = new int[tileCount];
                _moveTargetTileBySource = new int[tileCount];
                _previewOccupantHealth = new int[tileCount];
                _previewTileHealth = new int[tileCount];
                _displayAutoTargetTileBySource = new int[tileCount];
                _previewResolvedMoveTargetBySource = new int[tileCount];
                _previewMovementOccupantHealth = new int[tileCount];
                _displayMovementConsumedByTile = new bool[tileCount];
                _secureHoldTurnsByTile = new int[tileCount];
                _silenceTurnsByTile = new int[tileCount];
                _spawnChargeTurnsByTile = new int[tileCount];
                _interceptConsumedByTile = new int[tileCount];
                _previewInterceptConsumedByTile = new int[tileCount];
                _movementPhaseStartingLocks = new bool[tileCount];
            }
        }

        private void ResetBoardRuntimeToDefaults()
        {
            for (int row = 0; row < _boardRows; row++)
            {
                for (int column = 0; column < _boardColumns; column++)
                {
                    int tileIndex = ToTileIndex(row, column);
                    _boardTileData[tileIndex] = null;
                    _tileOccupantSeats[tileIndex] = null;
                    _occupantCurrentHealth[tileIndex] = 0;
                    _tileBlocksCity[tileIndex] = false;
                    _tileLocked[tileIndex] = false;
                    _attackTargetTileBySource[tileIndex] = -1;
                    _moveTargetTileBySource[tileIndex] = -1;
                    _displayAutoTargetTileBySource[tileIndex] = -1;
                    _previewResolvedMoveTargetBySource[tileIndex] = -1;
                    _previewMovementOccupantHealth[tileIndex] = 0;
                    _displayMovementConsumedByTile[tileIndex] = false;
                    _secureHoldTurnsByTile[tileIndex] = 0;
                    _silenceTurnsByTile[tileIndex] = 0;
                    _spawnChargeTurnsByTile[tileIndex] = 0;

                    if (row == 0)
                    {
                        _tileOwners[tileIndex] = GetTileOwnerForSeat(_canonicalTopSeat);
                        _tileAreaKinds[tileIndex] = TileAreaKind.Base;
                        _tileMaxHealth[tileIndex] = 30;
                        _tileCurrentHealth[tileIndex] = 30;
                        _tileBlocksCity[tileIndex] = true;
                    }
                    else if (row == _boardRows - 1)
                    {
                        _tileOwners[tileIndex] = GetTileOwnerForSeat(MatchPerspectiveUtility.GetOpposingSeat(_canonicalTopSeat));
                        _tileAreaKinds[tileIndex] = TileAreaKind.Base;
                        _tileMaxHealth[tileIndex] = 30;
                        _tileCurrentHealth[tileIndex] = 30;
                        _tileBlocksCity[tileIndex] = true;
                    }
                    else
                    {
                        _tileOwners[tileIndex] = TileOwner.Neutral;
                        _tileAreaKinds[tileIndex] = TileAreaKind.Freeplay;
                        _tileMaxHealth[tileIndex] = 0;
                        _tileCurrentHealth[tileIndex] = 0;
                    }
                }
            }
        }

        private bool IsInBounds(int row, int column)
        {
            return row >= 0 && row < _boardRows && column >= 0 && column < _boardColumns;
        }

        private int ToTileIndex(int row, int column)
        {
            return row * _boardColumns + column;
        }

        private int GetCanonicalRowForDisplayRow(int displayRow)
        {
            if (!ShouldFlipBoardRowsForCurrentView())
            {
                return displayRow;
            }

            return (_boardRows - 1) - displayRow;
        }

        private string GetTileVisualClass(int tileIndex)
        {
            if (_tileAreaKinds[tileIndex] == TileAreaKind.Freeplay || _tileOwners[tileIndex] == TileOwner.Neutral)
            {
                return "neutral-tile";
            }

            MatchSeat? ownerSeat = GetSeatFromTileOwner(_tileOwners[tileIndex]);
            if (ownerSeat.HasValue)
            {
                return GetBaseTileThemeClass(ownerSeat.Value);
            }

            return "neutral-tile";
        }

        private MatchSeat? GetSeatFromTileOwner(TileOwner owner)
        {
            if (owner == TileOwner.SeatOne)
            {
                return MatchSeat.SeatOne;
            }

            if (owner == TileOwner.SeatTwo)
            {
                return MatchSeat.SeatTwo;
            }

            return null;
        }

        private TileOwner GetTileOwnerForSeat(MatchSeat seat)
        {
            return seat == MatchSeat.SeatOne ? TileOwner.SeatOne : TileOwner.SeatTwo;
        }

        private MatchSeat? GetOccupantSeat(int tileIndex)
        {
            if (_tileOccupantSeats != null
                && tileIndex >= 0
                && tileIndex < _tileOccupantSeats.Length
                && _tileOccupantSeats[tileIndex].HasValue)
            {
                return _tileOccupantSeats[tileIndex];
            }

            return GetSeatFromTileOwner(_tileOwners[tileIndex]);
        }

        private void RepairBoardOccupantSeatData()
        {
            if (_boardTileData == null || _tileOccupantSeats == null)
            {
                return;
            }

            for (int tileIndex = 0; tileIndex < _boardTileData.Length && tileIndex < _tileOccupantSeats.Length; tileIndex++)
            {
                if (_boardTileData[tileIndex] == null || _tileOccupantSeats[tileIndex].HasValue)
                {
                    continue;
                }

                if (TryInferSeatFromCardPrefix(_boardTileData[tileIndex], out MatchSeat inferredSeat))
                {
                    _tileOccupantSeats[tileIndex] = inferredSeat;
                }
            }
        }

        private static bool TryInferSeatFromCardPrefix(CardTemplate cardData, out MatchSeat seat)
        {
            seat = MatchSeat.SeatOne;
            string cardId = cardData != null ? cardData.cardId : string.Empty;
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return false;
            }

            if (cardId.StartsWith("card.free_haven.", StringComparison.OrdinalIgnoreCase))
            {
                seat = MatchSeat.SeatOne;
                return true;
            }

            if (cardId.StartsWith("card.iron_citadel.", StringComparison.OrdinalIgnoreCase))
            {
                seat = MatchSeat.SeatTwo;
                return true;
            }

            return false;
        }

        private static bool IsWarShopSystemCard(CardTemplate cardData)
        {
            return cardData != null
                && !string.IsNullOrWhiteSpace(cardData.cardId)
                && cardData.cardId.StartsWith("card.system.warshop.", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetGeneratedCardArtClass(CardTemplate cardData)
        {
            if (cardData == null || string.IsNullOrWhiteSpace(cardData.cardId))
            {
                return string.Empty;
            }

            return cardData.cardId switch
            {
                "card.system.warshop.fieldmedic" => "generated-art-warshop-field-medic",
                "card.system.warshop.bombdrop" => "generated-art-warshop-bomb-drop",
                "card.system.warshop.frontierclaim" => "generated-art-warshop-frontier-claim",
                "card.system.warshop.rebuildorder" => "generated-art-warshop-rebuild-order",
                _ => string.Empty
            };
        }

        private static void ApplyGeneratedCardArtClasses(VisualElement element, CardTemplate cardData)
        {
            if (element == null)
            {
                return;
            }

            for (int i = 0; i < SpecialGeneratedArtClasses.Length; i++)
            {
                element.RemoveFromClassList(SpecialGeneratedArtClasses[i]);
            }

            string artClass = GetGeneratedCardArtClass(cardData);
            if (!string.IsNullOrWhiteSpace(artClass))
            {
                element.AddToClassList(artClass);
            }
        }

        private string GetFallbackRuleEntryTitle(CardTemplate cardData)
        {
            if (cardData == null)
            {
                return "CARD RULE";
            }

            if (IsWarShopSystemCard(cardData))
            {
                return $"WAR SHOP - {cardData.cardName.ToUpper()}";
            }

            return $"{GetCardTypeLabel(cardData).ToUpper()} - {cardData.cardName.ToUpper()}";
        }

        private string GetCardTypeLabel(CardTemplate cardData)
        {
            if (cardData == null)
            {
                return string.Empty;
            }

            if (cardData.cardType == CardType.Infrastructure)
            {
                return "BUILDING";
            }

            if (cardData.cardType == CardType.Unit && cardData.unitTag != UnitTag.None)
            {
                return GetUnitTagDisplayLabel(cardData.unitTag).ToUpperInvariant();
            }

            if (cardData.cardType == CardType.Ordinance)
            {
                return "ORDER";
            }

            return cardData.cardType.ToString().ToUpper();
        }

        private static string GetUnitTagDisplayLabel(UnitTag tag)
        {
            switch (tag)
            {
                case UnitTag.Civilian:
                    return "Civilian";
                case UnitTag.Military:
                    return "Military";
                case UnitTag.Special:
                    return "Special";
                default:
                    return "Unit";
            }
        }

        private string GetSeatDisplayName(MatchSeat seat)
        {
            ParticipantRuntimeState state = GetRuntimeState(seat);
            if (state != null && !string.IsNullOrWhiteSpace(state.cityName))
            {
                return state.cityName;
            }

            return seat == MatchSeat.SeatOne ? playerCityName : enemyCityName;
        }

        private MatchSeat GetInitialRoundInitiativeSeat()
        {
            if (!Application.isPlaying)
            {
                return prototypeMatch != null ? prototypeMatch.startingTurn : MatchSeat.SeatOne;
            }

            return UnityEngine.Random.value < 0.5f ? MatchSeat.SeatOne : MatchSeat.SeatTwo;
        }

        private int GetCardMovementRange(CardTemplate cardData)
        {
            return IsUnitCard(cardData) ? Mathf.Max(1, GetPrintedMovementRange(cardData)) : 0;
        }

        private int GetCardMovementRangeAtTile(int tileIndex)
        {
            CardTemplate cardData = tileIndex >= 0 && tileIndex < _boardTileData.Length ? _boardTileData[tileIndex] : null;
            if (!IsUnitCard(cardData))
            {
                return 0;
            }

            int movement = Mathf.Max(1, GetPrintedMovementRange(cardData)) + GetKeywordValue(cardData, AbilityKeyword.Sprint);
            if (TryGetTileSeat(tileIndex, out MatchSeat seat))
            {
                movement += GetInfrastructureMovementBonusForTile(tileIndex, seat);
            }

            return Mathf.Max(1, movement);
        }

        private int GetCardAttackRangeAtTile(int tileIndex)
        {
            CardTemplate cardData = tileIndex >= 0 && tileIndex < _boardTileData.Length ? _boardTileData[tileIndex] : null;
            return IsUnitCard(cardData) ? Mathf.Max(1, GetPrintedRange(cardData)) : 0;
        }

        private string GetBaseTileDisplayName(int tileIndex)
        {
            MatchSeat? baseSeat = GetSeatFromTileOwner(_tileOwners[tileIndex]);
            return baseSeat.HasValue ? $"{GetSeatDisplayName(baseSeat.Value)} base" : "the base";
        }

        private string GetCombatTargetDisplayName(int tileIndex, MatchSeat attackerSeat)
        {
            if (tileIndex < 0 || tileIndex >= _boardTileData.Length)
            {
                return "nothing";
            }

            if (IsSiegeableEnemyBaseTileForSeat(tileIndex, attackerSeat))
            {
                return GetBaseTileDisplayName(tileIndex);
            }

            return _boardTileData[tileIndex] != null ? _boardTileData[tileIndex].cardName : "nothing";
        }

        private string GetAbilityPreviewMarkup(CardTemplate cardData)
        {
            if (cardData == null)
            {
                return string.Empty;
            }

            return BuildAbilityPreviewMarkup(cardData.GetAbilitySummaryText());
        }

        private string BuildAbilityPreviewMarkup(string sourceText)
        {
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                return string.Empty;
            }

            string[] colorCycle =
            {
                "#E11D48",
                "#2563EB",
                "#16A34A",
                "#CA8A04",
                "#7C3AED"
            };

            string[] rawSegments = sourceText.Split('.');
            List<string> styledSegments = new List<string>();
            int colorIndex = 0;
            for (int i = 0; i < rawSegments.Length; i++)
            {
                string segment = rawSegments[i].Trim();
                if (string.IsNullOrEmpty(segment))
                {
                    continue;
                }

                string color = colorCycle[colorIndex % colorCycle.Length];
                styledSegments.Add($"<color={color}>{segment}.</color>");
                colorIndex++;
            }

            return string.Join(" ", styledSegments);
        }

        private void SetAbilityPreviewCard(CardTemplate cardData)
        {
            _abilityPreviewCard = cardData;
            // Card rules now live in the hold-to-inspect detail view. Keep the
            // awareness strip for gameplay events so it stays readable in play.
            _abilityPreviewText = string.Empty;
        }

        private void SetAbilityPreviewText(string previewText)
        {
            _abilityPreviewCard = null;
            _abilityPreviewText = previewText ?? string.Empty;
        }

        private bool IsCardVisibleToLocalUi(CardTemplate cardData)
        {
            if (cardData == null)
            {
                return false;
            }

            if (cardsInHand != null && cardsInHand.Contains(cardData))
            {
                return true;
            }

            for (int i = 0; i < _boardTileData.Length; i++)
            {
                if (_boardTileData[i] == cardData)
                {
                    return true;
                }
            }

            return false;
        }

        private string GetBaseTilePreviewText(int tileIndex)
        {
            if (_tileAreaKinds[tileIndex] != TileAreaKind.Base)
            {
                return string.Empty;
            }

            string ownerLabel = MatchPerspectiveUtility.IsLocalOwned(_tileOwners[tileIndex], _perspectiveSeat)
                ? "Your base tile"
                : "Enemy base tile";
            return $"{ownerLabel}. Enemy units cannot enter while it still stands. It must be destroyed from outside, then the broken lane opens toward the city.";
        }

        private void ShowAwarenessMessage(string text, float durationSeconds = 1.4f)
        {
            _awarenessOverrideText = text ?? string.Empty;
            _awarenessOverrideExpiresAt = string.IsNullOrWhiteSpace(_awarenessOverrideText)
                ? -1f
                : GetUnscaledNow() + Mathf.Max(0.1f, durationSeconds);
            if (_isInitializingMatchRuntime)
            {
                return;
            }

            UpdateUI();
        }

        private void QueueAutoAdvanceWithAwareness(string text, float delaySeconds = 1.25f)
        {
            ShowAwarenessMessage(text, delaySeconds);
            _autoAdvancePhase = _roundPhase;
            _autoAdvanceSeat = _activeTurnSeat;
            _autoAdvanceAtUnscaledTime = GetUnscaledNow() + Mathf.Max(0.15f, delaySeconds);
        }

        private bool IsUnitCard(CardTemplate cardData)
        {
            return cardData != null && cardData.cardType == CardType.Unit;
        }

        private bool IsInfrastructureCard(CardTemplate cardData)
        {
            return cardData != null && cardData.cardType == CardType.Infrastructure;
        }

        private bool IsBoardDeployableCard(CardTemplate cardData)
        {
            return IsUnitCard(cardData) || IsInfrastructureCard(cardData);
        }

        private bool IsLockCommandCard(CardTemplate cardData)
        {
            return cardData != null && cardData.commandCardKind == CommandCardKind.LockUnit;
        }

        private CardTemplate CreateLockCommandCard()
        {
            CardTemplate lockCard = ScriptableObject.CreateInstance<CardTemplate>();
            lockCard.cardId = $"command.lock.{System.Guid.NewGuid():N}";
            lockCard.cardName = "Lock";
            lockCard.treasuryCost = 0;
            lockCard.cardType = CardType.Ordinance;
            lockCard.commandCardKind = CommandCardKind.LockUnit;
            lockCard.health = 0;
            lockCard.attack = 0;
            lockCard.range = 0;
            lockCard.movementRange = 0;
            lockCard.keywordEffects.Add(new AbilityEffectData
            {
                keyword = AbilityKeyword.Lock,
                value = 1,
                trigger = AbilityTrigger.Instant,
                duration = AbilityDuration.ThisRound,
                targetScope = AbilityTargetScope.FriendlyUnit,
                shortDescription = "Lock 1: stop movement this round.",
                detailedDescription = "Place this on a friendly unit during Deploy. It will not move during this round's movement resolve."
            });
            return lockCard;
        }

        private void RemoveTemporaryCommandCards(ParticipantRuntimeState state)
        {
            if (state == null)
            {
                return;
            }

            state.hand.RemoveAll(IsLockCommandCard);
        }

        private void RefreshTurnCommandCardsForSeat(MatchSeat seat)
        {
            ParticipantRuntimeState state = GetRuntimeState(seat);
            if (state == null)
            {
                return;
            }

            RemoveTemporaryCommandCards(state);
            for (int i = 0; i < LocksPerTurn; i++)
            {
                state.hand.Add(CreateLockCommandCard());
            }
        }

        private bool TryGetRowColumnFromTileIndex(int tileIndex, out int row, out int column)
        {
            row = -1;
            column = -1;
            if (tileIndex < 0 || tileIndex >= _boardRows * _boardColumns)
            {
                return false;
            }

            row = tileIndex / _boardColumns;
            column = tileIndex % _boardColumns;
            return true;
        }

        private int GetForwardRowStepForSeat(MatchSeat seat)
        {
            return seat == _canonicalTopSeat ? 1 : -1;
        }

        private int GetForwardProgressForSeat(MatchSeat seat, int tileIndex)
        {
            if (!TryGetRowColumnFromTileIndex(tileIndex, out int row, out _))
            {
                return int.MinValue;
            }

            return GetForwardRowStepForSeat(seat) > 0 ? row : (_boardRows - 1 - row);
        }

        private bool CanTileReceiveLock(int tileIndex, MatchSeat seat)
        {
            CardTemplate occupant = _boardTileData[tileIndex];
            MatchSeat? occupantSeat = _tileOccupantSeats[tileIndex];
            return IsUnitCard(occupant) && occupantSeat.HasValue && occupantSeat.Value == seat && !_tileLocked[tileIndex];
        }

        private string GetInvalidLockTargetReason(int tileIndex, MatchSeat seat)
        {
            if (tileIndex < 0 || tileIndex >= _boardTileData.Length)
            {
                return "Lock needs a valid board target.";
            }

            CardTemplate occupant = _boardTileData[tileIndex];
            MatchSeat? occupantSeat = _tileOccupantSeats[tileIndex];
            if (!IsUnitCard(occupant) || !occupantSeat.HasValue)
            {
                return "Lock can only target one of your units.";
            }

            if (occupantSeat.Value != seat)
            {
                return "Lock can only target your own units.";
            }

            if (_tileLocked[tileIndex])
            {
                return $"{occupant.cardName} is already locked this turn.";
            }

            return "Invalid lock target.";
        }

        private void ClearAttackAssignmentsForSeat(MatchSeat seat)
        {
            for (int i = 0; i < _attackTargetTileBySource.Length; i++)
            {
                if (_tileOccupantSeats[i].HasValue && _tileOccupantSeats[i].Value == seat)
                {
                    _attackTargetTileBySource[i] = -1;
                }
            }
        }

        private void ClearFloatingBoardTexts()
        {
            _floatingBoardTexts.Clear();
        }

        private void AddFloatingBoardText(int tileIndex, string text, string cssClass = "tile-floating-damage")
        {
            _floatingBoardTexts.Add(new FloatingBoardTextRuntime
            {
                tileIndex = tileIndex,
                text = text,
                cssClass = cssClass,
                expiresAt = (Application.isPlaying ? Time.unscaledTime : 0f) + FloatingTextDurationSeconds
            });
        }

        private bool TryFindManualAttackTarget(int sourceTileIndex, out int targetTileIndex)
        {
            targetTileIndex = _attackTargetTileBySource[sourceTileIndex];
            if (targetTileIndex < 0 || targetTileIndex >= _boardTileData.Length)
            {
                return false;
            }

            return CanSourceUnitTargetTile(sourceTileIndex, targetTileIndex);
        }

        private bool HasManualCityAttackTarget(int sourceTileIndex)
        {
            return sourceTileIndex >= 0
                && sourceTileIndex < _attackTargetTileBySource.Length
                && _attackTargetTileBySource[sourceTileIndex] == ManualCityAttackTargetToken;
        }

        private bool IsLiveEnemyBaseTileForSeat(int tileIndex, MatchSeat seat)
        {
            if (tileIndex < 0 || tileIndex >= _boardTileData.Length)
            {
                return false;
            }

            if (_tileAreaKinds[tileIndex] != TileAreaKind.Base || _tileCurrentHealth[tileIndex] <= 0)
            {
                return false;
            }

            MatchSeat? tileSeat = GetSeatFromTileOwner(_tileOwners[tileIndex]);
            return tileSeat.HasValue && tileSeat.Value != seat;
        }

        private bool IsSiegeableEnemyBaseTileForSeat(int tileIndex, MatchSeat seat)
        {
            return IsLiveEnemyBaseTileForSeat(tileIndex, seat) && _boardTileData[tileIndex] == null;
        }

        private bool CanAttackCityDirectlyFromTile(int tileIndex, MatchSeat seat)
        {
            if (tileIndex < 0 || tileIndex >= _boardTileData.Length
                || _boardTileData[tileIndex] == null
                || !IsUnitCard(_boardTileData[tileIndex])
                || !_tileOccupantSeats[tileIndex].HasValue
                || _tileOccupantSeats[tileIndex].Value != seat)
            {
                return false;
            }

            if (!TryGetRowColumnFromTileIndex(tileIndex, out int row, out _))
            {
                return false;
            }

            int cityRow = seat == _canonicalTopSeat ? _boardRows - 1 : 0;
            return row == cityRow;
        }

        private string GetInvalidCityAttackReason(int sourceTileIndex, MatchSeat attackerSeat, MatchSeat displayedSeat)
        {
            CardTemplate sourceCard = sourceTileIndex >= 0 && sourceTileIndex < _boardTileData.Length
                ? _boardTileData[sourceTileIndex]
                : null;
            string sourceName = sourceCard != null ? sourceCard.cardName : "Unit";

            if (displayedSeat == attackerSeat)
            {
                return $"{sourceName} cannot attack your own city.";
            }

            if (!CanAttackCityDirectlyFromTile(sourceTileIndex, attackerSeat))
            {
                return $"{sourceName} can only attack the city from the closest opened base row.";
            }

            return $"{sourceName} cannot attack that city.";
        }

        private bool TryGetSpecialAttackTarget(int sourceTileIndex, MatchSeat seat, out int targetTileIndex, out MatchSeat cityTargetSeat)
        {
            targetTileIndex = -1;
            cityTargetSeat = seat;

            if (CanAttackCityDirectlyFromTile(sourceTileIndex, seat))
            {
                cityTargetSeat = MatchPerspectiveUtility.GetOpposingSeat(seat);
                return true;
            }

            return false;
        }

        private int GetAutoAttackTargetTile(int sourceTileIndex, MatchSeat seat, int range)
        {
            if (TryGetForcedProvokeTargetTile(sourceTileIndex, seat, out int forcedTargetTileIndex))
            {
                return forcedTargetTileIndex;
            }

            if (!TryGetRowColumnFromTileIndex(sourceTileIndex, out int sourceRow, out int sourceColumn))
            {
                return -1;
            }

            int rowStep = GetForwardRowStepForSeat(seat);
            for (int distance = 1; distance <= Mathf.Max(1, range); distance++)
            {
                int targetRow = sourceRow + (rowStep * distance);
                if (!IsInBounds(targetRow, sourceColumn))
                {
                    break;
                }

                int targetTileIndex = ToTileIndex(targetRow, sourceColumn);
                if (_boardTileData[targetTileIndex] != null)
                {
                    MatchSeat? targetSeat = _tileOccupantSeats[targetTileIndex];
                    if (targetSeat.HasValue && targetSeat.Value != seat)
                    {
                        return targetTileIndex;
                    }

                    break;
                }

                if (IsSiegeableEnemyBaseTileForSeat(targetTileIndex, seat))
                {
                    return targetTileIndex;
                }
            }

            return -1;
        }

        private int GetMissAttackIntentTile(int sourceTileIndex, MatchSeat seat, int range)
        {
            if (!TryGetRowColumnFromTileIndex(sourceTileIndex, out int sourceRow, out int sourceColumn))
            {
                return -1;
            }

            int rowStep = GetForwardRowStepForSeat(seat);
            int fallbackTileIndex = -1;
            for (int distance = 1; distance <= Mathf.Max(1, range); distance++)
            {
                int targetRow = sourceRow + (rowStep * distance);
                if (!IsInBounds(targetRow, sourceColumn))
                {
                    break;
                }

                fallbackTileIndex = ToTileIndex(targetRow, sourceColumn);
                if (_boardTileData[fallbackTileIndex] != null || IsLiveEnemyBaseTileForSeat(fallbackTileIndex, seat))
                {
                    break;
                }
            }

            return fallbackTileIndex;
        }

        private int ResolveIntentTargetTile(int sourceTileIndex, MatchSeat seat, CardTemplate attackerCard)
        {
            if (attackerCard == null)
            {
                return -1;
            }

            int attackRange = GetCardAttackRangeAtTile(sourceTileIndex);
            if (TryFindManualAttackTarget(sourceTileIndex, out int targetTileIndex))
            {
                return targetTileIndex;
            }

            return GetAutoAttackTargetTile(sourceTileIndex, seat, attackRange);
        }

        private bool TryResolveAttackTarget(int sourceTileIndex, MatchSeat seat, CardTemplate attackerCard, out int targetTileIndex, out MatchSeat? cityTargetSeat)
        {
            targetTileIndex = -1;
            cityTargetSeat = null;
            if (attackerCard == null)
            {
                return false;
            }

            if (TryGetForcedProvokeTargetTile(sourceTileIndex, seat, out int forcedTargetTileIndex))
            {
                targetTileIndex = forcedTargetTileIndex;
                return true;
            }

            if (HasManualCityAttackTarget(sourceTileIndex) && CanAttackCityDirectlyFromTile(sourceTileIndex, seat))
            {
                cityTargetSeat = MatchPerspectiveUtility.GetOpposingSeat(seat);
                return true;
            }

            if (TryFindManualAttackTarget(sourceTileIndex, out int manualTargetTileIndex))
            {
                targetTileIndex = manualTargetTileIndex;
                return true;
            }

            int attackRange = GetCardAttackRangeAtTile(sourceTileIndex);
            targetTileIndex = GetAutoAttackTargetTile(sourceTileIndex, seat, attackRange);
            if (targetTileIndex >= 0)
            {
                return true;
            }

            if (CanAttackCityDirectlyFromTile(sourceTileIndex, seat))
            {
                cityTargetSeat = MatchPerspectiveUtility.GetOpposingSeat(seat);
                return true;
            }

            return false;
        }

        private bool TryResolveDisplayAttackTarget(int sourceTileIndex, MatchSeat seat, CardTemplate attackerCard, out int targetTileIndex, out MatchSeat? cityTargetSeat)
        {
            targetTileIndex = -1;
            cityTargetSeat = null;
            if (attackerCard == null)
            {
                return false;
            }

            if (TryGetForcedProvokeTargetTile(sourceTileIndex, seat, out int forcedTargetTileIndex))
            {
                targetTileIndex = forcedTargetTileIndex;
                return true;
            }

            if (HasManualCityAttackTarget(sourceTileIndex))
            {
                if (CanAttackCityDirectlyFromTile(sourceTileIndex, seat))
                {
                    cityTargetSeat = MatchPerspectiveUtility.GetOpposingSeat(seat);
                    return true;
                }

                return false;
            }

            if (TryFindManualAttackTarget(sourceTileIndex, out int manualTargetTileIndex))
            {
                if (IsCurrentAttackTargetValid(sourceTileIndex, manualTargetTileIndex, seat, attackerCard))
                {
                    targetTileIndex = manualTargetTileIndex;
                    return true;
                }

                return false;
            }

            int plannedAutoTargetTileIndex = sourceTileIndex >= 0 && sourceTileIndex < _displayAutoTargetTileBySource.Length
                ? _displayAutoTargetTileBySource[sourceTileIndex]
                : -1;
            if (plannedAutoTargetTileIndex >= 0)
            {
                if (IsCurrentAttackTargetValid(sourceTileIndex, plannedAutoTargetTileIndex, seat, attackerCard))
                {
                    targetTileIndex = plannedAutoTargetTileIndex;
                    return true;
                }

                return false;
            }

            if (CanAttackCityDirectlyFromTile(sourceTileIndex, seat))
            {
                cityTargetSeat = MatchPerspectiveUtility.GetOpposingSeat(seat);
                return true;
            }

            return false;
        }

        private bool IsCurrentAttackTargetValid(int sourceTileIndex, int targetTileIndex, MatchSeat seat, CardTemplate attackerCard)
        {
            if (attackerCard == null
                || targetTileIndex < 0
                || targetTileIndex >= _boardTileData.Length
                || !IsAttackTargetableIgnoringProvoke(sourceTileIndex, targetTileIndex, seat))
            {
                return false;
            }

            return !TryGetForcedProvokeTargetTile(sourceTileIndex, seat, out int forcedTargetTileIndex)
                || forcedTargetTileIndex == targetTileIndex;
        }

        private void RemoveOccupantAtTile(int tileIndex, bool resolveCardFate = true, RemovedCardFateOverride fateOverride = RemovedCardFateOverride.None)
        {
            CardTemplate removedCard = tileIndex >= 0 && tileIndex < _boardTileData.Length ? _boardTileData[tileIndex] : null;
            MatchSeat? removedSeat = tileIndex >= 0 && tileIndex < _tileOccupantSeats.Length ? _tileOccupantSeats[tileIndex] : null;

            _boardTileData[tileIndex] = null;
            _tileOccupantSeats[tileIndex] = null;
            _occupantCurrentHealth[tileIndex] = 0;
            _secureHoldTurnsByTile[tileIndex] = 0;
            _silenceTurnsByTile[tileIndex] = 0;
            _spawnChargeTurnsByTile[tileIndex] = 0;
            _interceptConsumedByTile[tileIndex] = 0;
            _tileLocked[tileIndex] = false;
            _attackTargetTileBySource[tileIndex] = -1;
            _moveTargetTileBySource[tileIndex] = -1;
            if (_selectedBoardTileIndex == tileIndex)
            {
                _selectedBoardTileIndex = -1;
            }
            if (_selectedAttackerTileIndex == tileIndex)
            {
                _selectedAttackerTileIndex = -1;
            }

            if (resolveCardFate && removedCard != null && removedSeat.HasValue)
            {
                ResolveRemovedBoardCardFate(removedSeat.Value, removedCard, fateOverride);
            }
        }

        private void SanitizeBoardOccupancyState(string context)
        {
            int cleanedTiles = 0;
            for (int tileIndex = 0; tileIndex < _boardTileData.Length; tileIndex++)
            {
                bool hasCard = _boardTileData[tileIndex] != null;
                bool hasSeat = _tileOccupantSeats[tileIndex].HasValue;
                bool deadOrMissingOccupant = hasCard && (!hasSeat || _occupantCurrentHealth[tileIndex] <= 0);
                if (deadOrMissingOccupant)
                {
                    Debug.LogWarning($"[UIManager][Sanity] Cleared invalid occupant at tile {tileIndex} during {context}.");
                    RemoveOccupantAtTile(tileIndex, false);
                    cleanedTiles++;
                    continue;
                }

                if (hasCard)
                {
                    continue;
                }

                bool hadTransientState = hasSeat
                    || _occupantCurrentHealth[tileIndex] != 0
                    || _secureHoldTurnsByTile[tileIndex] != 0
                    || _silenceTurnsByTile[tileIndex] != 0
                    || _spawnChargeTurnsByTile[tileIndex] != 0
                    || _interceptConsumedByTile[tileIndex] != 0
                    || _tileLocked[tileIndex]
                    || _attackTargetTileBySource[tileIndex] >= 0
                    || _moveTargetTileBySource[tileIndex] >= 0;
                if (!hadTransientState)
                {
                    continue;
                }

                _tileOccupantSeats[tileIndex] = null;
                _occupantCurrentHealth[tileIndex] = 0;
                _secureHoldTurnsByTile[tileIndex] = 0;
                _silenceTurnsByTile[tileIndex] = 0;
                _spawnChargeTurnsByTile[tileIndex] = 0;
                _interceptConsumedByTile[tileIndex] = 0;
                _tileLocked[tileIndex] = false;
                _attackTargetTileBySource[tileIndex] = -1;
                _moveTargetTileBySource[tileIndex] = -1;
                cleanedTiles++;
            }

            if (cleanedTiles > 0)
            {
                Debug.LogWarning($"[UIManager][Sanity] Cleaned {cleanedTiles} tile state issue(s) during {context}.");
            }
        }

        private void BreakBaseTileAt(int tileIndex)
        {
            if (tileIndex < 0 || tileIndex >= _boardTileData.Length)
            {
                return;
            }

            RemoveOccupantAtTile(tileIndex);
            _tileCurrentHealth[tileIndex] = 0;
            _tileMaxHealth[tileIndex] = 0;
            _tileAreaKinds[tileIndex] = TileAreaKind.Freeplay;
            _tileOwners[tileIndex] = TileOwner.Neutral;
            _tileBlocksCity[tileIndex] = false;
            AddFloatingBoardText(tileIndex, "BROKEN", "tile-floating-status");
        }

        private bool TryConsumeIntercept(int targetTileIndex, bool usePreviewState)
        {
            CardTemplate targetCard = targetTileIndex >= 0 && targetTileIndex < _boardTileData.Length ? _boardTileData[targetTileIndex] : null;
            if (!CardHasKeywordAtTile(targetTileIndex, AbilityKeyword.Intercept))
            {
                return false;
            }

            int[] interceptState = usePreviewState ? _previewInterceptConsumedByTile : _interceptConsumedByTile;
            int interceptLimit = Mathf.Max(1, GetEffectiveKeywordValueAtTile(targetTileIndex, AbilityKeyword.Intercept));
            if (interceptState == null || targetTileIndex < 0 || targetTileIndex >= interceptState.Length || interceptState[targetTileIndex] >= interceptLimit)
            {
                return false;
            }

            interceptState[targetTileIndex]++;
            return true;
        }

        private int GetAttackDamageAgainstTarget(int sourceTileIndex, MatchSeat attackerSeat, int targetTileIndex, int attackDamage)
        {
            if (attackDamage <= 0)
            {
                return 0;
            }

            CardTemplate attackerCard = sourceTileIndex >= 0 && sourceTileIndex < _boardTileData.Length ? _boardTileData[sourceTileIndex] : null;
            int siegeBonus = attackerCard != null ? Mathf.Max(0, attackerCard.bonusSiegeAttack) : 0;
            if (!CardHasKeywordAtTile(sourceTileIndex, AbilityKeyword.Shatter))
            {
                return attackDamage;
            }

            CardTemplate defenderCard = targetTileIndex >= 0 && targetTileIndex < _boardTileData.Length ? _boardTileData[targetTileIndex] : null;
            if (defenderCard != null)
            {
                return IsInfrastructureCard(defenderCard) ? (attackDamage + siegeBonus) * 2 : attackDamage;
            }

            return IsLiveEnemyBaseTileForSeat(targetTileIndex, attackerSeat) ? (attackDamage + siegeBonus) * 2 : attackDamage;
        }

        private bool CanApplyBreachFromAttack(CardTemplate attackerCard, CardTemplate defeatedCard)
        {
            return CardHasKeyword(attackerCard, AbilityKeyword.Breach) && IsUnitCard(defeatedCard);
        }

        private bool CarrierHasAttachedItem(int tileIndex, string itemCardId)
        {
            if (tileIndex < 0 || tileIndex >= _boardTileData.Length || string.IsNullOrWhiteSpace(itemCardId))
            {
                return false;
            }

            CardTemplate carrier = _boardTileData[tileIndex];
            return carrier != null
                && carrier.attachedItemCard != null
                && string.Equals(carrier.attachedItemCard.cardId, itemCardId, StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyOnHitCarrierItemEffects(int sourceTileIndex, int targetTileIndex)
        {
            if (CarrierHasAttachedItem(sourceTileIndex, "card.free_haven.truce_bell"))
            {
                ApplySilenceToTile(targetTileIndex, 1);
            }
        }

        private int GetBreachCarryTargetTileIndex(int sourceTileIndex, int targetTileIndex)
        {
            if (!TryGetRowColumnFromTileIndex(sourceTileIndex, out int sourceRow, out int sourceColumn)
                || !TryGetRowColumnFromTileIndex(targetTileIndex, out int targetRow, out int targetColumn))
            {
                return -1;
            }

            int rowStep = Math.Sign(targetRow - sourceRow);
            int columnStep = Math.Sign(targetColumn - sourceColumn);
            if (rowStep == 0 && columnStep == 0)
            {
                return -1;
            }

            int nextRow = targetRow + rowStep;
            int nextColumn = targetColumn + columnStep;
            return IsInBounds(nextRow, nextColumn) ? ToTileIndex(nextRow, nextColumn) : -1;
        }

        private void ApplyBreachOverflowActual(int sourceTileIndex, MatchSeat attackerSeat, int defeatedTargetTileIndex, int overflowDamage)
        {
            if (overflowDamage <= 0)
            {
                return;
            }

            int carryTargetTileIndex = GetBreachCarryTargetTileIndex(sourceTileIndex, defeatedTargetTileIndex);
            if (carryTargetTileIndex < 0)
            {
                return;
            }

            CardTemplate carryTargetCard = _boardTileData[carryTargetTileIndex];
            if (IsInfrastructureCard(carryTargetCard))
            {
                ApplyTileDamage(carryTargetTileIndex, overflowDamage, attackerSeat, sourceTileIndex);
                return;
            }

            if (IsSiegeableEnemyBaseTileForSeat(carryTargetTileIndex, attackerSeat))
            {
                ApplyBaseTileDamage(carryTargetTileIndex, overflowDamage, attackerSeat, sourceTileIndex);
            }
        }

        private bool TryApplyDamageToOccupantActual(int targetTileIndex, int damage, MatchSeat attackerSeat, int sourceTileIndex, out bool prevented, out bool unitKilled)
        {
            prevented = false;
            unitKilled = false;
            if (damage <= 0 || _boardTileData[targetTileIndex] == null || _occupantCurrentHealth[targetTileIndex] <= 0)
            {
                return false;
            }

            if (IsInfrastructureCard(_boardTileData[targetTileIndex]))
            {
                ApplyMergedInfrastructureTileDamage(targetTileIndex, damage);
                if (_boardTileData[targetTileIndex] != null)
                {
                    ApplyOnHitCarrierItemEffects(sourceTileIndex, targetTileIndex);
                }
                return true;
            }

            if (TryConsumeIntercept(targetTileIndex, false))
            {
                prevented = true;
                AddFloatingBoardText(targetTileIndex, "BLOCK", "tile-floating-status");
                return false;
            }

            CardTemplate targetCard = _boardTileData[targetTileIndex];
            int startingHealth = Mathf.Max(0, _occupantCurrentHealth[targetTileIndex]);
            _occupantCurrentHealth[targetTileIndex] = Mathf.Max(0, startingHealth - damage);
            int appliedDamage = Mathf.Max(0, startingHealth - _occupantCurrentHealth[targetTileIndex]);
            AddFloatingBoardText(targetTileIndex, $"-{appliedDamage}");
            if (_occupantCurrentHealth[targetTileIndex] > 0)
            {
                ApplyOnHitCarrierItemEffects(sourceTileIndex, targetTileIndex);
                return false;
            }

            bool targetWasUnit = IsUnitCard(targetCard);
            int overflowDamage = Mathf.Max(0, damage - startingHealth);
            unitKilled = targetWasUnit;
            RemovedCardFateOverride fateOverride = CarrierHasAttachedItem(sourceTileIndex, "card.iron_citadel.ash_brand")
                ? RemovedCardFateOverride.Burn
                : RemovedCardFateOverride.None;
            RemoveOccupantAtTile(targetTileIndex, true, fateOverride);

            // Salvage is now an explicit keyword effect, not a passive baked into card bodies.

            CardTemplate attackerCard = sourceTileIndex >= 0 && sourceTileIndex < _boardTileData.Length ? _boardTileData[sourceTileIndex] : null;
            if (overflowDamage > 0 && CanApplyBreachFromAttack(attackerCard, targetCard))
            {
                ApplyBreachOverflowActual(sourceTileIndex, attackerSeat, targetTileIndex, overflowDamage);
            }

            return true;
        }

        private void ApplyTileDamage(int targetTileIndex, int damage, MatchSeat attackerSeat, int sourceTileIndex = -1)
        {
            if (damage <= 0)
            {
                return;
            }

            if (IsInfrastructureCard(_boardTileData[targetTileIndex]))
            {
                ApplyMergedInfrastructureTileDamage(targetTileIndex, damage);
                return;
            }

            if (_boardTileData[targetTileIndex] != null)
            {
                TryApplyDamageToOccupantActual(targetTileIndex, damage, attackerSeat, sourceTileIndex, out _, out _);
                return;
            }

            if (_tileCurrentHealth[targetTileIndex] <= 0)
            {
                return;
            }

            _tileCurrentHealth[targetTileIndex] = Mathf.Max(0, _tileCurrentHealth[targetTileIndex] - damage);
            AddFloatingBoardText(targetTileIndex, $"-{damage}");
            if (_tileCurrentHealth[targetTileIndex] == 0 && _tileAreaKinds[targetTileIndex] == TileAreaKind.Base)
            {
                BreakBaseTileAt(targetTileIndex);
            }
        }

        private void ApplyMergedInfrastructureTileDamage(int targetTileIndex, int damage)
        {
            if (damage <= 0
                || targetTileIndex < 0
                || targetTileIndex >= _boardTileData.Length
                || !IsInfrastructureCard(_boardTileData[targetTileIndex])
                || _tileCurrentHealth[targetTileIndex] <= 0)
            {
                return;
            }

            _tileCurrentHealth[targetTileIndex] = Mathf.Max(0, _tileCurrentHealth[targetTileIndex] - damage);
            _occupantCurrentHealth[targetTileIndex] = _tileCurrentHealth[targetTileIndex];
            AddFloatingBoardText(targetTileIndex, $"-{damage}");
            if (_tileCurrentHealth[targetTileIndex] == 0)
            {
                BreakBaseTileAt(targetTileIndex);
            }
        }

        private void ApplyTileDamage(int targetTileIndex, int damage)
        {
            MatchSeat attackerSeat = MatchSeat.SeatOne;
            if (!TryGetTileSeat(_selectedAttackerTileIndex, out attackerSeat))
            {
                MatchSeat? baseSeat = targetTileIndex >= 0 && targetTileIndex < _boardTileData.Length ? GetSeatFromTileOwner(_tileOwners[targetTileIndex]) : null;
                attackerSeat = baseSeat.HasValue ? MatchPerspectiveUtility.GetOpposingSeat(baseSeat.Value) : MatchSeat.SeatOne;
            }

            ApplyTileDamage(targetTileIndex, damage, attackerSeat, _selectedAttackerTileIndex);
        }

        private void ApplyBaseTileDamage(int targetTileIndex, int damage, MatchSeat attackerSeat = MatchSeat.SeatOne, int sourceTileIndex = -1)
        {
            if (damage <= 0
                || targetTileIndex < 0
                || targetTileIndex >= _boardTileData.Length
                || _tileAreaKinds[targetTileIndex] != TileAreaKind.Base
                || _tileCurrentHealth[targetTileIndex] <= 0)
            {
                return;
            }

            _tileCurrentHealth[targetTileIndex] = Mathf.Max(0, _tileCurrentHealth[targetTileIndex] - damage);
            AddFloatingBoardText(targetTileIndex, $"-{damage}");
            if (_tileCurrentHealth[targetTileIndex] == 0)
            {
                if (sourceTileIndex >= 0
                    && sourceTileIndex < _boardTileData.Length
                    && CardHasKeyword(_boardTileData[sourceTileIndex], AbilityKeyword.Reclaim)
                    && TryConsumeKeywordValue(_boardTileData[sourceTileIndex], AbilityKeyword.Reclaim))
                {
                    ReclaimBrokenBaseTile(targetTileIndex, attackerSeat);
                }
                else
                {
                    BreakBaseTileAt(targetTileIndex);
                }
            }
        }

        private void ReclaimBrokenBaseTile(int tileIndex, MatchSeat newOwnerSeat)
        {
            RemoveOccupantAtTile(tileIndex);
            _tileAreaKinds[tileIndex] = TileAreaKind.Base;
            _tileOwners[tileIndex] = newOwnerSeat == MatchSeat.SeatOne ? TileOwner.SeatOne : TileOwner.SeatTwo;
            _tileMaxHealth[tileIndex] = Mathf.Max(20, _tileMaxHealth[tileIndex]);
            _tileCurrentHealth[tileIndex] = _tileMaxHealth[tileIndex];
            _tileBlocksCity[tileIndex] = true;
            AddFloatingBoardText(tileIndex, "RECLAIM", "tile-floating-status");
        }

        private void ApplyCityDamage(MatchSeat targetSeat, int damage)
        {
            if (damage <= 0)
            {
                return;
            }

            ParticipantRuntimeState targetState = GetRuntimeState(targetSeat);
            if (targetState == null)
            {
                return;
            }

            targetState.health = Mathf.Max(0, targetState.health - damage);
            float flashExpiresAt = (Application.isPlaying ? Time.unscaledTime : 0f) + CityFlashDurationSeconds;
            if (targetSeat == MatchSeat.SeatOne)
            {
                _seatOneCityFlashExpiresAt = flashExpiresAt;
            }
            else
            {
                _seatTwoCityFlashExpiresAt = flashExpiresAt;
            }

            SyncVisibleStateFromPerspective();
            if (targetState.health <= 0)
            {
                EndMatch(MatchPerspectiveUtility.GetOpposingSeat(targetSeat), targetSeat);
            }
        }

        private void EndMatch(MatchSeat winnerSeat, MatchSeat defeatedSeat)
        {
            if (_matchEnded)
            {
                return;
            }

            _matchEnded = true;
            _winningSeat = winnerSeat;
            _matchEndMessage = $"{GetSeatDisplayName(defeatedSeat)} has fallen. Return to menu to start the next test.";
            _phaseEndsAtUnscaledTime = -1f;
            _nextDisplayActionAtUnscaledTime = -1f;
            _autoAdvanceAtUnscaledTime = -1f;
            _cardDeployInFlight = false;
            _placementFocusActive = false;
            _highlightedCardIndex = -1;
            _selectedAttackerTileIndex = -1;
            _selectedBoardTileIndex = -1;
            _displayStageLabel = "GAME OVER";
            _displayStageSeat = winnerSeat;
            _displayNarrationText = _matchEndMessage;
            SetAbilityPreviewCard(null);
            UpdateUI();
        }

        private void RequestBackToMenuAfterMatch()
        {
            _matchEnded = false;
            _matchEndMessage = string.Empty;
            if (MatchBackToMenuRequested != null)
            {
                MatchBackToMenuRequested.Invoke();
                return;
            }

            ShowLaunchModePicker("Match ended. Choose a mode to start again.");
        }

        private List<int> GetOrderedFriendlyUnitTilesForSeat(MatchSeat seat)
        {
            List<int> orderedTiles = new List<int>();
            bool useAttackPhaseBonuses = _roundPhase == MatchRoundPhase.CombatPlanning
                || (_roundPhase == MatchRoundPhase.DisplayResolution && _displayResolutionMode == DisplayResolutionMode.Attack);
            for (int i = 0; i < _boardTileData.Length; i++)
            {
                if (_tileOccupantSeats[i].HasValue
                    && _tileOccupantSeats[i].Value == seat
                    && IsUnitCard(_boardTileData[i])
                    && _occupantCurrentHealth[i] > 0)
                {
                    orderedTiles.Add(i);
                }
            }

            orderedTiles.Sort((left, right) =>
            {
                int leftProgress = GetForwardProgressForSeat(seat, left);
                int rightProgress = GetForwardProgressForSeat(seat, right);
                int progressCompare = rightProgress.CompareTo(leftProgress);
                if (progressCompare != 0)
                {
                    return progressCompare;
                }

                int rightAttack = GetCurrentAttackValueForTile(right, useAttackPhaseBonuses);
                int leftAttack = GetCurrentAttackValueForTile(left, useAttackPhaseBonuses);
                int attackCompare = rightAttack.CompareTo(leftAttack);
                if (attackCompare != 0)
                {
                    return attackCompare;
                }

                if (!TryGetRowColumnFromTileIndex(left, out int leftRow, out int leftColumn)
                    || !TryGetRowColumnFromTileIndex(right, out int rightRow, out int rightColumn))
                {
                    return left.CompareTo(right);
                }

                int rowCompare = leftRow.CompareTo(rightRow);
                if (rowCompare != 0)
                {
                    return rowCompare;
                }

                return leftColumn.CompareTo(rightColumn);
            });

            return orderedTiles;
        }

        private void ResolveAttackPhaseForSeat(MatchSeat seat)
        {
            List<int> actingTiles = GetOrderedFriendlyUnitTilesForSeat(seat);
            foreach (int sourceTileIndex in actingTiles)
            {
                CardTemplate attackerCard = _boardTileData[sourceTileIndex];
                if (!IsUnitCard(attackerCard) || _occupantCurrentHealth[sourceTileIndex] <= 0)
                {
                    continue;
                }

                if (!TryResolveAttackTarget(sourceTileIndex, seat, attackerCard, out int targetTileIndex, out MatchSeat? cityTargetSeat))
                {
                    AddFloatingBoardText(sourceTileIndex, "MISS", "tile-floating-status");
                    TryConsumeEphemeralAttackerAfterAttack(sourceTileIndex);
                    continue;
                }

                int attackDamage = GetCurrentAttackValueForTile(sourceTileIndex, true);
                if (cityTargetSeat.HasValue)
                {
                    ApplyCityDamage(cityTargetSeat.Value, attackDamage);
                    AddFloatingBoardText(sourceTileIndex, "CITY!", "tile-floating-action");
                    TryResolveGuerrillaRetreat(sourceTileIndex, seat);
                    TryConsumeEphemeralAttackerAfterAttack(sourceTileIndex);
                    continue;
                }

                if (IsSiegeableEnemyBaseTileForSeat(targetTileIndex, seat))
                {
                    ApplyBaseTileDamage(targetTileIndex, GetAttackDamageAgainstTarget(sourceTileIndex, seat, targetTileIndex, attackDamage), seat, sourceTileIndex);
                    AddFloatingBoardText(sourceTileIndex, "SIEGE!", "tile-floating-action");
                    TryResolveGuerrillaRetreat(sourceTileIndex, seat);
                    TryConsumeEphemeralAttackerAfterAttack(sourceTileIndex);
                    continue;
                }

                ApplyTileDamage(targetTileIndex, GetAttackDamageAgainstTarget(sourceTileIndex, seat, targetTileIndex, attackDamage), seat, sourceTileIndex);
                TryResolveGuerrillaRetreat(sourceTileIndex, seat);
                TryConsumeEphemeralAttackerAfterAttack(sourceTileIndex);
            }
        }

        private void TryConsumeEphemeralAttackerAfterAttack(int sourceTileIndex)
        {
            if (sourceTileIndex < 0
                || sourceTileIndex >= _boardTileData.Length
                || !IsBelfryTokenCard(_boardTileData[sourceTileIndex]))
            {
                return;
            }

            AddFloatingBoardText(sourceTileIndex, "BURN", "tile-floating-status");
            RemoveOccupantAtTile(sourceTileIndex, true, RemovedCardFateOverride.Burn);
        }

        private List<int> GetDiagonalDetourCandidates(int sourceRow, int sourceColumn, int rowStep, MatchSeat seat)
        {
            List<int> candidates = new List<int>(2);
            int leftColumn = sourceColumn - 1;
            int rightColumn = sourceColumn + 1;
            int targetRow = sourceRow + rowStep;

            if (IsInBounds(targetRow, leftColumn))
            {
                int leftIndex = ToTileIndex(targetRow, leftColumn);
                if (CanUseInfrastructureDetourTile(leftIndex, seat))
                {
                    candidates.Add(leftIndex);
                }
            }

            if (IsInBounds(targetRow, rightColumn))
            {
                int rightIndex = ToTileIndex(targetRow, rightColumn);
                if (CanUseInfrastructureDetourTile(rightIndex, seat))
                {
                    candidates.Add(rightIndex);
                }
            }

            return candidates;
        }

        private bool CanUseInfrastructureDetourTile(int tileIndex, MatchSeat seat)
        {
            if (tileIndex < 0 || tileIndex >= _boardTileData.Length)
            {
                return false;
            }

            if (_boardTileData[tileIndex] != null)
            {
                return false;
            }

            return _tileAreaKinds[tileIndex] == TileAreaKind.Freeplay;
        }

        private int GetDefaultMoveTargetTile(int sourceTileIndex, MatchSeat seat)
        {
            if (sourceTileIndex < 0 || sourceTileIndex >= _boardTileData.Length)
            {
                return -1;
            }

            CardTemplate sourceCard = _boardTileData[sourceTileIndex];
            if (sourceCard == null
                || !IsUnitCard(sourceCard)
                || _occupantCurrentHealth[sourceTileIndex] <= 0
                || _tileLocked[sourceTileIndex])
            {
                return -1;
            }

            if (!TryGetRowColumnFromTileIndex(sourceTileIndex, out int sourceRow, out int sourceColumn))
            {
                return -1;
            }

            int rowStep = GetForwardRowStepForSeat(seat);
            int targetRow = sourceRow + rowStep;
            if (!IsInBounds(targetRow, sourceColumn))
            {
                return -1;
            }

            int forwardTileIndex = ToTileIndex(targetRow, sourceColumn);
            if (IsInfrastructureCard(_boardTileData[forwardTileIndex]))
            {
                List<int> detours = GetDiagonalDetourCandidates(sourceRow, sourceColumn, rowStep, seat);
                return detours.Count > 0 ? detours[0] : -1;
            }

            if (IsLiveEnemyBaseTileForSeat(forwardTileIndex, seat) || _boardTileData[forwardTileIndex] != null)
            {
                return -1;
            }

            List<int> candidates = GetValidMoveTargetTiles(sourceTileIndex, seat);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] == forwardTileIndex)
                {
                    return forwardTileIndex;
                }
            }

            return -1;
        }

        private int GetDesiredMoveTargetTile(int sourceTileIndex, MatchSeat seat)
        {
            if (sourceTileIndex < 0 || sourceTileIndex >= _boardTileData.Length)
            {
                return -1;
            }

            int assignedTargetTileIndex = _moveTargetTileBySource[sourceTileIndex];
            if (assignedTargetTileIndex >= 0 && CanSourceUnitMoveToTile(sourceTileIndex, assignedTargetTileIndex, seat))
            {
                return assignedTargetTileIndex;
            }

            return GetDefaultMoveTargetTile(sourceTileIndex, seat);
        }

        private int GetDesiredMoveTargetTileForPreview(int sourceTileIndex, MatchSeat seat, HashSet<int> visitingTiles)
        {
            if (sourceTileIndex < 0 || sourceTileIndex >= _boardTileData.Length)
            {
                return -1;
            }

            int assignedTargetTileIndex = _moveTargetTileBySource[sourceTileIndex];
            if (assignedTargetTileIndex >= 0 && CanSourceUnitMoveToTileForPreview(sourceTileIndex, assignedTargetTileIndex, seat, visitingTiles))
            {
                return assignedTargetTileIndex;
            }

            return GetDefaultMoveTargetTileForPreview(sourceTileIndex, seat, visitingTiles);
        }

        private List<int> GetValidMoveTargetTiles(int sourceTileIndex, MatchSeat seat)
        {
            List<int> candidates = new List<int>();
            CardTemplate sourceCard = _boardTileData[sourceTileIndex];
            if (sourceCard == null
                || !IsUnitCard(sourceCard)
                || _occupantCurrentHealth[sourceTileIndex] <= 0
                || _tileLocked[sourceTileIndex])
            {
                return candidates;
            }

            if (!TryGetRowColumnFromTileIndex(sourceTileIndex, out int sourceRow, out int sourceColumn))
            {
                return candidates;
            }

            int rowStep = GetForwardRowStepForSeat(seat);
            int movementRange = GetCardMovementRangeAtTile(sourceTileIndex);
            if (CardHasKeywordAtTile(sourceTileIndex, AbilityKeyword.Maneuver))
            {
                AddManeuverMoveCandidates(candidates, sourceRow, sourceColumn, movementRange, seat);
                return candidates;
            }

            for (int distance = 1; distance <= movementRange; distance++)
            {
                int targetRow = sourceRow + (rowStep * distance);
                if (!IsInBounds(targetRow, sourceColumn))
                {
                    break;
                }

                int targetTileIndex = ToTileIndex(targetRow, sourceColumn);
                if (distance == 1 && IsInfrastructureCard(_boardTileData[targetTileIndex]))
                {
                    candidates.AddRange(GetDiagonalDetourCandidates(sourceRow, sourceColumn, rowStep, seat));
                    break;
                }

                if (IsLiveEnemyBaseTileForSeat(targetTileIndex, seat))
                {
                    break;
                }

                if (_boardTileData[targetTileIndex] == null)
                {
                    candidates.Add(targetTileIndex);
                    continue;
                }

                break;
            }

            return candidates;
        }

        private int GetDefaultMoveTargetTileForPreview(int sourceTileIndex, MatchSeat seat, HashSet<int> visitingTiles)
        {
            if (sourceTileIndex < 0 || sourceTileIndex >= _boardTileData.Length)
            {
                return -1;
            }

            CardTemplate sourceCard = _boardTileData[sourceTileIndex];
            if (sourceCard == null
                || !IsUnitCard(sourceCard)
                || _occupantCurrentHealth[sourceTileIndex] <= 0
                || _tileLocked[sourceTileIndex])
            {
                return -1;
            }

            if (!TryGetRowColumnFromTileIndex(sourceTileIndex, out int sourceRow, out int sourceColumn))
            {
                return -1;
            }

            int rowStep = GetForwardRowStepForSeat(seat);
            int targetRow = sourceRow + rowStep;
            if (!IsInBounds(targetRow, sourceColumn))
            {
                return -1;
            }

            int forwardTileIndex = ToTileIndex(targetRow, sourceColumn);
            if (IsInfrastructureCard(_boardTileData[forwardTileIndex]))
            {
                List<int> detours = GetDiagonalDetourCandidatesForPreview(sourceRow, sourceColumn, rowStep, seat, visitingTiles);
                return detours.Count > 0 ? detours[0] : -1;
            }

            if (IsLiveEnemyBaseTileForSeat(forwardTileIndex, seat))
            {
                return -1;
            }

            List<int> candidates = GetValidMoveTargetTilesForPreview(sourceTileIndex, seat, visitingTiles);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] == forwardTileIndex)
                {
                    return forwardTileIndex;
                }
            }

            return -1;
        }

        private bool CanSourceUnitMoveToTileForPreview(int sourceTileIndex, int targetTileIndex, MatchSeat seat, HashSet<int> visitingTiles)
        {
            if (targetTileIndex < 0 || targetTileIndex >= _boardTileData.Length)
            {
                return false;
            }

            List<int> candidates = GetValidMoveTargetTilesForPreview(sourceTileIndex, seat, visitingTiles);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] == targetTileIndex)
                {
                    return true;
                }
            }

            return false;
        }

        private List<int> GetValidMoveTargetTilesForPreview(int sourceTileIndex, MatchSeat seat, HashSet<int> visitingTiles)
        {
            List<int> candidates = new List<int>();
            CardTemplate sourceCard = _boardTileData[sourceTileIndex];
            if (sourceCard == null
                || !IsUnitCard(sourceCard)
                || _occupantCurrentHealth[sourceTileIndex] <= 0
                || _tileLocked[sourceTileIndex])
            {
                return candidates;
            }

            if (!TryGetRowColumnFromTileIndex(sourceTileIndex, out int sourceRow, out int sourceColumn))
            {
                return candidates;
            }

            int rowStep = GetForwardRowStepForSeat(seat);
            int movementRange = GetCardMovementRangeAtTile(sourceTileIndex);
            if (CardHasKeywordAtTile(sourceTileIndex, AbilityKeyword.Maneuver))
            {
                AddManeuverMoveCandidatesForPreview(candidates, sourceTileIndex, sourceRow, sourceColumn, movementRange, seat, visitingTiles);
                return candidates;
            }

            for (int distance = 1; distance <= movementRange; distance++)
            {
                int targetRow = sourceRow + (rowStep * distance);
                if (!IsInBounds(targetRow, sourceColumn))
                {
                    break;
                }

                int targetTileIndex = ToTileIndex(targetRow, sourceColumn);
                if (distance == 1 && IsInfrastructureCard(_boardTileData[targetTileIndex]))
                {
                    candidates.AddRange(GetDiagonalDetourCandidatesForPreview(sourceRow, sourceColumn, rowStep, seat, visitingTiles));
                    break;
                }

                if (IsLiveEnemyBaseTileForSeat(targetTileIndex, seat))
                {
                    break;
                }

                if (CanOccupyTileInPreview(sourceTileIndex, targetTileIndex, seat, visitingTiles))
                {
                    candidates.Add(targetTileIndex);
                    continue;
                }

                break;
            }

            return candidates;
        }

        private void AddManeuverMoveCandidatesForPreview(List<int> candidates, int sourceTileIndex, int sourceRow, int sourceColumn, int movementRange, MatchSeat seat, HashSet<int> visitingTiles)
        {
            int[,] directions = { { -1, 0 }, { 1, 0 }, { 0, -1 }, { 0, 1 } };
            for (int directionIndex = 0; directionIndex < directions.GetLength(0); directionIndex++)
            {
                for (int distance = 1; distance <= movementRange; distance++)
                {
                    int targetRow = sourceRow + (directions[directionIndex, 0] * distance);
                    int targetColumn = sourceColumn + (directions[directionIndex, 1] * distance);
                    if (!IsInBounds(targetRow, targetColumn))
                    {
                        break;
                    }

                    int targetTileIndex = ToTileIndex(targetRow, targetColumn);
                    if (IsLiveEnemyBaseTileForSeat(targetTileIndex, seat))
                    {
                        break;
                    }

                    if (CanOccupyTileInPreview(sourceTileIndex, targetTileIndex, seat, visitingTiles))
                    {
                        candidates.Add(targetTileIndex);
                        continue;
                    }

                    break;
                }
            }
        }

        private List<int> GetDiagonalDetourCandidatesForPreview(int sourceRow, int sourceColumn, int rowStep, MatchSeat seat, HashSet<int> visitingTiles)
        {
            List<int> candidates = new List<int>(2);
            int leftColumn = sourceColumn - 1;
            int rightColumn = sourceColumn + 1;
            int targetRow = sourceRow + rowStep;

            if (IsInBounds(targetRow, leftColumn))
            {
                int leftIndex = ToTileIndex(targetRow, leftColumn);
                if (CanOccupyTileInPreview(ToTileIndex(sourceRow, sourceColumn), leftIndex, seat, visitingTiles) && _tileAreaKinds[leftIndex] == TileAreaKind.Freeplay)
                {
                    candidates.Add(leftIndex);
                }
            }

            if (IsInBounds(targetRow, rightColumn))
            {
                int rightIndex = ToTileIndex(targetRow, rightColumn);
                if (CanOccupyTileInPreview(ToTileIndex(sourceRow, sourceColumn), rightIndex, seat, visitingTiles) && _tileAreaKinds[rightIndex] == TileAreaKind.Freeplay)
                {
                    candidates.Add(rightIndex);
                }
            }

            return candidates;
        }

        private bool CanOccupyTileInPreview(int sourceTileIndex, int targetTileIndex, MatchSeat seat, HashSet<int> visitingTiles)
        {
            if (targetTileIndex < 0 || targetTileIndex >= _boardTileData.Length)
            {
                return false;
            }

            if (_boardTileData[targetTileIndex] == null)
            {
                return true;
            }

            if (!_tileOccupantSeats[targetTileIndex].HasValue
                || _tileOccupantSeats[targetTileIndex].Value != seat
                || !IsUnitCard(_boardTileData[targetTileIndex])
                || _occupantCurrentHealth[targetTileIndex] <= 0)
            {
                return false;
            }

            if (visitingTiles == null)
            {
                return false;
            }

            if (visitingTiles.Contains(targetTileIndex))
            {
                return false;
            }

            visitingTiles.Add(targetTileIndex);
            int vacateTargetTileIndex = GetDesiredMoveTargetTileForPreview(targetTileIndex, seat, visitingTiles);
            visitingTiles.Remove(targetTileIndex);
            return vacateTargetTileIndex >= 0 && vacateTargetTileIndex != sourceTileIndex;
        }

        private void AddManeuverMoveCandidates(List<int> candidates, int sourceRow, int sourceColumn, int movementRange, MatchSeat seat)
        {
            int[,] directions = { { -1, 0 }, { 1, 0 }, { 0, -1 }, { 0, 1 } };
            for (int directionIndex = 0; directionIndex < directions.GetLength(0); directionIndex++)
            {
                for (int distance = 1; distance <= movementRange; distance++)
                {
                    int targetRow = sourceRow + (directions[directionIndex, 0] * distance);
                    int targetColumn = sourceColumn + (directions[directionIndex, 1] * distance);
                    if (!IsInBounds(targetRow, targetColumn))
                    {
                        break;
                    }

                    int targetTileIndex = ToTileIndex(targetRow, targetColumn);
                    if (_boardTileData[targetTileIndex] != null || IsLiveEnemyBaseTileForSeat(targetTileIndex, seat))
                    {
                        break;
                    }

                    candidates.Add(targetTileIndex);
                }
            }
        }

        private int GetNoMoveIntentTile(int sourceTileIndex, MatchSeat seat)
        {
            if (!TryGetRowColumnFromTileIndex(sourceTileIndex, out int sourceRow, out int sourceColumn))
            {
                return -1;
            }

            int targetRow = sourceRow + GetForwardRowStepForSeat(seat);
            return IsInBounds(targetRow, sourceColumn) ? ToTileIndex(targetRow, sourceColumn) : -1;
        }

        private bool CanSourceUnitMoveToTile(int sourceTileIndex, int targetTileIndex, MatchSeat seat)
        {
            if (targetTileIndex < 0 || targetTileIndex >= _boardTileData.Length)
            {
                return false;
            }

            List<int> candidates = GetValidMoveTargetTiles(sourceTileIndex, seat);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] == targetTileIndex)
                {
                    return true;
                }
            }

            return false;
        }

        private int GetPlannedMoveTargetTile(int sourceTileIndex, MatchSeat seat)
        {
            if (sourceTileIndex < 0 || sourceTileIndex >= _boardTileData.Length)
            {
                return -1;
            }

            if (_isRefreshingMovementPreview)
            {
                return GetDesiredMoveTargetTile(sourceTileIndex, seat);
            }

            RefreshMovementPreviewState();
            if (_previewResolvedMoveTargetBySource != null
                && sourceTileIndex < _previewResolvedMoveTargetBySource.Length)
            {
                return _previewResolvedMoveTargetBySource[sourceTileIndex];
            }

            return GetDesiredMoveTargetTile(sourceTileIndex, seat);
        }

        private void ClearMovementAssignmentsForSeat(MatchSeat seat)
        {
            for (int i = 0; i < _moveTargetTileBySource.Length; i++)
            {
                if (_tileOccupantSeats[i].HasValue && _tileOccupantSeats[i].Value == seat)
                {
                    _moveTargetTileBySource[i] = -1;
                }
            }
        }

        private bool HasAnyDeployableCardForSeat(MatchSeat seat)
        {
            ParticipantRuntimeState state = GetRuntimeState(seat);
            if (state == null)
            {
                return false;
            }

            for (int handIndex = 0; handIndex < state.hand.Count; handIndex++)
            {
                CardTemplate cardData = state.hand[handIndex];
                if (cardData == null
                    || IsLockCommandCard(cardData)
                    || GetEffectiveDeploymentCost(cardData, seat) > state.treasury)
                {
                    continue;
                }

                for (int tileIndex = 0; tileIndex < _boardTileData.Length; tileIndex++)
                {
                    if ((IsBoardDeployableCard(cardData) && CanDeployCardToTile(tileIndex, seat))
                        || (cardData.cardType == CardType.Ordinance && CanApplyOrdinanceToTile(cardData, tileIndex, seat))
                        || (cardData.cardType == CardType.Item && CanApplyItemToTile(cardData, tileIndex, seat)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasAnyLockActionForSeat(MatchSeat seat)
        {
            ParticipantRuntimeState state = GetRuntimeState(seat);
            if (state == null)
            {
                return false;
            }

            bool hasLockCard = false;
            for (int handIndex = 0; handIndex < state.hand.Count; handIndex++)
            {
                if (IsLockCommandCard(state.hand[handIndex]))
                {
                    hasLockCard = true;
                    break;
                }
            }

            if (!hasLockCard)
            {
                return false;
            }

            for (int tileIndex = 0; tileIndex < _boardTileData.Length; tileIndex++)
            {
                if (CanTileReceiveLock(tileIndex, seat))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasAnyMovementActionForSeat(MatchSeat seat)
        {
            for (int tileIndex = 0; tileIndex < _boardTileData.Length; tileIndex++)
            {
                if (_tileOccupantSeats[tileIndex].HasValue
                    && _tileOccupantSeats[tileIndex].Value == seat
                    && GetValidMoveTargetTiles(tileIndex, seat).Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasAnyAttackActionForSeat(MatchSeat seat)
        {
            for (int tileIndex = 0; tileIndex < _boardTileData.Length; tileIndex++)
            {
                CardTemplate cardData = _boardTileData[tileIndex];
                if (!_tileOccupantSeats[tileIndex].HasValue
                    || _tileOccupantSeats[tileIndex].Value != seat
                    || !IsUnitCard(cardData)
                    || _occupantCurrentHealth[tileIndex] <= 0)
                {
                    continue;
                }

                if (CanAttackCityDirectlyFromTile(tileIndex, seat))
                {
                    return true;
                }

                int range = GetCardAttackRangeAtTile(tileIndex);
                if (GetAutoAttackTargetTile(tileIndex, seat, range) >= 0)
                {
                    return true;
                }

                for (int targetTileIndex = 0; targetTileIndex < _boardTileData.Length; targetTileIndex++)
                {
                    if (CanSourceUnitTargetTile(tileIndex, targetTileIndex))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void EvaluateAutoAdvanceForPlanningPhase(MatchRoundPhase phase, MatchSeat seat)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            bool shouldAutoAdvance = false;
            string cityName = GetSeatDisplayName(seat);
            string message = string.Empty;

            if (phase == MatchRoundPhase.DeployPlanning)
            {
                shouldAutoAdvance = !HasAnyDeployableCardForSeat(seat)
                    && !HasAnyMovementActionForSeat(seat)
                    && !HasAnyLockActionForSeat(seat);
                message = $"{cityName} has no valid deploys, movements, or locks. Auto-readying.";
            }
            else if (phase == MatchRoundPhase.CombatPlanning)
            {
                shouldAutoAdvance = !HasAnyAttackActionForSeat(seat)
                    && !HasAnyWarShopActionForSeat(seat);
                message = $"{cityName} has no valid attacks or War Shop plays. Auto-readying.";
            }

            if (shouldAutoAdvance)
            {
                QueueAutoAdvanceWithAwareness(message);
            }
        }

        private bool HasAnyWarShopActionForSeat(MatchSeat seat)
        {
            if (_roundPhase != MatchRoundPhase.CombatPlanning || HasUsedWarShopPurchase(seat))
            {
                return false;
            }

            for (int optionIndex = 0; optionIndex <= (int)WarShopOption.RebuildOrder; optionIndex++)
            {
                WarShopOption option = (WarShopOption)optionIndex;
                if (!CanAffordWarShopOption(option, seat))
                {
                    continue;
                }

                for (int tileIndex = 0; tileIndex < _boardTileData.Length; tileIndex++)
                {
                    if (CanApplyWarShopOptionToTile(option, tileIndex, seat))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void BuildDisplayMoveQueue()
        {
            _displayMoveQueue.Clear();
            _displayMoveQueueIndex = 0;

            MatchSeat secondSeat = MatchPerspectiveUtility.GetOpposingSeat(_roundInitiativeSeat);
            RefreshMovementPreviewState();
            AppendDisplayMoveStepsForSeat(_roundInitiativeSeat);
            AppendDisplayMoveStepsForSeat(secondSeat);
        }

        private void AppendDisplayMoveStepsForSeat(MatchSeat seat)
        {
            List<int> orderedTiles = GetOrderedFriendlyUnitTilesForSeat(seat);
            for (int i = 0; i < orderedTiles.Count; i++)
            {
                int sourceTileIndex = orderedTiles[i];
                if (sourceTileIndex >= 0
                    && sourceTileIndex < _displayMovementConsumedByTile.Length
                    && _displayMovementConsumedByTile[sourceTileIndex])
                {
                    continue;
                }

                int targetTileIndex = GetPlannedMoveTargetTile(sourceTileIndex, seat);
                if (targetTileIndex < 0)
                {
                    continue;
                }

                AppendDisplayMovePathSteps(seat, sourceTileIndex, targetTileIndex);
            }
        }

        private void AppendDisplayMovePathSteps(MatchSeat seat, int sourceTileIndex, int targetTileIndex)
        {
            if (!TryGetRowColumnFromTileIndex(sourceTileIndex, out int sourceRow, out int sourceColumn)
                || !TryGetRowColumnFromTileIndex(targetTileIndex, out int targetRow, out int targetColumn))
            {
                _displayMoveQueue.Add(new DisplayMoveStepRuntime
                {
                    seat = seat,
                    sourceTileIndex = sourceTileIndex,
                    targetTileIndex = targetTileIndex
                });
                return;
            }

            int rowDelta = targetRow - sourceRow;
            int columnDelta = targetColumn - sourceColumn;
            bool isStraightVertical = sourceColumn == targetColumn;
            bool isStraightHorizontal = sourceRow == targetRow;

            if (!isStraightVertical && !isStraightHorizontal)
            {
                _displayMoveQueue.Add(new DisplayMoveStepRuntime
                {
                    seat = seat,
                    sourceTileIndex = sourceTileIndex,
                    targetTileIndex = targetTileIndex
                });
                return;
            }

            int rowStep = rowDelta == 0 ? 0 : (rowDelta > 0 ? 1 : -1);
            int columnStep = columnDelta == 0 ? 0 : (columnDelta > 0 ? 1 : -1);
            int stepCount = Mathf.Max(Mathf.Abs(rowDelta), Mathf.Abs(columnDelta));

            int probeRow = sourceRow;
            int probeColumn = sourceColumn;
            for (int step = 1; step < stepCount; step++)
            {
                probeRow += rowStep;
                probeColumn += columnStep;
                int intermediateTileIndex = ToTileIndex(probeRow, probeColumn);
                if (_boardTileData[intermediateTileIndex] != null)
                {
                    _displayMoveQueue.Add(new DisplayMoveStepRuntime
                    {
                        seat = seat,
                        sourceTileIndex = sourceTileIndex,
                        targetTileIndex = targetTileIndex
                    });
                    return;
                }
            }

            int currentSourceTileIndex = sourceTileIndex;
            int currentRow = sourceRow;
            int currentColumn = sourceColumn;
            for (int step = 0; step < stepCount; step++)
            {
                currentRow += rowStep;
                currentColumn += columnStep;
                int stepTargetTileIndex = ToTileIndex(currentRow, currentColumn);
                _displayMoveQueue.Add(new DisplayMoveStepRuntime
                {
                    seat = seat,
                    sourceTileIndex = currentSourceTileIndex,
                    targetTileIndex = stepTargetTileIndex
                });

                if (stepTargetTileIndex == targetTileIndex)
                {
                    break;
                }

                currentSourceTileIndex = stepTargetTileIndex;
            }
        }

        private void MoveOccupant(int fromTileIndex, int toTileIndex)
        {
            _boardTileData[toTileIndex] = _boardTileData[fromTileIndex];
            _tileOccupantSeats[toTileIndex] = _tileOccupantSeats[fromTileIndex];
            _occupantCurrentHealth[toTileIndex] = _occupantCurrentHealth[fromTileIndex];
            _secureHoldTurnsByTile[toTileIndex] = 0;
            _silenceTurnsByTile[toTileIndex] = _silenceTurnsByTile[fromTileIndex];
            _spawnChargeTurnsByTile[toTileIndex] = _spawnChargeTurnsByTile[fromTileIndex];
            _interceptConsumedByTile[toTileIndex] = _interceptConsumedByTile[fromTileIndex];
            _tileLocked[toTileIndex] = _tileLocked[fromTileIndex];
            _attackTargetTileBySource[toTileIndex] = -1;
            _moveTargetTileBySource[toTileIndex] = -1;
            RemoveOccupantAtTile(fromTileIndex, false);
        }

        private void ResolveDisplayMoveStep(DisplayMoveStepRuntime step)
        {
            if (step == null
                || step.sourceTileIndex < 0
                || step.targetTileIndex < 0
                || step.sourceTileIndex >= _boardTileData.Length
                || step.targetTileIndex >= _boardTileData.Length)
            {
                return;
            }

            if (_boardTileData[step.sourceTileIndex] == null
                || !IsUnitCard(_boardTileData[step.sourceTileIndex])
                || _occupantCurrentHealth[step.sourceTileIndex] <= 0
                || _boardTileData[step.targetTileIndex] != null)
            {
                return;
            }

            _displayStageSeat = step.seat;
            _displayStageLabel = "MOVE";
            _selectedAttackerTileIndex = step.sourceTileIndex;
            _selectedBoardTileIndex = step.targetTileIndex;
            string moverName = _boardTileData[step.sourceTileIndex].cardName;
            BeginResolveMoveMotion(step.sourceTileIndex, step.targetTileIndex, "MOVE!");
            MoveOccupant(step.sourceTileIndex, step.targetTileIndex);
            ApplyBrambleWallLockIfNeeded(step.targetTileIndex, step.seat);
            int movementDistance = GetMoveDistanceBetweenTiles(step.sourceTileIndex, step.targetTileIndex);
            _displayNarrationText = movementDistance > 1
                ? $"{moverName} moved {movementDistance} tiles."
                : $"{moverName} moved 1 tile.";
            SanitizeBoardOccupancyState("display move resolve");
        }

        private void ApplyBrambleWallLockIfNeeded(int movedTileIndex, MatchSeat movingSeat)
        {
            if (movedTileIndex < 0 || movedTileIndex >= _boardTileData.Length)
            {
                return;
            }

            CardTemplate movedCard = _boardTileData[movedTileIndex];
            if (!IsUnitCard(movedCard))
            {
                return;
            }

            for (int tileIndex = 0; tileIndex < _boardTileData.Length; tileIndex++)
            {
                MatchSeat? ownerSeat = _tileOccupantSeats[tileIndex];
                if (!ownerSeat.HasValue
                    || ownerSeat.Value == movingSeat
                    || !IsInfrastructureCard(_boardTileData[tileIndex])
                    || _occupantCurrentHealth[tileIndex] <= 0
                    || !CardHasKeyword(_boardTileData[tileIndex], AbilityKeyword.Lock))
                {
                    continue;
                }

                if (!TryGetRowColumnFromTileIndex(tileIndex, out int gatehouseRow, out int gatehouseColumn))
                {
                    continue;
                }

                int frontRow = gatehouseRow + GetForwardRowStepForSeat(ownerSeat.Value);
                if (!IsInBounds(frontRow, gatehouseColumn))
                {
                    continue;
                }

                int brambleTileIndex = ToTileIndex(frontRow, gatehouseColumn);
                if (brambleTileIndex == movedTileIndex)
                {
                    _tileLocked[movedTileIndex] = true;
                    AddFloatingBoardText(movedTileIndex, "LOCK", "tile-floating-status");
                    return;
                }
            }
        }

        private void ResolveMovementPhaseForSeat(MatchSeat seat)
        {
            RefreshMovementPreviewState();
            List<int> orderedTiles = GetOrderedFriendlyUnitTilesForSeat(seat);
            HashSet<int> pendingSources = new HashSet<int>();
            for (int i = 0; i < orderedTiles.Count; i++)
            {
                int sourceTileIndex = orderedTiles[i];
                if (_previewResolvedMoveTargetBySource != null
                    && sourceTileIndex >= 0
                    && sourceTileIndex < _previewResolvedMoveTargetBySource.Length
                    && _previewResolvedMoveTargetBySource[sourceTileIndex] >= 0)
                {
                    pendingSources.Add(sourceTileIndex);
                }
            }

            bool movedAny;
            do
            {
                movedAny = false;
                for (int i = 0; i < orderedTiles.Count; i++)
                {
                    int sourceTileIndex = orderedTiles[i];
                    if (!pendingSources.Contains(sourceTileIndex))
                    {
                        continue;
                    }

                    if (_boardTileData[sourceTileIndex] == null
                        || !IsUnitCard(_boardTileData[sourceTileIndex])
                        || _occupantCurrentHealth[sourceTileIndex] <= 0)
                    {
                        pendingSources.Remove(sourceTileIndex);
                        continue;
                    }

                    if (_tileLocked[sourceTileIndex])
                    {
                        _tileLocked[sourceTileIndex] = false;
                        pendingSources.Remove(sourceTileIndex);
                        continue;
                    }

                    int resolvedTargetTileIndex = _previewResolvedMoveTargetBySource != null
                        && sourceTileIndex >= 0
                        && sourceTileIndex < _previewResolvedMoveTargetBySource.Length
                        ? _previewResolvedMoveTargetBySource[sourceTileIndex]
                        : -1;
                    if (resolvedTargetTileIndex < 0)
                    {
                        pendingSources.Remove(sourceTileIndex);
                        continue;
                    }

                    if (_boardTileData[resolvedTargetTileIndex] != null)
                    {
                        continue;
                    }

                    MoveOccupant(sourceTileIndex, resolvedTargetTileIndex);
                    ApplyBrambleWallLockIfNeeded(resolvedTargetTileIndex, seat);
                    pendingSources.Remove(sourceTileIndex);
                    movedAny = true;
                }
            }
            while (movedAny && pendingSources.Count > 0);
        }

        private void CleanupResolvedTurnStateForSeat(MatchSeat seat)
        {
            ParticipantRuntimeState state = GetRuntimeState(seat);
            RemoveTemporaryCommandCards(state);
            ClearAttackAssignmentsForSeat(seat);
            ClearMovementAssignmentsForSeat(seat);
        }

        private void ResolveCurrentTurnForActiveSeat()
        {
            ClearFloatingBoardTexts();
            ResolveAttackPhaseForSeat(_activeTurnSeat);
            ResolveMovementPhaseForSeat(_activeTurnSeat);
            SanitizeBoardOccupancyState("host turn resolve");
            CleanupResolvedTurnStateForSeat(_activeTurnSeat);
            _selectedAttackerTileIndex = -1;
            _selectedBoardTileIndex = -1;
            _displayNarrationText = string.Empty;
            SetAbilityPreviewCard(null);
        }

        private bool IsTileTargetedByFriendlyIntent(int tileIndex, MatchSeat seat)
        {
            for (int i = 0; i < _attackTargetTileBySource.Length; i++)
            {
                if (!_tileOccupantSeats[i].HasValue || _tileOccupantSeats[i].Value != seat)
                {
                    continue;
                }

                int displayedTarget = GetDisplayedAttackTargetTileForSource(i);
                if (displayedTarget == tileIndex)
                {
                    return true;
                }
            }

            return false;
        }

        private int GetDisplayedAttackTargetTileForSource(int sourceTileIndex)
        {
            if (sourceTileIndex < 0 || sourceTileIndex >= _attackTargetTileBySource.Length)
            {
                return -1;
            }

            if (_roundPhase == MatchRoundPhase.DisplayResolution
                && _displayResolutionMode == DisplayResolutionMode.Attack
                && sourceTileIndex == _selectedAttackerTileIndex
                && _selectedBoardTileIndex >= 0)
            {
                return _selectedBoardTileIndex;
            }

            if (_attackTargetTileBySource[sourceTileIndex] >= 0)
            {
                return _attackTargetTileBySource[sourceTileIndex];
            }

            CardTemplate attackerCard = _boardTileData[sourceTileIndex];
            MatchSeat? sourceSeat = _tileOccupantSeats[sourceTileIndex];
            if ((_roundPhase == MatchRoundPhase.CombatPlanning || _roundPhase == MatchRoundPhase.DisplayResolution)
                && IsUnitCard(attackerCard)
                && sourceSeat.HasValue
                && _occupantCurrentHealth[sourceTileIndex] > 0)
            {
                int attackRange = GetCardAttackRangeAtTile(sourceTileIndex);
                return _roundPhase == MatchRoundPhase.DisplayResolution
                    ? _displayAutoTargetTileBySource[sourceTileIndex]
                    : GetAutoAttackTargetTile(sourceTileIndex, sourceSeat.Value, attackRange);
            }

            if (_roundPhase == MatchRoundPhase.DisplayResolution)
            {
                return _displayAutoTargetTileBySource[sourceTileIndex];
            }

            return -1;
        }

        private MatchSeat? GetDisplayedCityAttackSeatForSource(int sourceTileIndex)
        {
            if (sourceTileIndex < 0 || sourceTileIndex >= _attackTargetTileBySource.Length)
            {
                return null;
            }

            CardTemplate attackerCard = _boardTileData[sourceTileIndex];
            MatchSeat? sourceSeat = _tileOccupantSeats[sourceTileIndex];
            if (!IsUnitCard(attackerCard) || !sourceSeat.HasValue || _occupantCurrentHealth[sourceTileIndex] <= 0)
            {
                return null;
            }

            if (HasManualCityAttackTarget(sourceTileIndex) && CanAttackCityDirectlyFromTile(sourceTileIndex, sourceSeat.Value))
            {
                return MatchPerspectiveUtility.GetOpposingSeat(sourceSeat.Value);
            }

            if (_attackTargetTileBySource[sourceTileIndex] >= 0)
            {
                return null;
            }

            if (GetAutoAttackTargetTile(sourceTileIndex, sourceSeat.Value, GetCardAttackRangeAtTile(sourceTileIndex)) >= 0)
            {
                return null;
            }

            return CanAttackCityDirectlyFromTile(sourceTileIndex, sourceSeat.Value)
                ? MatchPerspectiveUtility.GetOpposingSeat(sourceSeat.Value)
                : (MatchSeat?)null;
        }

        private bool ShouldRenderCurrentDisplayStepForSource(int sourceTileIndex)
        {
            if (_roundPhase != MatchRoundPhase.DisplayResolution)
            {
                return true;
            }

            return sourceTileIndex == _selectedAttackerTileIndex;
        }

        private bool IsTileWithinAttackRange(int sourceTileIndex, int targetTileIndex)
        {
            CardTemplate sourceCard = _boardTileData[sourceTileIndex];
            if (!IsUnitCard(sourceCard))
            {
                return false;
            }

            if (!TryGetRowColumnFromTileIndex(sourceTileIndex, out int sourceRow, out int sourceColumn)
                || !TryGetRowColumnFromTileIndex(targetTileIndex, out int targetRow, out int targetColumn))
            {
                return false;
            }

            int rowDelta = Mathf.Abs(targetRow - sourceRow);
            int columnDelta = Mathf.Abs(targetColumn - sourceColumn);
            return Mathf.Max(rowDelta, columnDelta) <= GetCardAttackRangeAtTile(sourceTileIndex)
                && (rowDelta > 0 || columnDelta > 0);
        }

        private bool ShouldShowAttackRangeMarkerForTile(int tileIndex, out bool isValidTarget)
        {
            isValidTarget = false;
            if (_roundPhase != MatchRoundPhase.CombatPlanning || _selectedAttackerTileIndex < 0 || tileIndex == _selectedAttackerTileIndex)
            {
                return false;
            }

            if (!IsTileWithinAttackRange(_selectedAttackerTileIndex, tileIndex))
            {
                return false;
            }

            isValidTarget = CanSourceUnitTargetTile(_selectedAttackerTileIndex, tileIndex);
            return true;
        }

        private bool ShouldShowInvalidAttackMarkerOnTile(int tileIndex)
        {
            if (_roundPhase != MatchRoundPhase.CombatPlanning || _selectedAttackerTileIndex < 0 || tileIndex == _selectedAttackerTileIndex)
            {
                return false;
            }

            if (!IsTileWithinAttackRange(_selectedAttackerTileIndex, tileIndex))
            {
                return false;
            }

            return !CanSourceUnitTargetTile(_selectedAttackerTileIndex, tileIndex)
                && _boardTileData[tileIndex] == null
                && _tileAreaKinds[tileIndex] != TileAreaKind.Base;
        }

        private bool ShouldShowSelectedHandTargetForTile(int tileIndex)
        {
            if (_roundPhase == MatchRoundPhase.CombatPlanning
                && HasSelectedWarShopOption()
                && _activeTurnSeat == _localSeat)
            {
                return CanApplyWarShopOptionToTile(GetSelectedWarShopOption(), tileIndex, _activeTurnSeat);
            }

            if (_roundPhase != MatchRoundPhase.DeployPlanning
                || _highlightedCardIndex < 0
                || _highlightedCardIndex >= cardsInHand.Count
                || _activeTurnSeat != _localSeat)
            {
                return false;
            }

            CardTemplate selectedCard = cardsInHand[_highlightedCardIndex];
            if (selectedCard == null)
            {
                return false;
            }

            if (IsLockCommandCard(selectedCard))
            {
                return CanTileReceiveLock(tileIndex, _localSeat);
            }

            if (IsBoardDeployableCard(selectedCard))
            {
                return CanDeployCardToTile(tileIndex, _activeTurnSeat);
            }

            if (selectedCard.cardType == CardType.Ordinance)
            {
                return CanApplyOrdinanceToTile(selectedCard, tileIndex, _activeTurnSeat);
            }

            if (selectedCard.cardType == CardType.Item)
            {
                return CanApplyItemToTile(selectedCard, tileIndex, _activeTurnSeat);
            }

            return false;
        }

        private string GetInvalidMoveReason(int sourceTileIndex, int targetTileIndex, MatchSeat seat)
        {
            CardTemplate sourceCard = sourceTileIndex >= 0 && sourceTileIndex < _boardTileData.Length
                ? _boardTileData[sourceTileIndex]
                : null;
            string sourceName = sourceCard != null ? sourceCard.cardName : "Unit";

            if (!IsUnitCard(sourceCard))
            {
                return "Only units can move.";
            }

            if (targetTileIndex < 0 || targetTileIndex >= _boardTileData.Length)
            {
                return $"{sourceName} cannot move off the board.";
            }

            if (_tileLocked[sourceTileIndex] || CardHasKeyword(sourceCard, AbilityKeyword.Lock))
            {
                return $"{sourceName} is locked and cannot move this turn.";
            }

            if (_boardTileData[targetTileIndex] != null)
            {
                return $"{sourceName} cannot move there because the tile is occupied.";
            }

            if (_tileAreaKinds[targetTileIndex] == TileAreaKind.Base)
            {
                MatchSeat? baseSeat = GetSeatFromTileOwner(_tileOwners[targetTileIndex]);
                if (baseSeat.HasValue && baseSeat.Value != seat && _tileCurrentHealth[targetTileIndex] > 0)
                {
                    return $"{sourceName} cannot enter an enemy base tile until it is destroyed.";
                }
            }

            if (!IsTileWithinMoveRange(sourceTileIndex, targetTileIndex))
            {
                return $"{sourceName} cannot move that far this turn.";
            }

            if (!CardHasKeywordAtTile(sourceTileIndex, AbilityKeyword.Maneuver))
            {
                return $"{sourceName} can only move forward unless Maneuver changes its movement.";
            }

            return $"{sourceName} cannot move to that tile.";
        }

        private bool ShouldShowDeployMoveMarkerForTile(int tileIndex, out bool isAssignedTarget, out bool isExtendedTarget)
        {
            isAssignedTarget = false;
            isExtendedTarget = false;

            if (_roundPhase != MatchRoundPhase.DeployPlanning
                || _selectedAttackerTileIndex < 0
                || tileIndex == _selectedAttackerTileIndex
                || _selectedAttackerTileIndex >= _boardTileData.Length)
            {
                return false;
            }

            MatchSeat? sourceSeat = _tileOccupantSeats[_selectedAttackerTileIndex];
            if (!sourceSeat.HasValue
                || _activeTurnSeat != _localSeat
                || sourceSeat.Value != _localSeat
                || !CanSourceUnitMoveToTile(_selectedAttackerTileIndex, tileIndex, sourceSeat.Value))
            {
                return false;
            }

            int defaultTargetTileIndex = GetDefaultMoveTargetTile(_selectedAttackerTileIndex, sourceSeat.Value);
            int plannedTargetTileIndex = GetPlannedMoveTargetTile(_selectedAttackerTileIndex, sourceSeat.Value);
            isAssignedTarget = tileIndex == plannedTargetTileIndex;
            isExtendedTarget = tileIndex != defaultTargetTileIndex;
            return true;
        }

        private bool IsPreviewStruggleTile(int tileIndex)
        {
            if (_roundPhase != MatchRoundPhase.DeployPlanning)
            {
                return false;
            }

            RefreshMovementPreviewState();
            for (int sourceTileIndex = 0; sourceTileIndex < _previewMoveTargetTileBySource.Length; sourceTileIndex++)
            {
                if (_previewMoveTargetTileBySource[sourceTileIndex] == tileIndex
                    && _previewMoveTargetContestedBySource[sourceTileIndex])
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetPreviewStruggleOutcome(int tileIndex, out int winnerSourceTileIndex, out int loserSourceTileIndex)
        {
            winnerSourceTileIndex = -1;
            loserSourceTileIndex = -1;

            if (_roundPhase != MatchRoundPhase.DeployPlanning)
            {
                return false;
            }

            RefreshMovementPreviewState();
            List<int> contenders = new List<int>();
            for (int sourceTileIndex = 0; sourceTileIndex < _previewMoveTargetTileBySource.Length; sourceTileIndex++)
            {
                if (_previewMoveTargetTileBySource[sourceTileIndex] == tileIndex
                    && _previewMoveTargetContestedBySource[sourceTileIndex])
                {
                    contenders.Add(sourceTileIndex);
                }
            }

            if (contenders.Count < 2)
            {
                return false;
            }

            contenders.Sort(CompareStruggleCandidates);
            winnerSourceTileIndex = contenders[0];
            loserSourceTileIndex = contenders[1];
            return true;
        }

        private string GetStrugglePreviewText(int tileIndex)
        {
            if (!TryGetPreviewStruggleOutcome(tileIndex, out int winnerSourceTileIndex, out int loserSourceTileIndex))
            {
                return "No struggle preview available.";
            }

            string winnerName = _boardTileData[winnerSourceTileIndex] != null ? _boardTileData[winnerSourceTileIndex].cardName : "Winner";
            string loserName = _boardTileData[loserSourceTileIndex] != null ? _boardTileData[loserSourceTileIndex].cardName : "Loser";
            int damage = _boardTileData[winnerSourceTileIndex] != null ? Mathf.Max(0, _boardTileData[winnerSourceTileIndex].attack) : 0;
            int projectedHealth = loserSourceTileIndex >= 0 && loserSourceTileIndex < _previewMovementOccupantHealth.Length
                ? Mathf.Max(0, _previewMovementOccupantHealth[loserSourceTileIndex])
                : 0;
            return $"{winnerName} wins the struggle here. {winnerName} deals {damage}AT to {loserName}. {loserName} falls to {projectedHealth}HP unless movement changes.";
        }

        private string GetIntentBadgeTextForTile(int tileIndex)
        {
            if (_roundPhase == MatchRoundPhase.DeployPlanning
                && _boardTileData[tileIndex] == null
                && IsPreviewStruggleTile(tileIndex))
            {
                return "STRUGGLE";
            }

            if (_roundPhase == MatchRoundPhase.DeployPlanning
                && _boardTileData[tileIndex] != null
                && IsUnitCard(_boardTileData[tileIndex])
                && _occupantCurrentHealth[tileIndex] > 0
                && _tileOccupantSeats[tileIndex].HasValue
                && GetPlannedMoveTargetTile(tileIndex, _tileOccupantSeats[tileIndex].Value) < 0)
            {
                return "NO MOVE";
            }

            if (_roundPhase != MatchRoundPhase.CombatPlanning || _boardTileData[tileIndex] == null || !IsUnitCard(_boardTileData[tileIndex]))
            {
                return string.Empty;
            }

            MatchSeat? sourceSeat = _tileOccupantSeats[tileIndex];
            if (!sourceSeat.HasValue || _occupantCurrentHealth[tileIndex] <= 0)
            {
                return string.Empty;
            }

            if (HasManualCityAttackTarget(tileIndex) && CanAttackCityDirectlyFromTile(tileIndex, sourceSeat.Value))
            {
                return "Attack City";
            }

            if (HasManualCityAttackTarget(tileIndex))
            {
                return "MISS";
            }

            CardTemplate attackerCard = _boardTileData[tileIndex];
            if (_attackTargetTileBySource[tileIndex] >= 0)
            {
                return IsCurrentAttackTargetValid(tileIndex, _attackTargetTileBySource[tileIndex], sourceSeat.Value, attackerCard)
                    ? string.Empty
                    : "MISS";
            }

            int autoTargetTileIndex = GetAutoAttackTargetTile(tileIndex, sourceSeat.Value, GetCardAttackRangeAtTile(tileIndex));
            if (autoTargetTileIndex >= 0 && IsSiegeableEnemyBaseTileForSeat(autoTargetTileIndex, sourceSeat.Value))
            {
                return "SIEGE";
            }

            if (autoTargetTileIndex >= 0)
            {
                return string.Empty;
            }

            return CanAttackCityDirectlyFromTile(tileIndex, sourceSeat.Value) ? "Attack City" : "MISS";
        }

        private int GetMoveDistanceBetweenTiles(int sourceTileIndex, int targetTileIndex)
        {
            if (!TryGetRowColumnFromTileIndex(sourceTileIndex, out int sourceRow, out int sourceColumn)
                || !TryGetRowColumnFromTileIndex(targetTileIndex, out int targetRow, out int targetColumn))
            {
                return 1;
            }

            int rowDelta = Mathf.Abs(targetRow - sourceRow);
            int columnDelta = Mathf.Abs(targetColumn - sourceColumn);
            return Mathf.Max(1, Mathf.Max(rowDelta, columnDelta));
        }

        private bool IsTileWithinMoveRange(int sourceTileIndex, int targetTileIndex)
        {
            return GetMoveDistanceBetweenTiles(sourceTileIndex, targetTileIndex) <= GetCardMovementRangeAtTile(sourceTileIndex);
        }

        private void RefreshMovementPreviewState()
        {
            if (_previewMoveTargetTileBySource == null || _previewMoveTargetTileBySource.Length != _boardTileData.Length)
            {
                _previewMoveTargetTileBySource = new int[_boardTileData.Length];
            }

            if (_previewResolvedMoveTargetBySource == null || _previewResolvedMoveTargetBySource.Length != _boardTileData.Length)
            {
                _previewResolvedMoveTargetBySource = new int[_boardTileData.Length];
            }

            if (_previewMovementOccupantHealth == null || _previewMovementOccupantHealth.Length != _boardTileData.Length)
            {
                _previewMovementOccupantHealth = new int[_boardTileData.Length];
            }

            if (_previewMoveTargetContestedBySource == null || _previewMoveTargetContestedBySource.Length != _boardTileData.Length)
            {
                _previewMoveTargetContestedBySource = new bool[_boardTileData.Length];
            }

            _isRefreshingMovementPreview = true;
            Dictionary<int, List<int>> contendersByTarget = new Dictionary<int, List<int>>();
            int[] previewInterceptConsumed = new int[_boardTileData.Length];
            Array.Copy(_interceptConsumedByTile, previewInterceptConsumed, Mathf.Min(_interceptConsumedByTile.Length, previewInterceptConsumed.Length));
            for (int i = 0; i < _boardTileData.Length; i++)
            {
                _previewMoveTargetTileBySource[i] = -1;
                _previewResolvedMoveTargetBySource[i] = -1;
                _previewMoveTargetContestedBySource[i] = false;
                _previewMovementOccupantHealth[i] = _occupantCurrentHealth[i];
            }

            MatchSeat[] seats = { MatchSeat.SeatOne, MatchSeat.SeatTwo };
            for (int seatIndex = 0; seatIndex < seats.Length; seatIndex++)
            {
                MatchSeat seat = seats[seatIndex];
                List<int> orderedTiles = GetOrderedFriendlyUnitTilesForSeat(seat);
                for (int i = 0; i < orderedTiles.Count; i++)
                {
                    int sourceTileIndex = orderedTiles[i];
                    var visitingTiles = new HashSet<int> { sourceTileIndex };
                    int targetTileIndex = GetDesiredMoveTargetTileForPreview(sourceTileIndex, seat, visitingTiles);
                    if (targetTileIndex < 0)
                    {
                        continue;
                    }

                    _previewMoveTargetTileBySource[sourceTileIndex] = targetTileIndex;
                    if (!contendersByTarget.TryGetValue(targetTileIndex, out List<int> contenders))
                    {
                        contenders = new List<int>();
                        contendersByTarget[targetTileIndex] = contenders;
                    }

                    contenders.Add(sourceTileIndex);
                }
            }

            foreach (KeyValuePair<int, List<int>> pair in contendersByTarget)
            {
                List<int> contenders = pair.Value;
                if (contenders.Count == 1)
                {
                    _previewResolvedMoveTargetBySource[contenders[0]] = pair.Key;
                    continue;
                }

                if (contenders.Count < 2)
                {
                    continue;
                }

                contenders.Sort(CompareStruggleCandidates);
                int winnerSourceTileIndex = contenders[0];
                int loserSourceTileIndex = contenders[1];
                _previewResolvedMoveTargetBySource[winnerSourceTileIndex] = pair.Key;

                bool opposingSeats = _tileOccupantSeats[winnerSourceTileIndex].HasValue
                    && _tileOccupantSeats[loserSourceTileIndex].HasValue
                    && _tileOccupantSeats[winnerSourceTileIndex].Value != _tileOccupantSeats[loserSourceTileIndex].Value;
                if (!opposingSeats)
                {
                    continue;
                }

                for (int i = 0; i < contenders.Count; i++)
                {
                    _previewMoveTargetContestedBySource[contenders[i]] = true;
                }

                CardTemplate winnerCard = _boardTileData[winnerSourceTileIndex];
                if (winnerCard != null && loserSourceTileIndex >= 0 && loserSourceTileIndex < _previewMovementOccupantHealth.Length)
                {
                    int interceptLimit = Mathf.Max(1, GetEffectiveKeywordValueAtTile(loserSourceTileIndex, AbilityKeyword.Intercept));
                    if (CardHasKeywordAtTile(loserSourceTileIndex, AbilityKeyword.Intercept) && previewInterceptConsumed[loserSourceTileIndex] < interceptLimit)
                    {
                        previewInterceptConsumed[loserSourceTileIndex]++;
                    }
                    else
                    {
                        _previewMovementOccupantHealth[loserSourceTileIndex] = Mathf.Max(0, _previewMovementOccupantHealth[loserSourceTileIndex] - Mathf.Max(0, winnerCard.attack));
                    }
                }
            }

            _isRefreshingMovementPreview = false;
        }

        private int GetRenderedOccupantHealth(int tileIndex)
        {
            if (_roundPhase == MatchRoundPhase.DeployPlanning
                && _previewMovementOccupantHealth != null
                && tileIndex >= 0
                && tileIndex < _previewMovementOccupantHealth.Length)
            {
                return _previewMovementOccupantHealth[tileIndex];
            }

            if (_roundPhase == MatchRoundPhase.CombatPlanning
                && _previewOccupantHealth != null
                && tileIndex >= 0
                && tileIndex < _previewOccupantHealth.Length)
            {
                return _previewOccupantHealth[tileIndex];
            }

            return _occupantCurrentHealth[tileIndex];
        }

        private int GetRenderedTileHealth(int tileIndex)
        {
            if (_roundPhase == MatchRoundPhase.CombatPlanning
                && _previewTileHealth != null
                && tileIndex >= 0
                && tileIndex < _previewTileHealth.Length)
            {
                return _previewTileHealth[tileIndex];
            }

            return _tileCurrentHealth[tileIndex];
        }

        private string FormatStatValueMarkup(int displayedValue, int baselineValue, string suffix, string neutralColor, string positiveColor, string negativeColor)
        {
            string color = neutralColor;
            if (displayedValue > baselineValue)
            {
                color = positiveColor;
            }
            else if (displayedValue < baselineValue)
            {
                color = negativeColor;
            }

            return $"<color={color}><b>{displayedValue}</b><b>{suffix}</b></color>";
        }

        private string GetOccupantHealthMarkup(int tileIndex)
        {
            CardTemplate cardData = tileIndex >= 0 && tileIndex < _boardTileData.Length ? _boardTileData[tileIndex] : null;
            int currentHealth = Mathf.Max(0, _occupantCurrentHealth[tileIndex]);
            int printedHealth = cardData != null ? Mathf.Max(0, cardData.health) : currentHealth;
            int displayedHealth = GetRenderedOccupantHealth(tileIndex);
            int baselineHealth = displayedHealth != currentHealth
                ? currentHealth
                : currentHealth > printedHealth ? printedHealth : currentHealth;
            return FormatStatValueMarkup(displayedHealth, baselineHealth, "HP", "#EE3333", "#16A34A", "#DC2626");
        }

        private string GetTileHealthMarkup(int tileIndex)
        {
            int currentHealth = Mathf.Max(0, _tileCurrentHealth[tileIndex]);
            int displayedHealth = GetRenderedTileHealth(tileIndex);
            int baselineHealth = currentHealth;
            return FormatStatValueMarkup(displayedHealth, baselineHealth, "HP", "#EE3333", "#16A34A", "#DC2626");
        }

        private string GetCombinedBaseOccupantHealthMarkup(int tileIndex)
        {
            int displayedTileHealth = GetRenderedTileHealth(tileIndex);
            int displayedOccupantHealth = GetRenderedOccupantHealth(tileIndex);

            string tileColor = displayedTileHealth < Mathf.Max(0, _tileCurrentHealth[tileIndex]) ? "#DC2626" : "#EE3333";
            string occupantColor = displayedOccupantHealth < Mathf.Max(0, _occupantCurrentHealth[tileIndex]) ? "#DC2626" : "#2563EB";

            return $"<color={tileColor}><b>{displayedTileHealth}</b></color>+<color={occupantColor}><b>{displayedOccupantHealth}</b></color><b>HP</b>";
        }

        private string GetAttackMarkup(int tileIndex)
        {
            CardTemplate cardData = tileIndex >= 0 && tileIndex < _boardTileData.Length ? _boardTileData[tileIndex] : null;
            int baselineAttack = cardData != null ? Mathf.Max(0, cardData.attack) : 0;
            bool includeAttackPhaseBonuses = _roundPhase == MatchRoundPhase.CombatPlanning
                || (_roundPhase == MatchRoundPhase.DisplayResolution && _displayResolutionMode == DisplayResolutionMode.Attack);
            int displayedAttack = GetRenderedAttackValue(tileIndex, includeAttackPhaseBonuses);
            string neutralColor = (_roundPhase == MatchRoundPhase.DeployPlanning && _previewMoveTargetContestedBySource != null
                && tileIndex >= 0 && tileIndex < _previewMoveTargetContestedBySource.Length
                && _previewMoveTargetContestedBySource[tileIndex])
                ? "#C2410C"
                : "#333333";
            return FormatStatValueMarkup(displayedAttack, baselineAttack, "AT", neutralColor, "#2563EB", "#C2410C");
        }

        private int GetRenderedAttackValue(int tileIndex, bool includeAttackPhaseBonuses)
        {
            int effectiveAttack = GetCurrentAttackValueForTile(tileIndex, includeAttackPhaseBonuses);
            if (_roundPhase == MatchRoundPhase.CombatPlanning
                && _previewAttackDamageBySource != null
                && tileIndex >= 0
                && tileIndex < _previewAttackDamageBySource.Length
                && _previewAttackDamageBySource[tileIndex] >= 0)
            {
                int previewAttack = _previewAttackDamageBySource[tileIndex];
                return previewAttack > effectiveAttack ? previewAttack : effectiveAttack;
            }

            return effectiveAttack;
        }

        private void RefreshCombatPreviewState()
        {
            if (_previewOccupantHealth == null || _previewOccupantHealth.Length != _occupantCurrentHealth.Length)
            {
                _previewOccupantHealth = new int[_occupantCurrentHealth.Length];
            }

            if (_previewTileHealth == null || _previewTileHealth.Length != _tileCurrentHealth.Length)
            {
                _previewTileHealth = new int[_tileCurrentHealth.Length];
            }

            if (_previewAttackDamageBySource == null || _previewAttackDamageBySource.Length != _occupantCurrentHealth.Length)
            {
                _previewAttackDamageBySource = new int[_occupantCurrentHealth.Length];
            }

            _occupantCurrentHealth.CopyTo(_previewOccupantHealth, 0);
            _tileCurrentHealth.CopyTo(_previewTileHealth, 0);
            for (int i = 0; i < _previewAttackDamageBySource.Length; i++)
            {
                _previewAttackDamageBySource[i] = -1;
            }
            if (_previewInterceptConsumedByTile == null || _previewInterceptConsumedByTile.Length != _interceptConsumedByTile.Length)
            {
                _previewInterceptConsumedByTile = new int[_interceptConsumedByTile.Length];
            }

            Array.Copy(_interceptConsumedByTile, _previewInterceptConsumedByTile, _interceptConsumedByTile.Length);
            _previewSeatOneCityHealth = _seatOneState != null ? _seatOneState.health : 0;
            _previewSeatTwoCityHealth = _seatTwoState != null ? _seatTwoState.health : 0;

            if (_roundPhase != MatchRoundPhase.CombatPlanning)
            {
                return;
            }

            SimulatePreviewAttackPhaseForSeat(_roundInitiativeSeat);
            SimulatePreviewAttackPhaseForSeat(MatchPerspectiveUtility.GetOpposingSeat(_roundInitiativeSeat));
        }

        private void SimulatePreviewAttackPhaseForSeat(MatchSeat seat)
        {
            List<int> actingTiles = GetOrderedFriendlyUnitTilesForSeat(seat);
            for (int i = 0; i < actingTiles.Count; i++)
            {
                int sourceTileIndex = actingTiles[i];
                CardTemplate attackerCard = _boardTileData[sourceTileIndex];
                if (!IsUnitCard(attackerCard) || _previewOccupantHealth[sourceTileIndex] <= 0)
                {
                    if (sourceTileIndex >= 0 && sourceTileIndex < _previewAttackDamageBySource.Length)
                    {
                        _previewAttackDamageBySource[sourceTileIndex] = 0;
                    }
                    continue;
                }

                int targetTileIndex = ResolvePreviewTargetTile(sourceTileIndex, seat, attackerCard);
                if (targetTileIndex < 0)
                {
                    if (CanAttackCityDirectlyFromTile(sourceTileIndex, seat))
                    {
                        int cityDamage = GetCurrentAttackValueForTile(sourceTileIndex, true);
                        _previewAttackDamageBySource[sourceTileIndex] = cityDamage;
                        ApplyPreviewCityDamage(MatchPerspectiveUtility.GetOpposingSeat(seat), cityDamage);
                        continue;
                    }

                    _previewAttackDamageBySource[sourceTileIndex] = 0;
                    continue;
                }

                int previewDamage = GetAttackDamageAgainstTarget(sourceTileIndex, seat, targetTileIndex, GetCurrentAttackValueForTile(sourceTileIndex, true));
                _previewAttackDamageBySource[sourceTileIndex] = previewDamage;
                if (IsSiegeableEnemyBaseTileForSeat(targetTileIndex, seat))
                {
                    ApplyPreviewBaseTileDamage(targetTileIndex, previewDamage);
                    continue;
                }

                ApplyPreviewDamage(sourceTileIndex, targetTileIndex, seat, previewDamage);
            }
        }

        private void ApplyPreviewCityDamage(MatchSeat seat, int damage)
        {
            if (damage <= 0)
            {
                return;
            }

            if (seat == MatchSeat.SeatOne)
            {
                _previewSeatOneCityHealth = Mathf.Max(0, _previewSeatOneCityHealth - damage);
                return;
            }

            _previewSeatTwoCityHealth = Mathf.Max(0, _previewSeatTwoCityHealth - damage);
        }

        private int ResolvePreviewTargetTile(int sourceTileIndex, MatchSeat seat, CardTemplate attackerCard)
        {
            if (attackerCard == null)
            {
                return -1;
            }

            if (TryGetForcedProvokeTargetTile(sourceTileIndex, seat, out int forcedTargetTileIndex))
            {
                return forcedTargetTileIndex;
            }

            int manualTarget = _attackTargetTileBySource[sourceTileIndex];
            if (manualTarget >= 0 && IsPreviewTargetValid(sourceTileIndex, manualTarget, seat, attackerCard))
            {
                return manualTarget;
            }

            return GetPreviewAutoAttackTargetTile(sourceTileIndex, seat, GetCardAttackRangeAtTile(sourceTileIndex));
        }

        private bool IsPreviewTargetValid(int sourceTileIndex, int targetTileIndex, MatchSeat seat, CardTemplate attackerCard)
        {
            if (attackerCard == null || !IsTileWithinAttackRange(sourceTileIndex, targetTileIndex))
            {
                return false;
            }

            if (IsSiegeableEnemyBaseTileForSeat(targetTileIndex, seat) && _previewTileHealth[targetTileIndex] > 0)
            {
                return true;
            }

            if (_boardTileData[targetTileIndex] != null && _previewOccupantHealth[targetTileIndex] > 0)
            {
                MatchSeat? targetSeat = _tileOccupantSeats[targetTileIndex];
                return targetSeat.HasValue && targetSeat.Value != seat;
            }

            MatchSeat? baseSeat = GetSeatFromTileOwner(_tileOwners[targetTileIndex]);
            return _tileAreaKinds[targetTileIndex] == TileAreaKind.Base
                && _previewTileHealth[targetTileIndex] > 0
                && baseSeat.HasValue
                && baseSeat.Value != seat;
        }

        private int GetPreviewAutoAttackTargetTile(int sourceTileIndex, MatchSeat seat, int range)
        {
            if (TryGetForcedProvokeTargetTile(sourceTileIndex, seat, out int forcedTargetTileIndex))
            {
                return forcedTargetTileIndex;
            }

            if (!TryGetRowColumnFromTileIndex(sourceTileIndex, out int sourceRow, out int sourceColumn))
            {
                return -1;
            }

            int rowStep = GetForwardRowStepForSeat(seat);
            for (int distance = 1; distance <= Mathf.Max(1, range); distance++)
            {
                int targetRow = sourceRow + (rowStep * distance);
                if (!IsInBounds(targetRow, sourceColumn))
                {
                    break;
                }

                int targetTileIndex = ToTileIndex(targetRow, sourceColumn);
                if (_boardTileData[targetTileIndex] != null && _previewOccupantHealth[targetTileIndex] > 0)
                {
                    MatchSeat? targetSeat = _tileOccupantSeats[targetTileIndex];
                    if (targetSeat.HasValue && targetSeat.Value != seat)
                    {
                        return targetTileIndex;
                    }

                    break;
                }

                if (IsSiegeableEnemyBaseTileForSeat(targetTileIndex, seat) && _previewTileHealth[targetTileIndex] > 0)
                {
                    return targetTileIndex;
                }
            }

            return -1;
        }

        private void ApplyPreviewDamage(int sourceTileIndex, int targetTileIndex, MatchSeat attackerSeat, int damage)
        {
            if (damage <= 0)
            {
                return;
            }

            if (_boardTileData[targetTileIndex] != null && _previewOccupantHealth[targetTileIndex] > 0)
            {
                CardTemplate targetCard = _boardTileData[targetTileIndex];
                if (IsInfrastructureCard(targetCard))
                {
                    ApplyPreviewBaseTileDamage(targetTileIndex, damage);
                    _previewOccupantHealth[targetTileIndex] = _previewTileHealth[targetTileIndex];
                    return;
                }

                if (TryConsumeIntercept(targetTileIndex, true))
                {
                    return;
                }

                int startingHealth = Mathf.Max(0, _previewOccupantHealth[targetTileIndex]);
                _previewOccupantHealth[targetTileIndex] = Mathf.Max(0, startingHealth - damage);
                int overflowDamage = Mathf.Max(0, damage - startingHealth);
                if (_previewOccupantHealth[targetTileIndex] == 0 && overflowDamage > 0 && CanApplyBreachFromAttack(_boardTileData[sourceTileIndex], targetCard))
                {
                    ApplyPreviewBreachOverflow(sourceTileIndex, targetTileIndex, attackerSeat, overflowDamage);
                }

                return;
            }

            if (_previewTileHealth[targetTileIndex] > 0)
            {
                _previewTileHealth[targetTileIndex] = Mathf.Max(0, _previewTileHealth[targetTileIndex] - damage);
            }
        }

        private void ApplyPreviewBreachOverflow(int sourceTileIndex, int defeatedTargetTileIndex, MatchSeat attackerSeat, int overflowDamage)
        {
            if (overflowDamage <= 0)
            {
                return;
            }

            int carryTargetTileIndex = GetBreachCarryTargetTileIndex(sourceTileIndex, defeatedTargetTileIndex);
            if (carryTargetTileIndex < 0)
            {
                return;
            }

            CardTemplate carryTargetCard = _boardTileData[carryTargetTileIndex];
            if (IsInfrastructureCard(carryTargetCard) && _previewOccupantHealth[carryTargetTileIndex] > 0)
            {
                ApplyPreviewDamage(sourceTileIndex, carryTargetTileIndex, attackerSeat, GetAttackDamageAgainstTarget(sourceTileIndex, attackerSeat, carryTargetTileIndex, overflowDamage));
                return;
            }

            if (IsSiegeableEnemyBaseTileForSeat(carryTargetTileIndex, attackerSeat))
            {
                ApplyPreviewBaseTileDamage(carryTargetTileIndex, GetAttackDamageAgainstTarget(sourceTileIndex, attackerSeat, carryTargetTileIndex, overflowDamage));
            }
        }

        private void ApplyPreviewBaseTileDamage(int targetTileIndex, int damage)
        {
            if (damage <= 0
                || targetTileIndex < 0
                || targetTileIndex >= _previewTileHealth.Length
                || _tileAreaKinds[targetTileIndex] != TileAreaKind.Base
                || _previewTileHealth[targetTileIndex] <= 0)
            {
                return;
            }

            _previewTileHealth[targetTileIndex] = Mathf.Max(0, _previewTileHealth[targetTileIndex] - damage);
        }

        private bool CanSourceUnitTargetTile(int sourceTileIndex, int targetTileIndex)
        {
            CardTemplate sourceCard = _boardTileData[sourceTileIndex];
            MatchSeat? sourceSeat = _tileOccupantSeats[sourceTileIndex];
            if (!IsUnitCard(sourceCard) || !sourceSeat.HasValue)
            {
                return false;
            }

            if (TryGetForcedProvokeTargetTile(sourceTileIndex, sourceSeat.Value, out int forcedTargetTileIndex))
            {
                return targetTileIndex == forcedTargetTileIndex;
            }

            return IsAttackTargetableIgnoringProvoke(sourceTileIndex, targetTileIndex, sourceSeat.Value);
        }

        private bool TryGetForcedProvokeTargetTile(int sourceTileIndex, MatchSeat sourceSeat, out int targetTileIndex)
        {
            targetTileIndex = -1;
            if (sourceTileIndex < 0 || sourceTileIndex >= _boardTileData.Length)
            {
                return false;
            }

            CardTemplate sourceCard = _boardTileData[sourceTileIndex];
            if (!IsUnitCard(sourceCard)
                || !_tileOccupantSeats[sourceTileIndex].HasValue
                || _tileOccupantSeats[sourceTileIndex].Value != sourceSeat
                || _occupantCurrentHealth[sourceTileIndex] <= 0)
            {
                return false;
            }

            int bestDistance = int.MaxValue;
            for (int tileIndex = 0; tileIndex < _boardTileData.Length; tileIndex++)
            {
                CardTemplate targetCard = _boardTileData[tileIndex];
                if (targetCard == null
                    || !_tileOccupantSeats[tileIndex].HasValue
                    || _tileOccupantSeats[tileIndex].Value == sourceSeat
                    || _occupantCurrentHealth[tileIndex] <= 0
                    || !CardHasKeyword(targetCard, AbilityKeyword.Provoke)
                    || !IsAttackTargetableIgnoringProvoke(sourceTileIndex, tileIndex, sourceSeat))
                {
                    continue;
                }

                int distance = GetMoveDistanceBetweenTiles(sourceTileIndex, tileIndex);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    targetTileIndex = tileIndex;
                }
            }

            return targetTileIndex >= 0;
        }

        private bool AreTilesOrthogonallyAdjacent(int firstTileIndex, int secondTileIndex)
        {
            if (!TryGetRowColumnFromTileIndex(firstTileIndex, out int firstRow, out int firstColumn)
                || !TryGetRowColumnFromTileIndex(secondTileIndex, out int secondRow, out int secondColumn))
            {
                return false;
            }

            return Mathf.Abs(firstRow - secondRow) + Mathf.Abs(firstColumn - secondColumn) == 1;
        }

        private bool IsAttackTargetableIgnoringProvoke(int sourceTileIndex, int targetTileIndex, MatchSeat sourceSeat)
        {
            if (!TryGetRowColumnFromTileIndex(sourceTileIndex, out int sourceRow, out int sourceColumn)
                || !TryGetRowColumnFromTileIndex(targetTileIndex, out int targetRow, out int targetColumn))
            {
                return false;
            }

            int rowDelta = Mathf.Abs(targetRow - sourceRow);
            int columnDelta = Mathf.Abs(targetColumn - sourceColumn);
            bool inRange = Mathf.Max(rowDelta, columnDelta) <= GetCardAttackRangeAtTile(sourceTileIndex)
                && (rowDelta > 0 || columnDelta > 0);
            if (!inRange)
            {
                return false;
            }

            if (IsSiegeableEnemyBaseTileForSeat(targetTileIndex, sourceSeat))
            {
                return true;
            }

            if (_boardTileData[targetTileIndex] != null)
            {
                if (IsBelfryTokenCard(_boardTileData[targetTileIndex]))
                {
                    return false;
                }

                MatchSeat? targetSeat = _tileOccupantSeats[targetTileIndex];
                return targetSeat.HasValue && targetSeat.Value != sourceSeat;
            }

            MatchSeat? baseSeat = GetSeatFromTileOwner(_tileOwners[targetTileIndex]);
            return _tileAreaKinds[targetTileIndex] == TileAreaKind.Base
                && _tileCurrentHealth[targetTileIndex] > 0
                && baseSeat.HasValue
                && baseSeat.Value != sourceSeat;
        }

        private string GetInvalidAttackReason(int sourceTileIndex, int targetTileIndex)
        {
            CardTemplate sourceCard = sourceTileIndex >= 0 && sourceTileIndex < _boardTileData.Length
                ? _boardTileData[sourceTileIndex]
                : null;
            MatchSeat? sourceSeat = sourceTileIndex >= 0 && sourceTileIndex < _tileOccupantSeats.Length
                ? _tileOccupantSeats[sourceTileIndex]
                : null;
            string sourceName = sourceCard != null ? sourceCard.cardName : "Unit";

            if (!IsUnitCard(sourceCard) || !sourceSeat.HasValue)
            {
                return "Select one of your units before choosing an attack target.";
            }

            if (TryGetForcedProvokeTargetTile(sourceTileIndex, sourceSeat.Value, out int forcedTargetTileIndex)
                && forcedTargetTileIndex != targetTileIndex)
            {
                string forcedName = GetCombatTargetDisplayName(forcedTargetTileIndex, sourceSeat.Value);
                return $"{sourceName} must attack {forcedName} because {forcedName} has Provoke.";
            }

            if (targetTileIndex < 0 || targetTileIndex >= _boardTileData.Length)
            {
                return $"{sourceName} cannot attack off the board.";
            }

            if (!IsTileWithinAttackRange(sourceTileIndex, targetTileIndex))
            {
                return $"{sourceName} cannot attack that far.";
            }

            if (_boardTileData[targetTileIndex] != null)
            {
                MatchSeat? targetSeat = _tileOccupantSeats[targetTileIndex];
                if (targetSeat.HasValue && targetSeat.Value == sourceSeat.Value)
                {
                    return $"{sourceName} cannot attack a friendly card.";
                }
            }

            if (_boardTileData[targetTileIndex] == null)
            {
                if (IsLiveEnemyBaseTileForSeat(targetTileIndex, sourceSeat.Value) && !IsSiegeableEnemyBaseTileForSeat(targetTileIndex, sourceSeat.Value))
                {
                    return $"{sourceName} must attack the card on that base tile before the base tile.";
                }

                if (!IsLiveEnemyBaseTileForSeat(targetTileIndex, sourceSeat.Value))
                {
                    return $"{sourceName} has no valid target on that tile.";
                }
            }

            return $"{sourceName} cannot attack that target.";
        }

        private bool TryApplyOrdinanceCardToTile(CardTemplate selectedCard, int tileIndex, int handIndex, MatchSeat seat)
        {
            if (_roundPhase != MatchRoundPhase.DeployPlanning
                || selectedCard == null
                || selectedCard.cardType != CardType.Ordinance
                || IsLockCommandCard(selectedCard)
                || handIndex < 0
                || handIndex >= cardsInHand.Count
                || !CanApplyOrdinanceToTile(selectedCard, tileIndex, seat))
            {
                return false;
            }

            if (!TrySpendTreasuryForCard(selectedCard, seat))
            {
                ShowInvalidActionAndClearSelection("Not enough treasury.");
                return true;
            }

            CardTemplate targetCard = _boardTileData[tileIndex];
            AbilityEffectData appliedEffect = GetPrimaryKeywordEffect(selectedCard);
            if (!TryAddOrStackKeyword(targetCard, appliedEffect))
            {
                return false;
            }

            cardsInHand.RemoveAt(handIndex);
            DiscardCardForSeat(seat, selectedCard);
            _highlightedCardIndex = -1;
            _selectedBoardTileIndex = tileIndex;
            _selectedAttackerTileIndex = -1;
            AddFloatingBoardText(tileIndex, GetKeywordBadgeText(appliedEffect), "tile-floating-status");
            ShowAwarenessMessage($"<b>{GetSeatDisplayName(seat)} added {selectedCard.cardName} to {targetCard.cardName}.</b>", 2.4f);
            SyncVisibleStateFromPerspective();
            return true;
        }

        private bool CanApplyOrdinanceToTile(CardTemplate ordinanceCard, int tileIndex, MatchSeat seat)
        {
            if (ordinanceCard == null
                || ordinanceCard.cardType != CardType.Ordinance
                || IsLockCommandCard(ordinanceCard)
                || tileIndex < 0
                || tileIndex >= _boardTileData.Length
                || !_tileOccupantSeats[tileIndex].HasValue
                || _tileOccupantSeats[tileIndex].Value != seat
                || _occupantCurrentHealth[tileIndex] <= 0)
            {
                return false;
            }

            CardTemplate targetCard = _boardTileData[tileIndex];
            AbilityEffectData appliedEffect = GetPrimaryKeywordEffect(ordinanceCard);
            if (targetCard == null
                || IsSystemRuntimeCard(targetCard)
                || appliedEffect == null
                || appliedEffect.keyword == AbilityKeyword.None)
            {
                return false;
            }

            if (IsInfrastructureCard(targetCard))
            {
                return CardHasKeyword(targetCard, appliedEffect.keyword) && IsStackableKeyword(appliedEffect.keyword);
            }

            if (!IsUnitCard(targetCard))
            {
                return false;
            }

            if (CardHasKeyword(targetCard, appliedEffect.keyword))
            {
                return IsStackableKeyword(appliedEffect.keyword);
            }

            return !CardHasAnyKeyword(targetCard);
        }

        private string GetInvalidOrdinanceTargetReason(CardTemplate ordinanceCard, int tileIndex, MatchSeat seat)
        {
            string cardName = ordinanceCard != null ? ordinanceCard.cardName : "Order";
            AbilityEffectData appliedEffect = GetPrimaryKeywordEffect(ordinanceCard);
            string abilityName = appliedEffect != null && appliedEffect.keyword != AbilityKeyword.None
                ? appliedEffect.keyword.ToString()
                : "ability";

            if (tileIndex < 0 || tileIndex >= _boardTileData.Length)
            {
                return $"{cardName} needs a valid board target.";
            }

            if (!_tileOccupantSeats[tileIndex].HasValue || _boardTileData[tileIndex] == null)
            {
                return $"{cardName} must target one of your cards.";
            }

            if (_tileOccupantSeats[tileIndex].Value != seat)
            {
                return $"{cardName} can only target friendly cards.";
            }

            if (_occupantCurrentHealth[tileIndex] <= 0)
            {
                return $"{cardName} cannot target a destroyed card.";
            }

            CardTemplate targetCard = _boardTileData[tileIndex];
            if (appliedEffect == null || appliedEffect.keyword == AbilityKeyword.None)
            {
                return $"{cardName} has no ability to apply.";
            }

            if (IsInfrastructureCard(targetCard))
            {
                return CardHasKeyword(targetCard, appliedEffect.keyword)
                    ? $"{targetCard.cardName} can only stack {abilityName} if that keyword is stackable."
                    : $"{cardName} can only add new abilities to units. Buildings can only stack abilities they already have.";
            }

            if (!IsUnitCard(targetCard))
            {
                return $"{cardName} must target a unit.";
            }

            if (CardHasKeyword(targetCard, appliedEffect.keyword) && !IsStackableKeyword(appliedEffect.keyword))
            {
                return $"{abilityName} cannot stack on {targetCard.cardName}.";
            }

            if (CardHasAnyKeyword(targetCard) && !CardHasKeyword(targetCard, appliedEffect.keyword))
            {
                return $"{targetCard.cardName} already has an ability. Units can only hold one ability type.";
            }

            return $"{cardName} cannot target {targetCard.cardName}.";
        }

        private bool TryApplyItemCardToTile(CardTemplate selectedCard, int tileIndex, int handIndex, MatchSeat seat)
        {
            if (_roundPhase != MatchRoundPhase.DeployPlanning
                || selectedCard == null
                || selectedCard.cardType != CardType.Item
                || handIndex < 0
                || handIndex >= cardsInHand.Count
                || !CanApplyItemToTile(selectedCard, tileIndex, seat))
            {
                return false;
            }

            if (!TrySpendTreasuryForCard(selectedCard, seat))
            {
                ShowInvalidActionAndClearSelection("Not enough treasury.");
                return true;
            }

            CardTemplate targetCard = _boardTileData[tileIndex];
            targetCard.attachedItemCard = CloneRuntimeCard(selectedCard);
            ApplyItemPayloadToCarrier(targetCard, selectedCard, tileIndex);
            cardsInHand.RemoveAt(handIndex);
            _highlightedCardIndex = -1;
            _selectedBoardTileIndex = tileIndex;
            _selectedAttackerTileIndex = -1;
            AddFloatingBoardText(tileIndex, "ITEM", "tile-floating-status");
            ShowAwarenessMessage($"<b>{GetSeatDisplayName(seat)} equipped {targetCard.cardName} with {selectedCard.cardName}.</b>", 2.4f);
            SyncVisibleStateFromPerspective();
            return true;
        }

        private bool CanApplyItemToTile(CardTemplate itemCard, int tileIndex, MatchSeat seat)
        {
            if (itemCard == null
                || itemCard.cardType != CardType.Item
                || tileIndex < 0
                || tileIndex >= _boardTileData.Length
                || !_tileOccupantSeats[tileIndex].HasValue
                || _tileOccupantSeats[tileIndex].Value != seat
                || _occupantCurrentHealth[tileIndex] <= 0)
            {
                return false;
            }

            CardTemplate targetCard = _boardTileData[tileIndex];
            if (IsSystemRuntimeCard(targetCard) || !IsUnitCard(targetCard) || targetCard.attachedItemCard != null)
            {
                return false;
            }

            if (itemCard.cardId == "card.free_haven.reinforced_plating")
            {
                return CardHasKeywordAtTile(tileIndex, AbilityKeyword.Intercept);
            }

            return true;
        }

        private string GetInvalidItemTargetReason(CardTemplate itemCard, int tileIndex, MatchSeat seat)
        {
            string itemName = itemCard != null ? itemCard.cardName : "Item";
            if (tileIndex < 0 || tileIndex >= _boardTileData.Length)
            {
                return $"{itemName} needs a valid board target.";
            }

            if (!_tileOccupantSeats[tileIndex].HasValue || _boardTileData[tileIndex] == null)
            {
                return $"{itemName} must target one of your units.";
            }

            if (_tileOccupantSeats[tileIndex].Value != seat)
            {
                return $"{itemName} can only target friendly units.";
            }

            if (_occupantCurrentHealth[tileIndex] <= 0)
            {
                return $"{itemName} cannot target a destroyed unit.";
            }

            CardTemplate targetCard = _boardTileData[tileIndex];
            if (!IsUnitCard(targetCard))
            {
                return $"{itemName} can only attach to units.";
            }

            if (targetCard.attachedItemCard != null)
            {
                return $"{targetCard.cardName} already has an item.";
            }

            if (itemCard != null && itemCard.cardId == "card.free_haven.reinforced_plating" && !CardHasKeywordAtTile(tileIndex, AbilityKeyword.Intercept))
            {
                return $"{itemName} can only attach to a unit that already has Intercept.";
            }

            return $"{itemName} cannot attach to {targetCard.cardName}.";
        }

        private void ApplyItemPayloadToCarrier(CardTemplate targetCard, CardTemplate itemCard, int tileIndex)
        {
            if (targetCard == null || itemCard == null)
            {
                return;
            }

            targetCard.bonusHealth += Mathf.Max(0, itemCard.bonusHealth);
            targetCard.bonusAttack += Mathf.Max(0, itemCard.bonusAttack);
            targetCard.bonusRange += Mathf.Max(0, itemCard.bonusRange);
            targetCard.bonusMovementRange += Mathf.Max(0, itemCard.bonusMovementRange);
            targetCard.bonusSiegeAttack += Mathf.Max(0, itemCard.bonusSiegeAttack);

            if (itemCard.bonusHealth > 0 && tileIndex >= 0 && tileIndex < _occupantCurrentHealth.Length)
            {
                targetCard.health += itemCard.bonusHealth;
                _occupantCurrentHealth[tileIndex] += itemCard.bonusHealth;
            }

            if (itemCard.cardId == "card.free_haven.truce_bell" || itemCard.cardId == "card.iron_citadel.ash_brand")
            {
                return;
            }

            AbilityEffectData itemEffect = GetPrimaryKeywordEffect(itemCard);
            if (itemEffect != null && itemEffect.keyword != AbilityKeyword.None)
            {
                TryAddOrStackKeyword(targetCard, itemEffect);
            }
        }

        private static string GetKeywordBadgeText(AbilityEffectData effect)
        {
            if (effect == null || effect.keyword == AbilityKeyword.None)
            {
                return "ABILITY";
            }

            return effect.keyword.ToString().ToUpperInvariant();
        }

        private static string FormatKeywordIconText(AbilityEffectData effect)
        {
            if (effect == null || effect.keyword == AbilityKeyword.None)
            {
                return string.Empty;
            }

            return IsStackableKeyword(effect.keyword) ? Mathf.Max(1, effect.value).ToString() : string.Empty;
        }

        private static string FormatKeywordDisplayTitle(AbilityEffectData effect)
        {
            if (effect == null || effect.keyword == AbilityKeyword.None)
            {
                return string.Empty;
            }

            return IsStackableKeyword(effect.keyword)
                ? $"{effect.keyword.ToString().ToUpperInvariant()} {Mathf.Max(1, effect.value)}"
                : effect.keyword.ToString().ToUpperInvariant();
        }

        private string GetInspectorEffectDescription(CardTemplate ownerCard, AbilityEffectData effect, bool appliedRuleCopy)
        {
            if (effect == null || effect.keyword == AbilityKeyword.None)
            {
                return string.Empty;
            }

            string detail = effect.GetDetailedDescription(appliedRuleCopy);
            if (appliedRuleCopy || ownerCard == null)
            {
                return detail;
            }

            string glossary = effect.GetKeywordGlossDescription();
            if (!string.IsNullOrWhiteSpace(glossary)
                && !string.IsNullOrWhiteSpace(detail)
                && detail.IndexOf(glossary, System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                return $"{glossary} {detail}".Trim();
            }

            if (!string.IsNullOrWhiteSpace(glossary))
            {
                return glossary;
            }

            return detail;
        }

        private string GetAppliedInspectorEffectDescription(AbilityEffectData effect)
        {
            if (effect == null || effect.keyword == AbilityKeyword.None)
            {
                return string.Empty;
            }

            string detail = effect.GetDetailedDescription(true);
            string glossary = effect.GetKeywordGlossDescription();
            if (!string.IsNullOrWhiteSpace(glossary)
                && !string.IsNullOrWhiteSpace(detail)
                && detail.IndexOf(glossary, System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                return $"{glossary} {detail}".Trim();
            }

            return !string.IsNullOrWhiteSpace(detail) ? detail : glossary;
        }

        private string GetItemInspectorLeadDescription(CardTemplate itemCard)
        {
            if (itemCard == null)
            {
                return string.Empty;
            }

            if (itemCard.cardId == "card.free_haven.truce_bell")
            {
                return "Attach to a friendly unit with no item. Cards damaged by this carrier are Silenced for 1 turn.";
            }

            if (itemCard.cardId == "card.iron_citadel.ash_brand")
            {
                return "Attach to a friendly unit with no item. Units killed by this carrier are burned instead of returning to discard.";
            }

            if (itemCard.cardId == "card.iron_citadel.demolition_rig")
            {
                return "Attach to a friendly unit with no item. This carrier deals double damage to buildings and base tiles.";
            }

            var parts = new List<string>();
            if (itemCard.bonusHealth > 0) parts.Add($"+{itemCard.bonusHealth} HP");
            if (itemCard.bonusAttack > 0) parts.Add($"+{itemCard.bonusAttack} AT");
            if (itemCard.bonusRange > 0) parts.Add($"+{itemCard.bonusRange} attack range");
            if (itemCard.bonusMovementRange > 0) parts.Add($"+{itemCard.bonusMovementRange} movement");
            if (itemCard.bonusSiegeAttack > 0) parts.Add($"+{itemCard.bonusSiegeAttack} siege AT");

            AbilityEffectData itemEffect = GetPrimaryKeywordEffect(itemCard);
            string attachRule = itemEffect != null && itemEffect.keyword == AbilityKeyword.Intercept
                ? "Attach only to a friendly unit that already has Intercept."
                : "Attach to a friendly unit with no item.";

            if (parts.Count == 0)
            {
                return attachRule;
            }

            return $"{attachRule} Grants {string.Join(", ", parts)} until the unit dies.";
        }

        private string GetThumbnailCardTypeLabel(CardTemplate cardData)
        {
            if (cardData == null)
            {
                return string.Empty;
            }

            switch (cardData.cardType)
            {
                case CardType.Unit:
                    return GetUnitTagDisplayLabel(cardData.unitTag).ToUpperInvariant();
                case CardType.Infrastructure:
                    return "BUILDING";
                case CardType.Ordinance:
                    return "ORDER";
                case CardType.Item:
                    return "ITEM";
                default:
                    return GetCardTypeLabel(cardData);
            }
        }

        private static void ApplyStatusBadgeSprite(VisualElement element, Sprite sprite)
        {
            if (element == null)
            {
                return;
            }

            if (sprite != null)
            {
                element.style.backgroundImage = new StyleBackground(sprite);
            }
            else
            {
                element.style.backgroundImage = StyleKeyword.Null;
            }

            element.EnableInClassList("status-icon-has-art", sprite != null);
        }

        private bool TryApplyLockCardToTile(CardTemplate selectedCard, int tileIndex)
        {
            if (_roundPhase != MatchRoundPhase.DeployPlanning
                || !IsLockCommandCard(selectedCard)
                || !CanTileReceiveLock(tileIndex, _localSeat))
            {
                return false;
            }

            _tileLocked[tileIndex] = true;
            cardsInHand.Remove(selectedCard);
            _highlightedCardIndex = -1;
            _selectedBoardTileIndex = tileIndex;
            _selectedAttackerTileIndex = -1;
            SetAbilityPreviewCard(_boardTileData[tileIndex]);
            AddFloatingBoardText(tileIndex, "LOCK", "tile-floating-status");
            return true;
        }

        private void UpdateAbilityPreview()
        {
            var previewShell = _root.Q<VisualElement>("ability-preview-shell");
            var previewLabel = _root.Q<Label>("ability-preview-text");
            if (previewShell == null || previewLabel == null)
            {
                return;
            }

            bool hasAwarenessOverride = !string.IsNullOrWhiteSpace(_awarenessOverrideText);
            bool hasDisplayNarration = !string.IsNullOrWhiteSpace(_displayNarrationText);
            bool hasCardAbilityPreview = false;
            bool isRoundAnnouncement = hasAwarenessOverride && IsRoundAnnouncementText(_awarenessOverrideText);
            string previewMarkup = hasAwarenessOverride
                ? BuildAwarenessPreviewMarkup(_awarenessOverrideText)
                : hasDisplayNarration
                ? BuildStatusPreviewMarkup(_displayNarrationText)
                : BuildStatusPreviewMarkup(_abilityPreviewText);

            bool hasPreview = !string.IsNullOrWhiteSpace(previewMarkup);
            previewShell.style.display = DisplayStyle.Flex;
            previewShell.EnableInClassList("ability-preview-empty", !hasPreview);
            previewShell.EnableInClassList("ability-preview-round", hasPreview && isRoundAnnouncement);
            previewShell.EnableInClassList("ability-preview-status", hasPreview && !isRoundAnnouncement && !hasCardAbilityPreview);
            previewShell.EnableInClassList("ability-preview-ability", hasPreview && hasCardAbilityPreview);
            previewLabel.enableRichText = true;
            previewLabel.text = hasPreview ? previewMarkup : string.Empty;
            if (_lastAbilityPreviewMarkup != previewMarkup)
            {
                _lastAbilityPreviewMarkup = previewMarkup;
                _abilityPreviewMarqueeStartTime = GetUnscaledNow();
                previewLabel.style.translate = new StyleTranslate(new Translate(new Length(0f, LengthUnit.Pixel), new Length(0f, LengthUnit.Pixel)));
            }
        }

        private string BuildAwarenessPreviewMarkup(string sourceText)
        {
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                return string.Empty;
            }

            return IsRoundAnnouncementText(sourceText) ? sourceText : BuildStatusPreviewMarkup(sourceText);
        }

        private bool IsRoundAnnouncementText(string sourceText)
        {
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                return false;
            }

            string normalized = sourceText.Replace("<b>", string.Empty).Replace("</b>", string.Empty).Trim();
            return normalized.StartsWith("Round ", StringComparison.OrdinalIgnoreCase)
                && normalized.IndexOf(" starts", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string BuildStatusPreviewMarkup(string sourceText)
        {
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                return string.Empty;
            }

            if (sourceText.Contains("<"))
            {
                return sourceText;
            }

            string[] rawSegments = sourceText.Split('.');
            List<string> styledSegments = new List<string>();
            for (int i = 0; i < rawSegments.Length; i++)
            {
                string segment = rawSegments[i].Trim();
                if (string.IsNullOrEmpty(segment))
                {
                    continue;
                }

                styledSegments.Add($"<color=#253238>{BuildStatusSentenceMarkup(segment)}.</color>");
            }

            return string.Join(" ", styledSegments);
        }

        private string BuildStatusSentenceMarkup(string sentence)
        {
            string[] tokens = sentence.Split(' ');
            for (int i = 0; i < tokens.Length; i++)
            {
                tokens[i] = BuildStatusTokenMarkup(tokens[i]);
            }

            return string.Join(" ", tokens);
        }

        private string BuildStatusTokenMarkup(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return token;
            }

            string stripped = token.Trim(',', '.', '!', '?', ':', ';', '(', ')');
            string lowered = stripped.ToLowerInvariant();
            bool isNumber = int.TryParse(stripped, out _);
            string loweredCompact = lowered.Replace("+", string.Empty).Replace("-", string.Empty);
            bool isStat = lowered == "hp"
                || lowered == "at"
                || (loweredCompact.EndsWith("hp", StringComparison.OrdinalIgnoreCase) && ContainsDigit(loweredCompact))
                || (loweredCompact.EndsWith("at", StringComparison.OrdinalIgnoreCase) && ContainsDigit(loweredCompact));
            bool isAction = lowered == "moved"
                || lowered == "move"
                || lowered == "dealt"
                || lowered == "attacked"
                || lowered == "attack"
                || lowered == "destroyed"
                || lowered == "locked"
                || lowered == "miss"
                || lowered == "siege"
                || lowered == "sieging"
                || lowered == "resolved";
            bool isPlace = lowered == "base"
                || lowered == "tile"
                || lowered == "city"
                || lowered == "lane";

            if (isNumber || isStat)
            {
                return $"<b><color=#DC2626>{token}</color></b>";
            }

            if (isAction)
            {
                return $"<b><color=#C2410C>{token}</color></b>";
            }

            if (isPlace)
            {
                return $"<b><color=#2563EB>{token}</color></b>";
            }

            return token;
        }

        private bool ContainsDigit(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsDigit(text[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateAbilityPreviewMarquee()
        {
            var previewShell = _root?.Q<VisualElement>("ability-preview-shell");
            var previewLabel = _root?.Q<Label>("ability-preview-text");
            if (previewShell == null || previewLabel == null)
            {
                return;
            }

            if (previewShell.ClassListContains("ability-preview-empty") || string.IsNullOrWhiteSpace(previewLabel.text))
            {
                previewLabel.style.translate = new StyleTranslate(new Translate(new Length(0f, LengthUnit.Pixel), new Length(0f, LengthUnit.Pixel)));
                return;
            }

            float shellHeight = previewShell.resolvedStyle.height
                - previewShell.resolvedStyle.paddingTop
                - previewShell.resolvedStyle.paddingBottom;
            float labelHeight = previewLabel.resolvedStyle.height;
            if (shellHeight <= 0f || labelHeight <= shellHeight + 2f)
            {
                previewLabel.style.translate = new StyleTranslate(new Translate(new Length(0f, LengthUnit.Pixel), new Length(0f, LengthUnit.Pixel)));
                return;
            }

            float travel = labelHeight - shellHeight;
            float travelDuration = travel / AbilityMarqueeSpeed;
            float cycleDuration = (AbilityMarqueePauseSeconds * 2f) + (travelDuration * 2f);
            float cycleTime = Mathf.Repeat(GetUnscaledNow() - Mathf.Max(0f, _abilityPreviewMarqueeStartTime), cycleDuration);

            float y;
            if (cycleTime < AbilityMarqueePauseSeconds)
            {
                y = 0f;
            }
            else if (cycleTime < AbilityMarqueePauseSeconds + travelDuration)
            {
                y = -Mathf.Lerp(0f, travel, (cycleTime - AbilityMarqueePauseSeconds) / travelDuration);
            }
            else if (cycleTime < (AbilityMarqueePauseSeconds * 2f) + travelDuration)
            {
                y = -travel;
            }
            else
            {
                float returnTime = cycleTime - ((AbilityMarqueePauseSeconds * 2f) + travelDuration);
                y = -Mathf.Lerp(travel, 0f, returnTime / travelDuration);
            }

            previewLabel.style.translate = new StyleTranslate(new Translate(new Length(0f, LengthUnit.Pixel), new Length(y, LengthUnit.Pixel)));
        }

        private float GetUnscaledNow()
        {
            return Application.isPlaying ? Time.unscaledTime : Time.realtimeSinceStartup;
        }

        private bool TrySpendTreasuryForDeployment(CardTemplate cardToPlay)
        {
            return TrySpendTreasuryForCard(cardToPlay, _localSeat);
        }

        private bool TrySpendTreasuryForCard(CardTemplate cardToPlay, MatchSeat seat)
        {
            ParticipantRuntimeState activeState = GetRuntimeState(seat);
            if (activeState == null || cardToPlay == null)
            {
                return false;
            }

            int effectiveCost = GetEffectiveDeploymentCost(cardToPlay, seat);
            if (activeState.treasury < effectiveCost)
            {
                Debug.LogWarning($"Cannot deploy {cardToPlay.cardName}. Need {effectiveCost} treasury but only have {activeState.treasury}.");
                return false;
            }

            activeState.treasury -= effectiveCost;
            return true;
        }

        private bool CanDeployCardToTile(int tileIndex, MatchSeat deployingSeat)
        {
            if (tileIndex < 0 || tileIndex >= _boardTileData.Length)
            {
                return false;
            }

            MatchSeat? tileSeat = GetSeatFromTileOwner(_tileOwners[tileIndex]);
            return _boardTileData[tileIndex] == null
                && _tileAreaKinds[tileIndex] == TileAreaKind.Base
                && tileSeat.HasValue
                && tileSeat.Value == deployingSeat
                && _tileCurrentHealth[tileIndex] > 0;
        }

        private string GetInvalidDeployReason(int tileIndex, MatchSeat deployingSeat)
        {
            if (tileIndex < 0 || tileIndex >= _boardTileData.Length)
            {
                return "Cards cannot be deployed off the board.";
            }

            if (_boardTileData[tileIndex] != null)
            {
                return "Deploy only to an empty base tile.";
            }

            if (_tileAreaKinds[tileIndex] != TileAreaKind.Base)
            {
                return "Deploy only to your base tiles.";
            }

            MatchSeat? tileSeat = GetSeatFromTileOwner(_tileOwners[tileIndex]);
            if (!tileSeat.HasValue || tileSeat.Value != deployingSeat)
            {
                return "Deploy only to your own base tiles.";
            }

            if (_tileCurrentHealth[tileIndex] <= 0)
            {
                return "That base tile has been destroyed.";
            }

            return "Invalid deploy target.";
        }

        private void CompleteCardDeployment(CardTemplate cardToPlay, int tileIndex, int handIndex, MatchSeat deployingSeat)
        {
            _cardDeployInFlight = false;

            ParticipantRuntimeState deployingState = GetRuntimeState(deployingSeat);
            if (cardToPlay == null
                || deployingState == null
                || handIndex < 0
                || handIndex >= deployingState.hand.Count
                || !IsBoardDeployableCard(cardToPlay))
            {
                return;
            }

            CardTemplate deployedCard = CloneRuntimeCard(cardToPlay);
            _boardTileData[tileIndex] = deployedCard;
            _tileOccupantSeats[tileIndex] = deployingSeat;
            if (IsInfrastructureCard(deployedCard))
            {
                int mergedHealth = Mathf.Max(0, deployedCard.health);
                _tileMaxHealth[tileIndex] = Mathf.Max(0, _tileMaxHealth[tileIndex]) + mergedHealth;
                _tileCurrentHealth[tileIndex] = Mathf.Max(0, _tileCurrentHealth[tileIndex]) + mergedHealth;
                _occupantCurrentHealth[tileIndex] = _tileCurrentHealth[tileIndex];
            }
            else
            {
                _occupantCurrentHealth[tileIndex] = deployedCard.health;
            }
            deployingState.hand.RemoveAt(handIndex);
            _highlightedCardIndex = -1;
            _selectedBoardTileIndex = tileIndex;
            _selectedAttackerTileIndex = IsUnitCard(deployedCard) && _roundPhase == MatchRoundPhase.DeployPlanning
                ? tileIndex
                : -1;
            _placementFocusActive = false;
            SetAbilityPreviewCard(deployedCard);
            SyncVisibleStateFromPerspective();
            ShowAwarenessMessage($"{GetSeatDisplayName(deployingSeat)} placed {deployedCard.cardName}.", 2f);
            UpdateUI();
            if (!_isApplyingRemoteSeatAction)
            {
                AnimateBoardTileDeployment(tileIndex);
                CenterBoardOnTileIfNeeded(tileIndex);
            }
            Debug.Log($"Deployed {deployedCard.cardName} to Tile {tileIndex} for {GetEffectiveDeploymentCost(cardToPlay, deployingSeat)} treasury.");
        }

        private void RequestBoardFitAndCenter(bool fitScaleToViewport)
        {
            _boardViewNeedsReset = true;
            _boardViewResetAttempts = 0;
            var boardScroll = _root?.Q<ScrollView>("board-scroll-view");
            if (boardScroll == null)
            {
                return;
            }

            FitAndCenterBoardView(boardScroll, fitScaleToViewport);
        }

        private Vector2 GetBaseBoardDimensions()
        {
            float tileFootprintWidth = TileBaseWidth + (TileBaseMargin * 2f);
            float tileFootprintHeight = TileBaseHeight + (TileBaseMargin * 2f);
            return new Vector2(_boardColumns * tileFootprintWidth, _boardRows * tileFootprintHeight);
        }

        private float GetBoardFitScale(float viewportWidth, float viewportHeight)
        {
            Vector2 boardBaseDimensions = GetBaseBoardDimensions();
            float availableWidth = Mathf.Max(1f, viewportWidth - (BoardFitPaddingX * 2f));
            float availableHeight = Mathf.Max(
                1f,
                viewportHeight
                    - GetCurrentBoardViewportPaddingTop()
                    - GetCurrentBoardViewportPaddingBottom()
                    - (GetCurrentBoardFitPaddingY() * 2f));
            float widthScale = availableWidth / Mathf.Max(1f, boardBaseDimensions.x);
            float heightScale = availableHeight / Mathf.Max(1f, boardBaseDimensions.y);
            float fitScale = Mathf.Min(widthScale, heightScale);
            if (_desktopDockLayoutActive)
            {
                fitScale *= DesktopBoardFitScaleFactor;
            }

            return Mathf.Clamp(fitScale, GetCurrentMinTileScale(), MaxTileScale);
        }

        private float GetCurrentMinTileScale()
        {
            return _desktopDockLayoutActive ? DesktopMinTileScale : MinTileScale;
        }

        private float GetCurrentBoardFitPaddingY()
        {
            return _desktopDockLayoutActive ? 0f : BoardFitPaddingY;
        }

        private float GetCurrentBoardViewportPaddingTop()
        {
            return _desktopDockLayoutActive ? 0f : BoardViewportPaddingTop;
        }

        private float GetCurrentBoardViewportPaddingBottom()
        {
            return _desktopDockLayoutActive ? 0f : BoardViewportPaddingBottom;
        }

        private void FitAndCenterBoardView(ScrollView boardScroll, bool fitScaleToViewport)
        {
            if (boardScroll == null)
            {
                return;
            }

            VisualElement viewport = boardScroll.contentViewport ?? boardScroll;
            VisualElement content = boardScroll.contentContainer;
            float viewportWidth = viewport.resolvedStyle.width;
            float viewportHeight = viewport.resolvedStyle.height;
            float contentWidth = content.resolvedStyle.width;
            float contentHeight = content.resolvedStyle.height;

            if (viewportWidth <= 0f || viewportHeight <= 0f || contentWidth <= 0f || contentHeight <= 0f)
            {
                if (_boardViewResetAttempts < 20)
                {
                    _boardViewResetAttempts++;
                    boardScroll.schedule.Execute(() => FitAndCenterBoardView(boardScroll, fitScaleToViewport)).StartingIn(16);
                }
                return;
            }

            if (fitScaleToViewport)
            {
                float fitScale = GetBoardFitScale(viewportWidth, viewportHeight);
                if (!Mathf.Approximately(_tileScale, fitScale))
                {
                    _tileScale = fitScale;
                    _boardViewNeedsReset = true;
                    boardScroll.schedule.Execute(UpdateUI).StartingIn(0);
                    return;
                }
            }

            Vector2 boardDimensions = GetBaseBoardDimensions() * _tileScale;
            float centeredHorizontalOffset = Mathf.Max(0f, (boardDimensions.x * 0.5f) - (viewportWidth * 0.5f));
            float centeredVerticalOffset = Mathf.Max(0f, (GetCurrentBoardViewportPaddingTop() + (boardDimensions.y * 0.5f)) - (viewportHeight * 0.5f));

            boardScroll.schedule.Execute(() =>
            {
                boardScroll.scrollOffset = ClampBoardScrollOffset(boardScroll, new Vector2(centeredHorizontalOffset, centeredVerticalOffset));
                _boardViewNeedsReset = false;
                _boardViewResetAttempts = 0;
            });
        }

        private int GetDisplayRowForCanonicalRow(int canonicalRow)
        {
            if (!ShouldFlipBoardRowsForCurrentView())
            {
                return canonicalRow;
            }

            return (_boardRows - 1) - canonicalRow;
        }

        private bool ShouldFlipBoardRowsForCurrentView()
        {
            if (_preserveCanonicalBoardView)
            {
                return false;
            }

            return MatchPerspectiveUtility.ShouldFlipRows(_canonicalTopSeat, _perspectiveSeat);
        }

        private void CenterBoardOnTileIfNeeded(int tileIndex)
        {
            var boardScroll = _root?.Q<ScrollView>("board-scroll-view");
            if (boardScroll == null)
            {
                return;
            }

            VisualElement viewport = boardScroll.contentViewport ?? boardScroll;
            float viewportWidth = viewport.resolvedStyle.width;
            float viewportHeight = viewport.resolvedStyle.height;
            if (viewportWidth <= 0f || viewportHeight <= 0f)
            {
                return;
            }

            Vector2 boardDimensions = GetBaseBoardDimensions() * _tileScale;
            if (boardDimensions.x <= viewportWidth && boardDimensions.y <= viewportHeight)
            {
                return;
            }

            if (IsTileComfortablyVisibleInViewport(boardScroll, tileIndex, 44f))
            {
                return;
            }

            int canonicalRow = tileIndex / _boardColumns;
            int column = tileIndex % _boardColumns;
            int displayRow = GetDisplayRowForCanonicalRow(canonicalRow);

            float tileFootprintWidth = (TileBaseWidth + (TileBaseMargin * 2f)) * _tileScale;
            float tileFootprintHeight = (TileBaseHeight + (TileBaseMargin * 2f)) * _tileScale;
            float tileCenterX = (column * tileFootprintWidth) + (tileFootprintWidth * 0.5f);
            float tileCenterY = GetCurrentBoardViewportPaddingTop() + (displayRow * tileFootprintHeight) + (tileFootprintHeight * 0.5f);
            Vector2 centeredOffset = new Vector2(tileCenterX - (viewportWidth * 0.5f), tileCenterY - (viewportHeight * 0.5f));

            boardScroll.schedule.Execute(() =>
            {
                boardScroll.scrollOffset = ClampBoardScrollOffset(boardScroll, centeredOffset);
            });
        }

        private bool IsTileComfortablyVisibleInViewport(ScrollView boardScroll, int tileIndex, float marginPixels)
        {
            if (tileIndex < 0)
            {
                return false;
            }

            VisualElement viewport = boardScroll.contentViewport ?? boardScroll;
            float viewportWidth = viewport.resolvedStyle.width;
            float viewportHeight = viewport.resolvedStyle.height;
            if (viewportWidth <= 0f || viewportHeight <= 0f)
            {
                return false;
            }

            int canonicalRow = tileIndex / _boardColumns;
            int column = tileIndex % _boardColumns;
            int displayRow = GetDisplayRowForCanonicalRow(canonicalRow);

            float tileFootprintWidth = (TileBaseWidth + (TileBaseMargin * 2f)) * _tileScale;
            float tileFootprintHeight = (TileBaseHeight + (TileBaseMargin * 2f)) * _tileScale;
            float tileLeft = column * tileFootprintWidth;
            float tileTop = GetCurrentBoardViewportPaddingTop() + (displayRow * tileFootprintHeight);
            float tileRight = tileLeft + tileFootprintWidth;
            float tileBottom = tileTop + tileFootprintHeight;

            float visibleLeft = boardScroll.scrollOffset.x + marginPixels;
            float visibleTop = boardScroll.scrollOffset.y + marginPixels;
            float visibleRight = boardScroll.scrollOffset.x + viewportWidth - marginPixels;
            float visibleBottom = boardScroll.scrollOffset.y + viewportHeight - marginPixels;

            return tileLeft >= visibleLeft
                && tileRight <= visibleRight
                && tileTop >= visibleTop
                && tileBottom <= visibleBottom;
        }

        private void ResetBoardViewToPlayerAnchorIfNeeded()
        {
            if (!_boardViewNeedsReset)
            {
                return;
            }

            RequestBoardFitAndCenter(true);
        }

        private Vector2 ClampBoardScrollOffset(ScrollView boardScroll, Vector2 candidateOffset)
        {
            VisualElement viewport = boardScroll.contentViewport ?? boardScroll;
            VisualElement content = boardScroll.contentContainer;
            float maxX = Mathf.Max(0f, content.resolvedStyle.width - viewport.resolvedStyle.width);
            float maxY = Mathf.Max(0f, content.resolvedStyle.height - viewport.resolvedStyle.height);
            return new Vector2(
                Mathf.Clamp(candidateOffset.x, 0f, maxX),
                Mathf.Clamp(candidateOffset.y, 0f, maxY));
        }

        private void ResetBoardPanState()
        {
            _boardPanActive = false;
            _boardPanMoved = false;
            _boardPanPointerId = -1;
        }

        private void RegisterBoardPanInteractions(ScrollView boardScroll)
        {
            VisualElement dragSurface = boardScroll.contentViewport ?? boardScroll;

            dragSurface.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                _boardPanActive = true;
                _boardPanMoved = false;
                _boardPanPointerId = evt.pointerId;
                _boardPanPointerStart = new Vector2(evt.position.x, evt.position.y);
                _boardPanScrollStart = boardScroll.scrollOffset;
            });

            dragSurface.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_boardPanActive || evt.pointerId != _boardPanPointerId)
                {
                    return;
                }

                Vector2 pointerPosition = new Vector2(evt.position.x, evt.position.y);
                Vector2 pointerDelta = pointerPosition - _boardPanPointerStart;
                if (!_boardPanMoved && pointerDelta.sqrMagnitude >= BoardPanDragThreshold * BoardPanDragThreshold)
                {
                    _boardPanMoved = true;
                    _suppressNextBoardClick = true;
                }

                if (!_boardPanMoved)
                {
                    return;
                }

                boardScroll.scrollOffset = ClampBoardScrollOffset(boardScroll, _boardPanScrollStart - pointerDelta);
                evt.StopPropagation();
            });

            dragSurface.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.pointerId != _boardPanPointerId)
                {
                    return;
                }

                bool consumedAsPan = _boardPanMoved;
                ResetBoardPanState();
                if (consumedAsPan)
                {
                    boardScroll.schedule.Execute(() =>
                    {
                        _suppressNextBoardClick = false;
                    });
                    evt.StopPropagation();
                }
            });

            dragSurface.RegisterCallback<PointerCaptureOutEvent>(evt =>
            {
                if (evt.pointerId == _boardPanPointerId)
                {
                    ResetBoardPanState();
                }
            });
        }

        private void PopulateHandCarousel()
        {
            var carousel = _root.Q<ScrollView>("hand-carousel");
            if (carousel == null) return;

            if (_roundPhase != MatchRoundPhase.DeployPlanning)
            {
                if (carousel.childCount > 0)
                {
                    carousel.Clear();
                }

                _lastHandCarouselSignature = $"{_roundPhase}|hidden";
                _lastHandCarouselHighlightIndex = -1;
                _lastHandCarouselPhase = _roundPhase;
                return;
            }

            string handSignature = BuildHandCarouselSignature();
            if (carousel.childCount > 0
                && _lastHandCarouselSignature == handSignature
                && _lastHandCarouselHighlightIndex == _highlightedCardIndex
                && _lastHandCarouselPhase == _roundPhase)
            {
                return;
            }

            bool shouldAnimateHandEntry = _lastHandCarouselSignature != handSignature || _lastHandCarouselPhase != _roundPhase;
            carousel.Clear();

            EnsureCardThumbnailTemplate();

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

            bool isLocalTurn = _activeTurnSeat == _localSeat && _roundPhase == MatchRoundPhase.DeployPlanning;
            carousel.EnableInClassList("hand-carousel-inactive-turn", !isLocalTurn);
            carousel.pickingMode = PickingMode.Position;

            int realCardEntryIndex = 0;
            for (int i = 0; i < cardsInHand.Count; i++)
            {
                var cardData = cardsInHand[i];
                if (cardData == null) continue;

                // Instantiate UXML template
                VisualElement cardInstance = cardThumbnailTemplate.Instantiate();

                // Add margins/spacings (offset index 0 to start clear of zoom panel)
                cardInstance.style.marginRight = 10;
                cardInstance.style.marginLeft = (i == 0) ? 100 : 10;

                VisualElement cardRoot = BindCardThumbnail(cardInstance, cardData, _localSeat, isLocalTurn, i == _highlightedCardIndex);
                if (shouldAnimateHandEntry)
                {
                    int extraDelayMs = 0;
                    if (IsRealDeckCard(cardData)
                        && _nextHandEntryRepoolRealIndex >= 0
                        && realCardEntryIndex >= _nextHandEntryRepoolRealIndex)
                    {
                        extraDelayMs = _nextHandEntryRepoolDelayMs;
                    }

                    PrepareHandCardEntryAnimation(cardRoot, i, extraDelayMs);
                }
                if (IsRealDeckCard(cardData))
                {
                    realCardEntryIndex++;
                }

                // Register click callback
                int index = i;
                RegisterCardHoldToInspect(cardInstance, () => index >= 0 && index < cardsInHand.Count ? cardsInHand[index] : null);
                cardInstance.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation(); // Prevent click-away deselect from firing
                    if (_cardHoldDetailOpened)
                    {
                        _cardHoldDetailOpened = false;
                        return;
                    }

                    if (_roundPhase == MatchRoundPhase.DisplayResolution)
                    {
                        return;
                    }

                    if (!isLocalTurn)
                    {
                        return;
                    }

                    bool dispatched = ShouldDispatchHandCardClick(index)
                        && TryDispatchUiAction(new MatchUiAction
                        {
                            actionType = MatchUiActionType.ToggleHandCard,
                            handIndex = index,
                            clickCount = evt.clickCount
                        });

                    if (dispatched)
                    {
                        HandleHandCardClicked(index);
                        return;
                    }

                    if (IsRemoteReplica())
                    {
                        return;
                    }

                    HandleHandCardClicked(index);
                });

                carousel.Add(cardInstance);
            }

            _lastHandCarouselSignature = handSignature;
            _lastHandCarouselHighlightIndex = _highlightedCardIndex;
            _lastHandCarouselPhase = _roundPhase;
            if (shouldAnimateHandEntry)
            {
                _nextHandEntryRepoolRealIndex = -1;
                _nextHandEntryRepoolDelayMs = 0;
            }
        }

        private void EnsureCardThumbnailTemplate()
        {
            #if UNITY_EDITOR
            if (cardThumbnailTemplate == null || cardThumbnailTemplate.name == "MainHUD")
            {
                cardThumbnailTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/UXML/CardThumbnail.uxml");
            }
            #endif
        }

        private VisualElement BindCardThumbnail(VisualElement cardInstance, CardTemplate cardData, MatchSeat seat, bool isInteractiveTurn, bool highlighted)
        {
            if (cardInstance == null || cardData == null)
            {
                return null;
            }

            var thumbName = cardInstance.Q<Label>("card-name");
            if (thumbName != null) thumbName.text = cardData.cardName.ToUpper();

            int effectiveCost = GetEffectiveDeploymentCost(cardData, seat);
            bool isDiscounted = effectiveCost < cardData.treasuryCost;
            bool canAfford = CanAffordCard(cardData, seat);
            var thumbCost = cardInstance.Q<Label>("card-cost");
            var thumbCostBadge = cardInstance.Q<VisualElement>(className: "thumbnail-cost-badge");
            if (thumbCost != null)
            {
                thumbCost.text = effectiveCost.ToString();
                thumbCost.EnableInClassList("thumbnail-cost-text-discounted", isDiscounted && canAfford);
                thumbCost.EnableInClassList("thumbnail-cost-text-unaffordable", !canAfford);
            }
            if (thumbCostBadge != null)
            {
                thumbCostBadge.EnableInClassList("thumbnail-cost-badge-discounted", isDiscounted && canAfford);
                thumbCostBadge.EnableInClassList("thumbnail-cost-badge-unaffordable", !canAfford);
            }

            var thumbType = cardInstance.Q<Label>("card-type-badge");
            if (thumbType != null)
            {
                thumbType.text = GetThumbnailCardTypeLabel(cardData);
            }

            var thumbArt = cardInstance.Q<VisualElement>("card-art");
            if (thumbArt != null)
            {
                thumbArt.style.backgroundImage = StyleKeyword.Null;
                ApplyGeneratedCardArtClasses(thumbArt, cardData);
                if (cardData.customArt != null)
                {
                    thumbArt.style.backgroundImage = new StyleBackground(cardData.customArt);
                }
            }

            var thumbHp = cardInstance.Q<Label>("card-hp");
            if (thumbHp != null) thumbHp.text = $"<b>{cardData.health}</b><b>HP</b>";

            var thumbAt = cardInstance.Q<Label>("card-at");
            if (thumbAt != null) thumbAt.text = $"<b>{GetPrintedAttack(cardData)}</b><b>AT</b>";

            var lockIcon = cardInstance.Q<Label>("card-lock-icon");
            if (lockIcon != null)
            {
                bool showLock = IsLockCommandCard(cardData);
                lockIcon.style.display = showLock ? DisplayStyle.Flex : DisplayStyle.None;
                lockIcon.style.visibility = Visibility.Visible;
                lockIcon.text = string.Empty;
                lockIcon.EnableInClassList("status-icon-lock", showLock);
                ApplyStatusBadgeSprite(lockIcon, null);
            }

            var abilityIcon = cardInstance.Q<Label>("card-ability-icon");
            if (abilityIcon != null)
            {
                AbilityEffectData effect = GetPrimaryKeywordEffect(cardData);
                bool showAbility = effect != null && effect.keyword != AbilityKeyword.None && !IsLockCommandCard(cardData);
                abilityIcon.style.display = showAbility ? DisplayStyle.Flex : DisplayStyle.None;
                abilityIcon.style.visibility = Visibility.Visible;
                abilityIcon.text = showAbility ? FormatKeywordIconText(effect) : string.Empty;
                ApplyKeywordBadgeClasses(abilityIcon, effect);
                ApplyStatusBadgeSprite(abilityIcon, null);
            }

            var itemIcon = cardInstance.Q<Label>("card-item-icon");
            if (itemIcon != null)
            {
                bool showItem = cardData.cardType == CardType.Item || cardData.attachedItemCard != null;
                itemIcon.style.display = showItem ? DisplayStyle.Flex : DisplayStyle.None;
                itemIcon.style.visibility = Visibility.Visible;
                itemIcon.text = string.Empty;
                Sprite itemSprite = cardData.cardType == CardType.Item
                    ? cardData.customArt
                    : cardData.attachedItemCard != null ? cardData.attachedItemCard.customArt : null;
                ApplyStatusBadgeSprite(itemIcon, itemSprite);
            }

            var cardRoot = cardInstance.Q<VisualElement>(className: "card-thumbnail");
            if (cardRoot != null)
            {
                string seatThemeClass = GetSeatThemeClass(seat);
                cardRoot.EnableInClassList("seat-theme-one", seatThemeClass == "seat-theme-one");
                cardRoot.EnableInClassList("seat-theme-two", seatThemeClass == "seat-theme-two");
                cardRoot.EnableInClassList("card-thumbnail-inactive-turn", !isInteractiveTurn);
                cardRoot.EnableInClassList("highlighted", highlighted);
            }

            return cardRoot;
        }

        private void RegisterCardHoldToInspect(VisualElement element, Func<CardTemplate> cardResolver)
        {
            if (element == null || cardResolver == null)
            {
                return;
            }

            element.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                _cardHoldDetailOpened = false;
                object holdToken = new object();
                element.userData = holdToken;
                element.schedule.Execute(() =>
                {
                    if (!ReferenceEquals(element.userData, holdToken))
                    {
                        return;
                    }

                    CardTemplate card = cardResolver();
                    if (card == null)
                    {
                        return;
                    }

                    ResetBoardPanState();
                    _suppressNextBoardClick = false;
                    _cardHoldDetailOpened = true;
                    OpenInspectorOverlay(card);
                }).StartingIn(Mathf.RoundToInt(CardHoldDetailSeconds * 1000f));
            });

            element.RegisterCallback<PointerUpEvent>(_ => element.userData = null);
            element.RegisterCallback<PointerLeaveEvent>(_ => element.userData = null);
        }

        private static void ApplyKeywordBadgeClasses(VisualElement element, AbilityEffectData effect)
        {
            if (element == null)
            {
                return;
            }

            AbilityKeyword keyword = effect != null ? effect.keyword : AbilityKeyword.None;
            foreach (AbilityKeyword value in Enum.GetValues(typeof(AbilityKeyword)))
            {
                if (value == AbilityKeyword.None)
                {
                    continue;
                }

                element.EnableInClassList($"status-icon-{value.ToString().ToLowerInvariant()}", keyword == value);
            }
        }

        private void PrepareHandCardEntryAnimation(VisualElement cardRoot, int index, int extraDelayMs = 0)
        {
            if (cardRoot == null || !Application.isPlaying)
            {
                return;
            }

            cardRoot.AddToClassList("hand-card-enter-pre");
            int delayMs = (Mathf.Max(0, index) * 85) + Mathf.Max(0, extraDelayMs);
            cardRoot.schedule.Execute(() =>
            {
                cardRoot.RemoveFromClassList("hand-card-enter-pre");
                cardRoot.AddToClassList("hand-card-enter-active");
            }).StartingIn(delayMs);
            cardRoot.schedule.Execute(() => cardRoot.RemoveFromClassList("hand-card-enter-active")).StartingIn(delayMs + 360);
        }

        private bool TryAnimateVisibleHandExitBeforeDeployEnd()
        {
            if (!Application.isPlaying || _root == null)
            {
                return false;
            }

            var carousel = _root.Q<ScrollView>("hand-carousel");
            VisualElement container = carousel != null ? carousel.contentContainer : null;
            if (container == null || container.childCount <= 0 || _roundPhase != MatchRoundPhase.DeployPlanning)
            {
                return false;
            }

            int cardIndex = 0;
            foreach (VisualElement child in container.Children())
            {
                VisualElement cardRoot = child.Q<VisualElement>(className: "card-thumbnail");
                if (cardRoot == null)
                {
                    continue;
                }

                int delayMs = cardIndex * 70;
                cardRoot.schedule.Execute(() => cardRoot.AddToClassList("hand-card-exit-active")).StartingIn(delayMs);
                cardIndex++;
            }

            return cardIndex > 0;
        }

        private void HandleHandCardClicked(int handIndex)
        {
            if (handIndex < 0 || handIndex >= cardsInHand.Count)
            {
                return;
            }

            CardTemplate cardData = cardsInHand[handIndex];
            if (_roundPhase == MatchRoundPhase.DeployPlanning
                && _activeTurnSeat == _localSeat
                && !CanAffordCard(cardData, _localSeat))
            {
                ShakeHandCardAtIndex(handIndex);
                ShowInvalidActionAndClearSelection("Not enough treasury.");
                return;
            }

            if (_highlightedCardIndex == handIndex)
            {
                _highlightedCardIndex = -1;
                _placementFocusActive = false;
                SetAbilityPreviewCard(null);
                _selectedBoardTileIndex = -1;
            }
            else
            {
                _highlightedCardIndex = handIndex;
                _selectedBoardTileIndex = -1;
                SetAbilityPreviewCard(cardData);
            }

            CaptureCurrentTransientUiState(_localSeat);
            UpdateUI();
        }

        private void ShakeHandCardAtIndex(int handIndex)
        {
            var handCarousel = _root?.Q<ScrollView>("hand-carousel");
            if (handCarousel?.contentContainer == null
                || handIndex < 0
                || handIndex >= handCarousel.contentContainer.childCount)
            {
                return;
            }

            VisualElement cardInstance = handCarousel.contentContainer[handIndex];
            VisualElement cardRoot = cardInstance?.Q<VisualElement>(className: "card-thumbnail") ?? cardInstance;
            if (cardRoot == null)
            {
                return;
            }

            object shakeToken = new object();
            cardRoot.userData = shakeToken;
            cardRoot.EnableInClassList("card-thumbnail-shaking", true);

            void ApplyShakeOffset(int delayMs, float offsetX)
            {
                cardRoot.schedule.Execute(() =>
                {
                    if (!ReferenceEquals(cardRoot.userData, shakeToken))
                    {
                        return;
                    }

                    cardRoot.style.translate = new StyleTranslate(new Translate(
                        new Length(offsetX, LengthUnit.Pixel),
                        new Length(0f, LengthUnit.Pixel)));
                }).StartingIn(delayMs);
            }

            ApplyShakeOffset(0, -16f);
            ApplyShakeOffset(55, 15f);
            ApplyShakeOffset(110, -11f);
            ApplyShakeOffset(165, 8f);
            ApplyShakeOffset(220, -4f);

            cardRoot.schedule.Execute(() =>
            {
                if (!ReferenceEquals(cardRoot.userData, shakeToken))
                {
                    return;
                }

                cardRoot.userData = null;
                cardRoot.EnableInClassList("card-thumbnail-shaking", false);
                cardRoot.style.translate = StyleKeyword.Null;
            }).StartingIn(285);
        }

        private void UpdateContextualActionBar()
        {
            var actionBar = _root.Q<VisualElement>("contextual-action-bar");
            if (actionBar == null)
            {
                return;
            }

            bool hasSelectedCard = _highlightedCardIndex >= 0 && _highlightedCardIndex < cardsInHand.Count;
            if (!hasSelectedCard)
            {
                _placementFocusActive = false;
            }

            if (hasSelectedCard)
            {
                actionBar.RemoveFromClassList("contextual-bar-hidden");
            }
            else
            {
                actionBar.AddToClassList("contextual-bar-hidden");
            }

            var deployBtn = actionBar.Q<Button>("deploy-btn");
            if (deployBtn != null)
            {
                deployBtn.style.display = hasSelectedCard ? DisplayStyle.Flex : DisplayStyle.None;
            }

            var moveBtn = actionBar.Q<Button>("move-btn");
            if (moveBtn != null)
            {
                moveBtn.style.display = DisplayStyle.None;
            }

            var attackBtn = actionBar.Q<Button>("attack-btn");
            if (attackBtn != null)
            {
                attackBtn.style.display = DisplayStyle.None;
            }
        }

        public void OpenInspectorOverlay(CardTemplate cardData)
        {
            ResetBoardPanState();
            _suppressNextBoardClick = false;
            detailedCardData = cardData;
            isInspectorOverlayOpen = true;
            UpdateUI();
        }

        public void CloseInspectorOverlay()
        {
            ResetBoardPanState();
            _suppressNextBoardClick = false;
            isInspectorOverlayOpen = false;
            UpdateUI();
        }

        private string BuildDetailedCardRulesText(CardTemplate cardData)
        {
            if (cardData == null)
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder();
            bool appliedRuleCopy = cardData.cardType == CardType.Unit || cardData.cardType == CardType.Infrastructure;

            if (cardData.bonusHealth > 0 || cardData.bonusAttack > 0 || cardData.bonusRange > 0 || cardData.bonusMovementRange > 0 || cardData.bonusSiegeAttack > 0)
            {
                builder.Append("Current bonuses: ");
                var bonuses = new List<string>();
                if (cardData.bonusHealth > 0) bonuses.Add($"+{cardData.bonusHealth} HP");
                if (cardData.bonusAttack > 0) bonuses.Add($"+{cardData.bonusAttack} AT");
                if (cardData.bonusRange > 0) bonuses.Add($"+{cardData.bonusRange} attack range");
                if (cardData.bonusMovementRange > 0) bonuses.Add($"+{cardData.bonusMovementRange} movement");
                if (cardData.bonusSiegeAttack > 0) bonuses.Add($"+{cardData.bonusSiegeAttack} siege AT");
                builder.Append(string.Join(", ", bonuses)).Append('.');
            }

            if (cardData.cardType == CardType.Item)
            {
                if (builder.Length > 0)
                {
                    builder.Append("\n\n");
                }

                builder.Append("ITEM - ").Append(cardData.cardName).Append("\n").Append(GetItemInspectorLeadDescription(cardData));
            }

            if (cardData.keywordEffects != null && cardData.keywordEffects.Count > 0)
            {
                for (int i = 0; i < cardData.keywordEffects.Count; i++)
                {
                    AbilityEffectData effect = cardData.keywordEffects[i];
                    if (effect == null || effect.keyword == AbilityKeyword.None)
                    {
                        continue;
                    }

                    if (builder.Length > 0)
                    {
                        builder.Append("\n\n");
                    }

                    builder.Append(FormatKeywordDisplayTitle(effect));
                    string detail = GetInspectorEffectDescription(cardData, effect, appliedRuleCopy);
                    if (!string.IsNullOrWhiteSpace(detail))
                    {
                        builder.Append("\n").Append(detail.Trim());
                    }
                }
            }
            else
            {
                string baseRules = cardData.GetDetailedAbilityText();
                if (!string.IsNullOrWhiteSpace(baseRules))
                {
                    if (builder.Length > 0)
                    {
                        builder.Append("\n\n");
                    }
                    builder.Append(baseRules.Trim());
                }
            }

            if (cardData.attachedItemCard != null)
            {
                if (builder.Length > 0)
                {
                    builder.Append("\n\n");
                }

                builder.Append("ITEM - ")
                    .Append(cardData.attachedItemCard.cardName);

                AbilityEffectData itemEffect = GetPrimaryKeywordEffect(cardData.attachedItemCard);
                if (itemEffect != null && itemEffect.keyword != AbilityKeyword.None)
                {
                    builder.Append("\n").Append(FormatKeywordDisplayTitle(itemEffect));
                    string itemDetail = GetAppliedInspectorEffectDescription(itemEffect);
                    if (!string.IsNullOrWhiteSpace(itemDetail))
                    {
                        builder.Append("\n").Append(itemDetail.Trim());
                    }
                }
                else
                {
                    string itemRules = cardData.attachedItemCard.GetDetailedAbilityText();
                    if (!string.IsNullOrWhiteSpace(itemRules))
                    {
                        builder.Append("\n").Append(itemRules.Trim());
                    }
                    else
                    {
                        builder.Append("\nPermanent equipment until this unit is destroyed.");
                    }
                }
            }

            if (builder.Length == 0)
            {
                builder.Append("Plain card. Upgrade with orders or items to add rules.");
            }

            return builder.ToString();
        }

        private int GetPrintedHealthValue(CardTemplate cardData)
        {
            if (cardData == null)
            {
                return 0;
            }

            Dictionary<string, CardTemplate> knownLookup = BuildKnownCardLookup();
            if (knownLookup != null
                && !string.IsNullOrWhiteSpace(cardData.cardId)
                && knownLookup.TryGetValue(cardData.cardId, out CardTemplate sourceTemplate)
                && sourceTemplate != null)
            {
                return Mathf.Max(0, sourceTemplate.health);
            }

            return Mathf.Max(0, cardData.health);
        }

        private int GetDetailedCardCurrentHealth(CardTemplate cardData)
        {
            if (cardData == null)
            {
                return 0;
            }

            for (int tileIndex = 0; tileIndex < _boardTileData.Length; tileIndex++)
            {
                if (ReferenceEquals(_boardTileData[tileIndex], cardData))
                {
                    return Mathf.Max(0, _occupantCurrentHealth[tileIndex]);
                }
            }

            return Mathf.Max(0, cardData.health);
        }

        private void PopulateInspectorRuleEntries(VisualElement container, CardTemplate cardData)
        {
            if (container == null)
            {
                return;
            }

            container.Clear();
            if (cardData == null)
            {
                return;
            }

            bool addedEntry = false;
            bool appliedRuleCopy = cardData.cardType == CardType.Unit || cardData.cardType == CardType.Infrastructure;

            if (cardData.bonusHealth > 0 || cardData.bonusAttack > 0 || cardData.bonusRange > 0 || cardData.bonusMovementRange > 0 || cardData.bonusSiegeAttack > 0)
            {
                var bonuses = new List<string>();
                if (cardData.bonusHealth > 0) bonuses.Add($"+{cardData.bonusHealth} HP");
                if (cardData.bonusAttack > 0) bonuses.Add($"+{cardData.bonusAttack} AT");
                if (cardData.bonusRange > 0) bonuses.Add($"+{cardData.bonusRange} attack range");
                if (cardData.bonusMovementRange > 0) bonuses.Add($"+{cardData.bonusMovementRange} movement range");
                if (cardData.bonusSiegeAttack > 0) bonuses.Add($"+{cardData.bonusSiegeAttack} siege AT");
                AddInspectorRuleEntry(container, "CURRENT BONUSES", string.Join(", ", bonuses) + ".", null, null, "inspector-bonus-icon");
                addedEntry = true;
            }

            if (cardData.cardType == CardType.Item)
            {
                AddInspectorRuleEntry(
                    container,
                    $"ITEM - {cardData.cardName.ToUpper()}",
                    GetItemInspectorLeadDescription(cardData),
                    null,
                    cardData.customArt);
                addedEntry = true;
            }

            if (cardData.keywordEffects != null)
            {
                for (int i = 0; i < cardData.keywordEffects.Count; i++)
                {
                    AbilityEffectData effect = cardData.keywordEffects[i];
                    if (effect == null || effect.keyword == AbilityKeyword.None)
                    {
                        continue;
                    }

                    AddInspectorRuleEntry(container, FormatKeywordDisplayTitle(effect), GetInspectorEffectDescription(cardData, effect, appliedRuleCopy), effect, null);
                    addedEntry = true;
                }
            }

            if (cardData.attachedItemCard != null)
            {
                AbilityEffectData itemEffect = GetPrimaryKeywordEffect(cardData.attachedItemCard);
                string itemDetail = itemEffect != null && itemEffect.keyword != AbilityKeyword.None
                    ? GetAppliedInspectorEffectDescription(itemEffect)
                    : cardData.attachedItemCard.GetDetailedAbilityText();
                AddInspectorRuleEntry(
                    container,
                    $"ITEM - {cardData.attachedItemCard.cardName.ToUpper()}",
                    itemDetail,
                    null,
                    cardData.attachedItemCard.customArt);
                addedEntry = true;
            }

            if (!addedEntry)
            {
                string baseRules = cardData.GetDetailedAbilityText();
                if (!string.IsNullOrWhiteSpace(baseRules))
                {
                    AddInspectorRuleEntry(container, GetFallbackRuleEntryTitle(cardData), baseRules.Trim(), null, null);
                    addedEntry = true;
                }
            }

            if (!addedEntry)
            {
                AddInspectorRuleEntry(container, "PLAIN CARD", "Upgrade with orders or items to add rules.", null, null);
            }
        }

        private void AddInspectorRuleEntry(VisualElement container, string title, string detail, AbilityEffectData effect, Sprite iconSprite, string extraIconClass = null)
        {
            var row = new VisualElement();
            row.AddToClassList("inspector-rule-row");

            var icon = new VisualElement();
            icon.AddToClassList("inspector-rule-icon");
            if (!string.IsNullOrWhiteSpace(extraIconClass))
            {
                icon.AddToClassList(extraIconClass);
            }
            if (effect != null && effect.keyword != AbilityKeyword.None)
            {
                ApplyKeywordBadgeClasses(icon, effect);
                ApplyStatusBadgeSprite(icon, null);
            }
            else
            {
                ApplyStatusBadgeSprite(icon, iconSprite);
            }

            var copy = new VisualElement();
            copy.AddToClassList("inspector-rule-copy");

            var titleLabel = new Label(title ?? string.Empty);
            titleLabel.AddToClassList("inspector-rule-title");
            copy.Add(titleLabel);

            if (!string.IsNullOrWhiteSpace(detail))
            {
                var detailLabel = new Label(detail.Trim());
                detailLabel.AddToClassList("inspector-rule-detail");
                copy.Add(detailLabel);
            }

            row.Add(icon);
            row.Add(copy);
            container.Add(row);
        }

        private void UpdateInspectorOverlay()
        {
            var overlay = _root.Q<VisualElement>("overlay-scrim");
            if (overlay == null) return;

            if (isInspectorOverlayOpen && detailedCardData != null)
            {
                overlay.BringToFront();
                overlay.style.display = DisplayStyle.Flex;
                overlay.pickingMode = PickingMode.Position;
                overlay.RemoveFromClassList("overlay-hidden");

                // Bind detailed popup fields
                var overlayName = overlay.Q<Label>("overlay-card-name");
                if (overlayName != null) overlayName.text = detailedCardData.cardName.ToUpper();

                var overlayCost = overlay.Q<Label>("overlay-card-cost");
                if (overlayCost != null)
                {
                    int effectiveCost = GetEffectiveDeploymentCost(detailedCardData, _localSeat);
                    overlayCost.text = effectiveCost.ToString();
                    overlayCost.style.color = effectiveCost < detailedCardData.treasuryCost
                        ? new StyleColor(new Color(0.09f, 0.64f, 0.29f))
                        : new StyleColor(new Color(0.2f, 0.2f, 0.2f));
                }

                var overlayType = overlay.Q<Label>("overlay-card-type");
                if (overlayType != null) overlayType.text = GetCardTypeLabel(detailedCardData);

                var overlayHealth = overlay.Q<Label>("overlay-card-health");
                if (overlayHealth != null)
                {
                    int currentHealth = GetDetailedCardCurrentHealth(detailedCardData);
                    int printedHealth = GetPrintedHealthValue(detailedCardData);
                    overlayHealth.text = $"{currentHealth}/{printedHealth}";
                }

                var overlayAttack = overlay.Q<Label>("overlay-card-attack");
                if (overlayAttack != null) overlayAttack.text = Mathf.Max(0, detailedCardData.attack).ToString();

                var overlayRange = overlay.Q<Label>("overlay-card-range");
                if (overlayRange != null) overlayRange.text = $"Attack Range {Mathf.Max(0, detailedCardData.range)}\nMovement Range {Mathf.Max(0, detailedCardData.movementRange)}";

                var overlayRules = overlay.Q<VisualElement>("overlay-card-rules");
                if (overlayRules != null) PopulateInspectorRuleEntries(overlayRules, detailedCardData);

                var overlayArt = overlay.Q<VisualElement>("overlay-card-art");
                if (overlayArt != null)
                {
                    overlayArt.style.backgroundImage = StyleKeyword.Null;
                    ApplyGeneratedCardArtClasses(overlayArt, detailedCardData);
                    if (detailedCardData.customArt != null)
                    {
                        overlayArt.style.backgroundImage = new StyleBackground(detailedCardData.customArt);
                    }
                }
            }
            else
            {
                overlay.AddToClassList("overlay-hidden");
                overlay.pickingMode = PickingMode.Ignore;
                overlay.schedule.Execute(() =>
                {
                    if (!isInspectorOverlayOpen)
                    {
                        overlay.style.display = DisplayStyle.None;
                    }
                }).StartingIn(250);
            }
        }

        private void OpenPileViewer(PileViewerKind kind)
        {
            if (kind == PileViewerKind.None)
            {
                return;
            }

            _pileViewerKind = kind;
            _lastPileViewerSignature = string.Empty;
            isInspectorOverlayOpen = false;
            UpdateUI();
        }

        private void ClosePileViewer()
        {
            _pileViewerKind = PileViewerKind.None;
            _lastPileViewerSignature = string.Empty;
            UpdateUI();
        }

        private void OpenWarShop()
        {
            if (_roundPhase != MatchRoundPhase.CombatPlanning)
            {
                return;
            }

            if (_activeTurnSeat != _localSeat)
            {
                ShowInvalidActionAndClearSelection("War Shop only opens on your attack turn.");
                return;
            }

            if (HasUsedWarShopPurchase(_localSeat))
            {
                ShowInvalidActionAndClearSelection("War Shop already used this attack turn.");
                return;
            }

            _warShopOverlayOpen = true;
            _pileViewerKind = PileViewerKind.None;
            isInspectorOverlayOpen = false;
            _highlightedCardIndex = -1;
            _selectedAttackerTileIndex = -1;
            _selectedBoardTileIndex = -1;
            SetAbilityPreviewCard(null);
            UpdateUI();
        }

        private void CloseWarShop()
        {
            _warShopOverlayOpen = false;
            UpdateUI();
        }

        private void OpenEncyclopedia()
        {
            _encyclopediaOpen = true;
            _pileViewerKind = PileViewerKind.None;
            _warShopOverlayOpen = false;
            isInspectorOverlayOpen = false;
            UpdateUI();
        }

        private void CloseEncyclopedia()
        {
            _encyclopediaOpen = false;
            UpdateUI();
        }

        private void SelectEncyclopediaTab(int tabIndex)
        {
            _encyclopediaTabIndex = Mathf.Max(0, tabIndex);
            UpdateUI();
        }

        private static List<EncyclopediaSectionData> BuildEncyclopediaSections()
        {
            return new List<EncyclopediaSectionData>
            {
                new EncyclopediaSectionData
                {
                    TabLabel = "START",
                    Title = "Start Here",
                    Body = "<b>Deploy</b> is for placing cards and setting movement. <b>Attack</b> is for setting strikes, siege, and city pressure.\n\nMost turns are simple: <b>play</b>, <b>preview</b>, then tap <b>Ready</b>.\n\nIf you do nothing, units usually default <b>forward</b>."
                },
                new EncyclopediaSectionData
                {
                    TabLabel = "FLOW",
                    Title = "Round Flow",
                    Body = "<b>Deploy</b>: place units, buildings, orders, and items.\n\n<b>Move Resolve</b>: planned movement and struggles animate.\n\n<b>Attack</b>: choose targets or leave the default forward attack.\n\n<b>Attack Resolve</b>: strikes, siege, deaths, and city damage play out one by one."
                },
                new EncyclopediaSectionData
                {
                    TabLabel = "TIMING",
                    Title = "Turn Timers",
                    Body = "<b>Deploy</b> gives each side up to <b>60s</b>. <b>Attack</b> gives each side up to <b>30s</b>.\n\nThe glowing border around the board is the live countdown. When it drains away, that planning turn ends.\n\n<b>Testing</b> mode has <b>no timer</b>."
                },
                new EncyclopediaSectionData
                {
                    TabLabel = "CARDS",
                    Title = "Card Types",
                    Body = "<b>Civilian</b> and <b>Military</b> are your main units. <b>Special</b> units are rarer power pieces.\n\n<b>Buildings</b> can only be placed on your base tiles. <b>Orders</b> apply keyword effects. <b>Items</b> attach to friendly units.\n\nHold any card to see its full rule text."
                },
                new EncyclopediaSectionData
                {
                    TabLabel = "FATES",
                    Title = "What Returns",
                    Body = "<b>Civilian</b> and <b>Military</b> cards go to <b>discard</b> when they die.\n\n<b>Special</b> units and <b>Buildings</b> are usually <b>gone for the match</b> once destroyed.\n\n<b>Orders</b> go to discard after use. <b>Items</b> return to discard when their carrier dies. <b>Lock</b> is a system card and does not belong to your deck.\n\nCards do not jump straight back to hand. Discarded cards return only when your deck is reshuffled and drawn again."
                },
                new EncyclopediaSectionData
                {
                    TabLabel = "HOLD",
                    Title = "Hold To Inspect",
                    Body = "<b>Tap</b> is for normal play. <b>Hold</b> opens a large detail view.\n\nYou can hold cards in your <b>hand</b>, on the <b>board</b>, and inside <b>Deck</b> or <b>Discard</b> viewers.\n\nThe detail card shows printed stats, current bonuses, and the full wording of attached effects."
                },
                new EncyclopediaSectionData
                {
                    TabLabel = "PLAN",
                    Title = "Planning Better",
                    Body = "<b>Deploy</b>: watch movement arrows before locking in. A unit behind another unit may still move if that lane is clearing this turn.\n\n<b>Attack</b>: focus fire matters. Multiple attacks into one target can open lanes or set up breach.\n\nAlways check enemy badges for <b>Lock</b>, <b>Items</b>, and applied <b>Orders</b>."
                },
                new EncyclopediaSectionData
                {
                    TabLabel = "KEYS",
                    Title = "Keywords",
                    Body = "<b>Intercept</b> blocks damage. <b>Provoke</b> forces attacks if able. <b>Sprint</b> increases movement. <b>Maneuver</b> allows broader movement. <b>Secure</b> and <b>Reclaim</b> change territory.\n\nNumeric keywords usually <b>stack</b>. Rule-changing keywords usually <b>do not</b>.\n\nBadge icons on cards show what is currently attached."
                },
                new EncyclopediaSectionData
                {
                    TabLabel = "ICONS",
                    Title = "Icons & Highlights",
                    Body = "<b>Blue</b> and <b>red</b> frames show ownership. Bright tile glows show legal placement, movement, or attack targets.\n\nSmall badge icons on cards show <b>lock</b>, applied <b>ability</b>, and attached <b>item</b>.\n\nIf HP or AT changes colour, that value is being previewed or modified by buffs, debuffs, or incoming resolve."
                },
                new EncyclopediaSectionData
                {
                    TabLabel = "COMBAT",
                    Title = "Combat Rules",
                    Body = "<b>Attack Range</b> decides how far a unit can strike. <b>Movement Range</b> decides how far it can travel in Deploy.\n\n<b>Siege</b> hits enemy base tiles from outside. Once a lane is broken open, units can pressure the <b>city</b>.\n\nSome effects change legal targets, so if a click says <b>invalid</b>, read the awareness box for the reason."
                },
                new EncyclopediaSectionData
                {
                    TabLabel = "SHOP",
                    Title = "War Shop",
                    Body = "During <b>Attack</b>, you can enter the <b>War Shop</b> once per turn for an emergency purchase.\n\nThese are not deck cards. They are expensive backup tools for <b>full heals</b>, <b>obliterating a target</b>, <b>rebuilding</b>, or <b>claiming</b> a last-second tile.\n\nUse them when the board state matters more than hand tempo."
                }
            };
        }

        private void UpdateEncyclopediaOverlay()
        {
            var overlay = _root.Q<VisualElement>("encyclopedia-overlay");
            if (overlay == null)
            {
                return;
            }

            if (!_encyclopediaOpen)
            {
                overlay.style.display = DisplayStyle.None;
                overlay.pickingMode = PickingMode.Ignore;
                return;
            }

            overlay.style.display = DisplayStyle.Flex;
            overlay.pickingMode = PickingMode.Position;
            overlay.BringToFront();

            List<EncyclopediaSectionData> sections = BuildEncyclopediaSections();
            if (sections.Count == 0)
            {
                return;
            }

            _encyclopediaTabIndex = Mathf.Clamp(_encyclopediaTabIndex, 0, sections.Count - 1);
            EncyclopediaSectionData activeSection = sections[_encyclopediaTabIndex];

            var status = overlay.Q<Label>("encyclopedia-status");
            if (status != null)
            {
                status.text = "Tap any tab. Nothing here is forced.";
            }

            var tabStrip = overlay.Q<VisualElement>("encyclopedia-tab-strip");
            if (tabStrip != null)
            {
                tabStrip.Clear();
                for (int tabIndex = 0; tabIndex < sections.Count; tabIndex++)
                {
                    int capturedIndex = tabIndex;
                    var tabButton = new Button(() => SelectEncyclopediaTab(capturedIndex))
                    {
                        text = sections[tabIndex].TabLabel
                    };
                    tabButton.AddToClassList("encyclopedia-tab-button");
                    tabButton.AddToClassList(tabIndex == _encyclopediaTabIndex
                        ? "encyclopedia-tab-button-active"
                        : "encyclopedia-tab-button-inactive");
                    tabStrip.Add(tabButton);
                }
            }

            var content = overlay.Q<VisualElement>("encyclopedia-content");
            if (content == null)
            {
                return;
            }

            content.Clear();

            var sectionCard = new VisualElement();
            sectionCard.AddToClassList("encyclopedia-section-card");
            content.Add(sectionCard);

            var title = new Label(activeSection.Title);
            title.AddToClassList("encyclopedia-section-title");
            sectionCard.Add(title);

            var body = new Label(activeSection.Body);
            body.AddToClassList("encyclopedia-section-body");
            body.enableRichText = true;
            sectionCard.Add(body);
        }

        private MatchSeat GetPileViewerSeat()
        {
            return UsesHotseatControlMode() ? _activeTurnSeat : _localSeat;
        }

        private void UpdatePileViewer()
        {
            var overlay = _root.Q<VisualElement>("pile-viewer-overlay");
            if (overlay == null)
            {
                return;
            }

            if (_pileViewerKind == PileViewerKind.None
                || _awaitingLaunchModeSelection
                || _reconnectOverlayVisible
                || _arenaSelectionActive)
            {
                overlay.style.display = DisplayStyle.None;
                overlay.pickingMode = PickingMode.Ignore;
                _lastPileViewerSignature = string.Empty;
                return;
            }

            MatchSeat viewerSeat = GetPileViewerSeat();
            ParticipantRuntimeState state = GetRuntimeState(viewerSeat);
            List<CardTemplate> pile = _pileViewerKind == PileViewerKind.Deck
                ? (state != null ? state.drawPile : null)
                : (state != null ? state.discardPile : null);
            int count = pile != null ? pile.Count : 0;
            string signature = BuildPileViewerSignature(_pileViewerKind, viewerSeat, pile);

            overlay.style.display = DisplayStyle.Flex;
            overlay.pickingMode = PickingMode.Position;

            var title = overlay.Q<Label>("pile-viewer-title");
            if (title != null)
            {
                string pileName = _pileViewerKind == PileViewerKind.Deck ? "DECK" : "DISCARD";
                title.text = $"{pileName} ({count})";
            }

            if (_lastPileViewerSignature == signature)
            {
                return;
            }

            _lastPileViewerSignature = signature;
            var scroll = overlay.Q<ScrollView>("pile-viewer-scroll");
            VisualElement container = scroll != null ? scroll.contentContainer : null;
            if (container == null)
            {
                return;
            }

            container.Clear();
            if (pile == null || pile.Count == 0)
            {
                var empty = new Label(_pileViewerKind == PileViewerKind.Deck ? "No cards left in deck." : "Discard is empty.");
                empty.AddToClassList("pile-card-empty");
                container.Add(empty);
                return;
            }

            for (int i = 0; i < pile.Count; i++)
            {
                container.Add(CreatePileCardElement(pile[i], viewerSeat, i));
            }
        }

        private void UpdateWarShopUi()
        {
            var dock = _root.Q<VisualElement>("war-shop-dock");
            var dockCopy = _root.Q<Label>("war-shop-dock-copy");
            var overlay = _root.Q<VisualElement>("war-shop-overlay");
            if (dock == null || overlay == null)
            {
                return;
            }

            bool showDock = _roundPhase == MatchRoundPhase.CombatPlanning
                && !_awaitingLaunchModeSelection
                && !_reconnectOverlayVisible
                && !_arenaSelectionActive;
            bool canUseShop = showDock && _activeTurnSeat == _localSeat && !HasUsedWarShopPurchase(_localSeat);

            dock.EnableInClassList("war-shop-hidden", !showDock);
            dock.EnableInClassList("war-shop-dock-disabled", showDock && !canUseShop);
            if (dockCopy != null)
            {
                dockCopy.text = HasUsedWarShopPurchase(_activeTurnSeat)
                    ? "Used this turn"
                    : "1 max purchase per turn";
            }

            if (!showDock || !_warShopOverlayOpen)
            {
                overlay.style.display = DisplayStyle.None;
                overlay.pickingMode = PickingMode.Ignore;
                return;
            }

            overlay.style.display = DisplayStyle.Flex;
            overlay.pickingMode = PickingMode.Position;

            var status = overlay.Q<Label>("war-shop-status");
            if (status != null)
            {
                status.text = canUseShop
                    ? "Choose one emergency purchase for this attack turn."
                    : "War Shop is already spent for this attack turn.";
            }

            var scroll = overlay.Q<ScrollView>("war-shop-scroll");
            VisualElement container = scroll != null ? scroll.contentContainer : null;
            if (container == null)
            {
                return;
            }

            container.Clear();
            for (int i = 0; i < 4; i++)
            {
                WarShopOption option = (WarShopOption)i;
                container.Add(CreateWarShopOptionElement(option, canUseShop));
            }
        }

        private VisualElement CreateWarShopOptionElement(WarShopOption option, bool interactive)
        {
            CardTemplate optionCard = CreateWarShopOptionCard(option);
            VisualElement cardInstance = cardThumbnailTemplate != null ? cardThumbnailTemplate.Instantiate() : new VisualElement();
            cardInstance.AddToClassList("pile-card-thumbnail-shell");
            cardInstance.style.marginLeft = 10;
            cardInstance.style.marginRight = 10;
            cardInstance.style.marginTop = 10;
            cardInstance.style.marginBottom = 10;

            VisualElement cardRoot = BindCardThumbnail(cardInstance, optionCard, _localSeat, interactive, GetSelectedWarShopOption() == option);
            if (cardRoot != null)
            {
                cardRoot.AddToClassList("pile-card-thumbnail");
                if (!interactive)
                {
                    cardRoot.style.opacity = 0.56f;
                }
            }

            RegisterCardHoldToInspect(cardInstance, () => optionCard);
            cardInstance.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                if (!interactive || _cardHoldDetailOpened)
                {
                    _cardHoldDetailOpened = false;
                    return;
                }

                bool dispatched = TryDispatchUiAction(new MatchUiAction
                {
                    actionType = MatchUiActionType.SelectWarShopOption,
                    handIndex = (int)option
                });

                if (dispatched)
                {
                    HandleWarShopOptionSelected(option);
                    return;
                }

                if (!IsRemoteReplica())
                {
                    HandleWarShopOptionSelected(option);
                }
            });

            return cardInstance;
        }

        private CardTemplate CreateWarShopOptionCard(WarShopOption option)
        {
            CardTemplate card = ScriptableObject.CreateInstance<CardTemplate>();
            card.cardId = $"card.system.warshop.{option.ToString().ToLowerInvariant()}";
            card.cardType = CardType.Ordinance;
            card.unitTag = UnitTag.None;
            card.infrastructureKind = InfrastructureKind.None;
            card.commandCardKind = CommandCardKind.None;
            card.health = 0;
            card.attack = 0;
            card.range = 0;
            card.movementRange = 0;

            switch (option)
            {
                case WarShopOption.FieldMedic:
                    card.cardName = "Field Medic";
                    card.treasuryCost = WarShopFieldMedicCost;
                    card.abilityText = "Restore a friendly unit to full health.";
                    card.detailedAbilityText = "Choose a friendly unit. Restore it to its printed maximum health instantly.";
                    break;
                case WarShopOption.BombDrop:
                    card.cardName = "Bomb Drop";
                    card.treasuryCost = WarShopBombDropCost;
                    card.abilityText = "Destroy an enemy unit, building, or base tile.";
                    card.detailedAbilityText = "Choose an enemy unit, building, or base tile. Destroy it instantly.";
                    break;
                case WarShopOption.FrontierClaim:
                    card.cardName = "Frontier Claim";
                    card.treasuryCost = WarShopFrontierClaimCost;
                    card.abilityText = $"Create a {WarShopFrontierClaimHealth} HP base tile on connected Freespace.";
                    card.detailedAbilityText = $"Choose an empty Freespace tile orthogonally adjacent to your base network. It immediately becomes a {WarShopFrontierClaimHealth} HP friendly base tile.";
                    break;
                case WarShopOption.RebuildOrder:
                    card.cardName = "Rebuild Order";
                    card.treasuryCost = WarShopRebuildOrderCost;
                    card.abilityText = "Scrap a friendly building and restore its tile to 20 HP base.";
                    card.detailedAbilityText = "Choose one of your buildings. Remove it, return that building to discard, and restore the tile beneath it to a healthy 20 HP base tile.";
                    break;
                default:
                    card.cardName = "War Shop";
                    card.treasuryCost = 0;
                    card.abilityText = string.Empty;
                    card.detailedAbilityText = string.Empty;
                    break;
            }

            return card;
        }

        private void HandleWarShopOptionSelected(WarShopOption option)
        {
            if (_roundPhase != MatchRoundPhase.CombatPlanning || _activeTurnSeat != _localSeat)
            {
                return;
            }

            if (HasUsedWarShopPurchase(_localSeat))
            {
                ShowInvalidActionAndClearSelection("War Shop already used this attack turn.");
                return;
            }

            if (!CanAffordWarShopOption(option, _localSeat))
            {
                ShowInvalidActionAndClearSelection($"Not enough treasury for {CreateWarShopOptionCard(option).cardName}.");
                return;
            }

            SetSelectedWarShopOption(option);
            _highlightedCardIndex = -1;
            _selectedAttackerTileIndex = -1;
            _selectedBoardTileIndex = -1;
            _warShopOverlayOpen = false;
            SetAbilityPreviewText($"War Shop: choose a target for {CreateWarShopOptionCard(option).cardName}.");
            UpdateUI();
        }

        private int GetWarShopOptionCost(WarShopOption option)
        {
            switch (option)
            {
                case WarShopOption.FieldMedic:
                    return WarShopFieldMedicCost;
                case WarShopOption.BombDrop:
                    return WarShopBombDropCost;
                case WarShopOption.FrontierClaim:
                    return WarShopFrontierClaimCost;
                case WarShopOption.RebuildOrder:
                    return WarShopRebuildOrderCost;
                default:
                    return 0;
            }
        }

        private bool CanAffordWarShopOption(WarShopOption option, MatchSeat seat)
        {
            ParticipantRuntimeState state = GetRuntimeState(seat);
            return state != null && state.treasury >= GetWarShopOptionCost(option);
        }

        private bool CanApplyWarShopOptionToTile(WarShopOption option, int tileIndex, MatchSeat seat)
        {
            if (option == WarShopOption.None
                || tileIndex < 0
                || tileIndex >= _boardTileData.Length)
            {
                return false;
            }

            CardTemplate tileCard = _boardTileData[tileIndex];
            MatchSeat enemySeat = MatchPerspectiveUtility.GetOpposingSeat(seat);
            switch (option)
            {
                case WarShopOption.FieldMedic:
                    return tileCard != null
                        && !IsSystemRuntimeCard(tileCard)
                        && IsUnitCard(tileCard)
                        && _tileOccupantSeats[tileIndex].HasValue
                        && _tileOccupantSeats[tileIndex].Value == seat
                        && _occupantCurrentHealth[tileIndex] > 0
                        && _occupantCurrentHealth[tileIndex] < GetPrintedHealthValue(tileCard);

                case WarShopOption.BombDrop:
                    if (tileCard != null)
                    {
                        return !IsSystemRuntimeCard(tileCard)
                            && _tileOccupantSeats[tileIndex].HasValue
                            && _tileOccupantSeats[tileIndex].Value == enemySeat
                            && _occupantCurrentHealth[tileIndex] > 0;
                    }

                    return IsLiveEnemyBaseTileForSeat(tileIndex, seat);

                case WarShopOption.FrontierClaim:
                    return tileCard == null
                        && _tileAreaKinds[tileIndex] == TileAreaKind.Freeplay
                        && HasOrthogonalAdjacentFriendlyBase(tileIndex, GetTileOwnerForSeat(seat));

                case WarShopOption.RebuildOrder:
                    return tileCard != null
                        && IsInfrastructureCard(tileCard)
                        && _tileOccupantSeats[tileIndex].HasValue
                        && _tileOccupantSeats[tileIndex].Value == seat
                        && _occupantCurrentHealth[tileIndex] > 0;

                default:
                    return false;
            }
        }

        private string GetInvalidWarShopTargetReason(WarShopOption option, int tileIndex, MatchSeat seat)
        {
            string optionName = CreateWarShopOptionCard(option).cardName;
            if (option == WarShopOption.None)
            {
                return "Choose a War Shop purchase first.";
            }

            if (!CanAffordWarShopOption(option, seat))
            {
                return $"Not enough treasury for {optionName}.";
            }

            if (tileIndex < 0 || tileIndex >= _boardTileData.Length)
            {
                return $"{optionName} needs a valid board target.";
            }

            CardTemplate tileCard = _boardTileData[tileIndex];
            switch (option)
            {
                case WarShopOption.FieldMedic:
                    if (tileCard == null || !_tileOccupantSeats[tileIndex].HasValue || _tileOccupantSeats[tileIndex].Value != seat)
                    {
                        return $"{optionName} must target a friendly unit.";
                    }

                    if (!IsUnitCard(tileCard) || IsSystemRuntimeCard(tileCard))
                    {
                        return $"{optionName} can only heal real friendly units.";
                    }

                    if (_occupantCurrentHealth[tileIndex] >= GetPrintedHealthValue(tileCard))
                    {
                        return $"{tileCard.cardName} is already at full health.";
                    }

                    return $"{optionName} cannot target {tileCard.cardName}.";

                case WarShopOption.BombDrop:
                    if (tileCard != null)
                    {
                        return _tileOccupantSeats[tileIndex].HasValue && _tileOccupantSeats[tileIndex].Value == seat
                            ? $"{optionName} can only hit enemy cards."
                            : $"{optionName} cannot target that card.";
                    }

                    return IsLiveEnemyBaseTileForSeat(tileIndex, seat)
                        ? $"{optionName} cannot target that base tile right now."
                        : $"{optionName} must target an enemy card or enemy base tile.";

                case WarShopOption.FrontierClaim:
                    if (tileCard != null)
                    {
                        return $"{optionName} needs an empty Freespace tile.";
                    }

                    if (_tileAreaKinds[tileIndex] != TileAreaKind.Freeplay)
                    {
                        return $"{optionName} can only claim Freespace.";
                    }

                    return $"{optionName} must touch your base network orthogonally.";

                case WarShopOption.RebuildOrder:
                    if (tileCard == null || !_tileOccupantSeats[tileIndex].HasValue || _tileOccupantSeats[tileIndex].Value != seat)
                    {
                        return $"{optionName} must target one of your buildings.";
                    }

                    if (!IsInfrastructureCard(tileCard))
                    {
                        return $"{optionName} can only target a friendly building.";
                    }

                    return $"{optionName} cannot target {tileCard.cardName}.";

                default:
                    return $"{optionName} has no valid target there.";
            }
        }

        private bool TryApplyWarShopOptionToTile(WarShopOption option, int tileIndex, MatchSeat seat)
        {
            if (_roundPhase != MatchRoundPhase.CombatPlanning
                || option == WarShopOption.None
                || seat != _activeTurnSeat
                || HasUsedWarShopPurchase(seat)
                || !CanApplyWarShopOptionToTile(option, tileIndex, seat)
                || !CanAffordWarShopOption(option, seat))
            {
                return false;
            }

            ParticipantRuntimeState state = GetRuntimeState(seat);
            if (state == null)
            {
                return false;
            }

            string optionName = CreateWarShopOptionCard(option).cardName;
            int cost = GetWarShopOptionCost(option);

            switch (option)
            {
                case WarShopOption.FieldMedic:
                {
                    CardTemplate targetCard = _boardTileData[tileIndex];
                    int printedHealth = GetPrintedHealthValue(targetCard);
                    int currentHealth = _occupantCurrentHealth[tileIndex];
                    int healedHealth = Mathf.Max(currentHealth, printedHealth);
                    int healedAmount = Mathf.Max(0, healedHealth - currentHealth);
                    _occupantCurrentHealth[tileIndex] = healedHealth;
                    AddFloatingBoardText(tileIndex, $"+{healedAmount}");
                    ShowAwarenessMessage($"<b>{GetSeatDisplayName(seat)} used {optionName} on {targetCard.cardName}, restoring it to full health.</b>", 2.4f);
                    break;
                }

                case WarShopOption.BombDrop:
                {
                    CardTemplate targetCard = _boardTileData[tileIndex];
                    if (targetCard != null)
                    {
                        if (IsInfrastructureCard(targetCard))
                        {
                            int mergedHealth = Mathf.Max(0, _tileCurrentHealth[tileIndex]);
                            ApplyMergedInfrastructureTileDamage(tileIndex, mergedHealth);
                        }
                        else
                        {
                            int currentHealth = Mathf.Max(0, _occupantCurrentHealth[tileIndex]);
                            _occupantCurrentHealth[tileIndex] = 0;
                            if (currentHealth > 0)
                            {
                                AddFloatingBoardText(tileIndex, $"-{currentHealth}");
                            }

                            RemoveOccupantAtTile(tileIndex, true);
                        }
                    }
                    else
                    {
                        int tileHealth = Mathf.Max(0, _tileCurrentHealth[tileIndex]);
                        ApplyBaseTileDamage(tileIndex, tileHealth, seat, -1);
                    }

                    string targetName = targetCard != null
                        ? targetCard.cardName
                        : GetBaseTileDisplayName(tileIndex);
                    ShowAwarenessMessage($"<b>{GetSeatDisplayName(seat)} dropped {optionName} on {targetName} and obliterated it.</b>", 2.4f);
                    break;
                }

                case WarShopOption.FrontierClaim:
                    _tileAreaKinds[tileIndex] = TileAreaKind.Base;
                    _tileOwners[tileIndex] = GetTileOwnerForSeat(seat);
                    _tileMaxHealth[tileIndex] = WarShopFrontierClaimHealth;
                    _tileCurrentHealth[tileIndex] = WarShopFrontierClaimHealth;
                    _tileBlocksCity[tileIndex] = true;
                    _secureHoldTurnsByTile[tileIndex] = 0;
                    AddFloatingBoardText(tileIndex, "CLAIM", "tile-floating-status");
                    ShowAwarenessMessage($"<b>{GetSeatDisplayName(seat)} used {optionName} to claim a new base tile.</b>", 2.4f);
                    break;

                case WarShopOption.RebuildOrder:
                {
                    CardTemplate removedBuilding = _boardTileData[tileIndex];
                    RemoveOccupantAtTile(tileIndex, false);
                    if (removedBuilding != null)
                    {
                        DiscardCardForSeat(seat, removedBuilding);
                    }

                    _tileAreaKinds[tileIndex] = TileAreaKind.Base;
                    _tileOwners[tileIndex] = GetTileOwnerForSeat(seat);
                    _tileMaxHealth[tileIndex] = 20;
                    _tileCurrentHealth[tileIndex] = 20;
                    _tileBlocksCity[tileIndex] = true;
                    AddFloatingBoardText(tileIndex, "REBUILD", "tile-floating-status");
                    ShowAwarenessMessage($"<b>{GetSeatDisplayName(seat)} used {optionName} to restore that tile to base form.</b>", 2.4f);
                    break;
                }
            }

            state.treasury = Mathf.Max(0, state.treasury - cost);
            SetWarShopPurchaseUsed(seat, true);
            SetSelectedWarShopOption(WarShopOption.None);
            _warShopOverlayOpen = false;
            _selectedBoardTileIndex = tileIndex;
            _selectedAttackerTileIndex = -1;
            _highlightedCardIndex = -1;
            SetAbilityPreviewText($"{optionName} resolved.");
            SyncVisibleStateFromPerspective();
            return true;
        }

        private string BuildPileViewerSignature(PileViewerKind kind, MatchSeat seat, List<CardTemplate> pile)
        {
            int count = pile != null ? pile.Count : 0;
            var builder = new System.Text.StringBuilder();
            builder.Append(kind).Append('|').Append(seat).Append('|').Append(count);
            if (pile != null)
            {
                for (int i = 0; i < pile.Count; i++)
                {
                    CardTemplate card = pile[i];
                    builder.Append('|').Append(card != null ? card.cardId : "null");
                }
            }

            return builder.ToString();
        }

        private VisualElement CreatePileCardElement(CardTemplate card, MatchSeat viewerSeat, int index)
        {
            EnsureCardThumbnailTemplate();
            if (cardThumbnailTemplate == null || card == null)
            {
                var fallback = new Label(card != null ? $"{index + 1}. {card.cardName}" : $"{index + 1}. Unknown card");
                fallback.AddToClassList("pile-card-empty");
                return fallback;
            }

            VisualElement cardInstance = cardThumbnailTemplate.Instantiate();
            cardInstance.AddToClassList("pile-card-thumbnail-shell");
            cardInstance.style.marginLeft = 10;
            cardInstance.style.marginRight = 10;
            cardInstance.style.marginTop = 10;
            cardInstance.style.marginBottom = 10;

            VisualElement cardRoot = BindCardThumbnail(cardInstance, card, viewerSeat, true, false);
            if (cardRoot != null)
            {
                cardRoot.AddToClassList("pile-card-thumbnail");
            }
            RegisterCardHoldToInspect(cardInstance, () => card);

            return cardInstance;
        }

        private void UpdateHUDVisibility()
        {
            var topHUD = _root.Q<VisualElement>("top-hud");
            var bottomHUD = _root.Q<VisualElement>("bottom-hud");
            var controlsRow = _root.Q<VisualElement>(className: "controls-row");
            var handCarousel = _root.Q<ScrollView>("hand-carousel");

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
                if (_hudHidden || _placementFocusActive)
                {
                    controlsRow.AddToClassList("hud-hidden");
                }
                else
                {
                    controlsRow.RemoveFromClassList("hud-hidden");
                }
            }

            if (handCarousel != null)
            {
                bool hideCardsOnly = _roundPhase == MatchRoundPhase.CombatPlanning;
                handCarousel.EnableInClassList("hand-carousel-hidden", hideCardsOnly);
                handCarousel.pickingMode = hideCardsOnly ? PickingMode.Ignore : PickingMode.Position;
            }
        }

        private void RegisterEvents()
        {
            if (_root == null || _eventsRegistered) return;

            _eventsRegistered = true;

            RegisterLaunchModeButton("mode-turn-based-button", MatchLaunchMode.TurnBased);
            RegisterLaunchModeButton("mode-testing-button", MatchLaunchMode.Testing);
            RegisterTutorialModeButton();
            RegisterCitySelectionButton("mode-city-freehaven-button", MatchSeat.SeatOne);
            RegisterCitySelectionButton("mode-city-citadel-button", MatchSeat.SeatTwo);
            RegisterLaunchModeButton("mode-online-quickmatch-button", MatchLaunchMode.OnlineQuickMatch);
            RegisterWebInstallButton();
            RegisterArenaChoiceButton("arena-freehaven-garden-button", ArenaId.FreehavenGarden);
            RegisterArenaChoiceButton("arena-citadel-training-button", ArenaId.CitadelTrainingGrounds);

            var reconnectBackButton = _root.Q<Button>("reconnect-back-button");
            if (reconnectBackButton != null)
            {
                reconnectBackButton.clicked += () =>
                {
                    if (ReconnectBackToMenuRequested != null)
                    {
                        ReconnectBackToMenuRequested.Invoke();
                    }
                    else
                    {
                        ShowLaunchModePicker("Reconnect cancelled.");
                    }
                };
            }

            var matchEndBackButton = _root.Q<Button>("match-end-back-button");
            if (matchEndBackButton != null)
            {
                matchEndBackButton.clicked += () =>
                {
                    if (TryDispatchUiAction(new MatchUiAction
                        {
                            actionType = MatchUiActionType.BackToMenu
                        }))
                    {
                        return;
                    }

                    RequestBackToMenuAfterMatch();
                };
            }

            // Bind Zoom Panel Buttons
            var zoomInBtn = _root.Q<Button>("zoom-in-button");
            if (zoomInBtn != null)
            {
                zoomInBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation(); // Stop click-away deselect
                    _tileScale = Mathf.Clamp(_tileScale + ZoomStep, GetCurrentMinTileScale(), MaxTileScale);
                    UpdateUI();
                });
            }

            var zoomOutBtn = _root.Q<Button>("zoom-out-button");
            if (zoomOutBtn != null)
            {
                zoomOutBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation(); // Stop click-away deselect
                    _tileScale = Mathf.Clamp(_tileScale - ZoomStep, GetCurrentMinTileScale(), MaxTileScale);
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

            var centerBoardBtn = _root.Q<Button>("center-board-button");
            if (centerBoardBtn != null)
            {
                centerBoardBtn.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    RequestBoardFitAndCenter(true);
                });
            }

            var encyclopediaButton = _root.Q<Button>("encyclopedia-button");
            if (encyclopediaButton != null)
            {
                encyclopediaButton.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    if (_encyclopediaOpen)
                    {
                        CloseEncyclopedia();
                    }
                    else
                    {
                        OpenEncyclopedia();
                    }
                });
            }

            void RegisterCityAttackLabel(string labelName, MatchSeat displayedSeat)
            {
                var cityLabel = _root.Q<Label>(labelName);
                if (cityLabel == null)
                {
                    return;
                }

                cityLabel.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();

                    if (TryDispatchUiAction(new MatchUiAction
                        {
                            actionType = MatchUiActionType.TargetCity,
                            targetSeat = displayedSeat
                        }))
                    {
                        HandleCityAttackClicked(displayedSeat);
                        return;
                    }

                    if (IsRemoteReplica())
                    {
                        ShowInvalidActionAndClearSelection("Waiting for host sync.");
                        return;
                    }

                    HandleCityAttackClicked(displayedSeat);
                });
            }

            RegisterCityAttackLabel("enemy-city-nameplate", MatchPerspectiveUtility.GetOpposingSeat(_perspectiveSeat));
            RegisterCityAttackLabel("player-city-nameplate", _perspectiveSeat);

            // Reset selection when clicking empty space (bubbles up to root visual element)
            _root.RegisterCallback<ClickEvent>(evt =>
            {
                if (_matchEnded || _roundPhase == MatchRoundPhase.DisplayResolution)
                {
                    return;
                }

                bool dispatched = _externalCommandSink != null
                    && _activeTurnSeat == _localSeat
                    && TryDispatchUiAction(new MatchUiAction
                    {
                        actionType = MatchUiActionType.ClearSelection
                    });

                if (dispatched)
                {
                    ClearSelectionsAndRefresh();
                    return;
                }

                ClearSelectionsAndRefresh();
            });

            _root.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                ApplySafeAreaIfNeeded();
                UpdateDesktopDockLayout();
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

                    evt.StopPropagation();
                });

                var closeBtn = overlay.Q<Button>("overlay-close-btn");
                if (closeBtn != null)
                {
                    closeBtn.clicked += CloseInspectorOverlay;
                }
            }

            var pileOverlay = _root.Q<VisualElement>("pile-viewer-overlay");
            if (pileOverlay != null)
            {
                pileOverlay.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.target == pileOverlay)
                    {
                        ClosePileViewer();
                        evt.StopPropagation();
                    }
                });

                var pileCloseButton = pileOverlay.Q<Button>("pile-viewer-close-button");
                if (pileCloseButton != null)
                {
                    pileCloseButton.clicked += ClosePileViewer;
                }
            }

            var warShopDock = _root.Q<VisualElement>("war-shop-dock");
            if (warShopDock != null)
            {
                warShopDock.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    if (_warShopOverlayOpen)
                    {
                        CloseWarShop();
                    }
                    else
                    {
                        OpenWarShop();
                    }
                });
            }

            RegisterMobileDockToggle("mobile-left-dock-toggle", true);
            RegisterMobileDockToggle("mobile-right-dock-toggle", false);
            RegisterMobileDockClose("mobile-left-dock-close", true);
            RegisterMobileDockClose("mobile-right-dock-close", false);

            var mobileDockScrim = _root.Q<VisualElement>("mobile-dock-scrim");
            if (mobileDockScrim != null)
            {
                mobileDockScrim.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    CloseMobileDocks();
                });
            }

            var mobileLeftDockPanel = _root.Q<VisualElement>("mobile-left-dock-panel");
            if (mobileLeftDockPanel != null)
            {
                mobileLeftDockPanel.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            }

            var mobileRightDockPanel = _root.Q<VisualElement>("mobile-right-dock-panel");
            if (mobileRightDockPanel != null)
            {
                mobileRightDockPanel.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            }

            var warShopOverlay = _root.Q<VisualElement>("war-shop-overlay");
            if (warShopOverlay != null)
            {
                warShopOverlay.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.target == warShopOverlay)
                    {
                        CloseWarShop();
                    }

                    evt.StopPropagation();
                });

                var warShopPanel = warShopOverlay.Q<VisualElement>(className: "war-shop-panel");
                if (warShopPanel != null)
                {
                    warShopPanel.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
                }

                var warShopCloseButton = warShopOverlay.Q<Button>("war-shop-close-button");
                if (warShopCloseButton != null)
                {
                    warShopCloseButton.clicked += CloseWarShop;
                }
            }

            var encyclopediaOverlay = _root.Q<VisualElement>("encyclopedia-overlay");
            if (encyclopediaOverlay != null)
            {
                encyclopediaOverlay.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.target == encyclopediaOverlay)
                    {
                        CloseEncyclopedia();
                    }

                    evt.StopPropagation();
                });

                var encyclopediaCloseButton = encyclopediaOverlay.Q<Button>("encyclopedia-close-button");
                if (encyclopediaCloseButton != null)
                {
                    encyclopediaCloseButton.clicked += CloseEncyclopedia;
                }
            }

            var deckContainer = _root.Q<VisualElement>("deck-container");
            if (deckContainer != null)
            {
                deckContainer.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    OpenPileViewer(PileViewerKind.Deck);
                });
            }

            var discardContainer = _root.Q<VisualElement>("discard-container");
            if (discardContainer != null)
            {
                discardContainer.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    OpenPileViewer(PileViewerKind.Discard);
                });
            }

            var endTurnBtn = _root.Q<Button>("end-turn-button");
            if (endTurnBtn != null)
            {
                endTurnBtn.clicked += () =>
                {
                    if (TryDispatchUiAction(new MatchUiAction
                        {
                            actionType = MatchUiActionType.EndTurn
                        }))
                    {
                        return;
                    }

                    if (IsRemoteReplica())
                    {
                        ShowInvalidActionAndClearSelection("Waiting for host sync.");
                        return;
                    }

                    if (_cardDeployInFlight)
                    {
                        Debug.LogWarning("Wait for the current deployment animation to finish before ending the turn.");
                        return;
                    }

                    if (_roundPhase == MatchRoundPhase.DisplayResolution)
                    {
                        return;
                    }

                    AdvancePhaseFromReadyOrTimeout();
                };
            }

            var boardScroll = _root.Q<ScrollView>("board-scroll-view");
            if (boardScroll != null)
            {
                boardScroll.mode = ScrollViewMode.VerticalAndHorizontal;
                RegisterBoardPanInteractions(boardScroll);
                boardScroll.RegisterCallback<GeometryChangedEvent>(evt =>
                {
                    ResetBoardViewToPlayerAnchorIfNeeded();
                });
            }
        }

        private void RegisterLaunchModeButton(string buttonName, MatchLaunchMode launchMode)
        {
            var button = _root.Q<Button>(buttonName);
            if (button == null)
            {
                return;
            }

            button.clicked += () =>
            {
                if (launchMode == MatchLaunchMode.Testing && !ShouldExposeTestingMode())
                {
                    _launchModeStatusText = "Testing mode is only available in admin mode.";
                    UpdateLaunchModeOverlay();
                    return;
                }

                _selectedLaunchMode = launchMode;
                _launchModeOnlineCityStepActive = false;
                switch (launchMode)
                {
                    case MatchLaunchMode.TurnBased:
                        _launchModeStatusText = "Starting turn-based test match...";
                        break;
                    case MatchLaunchMode.Testing:
                        _launchModeStatusText = "Starting testing sandbox...";
                        break;
                    case MatchLaunchMode.OnlineQuickMatch:
                        _launchModeStatusText = "Choose your city for Online Quick Match.";
                        _launchModeOnlineCityStepActive = true;
                        break;
                    case MatchLaunchMode.MultiplayerHost:
                        _launchModeStatusText = "Starting multiplayer host...";
                        break;
                    case MatchLaunchMode.MultiplayerClient:
                        _launchModeStatusText = "Connecting as multiplayer client...";
                        break;
                }

                UpdateLaunchModeOverlay();
                if (launchMode == MatchLaunchMode.OnlineQuickMatch)
                {
                    return;
                }

                if (LaunchModeSelected != null)
                {
                    LaunchModeSelected.Invoke(launchMode);
                }
                else if (launchMode == MatchLaunchMode.TurnBased)
                {
                    StartTurnBasedSession();
                }
                else if (launchMode == MatchLaunchMode.Testing)
                {
                    StartTestingSession();
                }
            };
        }

        private void RegisterCitySelectionButton(string buttonName, MatchSeat selectedSeat)
        {
            var button = _root.Q<Button>(buttonName);
            if (button == null)
            {
                return;
            }

            button.clicked += () =>
            {
                _selectedOnlineSeat = selectedSeat;
                if (_selectedLaunchMode == MatchLaunchMode.OnlineQuickMatch && _launchModeOnlineCityStepActive)
                {
                    _launchModeOnlineCityStepActive = false;
                    _launchModeStatusText = $"Finding {GetOpposingCityName(selectedSeat)} opponent...";
                    UpdateLaunchModeOverlay();
                    OnlineQuickMatchRequested?.Invoke(_selectedOnlineSeat);
                    return;
                }

                _launchModeStatusText = $"{GetSeatDisplayName(selectedSeat)} selected. Online Quick Match will find {GetOpposingCityName(selectedSeat)}.";
                UpdateLaunchModeOverlay();
            };
        }

        private void RegisterTutorialModeButton()
        {
            var button = _root.Q<Button>("mode-tutorial-button");
            if (button == null)
            {
                return;
            }

            button.clicked += () =>
            {
                _launchModeStatusText = "Read the guide, then choose a mode when you're ready.";
                _encyclopediaTabIndex = 0;
                UpdateLaunchModeOverlay();
                OpenEncyclopedia();
            };
        }

        private string GetOpposingCityName(MatchSeat selectedSeat)
        {
            return GetSeatDisplayName(MatchPerspectiveUtility.GetOpposingSeat(selectedSeat));
        }

        private void RegisterWebInstallButton()
        {
            var button = _root.Q<Button>("mode-install-app-button");
            if (button == null)
            {
                return;
            }

            button.clicked += () =>
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                CardzRequestWebAppInstall();
                _launchModeStatusText = "If the browser can install this page, it will ask now. On iPhone/iPad use Share > Add to Home Screen.";
#else
                _launchModeStatusText = "Fullscreen install is only available from the WebGL page.";
#endif
                UpdateLaunchModeOverlay();
            };
        }

        private void RegisterArenaChoiceButton(string buttonName, ArenaId arenaId)
        {
            var button = _root.Q<Button>(buttonName);
            if (button == null)
            {
                return;
            }

            button.clicked += () =>
            {
                var action = new MatchUiAction
                {
                    actionType = MatchUiActionType.ChooseArena,
                    arenaId = arenaId
                };

                Debug.Log($"[UIManager][Arena] Clicked {arenaId}. externalSink={_externalCommandSink != null}, localSeat={_localSeat}, active={_arenaSelectionActive}.");
                if (TryDispatchUiAction(action))
                {
                    PreviewLocalArenaChoice(arenaId);
                    return;
                }

                HandleArenaChoice(arenaId);
            };
        }

        private void HandleCityAttackClicked(MatchSeat displayedSeat)
        {
            if (_roundPhase != MatchRoundPhase.CombatPlanning
                || _selectedAttackerTileIndex < 0
                || _activeTurnSeat != _localSeat
                || !_tileOccupantSeats[_selectedAttackerTileIndex].HasValue
                || _tileOccupantSeats[_selectedAttackerTileIndex].Value != _localSeat)
            {
                return;
            }

            if (displayedSeat == _localSeat || !CanAttackCityDirectlyFromTile(_selectedAttackerTileIndex, _localSeat))
            {
                ShowInvalidActionAndClearSelection(GetInvalidCityAttackReason(_selectedAttackerTileIndex, _localSeat, displayedSeat));
                return;
            }

            _attackTargetTileBySource[_selectedAttackerTileIndex] = ManualCityAttackTargetToken;
            _selectedBoardTileIndex = -1;
            CardTemplate cityAttacker = _boardTileData[_selectedAttackerTileIndex];
            SetAbilityPreviewText("Attack City selected.");
            ShowAwarenessMessage($"{cityAttacker?.cardName ?? "Unit"} will attack {GetSeatDisplayName(displayedSeat)}.", 2f);
            UpdateUI();
        }

        private void EnsureBoardVisualTree(ScrollView boardScroll)
        {
            int tileCount = _boardRows * _boardColumns;
            if (_boardScrollView == boardScroll
                && _boardSurfaceElement != null
                && _boardGridLayerElement != null
                && _boardEffectsLayerElement != null
                && _boardVisualTileCount == tileCount
                && _boardTileElements != null
                && _boardTileElements.Length == tileCount
                && _boardTileTextureLayers != null
                && _boardTileTextureLayers.Length == tileCount
                && _boardTileAreaOverlays != null
                && _boardTileAreaOverlays.Length == tileCount)
            {
                return;
            }

            _boardScrollView = boardScroll;
            _boardVisualTileCount = tileCount;
            _boardRowElements = new VisualElement[_boardRows];
            _boardTileElements = new VisualElement[tileCount];
            _boardTileTextureLayers = new VisualElement[tileCount];
            _boardTileAreaOverlays = new VisualElement[tileCount];
            _boardTileOwnershipFrames = new VisualElement[tileCount];
            _boardTileSelectionGlows = new VisualElement[tileCount];
            _boardTileStatsBars = new VisualElement[tileCount];
            _boardTileHpLabels = new Label[tileCount];
            _boardTileCardContents = new VisualElement[tileCount];
            _boardTileArtPlaceholders = new VisualElement[tileCount];
            _boardTileNameLabels = new Label[tileCount];
            _boardTileAttackLabels = new Label[tileCount];
            _boardTileRightStatClusters = new VisualElement[tileCount];
            _boardTileLockLabels = new Label[tileCount];
            _boardTileAbilityLabels = new Label[tileCount];
            _boardTileItemLabels = new Label[tileCount];
            _boardTileIntentBadges = new Label[tileCount];
            _boardTileInvalidMarkers = new Label[tileCount];
            _boardTileDoomMarkers = new Label[tileCount];

            boardScroll.Clear();

            _boardSurfaceElement = new VisualElement();
            _boardSurfaceElement.AddToClassList("board-surface");

            _boardOwnershipFrameElement = new VisualElement();
            _boardOwnershipFrameElement.AddToClassList("board-ownership-frame");
            _boardOwnershipFrameElement.pickingMode = PickingMode.Ignore;

            _boardOwnershipTimerLayerElement = new VisualElement();
            _boardOwnershipTimerLayerElement.AddToClassList("board-ownership-timer-layer");
            _boardOwnershipTimerLayerElement.pickingMode = PickingMode.Ignore;

            _boardOwnershipTimerTopElement = new VisualElement();
            _boardOwnershipTimerTopElement.AddToClassList("board-ownership-timer-edge");
            _boardOwnershipTimerTopElement.AddToClassList("board-ownership-timer-top");
            _boardOwnershipTimerLayerElement.Add(_boardOwnershipTimerTopElement);

            _boardOwnershipTimerRightElement = new VisualElement();
            _boardOwnershipTimerRightElement.AddToClassList("board-ownership-timer-edge");
            _boardOwnershipTimerRightElement.AddToClassList("board-ownership-timer-right");
            _boardOwnershipTimerLayerElement.Add(_boardOwnershipTimerRightElement);

            _boardOwnershipTimerBottomElement = new VisualElement();
            _boardOwnershipTimerBottomElement.AddToClassList("board-ownership-timer-edge");
            _boardOwnershipTimerBottomElement.AddToClassList("board-ownership-timer-bottom");
            _boardOwnershipTimerLayerElement.Add(_boardOwnershipTimerBottomElement);

            _boardOwnershipTimerLeftElement = new VisualElement();
            _boardOwnershipTimerLeftElement.AddToClassList("board-ownership-timer-edge");
            _boardOwnershipTimerLeftElement.AddToClassList("board-ownership-timer-left");
            _boardOwnershipTimerLayerElement.Add(_boardOwnershipTimerLeftElement);

            _boardGridLayerElement = new VisualElement();
            _boardGridLayerElement.AddToClassList("board-grid-layer");

            for (int displayRow = 0; displayRow < _boardRows; displayRow++)
            {
                int sourceRow = GetCanonicalRowForDisplayRow(displayRow);
                var rowElement = new VisualElement();
                rowElement.AddToClassList("board-row");
                _boardRowElements[displayRow] = rowElement;

                for (int c = 0; c < _boardColumns; c++)
                {
                    int tileIdx = ToTileIndex(sourceRow, c);
                    var tileElement = new VisualElement();
                    tileElement.AddToClassList("board-tile");
                    int outlineIdx = ((sourceRow * _boardColumns + c) % 4) + 1;

                    var textureLayer = new VisualElement();
                    textureLayer.AddToClassList("tile-texture-layer");
                    textureLayer.pickingMode = PickingMode.Ignore;
                    tileElement.Add(textureLayer);

                    var areaOverlay = new VisualElement();
                    areaOverlay.AddToClassList("tile-area-overlay");
                    areaOverlay.AddToClassList($"tile-variant-{outlineIdx}");
                    areaOverlay.pickingMode = PickingMode.Ignore;
                    tileElement.Add(areaOverlay);

                    var selectionGlow = new VisualElement();
                    selectionGlow.AddToClassList("tile-selection-glow");
                    selectionGlow.pickingMode = PickingMode.Ignore;
                    tileElement.Add(selectionGlow);

                    var ownershipFrame = new VisualElement();
                    ownershipFrame.AddToClassList("tile-ownership-frame");
                    ownershipFrame.pickingMode = PickingMode.Ignore;
                    tileElement.Add(ownershipFrame);

                    var statsBar = new VisualElement();
                    var hpLabel = new Label();
                    hpLabel.AddToClassList("tile-stat-text-left");
                    hpLabel.enableRichText = true;
                    statsBar.Add(hpLabel);

                    var rightStatCluster = new VisualElement();
                    rightStatCluster.AddToClassList("tile-stat-right-cluster");

                    var lockLabel = new Label("L");
                    lockLabel.AddToClassList("tile-lock-badge");
                    rightStatCluster.Add(lockLabel);

                    var abilityLabel = new Label("A");
                    abilityLabel.AddToClassList("tile-status-badge");
                    abilityLabel.AddToClassList("tile-ability-badge");
                    rightStatCluster.Add(abilityLabel);

                    var itemLabel = new Label("I");
                    itemLabel.AddToClassList("tile-status-badge");
                    itemLabel.AddToClassList("tile-item-badge");
                    rightStatCluster.Add(itemLabel);

                    var atLabel = new Label();
                    atLabel.AddToClassList("tile-stat-text-right");
                    atLabel.enableRichText = true;
                    statsBar.Add(atLabel);
                    statsBar.Add(rightStatCluster);

                    var tileCardContent = new VisualElement();
                    tileCardContent.AddToClassList("tile-card-content");

                    var artPlaceholder = new VisualElement();
                    artPlaceholder.AddToClassList("tile-art-placeholder");
                    artPlaceholder.AddToClassList("tile-card-art-window");

                    var cardNameLabel = new Label();
                    cardNameLabel.AddToClassList("tile-card-name-overlay");
                    tileCardContent.Add(artPlaceholder);
                    tileCardContent.Add(cardNameLabel);

                    tileElement.Add(statsBar);
                    tileElement.Add(tileCardContent);

                    var intentBadge = new Label();
                    intentBadge.AddToClassList("tile-intent-badge");
                    intentBadge.pickingMode = PickingMode.Ignore;
                    tileElement.Add(intentBadge);

                    var invalidMarker = new Label("X");
                    invalidMarker.AddToClassList("attack-range-invalid-marker");
                    invalidMarker.pickingMode = PickingMode.Ignore;
                    tileElement.Add(invalidMarker);

                    var doomMarker = new Label("X");
                    doomMarker.AddToClassList("tile-preview-doom-marker");
                    doomMarker.pickingMode = PickingMode.Ignore;
                    tileElement.Add(doomMarker);

                    tileElement.RegisterCallback<ClickEvent>(evt =>
                    {
                        evt.StopPropagation();
                    });

                    int currentTileIdx = tileIdx;
                    RegisterCardHoldToInspect(tileElement, () =>
                    {
                        return currentTileIdx >= 0 && currentTileIdx < _boardTileData.Length
                            ? _boardTileData[currentTileIdx]
                            : null;
                    });
                    tileElement.RegisterCallback<PointerUpEvent>(evt =>
                    {
                        if (evt.button != 0)
                        {
                            return;
                        }

                        if (_cardHoldDetailOpened)
                        {
                            _cardHoldDetailOpened = false;
                            return;
                        }

                        if (ShouldDispatchBoardTileClick(currentTileIdx)
                            && TryDispatchUiAction(new MatchUiAction
                            {
                                actionType = MatchUiActionType.BoardTilePointerUp,
                                tileIndex = currentTileIdx
                            }))
                        {
                            HandleBoardTilePointerUp(currentTileIdx);
                            return;
                        }

                        if (IsRemoteReplica() && WouldBoardTileClickChangePlanningState(currentTileIdx))
                        {
                            ShowInvalidActionAndClearSelection("Waiting for host sync.");
                            return;
                        }

                        HandleBoardTilePointerUp(currentTileIdx);
                    });

                    _boardTileElements[tileIdx] = tileElement;
                    _boardTileTextureLayers[tileIdx] = textureLayer;
                    _boardTileAreaOverlays[tileIdx] = areaOverlay;
                    _boardTileOwnershipFrames[tileIdx] = ownershipFrame;
                    _boardTileSelectionGlows[tileIdx] = selectionGlow;
                    _boardTileStatsBars[tileIdx] = statsBar;
                    _boardTileHpLabels[tileIdx] = hpLabel;
                    _boardTileCardContents[tileIdx] = tileCardContent;
                    _boardTileArtPlaceholders[tileIdx] = artPlaceholder;
                    _boardTileNameLabels[tileIdx] = cardNameLabel;
                    _boardTileAttackLabels[tileIdx] = atLabel;
                    _boardTileRightStatClusters[tileIdx] = rightStatCluster;
                    _boardTileLockLabels[tileIdx] = lockLabel;
                    _boardTileAbilityLabels[tileIdx] = abilityLabel;
                    _boardTileItemLabels[tileIdx] = itemLabel;
                    _boardTileIntentBadges[tileIdx] = intentBadge;
                    _boardTileInvalidMarkers[tileIdx] = invalidMarker;
                    _boardTileDoomMarkers[tileIdx] = doomMarker;

                    rowElement.Add(tileElement);
                }

                _boardGridLayerElement.Add(rowElement);
            }

            _boardEffectsLayerElement = new VisualElement();
            _boardEffectsLayerElement.AddToClassList("board-effects-layer");
            _boardEffectsLayerElement.pickingMode = PickingMode.Ignore;

            _boardMotionLayerElement = new VisualElement();
            _boardMotionLayerElement.AddToClassList("board-motion-layer");
            _boardMotionLayerElement.pickingMode = PickingMode.Ignore;

            _boardSurfaceElement.Add(_boardOwnershipFrameElement);
            _boardSurfaceElement.Add(_boardOwnershipTimerLayerElement);
            _boardSurfaceElement.Add(_boardGridLayerElement);
            _boardSurfaceElement.Add(_boardEffectsLayerElement);
            _boardSurfaceElement.Add(_boardMotionLayerElement);
            boardScroll.Add(_boardSurfaceElement);
        }

        private void HandleBoardTilePointerUp(int currentTileIdx)
        {
            if (_suppressNextBoardClick)
            {
                _suppressNextBoardClick = false;
                return;
            }

            if (_cardDeployInFlight || _roundPhase == MatchRoundPhase.DisplayResolution)
            {
                return;
            }

            CardTemplate boardCardForTile = _boardTileData[currentTileIdx];
            bool hasBaseStats = _tileAreaKinds[currentTileIdx] == TileAreaKind.Base && _tileMaxHealth[currentTileIdx] > 0;

            if (_highlightedCardIndex != -1 && _activeTurnSeat != _localSeat)
            {
                _highlightedCardIndex = -1;
                _placementFocusActive = false;
            }

            if (HasSelectedWarShopOption())
            {
                WarShopOption selectedOption = GetSelectedWarShopOption();
                if (TryApplyWarShopOptionToTile(selectedOption, currentTileIdx, _localSeat))
                {
                    UpdateUI();
                }
                else
                {
                    ShowInvalidActionAndClearSelection(GetInvalidWarShopTargetReason(selectedOption, currentTileIdx, _localSeat));
                }

                return;
            }

            if (_highlightedCardIndex != -1 && _highlightedCardIndex < cardsInHand.Count)
            {
                CardTemplate selectedHandCard = cardsInHand[_highlightedCardIndex];
                if (IsLockCommandCard(selectedHandCard))
                {
                    if (TryApplyLockCardToTile(selectedHandCard, currentTileIdx))
                    {
                        ShowAwarenessMessage($"{selectedHandCard.cardName} applied.");
                        UpdateUI();
                    }
                    else
                    {
                        ShowInvalidActionAndClearSelection(GetInvalidLockTargetReason(currentTileIdx, _localSeat));
                    }

                    return;
                }

                if (_activeTurnSeat != _localSeat)
                {
                    Debug.LogWarning($"It is {_activeTurnSeat}'s turn. {_localSeat} cannot deploy right now.");
                    ShowInvalidActionAndClearSelection("It isn't your turn.");
                    return;
                }

                int selectedHandIndex = _highlightedCardIndex;
                if (selectedHandIndex >= cardsInHand.Count)
                {
                    return;
                }

                var cardToPlay = cardsInHand[selectedHandIndex];
                MatchSeat deployingSeat = _activeTurnSeat;
                if (_roundPhase != MatchRoundPhase.DeployPlanning)
                {
                    ShowInvalidActionAndClearSelection("You can only play cards during deploy phase.");
                    return;
                }

                if (cardToPlay != null && cardToPlay.cardType == CardType.Ordinance && !IsLockCommandCard(cardToPlay))
                {
                    if (TryApplyOrdinanceCardToTile(cardToPlay, currentTileIdx, selectedHandIndex, deployingSeat))
                    {
                        UpdateUI();
                    }
                    else
                    {
                        ShowInvalidActionAndClearSelection(GetInvalidOrdinanceTargetReason(cardToPlay, currentTileIdx, deployingSeat));
                    }

                    return;
                }

                if (cardToPlay != null && cardToPlay.cardType == CardType.Item)
                {
                    if (TryApplyItemCardToTile(cardToPlay, currentTileIdx, selectedHandIndex, deployingSeat))
                    {
                        UpdateUI();
                    }
                    else
                    {
                        ShowInvalidActionAndClearSelection(GetInvalidItemTargetReason(cardToPlay, currentTileIdx, deployingSeat));
                    }

                    return;
                }

                if (!IsBoardDeployableCard(cardToPlay))
                {
                    ShowInvalidActionAndClearSelection("This card needs a valid target.");
                    return;
                }

                if (!CanDeployCardToTile(currentTileIdx, deployingSeat))
                {
                    Debug.LogWarning($"Tile {currentTileIdx} is not a valid deployment tile for {deployingSeat}.");
                    ShowInvalidActionAndClearSelection(GetInvalidDeployReason(currentTileIdx, deployingSeat));
                    return;
                }

                if (!TrySpendTreasuryForCard(cardToPlay, deployingSeat))
                {
                    ShowInvalidActionAndClearSelection("Not enough treasury.");
                    SyncVisibleStateFromPerspective();
                    UpdateUI();
                    return;
                }

                _cardDeployInFlight = true;
                if (_isApplyingRemoteSeatAction)
                {
                    CompleteCardDeployment(cardToPlay, currentTileIdx, selectedHandIndex, deployingSeat);
                    return;
                }

                var carousel = _root.Q<ScrollView>("hand-carousel");
                VisualElement handCardElement = null;
                if (carousel != null)
                {
                    int idx = 0;
                    foreach (VisualElement child in carousel.contentContainer.Children())
                    {
                        if (idx == selectedHandIndex)
                        {
                            handCardElement = child;
                            break;
                        }

                        idx++;
                    }
                }

                VisualElement targetTileElement = _boardTileElements != null
                    && currentTileIdx >= 0
                    && currentTileIdx < _boardTileElements.Length
                    ? _boardTileElements[currentTileIdx]
                    : null;

                if (handCardElement != null && targetTileElement != null)
                {
                    Vector2 startPos = handCardElement.worldBound.position;
                    Vector2 targetPos = targetTileElement.worldBound.position;
                    float targetSize = targetTileElement.worldBound.width;

                    PlayFlyingCardAnimation(cardToPlay, startPos, targetPos, targetSize, () =>
                    {
                        CompleteCardDeployment(cardToPlay, currentTileIdx, selectedHandIndex, deployingSeat);
                    });
                }
                else
                {
                    CompleteCardDeployment(cardToPlay, currentTileIdx, selectedHandIndex, deployingSeat);
                }

                return;
            }

            bool isFriendlyUnit = _tileOccupantSeats[currentTileIdx].HasValue
                && _tileOccupantSeats[currentTileIdx].Value == _localSeat
                && IsUnitCard(boardCardForTile);

            if (isFriendlyUnit && _roundPhase == MatchRoundPhase.DeployPlanning && _activeTurnSeat == _localSeat)
            {
                if (_selectedAttackerTileIndex == currentTileIdx)
                {
                    _moveTargetTileBySource[currentTileIdx] = -1;
                    _selectedAttackerTileIndex = -1;
                    _selectedBoardTileIndex = currentTileIdx;
                    SetAbilityPreviewCard(boardCardForTile);
                    UpdateUI();
                    return;
                }

                _selectedAttackerTileIndex = currentTileIdx;
                _selectedBoardTileIndex = currentTileIdx;
                SetAbilityPreviewCard(boardCardForTile);
                UpdateUI();
                return;
            }

            if (isFriendlyUnit && _roundPhase == MatchRoundPhase.CombatPlanning && _activeTurnSeat == _localSeat)
            {
                if (_selectedAttackerTileIndex == currentTileIdx)
                {
                    _attackTargetTileBySource[currentTileIdx] = -1;
                    _selectedAttackerTileIndex = -1;
                    _selectedBoardTileIndex = currentTileIdx;
                    SetAbilityPreviewCard(boardCardForTile);
                    UpdateUI();
                    return;
                }

                _selectedAttackerTileIndex = currentTileIdx;
                _selectedBoardTileIndex = currentTileIdx;
                SetAbilityPreviewCard(boardCardForTile);
                UpdateUI();
                return;
            }

            if (_roundPhase == MatchRoundPhase.DeployPlanning
                && _selectedAttackerTileIndex >= 0
                && _activeTurnSeat == _localSeat)
            {
                if (CanSourceUnitMoveToTile(_selectedAttackerTileIndex, currentTileIdx, _localSeat))
                {
                    _moveTargetTileBySource[_selectedAttackerTileIndex] = currentTileIdx;
                    _selectedBoardTileIndex = currentTileIdx;
                    CardTemplate movingCard = _boardTileData[_selectedAttackerTileIndex];
                    SetAbilityPreviewText("Movement updated.");
                    ShowAwarenessMessage($"{movingCard?.cardName ?? "Unit"} movement planned.", 1.8f);
                    UpdateUI();
                }
                else if (IsPreviewStruggleTile(currentTileIdx))
                {
                    _selectedBoardTileIndex = currentTileIdx;
                    SetAbilityPreviewText(GetStrugglePreviewText(currentTileIdx));
                    UpdateUI();
                }
                else
                {
                    ShowInvalidActionAndClearSelection(GetInvalidMoveReason(_selectedAttackerTileIndex, currentTileIdx, _localSeat));
                }

                return;
            }

            if (_selectedAttackerTileIndex >= 0 && CanSourceUnitTargetTile(_selectedAttackerTileIndex, currentTileIdx))
            {
                _attackTargetTileBySource[_selectedAttackerTileIndex] = currentTileIdx;
                _selectedBoardTileIndex = currentTileIdx;
                CardTemplate attackingCard = _boardTileData[_selectedAttackerTileIndex];
                string targetName = GetCombatTargetDisplayName(currentTileIdx, _localSeat);
                if (boardCardForTile != null)
                {
                    SetAbilityPreviewCard(boardCardForTile);
                }
                else if (hasBaseStats)
                {
                    SetAbilityPreviewText(GetBaseTilePreviewText(currentTileIdx));
                }

                ShowAwarenessMessage($"{attackingCard?.cardName ?? "Unit"} will attack {targetName}.", 2f);
                UpdateUI();
                return;
            }

            if (_roundPhase == MatchRoundPhase.CombatPlanning
                && _selectedAttackerTileIndex >= 0
                && _activeTurnSeat == _localSeat)
            {
                ShowInvalidActionAndClearSelection(GetInvalidAttackReason(_selectedAttackerTileIndex, currentTileIdx));
                return;
            }

            if (boardCardForTile != null)
            {
                _highlightedCardIndex = -1;
                _selectedBoardTileIndex = currentTileIdx;
                SetAbilityPreviewCard(boardCardForTile);
                UpdateUI();
                return;
            }

            if (hasBaseStats)
            {
                _highlightedCardIndex = -1;
                _selectedBoardTileIndex = currentTileIdx;
                SetAbilityPreviewText(GetBaseTilePreviewText(currentTileIdx));
                UpdateUI();
                return;
            }

            if (_roundPhase == MatchRoundPhase.DeployPlanning && IsPreviewStruggleTile(currentTileIdx))
            {
                _highlightedCardIndex = -1;
                _selectedBoardTileIndex = currentTileIdx;
                SetAbilityPreviewText(GetStrugglePreviewText(currentTileIdx));
                UpdateUI();
            }
        }

        private void PopulateBoard()
        {
            var boardScroll = _root.Q<ScrollView>("board-scroll-view");
            if (boardScroll == null) return;

            boardScroll.mode = ScrollViewMode.VerticalAndHorizontal;
            EnsureBoardVisualTree(boardScroll);

            float tileFootprintWidth = (TileBaseWidth + (TileBaseMargin * 2f)) * _tileScale;
            float tileFootprintHeight = (TileBaseHeight + (TileBaseMargin * 2f)) * _tileScale;
            float boardWidth = _boardColumns * tileFootprintWidth;
            float boardHeight = _boardRows * tileFootprintHeight;
            _boardSurfaceElement.style.width = boardWidth;
            _boardSurfaceElement.style.height = boardHeight;
            bool showActiveGridFrame = _roundPhase != MatchRoundPhase.DisplayResolution;
            if (_boardOwnershipFrameElement != null)
            {
                _boardOwnershipFrameElement.style.display = showActiveGridFrame ? DisplayStyle.Flex : DisplayStyle.None;
                string activeBoardFrameClass = GetBoardActiveFrameClass(_activeTurnSeat);
                _boardOwnershipFrameElement.EnableInClassList("board-ownership-frame-seat-one", activeBoardFrameClass == "board-ownership-frame-seat-one");
                _boardOwnershipFrameElement.EnableInClassList("board-ownership-frame-seat-two", activeBoardFrameClass == "board-ownership-frame-seat-two");
            }
            UpdateBoardPlanningCountdownFrame(showActiveGridFrame, boardWidth, boardHeight);
            _boardGridLayerElement.style.width = boardWidth;
            _boardGridLayerElement.style.height = boardHeight;
            _boardEffectsLayerElement.style.width = boardWidth;
            _boardEffectsLayerElement.style.height = boardHeight;

            for (int displayRow = 0; displayRow < _boardRows; displayRow++)
            {
                VisualElement rowElement = _boardRowElements[displayRow];
                if (rowElement == null)
                {
                    continue;
                }

                rowElement.style.width = boardWidth;
                rowElement.style.height = tileFootprintHeight;
            }

            for (int tileIdx = 0; tileIdx < _boardTileElements.Length; tileIdx++)
            {
                VisualElement tileElement = _boardTileElements[tileIdx];
                if (tileElement == null)
                {
                    continue;
                }

                CardTemplate playedCard = _boardTileData[tileIdx];
                bool hasBaseStats = _tileAreaKinds[tileIdx] == TileAreaKind.Base && _tileMaxHealth[tileIdx] > 0;
                string tileVisualClass = GetTileVisualClass(tileIdx);
                float scaledWidth = TileBaseWidth * _tileScale;
                float scaledHeight = TileBaseHeight * _tileScale;
                float scaledMargin = TileBaseMargin * _tileScale;

                tileElement.style.width = scaledWidth;
                tileElement.style.height = scaledHeight;
                tileElement.style.marginLeft = scaledMargin;
                tileElement.style.marginRight = scaledMargin;
                tileElement.style.marginTop = scaledMargin;
                tileElement.style.marginBottom = scaledMargin;
                tileElement.EnableInClassList("seat-one-base-tile", false);
                tileElement.EnableInClassList("seat-two-base-tile", false);
                tileElement.EnableInClassList("neutral-tile", false);

                VisualElement textureLayer = _boardTileTextureLayers != null && tileIdx < _boardTileTextureLayers.Length
                    ? _boardTileTextureLayers[tileIdx]
                    : null;
                VisualElement areaOverlay = _boardTileAreaOverlays != null && tileIdx < _boardTileAreaOverlays.Length
                    ? _boardTileAreaOverlays[tileIdx]
                    : null;
                bool suppressResolveTileDecor = _resolveAnimationHiddenTiles.Contains(tileIdx);
                bool hideForResolveMotion = _resolveAnimationHiddenTiles.Contains(tileIdx) && playedCard != null;
                bool showTileAreaTint = playedCard == null || hideForResolveMotion;
                bool showBaseTexture = showTileAreaTint
                    && (tileVisualClass == "seat-one-base-tile" || tileVisualClass == "seat-two-base-tile");
                bool showGrassTexture = showTileAreaTint && tileVisualClass == "neutral-tile";
                if (textureLayer != null)
                {
                    textureLayer.style.display = showBaseTexture || showGrassTexture ? DisplayStyle.Flex : DisplayStyle.None;
                    textureLayer.EnableInClassList("tile-texture-bricks", showBaseTexture);
                    textureLayer.EnableInClassList("tile-texture-grass", showGrassTexture);
                }

                if (areaOverlay != null)
                {
                    areaOverlay.style.display = DisplayStyle.Flex;
                    areaOverlay.EnableInClassList("seat-one-base-tile", showTileAreaTint && tileVisualClass == "seat-one-base-tile");
                    areaOverlay.EnableInClassList("seat-two-base-tile", showTileAreaTint && tileVisualClass == "seat-two-base-tile");
                    areaOverlay.EnableInClassList("neutral-tile", showTileAreaTint && tileVisualClass == "neutral-tile");
                }

                tileElement.EnableInClassList("board-card-selected", tileIdx == _selectedBoardTileIndex);

                VisualElement ownershipFrame = _boardTileOwnershipFrames[tileIdx];
                VisualElement selectionGlow = _boardTileSelectionGlows[tileIdx];
                bool hasSelectedActionSeat = TryGetSelectedActionSeat(out MatchSeat selectedActionSeat);
                MatchSeat? sourceSeat = _tileOccupantSeats[tileIdx];
                MatchSeat? occupantSeat = GetOccupantSeat(tileIdx);
                MatchSeat selectionIdentitySeat = occupantSeat ?? selectedActionSeat;
                if (selectionGlow != null)
                {
                    bool showSelectionGlow = !suppressResolveTileDecor && tileIdx == _selectedBoardTileIndex;
                    selectionGlow.style.display = showSelectionGlow ? DisplayStyle.Flex : DisplayStyle.None;
                    selectionGlow.EnableInClassList("tile-selection-glow-seat-one", showSelectionGlow && selectionIdentitySeat == MatchSeat.SeatOne);
                    selectionGlow.EnableInClassList("tile-selection-glow-seat-two", showSelectionGlow && selectionIdentitySeat == MatchSeat.SeatTwo);
                }

                tileElement.EnableInClassList("attack-source-selected-seat-one", !suppressResolveTileDecor && tileIdx == _selectedAttackerTileIndex && sourceSeat == MatchSeat.SeatOne);
                tileElement.EnableInClassList("attack-source-selected-seat-two", !suppressResolveTileDecor && tileIdx == _selectedAttackerTileIndex && sourceSeat == MatchSeat.SeatTwo);
                tileElement.EnableInClassList("tile-occupant-seat-one", playedCard != null && occupantSeat == MatchSeat.SeatOne);
                tileElement.EnableInClassList("tile-occupant-seat-two", playedCard != null && occupantSeat == MatchSeat.SeatTwo);
                bool showPreviewDoom = _roundPhase == MatchRoundPhase.CombatPlanning
                    && playedCard != null
                    && _occupantCurrentHealth[tileIdx] > 0
                    && GetRenderedOccupantHealth(tileIdx) <= 0;
                tileElement.EnableInClassList("tile-preview-doomed", showPreviewDoom);

                bool showTargetHighlight = _roundPhase == MatchRoundPhase.DisplayResolution
                    ? _selectedBoardTileIndex == tileIdx
                        && _selectedAttackerTileIndex >= 0
                        && _selectedAttackerTileIndex < _tileOccupantSeats.Length
                        && _tileOccupantSeats[_selectedAttackerTileIndex].HasValue
                    : IsTileTargetedByFriendlyIntent(tileIdx, MatchSeat.SeatOne)
                        || IsTileTargetedByFriendlyIntent(tileIdx, MatchSeat.SeatTwo);
                tileElement.EnableInClassList("attack-targeted-tile", showTargetHighlight && !suppressResolveTileDecor);

                bool showRangeMarker = ShouldShowAttackRangeMarkerForTile(tileIdx, out bool isValidAttackTarget);
                bool showInvalidAttackMarker = ShouldShowInvalidAttackMarkerOnTile(tileIdx);
                bool showDeployMoveMarker = ShouldShowDeployMoveMarkerForTile(tileIdx, out bool isAssignedMoveTarget, out bool isExtendedMoveTarget);
                bool showSelectedHandTarget = ShouldShowSelectedHandTargetForTile(tileIdx);
                tileElement.EnableInClassList("attack-range-valid-tile", showRangeMarker && isValidAttackTarget);
                tileElement.EnableInClassList("attack-range-invalid-tile", showInvalidAttackMarker);
                tileElement.EnableInClassList("deploy-move-target-tile", (showDeployMoveMarker && isAssignedMoveTarget) || showSelectedHandTarget);
                tileElement.EnableInClassList("deploy-move-target-seat-one", showDeployMoveMarker && isAssignedMoveTarget && hasSelectedActionSeat && selectedActionSeat == MatchSeat.SeatOne);
                tileElement.EnableInClassList("deploy-move-target-seat-two", showDeployMoveMarker && isAssignedMoveTarget && hasSelectedActionSeat && selectedActionSeat == MatchSeat.SeatTwo);
                tileElement.EnableInClassList("deploy-move-optional-tile", (showDeployMoveMarker && !isAssignedMoveTarget) || showSelectedHandTarget);
                tileElement.EnableInClassList("deploy-move-optional-seat-one", showDeployMoveMarker && !isAssignedMoveTarget && hasSelectedActionSeat && selectedActionSeat == MatchSeat.SeatOne);
                tileElement.EnableInClassList("deploy-move-optional-seat-two", showDeployMoveMarker && !isAssignedMoveTarget && hasSelectedActionSeat && selectedActionSeat == MatchSeat.SeatTwo);
                tileElement.EnableInClassList("deploy-move-extended-tile", showDeployMoveMarker && isExtendedMoveTarget);
                tileElement.EnableInClassList("selected-hand-target-tile", showSelectedHandTarget);
                tileElement.EnableInClassList("selected-hand-target-seat-one", showSelectedHandTarget && hasSelectedActionSeat && selectedActionSeat == MatchSeat.SeatOne);
                tileElement.EnableInClassList("selected-hand-target-seat-two", showSelectedHandTarget && hasSelectedActionSeat && selectedActionSeat == MatchSeat.SeatTwo);

                VisualElement statsBar = _boardTileStatsBars[tileIdx];
                Label hpLabel = _boardTileHpLabels[tileIdx];
                VisualElement tileCardContent = _boardTileCardContents[tileIdx];
                VisualElement artPlaceholder = _boardTileArtPlaceholders[tileIdx];
                Label cardNameLabel = _boardTileNameLabels[tileIdx];
                Label atLabel = _boardTileAttackLabels[tileIdx];
                VisualElement rightStatCluster = _boardTileRightStatClusters[tileIdx];
                Label lockLabel = _boardTileLockLabels[tileIdx];
                Label abilityLabel = _boardTileAbilityLabels != null && tileIdx < _boardTileAbilityLabels.Length ? _boardTileAbilityLabels[tileIdx] : null;
                Label itemLabel = _boardTileItemLabels != null && tileIdx < _boardTileItemLabels.Length ? _boardTileItemLabels[tileIdx] : null;
                Label doomMarker = _boardTileDoomMarkers != null && tileIdx < _boardTileDoomMarkers.Length ? _boardTileDoomMarkers[tileIdx] : null;

                bool showBaseOnlyContent = playedCard == null && hasBaseStats;
                bool showStatsBar = playedCard != null || hasBaseStats;
                bool showCardContent = playedCard != null || showBaseOnlyContent;
                bool isEnemySide = occupantSeat.HasValue
                    ? occupantSeat.Value != _perspectiveSeat
                    : MatchPerspectiveUtility.IsRemoteOwned(_tileOwners[tileIdx], _perspectiveSeat);
                MatchSeat? statsThemeSeat = playedCard != null
                    ? occupantSeat
                    : hasBaseStats ? GetSeatFromTileOwner(_tileOwners[tileIdx]) : null;

                if (ownershipFrame != null)
                {
                    bool showOwnershipFrame = playedCard != null && occupantSeat.HasValue && !hideForResolveMotion;
                    ownershipFrame.style.display = showOwnershipFrame ? DisplayStyle.Flex : DisplayStyle.None;
                    string ownershipFrameClass = showOwnershipFrame ? GetSeatOwnershipFrameClass(occupantSeat.Value) : string.Empty;
                    bool showBaseOccupancyFrame = showOwnershipFrame && hasBaseStats;
                    ownershipFrame.EnableInClassList("tile-ownership-frame-seat-one", ownershipFrameClass == "tile-ownership-frame-seat-one");
                    ownershipFrame.EnableInClassList("tile-ownership-frame-seat-two", ownershipFrameClass == "tile-ownership-frame-seat-two");
                    ownershipFrame.EnableInClassList("tile-ownership-frame-on-base", showBaseOccupancyFrame);
                }

                if (statsBar != null)
                {
                    statsBar.style.display = showStatsBar && !hideForResolveMotion ? DisplayStyle.Flex : DisplayStyle.None;
                    statsBar.EnableInClassList("tile-stats-bar-bottom", showStatsBar);
                    statsBar.EnableInClassList("tile-stats-card-overlay", showStatsBar && playedCard != null);
                    statsBar.EnableInClassList("tile-stats-base-only", showBaseOnlyContent);
                    string statsThemeClass = statsThemeSeat.HasValue ? GetSeatStatsClass(statsThemeSeat.Value) : string.Empty;
                    statsBar.EnableInClassList("tile-stats-theme-seat-one", statsThemeClass == "tile-stats-theme-seat-one");
                    statsBar.EnableInClassList("tile-stats-theme-seat-two", statsThemeClass == "tile-stats-theme-seat-two");
                    statsBar.EnableInClassList("tile-stats-theme-neutral", string.IsNullOrEmpty(statsThemeClass));
                }

                if (tileCardContent != null)
                {
                    tileCardContent.style.display = showCardContent && !hideForResolveMotion ? DisplayStyle.Flex : DisplayStyle.None;
                    tileCardContent.EnableInClassList("tile-card-content-base-only", showBaseOnlyContent);
                    string seatThemeClass = occupantSeat.HasValue ? GetSeatThemeClass(occupantSeat.Value) : string.Empty;
                    tileCardContent.EnableInClassList("seat-theme-one", seatThemeClass == "seat-theme-one");
                    tileCardContent.EnableInClassList("seat-theme-two", seatThemeClass == "seat-theme-two");
                }

                if (showStatsBar && hpLabel != null)
                {
                    if (playedCard != null)
                    {
                        hpLabel.text = IsInfrastructureCard(playedCard)
                            ? GetTileHealthMarkup(tileIdx)
                            : hasBaseStats ? GetCombinedBaseOccupantHealthMarkup(tileIdx) : GetOccupantHealthMarkup(tileIdx);
                    }
                    else
                    {
                        hpLabel.text = GetTileHealthMarkup(tileIdx);
                    }
                }

                if (rightStatCluster != null)
                {
                    rightStatCluster.style.display = playedCard != null ? DisplayStyle.Flex : DisplayStyle.None;
                }

                if (atLabel != null)
                {
                    atLabel.text = playedCard != null ? GetAttackMarkup(tileIdx) : string.Empty;
                }

                if (lockLabel != null)
                {
                    bool showLockBadge = playedCard != null && _tileLocked[tileIdx];
                    lockLabel.style.display = showLockBadge ? DisplayStyle.Flex : DisplayStyle.None;
                    lockLabel.text = string.Empty;
                    lockLabel.EnableInClassList("status-icon-lock", showLockBadge);
                    ApplyStatusBadgeSprite(lockLabel, null);
                }

                if (abilityLabel != null)
                {
                    AbilityEffectData effect = GetPrimaryKeywordEffect(playedCard);
                    bool showAbilityBadge = playedCard != null && effect != null && effect.keyword != AbilityKeyword.None;
                    abilityLabel.style.display = showAbilityBadge ? DisplayStyle.Flex : DisplayStyle.None;
                    abilityLabel.text = showAbilityBadge ? FormatKeywordIconText(effect) : string.Empty;
                    ApplyKeywordBadgeClasses(abilityLabel, effect);
                    ApplyStatusBadgeSprite(abilityLabel, null);
                }

                if (itemLabel != null)
                {
                    bool showItemBadge = playedCard != null && playedCard.attachedItemCard != null;
                    itemLabel.style.display = showItemBadge ? DisplayStyle.Flex : DisplayStyle.None;
                    itemLabel.text = string.Empty;
                    Sprite itemSprite = showItemBadge ? playedCard.attachedItemCard.customArt : null;
                    ApplyStatusBadgeSprite(itemLabel, itemSprite);
                }

                if (doomMarker != null)
                {
                    doomMarker.style.display = showPreviewDoom ? DisplayStyle.Flex : DisplayStyle.None;
                }

                if (artPlaceholder != null)
                {
                    artPlaceholder.style.backgroundImage = playedCard != null && playedCard.customArt != null
                        ? new StyleBackground(playedCard.customArt)
                        : new StyleBackground();
                    string artThemeClass = occupantSeat.HasValue ? GetCardArtThemeClass(occupantSeat.Value) : string.Empty;
                    artPlaceholder.EnableInClassList("tile-card-art-seat-one", playedCard != null && artThemeClass == "tile-card-art-seat-one");
                    artPlaceholder.EnableInClassList("tile-card-art-seat-two", playedCard != null && artThemeClass == "tile-card-art-seat-two");
                    artPlaceholder.EnableInClassList("tile-card-art-neutral-tile", playedCard != null && !occupantSeat.HasValue);
                    artPlaceholder.EnableInClassList("tile-card-art-base-only", showBaseOnlyContent);
                    artPlaceholder.EnableInClassList("tile-card-art-align-bottom", playedCard != null);
                }

                if (cardNameLabel != null)
                {
                    cardNameLabel.text = playedCard != null ? playedCard.cardName.ToUpper() : showBaseOnlyContent ? "BASE TILE" : string.Empty;
                    cardNameLabel.style.display = showCardContent ? DisplayStyle.Flex : DisplayStyle.None;
                    cardNameLabel.EnableInClassList("tile-card-name-top", playedCard != null);
                    cardNameLabel.EnableInClassList("tile-card-name-base", showBaseOnlyContent);
                    string nameThemeClass = occupantSeat.HasValue ? GetSeatThemeClass(occupantSeat.Value) : string.Empty;
                    cardNameLabel.EnableInClassList("seat-theme-one", nameThemeClass == "seat-theme-one");
                    cardNameLabel.EnableInClassList("seat-theme-two", nameThemeClass == "seat-theme-two");
                }

                if (statsBar != null && tileCardContent != null)
                {
                    if (statsBar.parent == tileElement)
                    {
                        statsBar.RemoveFromHierarchy();
                    }

                    if (tileCardContent.parent == tileElement)
                    {
                        tileCardContent.RemoveFromHierarchy();
                    }

                    int insertIndex = Mathf.Min(2, tileElement.childCount);
                    if (showCardContent)
                    {
                        tileElement.Insert(insertIndex, tileCardContent);
                        insertIndex++;
                    }

                    if (showStatsBar)
                    {
                        tileElement.Insert(insertIndex, statsBar);
                    }
                }

                string intentBadgeText = GetIntentBadgeTextForTile(tileIdx);
                Label intentBadge = _boardTileIntentBadges[tileIdx];
                if (intentBadge != null)
                {
                    bool showIntentBadge = !string.IsNullOrWhiteSpace(intentBadgeText);
                    intentBadge.style.display = showIntentBadge ? DisplayStyle.Flex : DisplayStyle.None;
                    intentBadge.text = intentBadgeText;
                    intentBadge.EnableInClassList("tile-intent-badge-siege", intentBadgeText == "SIEGE");
                    intentBadge.EnableInClassList("tile-intent-badge-city", intentBadgeText == "Attack City");
                    intentBadge.EnableInClassList("tile-intent-badge-struggle", intentBadgeText == "STRUGGLE");
                    intentBadge.EnableInClassList("tile-intent-badge-miss", intentBadgeText == "MISS");
                    intentBadge.EnableInClassList("tile-intent-badge-no-move", intentBadgeText == "NO MOVE");
                }

                Label invalidMarker = _boardTileInvalidMarkers[tileIdx];
                if (invalidMarker != null)
                {
                    invalidMarker.style.display = showInvalidAttackMarker ? DisplayStyle.Flex : DisplayStyle.None;
                    invalidMarker.EnableInClassList("attack-range-invalid-marker-seat-one", showInvalidAttackMarker && hasSelectedActionSeat && selectedActionSeat == MatchSeat.SeatOne);
                    invalidMarker.EnableInClassList("attack-range-invalid-marker-seat-two", showInvalidAttackMarker && hasSelectedActionSeat && selectedActionSeat == MatchSeat.SeatTwo);
                }
            }

            _boardEffectsLayerElement.Clear();
            PopulateAttackIntentVisuals(_boardEffectsLayerElement, tileFootprintWidth, tileFootprintHeight);
            PopulateMovementIntentVisuals(_boardEffectsLayerElement, tileFootprintWidth, tileFootprintHeight);
            PopulateFloatingBoardTextVisuals(_boardEffectsLayerElement, tileFootprintWidth, tileFootprintHeight);
            ResetBoardViewToPlayerAnchorIfNeeded();
        }

        private Vector2 GetBoardSurfaceTileCenter(int tileIndex, float tileFootprintWidth, float tileFootprintHeight)
        {
            TryGetRowColumnFromTileIndex(tileIndex, out int canonicalRow, out int column);
            int displayRow = GetDisplayRowForCanonicalRow(canonicalRow);
            return new Vector2(
                (column * tileFootprintWidth) + (tileFootprintWidth * 0.5f),
                (displayRow * tileFootprintHeight) + (tileFootprintHeight * 0.5f));
        }

        private Rect GetBoardSurfaceTileRect(int tileIndex)
        {
            float tileFootprintWidth = (TileBaseWidth + (TileBaseMargin * 2f)) * _tileScale;
            float tileFootprintHeight = (TileBaseHeight + (TileBaseMargin * 2f)) * _tileScale;
            TryGetRowColumnFromTileIndex(tileIndex, out int canonicalRow, out int column);
            int displayRow = GetDisplayRowForCanonicalRow(canonicalRow);
            return new Rect(
                column * tileFootprintWidth,
                displayRow * tileFootprintHeight,
                tileFootprintWidth,
                tileFootprintHeight);
        }

        private void ClearResolveMotionVisuals()
        {
            _resolveMotionAnimationSerial++;
            _resolveAnimationHiddenTiles.Clear();
            if (_boardMotionLayerElement != null)
            {
                for (int childIndex = _boardMotionLayerElement.childCount - 1; childIndex >= 0; childIndex--)
                {
                    VisualElement child = _boardMotionLayerElement.ElementAt(childIndex);
                    if (child != null && child.ClassListContains("resolve-motion-proxy"))
                    {
                        child.RemoveFromHierarchy();
                    }
                }
            }
            if (_activeResolveMotionProxy != null)
            {
                _activeResolveMotionProxy.RemoveFromHierarchy();
                _activeResolveMotionProxy = null;
            }
        }

        private VisualElement CreateResolveMotionProxy(int tileIndex)
        {
            if (_boardMotionLayerElement == null
                || tileIndex < 0
                || tileIndex >= _boardTileData.Length
                || _boardTileData[tileIndex] == null)
            {
                return null;
            }

            Rect tileRect = GetBoardSurfaceTileRect(tileIndex);
            MatchSeat? occupantSeat = GetOccupantSeat(tileIndex);
            string statsThemeClass = occupantSeat.HasValue ? GetSeatStatsClass(occupantSeat.Value) : string.Empty;
            string seatThemeClass = occupantSeat.HasValue ? GetSeatThemeClass(occupantSeat.Value) : string.Empty;
            bool showSourceSelectionGlow = tileIndex == _selectedAttackerTileIndex && occupantSeat.HasValue;

            var proxyRoot = new VisualElement();
            proxyRoot.AddToClassList("resolve-motion-proxy");
            proxyRoot.pickingMode = PickingMode.Ignore;
            if (showSourceSelectionGlow && occupantSeat.HasValue)
            {
                proxyRoot.AddToClassList(occupantSeat.Value == MatchSeat.SeatOne
                    ? "attack-source-selected-seat-one"
                    : "attack-source-selected-seat-two");
            }
            proxyRoot.style.left = tileRect.xMin;
            proxyRoot.style.top = tileRect.yMin;
            proxyRoot.style.width = tileRect.width;
            proxyRoot.style.height = tileRect.height;

            if (showSourceSelectionGlow)
            {
                var selectionGlow = new VisualElement();
                selectionGlow.AddToClassList("tile-selection-glow");
                selectionGlow.AddToClassList(occupantSeat.Value == MatchSeat.SeatOne ? "tile-selection-glow-seat-one" : "tile-selection-glow-seat-two");
                proxyRoot.Add(selectionGlow);
            }

            if (occupantSeat.HasValue)
            {
                var ownershipFrame = new VisualElement();
                ownershipFrame.AddToClassList("tile-ownership-frame");
                ownershipFrame.AddToClassList(GetSeatOwnershipFrameClass(occupantSeat.Value));
                proxyRoot.Add(ownershipFrame);
            }

            var statsBar = new VisualElement();
            statsBar.AddToClassList("tile-stats-bar-bottom");
            statsBar.AddToClassList("tile-stats-card-overlay");
            if (!string.IsNullOrEmpty(statsThemeClass))
            {
                statsBar.AddToClassList(statsThemeClass);
            }

            var hpLabel = new Label(_boardTileHpLabels != null && tileIndex < _boardTileHpLabels.Length && _boardTileHpLabels[tileIndex] != null
                ? _boardTileHpLabels[tileIndex].text
                : string.Empty);
            hpLabel.AddToClassList("tile-stat-text-left");
            hpLabel.enableRichText = true;
            statsBar.Add(hpLabel);

            var atLabel = new Label(_boardTileAttackLabels != null && tileIndex < _boardTileAttackLabels.Length && _boardTileAttackLabels[tileIndex] != null
                ? _boardTileAttackLabels[tileIndex].text
                : string.Empty);
            atLabel.AddToClassList("tile-stat-text-right");
            atLabel.enableRichText = true;
            var rightStatCluster = new VisualElement();
            rightStatCluster.AddToClassList("tile-stat-right-cluster");

            Label sourceLockLabel = _boardTileLockLabels != null && tileIndex < _boardTileLockLabels.Length ? _boardTileLockLabels[tileIndex] : null;
            if (sourceLockLabel != null && sourceLockLabel.resolvedStyle.display != DisplayStyle.None)
            {
                var lockLabel = new Label(sourceLockLabel.text);
                lockLabel.AddToClassList("tile-lock-badge");
                lockLabel.EnableInClassList("status-icon-lock", sourceLockLabel.ClassListContains("status-icon-lock"));
                ApplyStatusBadgeSprite(lockLabel, null);
                rightStatCluster.Add(lockLabel);
            }

            Label sourceAbilityLabel = _boardTileAbilityLabels != null && tileIndex < _boardTileAbilityLabels.Length ? _boardTileAbilityLabels[tileIndex] : null;
            if (sourceAbilityLabel != null && sourceAbilityLabel.resolvedStyle.display != DisplayStyle.None)
            {
                var abilityLabel = new Label(sourceAbilityLabel.text);
                abilityLabel.AddToClassList("tile-status-badge");
                abilityLabel.AddToClassList("tile-ability-badge");
                CopyStatusIconClasses(sourceAbilityLabel, abilityLabel);
                abilityLabel.style.backgroundImage = sourceAbilityLabel.style.backgroundImage;
                rightStatCluster.Add(abilityLabel);
            }

            Label sourceItemLabel = _boardTileItemLabels != null && tileIndex < _boardTileItemLabels.Length ? _boardTileItemLabels[tileIndex] : null;
            if (sourceItemLabel != null && sourceItemLabel.resolvedStyle.display != DisplayStyle.None)
            {
                var itemLabel = new Label(sourceItemLabel.text);
                itemLabel.AddToClassList("tile-status-badge");
                itemLabel.AddToClassList("tile-item-badge");
                CopyStatusIconClasses(sourceItemLabel, itemLabel);
                itemLabel.style.backgroundImage = sourceItemLabel.style.backgroundImage;
                rightStatCluster.Add(itemLabel);
            }

            statsBar.Add(atLabel);
            statsBar.Add(rightStatCluster);
            proxyRoot.Add(statsBar);

            var tileCardContent = new VisualElement();
            tileCardContent.AddToClassList("tile-card-content");
            if (!string.IsNullOrEmpty(seatThemeClass))
            {
                tileCardContent.AddToClassList(seatThemeClass);
            }

            var artPlaceholder = new VisualElement();
            artPlaceholder.AddToClassList("tile-art-placeholder");
            artPlaceholder.AddToClassList("tile-card-art-window");
            if (_boardTileArtPlaceholders != null
                && tileIndex < _boardTileArtPlaceholders.Length
                && _boardTileArtPlaceholders[tileIndex] != null)
            {
                VisualElement sourceArt = _boardTileArtPlaceholders[tileIndex];
                artPlaceholder.style.backgroundImage = sourceArt.style.backgroundImage;
                artPlaceholder.EnableInClassList("tile-card-art-align-bottom", sourceArt.ClassListContains("tile-card-art-align-bottom"));
                artPlaceholder.EnableInClassList("tile-card-art-seat-one", sourceArt.ClassListContains("tile-card-art-seat-one"));
                artPlaceholder.EnableInClassList("tile-card-art-seat-two", sourceArt.ClassListContains("tile-card-art-seat-two"));
                artPlaceholder.EnableInClassList("tile-card-art-neutral-tile", sourceArt.ClassListContains("tile-card-art-neutral-tile"));
            }

            var cardNameLabel = new Label(_boardTileNameLabels != null && tileIndex < _boardTileNameLabels.Length && _boardTileNameLabels[tileIndex] != null
                ? _boardTileNameLabels[tileIndex].text
                : string.Empty);
            cardNameLabel.AddToClassList("tile-card-name-overlay");
            cardNameLabel.AddToClassList("tile-card-name-top");
            cardNameLabel.EnableInClassList("seat-theme-one", seatThemeClass == "seat-theme-one");
            cardNameLabel.EnableInClassList("seat-theme-two", seatThemeClass == "seat-theme-two");

            tileCardContent.Add(artPlaceholder);
            tileCardContent.Add(cardNameLabel);
            proxyRoot.Add(tileCardContent);

            return proxyRoot;
        }

        private static void CopyStatusIconClasses(VisualElement source, VisualElement target)
        {
            if (source == null || target == null)
            {
                return;
            }

            string[] knownClasses =
            {
                "status-icon-has-art",
                "status-icon-lock",
                "status-icon-gather",
                "status-icon-siphon",
                "status-icon-discount",
                "status-icon-strike",
                "status-icon-shatter",
                "status-icon-breach",
                "status-icon-intercept",
                "status-icon-secure",
                "status-icon-reclaim",
                "status-icon-sprint",
                "status-icon-maneuver",
                "status-icon-lock",
                "status-icon-silence",
                "status-icon-garrison",
                "status-icon-provoke",
                "status-icon-spawn",
                "status-icon-burn",
                "status-icon-salvage",
                "status-icon-bonus"
            };

            for (int i = 0; i < knownClasses.Length; i++)
            {
                string className = knownClasses[i];
                if (source.ClassListContains(className))
                {
                    target.AddToClassList(className);
                }
            }
        }

        private static void AttachResolveMotionIntentBadge(VisualElement proxyRoot, string badgeText)
        {
            if (proxyRoot == null || string.IsNullOrWhiteSpace(badgeText))
            {
                return;
            }

            var badge = new Label(badgeText);
            badge.AddToClassList("tile-intent-badge");
            badge.AddToClassList("resolve-motion-intent-badge");
            badge.EnableInClassList("tile-intent-badge-siege", badgeText == "SIEGE");
            badge.EnableInClassList("tile-intent-badge-city", badgeText == "CITY!");
            badge.EnableInClassList("tile-intent-badge-struggle", badgeText == "STRUGGLE");
            badge.EnableInClassList("tile-intent-badge-miss", badgeText == "MISS");
            badge.EnableInClassList("tile-intent-badge-no-move", badgeText == "NO MOVE");
            proxyRoot.Add(badge);
        }

        private void BeginResolveMoveMotion(int sourceTileIndex, int targetTileIndex, string badgeText)
        {
            if (_boardMotionLayerElement == null)
            {
                return;
            }

            VisualElement proxy = CreateResolveMotionProxy(sourceTileIndex);
            if (proxy == null)
            {
                return;
            }

            ClearResolveMotionVisuals();
            int motionSerial = _resolveMotionAnimationSerial;
            _activeResolveMotionProxy = proxy;
            _resolveAnimationHiddenTiles.Add(sourceTileIndex);
            _resolveAnimationHiddenTiles.Add(targetTileIndex);
            AttachResolveMotionIntentBadge(proxy, badgeText);
            _boardMotionLayerElement.Add(proxy);

            Rect sourceRect = GetBoardSurfaceTileRect(sourceTileIndex);
            Rect targetRect = GetBoardSurfaceTileRect(targetTileIndex);
            Vector2 delta = targetRect.position - sourceRect.position;

            proxy.schedule.Execute(() =>
            {
                if (motionSerial != _resolveMotionAnimationSerial || proxy.parent == null)
                {
                    return;
                }

                proxy.AddToClassList("resolve-motion-proxy-move-active");
                proxy.style.translate = new StyleTranslate(new Translate(
                    new Length(delta.x, LengthUnit.Pixel),
                    new Length(delta.y, LengthUnit.Pixel)));
            }).StartingIn(16);

            proxy.schedule.Execute(() =>
            {
                if (motionSerial != _resolveMotionAnimationSerial)
                {
                    return;
                }

                _resolveAnimationHiddenTiles.Remove(sourceTileIndex);
                _resolveAnimationHiddenTiles.Remove(targetTileIndex);
                proxy.RemoveFromHierarchy();
                if (_activeResolveMotionProxy == proxy)
                {
                    _activeResolveMotionProxy = null;
                }

                UpdateUI();
            }).StartingIn(520);
        }

        private void BeginResolveAttackMotion(int sourceTileIndex, Vector2 lungeDelta, string badgeText)
        {
            if (_boardMotionLayerElement == null)
            {
                return;
            }

            VisualElement proxy = CreateResolveMotionProxy(sourceTileIndex);
            if (proxy == null)
            {
                return;
            }

            ClearResolveMotionVisuals();
            int motionSerial = _resolveMotionAnimationSerial;
            _activeResolveMotionProxy = proxy;
            _resolveAnimationHiddenTiles.Add(sourceTileIndex);
            AttachResolveMotionIntentBadge(proxy, badgeText);
            _boardMotionLayerElement.Add(proxy);

            proxy.schedule.Execute(() =>
            {
                if (motionSerial != _resolveMotionAnimationSerial || proxy.parent == null)
                {
                    return;
                }

                proxy.AddToClassList("resolve-motion-proxy-attack-active");
                proxy.style.translate = new StyleTranslate(new Translate(
                    new Length(lungeDelta.x, LengthUnit.Pixel),
                    new Length(lungeDelta.y, LengthUnit.Pixel)));
            }).StartingIn(16);

            proxy.schedule.Execute(() =>
            {
                if (motionSerial != _resolveMotionAnimationSerial || proxy.parent == null)
                {
                    return;
                }

                proxy.style.translate = new StyleTranslate(new Translate(
                    new Length(0f, LengthUnit.Pixel),
                    new Length(0f, LengthUnit.Pixel)));
            }).StartingIn(270);

            proxy.schedule.Execute(() =>
            {
                if (motionSerial != _resolveMotionAnimationSerial)
                {
                    return;
                }

                _resolveAnimationHiddenTiles.Remove(sourceTileIndex);
                proxy.RemoveFromHierarchy();
                if (_activeResolveMotionProxy == proxy)
                {
                    _activeResolveMotionProxy = null;
                }

                UpdateUI();
            }).StartingIn(560);
        }

        private void BeginResolveDeathMotion(VisualElement proxy, int tileIndex)
        {
            if (_boardMotionLayerElement == null || proxy == null)
            {
                return;
            }

            int motionSerial = _resolveMotionAnimationSerial;
            _boardMotionLayerElement.Add(proxy);

            proxy.schedule.Execute(() =>
            {
                if (motionSerial != _resolveMotionAnimationSerial || proxy.parent == null)
                {
                    return;
                }

                proxy.AddToClassList("resolve-motion-proxy-death-active");
                proxy.style.translate = new StyleTranslate(new Translate(
                    new Length(0f, LengthUnit.Pixel),
                    new Length(-18f, LengthUnit.Pixel)));
            }).StartingIn(16);

            proxy.schedule.Execute(() =>
            {
                if (proxy.parent == null)
                {
                    return;
                }

                proxy.RemoveFromHierarchy();
                UpdateUI();
            }).StartingIn(360);
        }

        private void PopulateAttackIntentVisuals(VisualElement boardEffectsLayer, float tileFootprintWidth, float tileFootprintHeight)
        {
            if (_roundPhase == MatchRoundPhase.DisplayResolution && _displayResolutionMode != DisplayResolutionMode.Attack)
            {
                return;
            }

            for (int sourceTileIndex = 0; sourceTileIndex < _attackTargetTileBySource.Length; sourceTileIndex++)
            {
                int targetTileIndex = GetDisplayedAttackTargetTileForSource(sourceTileIndex);
                MatchSeat? cityTargetSeat = GetDisplayedCityAttackSeatForSource(sourceTileIndex);
                if ((_tileOccupantSeats[sourceTileIndex] == null
                    || _boardTileData[sourceTileIndex] == null
                    || !IsUnitCard(_boardTileData[sourceTileIndex])
                    || _occupantCurrentHealth[sourceTileIndex] <= 0
                    || !ShouldRenderCurrentDisplayStepForSource(sourceTileIndex)))
                {
                    continue;
                }

                if (cityTargetSeat.HasValue)
                {
                    continue;
                }

                MatchSeat sourceSeat = _tileOccupantSeats[sourceTileIndex].Value;
                if (targetTileIndex < 0)
                {
                    bool canShowMissIntent = _roundPhase == MatchRoundPhase.CombatPlanning
                        || (_roundPhase == MatchRoundPhase.DisplayResolution && _displayResolutionMode == DisplayResolutionMode.Attack);
                    if (!canShowMissIntent)
                    {
                        continue;
                    }

                    targetTileIndex = GetMissAttackIntentTile(sourceTileIndex, sourceSeat, GetCardAttackRangeAtTile(sourceTileIndex));
                }

                if (targetTileIndex < 0
                    || _boardTileData[sourceTileIndex] == null)
                {
                    continue;
                }

                Vector2 sourceTileCenter = GetBoardSurfaceTileCenter(sourceTileIndex, tileFootprintWidth, tileFootprintHeight);
                Vector2 targetTileCenter = GetBoardSurfaceTileCenter(targetTileIndex, tileFootprintWidth, tileFootprintHeight);
                Vector2 direction = targetTileCenter - sourceTileCenter;
                float length = direction.magnitude;
                if (length <= 1f)
                {
                    continue;
                }

                Vector2 directionUnit = direction / length;
                Vector2 sourceCenter = GetIntentEndpoint(sourceTileCenter, directionUnit, tileFootprintWidth, tileFootprintHeight, sourceSeat, true, true);
                Vector2 targetCenter = GetIntentEndpoint(targetTileCenter, directionUnit, tileFootprintWidth, tileFootprintHeight, sourceSeat, false, true);
                direction = targetCenter - sourceCenter;
                length = direction.magnitude;
                if (length <= 1f)
                {
                    continue;
                }

                directionUnit = direction / length;
                float arrowOffset = 12f;
                float lineLength = Mathf.Max(10f, length - arrowOffset);
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                Vector2 lineMidpoint = sourceCenter + (directionUnit * (lineLength * 0.5f));
                Vector2 arrowCenter = sourceCenter + (directionUnit * lineLength);

                var line = new VisualElement();
                line.AddToClassList("attack-intent-line");
                line.AddToClassList(sourceSeat == MatchSeat.SeatOne ? "attack-intent-line-seat-one" : "attack-intent-line-seat-two");
                line.pickingMode = PickingMode.Ignore;
                line.style.width = lineLength;
                line.style.left = lineMidpoint.x - (lineLength * 0.5f);
                line.style.top = lineMidpoint.y - 3f;
                line.style.rotate = new StyleRotate(new Rotate(new Angle(angle, AngleUnit.Degree)));
                boardEffectsLayer.Add(line);

                var arrowHead = new Label(">");
                arrowHead.AddToClassList("attack-intent-arrowhead");
                arrowHead.AddToClassList(sourceSeat == MatchSeat.SeatOne ? "attack-intent-arrow-seat-one" : "attack-intent-arrow-seat-two");
                arrowHead.pickingMode = PickingMode.Ignore;
                arrowHead.style.left = arrowCenter.x;
                arrowHead.style.top = arrowCenter.y;
                arrowHead.style.rotate = new StyleRotate(new Rotate(new Angle(angle, AngleUnit.Degree)));
                boardEffectsLayer.Add(arrowHead);
            }
        }

        private void PopulateMovementIntentVisuals(VisualElement boardEffectsLayer, float tileFootprintWidth, float tileFootprintHeight)
        {
            if (_roundPhase == MatchRoundPhase.DeployPlanning)
            {
                for (int sourceTileIndex = 0; sourceTileIndex < _moveTargetTileBySource.Length; sourceTileIndex++)
                {
                    if (!_tileOccupantSeats[sourceTileIndex].HasValue
                        || _boardTileData[sourceTileIndex] == null
                        || !IsUnitCard(_boardTileData[sourceTileIndex])
                        || _occupantCurrentHealth[sourceTileIndex] <= 0)
                    {
                        continue;
                    }

                    MatchSeat sourceMoveSeat = _tileOccupantSeats[sourceTileIndex].Value;
                    int targetTileIndex = GetPlannedMoveTargetTile(sourceTileIndex, sourceMoveSeat);
                    if (targetTileIndex < 0)
                    {
                        targetTileIndex = GetNoMoveIntentTile(sourceTileIndex, sourceMoveSeat);
                        if (targetTileIndex < 0)
                        {
                            continue;
                        }
                    }

                    PopulateMovementIntentArrow(boardEffectsLayer, tileFootprintWidth, tileFootprintHeight, sourceTileIndex, targetTileIndex, sourceMoveSeat);
                }
                return;
            }

            if (_roundPhase != MatchRoundPhase.DisplayResolution
                || _displayResolutionMode != DisplayResolutionMode.Movement
                || _selectedAttackerTileIndex < 0
                || _selectedBoardTileIndex < 0)
            {
                return;
            }

            if (_selectedAttackerTileIndex >= _boardTileData.Length
                || _selectedBoardTileIndex >= _boardTileData.Length)
            {
                return;
            }

            MatchSeat selectedMoveSeat = _tileOccupantSeats[_selectedAttackerTileIndex].HasValue
                ? _tileOccupantSeats[_selectedAttackerTileIndex].Value
                : MatchSeat.SeatOne;
            PopulateMovementIntentArrow(boardEffectsLayer, tileFootprintWidth, tileFootprintHeight, _selectedAttackerTileIndex, _selectedBoardTileIndex, selectedMoveSeat);
        }

        private void PopulateMovementIntentArrow(VisualElement boardEffectsLayer, float tileFootprintWidth, float tileFootprintHeight, int sourceTileIndex, int targetTileIndex, MatchSeat moveSeat)
        {
            Vector2 sourceTileCenter = GetBoardSurfaceTileCenter(sourceTileIndex, tileFootprintWidth, tileFootprintHeight);
            Vector2 targetTileCenter = GetBoardSurfaceTileCenter(targetTileIndex, tileFootprintWidth, tileFootprintHeight);
            Vector2 direction = targetTileCenter - sourceTileCenter;
            float length = direction.magnitude;
            if (length <= 1f)
            {
                return;
            }

            Vector2 directionUnit = direction / length;
            Vector2 sourceCenter = GetIntentEndpoint(sourceTileCenter, directionUnit, tileFootprintWidth, tileFootprintHeight, moveSeat, true, false);
            Vector2 targetCenter = GetIntentEndpoint(targetTileCenter, directionUnit, tileFootprintWidth, tileFootprintHeight, moveSeat, false, false);
            direction = targetCenter - sourceCenter;
            length = direction.magnitude;
            if (length <= 1f)
            {
                return;
            }

            directionUnit = direction / length;
            float arrowOffset = 12f;
            float lineLength = Mathf.Max(10f, length - arrowOffset);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Vector2 lineMidpoint = sourceCenter + (directionUnit * (lineLength * 0.5f));
            Vector2 arrowCenter = sourceCenter + (directionUnit * lineLength);

            var line = new VisualElement();
            line.AddToClassList("move-intent-line");
            line.pickingMode = PickingMode.Ignore;
            line.style.width = lineLength;
            line.style.left = lineMidpoint.x - (lineLength * 0.5f);
            line.style.top = lineMidpoint.y - 3f;
            line.style.rotate = new StyleRotate(new Rotate(new Angle(angle, AngleUnit.Degree)));
            boardEffectsLayer.Add(line);

            var arrowHead = new Label(">");
            arrowHead.AddToClassList("move-intent-arrowhead");
            arrowHead.pickingMode = PickingMode.Ignore;
            arrowHead.style.left = arrowCenter.x;
            arrowHead.style.top = arrowCenter.y;
            arrowHead.style.rotate = new StyleRotate(new Rotate(new Angle(angle, AngleUnit.Degree)));
            boardEffectsLayer.Add(arrowHead);
        }

        private Vector2 GetIntentEndpoint(Vector2 tileCenter, Vector2 directionUnit, float tileFootprintWidth, float tileFootprintHeight, MatchSeat seat, bool isSource, bool separateBySeat)
        {
            float edgeInsetX = tileFootprintWidth * 0.23f;
            float edgeInsetY = tileFootprintHeight * 0.22f;
            float seatOffsetX = separateBySeat ? tileFootprintWidth * (seat == MatchSeat.SeatOne ? -0.16f : 0.16f) : 0f;
            Vector2 edgeOffset = new Vector2(directionUnit.x * edgeInsetX, directionUnit.y * edgeInsetY);
            return tileCenter + new Vector2(seatOffsetX, 0f) + (isSource ? edgeOffset : -edgeOffset);
        }

        private void PopulateFloatingBoardTextVisuals(VisualElement boardEffectsLayer, float tileFootprintWidth, float tileFootprintHeight)
        {
            float now = Application.isPlaying ? Time.unscaledTime : 0f;
            for (int i = _floatingBoardTexts.Count - 1; i >= 0; i--)
            {
                FloatingBoardTextRuntime floatingText = _floatingBoardTexts[i];
                if (Application.isPlaying && floatingText.expiresAt <= now)
                {
                    _floatingBoardTexts.RemoveAt(i);
                    continue;
                }

                Vector2 center = GetBoardSurfaceTileCenter(floatingText.tileIndex, tileFootprintWidth, tileFootprintHeight);
                var popup = new Label(floatingText.text);
                popup.AddToClassList(floatingText.cssClass);
                popup.AddToClassList("tile-floating-popup");
                popup.pickingMode = PickingMode.Ignore;
                popup.style.left = center.x - 52f;
                popup.style.top = center.y - 58f;
                boardEffectsLayer.Add(popup);

                if (_externalCommandSink != null)
                {
                    popup.AddToClassList("tile-floating-popup-active");
                }
                else
                {
                    popup.schedule.Execute(() =>
                    {
                        popup.AddToClassList("tile-floating-popup-active");
                    });
                }
            }
        }

        private void PlayFlyingCardAnimation(CardTemplate cardData, Vector2 startPos, Vector2 targetPos, float targetSize, System.Action onComplete)
        {
            var flyingContainer = new VisualElement();
            flyingContainer.style.position = Position.Absolute;
            flyingContainer.style.left = startPos.x;
            flyingContainer.style.top = startPos.y;
            flyingContainer.style.width = 210;
            flyingContainer.style.height = 318;

            // Clone layout visual content
            VisualElement cardInstance = cardThumbnailTemplate.Instantiate();
            cardInstance.style.width = Length.Percent(100);
            cardInstance.style.height = Length.Percent(100);
            BindCardThumbnail(cardInstance, cardData, _activeTurnSeat, true, false);

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

        private void RegisterMobileDockToggle(string buttonName, bool isLeft)
        {
            var button = _root.Q<Button>(buttonName);
            if (button == null)
            {
                return;
            }

            button.RegisterCallback<PointerDownEvent>(evt =>
            {
                evt.StopPropagation();
            });

            button.RegisterCallback<PointerUpEvent>(evt =>
            {
                evt.StopPropagation();
                ToggleMobileDock(isLeft);
            });
        }

        private void RegisterMobileDockClose(string buttonName, bool isLeft)
        {
            var button = _root.Q<Button>(buttonName);
            if (button == null)
            {
                return;
            }

            button.RegisterCallback<PointerDownEvent>(evt =>
            {
                evt.StopPropagation();
            });

            button.RegisterCallback<PointerUpEvent>(evt =>
            {
                evt.StopPropagation();
                if (isLeft)
                {
                    _mobileLeftDockOpen = false;
                }
                else
                {
                    _mobileRightDockOpen = false;
                }

                UpdateDesktopDockLayout();
            });
        }

        private void ToggleMobileDock(bool isLeft)
        {
            if (_desktopDockLayoutActive)
            {
                return;
            }

            if (isLeft)
            {
                _mobileLeftDockOpen = !_mobileLeftDockOpen;
                if (_mobileLeftDockOpen)
                {
                    _mobileRightDockOpen = false;
                }
            }
            else
            {
                _mobileRightDockOpen = !_mobileRightDockOpen;
                if (_mobileRightDockOpen)
                {
                    _mobileLeftDockOpen = false;
                }
            }

            UpdateDesktopDockLayout();
        }

        private void CloseMobileDocks()
        {
            if (!_mobileLeftDockOpen && !_mobileRightDockOpen)
            {
                return;
            }

            _mobileLeftDockOpen = false;
            _mobileRightDockOpen = false;
            UpdateDesktopDockLayout();
        }

        private void ApplySafeAreaIfNeeded()
        {
            var safeAreaRoot = _root?.Q<VisualElement>("safe-area-root");
            var mainCanvas = _root?.Q<VisualElement>("main-canvas");
            var topHud = _root?.Q<VisualElement>("top-hud");
            var bottomHud = _root?.Q<VisualElement>("bottom-hud");
            if (safeAreaRoot == null || mainCanvas == null || topHud == null || bottomHud == null)
            {
                return;
            }

            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
            Rect safeArea = Screen.safeArea;
            if (screenSize == _lastScreenSize && safeArea == _lastSafeArea)
            {
                return;
            }

            float rootWidth = _root.resolvedStyle.width;
            float rootHeight = _root.resolvedStyle.height;
            if (rootWidth <= 0f || rootHeight <= 0f || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            _lastScreenSize = screenSize;
            _lastSafeArea = safeArea;

            float scaleX = rootWidth / Screen.width;
            float scaleY = rootHeight / Screen.height;

            float leftInset = safeArea.xMin * scaleX;
            float rightInset = Mathf.Max(0f, Screen.width - safeArea.xMax) * scaleX;
            float topInset = Mathf.Max(0f, Screen.height - safeArea.yMax) * scaleY;
            float bottomInset = safeArea.yMin * scaleY;
            float extraTopInset = ShouldApplyMobileSafeAreaBuffer() ? 34f : 0f;
            bool isDesktopWideLayout = rootWidth >= DesktopDockMinViewportWidth
                && (rootWidth / Mathf.Max(1f, rootHeight)) >= DesktopDockMinAspectRatio;
            float baseCanvasVerticalPadding = isDesktopWideLayout ? 4f : 40f;

            // Keep the canvas and arena art full-bleed. Only the interactive chrome respects notches/cutouts.
            safeAreaRoot.style.paddingLeft = 0f;
            safeAreaRoot.style.paddingRight = 0f;
            safeAreaRoot.style.paddingTop = 0f;
            safeAreaRoot.style.paddingBottom = 0f;

            mainCanvas.style.paddingLeft = leftInset;
            mainCanvas.style.paddingRight = rightInset;
            mainCanvas.style.paddingTop = baseCanvasVerticalPadding;
            mainCanvas.style.paddingBottom = baseCanvasVerticalPadding;

            float topHudBaseHeight = isDesktopWideLayout ? 74f : 140f;
            float topHudBasePadding = isDesktopWideLayout ? 2f : 10f;
            topHud.style.height = topHudBaseHeight + topInset + extraTopInset;
            topHud.style.maxHeight = topHudBaseHeight + topInset + extraTopInset;
            topHud.style.paddingTop = topHudBasePadding + topInset + extraTopInset;
            topHud.style.paddingLeft = 36f;
            topHud.style.paddingRight = 36f;

            bottomHud.style.paddingBottom = bottomInset + (isDesktopWideLayout ? 0f : 24f);

            ApplySafeAreaToOverlay("game-mode-overlay", leftInset, rightInset, topInset + extraTopInset, bottomInset, 48f);
            ApplySafeAreaToOverlay("arena-selection-overlay", leftInset, rightInset, topInset + extraTopInset, bottomInset, 34f);
            ApplySafeAreaToOverlay("reconnect-overlay", leftInset, rightInset, topInset + extraTopInset, bottomInset, 48f);
            ApplySafeAreaToOverlay("match-end-overlay", leftInset, rightInset, topInset + extraTopInset, bottomInset, 48f);
        }

        private void UpdateDesktopDockLayout()
        {
            var gameplayShell = _root?.Q<VisualElement>("gameplay-shell");
            var leftDock = _root?.Q<VisualElement>("left-desktop-dock");
            var rightDock = _root?.Q<VisualElement>("right-desktop-dock");
            var centerStageShell = _root?.Q<VisualElement>("center-stage-shell");
            var centerStageContent = _root?.Q<VisualElement>("center-stage-content");
            var bottomHud = _root?.Q<VisualElement>("bottom-hud");
            var desktopControlStrip = _root?.Q<VisualElement>("desktop-control-strip");
            var controlsRow = _root?.Q<VisualElement>(className: "controls-row");
            var zoomControlsPanel = _root?.Q<VisualElement>("zoom-controls-panel");
            var handCarousel = _root?.Q<ScrollView>("hand-carousel");
            var playAreaContainer = _root?.Q<VisualElement>("play-area-container");
            var mobileLeftToggle = _root?.Q<Button>("mobile-left-dock-toggle");
            var mobileRightToggle = _root?.Q<Button>("mobile-right-dock-toggle");
            var mobileDockScrim = _root?.Q<VisualElement>("mobile-dock-scrim");
            var mobileLeftDockPanel = _root?.Q<VisualElement>("mobile-left-dock-panel");
            var mobileRightDockPanel = _root?.Q<VisualElement>("mobile-right-dock-panel");
            if (gameplayShell == null
                || leftDock == null
                || rightDock == null
                || centerStageShell == null
                || centerStageContent == null
                || bottomHud == null
                || desktopControlStrip == null
                || playAreaContainer == null)
            {
                return;
            }

            float rootWidth = _root.resolvedStyle.width;
            float rootHeight = _root.resolvedStyle.height;
            if (rootWidth <= 0f || rootHeight <= 0f)
            {
                return;
            }

            float shellWidth = gameplayShell.resolvedStyle.width > 0f
                ? gameplayShell.resolvedStyle.width
                : rootWidth;

            bool canShowDesktopDocks = rootWidth >= DesktopDockMinViewportWidth
                && (rootWidth / Mathf.Max(1f, rootHeight)) >= DesktopDockMinAspectRatio
                && shellWidth >= DesktopDockCenterStageWidth + (DesktopDockMinWidth * 2f) + (DesktopDockGapWidth * 2f);

            bool desktopDockLayoutChanged = canShowDesktopDocks != _desktopDockLayoutActive;
            gameplayShell.EnableInClassList("desktop-dock-layout", canShowDesktopDocks);

            if (canShowDesktopDocks && !_desktopDockLayoutActive)
            {
                _mobileLeftDockOpen = false;
                _mobileRightDockOpen = false;
            }

            _desktopDockLayoutActive = canShowDesktopDocks;

            if (canShowDesktopDocks)
            {
                if (zoomControlsPanel?.parent != desktopControlStrip || controlsRow?.parent != desktopControlStrip)
                {
                    zoomControlsPanel?.RemoveFromHierarchy();
                    controlsRow?.RemoveFromHierarchy();
                    if (zoomControlsPanel != null)
                    {
                        desktopControlStrip.Add(zoomControlsPanel);
                    }

                    if (controlsRow != null)
                    {
                        desktopControlStrip.Add(controlsRow);
                    }
                }

                if (handCarousel != null)
                {
                    handCarousel.mode = ScrollViewMode.Vertical;
                }
            }
            else
            {
                if (controlsRow?.parent != bottomHud || zoomControlsPanel?.parent != bottomHud)
                {
                    controlsRow?.RemoveFromHierarchy();
                    zoomControlsPanel?.RemoveFromHierarchy();
                    if (controlsRow != null)
                    {
                        bottomHud.Add(controlsRow);
                    }

                    if (zoomControlsPanel != null)
                    {
                        bottomHud.Add(zoomControlsPanel);
                    }
                }

                if (handCarousel != null)
                {
                    handCarousel.mode = ScrollViewMode.Horizontal;
                }
            }

            float dockWidth = 0f;
            if (!canShowDesktopDocks)
            {
                leftDock.style.width = 0f;
                leftDock.style.minWidth = 0f;
                leftDock.style.maxWidth = 0f;
                rightDock.style.width = 0f;
                rightDock.style.minWidth = 0f;
                rightDock.style.maxWidth = 0f;

                centerStageShell.style.flexGrow = 1f;
                centerStageShell.style.width = StyleKeyword.Auto;
                centerStageShell.style.minWidth = StyleKeyword.Auto;
                centerStageShell.style.maxWidth = StyleKeyword.None;
                centerStageContent.style.position = Position.Relative;
                centerStageContent.style.left = 0f;
                centerStageContent.style.top = 0f;
                centerStageContent.style.width = Length.Percent(100f);
                centerStageContent.style.height = Length.Percent(100f);
                centerStageContent.style.scale = new Scale(new Vector3(1f, 1f, 1f));
                centerStageContent.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
            }
            else
            {
                dockWidth = Mathf.Clamp(
                    (shellWidth - DesktopDockCenterStageWidth - (DesktopDockGapWidth * 2f)) * 0.5f,
                    DesktopDockMinWidth,
                    DesktopDockMaxWidth);

                leftDock.style.width = dockWidth;
                leftDock.style.minWidth = dockWidth;
                leftDock.style.maxWidth = dockWidth;
                rightDock.style.width = dockWidth;
                rightDock.style.minWidth = dockWidth;
                rightDock.style.maxWidth = dockWidth;

                float centerTargetWidth = Mathf.Min(
                    DesktopDockCenterStageWidth,
                    Mathf.Max(1f, shellWidth - (dockWidth * 2f) - (DesktopDockGapWidth * 2f)));
                centerStageShell.style.flexGrow = 0f;
                centerStageShell.style.width = centerTargetWidth;
                centerStageShell.style.minWidth = centerTargetWidth;
                centerStageShell.style.maxWidth = centerTargetWidth;
                centerStageContent.style.position = Position.Relative;
                centerStageContent.style.left = 0f;
                centerStageContent.style.top = 0f;
                centerStageContent.style.width = Length.Percent(100f);
                centerStageContent.style.height = Length.Percent(100f);
                centerStageContent.style.scale = new Scale(new Vector3(1f, 1f, 1f));
                centerStageContent.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
            }

            bool showGameplay = !_awaitingLaunchModeSelection && !_reconnectOverlayVisible && !_arenaSelectionActive;
            bool showMobileDockToggles = !canShowDesktopDocks && showGameplay;
            if (!showMobileDockToggles)
            {
                _mobileLeftDockOpen = false;
                _mobileRightDockOpen = false;
            }

            bool showMobileDockScrim = showMobileDockToggles && (_mobileLeftDockOpen || _mobileRightDockOpen);

            if (mobileLeftToggle != null)
            {
                mobileLeftToggle.style.display = showMobileDockToggles ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (mobileRightToggle != null)
            {
                mobileRightToggle.style.display = showMobileDockToggles ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (mobileDockScrim != null)
            {
                mobileDockScrim.style.display = showMobileDockScrim ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (mobileLeftDockPanel != null)
            {
                mobileLeftDockPanel.style.display = showMobileDockToggles && _mobileLeftDockOpen ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (mobileRightDockPanel != null)
            {
                mobileRightDockPanel.style.display = showMobileDockToggles && _mobileRightDockOpen ? DisplayStyle.Flex : DisplayStyle.None;
            }

            playAreaContainer.EnableInClassList("mobile-dock-enabled", showMobileDockToggles);
            playAreaContainer.EnableInClassList("mobile-dock-scrim-visible", showMobileDockScrim);
            playAreaContainer.EnableInClassList("mobile-left-dock-open", showMobileDockToggles && _mobileLeftDockOpen);
            playAreaContainer.EnableInClassList("mobile-right-dock-open", showMobileDockToggles && _mobileRightDockOpen);

            if (desktopDockLayoutChanged)
            {
                _tileScale = Mathf.Max(GetCurrentMinTileScale(), Mathf.Min(_tileScale, MaxTileScale));
                _boardViewNeedsReset = true;
                var boardScroll = _root?.Q<ScrollView>("board-scroll-view");
                boardScroll?.schedule.Execute(() => RequestBoardFitAndCenter(true)).StartingIn(0);
                boardScroll?.schedule.Execute(() => RequestBoardFitAndCenter(true)).StartingIn(80);
            }
        }

        private static bool ShouldApplyMobileSafeAreaBuffer()
        {
            return Application.isMobilePlatform;
        }

        private void ApplySafeAreaToOverlay(string elementName, float leftInset, float rightInset, float topInset, float bottomInset, float basePadding)
        {
            var overlay = _root?.Q<VisualElement>(elementName);
            if (overlay == null)
            {
                return;
            }

            overlay.style.paddingLeft = basePadding + leftInset;
            overlay.style.paddingRight = basePadding + rightInset;
            overlay.style.paddingTop = basePadding + topInset;
            overlay.style.paddingBottom = basePadding + bottomInset;
        }
    }
}
