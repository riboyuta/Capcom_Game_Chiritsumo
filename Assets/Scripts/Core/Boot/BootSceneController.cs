using UnityEngine;

public sealed class BootSceneController : MonoBehaviour
{
    private void Start()
    {

        Debug.Log("[BootSceneController] Boot scene started.");

        SceneFlow.LoadTitle();
    }
}