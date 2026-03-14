using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
public sealed class DebugOverlay : MonoBehaviour
{
    [Header("参照先")]
    // GameRoot から状態や残り時間を読むための参照
    [SerializeField] private GameRoot gameRoot;

    [Header("表示設定")]
    // デバッグ表示を最初から見せるかどうか
    [SerializeField] private bool isVisible = true;

    // 実行中にキーで表示ON/OFFを切り替えられるようにするか
    [SerializeField] private bool allowRuntimeToggle = true;



    [Header("レイアウト設定")]
    // 左上からどの位置に表示を始めるか
    [SerializeField] private Vector2 startPosition = new Vector2(10f, 10f);

    // 1行ごとの縦間隔
    [SerializeField] private float lineHeight = 22f;

    // 1行の表示幅
    [SerializeField] private float width = 300f;

    private void Update()
    {
        // 実行中切り替えを許可していないなら何もしない
        if (!allowRuntimeToggle)
        {
            return;
        }


        if (BootSceneController.Instance.DebugInput.ToggleDebugViewPressed)
        {
            isVisible = !isVisible;
        }

    }

    private void OnGUI()
    {
        // 非表示設定なら描画しない
        if (!isVisible)
        {
            return;
        }

        // GameRoot が未設定なら、何が足りないかだけ表示する
        if (gameRoot == null)
        {
            DrawLine(0, "DebugOverlay : GameRoot が未設定です");
            return;
        }

        // 現在のシーン名を表示
        DrawLine(0, $"Scene : {SceneManager.GetActiveScene().name}");

        // 現在の状態名を表示
        DrawLine(1, $"State : {gameRoot.GetCurrentStateName()}");

        // 残りプレイ時間を小数点2桁で表示
        DrawLine(2, $"Timer : {gameRoot.GetRemainingPlayTime():F2}");
    }

    private void DrawLine(int lineIndex, string text)
    {
        // 表示位置を行番号から計算する
        float x = startPosition.x;
        float y = startPosition.y + (lineHeight * lineIndex);

        // Label を描画する矩形領域
        Rect rect = new Rect(x, y, width, lineHeight);

        // 画面上に文字列を表示する
        GUI.Label(rect, text);
    }
}