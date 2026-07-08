using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using LastFreeCity.UI;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace LastFreeCity.Gameplay
{
    public class LocalMultiplayerTestBootstrap : MonoBehaviour, IMatchUiCommandSink
    {
        private const string SeatAssignmentMessageName = "lfc.seat";
        private const string SnapshotMessageName = "lfc.snapshot";
        private const string TimerSyncMessageName = "lfc.timer";
        private const string UiActionMessageName = "lfc.action";
        private const string ReturnToMenuMessageName = "lfc.return_to_menu";
        private const float ReconnectWindowSeconds = 30f;
        private const float ReconnectAttemptIntervalSeconds = 2f;
        private const float DiagnosticLogIntervalSeconds = 5f;
        private const float LobbyHeartbeatIntervalSeconds = 15f;
        private const float LobbyPollIntervalSeconds = 8f;
        private const float QuickMatchFreshLobbySeconds = 45f;
        private const float QuickMatchGuestSearchSeconds = 12f;
        private const int QuickMatchGuestSearchDelayMs = 2000;
        private const string OnlineProfileArg = "-lfcProfile";
        private const string LobbyDataHostSeat = "hostSeat";
        private const string LobbyDataWantedSeat = "wantedSeat";
        private const string LobbyDataRelayJoinCode = "relayJoinCode";
        private const string LobbyDataStatus = "status";
        private const string LobbyStatusWaiting = "waiting";
        private const string LobbyStatusMatched = "matched";

        [Header("References")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private MatchPrototypeDefinition prototypeMatch;

        [Header("Connection")]
        [SerializeField] private string connectAddress = "127.0.0.1";
        [SerializeField] private ushort port = 7777;
        [SerializeField] private bool useWebSockets = false;
        [SerializeField] private string webSocketPath = "/";
        [SerializeField] private bool useWebGlPageHostAsAddress = true;
        [SerializeField] private bool autoStartFromCommandLine = true;
        [SerializeField] private float snapshotSendInterval = 0.15f;
        [SerializeField] private float timerSyncSendInterval = 0.25f;

        private NetworkManager _networkManager;
        private UnityTransport _transport;
        private readonly Dictionary<ulong, MatchSeat> _seatAssignments = new Dictionary<ulong, MatchSeat>();
        private MatchLaunchMode _launchMode = MatchLaunchMode.None;
        private MatchSeat? _localAssignedSeat;
        private string _lastSnapshotJson = string.Empty;
        private string _lastStableSnapshotJson = string.Empty;
        private float _nextSnapshotSendAt;
        private float _nextTimerSyncSendAt;
        private bool _messageHandlersRegistered;
        private bool _networkSessionInitialized;
        private bool _shutdownRequested;
        private bool _shutdownFinalized;
        private bool _clientStartPending;
        private bool _reconnectWaitActive;
        private bool _reconnectAllowClientRetry;
        private bool _manualReconnectCancel;
        private bool _returningMatchToMenu;
        private string _reconnectWaitMessage = string.Empty;
        private float _reconnectEndsAt;
        private float _nextReconnectAttemptAt;
        private float _nextDiagnosticLogAt;
        private int _snapshotBroadcastCount;
        private bool _receivedFirstSnapshot;
        private bool _sentDisplayResolutionSnapshot;
        private Lobby _onlineLobby;
        private MatchSeat _onlineSelectedSeat = MatchSeat.SeatOne;
        private MatchSeat _onlineHostSeat = MatchSeat.SeatOne;
        private bool _onlineQuickMatchActive;
        private bool _onlineIsLobbyHost;
        private bool _onlineServicesReady;
        private OnlineConnectionState _onlineConnectionState = OnlineConnectionState.Offline;
        private float _nextLobbyHeartbeatAt;
        private float _nextLobbyPollAt;
        private bool _lobbyHeartbeatInFlight;
        private bool _lobbyPollInFlight;

        public OnlineConnectionState CurrentOnlineConnectionState => _onlineConnectionState;

        private bool HasRemoteClientConnected()
        {
            return _seatAssignments.ContainsValue(MatchSeat.SeatTwo);
        }

        private bool IsDedicatedServerMode()
        {
            return _launchMode == MatchLaunchMode.DedicatedServer;
        }

        private bool HasRequiredPlayersConnected()
        {
            if (IsDedicatedServerMode() || _launchMode == MatchLaunchMode.OnlineQuickMatch)
            {
                return _seatAssignments.ContainsValue(MatchSeat.SeatOne)
                    && _seatAssignments.ContainsValue(MatchSeat.SeatTwo);
            }

            return HasRemoteClientConnected();
        }

        private void Awake()
        {
            KeepApplicationResponsiveInBackground();

            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }

            if (prototypeMatch == null && uiManager != null)
            {
                prototypeMatch = uiManager.prototypeMatch;
            }

            EnsureNetworkingComponents();
            ConfigureTransport();
            RegisterUiCallbacks();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            KeepApplicationResponsiveInBackground();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            KeepApplicationResponsiveInBackground();
        }

        private static void KeepApplicationResponsiveInBackground()
        {
            Application.runInBackground = true;
            if (Application.targetFrameRate < 30)
            {
                Application.targetFrameRate = 60;
            }
        }

        private void Start()
        {
            ApplyConnectionSettingsFromStartupArgs();
            LogConnectionSettings("Startup settings applied");

            if (!autoStartFromCommandLine)
            {
                return;
            }

            string mode = GetCommandLineValue("-lfcMode");
            if (string.Equals(mode, "host", StringComparison.OrdinalIgnoreCase))
            {
                StartHostMode();
            }
            else if (string.Equals(mode, "server", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "dedicated", StringComparison.OrdinalIgnoreCase))
            {
                StartDedicatedServerMode();
            }
            else if (string.Equals(mode, "client", StringComparison.OrdinalIgnoreCase))
            {
                StartClientMode();
            }
        }

        private void Update()
        {
            TickReconnectWait();
            TickOnlineLobbyMaintenance();

            if (_clientStartPending && !_shutdownRequested && IsLocalLoopbackAddress(connectAddress) && IsLoopbackHostPortAvailable())
            {
                _clientStartPending = false;
                TryStartClientConnection();
            }

            EnsureInitializedForCurrentNetworkMode();
            TickDiagnosticLog();

            if (!Application.isPlaying || _networkManager == null || !_networkManager.IsServer)
            {
                return;
            }

            if (Time.unscaledTime < _nextSnapshotSendAt)
            {
                TryBroadcastTimerSync();
                return;
            }

            _nextSnapshotSendAt = Time.unscaledTime + Mathf.Max(0.05f, snapshotSendInterval);
            BroadcastSnapshotIfChanged();
            TryBroadcastTimerSync();
        }

        private void OnDisable()
        {
            ShutdownMultiplayer();
        }

        private void OnDestroy()
        {
            ShutdownMultiplayer();
            UnregisterMessageHandlers();
            UnregisterNetworkCallbacks();
            UnregisterUiCallbacks();
        }

        private void OnApplicationQuit()
        {
            ShutdownMultiplayer();
        }

        [ContextMenu("Start Host")]
        public void StartHostMode()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (uiManager != null)
            {
                uiManager.SetLaunchModeStatus("Browser builds should use Online Quick Match.");
            }

            Debug.LogWarning("[LocalMultiplayer] WebGL cannot start a direct host. Use Online Quick Match.");
            return;
#endif
            _launchMode = MatchLaunchMode.MultiplayerHost;
            _manualReconnectCancel = false;
            EndReconnectWait();
            ConfigureTransport();
            LogConnectionSettings("Starting host");

            if (_networkManager.IsListening)
            {
                if (uiManager != null && !_networkSessionInitialized)
                {
                    uiManager.SetLaunchModeStatus("Host is waiting for a client to connect...");
                }

                return;
            }

            RegisterMessageHandlers();
            RegisterNetworkCallbacks();

            if (_networkManager.StartHost())
            {
                RegisterMessageHandlers();
                RegisterNetworkCallbacks();
                _seatAssignments.Clear();
                _localAssignedSeat = MatchSeat.SeatOne;
                _seatAssignments[_networkManager.LocalClientId] = MatchSeat.SeatOne;
                _networkSessionInitialized = false;
                if (uiManager != null)
                {
                    uiManager.SetExternalCommandSink(null);
                    uiManager.SetLaunchModeStatus("Host started. Waiting for client to connect...");
                }
                Debug.Log("[LocalMultiplayer] Host started.");
            }
            else
            {
                UnregisterMessageHandlers();
                UnregisterNetworkCallbacks();
                Debug.LogWarning("[LocalMultiplayer] Host failed to start.");
            }
        }

        [ContextMenu("Start Client")]
        public void StartClientMode()
        {
            _launchMode = MatchLaunchMode.MultiplayerClient;
            _manualReconnectCancel = false;
            EndReconnectWait();
            ConfigureTransport();
            LogConnectionSettings("Starting client");
            PrepareClientUiForRemoteControl();

            if (_networkManager.IsListening)
            {
                return;
            }

            if (IsLocalLoopbackAddress(connectAddress) && !IsLoopbackHostPortAvailable())
            {
                _clientStartPending = true;
                if (uiManager != null)
                {
                    uiManager.SetLaunchModeStatus($"Waiting for local host on {connectAddress}:{port}...");
                }

                Debug.Log($"[LocalMultiplayer] Client start deferred until local host is listening on {connectAddress}:{port}.");
                return;
            }

            TryStartClientConnection();
        }

        [ContextMenu("Start Dedicated Server")]
        public void StartDedicatedServerMode()
        {
            _launchMode = MatchLaunchMode.DedicatedServer;
            _manualReconnectCancel = false;
            EndReconnectWait();
            ConfigureTransport();
            LogConnectionSettings("Starting dedicated server");

            if (_networkManager.IsListening)
            {
                if (uiManager != null && !_networkSessionInitialized)
                {
                    uiManager.SetLaunchModeStatus("Dedicated server is waiting for two clients...");
                }

                return;
            }

            RegisterMessageHandlers();
            RegisterNetworkCallbacks();

            if (_networkManager.StartServer())
            {
                RegisterMessageHandlers();
                RegisterNetworkCallbacks();
                _seatAssignments.Clear();
                _localAssignedSeat = null;
                _networkSessionInitialized = false;
                if (uiManager != null)
                {
                    uiManager.SetExternalCommandSink(null);
                    uiManager.SetLaunchModeStatus("Dedicated server started. Waiting for two clients...");
                }

                Debug.Log($"[LocalMultiplayer] Dedicated server started on port {port} using {(useWebSockets ? "WebSockets" : "UDP")}.");
                LogNetworkState("Dedicated server StartServer succeeded");
            }
            else
            {
                UnregisterMessageHandlers();
                UnregisterNetworkCallbacks();
                Debug.LogWarning("[LocalMultiplayer] Dedicated server failed to start.");
            }
        }

        private void TryStartClientConnection()
        {
            if (_networkManager.IsListening)
            {
                return;
            }

            RegisterMessageHandlers();
            RegisterNetworkCallbacks();
            LogConnectionSettings("Calling StartClient");

            if (_networkManager.StartClient())
            {
                RegisterMessageHandlers();
                RegisterNetworkCallbacks();
                if (uiManager != null)
                {
                    uiManager.SetLaunchModeStatus($"Client connecting to {connectAddress}:{port}...");
                }

                Debug.Log($"[LocalMultiplayer] Client connecting to {connectAddress}:{port}.");
                LogNetworkState("StartClient succeeded");
            }
            else
            {
                UnregisterMessageHandlers();
                UnregisterNetworkCallbacks();
                Debug.LogWarning("[LocalMultiplayer] Client failed to start.");
            }
        }

        [ContextMenu("Shutdown Multiplayer")]
        public void ShutdownMultiplayer()
        {
            if (_shutdownRequested)
            {
                return;
            }

            if (_onlineQuickMatchActive)
            {
                _ = CleanupOnlineLobbyAsync();
            }

            _shutdownRequested = true;
            _shutdownFinalized = false;

            if (_networkManager != null && (_networkManager.IsListening || _networkManager.ShutdownInProgress))
            {
                ShutdownTransportIfRunning();
                if (_networkManager.ShutdownInProgress)
                {
                    return;
                }
            }

            FinalizeShutdown("Multiplayer shutdown completed.");
        }

        [ContextMenu("Shutdown Host")]
        public void ShutdownHost()
        {
            ShutdownMultiplayer();
        }

        [ContextMenu("Shutdown Client")]
        public void ShutdownClient()
        {
            ShutdownMultiplayer();
        }

        private void FinalizeShutdown(string statusMessage)
        {
            if (_shutdownFinalized)
            {
                return;
            }

            _shutdownFinalized = true;
            UnregisterMessageHandlers();
            UnregisterNetworkCallbacks();
            _launchMode = MatchLaunchMode.None;
            _seatAssignments.Clear();
            _localAssignedSeat = null;
            _lastSnapshotJson = string.Empty;
            _lastStableSnapshotJson = string.Empty;
            _networkSessionInitialized = false;
            _nextSnapshotSendAt = 0f;
            _nextDiagnosticLogAt = 0f;
            _snapshotBroadcastCount = 0;
            _receivedFirstSnapshot = false;
            _sentDisplayResolutionSnapshot = false;
            _onlineQuickMatchActive = false;
            _onlineIsLobbyHost = false;
            _onlineLobby = null;
            _onlineConnectionState = OnlineConnectionState.Offline;
            _nextLobbyHeartbeatAt = 0f;
            _nextLobbyPollAt = 0f;
            _lobbyHeartbeatInFlight = false;
            _lobbyPollInFlight = false;
            _shutdownRequested = false;
            _clientStartPending = false;
            _reconnectWaitActive = false;
            _reconnectAllowClientRetry = false;
            _manualReconnectCancel = false;
            _returningMatchToMenu = false;
            _reconnectWaitMessage = string.Empty;
            _reconnectEndsAt = 0f;
            _nextReconnectAttemptAt = 0f;

            if (uiManager != null)
            {
                uiManager.SetExternalCommandSink(null);
                uiManager.HideReconnectWait();
                uiManager.ShowLaunchModePicker(statusMessage);
            }
        }

        public bool TryHandleUiAction(MatchUiAction action)
        {
            if (_launchMode != MatchLaunchMode.MultiplayerClient
                && !(_launchMode == MatchLaunchMode.OnlineQuickMatch && _networkManager != null && !_networkManager.IsServer))
            {
                return false;
            }

            if (_networkManager == null || !_networkManager.IsClient || !_networkManager.IsConnectedClient)
            {
                Debug.Log($"[LocalMultiplayer] Client action {action.actionType} ignored while waiting for host connection.");
                return true;
            }

            if (!_localAssignedSeat.HasValue)
            {
                Debug.Log($"[LocalMultiplayer] Client action {action.actionType} ignored while waiting for seat assignment.");
                return true;
            }

            SendUiActionToServer(action);
            return true;
        }

        private void EnsureNetworkingComponents()
        {
            _networkManager = GetComponent<NetworkManager>();
            if (_networkManager == null)
            {
                _networkManager = gameObject.AddComponent<NetworkManager>();
            }

            _transport = GetComponent<UnityTransport>();
            if (_transport == null)
            {
                _transport = gameObject.AddComponent<UnityTransport>();
            }

            if (_networkManager.NetworkConfig == null)
            {
                _networkManager.NetworkConfig = new NetworkConfig();
            }

            _networkManager.NetworkConfig.NetworkTransport = _transport;
        }

        private void ConfigureTransport()
        {
            if (_transport != null)
            {
                string listenAddress = _launchMode == MatchLaunchMode.MultiplayerHost
                    || _launchMode == MatchLaunchMode.DedicatedServer
                        ? "0.0.0.0"
                        : string.Empty;
                _transport.UseWebSockets = useWebSockets;
                _transport.SetConnectionData(connectAddress, port, listenAddress);

                var connectionData = _transport.ConnectionData;
                connectionData.ClientBindPort = 0;
                connectionData.WebSocketPath = string.IsNullOrWhiteSpace(webSocketPath) ? "/" : webSocketPath;
                _transport.ConnectionData = connectionData;
            }
        }

        private void LogConnectionSettings(string context)
        {
            string path = "/";
            if (_transport != null)
            {
                path = string.IsNullOrWhiteSpace(_transport.ConnectionData.WebSocketPath)
                    ? "/"
                    : _transport.ConnectionData.WebSocketPath;
            }

            Debug.Log($"[LocalMultiplayer][Diag] {context}: mode={_launchMode}, address={connectAddress}, port={port}, transport={(useWebSockets ? "WebSocket/TCP" : "UDP")}, wsPath={path}, pageHostAddress={useWebGlPageHostAsAddress}.");
        }

        private void TickDiagnosticLog()
        {
            if (_networkManager == null || Time.unscaledTime < _nextDiagnosticLogAt)
            {
                return;
            }

            bool interesting = _networkManager.IsListening
                || _clientStartPending
                || _reconnectWaitActive
                || _launchMode == MatchLaunchMode.MultiplayerClient
                || _launchMode == MatchLaunchMode.DedicatedServer
                || _launchMode == MatchLaunchMode.MultiplayerHost;
            if (!interesting)
            {
                return;
            }

            _nextDiagnosticLogAt = Time.unscaledTime + DiagnosticLogIntervalSeconds;
            LogNetworkState("Heartbeat");
        }

        private void LogNetworkState(string context)
        {
            if (_networkManager == null)
            {
                Debug.Log($"[LocalMultiplayer][Diag] {context}: NetworkManager missing.");
                return;
            }

            string connectedIds = BuildConnectedClientIdList();
            string assignments = BuildSeatAssignmentList();
            Debug.Log($"[LocalMultiplayer][Diag] {context}: mode={_launchMode}, listening={_networkManager.IsListening}, server={_networkManager.IsServer}, client={_networkManager.IsClient}, connectedClient={_networkManager.IsConnectedClient}, localClientId={_networkManager.LocalClientId}, connectedIds=[{connectedIds}], seats=[{assignments}], initialized={_networkSessionInitialized}, reconnect={_reconnectWaitActive}, shutdown={_shutdownRequested}.");
        }

        private string BuildConnectedClientIdList()
        {
            if (_networkManager == null)
            {
                return string.Empty;
            }

            string result = string.Empty;
            foreach (ulong clientId in _networkManager.ConnectedClientsIds)
            {
                if (!string.IsNullOrEmpty(result))
                {
                    result += ",";
                }

                result += clientId.ToString();
            }

            return result;
        }

        private string BuildSeatAssignmentList()
        {
            string result = string.Empty;
            foreach (KeyValuePair<ulong, MatchSeat> pair in _seatAssignments)
            {
                if (!string.IsNullOrEmpty(result))
                {
                    result += ",";
                }

                result += $"{pair.Key}:{pair.Value}";
            }

            return result;
        }

        private void ShutdownTransportIfRunning()
        {
            if (_networkManager != null && (_networkManager.IsListening || _networkManager.ShutdownInProgress))
            {
                _networkManager.Shutdown();
            }
        }

        private void PrepareClientUiForRemoteControl()
        {
            if (uiManager == null)
            {
                return;
            }

            uiManager.SetLaunchModeStatus("Waiting for host seat assignment...");
            uiManager.SetExternalCommandSink(this);
        }

        private void InitializeHostSession()
        {
            MatchSeat hostSeat = _launchMode == MatchLaunchMode.OnlineQuickMatch ? _onlineHostSeat : MatchSeat.SeatOne;
            _localAssignedSeat = hostSeat;
            _lastSnapshotJson = string.Empty;
            _lastStableSnapshotJson = string.Empty;
            _nextSnapshotSendAt = 0f;
            _sentDisplayResolutionSnapshot = false;
            _networkSessionInitialized = true;
            if (_launchMode == MatchLaunchMode.OnlineQuickMatch)
            {
                _onlineConnectionState = OnlineConnectionState.ChoosingArena;
            }

            if (uiManager != null)
            {
                uiManager.BeginSeatAssignedSession(hostSeat);
                uiManager.ConfigureNetworkPerspective(hostSeat);
                uiManager.SetExternalCommandSink(null);
                uiManager.UpdateUI();
            }
        }

        private void InitializeOnlineClientSession(MatchSeat selectedSeat)
        {
            _localAssignedSeat = selectedSeat;
            _lastSnapshotJson = string.Empty;
            _lastStableSnapshotJson = string.Empty;
            _nextSnapshotSendAt = 0f;
            _sentDisplayResolutionSnapshot = false;
            _onlineConnectionState = OnlineConnectionState.ChoosingArena;

            if (uiManager != null)
            {
                uiManager.BeginSeatAssignedSession(selectedSeat);
                uiManager.ConfigureNetworkPerspective(selectedSeat);
                uiManager.SetExternalCommandSink(this);
                uiManager.UpdateUI();
            }

            Debug.Log($"[OnlineMatch] Local online client session initialized as {selectedSeat} before host confirmation.");
        }

        private void RegisterUiCallbacks()
        {
            if (uiManager == null)
            {
                return;
            }

            uiManager.LaunchModeSelected -= HandleLaunchModeSelected;
            uiManager.LaunchModeSelected += HandleLaunchModeSelected;
            uiManager.OnlineQuickMatchRequested -= HandleOnlineQuickMatchRequested;
            uiManager.OnlineQuickMatchRequested += HandleOnlineQuickMatchRequested;
            uiManager.ReconnectBackToMenuRequested -= HandleReconnectBackToMenuRequested;
            uiManager.ReconnectBackToMenuRequested += HandleReconnectBackToMenuRequested;
            uiManager.MatchBackToMenuRequested -= HandleMatchBackToMenuRequested;
            uiManager.MatchBackToMenuRequested += HandleMatchBackToMenuRequested;
        }

        private void UnregisterUiCallbacks()
        {
            if (uiManager == null)
            {
                return;
            }

            uiManager.LaunchModeSelected -= HandleLaunchModeSelected;
            uiManager.OnlineQuickMatchRequested -= HandleOnlineQuickMatchRequested;
            uiManager.ReconnectBackToMenuRequested -= HandleReconnectBackToMenuRequested;
            uiManager.MatchBackToMenuRequested -= HandleMatchBackToMenuRequested;
        }

        private void HandleLaunchModeSelected(MatchLaunchMode launchMode)
        {
            switch (launchMode)
            {
                case MatchLaunchMode.TurnBased:
                    ShutdownMultiplayer();
                    if (uiManager != null)
                    {
                        uiManager.StartTurnBasedSession();
                    }
                    break;
                case MatchLaunchMode.Testing:
                    ShutdownMultiplayer();
                    if (uiManager != null)
                    {
                        uiManager.StartTestingSession();
                    }
                    break;
                case MatchLaunchMode.MultiplayerHost:
                    if (uiManager != null)
                    {
                        uiManager.SetLaunchModeStatus("Starting multiplayer host...");
                    }

                    StartHostMode();
                    break;
                case MatchLaunchMode.MultiplayerClient:
                    if (uiManager != null)
                    {
                        uiManager.SetLaunchModeStatus("Connecting as multiplayer client...");
                    }

                    StartClientMode();
                    break;
                case MatchLaunchMode.DedicatedServer:
                    if (uiManager != null)
                    {
                        uiManager.SetLaunchModeStatus("Starting dedicated server...");
                    }

                    StartDedicatedServerMode();
                    break;
            }
        }

        private void HandleOnlineQuickMatchRequested(MatchSeat selectedSeat)
        {
            _ = StartOnlineQuickMatchAsync(selectedSeat);
        }

        private async Task StartOnlineQuickMatchAsync(MatchSeat selectedSeat)
        {
            _onlineSelectedSeat = selectedSeat;
            _onlineQuickMatchActive = true;
            _onlineIsLobbyHost = false;
            _onlineLobby = null;
            _onlineHostSeat = MatchSeat.SeatOne;
            _launchMode = MatchLaunchMode.OnlineQuickMatch;
            _manualReconnectCancel = false;
            EndReconnectWait();
            _onlineConnectionState = OnlineConnectionState.Authenticating;

            if (uiManager != null)
            {
                uiManager.SetLaunchModeStatus("Signing in to Unity services...");
            }

            try
            {
                await EnsureUnityServicesReadyAsync();
                _onlineConnectionState = OnlineConnectionState.FindingMatch;
                if (uiManager != null)
                {
                    uiManager.SetLaunchModeStatus($"Looking for {GetSeatDisplayNameForOnline(selectedSeat)} vs {GetSeatDisplayNameForOnline(MatchPerspectiveUtility.GetOpposingSeat(selectedSeat))}...");
                }

                Lobby joinableLobby = await FindJoinableOppositeCityLobbyAsync(selectedSeat);
                if (joinableLobby == null && !ShouldPreferHostingOnlineMatch(selectedSeat))
                {
                    float searchEndsAt = Time.realtimeSinceStartup + QuickMatchGuestSearchSeconds;
                    while (joinableLobby == null && Time.realtimeSinceStartup < searchEndsAt)
                    {
                        if (uiManager != null)
                        {
                            uiManager.SetLaunchModeStatus($"Waiting for {GetSeatDisplayNameForOnline(MatchPerspectiveUtility.GetOpposingSeat(selectedSeat))} host...");
                        }

                        await Task.Delay(QuickMatchGuestSearchDelayMs);
                        joinableLobby = await FindJoinableOppositeCityLobbyAsync(selectedSeat);
                    }
                }

                if (joinableLobby != null)
                {
                    await JoinOnlineLobbyAsClientAsync(joinableLobby, selectedSeat);
                }
                else
                {
                    await CreateOnlineLobbyAsHostAsync(selectedSeat);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OnlineMatch] Quick match failed: {ex}");
                _onlineQuickMatchActive = false;
                _onlineConnectionState = OnlineConnectionState.Failed;
                if (_networkManager != null && (_networkManager.IsListening || _networkManager.ShutdownInProgress))
                {
                    ShutdownTransportIfRunning();
                }

                if (uiManager != null)
                {
                    uiManager.ShowLaunchModePicker($"Online quick match failed: {ex.Message}");
                }
            }
        }

        private async Task EnsureUnityServicesReadyAsync()
        {
            if (!_onlineServicesReady)
            {
                string profile = GetUnityServicesProfile();
                InitializationOptions options = new InitializationOptions().SetProfile(profile);
                await UnityServices.InitializeAsync(options);
                _onlineServicesReady = true;
                Debug.Log($"[OnlineMatch] Unity services initialized with auth profile '{profile}'.");
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[OnlineMatch] Signed in anonymously as {AuthenticationService.Instance.PlayerId}.");
            }
        }

        private async Task<Lobby> FindJoinableOppositeCityLobbyAsync(MatchSeat selectedSeat)
        {
            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions
            {
                Count = 25
            });

            MatchSeat wantedHostSeat = MatchPerspectiveUtility.GetOpposingSeat(selectedSeat);
            Debug.Log($"[OnlineMatch] Query found {response.Results.Count} lobbies. Looking for host={wantedHostSeat}, wanted={selectedSeat}.");
            foreach (Lobby lobby in response.Results)
            {
                if (!IsFreshQuickMatchLobby(lobby))
                {
                    Debug.Log($"[OnlineMatch] Skipping stale lobby {lobby.Id}; last updated {lobby.LastUpdated:O}.");
                    continue;
                }

                if (lobby.AvailableSlots <= 0)
                {
                    Debug.Log($"[OnlineMatch] Skipping full lobby {lobby.Id}.");
                    continue;
                }

                if (!TryReadLobbySeat(lobby, LobbyDataHostSeat, out MatchSeat hostSeat) || hostSeat != wantedHostSeat)
                {
                    Debug.Log($"[OnlineMatch] Skipping lobby {lobby.Id}; host seat mismatch.");
                    continue;
                }

                if (!TryReadLobbySeat(lobby, LobbyDataWantedSeat, out MatchSeat wantedSeat) || wantedSeat != selectedSeat)
                {
                    Debug.Log($"[OnlineMatch] Skipping lobby {lobby.Id}; wanted seat mismatch.");
                    continue;
                }

                if (!TryReadLobbyString(lobby, LobbyDataStatus, out string status) || status != LobbyStatusWaiting)
                {
                    Debug.Log($"[OnlineMatch] Skipping lobby {lobby.Id}; status is '{status}'.");
                    continue;
                }

                if (!TryReadLobbyString(lobby, LobbyDataRelayJoinCode, out string joinCode) || string.IsNullOrWhiteSpace(joinCode))
                {
                    Debug.Log($"[OnlineMatch] Skipping lobby {lobby.Id}; no Relay join code.");
                    continue;
                }

                Debug.Log($"[OnlineMatch] Joining lobby {lobby.Id} hosted by {hostSeat}.");
                return lobby;
            }

            return null;
        }

        private async Task CreateOnlineLobbyAsHostAsync(MatchSeat selectedSeat)
        {
            _onlineIsLobbyHost = true;
            _onlineHostSeat = selectedSeat;

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            _transport.SetRelayServerData(allocation.ToRelayServerData(GetRelayConnectionType()));
            _transport.UseWebSockets = IsRelayUsingWebSockets();

            RegisterMessageHandlers();
            RegisterNetworkCallbacks();
            _seatAssignments.Clear();
            _localAssignedSeat = selectedSeat;
            _seatAssignments[_networkManager.LocalClientId] = selectedSeat;
            _networkSessionInitialized = false;
            _lastSnapshotJson = string.Empty;
            _lastStableSnapshotJson = string.Empty;
            _sentDisplayResolutionSnapshot = false;

            Debug.Log($"[OnlineMatch] Starting Relay host as {selectedSeat} using {GetRelayConnectionType()}.");
            if (!_networkManager.StartHost())
            {
                throw new InvalidOperationException("Relay host failed to start.");
            }
            LogNetworkState("Online Relay StartHost succeeded");

            string lobbyName = $"Cardz {GetSeatDisplayNameForOnline(selectedSeat)}";
            _onlineLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, 2, new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    [LobbyDataHostSeat] = PublicLobbyData(selectedSeat.ToString()),
                    [LobbyDataWantedSeat] = PublicLobbyData(MatchPerspectiveUtility.GetOpposingSeat(selectedSeat).ToString()),
                    [LobbyDataRelayJoinCode] = PublicLobbyData(joinCode),
                    [LobbyDataStatus] = PublicLobbyData(LobbyStatusWaiting)
                }
            });

            if (uiManager != null)
            {
                uiManager.SetExternalCommandSink(null);
                uiManager.SetLaunchModeStatus($"Waiting for {GetSeatDisplayNameForOnline(MatchPerspectiveUtility.GetOpposingSeat(selectedSeat))}...");
            }

            _onlineConnectionState = OnlineConnectionState.WaitingForOpponent;
            _nextLobbyHeartbeatAt = 0f;
            _nextLobbyPollAt = 0f;
            Debug.Log($"[OnlineMatch] Created lobby {_onlineLobby.Id} as {selectedSeat}. Relay join code {joinCode}.");
        }

        private async Task JoinOnlineLobbyAsClientAsync(Lobby joinableLobby, MatchSeat selectedSeat)
        {
            _onlineIsLobbyHost = false;
            _onlineLobby = await LobbyService.Instance.JoinLobbyByIdAsync(joinableLobby.Id);
            if (!TryReadLobbyString(_onlineLobby, LobbyDataRelayJoinCode, out string joinCode) || string.IsNullOrWhiteSpace(joinCode))
            {
                throw new InvalidOperationException("Matched lobby did not contain a Relay join code.");
            }

            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            _transport.SetRelayServerData(allocation.ToRelayServerData(GetRelayConnectionType()));
            _transport.UseWebSockets = IsRelayUsingWebSockets();

            PrepareClientUiForRemoteControl();
            RegisterMessageHandlers();
            RegisterNetworkCallbacks();
            Debug.Log($"[OnlineMatch] Starting Relay client as {selectedSeat} using {GetRelayConnectionType()}.");
            if (!_networkManager.StartClient())
            {
                throw new InvalidOperationException("Relay client failed to start.");
            }
            LogNetworkState("Online Relay StartClient succeeded");
            InitializeOnlineClientSession(selectedSeat);

            if (uiManager != null)
            {
                uiManager.SetLaunchModeStatus("Match found. Connecting through Relay...");
            }

            _onlineConnectionState = OnlineConnectionState.InMatch;
            _nextLobbyPollAt = Time.unscaledTime + LobbyPollIntervalSeconds;
            Debug.Log($"[OnlineMatch] Joined lobby {_onlineLobby.Id} as {selectedSeat}. Relay join code {joinCode}.");
        }

        private void RegisterNetworkCallbacks()
        {
            if (_networkManager == null)
            {
                return;
            }

            _networkManager.OnClientConnectedCallback -= HandleClientConnected;
            _networkManager.OnClientConnectedCallback += HandleClientConnected;
            _networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            _networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            _networkManager.OnTransportFailure -= HandleTransportFailure;
            _networkManager.OnTransportFailure += HandleTransportFailure;
            _networkManager.OnClientStopped -= HandleClientStopped;
            _networkManager.OnClientStopped += HandleClientStopped;
            _networkManager.OnServerStopped -= HandleServerStopped;
            _networkManager.OnServerStopped += HandleServerStopped;
        }

        private void UnregisterNetworkCallbacks()
        {
            if (_networkManager == null)
            {
                return;
            }

            _networkManager.OnClientConnectedCallback -= HandleClientConnected;
            _networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            _networkManager.OnTransportFailure -= HandleTransportFailure;
            _networkManager.OnClientStopped -= HandleClientStopped;
            _networkManager.OnServerStopped -= HandleServerStopped;
        }

        private void RegisterMessageHandlers()
        {
            if (_messageHandlersRegistered || _networkManager == null || _networkManager.CustomMessagingManager == null)
            {
                if (!_messageHandlersRegistered && _networkManager != null && _networkManager.CustomMessagingManager == null)
                {
                    Debug.Log("[LocalMultiplayer][Diag] CustomMessagingManager is not ready yet; message handlers not registered.");
                }

                return;
            }

            _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(SeatAssignmentMessageName, HandleSeatAssignmentMessage);
            _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(SnapshotMessageName, HandleSnapshotMessage);
            _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(TimerSyncMessageName, HandleTimerSyncMessage);
            _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(UiActionMessageName, HandleUiActionMessage);
            _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(ReturnToMenuMessageName, HandleReturnToMenuMessage);
            _messageHandlersRegistered = true;
            Debug.Log("[LocalMultiplayer][Diag] Custom message handlers registered.");
        }

        private void UnregisterMessageHandlers()
        {
            if (!_messageHandlersRegistered || _networkManager == null || _networkManager.CustomMessagingManager == null)
            {
                return;
            }

            _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(SeatAssignmentMessageName);
            _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(SnapshotMessageName);
            _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(TimerSyncMessageName);
            _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(UiActionMessageName);
            _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(ReturnToMenuMessageName);
            _messageHandlersRegistered = false;
            Debug.Log("[LocalMultiplayer][Diag] Custom message handlers unregistered.");
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (_networkManager == null)
            {
                return;
            }

            Debug.Log($"[LocalMultiplayer][Diag] OnClientConnected clientId={clientId}, localClientId={_networkManager.LocalClientId}, isServer={_networkManager.IsServer}, isClient={_networkManager.IsClient}.");
            LogNetworkState("After OnClientConnected");

            if (_networkManager.IsServer)
            {
                _returningMatchToMenu = false;
                if (_seatAssignments.ContainsKey(clientId))
                {
                    Debug.Log($"[LocalMultiplayer][Diag] Client {clientId} already has seat {_seatAssignments[clientId]}.");
                    return;
                }

                if (!TryGetNextAvailableSeat(out MatchSeat assignedSeat))
                {
                    Debug.LogWarning($"[LocalMultiplayer] Rejecting client {clientId}; both test seats are already occupied.");
                    _networkManager.DisconnectClient(clientId);
                    return;
                }

                _seatAssignments[clientId] = assignedSeat;
                SendSeatAssignment(clientId, assignedSeat);
                Debug.Log($"[LocalMultiplayer] Assigned client {clientId} to {assignedSeat}.");
                LogNetworkState("After seat assignment");
                if (_onlineQuickMatchActive && _onlineIsLobbyHost)
                {
                    _ = MarkOnlineLobbyMatchedAsync();
                }

                if (HasRequiredPlayersConnected())
                {
                    if (!_networkSessionInitialized)
                    {
                        InitializeHostSession();
                    }

                    EndReconnectWait();
                    BroadcastSnapshotIfChanged(force: true);
                }
            }
            else if (IsClientOnlyLaunchMode() && clientId == _networkManager.LocalClientId)
            {
                EndReconnectWait();
                Debug.Log("[LocalMultiplayer] Client connected to host, awaiting seat assignment.");
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            string reason = _networkManager != null ? _networkManager.DisconnectReason : string.Empty;
            Debug.LogWarning($"[LocalMultiplayer][Diag] OnClientDisconnected clientId={clientId}, reason='{reason}', isServer={(_networkManager != null && _networkManager.IsServer)}, isClient={(_networkManager != null && _networkManager.IsClient)}.");
            LogNetworkState("After OnClientDisconnected");

            if (_networkManager != null && _networkManager.IsServer)
            {
                _seatAssignments.Remove(clientId);

                if (_returningMatchToMenu)
                {
                    return;
                }

                if (clientId != _networkManager.LocalClientId)
                {
                    BeginReconnectWait("Opponent disconnected. Waiting for reconnect...", false);
                }
            }

            if (_networkManager != null && !_networkManager.IsServer && clientId == _networkManager.LocalClientId)
            {
                _localAssignedSeat = null;
                BeginReconnectWait("Connection lost. Trying to reconnect...", true);
                Debug.LogWarning("[LocalMultiplayer] Client disconnected from host.");
            }
        }

        private void HandleTransportFailure()
        {
            Debug.LogWarning("[LocalMultiplayer] Transport failure detected.");
            if (IsClientOnlyLaunchMode() && !_manualReconnectCancel)
            {
                BeginReconnectWait("Connection lost. Trying to reconnect...", true);
                return;
            }

            ShutdownMultiplayer();
        }

        private void HandleClientStopped(bool wasHost)
        {
            if (_returningMatchToMenu)
            {
                FinalizeShutdown("Match ended. Choose a mode to start again.");
                return;
            }

            if (!wasHost && IsClientOnlyLaunchMode() && !_manualReconnectCancel)
            {
                BeginReconnectWait("Connection lost. Trying to reconnect...", true);
                return;
            }

            FinalizeShutdown(wasHost
                ? "Host stopped. Choose a mode to start again."
                : "Client disconnected. Choose a mode to reconnect.");
        }

        private void HandleServerStopped(bool wasClient)
        {
            if (_returningMatchToMenu)
            {
                FinalizeShutdown("Match ended. Choose a mode to start again.");
                return;
            }

            if (wasClient && IsClientOnlyLaunchMode() && !_manualReconnectCancel)
            {
                BeginReconnectWait("Host stopped. Waiting for reconnect...", true);
                return;
            }

            FinalizeShutdown(wasClient
                ? "Host stopped. Choose a mode to start again."
                : "Server stopped. Choose a mode to start again.");
        }

        private void HandleReconnectBackToMenuRequested()
        {
            _manualReconnectCancel = true;
            EndReconnectWait();
            ShutdownMultiplayer();

            if (uiManager != null)
            {
                uiManager.ShowLaunchModePicker("Reconnect cancelled.");
            }
        }

        private void HandleMatchBackToMenuRequested()
        {
            if (_networkManager == null || !_networkManager.IsServer)
            {
                _manualReconnectCancel = true;
                ShutdownMultiplayer();
                if (uiManager != null)
                {
                    uiManager.ShowLaunchModePicker("Match ended. Choose a mode to start again.");
                }

                return;
            }

            ReturnServerMatchToWaitingMenu();
        }

        private void ReturnServerMatchToWaitingMenu()
        {
            _returningMatchToMenu = true;
            EndReconnectWait();
            SendReturnToMenuToConnectedClients();

            if (IsDedicatedServerMode())
            {
                CancelInvoke(nameof(DisconnectRemoteClientsForMenuReset));
                Invoke(nameof(DisconnectRemoteClientsForMenuReset), 0.25f);
                if (uiManager != null)
                {
                    uiManager.SetExternalCommandSink(null);
                    uiManager.ShowLaunchModePicker("Dedicated server waiting for the next two clients...");
                }

                Debug.Log("[LocalMultiplayer] Dedicated server reset to waiting state after match end.");
                return;
            }

            ShutdownMultiplayer();
        }

        private void DisconnectRemoteClientsForMenuReset()
        {
            if (_networkManager == null || !_networkManager.IsServer)
            {
                return;
            }

            List<ulong> clientIds = new List<ulong>(_networkManager.ConnectedClientsIds);
            for (int i = 0; i < clientIds.Count; i++)
            {
                if (clientIds[i] == _networkManager.LocalClientId)
                {
                    continue;
                }

                _networkManager.DisconnectClient(clientIds[i]);
            }

            _seatAssignments.Clear();
            _localAssignedSeat = null;
            _networkSessionInitialized = false;
            _lastSnapshotJson = string.Empty;
            _lastStableSnapshotJson = string.Empty;
            _nextSnapshotSendAt = 0f;
            _sentDisplayResolutionSnapshot = false;
        }

        private void SendReturnToMenuToConnectedClients()
        {
            if (_networkManager == null || !_networkManager.IsServer || _networkManager.CustomMessagingManager == null)
            {
                return;
            }

            foreach (ulong clientId in _networkManager.ConnectedClientsIds)
            {
                if (clientId == _networkManager.LocalClientId)
                {
                    continue;
                }

                using var writer = new FastBufferWriter(1, Allocator.Temp, 8);
                _networkManager.CustomMessagingManager.SendNamedMessage(ReturnToMenuMessageName, clientId, writer);
            }
        }

        private void BeginReconnectWait(string message, bool allowClientRetry)
        {
            if (_manualReconnectCancel || _launchMode == MatchLaunchMode.None)
            {
                return;
            }

            bool wasAlreadyWaiting = _reconnectWaitActive;
            float existingEndsAt = _reconnectEndsAt;
            _reconnectWaitActive = true;
            _reconnectAllowClientRetry = _reconnectAllowClientRetry || allowClientRetry;
            _reconnectWaitMessage = string.IsNullOrWhiteSpace(message)
                ? "Trying to keep this match alive."
                : message;
            _reconnectEndsAt = wasAlreadyWaiting && existingEndsAt > Time.unscaledTime
                ? existingEndsAt
                : Time.unscaledTime + ReconnectWindowSeconds;
            if (allowClientRetry && (!wasAlreadyWaiting || _nextReconnectAttemptAt <= 0f))
            {
                _nextReconnectAttemptAt = Time.unscaledTime + 0.2f;
            }
            _clientStartPending = false;

            if (allowClientRetry && _networkManager != null && _networkManager.IsListening && !_networkManager.ShutdownInProgress)
            {
                ShutdownTransportIfRunning();
            }

            if (uiManager != null)
            {
                float remaining = Mathf.Max(0f, _reconnectEndsAt - Time.unscaledTime);
                uiManager.ShowReconnectWait(_reconnectWaitMessage, Mathf.CeilToInt(remaining));
            }
        }

        private void EndReconnectWait()
        {
            if (!_reconnectWaitActive)
            {
                return;
            }

            _reconnectWaitActive = false;
            _reconnectAllowClientRetry = false;
            _reconnectWaitMessage = string.Empty;
            _reconnectEndsAt = 0f;
            _nextReconnectAttemptAt = 0f;

            if (uiManager != null)
            {
                uiManager.HideReconnectWait();
            }
        }

        private void TickReconnectWait()
        {
            if (!_reconnectWaitActive)
            {
                return;
            }

            float remaining = Mathf.Max(0f, _reconnectEndsAt - Time.unscaledTime);
            if (uiManager != null)
            {
                uiManager.ShowReconnectWait(_reconnectWaitMessage, Mathf.CeilToInt(remaining));
            }

            if (_reconnectAllowClientRetry
                && !_shutdownRequested
                && _networkManager != null
                && !_networkManager.IsListening
                && !_networkManager.ShutdownInProgress
                && Time.unscaledTime >= _nextReconnectAttemptAt)
            {
                _nextReconnectAttemptAt = Time.unscaledTime + ReconnectAttemptIntervalSeconds;
                if (_launchMode != MatchLaunchMode.OnlineQuickMatch)
                {
                    ConfigureTransport();
                }

                TryStartClientConnection();
            }

            if (remaining > 0f)
            {
                return;
            }

            _manualReconnectCancel = true;
            EndReconnectWait();
            ShutdownMultiplayer();

            if (uiManager != null)
            {
                uiManager.ShowLaunchModePicker("Reconnect timed out. Choose a mode to start again.");
            }
        }

        private void SendSeatAssignment(ulong clientId, MatchSeat seat)
        {
            Debug.Log($"[LocalMultiplayer][Diag] Sending seat assignment {seat} to client {clientId}.");
            using var writer = new FastBufferWriter(sizeof(int), Allocator.Temp, 64);
            writer.WriteValueSafe((int)seat);
            _networkManager.CustomMessagingManager.SendNamedMessage(SeatAssignmentMessageName, clientId, writer);
        }

        private void SendUiActionToServer(MatchUiAction action)
        {
            using var writer = new FastBufferWriter(sizeof(int) * 6, Allocator.Temp, 128);
            writer.WriteValueSafe((int)action.actionType);
            writer.WriteValueSafe(action.handIndex);
            writer.WriteValueSafe(action.tileIndex);
            writer.WriteValueSafe((int)action.targetSeat);
            writer.WriteValueSafe(action.clickCount);
            writer.WriteValueSafe((int)action.arenaId);
            Debug.Log($"[LocalMultiplayer][Diag] Sending action {action.actionType} to server: hand={action.handIndex}, tile={action.tileIndex}, targetSeat={action.targetSeat}, arena={action.arenaId}.");
            _networkManager.CustomMessagingManager.SendNamedMessage(
                UiActionMessageName,
                NetworkManager.ServerClientId,
                writer,
                NetworkDelivery.ReliableSequenced);
        }

        private void BroadcastSnapshotIfChanged(bool force = false)
        {
            if (_networkManager == null || !_networkManager.IsServer || uiManager == null || !_networkSessionInitialized)
            {
                return;
            }

            bool displayResolutionActive = uiManager.IsDisplayResolutionActive;
            if (!displayResolutionActive)
            {
                _sentDisplayResolutionSnapshot = false;
            }

            string stableSnapshotJson = uiManager.ExportRuntimeSnapshotStableJson();
            if (!force && stableSnapshotJson == _lastStableSnapshotJson)
            {
                return;
            }

            if (!force && displayResolutionActive && _sentDisplayResolutionSnapshot)
            {
                TryBroadcastTimerSync();
                return;
            }

            string snapshotJson = uiManager.ExportRuntimeSnapshotJson();
            bool payloadChanged = snapshotJson != _lastSnapshotJson;
            _lastStableSnapshotJson = stableSnapshotJson;
            _lastSnapshotJson = snapshotJson;
            if (displayResolutionActive)
            {
                _sentDisplayResolutionSnapshot = true;
            }

            _snapshotBroadcastCount++;
            int sentCount = 0;
            foreach (ulong clientId in _networkManager.ConnectedClientsIds)
            {
                if (clientId == _networkManager.LocalClientId)
                {
                    continue;
                }

                using var writer = new FastBufferWriter(Mathf.Max(1024, snapshotJson.Length * 4 + 32), Allocator.Temp, int.MaxValue);
                writer.WriteValueSafe(snapshotJson);
                _networkManager.CustomMessagingManager.SendNamedMessage(
                    SnapshotMessageName,
                    clientId,
                    writer,
                    NetworkDelivery.ReliableFragmentedSequenced);
                sentCount++;
            }

            if (force || _snapshotBroadcastCount <= 3 || _snapshotBroadcastCount % 20 == 0)
            {
                Debug.Log($"[LocalMultiplayer][Diag] Broadcast snapshot #{_snapshotBroadcastCount}: bytes={snapshotJson.Length}, sentClients={sentCount}, force={force}, payloadChanged={payloadChanged}.");
            }

            TryBroadcastTimerSync(force: true);
        }

        private void TryBroadcastTimerSync(bool force = false)
        {
            if (_networkManager == null
                || !_networkManager.IsServer
                || uiManager == null
                || !_networkSessionInitialized
                || (!force && Time.unscaledTime < _nextTimerSyncSendAt))
            {
                return;
            }

            _nextTimerSyncSendAt = Time.unscaledTime + Mathf.Max(0.1f, timerSyncSendInterval);
            double serverTimeSeconds = GetSynchronizedServerTimeSeconds();
            string timerSyncJson = uiManager.ExportTimerSyncSnapshotJson(serverTimeSeconds);
            if (string.IsNullOrWhiteSpace(timerSyncJson))
            {
                return;
            }

            foreach (ulong clientId in _networkManager.ConnectedClientsIds)
            {
                if (clientId == _networkManager.LocalClientId)
                {
                    continue;
                }

                using var writer = new FastBufferWriter(Mathf.Max(256, timerSyncJson.Length * 4 + 32), Allocator.Temp, 2048);
                writer.WriteValueSafe(timerSyncJson);
                _networkManager.CustomMessagingManager.SendNamedMessage(
                    TimerSyncMessageName,
                    clientId,
                    writer,
                    NetworkDelivery.UnreliableSequenced);
            }
        }

        private void HandleSeatAssignmentMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (_networkManager == null || _networkManager.IsServer)
            {
                return;
            }

            reader.ReadValueSafe(out int seatValue);
            MatchSeat assignedSeat = (MatchSeat)seatValue;
            bool alreadyInitializedOnlineSeat = _launchMode == MatchLaunchMode.OnlineQuickMatch
                && _localAssignedSeat.HasValue
                && _localAssignedSeat.Value == assignedSeat;
            _localAssignedSeat = assignedSeat;
            Debug.Log($"[LocalMultiplayer][Diag] Received raw seat value {seatValue} from host {senderClientId}.");

            if (uiManager != null)
            {
                if (!alreadyInitializedOnlineSeat)
                {
                    uiManager.BeginSeatAssignedSession(_localAssignedSeat.Value);
                }

                uiManager.ConfigureNetworkPerspective(_localAssignedSeat.Value);
                uiManager.SetExternalCommandSink(this);
                uiManager.UpdateUI();
            }

            if (_launchMode == MatchLaunchMode.OnlineQuickMatch)
            {
                _onlineConnectionState = OnlineConnectionState.ChoosingArena;
            }

            Debug.Log(alreadyInitializedOnlineSeat
                ? $"[LocalMultiplayer] Confirmed existing online seat {_localAssignedSeat.Value} from host {senderClientId}."
                : $"[LocalMultiplayer] Received seat assignment {_localAssignedSeat.Value} from host {senderClientId}.");
        }

        private void HandleSnapshotMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (_networkManager == null || _networkManager.IsServer || uiManager == null)
            {
                return;
            }

            reader.ReadValueSafe(out string snapshotJson);
            if (!string.IsNullOrWhiteSpace(snapshotJson))
            {
                if (!_receivedFirstSnapshot)
                {
                    _receivedFirstSnapshot = true;
                    Debug.Log($"[LocalMultiplayer][Diag] Received first snapshot from {senderClientId}: bytes={snapshotJson.Length}.");
                }

                uiManager.ImportRuntimeSnapshotJson(snapshotJson);
            }
            else
            {
                Debug.LogWarning($"[LocalMultiplayer][Diag] Received empty snapshot from {senderClientId}.");
            }
        }

        private void HandleTimerSyncMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (_networkManager == null || _networkManager.IsServer || uiManager == null)
            {
                return;
            }

            reader.ReadValueSafe(out string timerSyncJson);
            if (!string.IsNullOrWhiteSpace(timerSyncJson))
            {
                uiManager.ImportTimerSyncSnapshotJson(timerSyncJson, GetSynchronizedServerTimeSeconds());
            }
        }

        private double GetSynchronizedServerTimeSeconds()
        {
            if (_networkManager != null && _networkManager.IsListening)
            {
                return _networkManager.ServerTime.Time;
            }

            return Time.unscaledTime;
        }

        private void HandleReturnToMenuMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (_networkManager == null || _networkManager.IsServer)
            {
                return;
            }

            _manualReconnectCancel = true;
            _returningMatchToMenu = true;
            EndReconnectWait();
            if (uiManager != null)
            {
                uiManager.SetExternalCommandSink(null);
                uiManager.ShowLaunchModePicker("Match ended. Choose Online Quick Match to join the next test.");
            }

            if (_networkManager.IsListening && !_networkManager.ShutdownInProgress)
            {
                ShutdownTransportIfRunning();
            }

            Debug.Log($"[LocalMultiplayer] Server {senderClientId} returned this client to menu.");
        }

        private void HandleUiActionMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (_networkManager == null || !_networkManager.IsServer || uiManager == null)
            {
                return;
            }

            if (!_seatAssignments.TryGetValue(senderClientId, out MatchSeat assignedSeat))
            {
                Debug.LogWarning($"[LocalMultiplayer] Ignored action from unassigned client {senderClientId}.");
                LogNetworkState("Ignored unassigned client action");
                return;
            }

            var action = new MatchUiAction();
            reader.ReadValueSafe(out int actionType);
            reader.ReadValueSafe(out action.handIndex);
            reader.ReadValueSafe(out action.tileIndex);
            reader.ReadValueSafe(out int targetSeatValue);
            reader.ReadValueSafe(out action.clickCount);
            reader.ReadValueSafe(out int arenaIdValue);
            action.actionType = (MatchUiActionType)actionType;
            action.targetSeat = (MatchSeat)targetSeatValue;
            action.arenaId = (ArenaId)arenaIdValue;
            Debug.Log($"[LocalMultiplayer][Diag] Received action {action.actionType} from client {senderClientId} as {assignedSeat}: hand={action.handIndex}, tile={action.tileIndex}, targetSeat={action.targetSeat}, arena={action.arenaId}.");

            if (action.actionType == MatchUiActionType.BackToMenu)
            {
                ReturnServerMatchToWaitingMenu();
                return;
            }

            uiManager.ApplyRemoteUiActionForSeat(assignedSeat, action);
            _lastSnapshotJson = string.Empty;
            _lastStableSnapshotJson = string.Empty;
            _sentDisplayResolutionSnapshot = false;
            BroadcastSnapshotIfChanged(force: true);
        }

        private void EnsureInitializedForCurrentNetworkMode()
        {
            if (_networkManager == null || !_networkManager.IsListening)
            {
                return;
            }

            if (_returningMatchToMenu)
            {
                return;
            }

            if (!_messageHandlersRegistered)
            {
                RegisterMessageHandlers();
                RegisterNetworkCallbacks();
            }

            if (_networkManager.IsServer)
            {
                if (_launchMode != MatchLaunchMode.MultiplayerHost
                    && _launchMode != MatchLaunchMode.DedicatedServer
                    && _launchMode != MatchLaunchMode.OnlineQuickMatch)
                {
                    _launchMode = _networkManager.IsHost
                        ? MatchLaunchMode.MultiplayerHost
                        : MatchLaunchMode.DedicatedServer;
                }

                if (_launchMode == MatchLaunchMode.MultiplayerHost
                    || _launchMode == MatchLaunchMode.OnlineQuickMatch)
                {
                    MatchSeat hostSeat = _launchMode == MatchLaunchMode.OnlineQuickMatch ? _onlineHostSeat : MatchSeat.SeatOne;
                    if (!_seatAssignments.ContainsKey(_networkManager.LocalClientId))
                    {
                        _seatAssignments[_networkManager.LocalClientId] = hostSeat;
                    }
                }

                if (_launchMode == MatchLaunchMode.MultiplayerHost
                    && !_seatAssignments.ContainsKey(_networkManager.LocalClientId))
                {
                    _seatAssignments[_networkManager.LocalClientId] = MatchSeat.SeatOne;
                }

                if (!_networkSessionInitialized)
                {
                    if (HasRequiredPlayersConnected())
                    {
                        InitializeHostSession();
                        Debug.Log(IsDedicatedServerMode()
                            ? "[LocalMultiplayer] Dedicated server session auto-initialized from active NetworkManager."
                            : "[LocalMultiplayer] Host/online session auto-initialized from active NetworkManager.");
                    }
                    else if (uiManager != null)
                    {
                        uiManager.SetLaunchModeStatus(IsDedicatedServerMode()
                            ? "Dedicated server is waiting for two clients..."
                            : "Host is waiting for a client to connect...");
                    }
                }

                return;
            }

            if (_networkManager.IsClient)
            {
                if (_launchMode != MatchLaunchMode.MultiplayerClient && _launchMode != MatchLaunchMode.OnlineQuickMatch)
                {
                    _launchMode = MatchLaunchMode.MultiplayerClient;
                    PrepareClientUiForRemoteControl();
                    Debug.Log("[LocalMultiplayer] Client session auto-initialized from active NetworkManager.");
                }
                else if (!_localAssignedSeat.HasValue && uiManager != null)
                {
                    uiManager.SetLaunchModeStatus("Client is waiting for host seat assignment...");
                }
            }
        }

        private static string GetCommandLineValue(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return string.Empty;
        }

        private void ApplyConnectionSettingsFromStartupArgs()
        {
            string commandLineAddress = GetCommandLineValue("-lfcAddress");
            if (!string.IsNullOrWhiteSpace(commandLineAddress))
            {
                connectAddress = commandLineAddress;
            }

            string commandLinePort = GetCommandLineValue("-lfcPort");
            if (ushort.TryParse(commandLinePort, out ushort parsedPort))
            {
                port = parsedPort;
            }

            string transport = GetCommandLineValue("-lfcTransport");
            if (string.Equals(transport, "websocket", StringComparison.OrdinalIgnoreCase)
                || string.Equals(transport, "websockets", StringComparison.OrdinalIgnoreCase)
                || string.Equals(transport, "ws", StringComparison.OrdinalIgnoreCase))
            {
                useWebSockets = true;
            }
            else if (string.Equals(transport, "udp", StringComparison.OrdinalIgnoreCase))
            {
                useWebSockets = false;
            }

            string path = GetCommandLineValue("-lfcWebSocketPath");
            if (!string.IsNullOrWhiteSpace(path))
            {
                webSocketPath = path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            useWebSockets = true;
            ApplyWebGlUrlConnectionSettings();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private void ApplyWebGlUrlConnectionSettings()
        {
            if (string.IsNullOrWhiteSpace(Application.absoluteURL))
            {
                return;
            }

            try
            {
                var uri = new Uri(Application.absoluteURL);
                string address = GetQueryValue(uri.Query, "lfcAddress");
                if (!string.IsNullOrWhiteSpace(address))
                {
                    connectAddress = address;
                }
                else if (useWebGlPageHostAsAddress && !string.IsNullOrWhiteSpace(uri.Host))
                {
                    connectAddress = uri.Host;
                }

                string portValue = GetQueryValue(uri.Query, "lfcPort");
                if (ushort.TryParse(portValue, out ushort parsedPort))
                {
                    port = parsedPort;
                }

                string path = GetQueryValue(uri.Query, "lfcWebSocketPath");
                if (!string.IsNullOrWhiteSpace(path))
                {
                    webSocketPath = path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalMultiplayer] Failed to parse WebGL URL connection settings: {ex.Message}");
            }
        }
#endif

        private static string GetQueryValue(string query, string key)
        {
            if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            string trimmedQuery = query.StartsWith("?", StringComparison.Ordinal) ? query.Substring(1) : query;
            string[] pairs = trimmedQuery.Split('&');
            for (int i = 0; i < pairs.Length; i++)
            {
                string[] parts = pairs[i].Split(new[] { '=' }, 2);
                if (parts.Length == 0 || !string.Equals(Uri.UnescapeDataString(parts[0]), key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            }

            return string.Empty;
        }

        private bool TryGetNextAvailableSeat(out MatchSeat seat)
        {
            if (_onlineQuickMatchActive && _onlineIsLobbyHost)
            {
                seat = MatchPerspectiveUtility.GetOpposingSeat(_onlineHostSeat);
                return !_seatAssignments.ContainsValue(seat);
            }

            if (!_seatAssignments.ContainsValue(MatchSeat.SeatOne))
            {
                seat = MatchSeat.SeatOne;
                return true;
            }

            if (!_seatAssignments.ContainsValue(MatchSeat.SeatTwo))
            {
                seat = MatchSeat.SeatTwo;
                return true;
            }

            seat = MatchSeat.SeatOne;
            return false;
        }

        private async void TickOnlineLobbyMaintenance()
        {
            if (!_onlineQuickMatchActive || _onlineLobby == null)
            {
                return;
            }

            if (_onlineIsLobbyHost && !_lobbyHeartbeatInFlight && Time.unscaledTime >= _nextLobbyHeartbeatAt)
            {
                _nextLobbyHeartbeatAt = Time.unscaledTime + LobbyHeartbeatIntervalSeconds;
                _lobbyHeartbeatInFlight = true;
                try
                {
                    await LobbyService.Instance.SendHeartbeatPingAsync(_onlineLobby.Id);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[OnlineMatch] Lobby heartbeat failed: {ex.Message}");
                }
                finally
                {
                    _lobbyHeartbeatInFlight = false;
                }
            }

            if (!_lobbyPollInFlight && Time.unscaledTime >= _nextLobbyPollAt)
            {
                _nextLobbyPollAt = Time.unscaledTime + LobbyPollIntervalSeconds;
                _lobbyPollInFlight = true;
                try
                {
                    _onlineLobby = await LobbyService.Instance.GetLobbyAsync(_onlineLobby.Id);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[OnlineMatch] Lobby poll failed: {ex.Message}");
                }
                finally
                {
                    _lobbyPollInFlight = false;
                }
            }
        }

        private static bool IsFreshQuickMatchLobby(Lobby lobby)
        {
            if (lobby == null)
            {
                return false;
            }

            DateTime lastUpdated = lobby.LastUpdated == default ? lobby.Created : lobby.LastUpdated;
            if (lastUpdated == default)
            {
                return true;
            }

            DateTime utcLastUpdated = lastUpdated.Kind == DateTimeKind.Utc ? lastUpdated : lastUpdated.ToUniversalTime();
            return (DateTime.UtcNow - utcLastUpdated).TotalSeconds <= QuickMatchFreshLobbySeconds;
        }

        private static bool ShouldPreferHostingOnlineMatch(MatchSeat selectedSeat)
        {
            return selectedSeat == MatchSeat.SeatOne;
        }

        private static string GetUnityServicesProfile()
        {
            string explicitProfile = GetCommandLineValue(OnlineProfileArg);
            if (!string.IsNullOrWhiteSpace(explicitProfile))
            {
                return SanitizeUnityServicesProfile(explicitProfile);
            }

#if UNITY_EDITOR
            return "cardz_editor";
#else
            return "cardz_player";
#endif
        }

        private static string SanitizeUnityServicesProfile(string profile)
        {
            string sanitized = string.Empty;
            foreach (char c in profile)
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                {
                    sanitized += c;
                }
            }

            if (string.IsNullOrEmpty(sanitized))
            {
                sanitized = "cardz_player";
            }

            return sanitized.Length > 30 ? sanitized.Substring(0, 30) : sanitized;
        }

        private async Task MarkOnlineLobbyMatchedAsync()
        {
            if (_onlineLobby == null || !_onlineIsLobbyHost)
            {
                return;
            }

            try
            {
                _onlineLobby = await LobbyService.Instance.UpdateLobbyAsync(_onlineLobby.Id, new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        [LobbyDataStatus] = PublicLobbyData(LobbyStatusMatched)
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[OnlineMatch] Failed to mark lobby matched: {ex.Message}");
            }
        }

        private async Task CleanupOnlineLobbyAsync()
        {
            if (_onlineLobby == null)
            {
                return;
            }

            try
            {
                if (_onlineIsLobbyHost)
                {
                    await LobbyService.Instance.DeleteLobbyAsync(_onlineLobby.Id);
                }
                else if (AuthenticationService.Instance.IsSignedIn)
                {
                    await LobbyService.Instance.RemovePlayerAsync(_onlineLobby.Id, AuthenticationService.Instance.PlayerId);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[OnlineMatch] Lobby cleanup failed: {ex.Message}");
            }
            finally
            {
                _onlineLobby = null;
                _onlineQuickMatchActive = false;
                _onlineIsLobbyHost = false;
                _onlineConnectionState = OnlineConnectionState.Offline;
            }
        }

        private static DataObject PublicLobbyData(string value)
        {
            return new DataObject(DataObject.VisibilityOptions.Public, value);
        }

        private static bool TryReadLobbyString(Lobby lobby, string key, out string value)
        {
            value = string.Empty;
            if (lobby == null || lobby.Data == null || !lobby.Data.TryGetValue(key, out DataObject data) || data == null)
            {
                return false;
            }

            value = data.Value;
            return true;
        }

        private static bool TryReadLobbySeat(Lobby lobby, string key, out MatchSeat seat)
        {
            seat = MatchSeat.SeatOne;
            return TryReadLobbyString(lobby, key, out string value)
                && Enum.TryParse(value, out seat);
        }

        private static bool IsRelayUsingWebSockets()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

        private static string GetRelayConnectionType()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return "wss";
#else
            return "dtls";
#endif
        }

        private static string GetSeatDisplayNameForOnline(MatchSeat seat)
        {
            return seat == MatchSeat.SeatOne ? "Free Haven" : "Iron Citadel";
        }

        private bool IsClientOnlyLaunchMode()
        {
            return _launchMode == MatchLaunchMode.MultiplayerClient
                || (_launchMode == MatchLaunchMode.OnlineQuickMatch && (_networkManager == null || !_networkManager.IsServer));
        }

        private static bool IsLocalLoopbackAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            return string.Equals(address, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsLoopbackHostPortAvailable()
        {
            try
            {
                IPEndPoint[] listeners = useWebSockets
                    ? IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()
                    : IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners();
                for (int i = 0; i < listeners.Length; i++)
                {
                    if (listeners[i].Port != port)
                    {
                        continue;
                    }

                    string address = listeners[i].Address.ToString();
                    if (address == "0.0.0.0" || address == "127.0.0.1" || address == "::" || address == "::1")
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalMultiplayer] Failed to inspect local {(useWebSockets ? "TCP" : "UDP")} listeners: {ex.Message}");
            }

            return false;
        }
    }

    // Kept for scene compatibility while the first multiplayer test pass moves to direct NGO messaging.
    public class LocalMultiplayerTestSession : MonoBehaviour
    {
    }
}
