using UnityEngine;

[DisallowMultipleComponent]
public sealed class SonarChargerPlayerMotionDetector : MonoBehaviour
{
    // プレイヤーの前フレーム位置
    private Vector3 previousPlayerPosition;
    private bool hasPreviousPlayerPosition;

    // 移動判定結果
    private bool hasInputMove;
    private bool hasPositionDeltaMove;

    // ダッシュ入力追跡
    private int lastConsumedDashInputFrame = -1;

    public bool HasInputMove => hasInputMove;
    public bool HasPositionDeltaMove => hasPositionDeltaMove;

    // 検知器をリセットし、初期状態に戻す
    public void ResetDetector(PlayerController targetPlayer)
    {
        // 移動判定フラグをリセット
        hasInputMove = false;
        hasPositionDeltaMove = false;

        if (targetPlayer != null)
        {
            // 現在のプレイヤー位置を前フレーム位置として記録
            previousPlayerPosition = targetPlayer.transform.position;
            hasPreviousPlayerPosition = true;
            // 現時点までのダッシュ入力を消費済みにする
            lastConsumedDashInputFrame = targetPlayer.LastAcceptedDashInputFrame;
        }
        else
        {
            // プレイヤーがいない場合は全てゼロ/無効状態に
            previousPlayerPosition = Vector3.zero;
            hasPreviousPlayerPosition = false;
            lastConsumedDashInputFrame = -1;
        }
    }

    // 毎フレーム更新: 入力と位置差分から移動判定を更新
    public void Tick(PlayerController targetPlayer, SonarChargerSettings settings)
    {
        if (targetPlayer == null || settings == null)
        {
            ResetMoveFlags();
            return;
        }

        UpdateInputMove(targetPlayer, settings);
        UpdatePositionDeltaMove(targetPlayer, settings);
    }

    // 指定モードに基づいて移動判定を返す
    public bool IsMoving(SonarChargerMoveDetectMode mode)
    {
        switch (mode)
        {
            case SonarChargerMoveDetectMode.Input:
                return hasInputMove;

            case SonarChargerMoveDetectMode.PositionDelta:
                return hasPositionDeltaMove;

            case SonarChargerMoveDetectMode.Either:
                return hasInputMove || hasPositionDeltaMove;

            default:
                return false;
        }
    }

    // 入力判定: 移動入力がしきい値以上かをチェック
    private void UpdateInputMove(PlayerController targetPlayer, SonarChargerSettings settings)
    {
        // プレイヤーの移動入力を取得
        Vector2 moveInput = targetPlayer.MoveInputDirection;
        // しきい値の2乗を計算（sqrMagnitudeと比較するため）
        float thresholdSquared = CalculateThresholdSquared(settings.inputMoveThreshold);
        // 入力の大きさがしきい値以上なら「動いている」
        hasInputMove = moveInput.sqrMagnitude >= thresholdSquared;
    }

    // 位置差分判定: 前フレームからしきい値以上移動したかをチェック
    private void UpdatePositionDeltaMove(PlayerController targetPlayer, SonarChargerSettings settings)
    {
        Vector3 currentPosition = targetPlayer.transform.position;

        // 前フレーム位置がない場合（初回）、現在位置を記録して終了
        if (!hasPreviousPlayerPosition)
        {
            previousPlayerPosition = currentPosition;
            hasPreviousPlayerPosition = true;
            hasPositionDeltaMove = false;
            return;
        }

        // 前フレームからの移動量を計算
        Vector2 delta = ToVector2(currentPosition) - ToVector2(previousPlayerPosition);
        // しきい値の2乗を計算
        float thresholdSquared = CalculateThresholdSquared(settings.positionMoveThreshold);
        // 移動量がしきい値以上なら「動いている」
        hasPositionDeltaMove = delta.sqrMagnitude >= thresholdSquared;
        // 現在位置を前フレーム位置として記録
        previousPlayerPosition = currentPosition;
    }

    // ダッシュ入力トリガー: 未消費の新しいダッシュ入力があれば消費してtrueを返す
    public bool ConsumeDashInputTrigger(PlayerController targetPlayer)
    {
        if (targetPlayer == null)
        {
            return false;
        }

        // プレイヤーが最後にダッシュを受け付けたフレームを取得
        int dashInputFrame = targetPlayer.LastAcceptedDashInputFrame;
        // フレームが無効、または既に消費済みならfalse
        if (dashInputFrame < 0 || dashInputFrame <= lastConsumedDashInputFrame)
        {
            return false;
        }

        // 新しいダッシュ入力を消費済みにマーク
        lastConsumedDashInputFrame = dashInputFrame;
        return true; // 新しいダッシュ入力検知
    }

    // ダッシュ入力ベースライン同期: 現時点までの入力を消費済みにする
    public void SyncDashInputBaseline(PlayerController targetPlayer)
    {
        lastConsumedDashInputFrame = targetPlayer?.LastAcceptedDashInputFrame ?? -1;
    }

    // ヘルパー: 移動フラグをリセット
    private void ResetMoveFlags()
    {
        hasInputMove = false;
        hasPositionDeltaMove = false;
    }

    // ユーティリティ: しきい値の2乗を計算（sqrMagnitude比較用）
    private static float CalculateThresholdSquared(float threshold)
    {
        float clamped = Mathf.Max(0.0f, threshold);
        return clamped * clamped;
    }

    // ユーティリティ: Vector3をVector2に変換
    private static Vector2 ToVector2(Vector3 vector)
    {
        return new Vector2(vector.x, vector.y);
    }
}