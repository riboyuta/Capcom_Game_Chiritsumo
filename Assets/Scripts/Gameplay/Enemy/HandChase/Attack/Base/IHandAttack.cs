using System;
using UnityEngine;

/// <summary>
/// Hand系の攻撃共通インターフェース。
/// Smash、Grab など全ての Hand 攻撃タイプが実装する。
/// </summary>
public interface IHandAttack
{
    /// <summary>
    /// 攻撃が完了したかどうか。
    /// </summary>
    bool IsFinished { get; }

    /// <summary>
    /// 攻撃を強制的にキャンセルする。
    /// </summary>
    void Cancel();
}
