using UnityEngine;

public sealed partial class PlayerController
{

    // 向きを意味付きで参照する窓口。(-1:left / +1:right)
    public int Facing => facing;

    // 移動入力方向を意味付きで参照する窓口。
    public Vector2 MoveInputDirection => playerInputReader != null ? playerInputReader.Move : Vector2.zero;

    // 斜め入力かどうかの参照口。
    public bool IsMoveInputDiagonal
    {
        get
        {
            Vector2 move = MoveInputDirection;
            const float diagonalThreshold = 0.5f;
            // TODO: 8方向ダッシュ入力感改善時に、斜め判定のしきい値を入力規約と合わせて再確認する。
            return Mathf.Abs(move.x) >= diagonalThreshold && Mathf.Abs(move.y) >= diagonalThreshold;
        }
    }

    private void UpdateFacingFromMoveInput()
    {
        // 掴まれ・叩きつけ・死亡などの行動不能中は向き更新を止める。
        // 死亡中の見た目固定に対して入力側の更新影響を持ち込まないため。
        if (IsActionLocked)
        {
            return;
        }

        if (isDashing)
        {
            return;
        }
        if (isWallGrabbing)
        {
            return;
        }
        // 移動入力の X 成分を -1 から 1 の範囲に収める。
        float inputX = Mathf.Clamp(playerInputReader.Move.x, -1f, 1f);

        // わずかな入力ぶれで向きが変わらないように閾値を設ける。
        const float facingThreshold = 0.1f;

        // 右入力が閾値を超えたら右向きにする。
        if (inputX > facingThreshold)
        {
            facing = 1;
        }
        // 左入力が負の閾値を下回ったら左向きにする。
        else if (inputX < -facingThreshold)
        {
            facing = -1;
        }
    }
}