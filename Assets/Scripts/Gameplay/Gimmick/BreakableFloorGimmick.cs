using System.Collections;
using UnityEngine;

// プレイヤーが乗ると振動し、一定時間後に壊れ（消滅し）、その後リスポーンする床ギミック
[RequireComponent(typeof(Collider))]
public class BreakableFloorGimmick : MonoBehaviour, IRespawnResettable
{
    private const string InteractedTriggerName = "Interacted";

    private enum FloorState
    {
        Idle,
        Vibrating,
        Broken
    }

    private struct AnimatorInitialState
    {
        public bool hasAnimator;
        public bool enabled;
        public int[] stateHashes;
        public float[] normalizedTimes;
        public float[] layerWeights;
    }

    [Header("見た目（ビジュアル）")]
    [Tooltip("振動や消滅表示を行う見た目の Transform。判定と見た目を分離するため子オブジェクトを指定して下さい。未指定時は自身を使用します。")]
    [SerializeField] private Transform visualTransform;

    [Header("壊れるまでの時間（秒）")]
    [Tooltip("プレイヤーなどが乗ってから床が破壊されるまでの時間（秒）。短くすると早く壊れます。")]
    [SerializeField, Min(0f)] private float timeToBreak = 2.0f;

    [Header("再出現までの時間（秒）")]
    [Tooltip("壊れた後、床が再表示／判定を復活させるまでの待機時間（秒）。ゲーム進行に合わせて調整してください。")]
    [SerializeField, Min(0f)] private float respawnInterval = 3.0f;

    [Header("振動: 強度")]
    [Tooltip("振動時の揺れ幅（メートル）。大きいほど見た目の振れが大きくなりますが、当たり判定と視覚のズレに注意してください。")]
    [SerializeField, Min(0f)] private float vibrationIntensity = 0.05f;

    [Header("振動: 速さ")]
    [Tooltip("振動の周波数。値を大きくすると速い振動になります。見た目の雰囲気に合わせて調整してください。")]
    [SerializeField, Min(0.1f)] private float vibrationSpeed = 30.0f;

    [Header("リスポーン設定")]
    [Tooltip("チェックを入れると、破壊から一定時間後に自動で復活します。外すと一度きりで壊れたままになります。")]
    [SerializeField] private bool autoRespawn = true;

    [Header("アニメーション")]
    [Tooltip("使用するアニメーターを入れる")]
    //  [SerializeField] private Animator anim;
    [SerializeField] private Animator[] anim = new Animator[6];

    private Collider floorCollider;
    private Renderer[] visualRenderers;
    private FloorState currentState = FloorState.Idle;

    private Vector3 initialVisualLocalPos;
    private FloorState initialState;
    private bool initialColliderEnabled;
    private bool[] initialRendererEnabledStates;
    private AnimatorInitialState[] initialAnimatorStates;
    private Coroutine sequenceCoroutine;
    private bool hasCapturedInitialState;
    private void Awake()
    {
        floorCollider = GetComponent<Collider>();

        // visualTransformが未指定の場合、最初の子オブジェクトを使用するか、自身を使用する
        if (visualTransform == null)
        {
            if (transform.childCount > 0)
            {
                visualTransform = transform.GetChild(0);
            }
            else
            {
                visualTransform = transform;
                Debug.LogWarning("[BreakableFloorGimmick] Visual Transform が指定されていないため、判定ごと振動します。子のメッシュオブジェクトを指定することを推奨します。");

            }

          
        }


        // 見た目のRendererを一括取得（オンオフ切り替え用）
        visualRenderers = visualTransform.GetComponentsInChildren<Renderer>(true);
    }
    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
        {
            return;
        }

        EnsureRuntimeReferences();

        initialState = currentState;
        initialColliderEnabled = floorCollider != null && floorCollider.enabled;

        if (visualTransform != null)
        {
            initialVisualLocalPos = visualTransform.localPosition;
        }

        CaptureRendererInitialStates();
        CaptureAnimatorInitialStates();

