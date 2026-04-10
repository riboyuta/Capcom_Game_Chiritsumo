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
    // 現在のリアクション状態を外部から参照するためのプロパティ。
    public PlayerReactionState ReactionState =>
        healthReactionSystem != null ? healthReactionSystem.ReactionState : PlayerReactionState.Normal;

    public bool IsGrabbed => healthReactionSystem != null && healthReactionSystem.IsGrabbed;
    public bool IsSmashed => healthReactionSystem != null && healthReactionSystem.IsSmashed;

    // リアクション状態を変更し、タイマーをリセットする。
    // Grabbed 状態以外に遷移する場合は掉み参照をクリアする。
    public bool IsDeadState => healthReactionSystem != null && healthReactionSystem.IsDeadState;

    // 入力禁止の公開判定。
    public bool IsActionLocked => healthReactionSystem != null && healthReactionSystem.IsActionLocked;
    // 外部から呼び出して掴み状態をキャンセルする。
    public void ForceReleaseGrab()
    {
        healthReactionSystem?.ForceReleaseGrab();
    }

    // 掉みを強制解放するメソッド。
    // 外部から呼び出して掉み状態をキャンセルすることができる。
    internal void StartGrabReaction(Transform grabAnchor)
    {
        healthReactionSystem?.StartGrab(grabAnchor);
    }

    // 外部からリアクション状態を変更する最小 bridge。
    internal void ChangeReactionState(PlayerReactionState nextState)
    {
        healthReactionSystem?.ChangeReactionState(nextState);
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