using Assets.HeroEditor4D.Common.Scripts.Enums;
using UnityEngine;
using Fusion;
using Fusion.Addons.Physics;
using Fusion.Sockets;
using System.Collections;
using System.Collections.Generic;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using System.Linq;
using DuloGames.UI;
using Server;

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static NetworkManager Instance;
    private const string GAME_VERSION = "1.0";

    [Header("Environment Configuration")]
    [SerializeField] private ServerEnvironmentConfig currentEnvironmentConfig;
    [Tooltip("DEPRECATED: Kullanmayın, sadece fallback için. Config kullanın!")]
    private string DEDICATED_ROOM_NAME = "MainGameRoom";

    public string playerNickname = "Player";
    private SimpleLoginManager loginManager;
    [Header("Connection Settings")]
    [SerializeField] private int maxConnectionAttempts = 5;
    [SerializeField] private float retryDelay = 2f;
    private int currentConnectionAttempt = 0;
    private bool isConnecting = false;
    private Coroutine connectionTimeoutCoroutine;
    private bool isQuitting = false;
    private const int MAX_RECONNECT_ATTEMPTS = 3;
    public event System.Action OnNetworkReady;
    private NetworkRunner _networkRunner;
    public NetworkRunner Runner => _networkRunner;
    [Header("Spawn Settings")]
    [SerializeField] private Vector2[] spawnPoints = new Vector2[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(-10, 0), new Vector2(0, 10), new Vector2(0, -10) };
    [SerializeField] private bool useRandomSpawn = true;
    [SerializeField] private float spawnRadius = 5f;
    [SerializeField] private Vector2 fixedSpawnPoint = Vector2.zero;

    [Header("Race-Based Spawn Settings")]
    [SerializeField] private Vector2[] humanSpawnPoints = new Vector2[] { new Vector2(-50, 0), new Vector2(-45, 5), new Vector2(-55, -5), new Vector2(-50, 10), new Vector2(-50, -10) };
    [SerializeField] private Vector2[] goblinSpawnPoints = new Vector2[] { new Vector2(50, 0), new Vector2(45, 5), new Vector2(55, -5), new Vector2(50, 10), new Vector2(50, -10) };
    [SerializeField] private bool useRaceBasedSpawns = true;
    [Header("Player Prefabs")]
    [SerializeField] private NetworkObject humanPlayerPrefab;
    [SerializeField] private NetworkObject goblinPlayerPrefab;
    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();

    private bool IsServerMode()
    {
        if (Application.isEditor) return false;
        string[] args = System.Environment.GetCommandLineArgs();
        bool hasServerArg = System.Array.Exists(args, arg => arg == "-server" || arg == "-batchmode" || arg == "-nographics" || arg.StartsWith("-room"));
        if (hasServerArg) return true;
#if UNITY_SERVER || DEDICATED_SERVER || SERVER_BUILD
        return true;
#else
        return false;
#endif
    }

    /// <summary>
    /// Session/Room adını döndürür. Öncelik sırası:
    /// 1. Command-line parametresi (-room)
    /// 2. ServerEnvironmentConfig (ScriptableObject)
    /// 3. DEDICATED_ROOM_NAME (fallback)
    /// </summary>
    private string GetSessionName()
    {
        // 1. Command-line parametresini kontrol et (en yüksek öncelik)
        string commandLineRoom = GetCommandLineRoom();
        if (!string.IsNullOrEmpty(commandLineRoom))
        {
            Debug.Log($"[NetworkManager] Using session name from command-line: {commandLineRoom}");
            return commandLineRoom;
        }

        // 2. Config'i kontrol et
        if (currentEnvironmentConfig != null && currentEnvironmentConfig.IsValid())
        {
            string sessionName = currentEnvironmentConfig.GetFullSessionName();
            Debug.Log($"[NetworkManager] Using session name from config: {sessionName} (Environment: {currentEnvironmentConfig.environmentName})");
            return sessionName;
        }

        // 3. Fallback
        Debug.LogWarning($"[NetworkManager] No config or command-line parameter! Using fallback: {DEDICATED_ROOM_NAME}");
        return DEDICATED_ROOM_NAME;
    }

    /// <summary>
    /// Command-line'dan -room parametresini okur
    /// </summary>
    private string GetCommandLineRoom()
    {
        try
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-room" && i + 1 < args.Length)
                {
                    return args[i + 1];
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[NetworkManager] GetCommandLineRoom error: {e.Message}");
        }
        return null;
    }

    /// <summary>
    /// Config'den region kodunu döndürür
    /// </summary>
    private string GetPhotonRegion()
    {
        if (currentEnvironmentConfig != null && currentEnvironmentConfig.IsValid())
        {
            return currentEnvironmentConfig.photonRegion;
        }
        return "tr"; // Fallback
    }

    /// <summary>
    /// Config'den max player sayısını döndürür
    /// </summary>
    private int GetMaxPlayers()
    {
        if (currentEnvironmentConfig != null && currentEnvironmentConfig.IsValid())
        {
            return currentEnvironmentConfig.maxPlayers;
        }
        return 100; // Fallback
    }

    /// <summary>
    /// Config validation - Awake'de çağrılır
    /// </summary>
    private void ValidateConfiguration()
    {
        if (currentEnvironmentConfig == null)
        {
            Debug.LogWarning($"[NetworkManager] ServerEnvironmentConfig atanmamış! Lütfen Inspector'dan bir config atayın. Fallback değerler kullanılacak.");
            return;
        }

        if (!currentEnvironmentConfig.IsValid())
        {
            Debug.LogError($"[NetworkManager] ServerEnvironmentConfig geçersiz! Fallback değerler kullanılacak.");
            return;
        }

        Debug.Log($"[NetworkManager] Environment Configuration Loaded:\n" +
                  $"  - Environment: {currentEnvironmentConfig.environmentName}\n" +
                  $"  - Session Name: {currentEnvironmentConfig.GetFullSessionName()}\n" +
                  $"  - Region: {currentEnvironmentConfig.photonRegion}\n" +
                  $"  - Max Players: {currentEnvironmentConfig.maxPlayers}\n" +
                  $"  - Is Visible: {currentEnvironmentConfig.isSessionVisible}\n" +
                  $"  - Is Open: {currentEnvironmentConfig.isSessionOpen}");
    }

    private void Awake()
    {
        try
        {
            // Config'i validate et
            ValidateConfiguration();

            bool serverMode = IsServerMode();
            if (serverMode)
            {
                ForceHeadlessMode();
                OptimizeServerPhysics();
            }
            else
            {
                OptimizeClientPhysics();
            }
            NetworkManager[] managers = FindObjectsByType<NetworkManager>(FindObjectsSortMode.None);
            if (managers.Length > 1)
            {
                if (managers[0] != this)
                {
                    Destroy(gameObject);
                    return;
                }
            }
            SetupPhysicsLayers();
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                StopAnyActiveLoading();
                if (!serverMode && LocalPlayerManager.Instance == null)
                {
                    GameObject localPlayerManagerObj = new GameObject("LocalPlayerManager");
                    localPlayerManagerObj.AddComponent<LocalPlayerManager>();
                    DontDestroyOnLoad(localPlayerManagerObj);
                }
                if (serverMode) StartCoroutine(AutoStartServer());
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            else
            {
                if (serverMode) StartCoroutine(AutoStartServer());
            }
            InitializeNetwork();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[NetworkManager] Awake error: {e.Message}\n{e.StackTrace}");
        }
    }

    private void SetupPhysicsLayers()
    {
        Physics2D.IgnoreLayerCollision(8, 9, true);
    }

    private void OptimizeServerPhysics()
    {
        Physics2D.simulationMode = SimulationMode2D.Script;
        Physics2D.velocityIterations = 3;
        Physics2D.positionIterations = 6;
        Physics2D.bounceThreshold = 1f;
    }

    private void OptimizeClientPhysics()
    {
        Physics2D.simulationMode = SimulationMode2D.Script;
        Physics2D.velocityIterations = 1;
        Physics2D.positionIterations = 3;
        Physics2D.bounceThreshold = 2f;
    }

    private void ForceHeadlessMode()
    {
        try
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            QualitySettings.SetQualityLevel(0, false);
            AudioListener.volume = 0f;
            AudioListener.pause = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[NetworkManager] ForceHeadlessMode error: {e.Message}");
        }
    }

    private IEnumerator AutoStartServer()
    {
        yield return new WaitForSeconds(0.2f);
        if (!TryServerSetup())
        {
            Debug.LogError("[SERVER] Server setup failed, shutting down");
            Application.Quit();
            yield break;
        }
        yield return new WaitForSeconds(0.5f);
        StartCoroutine(ConnectToServerCoroutine());
        yield return new WaitForSeconds(1f);
        StartCoroutine(SpawnServerManagersOnStartup());
    }

    private IEnumerator ConnectToServerCoroutine()
    {
        yield return new WaitForEndOfFrame();
        ConnectToServer();
    }

    private bool TryServerSetup()
    {
        try
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            QualitySettings.SetQualityLevel(0, false);
            if (IsServerMode())
            {
                ParseCommandLineArgs();
                DisableRenderingComponents();
            }
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SERVER] TryServerSetup error: {e.Message}");
            return false;
        }
    }

    private void DisableRenderingComponents()
    {
        try
        {
            if (IsServerMode())
            {
                Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                foreach (Camera cam in cameras)
                {
                    if (cam != null) cam.enabled = false;
                }
                Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                foreach (Canvas canvas in canvases)
                {
                    if (canvas != null) canvas.enabled = false;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SERVER] DisableRenderingComponents error: {e.Message}");
        }
    }

    private void OnEnable()
    {
        if (_networkRunner == null) InitializeNetwork();
        if (_networkRunner != null)
        {
            _networkRunner.RemoveCallbacks(this);
            _networkRunner.AddCallbacks(this);
        }
    }

    private void OnDisable()
    {
        if (_networkRunner != null && _networkRunner.IsRunning) _networkRunner.RemoveCallbacks(this);
    }

    public string GetLocalPlayerNickname()
    {
        string nickname = PlayerPrefs.GetString("Nickname", "");
        if (!string.IsNullOrEmpty(nickname))
        {
            playerNickname = nickname;
            return nickname;
        }
        return "Player";
    }

    private void InitializeNetwork()
    {
        if (_networkRunner == null)
        {
            _networkRunner = gameObject.GetComponent<NetworkRunner>();
            if (_networkRunner == null) _networkRunner = gameObject.AddComponent<NetworkRunner>();
            if (_networkRunner.GetComponent<RunnerSimulatePhysics2D>() == null)
            {
                var physicsSimulator = _networkRunner.gameObject.AddComponent<RunnerSimulatePhysics2D>();
                physicsSimulator.ClientPhysicsSimulation = Fusion.Addons.Physics.ClientPhysicsSimulation.SimulateForward;
            }
            _networkRunner.AddCallbacks(this);
        }
        else
        {
            _networkRunner.AddCallbacks(this);
        }
        if (Application.isMobilePlatform)
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
        }
    }

    public async void ConnectToServer()
    {
        if (_networkRunner != null && _networkRunner.IsRunning) return;
        if (isConnecting) return;
        isConnecting = true;
        currentConnectionAttempt++;
        try
        {
            StartGameArgs args;
            if (IsServerMode())
            {
                string sessionName = GetSessionName();
                string environmentName = currentEnvironmentConfig != null ? currentEnvironmentConfig.environmentName : "Unknown";

                args = new StartGameArgs()
                {
                    GameMode = GameMode.Server,
                    SessionName = sessionName,
                    Scene = SceneRef.FromIndex(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex),
                    PlayerCount = GetMaxPlayers(),
                    IsOpen = currentEnvironmentConfig != null ? currentEnvironmentConfig.isSessionOpen : true,
                    IsVisible = currentEnvironmentConfig != null ? currentEnvironmentConfig.isSessionVisible : true,
                    SessionProperties = new System.Collections.Generic.Dictionary<string, Fusion.SessionProperty>
                    {
                        { "ServerType", "Dedicated" },
                        { "Region", GetPhotonRegion() },
                        { "Version", GAME_VERSION },
                        { "Environment", environmentName }
                    }
                };

                Debug.Log($"[NetworkManager] Starting SERVER with session: {sessionName} | Environment: {environmentName} | Region: {GetPhotonRegion()}");
            }
            else
            {
                string sessionName = GetSessionName();

                args = new StartGameArgs()
                {
                    GameMode = GameMode.Client,
                    SessionName = sessionName,
                    Scene = SceneRef.FromIndex(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex),
                    PlayerCount = GetMaxPlayers()
                };

                Debug.Log($"[NetworkManager] Starting CLIENT, joining session: {sessionName}");
            }
            var result = await _networkRunner.StartGame(args);
            isConnecting = false;
            if (result.Ok)
            {
                currentConnectionAttempt = 0;
                if (IsServerMode()) OptimizeForServer();
                if (LoadingManager.Instance != null) LoadingManager.Instance.CompleteStep("NetworkConnection");
            }
            else
            {
                Debug.LogError($"[NetworkManager] Connection failed: {result.ShutdownReason}");
                HandleConnectionFailure(result);
            }
        }
        catch (System.Exception e)
        {
            isConnecting = false;
            Debug.LogError($"[NetworkManager] ConnectToServer exception: {e.Message}\n{e.StackTrace}");
            if (IsServerMode())
            {
                Debug.LogError("[SERVER] Critical server startup failure");
                Application.Quit();
            }
            else
            {
                if (LoadingManager.Instance != null) LoadingManager.Instance.CancelLoading();
                if (currentConnectionAttempt < maxConnectionAttempts) StartCoroutine(DelayedRetryConnection());
            }
        }
    }

    private void HandleConnectionFailure(StartGameResult result)
    {
        if (IsServerMode())
        {
            Debug.LogError($"[NetworkManager] SERVER failed to start: {result.ShutdownReason}");
            if (currentConnectionAttempt < maxConnectionAttempts)
            {
                StartCoroutine(DelayedRetryConnection());
            }
            else
            {
                Debug.LogError("[NetworkManager] SERVER failed to start after max attempts. Shutting down...");
                Application.Quit();
            }
        }
        else
        {
            Debug.LogError($"[NetworkManager] CLIENT connection failed: {result.ShutdownReason}");
            if (LoadingManager.Instance != null) LoadingManager.Instance.CancelLoading();
            if (currentConnectionAttempt >= maxConnectionAttempts)
            {
                currentConnectionAttempt = 0;
                return;
            }
            StartCoroutine(DelayedRetryConnection());
        }
    }

    private IEnumerator DelayedRetryConnection()
    {
        yield return new WaitForSeconds(retryDelay);
        if (!isQuitting && currentConnectionAttempt < maxConnectionAttempts)
        {
            try { ConnectToServer(); }
            catch (System.Exception e) { Debug.LogError($"[NetworkManager] DelayedRetryConnection error: {e.Message}"); }
        }
    }

    private void OptimizeForServer()
    {
#if SERVER_BUILD
        if (_networkRunner.IsServer)
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            QualitySettings.SetQualityLevel(0, false);
            Physics2D.simulationMode = SimulationMode2D.Script;
            System.GC.Collect();
        }
