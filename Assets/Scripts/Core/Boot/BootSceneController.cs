using Game.Input;
using UnityEngine;

public sealed class BootSceneController : MonoBehaviour
{
    public static BootSceneController Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {

    }

    private void Start()
    {
        Debug.Log("[BootSceneController] Boot scene started.");

        // Boot起動時は画面を暗転状態にしてからTitleへ遷移する。
        // Title側のStart()でフェードインが走る。
        if (FadeController.Instance != null)
        {
            FadeController.Instance.FadeOut(0f, () => SceneFlow.LoadTitle());
        }
        else
        {
            SceneFlow.LoadTitle();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}