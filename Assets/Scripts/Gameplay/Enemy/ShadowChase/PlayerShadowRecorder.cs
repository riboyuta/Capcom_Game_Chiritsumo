using System.Collections.Generic;
using UnityEngine;

// プレイヤーの追尾用 snapshot を一定間隔で履歴保存する。
// 記録は LateUpdate で行い、PlayerController 側で確定済みの
// CurrentVisualState を取りやすくする。
// PlayerController 本体へ記録呼び出しを追加しない構成。
[DisallowMultipleComponent]
public sealed class PlayerShadowRecorder : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("snapshot を生成する PlayerController です。未設定時は同一 GameObject から取得を試みます。")]
    [SerializeField] private PlayerController playerController;

    [Header("記録設定")]
    [Tooltip("何秒ごとに snapshot を記録するかです。小さいほど滑らかですが履歴数は増えます。")]
    [SerializeField] private float recordInterval = 0.02f;

    [Tooltip("履歴を何秒分保持するかです。ShadowChaserEnemy の delayTime より長くしてください。")]
    [SerializeField] private float maxHistoryDuration = 2.0f;

    [Header("デバッグ")]
    [Tooltip("履歴ラインを Scene 上に表示します。")]
    [SerializeField] private bool showDebugPath = true;

    [Tooltip("履歴ラインの色です。")]
    [SerializeField] private Color debugPathColor = Color.magenta;

    [Header("デバッグ(Runtime)")]
    [Tooltip("最後に記録した snapshot の時刻です。")]
    [SerializeField] private float lastRecordedTime;

    [Tooltip("現在保持している履歴数です。")]
    [SerializeField] private int debugHistoryCount;

    private readonly List<PlayerShadowSnapshot> history = new List<PlayerShadowSnapshot>();

    // 次に記録可能になる時刻。
    private float nextRecordTime = 0.0f;

    public IReadOnlyList<PlayerShadowSnapshot> History => history;

    public PlayerController TargetPlayerController => playerController;

    private void Awake()
    {
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>();
        }

        if (playerController == null)
        {
            Debug.LogError("PlayerShadowRecorder には PlayerController が必要です。", this);
            enabled = false;
            return;
        }

        recordInterval = Mathf.Max(0.001f, recordInterval);
        maxHistoryDuration = Mathf.Max(recordInterval, maxHistoryDuration);

        nextRecordTime = Time.time;
    }

    private void LateUpdate()
    {
        if (!enabled)
        {
            return;
        }

        if (playerController == null)
        {
            return;
        }

        float now = Time.time;

        if (now < nextRecordTime)
        {
            debugHistoryCount = history.Count;
            return;
        }

        RecordSnapshot(now);
        TrimOldHistory(now);

        // 長いフレームでも極端に取りこぼしすぎないように、基準時刻を積み上げる。
        if (nextRecordTime <= 0f)
        {
            nextRecordTime = now + recordInterval;
        }
        else
        {
            while (nextRecordTime <= now)
            {
                nextRecordTime += recordInterval;
            }
        }

        debugHistoryCount = history.Count;
    }

    private void RecordSnapshot(float currentTime)
    {
        PlayerShadowSnapshot snapshot = playerController.CaptureShadowSnapshot();

        // snapshot 側の time は CaptureShadowSnapshot 側で入れている想定だが、
        // 念のため現在時刻で上書きして履歴時刻を統一する。
        snapshot.time = currentTime;

        history.Add(snapshot);
        lastRecordedTime = currentTime;
    }

    private void TrimOldHistory(float currentTime)
    {
        float thresholdTime = currentTime - maxHistoryDuration;

        int removeCount = 0;
        int count = history.Count;
        for (int i = 0; i < count; ++i)
        {
            if (history[i].time < thresholdTime)
            {
                removeCount++;
                continue;
            }

            break;
        }

        if (removeCount > 0)
        {
            history.RemoveRange(0, removeCount);
        }
    }

    // delay 秒前の snapshot を返す。
    // useInterpolation=true なら前後 2 点から補間する。
    public bool TryGetSnapshotAtDelay(float delay, bool useInterpolation, out PlayerShadowSnapshot snapshot)
    {
        float targetTime = Time.time - Mathf.Max(0.0f, delay);
        return TryGetSnapshotAtTime(targetTime, useInterpolation, out snapshot);
    }

    // 指定時刻の snapshot を返す。
    public bool TryGetSnapshotAtTime(float targetTime, bool useInterpolation, out PlayerShadowSnapshot snapshot)
    {
        snapshot = default;

        int count = history.Count;
        if (count == 0)
        {
            return false;
        }

        if (count == 1)
        {
            snapshot = history[0];
            return true;
        }

        // 一番古い時刻より前を要求されたら最古を返す。
        if (targetTime <= history[0].time)
        {
            snapshot = history[0];
            return true;
        }

        // 一番新しい時刻より後を要求されたら最新を返す。
        if (targetTime >= history[count - 1].time)
        {
            snapshot = history[count - 1];
            return true;
        }

        for (int i = 1; i < count; ++i)
        {
            PlayerShadowSnapshot previous = history[i - 1];
            PlayerShadowSnapshot next = history[i];

            if (targetTime > next.time)
            {
                continue;
            }

            if (!useInterpolation)
            {
                snapshot = previous;
                return true;
            }

            float range = next.time - previous.time;
            if (range <= Mathf.Epsilon)
            {
                snapshot = previous;
                return true;
            }

            float t = Mathf.InverseLerp(previous.time, next.time, targetTime);
            snapshot = LerpSnapshot(previous, next, t);
            return true;
        }

        snapshot = history[count - 1];
        return true;
    }

    private PlayerShadowSnapshot LerpSnapshot(PlayerShadowSnapshot a, PlayerShadowSnapshot b, float t)
    {
        PlayerShadowSnapshot result = new PlayerShadowSnapshot();

        result.time = Mathf.Lerp(a.time, b.time, t);

        result.position = Vector3.Lerp(a.position, b.position, t);
        result.rotation = Quaternion.Slerp(a.rotation, b.rotation, t);
        result.velocity = Vector3.Lerp(a.velocity, b.velocity, t);

        result.facing = t < 0.5f ? a.facing : b.facing;

        result.isGrounded = t < 0.5f ? a.isGrounded : b.isGrounded;
        result.isTouchingWall = t < 0.5f ? a.isTouchingWall : b.isTouchingWall;
        result.wallSide = t < 0.5f ? a.wallSide : b.wallSide;

        result.isWallSliding = t < 0.5f ? a.isWallSliding : b.isWallSliding;
        result.isDashing = t < 0.5f ? a.isDashing : b.isDashing;
        result.isFastFalling = t < 0.5f ? a.isFastFalling : b.isFastFalling;

        result.isActionLocked = t < 0.5f ? a.isActionLocked : b.isActionLocked;
        result.isDead = t < 0.5f ? a.isDead : b.isDead;

        // visualState は離散状態として扱い、近い方を採用する。
        result.visualState = t < 0.5f ? a.visualState : b.visualState;

        return result;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugPath)
        {
            return;
        }

        int count = history.Count;
        if (count < 2)
        {
            return;
        }

        Gizmos.color = debugPathColor;

        for (int i = 1; i < count; ++i)
        {
            Gizmos.DrawLine(history[i - 1].position, history[i].position);
        }
    }
}