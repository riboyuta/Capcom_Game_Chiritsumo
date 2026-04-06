using UnityEngine;
using System;

[RequireComponent(typeof(Rigidbody))]
public class CannonFlightMonitor : MonoBehaviour
{
    private Vector3 flightDirection;
    private float flightSpeed;
    private float maxDistance;
    private LayerMask collisionLayers;
    private Action onFlightEnded;
    private Renderer[] hiddenRenderers;
    private Collider myCollider;
    private Collider cannonCollider;

    private Vector3 startPos;
    private Rigidbody rb;
    private bool isEnded = false;
    private Vector3 lastPos;
    private float stuckTimer = 0f;

    public void Initialize(Vector3 direction, float speed, float distance, LayerMask layerMask, Renderer[] renderersToRestore, Collider playerCol, Collider sourceCannonCol, Action onEnded)
    {
        flightDirection = direction.normalized;
        flightSpeed = speed;
        maxDistance = distance;
        collisionLayers = layerMask;
        onFlightEnded = onEnded;
        hiddenRenderers = renderersToRestore;
        myCollider = playerCol;
        cannonCollider = sourceCannonCol;

        rb = GetComponent<Rigidbody>();
        startPos = transform.position;
        lastPos = startPos;
        stuckTimer = 0f;
        isEnded = false;
        
        // 発射時は重力をオフにし、初速を与える
        rb.isKinematic = false;
        rb.useGravity = false;
        rb.linearVelocity = flightDirection * flightSpeed;
    }

    private void FixedUpdate()
    {
        if (isEnded) return;

        // 飛行中は常に一定の速度を維持する
        rb.linearVelocity = flightDirection * flightSpeed;

        // 指定された最大距離を超えたかチェック
        if (Vector3.Distance(startPos, transform.position) >= maxDistance)
        {
            EndFlight();
            return;
        }

        // 壁などに引っかかってしまった場合のフェイルセーフ（スタック検知）
        if (Vector3.Distance(lastPos, transform.position) < (flightSpeed * Time.fixedDeltaTime * 0.1f))
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer > 0.1f) // 0.1秒以上引っかかったら終了
            {
                EndFlight();
                return;
            }
        }
        else
        {
            stuckTimer = 0f;
        }
        lastPos = transform.position;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isEnded) return;

        // collisionLayers が何も指定されていない(0)場合は、全ての衝突で終了するフェイルセーフ
        bool shouldEnd = (collisionLayers.value == 0) || (((1 << collision.gameObject.layer) & collisionLayers) != 0);

        if (shouldEnd)
        {
            EndFlight();
        }
    }

    private void EndFlight()
    {
        if (isEnded) return;
        isEnded = true;

        // 発射後は速度をゼロにし、PlayerController への引き継ぎによって自然落下させる
        rb.linearVelocity = Vector3.zero;
        
        // レンダラーを通して透明状態から復帰させる
        if (hiddenRenderers != null)
        {
            foreach (var r in hiddenRenderers)
            {
                if (r != null)
                {
                    r.enabled = true;
                }
            }
        }

        // 大砲との衝突無視を解除する
        if (myCollider != null && cannonCollider != null)
        {
            Physics.IgnoreCollision(myCollider, cannonCollider, false);
        }

        // 大砲ギミック側からのコールバックを呼んで PlayerController をオンに戻す
        onFlightEnded?.Invoke();

        // 飛行監視スクリプト自身の役目を終えたため削除する
        Destroy(this);
    }
}
