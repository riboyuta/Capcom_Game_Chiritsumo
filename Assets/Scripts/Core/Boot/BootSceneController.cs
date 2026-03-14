using UnityEngine;

public sealed class BootSceneController : MonoBehaviour
{
    private void Start()
    {
        //タイトルシーン前に必要な初期化処理をここに書く
        // 例: ゲーム設定の読み込み、セーブデータの確認、広告の初期化など

        Debug.Log("[BootSceneController] Boot scene started.");

        SceneFlow.LoadTitle();
    }
}