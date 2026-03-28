using UnityEngine;

// PlayerController の攻撃リアクション状態を担当する partial。
// 通常被弾、掴まれ、叩きつけ、死亡の状態遷移を管理する。
public sealed partial class PlayerController
{
    // プレイヤーの攻撃リアクション状態。
    // 通常状態から、被弾、掉まれ、叩きつけ、死亡状態へ遷移する。
    public enum PlayerReactionState
    {
        Normal,   // 通常状態（自由に移動・アクション可能）
        Damaged,  // 被弾状態（短い無敵時間）
        Grabbed,  // 敵に掉まれている状態（移動不可、拘束演出）
        Smashed,  // 敵に叩きつけられた状態（衣撃演出）
        Dead      // 死亡状態（すべての入力を受け付けない）
    }

    [Header("Reaction")]
    // Damaged 状態が継続する時間（秒）
    [Header("Damaged継続時間")]
    [Tooltip("被弾状態が継続する時間です。この間は短い無敵時間が適用されます。")]
    [SerializeField] private float damagedStateDuration = 0.15f;
    // Grabbed 状態が継続する時間（秒）。この時間終了後に即死級ダメージまたは解放。
    [Header("Grabbed継続時間")]
    [Tooltip("敵に掴まれている状態が継続する時間です。この時間後に即死または解放されます。")]
    [SerializeField] private float grabbedStateDuration = 0.5f;
    // Smashed 状態が継続する時間（秒）
    [Header("Smashed継続時間")]
    [Tooltip("敵に叩きつけられた状態が継続する時間です。")]
    [SerializeField] private float smashedStateDuration = 0.35f;
    // Smash 攻撃を受けたら即座に死亡するか（false ならダメージとノックバックのみ）
    [Header("Smash即死判定")]
    [Tooltip("Smash攻撃を受けた瞬間に即死するかどうかを設定します。")]
    [SerializeField] private bool smashIsInstantDeath = true;
    // リアクション系のデバッグログを表示するか
    [Header("デバッグログ表示")]
    [Tooltip("リアクション状態の変化をデバッグログに出力するかどうかを設定します。")]
    [SerializeField] private bool showReactionDebugLog = false;

    [Header("Grab演出")]
    // Grabbed 状態終了後に即死級ダメージを与えるか（false なら単に解放）
    [Header("拘束後即死")]
    [Tooltip("Grabbed状態の継続時間終了後に即死級ダメージを与えるかどうかを設定します。")]
    [SerializeField] private bool killAfterGrabbedDuration = true;

    // 現在のリアクション状態
    private PlayerReactionState reactionState = PlayerReactionState.Normal;
    // 現在の状態に入ってからの経過時間
    private float reactionStateTimer = 0.0f;

    // 掴まれ中に追従する先（敵の手の Transform）
    private Transform currentGrabAnchor = null;

    // 現在のリアクション状態を外部から参照するためのプロパティ
    public PlayerReactionState ReactionState => reactionState;
    // 現在掉まれているかどうか
    public bool IsGrabbed => reactionState == PlayerReactionState.Grabbed;
    // 現在叩きつけられているかどうか
    public bool IsSmashed => reactionState == PlayerReactionState.Smashed;
    // 現在死亡状態かどうか
    public bool IsDeadState => reactionState == PlayerReactionState.Dead;

    // 入力禁止に使えるプロパティ。
    // Grabbed, Smashed, Dead のいずれかの状態なら true を返す。
    public bool IsActionLocked =>
        reactionState == PlayerReactionState.Grabbed ||
        reactionState == PlayerReactionState.Smashed ||
        reactionState == PlayerReactionState.Dead;

    // リアクション状態を初期化する。
    // ゲーム開始時またはリスポーン時に呼び出す。
    private void InitializeReactionState()
    {
        reactionState = PlayerReactionState.Normal;
        reactionStateTimer = 0.0f;
        currentGrabAnchor = null;
    }

    // リアクション状態の更新処理。
    // 毎フレーム呼び出され、状態タイマーを進めて各状態の処理を実行する。
    private void UpdateReactionState(float deltaTime)
    {
        // 状態タイマーを進める
        reactionStateTimer += deltaTime;

        switch (reactionState)
        {
            case PlayerReactionState.Normal:
                // 通常状態では何もしない
                break;

            case PlayerReactionState.Damaged:
                // 被弾状態の時間が終了したら通常状態に戻る
                if (reactionStateTimer >= damagedStateDuration)
                {
                    ChangeReactionState(PlayerReactionState.Normal);
                }
                break;

            case PlayerReactionState.Grabbed:
                // 掴まれ中は手の位置に追従させる（拘束演出）
                if (currentGrabAnchor != null)
                {
                    // ターゲット位置を取得（Z座標は現在の値を維持）
                    Vector3 target = currentGrabAnchor.position;
                    target.z = transform.position.z;

                    // 滑らかに移動させる（Lerpで追従）
                    transform.position = Vector3.Lerp(
                        transform.position,
                        target,
                        deltaTime * 15.0f
                    );
                }

                // 一定時間拘束したあとに即死級ダメージを入れるまたは解放する
                if (reactionStateTimer >= grabbedStateDuration)
                {
                    if (killAfterGrabbedDuration)
                    {
                        // 拘束演出後に即死級ダメージを与える
                        TakeDamage(999, Vector3.zero, 0.0f);
                    }
                    else
                    {
                        // 拘束を解除して通常状態に戻る
                        ForceReleaseGrab();
                        ChangeReactionState(PlayerReactionState.Normal);
                    }
                }
                break;

            case PlayerReactionState.Smashed:
                if (!smashIsInstantDeath && reactionStateTimer >= smashedStateDuration)
                {
                    ChangeReactionState(PlayerReactionState.Normal);
                }
                break;

            case PlayerReactionState.Dead:
                break;
        }
    }

    // リアクション状態を変更し、タイマーをリセットする。
    // Grabbed 状態以外に遷移する場合は掉み参照をクリアする。
    private void ChangeReactionState(PlayerReactionState nextState)
    {
        reactionState = nextState;
        reactionStateTimer = 0.0f;

        // Grabbed 状態以外に遷移したら掉み参照をクリア
        if (reactionState != PlayerReactionState.Grabbed)
        {
            currentGrabAnchor = null;
        }

        LogReaction($"Reaction state changed: {reactionState}");
    }

    // 掉みを強制解放するメソッド。
    // 外部から呼び出して掉み状態をキャンセルすることができる。
    public void ForceReleaseGrab()
    {
        // 掉み参照をクリア
        currentGrabAnchor = null;

        // Grabbed 状態なら通常状態に戻す
        if (reactionState == PlayerReactionState.Grabbed)
        {
            ChangeReactionState(PlayerReactionState.Normal);
        }
    }

    // デバッグログが有効ならメッセージを出力するユーティリティメソッド。
    private void LogReaction(string message)
    {
        if (!showReactionDebugLog)
        {
            return;
        }

        Debug.Log($"[PlayerReaction] {message}");
    }
}