        hasCapturedInitialState = true;
    }

    public void ResetToRespawnState()
    {
        if (!hasCapturedInitialState)
        {
            CaptureInitialState();
        }

        StopBreakSequence();

        currentState = initialState;

        if (floorCollider != null)
        {
            floorCollider.enabled = initialColliderEnabled;
        }

        if (visualTransform != null)
        {
            visualTransform.localPosition = initialVisualLocalPos;
        }

        RestoreRendererInitialStates();
        RestoreAnimatorInitialStates();
    }

    private void EnsureRuntimeReferences()
    {
        if (floorCollider == null)
        {
            floorCollider = GetComponent<Collider>();
        }

        if (visualTransform == null)
        {
            visualTransform = transform.childCount > 0 ? transform.GetChild(0) : transform;
        }

        if (visualRenderers == null)
        {
            visualRenderers = visualTransform != null
                ? visualTransform.GetComponentsInChildren<Renderer>(true)
                : new Renderer[0];
        }
    }

    private void StopBreakSequence()
    {
        // 死亡リセット後に古い破壊/復活処理が状態を書き換えないように停止する。
        if (sequenceCoroutine == null)
        {
            return;
        }

        StopCoroutine(sequenceCoroutine);
        sequenceCoroutine = null;
    }

    private void CaptureRendererInitialStates()
    {
        int rendererCount = visualRenderers != null ? visualRenderers.Length : 0;
        initialRendererEnabledStates = new bool[rendererCount];

        for (int i = 0; i < rendererCount; i++)
        {
            initialRendererEnabledStates[i] = visualRenderers[i] != null && visualRenderers[i].enabled;
        }
    }

    private void RestoreRendererInitialStates()
    {
        int rendererCount = visualRenderers != null ? visualRenderers.Length : 0;

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

    private void CaptureAnimatorInitialStates()
    {
        int animatorCount = anim != null ? anim.Length : 0;
        initialAnimatorStates = new AnimatorInitialState[animatorCount];

        for (int i = 0; i < animatorCount; i++)
        {
            Animator animator = anim[i];
            if (animator == null)
            {
                continue;
            }

            AnimatorInitialState state = new AnimatorInitialState
            {
                hasAnimator = true,
                enabled = animator.enabled
            };

            if (animator.runtimeAnimatorController == null || !animator.gameObject.activeInHierarchy)
            {
                initialAnimatorStates[i] = state;
                continue;
            }

            int layerCount = animator.layerCount;
            state.stateHashes = new int[layerCount];
            state.normalizedTimes = new float[layerCount];
            state.layerWeights = new float[layerCount];

            for (int layer = 0; layer < layerCount; layer++)
            {
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
                state.stateHashes[layer] = stateInfo.fullPathHash;
                state.normalizedTimes[layer] = stateInfo.normalizedTime;
                state.layerWeights[layer] = animator.GetLayerWeight(layer);
            }

            initialAnimatorStates[i] = state;
        }
    }

    private void RestoreAnimatorInitialStates()
    {
        // 破壊開始時の Trigger が残らないようにし、可能な範囲で初期再生状態へ戻す。
        int animatorCount = anim != null ? anim.Length : 0;

        for (int i = 0; i < animatorCount; i++)
        {
            Animator animator = anim[i];
            if (animator == null || initialAnimatorStates == null || i >= initialAnimatorStates.Length)
            {
                continue;
            }

            AnimatorInitialState state = initialAnimatorStates[i];
            if (!state.hasAnimator)
            {
                continue;
            }

            ResetAnimatorTriggerIfExists(animator, InteractedTriggerName);

            if (animator.runtimeAnimatorController != null
                && animator.gameObject.activeInHierarchy
                && state.stateHashes != null)
            {
                animator.enabled = true;

                int layerCount = Mathf.Min(animator.layerCount, state.stateHashes.Length);
                for (int layer = 0; layer < layerCount; layer++)
                {
                    if (state.layerWeights != null && layer < state.layerWeights.Length)
                    {
                        animator.SetLayerWeight(layer, state.layerWeights[layer]);
                    }

                    if (state.stateHashes[layer] != 0)
                    {
                        float normalizedTime = layer < state.normalizedTimes.Length ? state.normalizedTimes[layer] : 0f;
                        animator.Play(state.stateHashes[layer], layer, normalizedTime);
                    }
                }

                animator.Update(0f);
            }

            animator.enabled = state.enabled;
        }
    }

    private static void ResetAnimatorTriggerIfExists(Animator animator, string triggerName)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Trigger
                && parameters[i].name == triggerName)
            {
                animator.ResetTrigger(triggerName);
                return;
            }
        }
    }
    private void OnCollisionEnter(Collision collision)
    {
        CheckAndTrigger(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        CheckAndTrigger(other);
    }

    private void CheckAndTrigger(Collider other)
    {
        // 既に振動中や破損済みの場合は無視
        if (currentState != FloorState.Idle) return;

        // rigidbody があり、かつ Player タグが付いているオブジェクトが乗ったら反応
        if (other.attachedRigidbody != null && other.CompareTag("Player"))
        {
            // 乗ったらシーケンス開始
            sequenceCoroutine = StartCoroutine(BreakSequence());

            // SE:崩壊を始めた瞬間
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayOverlap("SFX_gimmick_breakable_enter");
            }

            // アニメーション再生
            for (int i = 0; i < 6; i++)
            {
                if (anim[i] != null)
                {
                    Debug.Log(anim[i]);
                    anim[i].SetTrigger(InteractedTriggerName);

                }
            }
          
        }
    }

    private IEnumerator BreakSequence()
    {
        currentState = FloorState.Vibrating;
        float elapsed = 0f;

        // 1. 振動フェーズ
        while (elapsed < timeToBreak)
        {
            elapsed += Time.deltaTime;

            // サイン波などを使ってランダムまたは規則的に揺らす
            float offsetX = Mathf.Sin(Time.time * vibrationSpeed) * vibrationIntensity;
            float offsetZ = Mathf.Cos(Time.time * vibrationSpeed * 1.2f) * vibrationIntensity;

            visualTransform.localPosition = initialVisualLocalPos + new Vector3(offsetX, 0f, offsetZ);

            yield return null;
        }

        // 位置をリセット
        visualTransform.localPosition = initialVisualLocalPos;

        // 2. 破壊（消滅）フェーズ
        BreakFloor();

        if (autoRespawn)
        {
            // 3. リスポーン待ち
            yield return new WaitForSeconds(respawnInterval);

            // 4. 再出現
            RespawnFloor();
        }
        sequenceCoroutine = null;
    }

    private void BreakFloor()
    {
        currentState = FloorState.Broken;
        floorCollider.enabled = false;

        foreach (var r in visualRenderers)
        {
            r.enabled = false;
        }

        // SE:壊れる瞬間
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayOverlap("SFX_gimmick_breakable_break");
        }
    }

    private void RespawnFloor()
    {
        currentState = FloorState.Idle;
        floorCollider.enabled = true;

        foreach (var r in visualRenderers)
        {
            r.enabled = true;
        }
    }
}
