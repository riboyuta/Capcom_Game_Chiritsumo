public sealed partial class PlayerController
{
    // =====================================================================
    // 死亡遷移定義
    // =====================================================================

    // 死亡開始時に記録する最小限の死因。
    // 将来、奈落死・ギミック死・特殊演出分岐を増やすときの識別キーとして使う。
    public enum DeathCause
    {
        Damage,
        Hazard
    }

    // =====================================================================
    // 実行時状態
    // =====================================================================

    // 死亡シーケンス開始済みかどうか。
    // true の間は追加の死亡開始要求を無視し、二重発火を防ぐ。
    private bool isDeathSequencePlaying = false;

    // 最後に受理した死因。
    // デバッグ確認と、将来の死因別演出分岐の参照元として保持する。
    private DeathCause lastDeathCause = DeathCause.Damage;

    // =====================================================================
    // 死亡開始入口
    // =====================================================================

    // 死亡シーケンス開始要求の統一入口。
    // 初回の要求だけを受理し、死因を保存したうえで Dead 状態への遷移を開始する。
    // すでに死亡開始済みなら何もせず戻り、二重死亡開始を防ぐ。
    private void RequestDeathStart(DeathCause cause)
    {
        if (isDeathSequencePlaying)
        {
            LogHealth("Death request ignored: already processing");
            return;
        }

        isDeathSequencePlaying = true;
        lastDeathCause = cause;

        LogHealth($"Death requested: {cause}");

        // まだ Dead に入っていないときだけ状態遷移を要求する。
        // ここで二重遷移を防ぎ、死亡開始入口を 1 箇所に寄せる。
        if (reactionState != PlayerReactionState.Dead)
        {
            ChangeReactionState(PlayerReactionState.Dead);
            LogHealth("Death state entered");
        }
    }
}