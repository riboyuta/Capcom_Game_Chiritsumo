using Game.Input;
using UnityEngine;

public sealed class BootSceneController : MonoBehaviour
{
    public static BootSceneController Instance { get; private set; }

    [Header("Input")]
    [SerializeField]
    private RawInputSource _rawInputSource;

    [SerializeField]
    private SystemInputBindings _systemInputBindings = new SystemInputBindings();

    [SerializeField]
    private DebugInputBindings _debugInputBindings = new DebugInputBindings();

    private SystemInputReader _systemInputReader;
    private DebugInputReader _debugInputReader;

    public SystemInputReader SystemInput => _systemInputReader;
    public DebugInputReader DebugInput => _debugInputReader;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (_rawInputSource == null)
        {
            _rawInputSource = GetComponent<RawInputSource>();
        }

        if (_rawInputSource == null)
        {
            Debug.LogError("[BootSceneController] RawInputSource is required.", this);
            enabled = false;
            return;
        }

        _systemInputReader = new SystemInputReader(_rawInputSource, _systemInputBindings);
        _debugInputReader = new DebugInputReader(_rawInputSource, _debugInputBindings);
    }

    private void Update()
    {
        if (_systemInputReader == null)
        {
            return;
        }

        _systemInputReader.Update();
    }

    private void Start()
    {
        Debug.Log("[BootSceneController] Boot scene started.");
        SceneFlow.LoadTitle();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}