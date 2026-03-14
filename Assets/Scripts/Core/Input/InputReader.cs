using UnityEngine;
using UnityEngine.InputSystem;

public sealed class InputReader
{
    // =========================
    // 外部公開する意味単位の入力
    // =========================

    // 移動入力（WASD / 矢印キー）
    public Vector2 Move { get; private set; }

    // 決定入力
    public bool SubmitPressed { get; private set; }

    // ポーズ入力
    public bool PausePressed { get; private set; }

    // デバッグ用：強制的にシーン遷移する入力
    public bool DebugTransitionPressed { get; private set; }

    // =========================
    // キーバインド定義
    // =========================
    [Header("デバッグ入力")]

    private readonly Key submitKey = Key.Enter;
    private readonly Key pauseKey = Key.P;
    private readonly Key debugtransitionKey = Key.F9;

    public void Update()
    {
        Keyboard keyboard = Keyboard.current;

        // キーボード未接続なら安全側に倒す
        if (keyboard == null)
        {
            Reset();
            return;
        }

        UpdateMove(keyboard);
        UpdateButtons(keyboard);
    }

    private void UpdateMove(Keyboard keyboard)
    {
        float x = 0f;
        float y = 0f;

        // 左入力
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            x -= 1f;
        }

        // 右入力
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            x += 1f;
        }

        // 下入力
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            y -= 1f;
        }

        // 上入力
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            y += 1f;
        }

        Move = new Vector2(x, y);

        // 斜め移動時の長さを1以下に揃える
        if (Move.sqrMagnitude > 1f)
        {
            Move = Move.normalized;
        }
    }

    private void UpdateButtons(Keyboard keyboard)
    {
        // 押した瞬間だけ拾う入力
        SubmitPressed = keyboard[submitKey].wasPressedThisFrame;
        PausePressed = keyboard[pauseKey].wasPressedThisFrame;
        DebugTransitionPressed = keyboard[debugtransitionKey].wasPressedThisFrame;
    }

    private void Reset()
    {
        Move = Vector2.zero;
        SubmitPressed = false;
        PausePressed = false;
        DebugTransitionPressed = false;
    }
}