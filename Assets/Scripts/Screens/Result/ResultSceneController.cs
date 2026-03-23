using UnityEngine;
using UnityEngine.InputSystem;

public sealed class ResultSceneController : MonoBehaviour
{

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

        //if (BootSceneController.Instance.DebugInput.NextScenePressed)
        //{
        //    ReturnToTitle();
        //}
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