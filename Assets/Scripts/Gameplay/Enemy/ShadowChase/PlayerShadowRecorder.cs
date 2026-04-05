using System.Collections.Generic;
using UnityEngine;

// プレイヤーの追尾用 snapshot を一定間隔で履歴保存する。
// 敵はこの Recorder だけを見ればよいようにする。
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

    private readonly List<PlayerShadowSnapshot> history = new List<PlayerShadowSnapshot>();
    private float recordTimer = 0.0f;

    public IReadOnlyList<PlayerShadowSnapshot> History => history;

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
    }

    private void FixedUpdate()
    {
        if (playerController == null)
        {
            return;
        }

        recordTimer += Time.fixedDeltaTime;

        if (recordTimer < recordInterval)
        {
            return;
        }

        // 長いフレームでも極端に取りこぼさないように interval 分だけ減算する。
        recordTimer -= recordInterval;

        RecordSnapshot();
        TrimOldHistory();
    }

    private void RecordSnapshot()
    {
        PlayerShadowSnapshot snapshot = playerController.CaptureShadowSnapshot();
        history.Add(snapshot);
    }

    private void TrimOldHistory()
    {
        float thresholdTime = Time.time - maxHistoryDuration;

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
        result.isStepping = t < 0.5f ? a.isStepping : b.isStepping;
        result.isFastFalling = t < 0.5f ? a.isFastFalling : b.isFastFalling;

        result.isActionLocked = t < 0.5f ? a.isActionLocked : b.isActionLocked;
        result.isDead = t < 0.5f ? a.isDead : b.isDead;

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