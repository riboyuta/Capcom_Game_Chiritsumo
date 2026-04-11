using System;
using UnityEngine;

public interface IHandAttack
{
    // 攻撃が完了したかどうか
    bool IsFinished { get; }

    // 攻撃を強制的にキャンセルする
    void Cancel();
}
