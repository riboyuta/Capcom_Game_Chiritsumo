using System.Collections;
using UnityEngine;

// プレイヤーが一定以上の速度（前ステップなど）で接触すると破壊されるオブジェクト
[RequireComponent(typeof(Collider))]
public class SpeedBreakableGimmick : MonoBehaviour, IRespawnResettable
{
    private enum GimmickState
    {
        Idle,
        Broken
    }

    [Header("破壊条件")]
    [Tooltip("この速度（m/s）以上で接触した場合に破壊されます。")]
    [SerializeField, Min(0f)] private float breakSpeedThreshold = 15.0f;

    [Header("復活設定")]
    [Tooltip("破壊後、一定時間経過で復活するかどうか")]
    [SerializeField] private bool willRespawn = false;

    [Tooltip("破壊されてから復活するまでの時間（秒）")]
    [SerializeField, Min(0f)] private float respawnInterval = 3.0f;

    [Header("見た目（ビジュアル）")]
    [Tooltip("破壊時に非表示にする見た目の Transform。未指定時は自身の階層の Renderer を使用します。")]
    [SerializeField] private Transform visualTransform;

    private Collider gimmickCollider;
    private Renderer[] visualRenderers;
    private GimmickState currentState = GimmickState.Idle;
    private Coroutine respawnCoroutine;
    private bool hasCapturedInitialState;
    private bool initialColliderEnabled;
    private bool[] initialRendererEnabledStates;
    private GimmickState initialState;
    private void Awake()
    {
        gimmickCollider = GetComponent<Collider>();

        if (visualTransform == null)
        {
            if (transform.childCount > 0)
            {
                visualTransform = transform.GetChild(0);
            }
            else
            {
                visualTransform = transform;
            }
        }

        visualRenderers = visualTransform.GetComponentsInChildren<Renderer>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 衝突時の速度条件を判定して破壊可否を決めます。
        CheckAndBreak(collision.collider, collision);
    }

    private void OnTriggerEnter(Collider other)
    {
        // トリガー接触時の速度条件を判定して破壊可否を決めます。
        CheckAndBreak(other, null);
    }

    private void CheckAndBreak(Collider other, Collision collision)
    {
        if (currentState != GimmickState.Idle) return;

        // Player タグが付いていないオブジェクトは無視する
        if (!other.CompareTag("Player")) return;

        // プレイヤーかどうかを判定し、前ステ状態や現在の速度を直接取得する
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null)
        {
            // IsStepping（前ステ状態）が true なら速度に関わらず確実に破壊する
            if (player.IsStepping || player.CurrentVelocity.magnitude >= breakSpeedThreshold)
            {
                BreakGimmick();
                return;
            }
        }

        // PlayerController が取得できなかった場合でも Rigidbody の速度で判定する
        Rigidbody targetRb = other.attachedRigidbody;
        if (targetRb != null)
        {
            float targetSpeed = targetRb.linearVelocity.magnitude;

            // 壁として衝突した際に速度が衝突解決で0になる場合があるため、相対速度も加味する
            if (collision != null)
            {
                targetSpeed = Mathf.Max(targetSpeed, collision.relativeVelocity.magnitude);
            }

            if (targetSpeed >= breakSpeedThreshold)
            {
                BreakGimmick();
            }
        }
    }

    private void BreakGimmick()
    {
        // 破壊状態に移行し、判定と見た目を無効化します。
        currentState = GimmickState.Broken;

        // 判定を一時的に無効化
        gimmickCollider.enabled = false;

        // 見た目を非表示
        foreach (var r in visualRenderers)
        {
            r.enabled = false;
        }

        // ここでパーティクル等を再生したい場合は追加する

        // 復活するかどうか
        if (willRespawn)
        {
            respawnCoroutine = StartCoroutine(RespawnSequence());
        }
    }

    private IEnumerator RespawnSequence()
    {
        yield return new WaitForSeconds(respawnInterval);

        RespawnGimmick();
    }

    private void RespawnGimmick()
    {
        // 復活状態へ戻し、判定と見た目を有効化します。
        currentState = GimmickState.Idle;

        // 判定を復活
        gimmickCollider.enabled = true;

        // 見た目を復活
        foreach (var r in visualRenderers)
        {
            r.enabled = true;
        }

        respawnCoroutine = null;
    }

    public void CaptureInitialState()
    {
        // 初期状態は一度だけ保存し、保存中にランタイム状態は変更しません。
        if (hasCapturedInitialState)
        {
            return;
        }

        initialState = currentState;
        initialColliderEnabled = gimmickCollider != null && gimmickCollider.enabled;

        if (visualRenderers == null)
        {
            initialRendererEnabledStates = new bool[0];
        }
        else
        {
            initialRendererEnabledStates = new bool[visualRenderers.Length];
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                initialRendererEnabledStates[i] = visualRenderers[i] != null && visualRenderers[i].enabled;
            }
        }

        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        // 死亡リセット時は進行中の復活待ちを止め、保存済み初期状態へ即時復元します。
        if (!hasCapturedInitialState)
        {
            CaptureInitialState();
        }

        if (respawnCoroutine != null)
        {
            StopCoroutine(respawnCoroutine);
            respawnCoroutine = null;
        }

        currentState = initialState;

        if (gimmickCollider != null)
        {
            gimmickCollider.enabled = initialColliderEnabled;
        }

        int rendererCount = visualRenderers == null ? 0 : visualRenderers.Length;
        for (int i = 0; i < rendererCount; i++)
        {
            if (visualRenderers[i] == null)
            {
                continue;
            }

            bool enabledState = i < initialRendererEnabledStates.Length && initialRendererEnabledStates[i];
            visualRenderers[i].enabled = enabledState;
        }
    }
}
