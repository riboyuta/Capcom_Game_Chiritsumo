using UnityEngine;

// バネ床ギミック。
// プレイヤーが接触すると固定高さだけ跳ね上げる。
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class SpringPad : MonoBehaviour
{
    [Header("跳ね返し: 到達高さ（メートル）")]
    [Tooltip("バネで跳ね上がったときに到達する高さ（m）。物理公式 v = √(2gh) で初速を自動計算します。")]
    [SerializeField, Min(0.1f)] private float bounceHeight = 5f;

    [Header("跳ね返し: 重力スケール")]
    [Tooltip("プレイヤーの重力倍率に合わせてください。PlayerMovementSettings.gravityScale と同じ値を設定すると正確な高さになります。")]
    [SerializeField, Min(0.1f)] private float gravityScale = 3f;

    [Header("跳ね返し: クールダウン（秒）")]
    [Tooltip("同一オブジェクトが短時間で連続して跳ねられるのを防ぐクールダウン時間（秒）。値を大きくすると連続バウンスを減らせます。")]
    [SerializeField, Min(0f)] private float bounceCooldown = 0.2f;

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
            anim.SetTrigger("Interacted");
        }

        // SE:作動する瞬間
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayOverlap("SFX_gimmick_springpad");
        }

        lastBounceTime = Time.time;

        // プレイヤーに固定ジャンプを適用する。
        // ジャンプボタンの押下状態に関係なく、常に一定高さだけ跳ねる。
        PlayerFacade facade = targetRb.GetComponent<PlayerFacade>();
        if (facade != null)
        {
            // ステップ回数を回復させる
            facade.TryRefillDash(DashRefillReason.Gimmick);

            Vector3 bounceVelocity = ComputeBounceVelocity(targetRb);

            // Celesteのような横向きバネの挙動（強制移動）を実現するため、
            // 「初速（bounceSpeed）で設定距離（bounceHeight）を移動するのにかかる時間」を計算し
            // その時間だけプレイヤーの入力をロックする。
            float effectiveGravity = Mathf.Abs(Physics.gravity.y) * gravityScale;
            float bounceSpeed = Mathf.Sqrt(2f * effectiveGravity * bounceHeight);
            float lockDuration = bounceSpeed > 0f ? bounceHeight / bounceSpeed : 0f;

            // 短すぎたり長すぎたりしないように制限する（最低限の強制移動時間を確保）
            lockDuration = Mathf.Clamp(lockDuration, 0.15f, 0.5f);

            facade.ApplyFixedJump(bounceVelocity, lockDuration);
        }
        else
        {
            // プレイヤー以外の Rigidbody 用フォールバック。
            ApplyBounce(targetRb);
        }
    }

    // ──────────────────────────────────────────────
    // 跳ね返しロジック
    // ──────────────────────────────────────────────

    // 跳ね返し後の速度ベクトルを計算する。
    // 横速度は維持し、跳ね返し方向の既存速度だけを新しい速度で上書きする。
    private Vector3 ComputeBounceVelocity(Rigidbody targetRb)
    {
        // このオブジェクトのローカル上方向を跳ね返し方向とする。
        Vector3 bounceDir = transform.up.normalized;

        // 有効重力の大きさを求める（下向きを正とする）。
        float effectiveGravity = Mathf.Abs(Physics.gravity.y) * gravityScale;

        // 指定高さへ到達するための初速を計算する。 v = √(2gh)
        float bounceSpeed = Mathf.Sqrt(2f * effectiveGravity * bounceHeight);

        // 現在の速度を取得する。
        Vector3 velocity = targetRb.linearVelocity;

        // 跳ね返し方向の既存速度成分を除去し、新しい速度で上書きする。
        // 横速度はそのまま維持される。
        float dot = Vector3.Dot(velocity, bounceDir);
        velocity -= dot * bounceDir;
        velocity += bounceDir * bounceSpeed;

        return velocity;
    }

    // プレイヤー以外の Rigidbody に跳ね返し速度を適用する。
    private void ApplyBounce(Rigidbody targetRb)
    {
        targetRb.linearVelocity = ComputeBounceVelocity(targetRb);
    }
}