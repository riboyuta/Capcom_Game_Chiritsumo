// 1 物理 Tick の最終挙動を誰が主導するかを表す。
internal enum PlayerAuthority
{
    // 掴まれ・叩きつけ・死亡などで通常移動しない。
    ActionLocked,

    // ノックバック専用挙動が主導する。
    Knockback,

    // レールやイベント拘束など外部制御が主導する。
    ExternalControl,

    // 通常移動システムが主導する。
    SelfLocomotion,
}

// 1 物理 Tick の主導権を決定する専用ユーティリティ。
internal static class PlayerAuthorityResolver
{
    // 現在 Tick の主導権を優先順位に従って解決する。
    internal static PlayerAuthority Resolve(
        bool isActionLocked,
        bool isKnockback,
        bool isExternallyControlled,
        bool isGrinding)
    {
        if (isActionLocked)
        {
            return PlayerAuthority.ActionLocked;
        }

        if (isKnockback)
        {
            return PlayerAuthority.Knockback;
        }

        // 現状は Grind を暫定的に ExternalControl 扱いにする。
        if (isExternallyControlled || isGrinding)
        {
            return PlayerAuthority.ExternalControl;
        }

        return PlayerAuthority.SelfLocomotion;
    }
}