using UnityEngine;

public sealed partial class PlayerController
{
    private void UpdateFacingFromMoveInput()
    {
        // 前ステ中の方向転換を無効化している間は向きを固定する。
        if (isStepping && !movementSettings.allowTurnDuringStep)
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