#endif
    }

    private void ParseCommandLineArgs()
    {
        try
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-room":
                        if (i + 1 < args.Length) DEDICATED_ROOM_NAME = args[i + 1];
                        break;
                    case "-players":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int playerCount)) { }
                        break;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SERVER] ParseCommandLineArgs error: {e.Message}");
        }
    }

    private void Update()
    {
        if (_networkRunner == null) return;
    }

    private bool IsNetworkObjectValid(NetworkObject netObj)
    {
        try
        {
            if (netObj == null) return false;
            if (!netObj) return false;
            if (!netObj.IsValid) return false;
            if (netObj.gameObject == null) return false;
            return true;
        }
        catch { return false; }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        bool shouldSpawn = false;
        if (runner.GameMode == GameMode.Host) shouldSpawn = runner.IsServer;
        else if (runner.GameMode == GameMode.Server) shouldSpawn = runner.IsServer;
        else if (runner.GameMode == GameMode.Client) shouldSpawn = false;
        if (player == runner.LocalPlayer)
        {
            if (LocalPlayerManager.Instance == null)
            {
                GameObject localPlayerManagerObj = new GameObject("LocalPlayerManager");
                localPlayerManagerObj.AddComponent<LocalPlayerManager>();
                DontDestroyOnLoad(localPlayerManagerObj);
            }
            if (LoadingManager.Instance != null) LoadingManager.Instance.CompleteStep("PlayerSpawn");
            StartCoroutine(DelayedPlayerInitialization(0f));
        }
        if (player == runner.LocalPlayer)
        {
            string nickname = GetLocalPlayerNickname();
            PlayerRace localRace;
            try { localRace = GetPlayerRaceFromPrefs(nickname); }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkManager] Race validation failed: {e.Message}");
                runner.Shutdown();
                return;
            }
            StartCoroutine(RegisterRaceWithRaceManager(player, localRace));
        }
        if (shouldSpawn)
        {
            if (_spawnedCharacters.ContainsKey(player)) return;
            if (player == runner.LocalPlayer) SpawnPlayerWithRace(runner, player);
            else StartCoroutine(DelayedSpawnForClient(runner, player));
        }
        if (player != runner.LocalPlayer && runner.IsServer)
        {
            StartCoroutine(SyncExistingCharactersToNewPlayer(player));
            StartCoroutine(SyncExistingMonstersToNewPlayer(player));
            StartCoroutine(SyncExistingNicknamesToNewPlayer(player));
            StartCoroutine(ImmediateCharacterSyncForNewPlayer(player));
        }
    }

    private IEnumerator ImmediateCharacterSyncForNewPlayer(PlayerRef newPlayer)
    {
        yield return new WaitForSeconds(0.5f);
        foreach (var kvp in _spawnedCharacters)
        {
            if (kvp.Value != null && kvp.Value.IsValid)
            {
                Character4D character4D = kvp.Value.GetComponent<Character4D>();
                if (character4D != null) character4D.RequestCharacterSyncRPC(newPlayer);
                yield return new WaitForSeconds(0.1f);
            }
        }
    }

    private IEnumerator RegisterRaceWithRaceManager(PlayerRef player, PlayerRace race)
    {
        float waitTime = 0f;
        while (RaceManager.Instance == null && waitTime < 1f)
        {
            yield return new WaitForSeconds(0.05f);
            waitTime += 0.05f;
        }
        if (RaceManager.Instance != null) RaceManager.Instance.RegisterPlayerRaceWithRetry(player, race);
        else Debug.LogError($"[NETWORK] RaceManager timeout after {waitTime:F1}s!");
    }

    private IEnumerator DelayedSpawnForClient(NetworkRunner runner, PlayerRef player)
    {
        PlayerRace? foundRace = null;
        float waitTime = 0f;
        while (foundRace == null && waitTime < 10f)
        {
            if (RaceManager.Instance != null && RaceManager.Instance.TryGetPlayerRace(player, out PlayerRace race))
            {
                foundRace = race;
                break;
            }
            yield return new WaitForSeconds(0.1f);
            waitTime += 0.1f;
        }
        if (foundRace.HasValue)
        {
            if (RaceManager.Instance != null) RaceManager.Instance.RegisterPlayerRaceWithRetry(player, foundRace.Value);
        }
        else
        {
            Debug.LogError($"[SPAWN] TIMEOUT: No race data for {player} after {waitTime:F1}s. Will NOT spawn.");
            yield break;
        }
        SpawnPlayerWithRace(runner, player);
    }

    private IEnumerator LoadRaceFromFirebaseForPlayer(PlayerRef player, System.Action<PlayerRace?> onComplete)
    {
        float waitTime = 0f;
        while (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsReady)
        {
            if (waitTime > 5f)
            {
                Debug.LogError($"[SPAWN] Firebase timeout!");
                onComplete?.Invoke(null);
                yield break;
            }
            yield return new WaitForSeconds(0.1f);
            waitTime += 0.1f;
        }
        onComplete?.Invoke(null);
    }

    private void SpawnPlayerWithRace(NetworkRunner runner, PlayerRef player)
    {
        // PROFESSIONAL MMORPG PATTERN: Validate session data before spawn
        if (player == runner.LocalPlayer)
        {
            if (PlayerDataSession.Instance == null || !PlayerDataSession.Instance.ValidateSessionForSpawn())
            {
                Debug.LogError("========================================");
                Debug.LogError("[NetworkManager] CRITICAL: PlayerDataSession validation failed!");
                Debug.LogError("[NetworkManager] Player cannot spawn without loaded Firebase data.");
                Debug.LogError("[NetworkManager] This indicates a bug in pre-load flow.");
                Debug.LogError($"[NetworkManager] Session Info: {(PlayerDataSession.Instance != null ? PlayerDataSession.Instance.GetSessionInfo() : "Instance is null")}");
                Debug.LogError("========================================");

                // Kick player - cannot spawn without session data
                Debug.Log("[NetworkManager] Shutting down and returning to login...");
                runner.Shutdown();

                // Clear session and return to login
                if (PlayerDataSession.Instance != null)
                {
                    PlayerDataSession.Instance.ClearSession();
                }

                UnityEngine.SceneManagement.SceneManager.LoadScene("Login");
                return;
            }

            Debug.Log($"[NetworkManager] Session validation passed for local player. Session: {PlayerDataSession.Instance.GetSessionInfo()}");
        }

        PlayerRace playerRace;
        if (RaceManager.Instance != null && RaceManager.Instance.TryGetPlayerRace(player, out PlayerRace race))
        {
            playerRace = race;
        }
        else if (player == runner.LocalPlayer)
        {
            string nickname = GetLocalPlayerNickname();
            try { playerRace = GetPlayerRaceFromPrefs(nickname); }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkManager] CRITICAL: Race validation failed for {nickname}: {e.Message}");
                runner.Shutdown();
                return;
            }
            if (RaceManager.Instance != null) RaceManager.Instance.RegisterPlayerRaceWithRetry(player, playerRace);
        }
        else
        {
            Debug.LogError($"[NetworkManager] No race data for remote player {player}. Starting aggressive retry...");
            StartCoroutine(AggressiveRaceRetryForPlayer(runner, player));
            return;
        }
        Vector2 spawnPosition = CalculateSpawnPosition(playerRace);
        NetworkObject prefabToSpawn = GetPlayerPrefab(playerRace);
        if (prefabToSpawn != null)
        {
            var networkPlayerObject = runner.Spawn(prefabToSpawn, new Vector3(spawnPosition.x, spawnPosition.y, 0), Quaternion.identity, player);
            if (networkPlayerObject != null)
            {
                _spawnedCharacters[player] = networkPlayerObject;
                StartCoroutine(InitializePlayerCharacterDelayed(networkPlayerObject, player, playerRace));
                StartCoroutine(SyncPlayerNicknameDelayed(networkPlayerObject, player));
            }
        }
        else
        {
            Debug.LogError($"[SPAWN] Prefab bulunamadı! Race: {playerRace}");
        }
    }

    private IEnumerator AggressiveRaceRetryForPlayer(NetworkRunner runner, PlayerRef player)
    {
        float totalWaitTime = 0f;
        int retryCount = 0;
        while (totalWaitTime < 30f && retryCount < 60)
        {
            if (RaceManager.Instance != null && RaceManager.Instance.TryGetPlayerRace(player, out PlayerRace race))
            {
                SpawnPlayerWithRace(runner, player);
                yield break;
            }
            retryCount++;
            yield return new WaitForSeconds(0.5f);
            totalWaitTime += 0.5f;
        }
        Debug.LogError($"[SPAWN] AGGRESSIVE RETRY FAILED after {totalWaitTime:F1}s");
        if (RaceManager.Instance != null) RaceManager.Instance.RegisterPlayerRaceWithRetry(player, PlayerRace.Human);
        SpawnPlayerWithRace(runner, player);
    }

    private IEnumerator SyncExistingMonstersToNewPlayer(PlayerRef newPlayer)
    {
        yield return new WaitForSeconds(3f);
        MonsterBehaviour[] allMonsters = FindObjectsByType<MonsterBehaviour>(FindObjectsSortMode.None);
        foreach (MonsterBehaviour monster in allMonsters)
        {
            if (monster != null && monster.Object != null && monster.Object.IsValid && !monster.IsDead)
            {
                monster.SyncStateToNewClient(newPlayer);
                yield return new WaitForSeconds(0.2f);
            }
        }
    }

    private IEnumerator SyncPlayerNicknameDelayed(NetworkObject playerObject, PlayerRef player)
    {
        yield return new WaitForSeconds(1f);
        PlayerStats playerStats = playerObject.GetComponent<PlayerStats>();
        if (playerStats != null && player == Runner.LocalPlayer)
        {
            string nickname = GetLocalPlayerNickname();
            if (!string.IsNullOrEmpty(nickname)) playerStats.SyncPlayerNicknameRPC(nickname);
        }
    }

    private IEnumerator SyncExistingCharactersToNewPlayer(PlayerRef newPlayer)
    {
        yield return new WaitForSeconds(1f);
        foreach (var kvp in _spawnedCharacters)
        {
            if (kvp.Value != null && kvp.Value.IsValid)
            {
                EquipmentSystem equipmentSystem = kvp.Value.GetComponent<EquipmentSystem>();
                if (equipmentSystem != null) equipmentSystem.RequestEquipmentSyncRPC();
                yield return new WaitForSeconds(0.2f);
            }
        }
    }

    private IEnumerator InitializePlayerCharacterDelayed(NetworkObject playerObject, PlayerRef player, PlayerRace playerRace)
    {
        yield return new WaitForEndOfFrame();
        Character4D character4D = playerObject.GetComponent<Character4D>();
        if (character4D != null)
        {
            character4D.SetDirection(Vector2.down);
            if (character4D.AnimationManager != null) character4D.AnimationManager.SetState(CharacterState.Idle);
        }
        NetworkTransform netTransform = playerObject.GetComponent<NetworkTransform>();
        if (netTransform != null && player == _networkRunner.LocalPlayer)
        {
            Vector2 spawnPos = CalculateSpawnPosition(playerRace);
            playerObject.transform.position = new Vector3(spawnPos.x, spawnPos.y, 0);
        }
        if (player == _networkRunner.LocalPlayer)
        {
            EquipmentSystem equipmentSystem = playerObject.GetComponent<EquipmentSystem>();
            if (equipmentSystem != null) equipmentSystem.BroadcastCurrentEquipment();
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new PlayerNetworkInput();
        GameObject localPlayer = FindLocalPlayerObject();
        if (localPlayer != null)
        {
            DeathSystem deathSystem = localPlayer.GetComponent<DeathSystem>();
            if (deathSystem != null && deathSystem.IsDead)
            {
                data.MovementInput = Vector2.zero;
                input.Set(data);
                return;
            }
            NetworkCharacterController netController = localPlayer.GetComponent<NetworkCharacterController>();
            if (netController != null)
            {
                PlayerController playerController = localPlayer.GetComponent<PlayerController>();
                if (playerController != null && playerController.IsTeleportCooldown())
                {
                    data.MovementInput = Vector2.zero;
                    input.Set(data);
                    return;
                }
            }
        }
        UIJoystick joystick = FindFirstObjectByType<UIJoystick>();
        if (joystick != null) data.MovementInput = joystick.JoystickAxis;
        input.Set(data);
    }

    private IEnumerator DelayedPlayerInitialization(float delay)
    {
        yield return new WaitForSeconds(0.1f);
        if (_networkRunner != null && _networkRunner.IsRunning)
        {
            if (LoadingManager.Instance != null)
            {
                LoadingManager.Instance.CompleteStep("NetworkConnection");
                LoadingManager.Instance.CompleteStep("NetworkInitialization");
            }
            if (IsServerMode())
            {
                if (LoadingManager.Instance != null)
                {
                    LoadingManager.Instance.CompleteStep("UIInitialization");
                    LoadingManager.Instance.CompleteStep("SystemsReady");
                }
            }
            else
            {
                StartCoroutine(FastClientInitialization());
            }
            OnNetworkReady?.Invoke();
        }
    }

    private IEnumerator FastClientInitialization()
    {
        ActivateGameUI();
        GameObject playerObj = FindLocalPlayerObjectOptimized();
        if (playerObj != null)
        {
            if (LocalPlayerManager.Instance == null)
            {
                GameObject localPlayerManagerObj = new GameObject("LocalPlayerManager");
                localPlayerManagerObj.AddComponent<LocalPlayerManager>();
                DontDestroyOnLoad(localPlayerManagerObj);
            }
            InitializePlayerSystemsInstant(playerObj);
            InitializeUiSystemsInstant();
        }
        yield return new WaitForEndOfFrame();
        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.CompleteStep("PlayerInitialization");
            LoadingManager.Instance.CompleteStep("UIInitialization");
            LoadingManager.Instance.CompleteStep("SystemsReady");
        }
    }

    private void InitializePlayerSystemsInstant(GameObject playerObj)
    {
        if (LocalPlayerManager.Instance == null)
        {
            Debug.LogError($"[NetworkManager.InitializePlayerSystemsInstant] LocalPlayerManager NULL! This should not happen!");
            return;
        }
        PlayerStats playerStats = playerObj.GetComponent<PlayerStats>();
        if (playerStats != null)
        {
            LocalPlayerManager.Instance.SetLocalPlayer(playerStats);
            string realNickname = GetLocalPlayerNickname();
            playerStats.SetPlayerDisplayName(realNickname);
        }
    }

    private void InitializeUiSystemsInstant()
    {
        if (PartyAvatarRenderer.Instance == null)
        {
            var avatarObj = new GameObject("PartyAvatarRenderer");
            avatarObj.AddComponent<PartyAvatarRenderer>();
            DontDestroyOnLoad(avatarObj);
        }
        if (SkillPreviewManager.Instance == null)
        {
            var previewObj = new GameObject("SkillPreviewManager");
            previewObj.AddComponent<SkillPreviewManager>();
            DontDestroyOnLoad(previewObj);
        }
        CombatInitializer combatInit = FindFirstObjectByType<CombatInitializer>();
    }

    private NetworkObject GetPlayerPrefab(PlayerRace race)
    {
        switch (race)
        {
            case PlayerRace.Human: return humanPlayerPrefab;
            case PlayerRace.Goblin: return goblinPlayerPrefab;
            default: return humanPlayerPrefab;
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (player == runner.LocalPlayer && LocalPlayerManager.Instance != null) LocalPlayerManager.Instance.ClearLocalPlayer();
        if (RaceManager.Instance != null) RaceManager.Instance.CleanupPlayerRace(player);
        if (runner.IsServer)
        {
            if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
            {
                if (IsNetworkObjectValid(networkObject)) runner.Despawn(networkObject);
            }
        }
        _spawnedCharacters.Remove(player);
    }

    private PlayerRace GetPlayerRaceFromPrefs(string nickname)
    {
        string raceKey = "PlayerRace_" + nickname;
        if (PlayerPrefs.HasKey(raceKey))
        {
            string raceString = PlayerPrefs.GetString(raceKey);
            if (System.Enum.TryParse<PlayerRace>(raceString, out PlayerRace race)) return race;
        }
        Debug.LogError($"[NetworkManager] CRITICAL: No race found for {nickname}! Login should have prevented this.");
        throw new System.Exception($"No race data for player {nickname}");
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        request.Accept();
        if (LoadingManager.Instance != null) LoadingManager.Instance.CompleteStep("NetworkConnection");
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"[NetworkManager] Connection failed: {reason}");
        isConnecting = false;
        if (connectionTimeoutCoroutine != null)
        {
            StopCoroutine(connectionTimeoutCoroutine);
            connectionTimeoutCoroutine = null;
        }
        if (loginManager == null) loginManager = FindAnyObjectByType<SimpleLoginManager>();
        if (loginManager != null) loginManager.OnConnectionFailed();
        if (!isQuitting && currentConnectionAttempt < maxConnectionAttempts) StartCoroutine(DelayedRetryConnection());
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"[NetworkManager] OnShutdown called. Reason: {shutdownReason}");
        _spawnedCharacters.Clear();

        // Server-specific shutdown handling
        if (IsServerMode())
        {
            Debug.LogWarning($"[SERVER] NetworkRunner shutdown! Reason: {shutdownReason}");

            // Kritik shutdown nedenleri için uygulama restart
            switch (shutdownReason)
            {
                case ShutdownReason.DisconnectedByPluginLogic:
                case ShutdownReason.PhotonCloudTimeout:
                case ShutdownReason.ServerInRoom:
                case ShutdownReason.GameNotFound:
                case ShutdownReason.GameClosed:
                case ShutdownReason.ConnectionTimeout:
                    Debug.LogError($"[SERVER] Critical shutdown reason: {shutdownReason}. Triggering recovery...");
                    if (ServerManager.Instance != null)
                    {
                        ServerManager.Instance.OnServerDisconnected(shutdownReason.ToString());
                    }
                    else
                    {
                        // ServerManager yoksa direkt quit
                        Debug.LogError("[SERVER] ServerManager not found! Shutting down application...");
                        Application.Quit();
                    }
                    break;

                case ShutdownReason.Ok:
                    // Normal shutdown, recovery gerekmez
                    Debug.Log("[SERVER] Normal shutdown completed.");
                    break;

                default:
                    Debug.LogWarning($"[SERVER] Unexpected shutdown reason: {shutdownReason}. Monitoring...");
                    if (ServerManager.Instance != null)
                    {
                        ServerManager.Instance.OnServerDisconnected(shutdownReason.ToString());
                    }
                    break;
            }
        }
    }

    private GameObject FindLocalPlayerObjectOptimized()
    {
        foreach (var kvp in _spawnedCharacters)
        {
            if (IsNetworkObjectValid(kvp.Value) && kvp.Value.HasInputAuthority) return kvp.Value.gameObject;
        }
        for (int i = 0; i < 3; i++)
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            foreach (GameObject player in players)
            {
                NetworkObject netObj = player.GetComponent<NetworkObject>();
                if (IsNetworkObjectValid(netObj) && netObj.HasInputAuthority) return player;
            }
            if (i < 2) System.Threading.Thread.Sleep(100);
        }
        return null;
    }

    public void OnObjectSpawned(NetworkRunner runner, NetworkObject obj)
    {
        string objName = obj.gameObject.name;
        if (objName.Contains("RaceManager") || obj.GetComponent<RaceManager>() != null)
        {
            if (runner.IsServer) runner.SetIsSimulated(obj, true);
            else runner.SetIsSimulated(obj, false);
        }
        if (objName.Contains("Monster") || obj.CompareTag("Monster"))
        {
            if (runner.IsServer) runner.SetIsSimulated(obj, true);
            else runner.SetIsSimulated(obj, false);
        }
        if (objName.Contains("PlayerPrefab") || obj.CompareTag("Player"))
        {
            if (obj.InputAuthority != PlayerRef.None) _spawnedCharacters[obj.InputAuthority] = obj;
        }
        var components = obj.GetComponents<NetworkBehaviour>();
        string componentList = string.Join(", ", System.Array.ConvertAll(components, c => c.GetType().Name));
    }

    private IEnumerator CheckRaceManagerPeriodically()
    {
        if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsReady)
        {
            if (LoadingManager.Instance != null) LoadingManager.Instance.CompleteStep("FirebaseConnection");
        }
        yield return new WaitForSeconds(2f);
        for (int i = 0; i < 10; i++)
        {
            if (RaceManager.Instance != null) break;
            NetworkObject[] allNetObjects = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
            foreach (var netObj in allNetObjects)
            {
                var raceManager = netObj.GetComponent<RaceManager>();
                if (raceManager != null) break;
            }
            yield return new WaitForSeconds(1f);
        }
    }

    public void OnObjectDestroyed(NetworkRunner runner, NetworkObject obj)
    {
        try
        {
            string objName = obj != null && obj.gameObject != null ? obj.gameObject.name : "NULL_NAME";
            if (obj != null && obj.CompareTag("Player"))
            {
                var playerRefToRemove = _spawnedCharacters.FirstOrDefault(kvp => kvp.Value == obj).Key;
                if (playerRefToRemove != PlayerRef.None) _spawnedCharacters.Remove(playerRefToRemove);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DESTROY] ERROR accessing destroyed object: {e.Message}");
        }
    }

    private GameObject FindLocalPlayerObject()
    {
        if (_networkRunner == null) return null;
        foreach (var kvp in _spawnedCharacters)
        {
            if (IsNetworkObjectValid(kvp.Value))
            {
                bool hasInputAuth = kvp.Value.HasInputAuthority;
                string objName = kvp.Value.gameObject.name;
                if (hasInputAuth) return kvp.Value.gameObject;
            }
        }
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (IsNetworkObjectValid(netObj))
            {
                bool hasInputAuth = netObj.HasInputAuthority;
                if (hasInputAuth)
                {
                    if (netObj.InputAuthority != PlayerRef.None) _spawnedCharacters[netObj.InputAuthority] = netObj;
                    return player;
                }
            }
        }
        return null;
    }

    private void ActivateGameUI()
    {
        if (IsServerMode()) return;
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        GameObject gameUI = null;
        foreach (GameObject obj in allObjects)
        {
            if (obj.name == "GameUI" && obj.scene.isLoaded)
            {
                gameUI = obj;
                break;
            }
        }
        if (gameUI == null) gameUI = GameObject.Find("GameUI");
        if (gameUI == null) gameUI = GameObject.Find("GameUI(Clone)");
        if (gameUI == null)
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (Canvas canvas in canvases)
            {
                Transform gameUITransform = canvas.transform.Find("GameUI");
                if (gameUITransform != null)
                {
                    gameUI = gameUITransform.gameObject;
                    break;
                }
            }
        }
        if (gameUI != null)
        {
            gameUI.SetActive(true);
            UIManager uiManager = gameUI.GetComponent<UIManager>();
            if (uiManager == null) uiManager = gameUI.GetComponentInChildren<UIManager>();
        }
    }

    private Vector2 CalculateSpawnPosition(PlayerRace race = PlayerRace.Human)
    {
        // Irk bazli spawn sistemi aktifse, irka gore spawn noktalarini kullan
        if (useRaceBasedSpawns)
        {
            Vector2[] raceSpawnPoints = GetRaceSpawnPoints(race);

            if (raceSpawnPoints != null && raceSpawnPoints.Length > 0)
            {
                if (useRandomSpawn)
                {
                    int randomIndex = UnityEngine.Random.Range(0, raceSpawnPoints.Length);
                    return raceSpawnPoints[randomIndex];
                }
                else
                {
                    return raceSpawnPoints[0];
                }
            }
        }

        // Eski sistem (fallback)
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            if (useRandomSpawn)
            {
                int randomIndex = UnityEngine.Random.Range(0, spawnPoints.Length);
                return spawnPoints[randomIndex];
            }
            else
            {
                return spawnPoints[0];
            }
        }
        if (useRandomSpawn)
        {
            float randomAngle = UnityEngine.Random.Range(0f, 360f);
            Vector2 spawnPos = new Vector2(Mathf.Cos(randomAngle * Mathf.Deg2Rad) * spawnRadius, Mathf.Sin(randomAngle * Mathf.Deg2Rad) * spawnRadius);
            return spawnPos;
        }
        return fixedSpawnPoint;
    }

    private Vector2[] GetRaceSpawnPoints(PlayerRace race)
    {
        switch (race)
        {
            case PlayerRace.Human:
                return humanSpawnPoints;
            case PlayerRace.Goblin:
                return goblinSpawnPoints;
            default:
                Debug.LogWarning($"[NetworkManager] Unknown race {race}, using Human spawn points");
                return humanSpawnPoints;
        }
    }

    private IEnumerator CleanupOrphanedObjects()
    {
        yield return new WaitForSeconds(1f);
        if (_networkRunner == null || !_networkRunner.IsServer) yield break;
        List<NetworkObject> objectsToCleanup = new List<NetworkObject>();
        try
        {
            NetworkObject[] allNetworkObjects = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
            foreach (NetworkObject netObj in allNetworkObjects)
            {
                if (netObj != null && netObj.IsValid && netObj.gameObject != null)
                {
                    if (netObj.InputAuthority == PlayerRef.None && !netObj.HasStateAuthority) objectsToCleanup.Add(netObj);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[NetworkManager] CleanupOrphanedObjects hata: {e.Message}");
            yield break;
        }
        foreach (NetworkObject netObj in objectsToCleanup)
        {
            if (netObj != null && netObj.IsValid)
            {
                try { _networkRunner.Despawn(netObj); }
                catch (System.Exception e) { Debug.LogError($"[NetworkManager] Despawn hatası: {e.Message}"); }
                yield return new WaitForEndOfFrame();
            }
        }
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
        if (_networkRunner != null && _networkRunner.IsRunning) _networkRunner.Shutdown();
    }

    private void StopAnyActiveLoading()
    {
        if (LoadingManager.Instance != null) LoadingManager.Instance.CancelLoading();
        if (!Application.isEditor)
        {
            GameObject gameUI = GameObject.Find("GameUI");
            if (gameUI != null) gameUI.SetActive(false);
            if (gameUI == null)
            {
                gameUI = GameObject.Find("GameUI(Clone)");
                if (gameUI != null) gameUI.SetActive(false);
            }
        }
    }

#pragma warning disable UNT0006
    public void OnConnectedToServer(NetworkRunner runner)
    {
        if (LoadingManager.Instance != null) LoadingManager.Instance.CompleteStep("NetworkConnection");
        if (runner.IsServer) StartCoroutine(SpawnServerManagersOptimized());
        else StartCoroutine(WaitForNetworkManagers());
    }

    private IEnumerator WaitForNetworkManagers()
    {
        float timeout = 10f;
        float elapsed = 0f;
        while (PartyManager.Instance == null && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }
        if (PartyManager.Instance == null) Debug.LogError("[NetworkManager-Client] PartyManager not replicated after timeout!");
    }

    private IEnumerator SpawnServerManagersOptimized()
    {
        yield return new WaitForSeconds(0.1f);
        if (!_networkRunner.IsServer) yield break;
        if (RaceManager.Instance == null)
        {
            var raceManagerPrefab = Resources.Load<NetworkObject>("RaceManager");
            if (raceManagerPrefab != null) _networkRunner.Spawn(raceManagerPrefab, Vector3.zero, Quaternion.identity);
        }
        if (CharacterDataManager.Instance == null)
        {
            var obj = new GameObject("CharacterDataManager");
            obj.AddComponent<CharacterDataManager>();
            DontDestroyOnLoad(obj);
        }
        SpawnNetworkManagerInstant("ChatSystem", () => FindFirstObjectByType<ChatManager>() == null);
        SpawnNetworkManagerInstant("PartyManager", () => PartyManager.Instance == null);
        SpawnNetworkManagerInstant("MonsterSpawner", () => FindFirstObjectByType<MonsterSpawner>() == null);
        SpawnNetworkManagerInstant("DecorationSpawner", () => FindFirstObjectByType<DecorationSpawner>() == null);
    }

    private IEnumerator SpawnServerManagersOnStartup()
    {
        if (!_networkRunner.IsServer) yield break;
        yield return new WaitForSeconds(0.1f);
        if (RaceManager.Instance == null)
        {
            var raceManagerPrefab = Resources.Load<NetworkObject>("RaceManager");
            if (raceManagerPrefab != null) _networkRunner.Spawn(raceManagerPrefab, Vector3.zero, Quaternion.identity);
        }
        SpawnManagersParallel();
    }

private void SpawnManagersParallel()
{
    if (CharacterDataManager.Instance == null)
    {
        var obj = new GameObject("CharacterDataManager");
        obj.AddComponent<CharacterDataManager>();
        DontDestroyOnLoad(obj);
    }
    
    // BindstoneManager satırını KALDIR - scene'de var
    
    SpawnNetworkManagerInstant("ChatSystem", () => FindFirstObjectByType<ChatManager>() == null);
    SpawnNetworkManagerInstant("PartyManager", () => PartyManager.Instance == null);
    SpawnNetworkManagerInstant("MonsterSpawner", () => FindFirstObjectByType<MonsterSpawner>() == null);
    SpawnNetworkManagerInstant("DecorationSpawner", () => FindFirstObjectByType<DecorationSpawner>() == null);
}

    private void SpawnNetworkManagerInstant(string prefabName, System.Func<bool> checkCondition)
    {
        if (checkCondition())
        {
            var prefab = Resources.Load<NetworkObject>(prefabName);
            if (prefab != null) _networkRunner.Spawn(prefab, Vector3.zero, Quaternion.identity);
        }
    }

    private IEnumerator SpawnNetworkManager(string prefabName, System.Func<bool> checkCondition)
    {
        yield return new WaitForEndOfFrame();
        if (checkCondition())
        {
            var prefab = Resources.Load<NetworkObject>(prefabName);
            if (prefab != null) _networkRunner.Spawn(prefab, Vector3.zero, Quaternion.identity);
        }
    }

    public void OnPlayerRaceReceived(PlayerRef player, PlayerRace race) { }

    /// <summary>
    /// NetworkRunner'ı sıfırlar ve YENİ INSTANCE oluşturur
    /// DOKÜMAN KRİTİK KURAL (satır 10-11):
    /// "NetworkRunner yalnızca bir kez kullanılabilir. Disconnect sonrası destroy edilmeli ve yeni instance oluşturulmalıdır."
    /// </summary>
    public void ResetConnection()
    {
        Debug.Log("[NetworkManager] ResetConnection called - Creating new NetworkRunner instance...");

        currentConnectionAttempt = 0;
        isConnecting = false;

        if (connectionTimeoutCoroutine != null)
        {
            StopCoroutine(connectionTimeoutCoroutine);
            connectionTimeoutCoroutine = null;
        }

        // ESKİ NetworkRunner'ı temizle
        if (_networkRunner != null)
        {
            Debug.Log("[NetworkManager] Removing callbacks from old runner...");
            try
            {
                _networkRunner.RemoveCallbacks(this);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NetworkManager] Error removing callbacks: {e.Message}");
            }

            // GameObject silme - ServerManager'da hallettik
            _networkRunner = null;
        }

        // YENİ NetworkRunner Component Ekle
        Debug.Log("[NetworkManager] Adding new NetworkRunner component...");
        _networkRunner = gameObject.GetComponent<NetworkRunner>();
        if (_networkRunner == null)
        {
            _networkRunner = gameObject.AddComponent<NetworkRunner>();
        }

        // Physics simulator ekle
        if (_networkRunner.GetComponent<RunnerSimulatePhysics2D>() == null)
        {
            var physicsSimulator = _networkRunner.gameObject.AddComponent<RunnerSimulatePhysics2D>();
            physicsSimulator.ClientPhysicsSimulation = Fusion.Addons.Physics.ClientPhysicsSimulation.SimulateForward;
            Debug.Log("[NetworkManager] Added RunnerSimulatePhysics2D to new runner");
        }

        // Callbacks ekle
        _networkRunner.AddCallbacks(this);

        Debug.Log("[NetworkManager] ✓ New NetworkRunner instance created and configured");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.LogWarning($"[NetworkManager] OnDisconnectedFromServer called. Reason: {reason}");
        _spawnedCharacters.Clear();
        isConnecting = false;

        // Server-specific disconnection handling
        if (IsServerMode())
        {
            Debug.LogError($"[SERVER] Disconnected from Photon Cloud! Reason: {reason}");

            // Timeout coroutine cleanup
            if (connectionTimeoutCoroutine != null)
            {
                StopCoroutine(connectionTimeoutCoroutine);
                connectionTimeoutCoroutine = null;
            }

            // ServerManager'a bildir ve recovery başlat
            if (ServerManager.Instance != null)
            {
                ServerManager.Instance.OnServerDisconnected(reason.ToString());
            }
            else
            {
                // ServerManager yoksa direkt yeniden bağlanmayı dene
                Debug.LogError("[SERVER] ServerManager not found! Attempting direct reconnection...");
                if (!isQuitting && currentConnectionAttempt < maxConnectionAttempts)
                {
                    StartCoroutine(DelayedRetryConnection());
                }
                else
                {
                    Debug.LogError("[SERVER] Max reconnection attempts reached. Shutting down...");
                    Application.Quit();
                }
            }
            return;
        }

        // Client-specific disconnection handling
        if (LocalPlayerManager.Instance != null) LocalPlayerManager.Instance.ClearLocalPlayer();
        if (connectionTimeoutCoroutine != null)
        {
            StopCoroutine(connectionTimeoutCoroutine);
            connectionTimeoutCoroutine = null;
        }
        if (loginManager == null) loginManager = FindAnyObjectByType<SimpleLoginManager>();
        if (loginManager != null) loginManager.OnConnectionFailed();
        if (!isQuitting && currentConnectionAttempt < maxConnectionAttempts) StartCoroutine(DelayedRetryConnection());
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        if (_networkRunner.IsServer) StartCoroutine(CleanupOrphanedObjects());
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (runner.IsClient) StartCoroutine(VerifyNetworkObjectsAfterSceneLoad());
    }

    public Vector2 GetSpawnPosition(PlayerRace race = PlayerRace.Human)
    {
        return CalculateSpawnPosition(race);
    }

    private IEnumerator SyncExistingNicknamesToNewPlayer(PlayerRef newPlayer)
    {
        yield return new WaitForSeconds(2f);
        foreach (var kvp in _spawnedCharacters)
        {
            if (kvp.Value != null && kvp.Value.IsValid && kvp.Value.HasInputAuthority)
            {
                PlayerStats playerStats = kvp.Value.GetComponent<PlayerStats>();
                if (playerStats != null)
                {
                    string currentNickname = playerStats.GetPlayerDisplayName();
                    if (!string.IsNullOrEmpty(currentNickname)) playerStats.SyncNicknameToSpecificPlayerRPC(currentNickname);
                }
                yield return new WaitForSeconds(0.1f);
            }
        }
    }

    private IEnumerator VerifyNetworkObjectsAfterSceneLoad()
    {
        yield return new WaitForSeconds(1f);
        NetworkObject[] allNetworkObjects = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
        foreach (NetworkObject netObj in allNetworkObjects)
        {
            if (netObj != null && netObj.IsValid)
            {
                if (netObj.transform.hasChanged) netObj.transform.hasChanged = false;
            }
        }
    }

    public void OnSceneLoadStart(NetworkRunner runner) { }
}