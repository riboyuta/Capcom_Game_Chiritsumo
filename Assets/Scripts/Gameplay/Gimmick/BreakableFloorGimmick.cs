using System.Collections;
using UnityEngine;

// プレイヤーが乗ると振動し、一定時間後に壊れ（消滅し）、その後リスポーンする床ギミック
[RequireComponent(typeof(Collider))]
public class BreakableFloorGimmick : MonoBehaviour
{
    private enum FloorState
    {
        Idle,
        Vibrating,
        Broken
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

    private Collider floorCollider;
    private Renderer[] visualRenderers;
    private FloorState currentState = FloorState.Idle;

    private Vector3 initialVisualLocalPos;
    private Coroutine sequenceCoroutine;

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

        initialVisualLocalPos = visualTransform.localPosition;

        // 見た目のRendererを一括取得（オンオフ切り替え用）
        visualRenderers = visualTransform.GetComponentsInChildren<Renderer>();
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

        // rigidbody があるオブジェクトが乗ったら反応
        if (other.attachedRigidbody != null)
        {
            // 乗ったらシーケンス開始
            sequenceCoroutine = StartCoroutine(BreakSequence());
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

        // 3. リスポーン待ち
        yield return new WaitForSeconds(respawnInterval);

        // 4. 再出現
        RespawnFloor();
    }

    private void BreakFloor()
    {
        currentState = FloorState.Broken;
        floorCollider.enabled = false;

        foreach (var r in visualRenderers)
        {
            r.enabled = false;
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