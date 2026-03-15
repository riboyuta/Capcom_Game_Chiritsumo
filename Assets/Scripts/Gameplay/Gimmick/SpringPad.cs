using UnityEngine;

// バネ床ギミック。
// プレイヤーが接触すると transform.up 方向へ跳ね返す。
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class SpringPad : MonoBehaviour
{
    [Header("Bounce Settings")]

    // 跳ね返し速度（m/s）。
    // 値が大きいほどプレイヤーが遠くまで飛ぶ。
    [SerializeField, Min(0.1f)] private float bounceSpeed = 15f;

    // true : 跳ね返し軸の既存速度を上書きする。
    //        バネ床なら Y 速度だけ差し替え、X 速度は残る。
    // false: 既存速度に跳ね返しベクトルを加算する。
    //        勢いが合算されるため、速度が大きくなりやすい。
    [SerializeField] private bool overrideVelocity = true;

    // 連続バウンス防止用クールダウン（秒）。
    [SerializeField, Min(0f)] private float bounceCooldown = 0.2f;

    // 最後にバウンスした時刻。
    private float lastBounceTime = -1f;

    // ──────────────────────────────────────────────
    // 接触検出
    // Collider が Trigger なら OnTriggerEnter、
    // 固体コライダーなら OnCollisionEnter が呼ばれる。
    // どちらでも動作するよう両方を実装する。
    // ──────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        TryBounce(other.attachedRigidbody);
    }

    private void OnCollisionEnter(Collision collision)
    {
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

        lastBounceTime = Time.time;
        ApplyBounce(targetRb);
    }

    // ──────────────────────────────────────────────
    // 跳ね返しロジック
    // ──────────────────────────────────────────────

    private void ApplyBounce(Rigidbody targetRb)
    {
        // このオブジェクトのローカル上方向を跳ね返し方向とする。
        // 例:
        //   回転なし        → (0, 1, 0) → 上方向に跳ねる
        //   Z軸 +90度回転  → (1, 0, 0) → 右方向に跳ねる
        //   Z軸 -90度回転  → (-1, 0, 0) → 左方向に跳ねる
        Vector3 bounceDir = transform.up.normalized;

        // 現在の速度を取得する。
        Vector3 velocity = targetRb.linearVelocity;

        if (overrideVelocity)
        {
            // 跳ね返し軸方向の速度成分を除去してから、
            // bounceSpeed 分を設定する。
            // これにより直交する軸の速度は保持される。
            //
            // 例（床バネ・上方向）:
            //   velocity = (3, -5, 0)
            //   bounceDir = (0, 1, 0)
            //   dot = -5
            //   velocity -= (0, -5, 0)  → (3, 0, 0)
            //   velocity += (0, 15, 0)  → (3, 15, 0)
            //   → 横速度 3 は維持、縦速度が 15 に上書きされる。
            float dot = Vector3.Dot(velocity, bounceDir);
            velocity -= dot * bounceDir;
            velocity += bounceDir * bounceSpeed;
        }
        else
        {
            // 既存速度にそのまま加算する。
            velocity += bounceDir * bounceSpeed;
        }

        targetRb.linearVelocity = velocity;
    }
}
