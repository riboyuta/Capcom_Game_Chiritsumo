using UnityEngine;
using UnityEngine.InputSystem;

public sealed class ResultSceneController : MonoBehaviour
{
    [Header("デバッグ入力")]
    [SerializeField] private Key returnToTitleKey = Key.Enter;
    //入力連打で二重遷移防止
    private bool isTransitioning;

    private void Start()
    {
        Debug.Log("[ResultSceneController] Result scene started.");
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

        if (keyboard[returnToTitleKey].wasPressedThisFrame)
        {
            ReturnToTitle();
        }
    }

    public void ReturnToTitle()
    {
        if (isTransitioning)
        {
            return;
        }

        isTransitioning = true;
        SceneFlow.LoadTitle();
    }
}