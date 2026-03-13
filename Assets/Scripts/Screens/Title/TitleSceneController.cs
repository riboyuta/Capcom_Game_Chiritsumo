using UnityEngine;
using UnityEngine.InputSystem;

public sealed class TitleSceneController : MonoBehaviour
{
    [Header("デバッグ入力")]
    [SerializeField] private Key startKey = Key.F9;

    //入力連打で二重遷移防止

    private bool isTransitioning;

    private void Start()
    {
        Debug.Log("[TitleSceneController] Title scene started.");
    }

    private void Update()
    {
        if (isTransitioning)
        {
            return;
        }

        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard[startKey].wasPressedThisFrame)
        {
            StartGame();
        }
    }

    public void StartGame()
    {
        if (isTransitioning)
        {
            return;
        }

        isTransitioning = true;
        SceneFlow.LoadGame();
    }
}