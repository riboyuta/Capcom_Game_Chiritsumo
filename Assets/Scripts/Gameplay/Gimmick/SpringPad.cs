using UnityEngine;

// バネ床ギミック。
// プレイヤーが接触すると transform.up 方向へ跳ね返す。
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class SpringPad : MonoBehaviour
{
    [Header("跳ね返し: 速度")]
    [Tooltip("跳ね返すときに与える速度（m/s）。値が大きいほど高く／遠くへ飛びます。")]
    [SerializeField, Min(0.1f)] private float bounceSpeed = 15f;

    [Header("跳ね返し: 速度上書きフラグ")]
    [Tooltip("true の場合、跳ね返し軸方向の既存速度を上書きします。false の場合は既存速度に跳ね返しベクトルを加算します。用途に応じて選択してください。")]
    [SerializeField] private bool overrideVelocity = true;

    [Header("跳ね返し: クールダウン（秒）")]
    [Tooltip("同一オブジェクトが短時間で連続して跳ねられるのを防ぐクールダウン時間（秒）。値を大きくすると連続バウンスを減らせます。")]
    [SerializeField, Min(0f)] private float bounceCooldown = 0.2f;

    [Header("最後にバウンスした時刻")]
    private float lastBounceTime = -1f;

    [Header("アニメーション")]
    [Tooltip("使用するアニメーターを入れる")]
    [SerializeField] private Animator anim;



    // ──────────────────────────────────────────────
    // 接触検出
    // Collider が Trigger なら OnTriggerEnter、
    // 固体コライダーなら OnCollisionEnter が呼ばれる。
    // どちらでも動作するよう両方を実装する。
    // ──────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        TryBounce(other.attachedRigidbody);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.collider.CompareTag("Player")) return;
        TryBounce(collision.rigidbody);
    }

    private void TryBounce(Rigidbody targetRb)
    {
        // Rigidbody がなければ物理的に動かせないので何もしない。
        if (targetRb == null)
        {
            return;
        }

        // クールダウン中なら無視する。
        if (Time.time - lastBounceTime < bounceCooldown)
        {
            return;
        }


        // アニメーション再生
        if (anim != null)
        {
            Debug.Log(anim);
            anim.SetTrigger("Interacted");
     
        }


        lastBounceTime = Time.time;
        ApplyBounce(targetRb);
    }

    // ──────────────────────────────────────────────
    // 跳ね返しロジック
    // ──────────────────────────────────────────────

    private void ApplyBounce(Rigidbody targetRb)
    {
        // このオブジェクトのローカル上方向を跳ね返し方向とする。
        Vector3 bounceDir = transform.up.normalized;

        // 現在の速度を取得する。
        Vector3 velocity = targetRb.linearVelocity;

        if (overrideVelocity)
        {
            float dot = Vector3.Dot(velocity, bounceDir);
            velocity -= dot * bounceDir;
            velocity += bounceDir * bounceSpeed;
        }
        else
        {
            velocity += bounceDir * bounceSpeed;
        }

        targetRb.linearVelocity = velocity;
    }
}