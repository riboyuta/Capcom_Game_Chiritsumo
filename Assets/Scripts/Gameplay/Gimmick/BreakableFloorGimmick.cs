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

    [Header("Floor Settings")]
    [Header("振動する見た目のオブジェクト（判定と分けるため、子オブジェクトを指定）")]
    [SerializeField] private Transform visualTransform;

    [Header("乗ってから床が壊れるまでの時間（秒）")]
    [SerializeField, Min(0f)] private float timeToBreak = 2.0f;

    [Header("壊れた後、再復活するまでの時間（秒）")]
    [SerializeField, Min(0f)] private float respawnInterval = 3.0f;

    [Header("Vibration Settings")]
    [Header("振動の強さ（揺れ幅）")]
    [SerializeField, Min(0f)] private float vibrationIntensity = 0.05f;

    [Header("振動の速さ")]
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