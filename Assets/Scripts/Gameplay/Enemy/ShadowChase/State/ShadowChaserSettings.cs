using System;
using UnityEngine;

// ShadowChaser 全体のデフォルト調整値。
// 追跡はプレイヤー履歴の遅延再生として扱い、追尾補正は持たない。
[Serializable]
public sealed class ShadowChaserSettings
{
    [Header("履歴再生")]
    [Tooltip("何秒前のプレイヤー状態を再生するかです。")]
    [Min(0f)] public float delayTime = 0.4f;

    [Tooltip("履歴の前後 2 点を補間して再生するかです。")]
    public bool useInterpolation = true;

    [Header("開始設定")]
    [Tooltip("シーン開始時に自動起動するかです。")]
    public bool isActiveOnStart = false;

    [Header("出現演出")]
    [Tooltip("出現演出の長さです。0なら即座に追跡へ移行します。")]
    [Min(0f)] public float appearDuration = 0.3f;

    [Tooltip("出現演出の開始位置オフセットです。")]
    public Vector3 appearOffset = new Vector3(0f, -1f, 0f);

    [Tooltip("出現位置を固定スポーン位置ではなく、遅延履歴上の位置に合わせるかです。")]
    public bool appearOnDelayedTrail = true;

    [Header("接触死")]
    [Tooltip("この半径内にプレイヤー中心が入ったら即死扱いにします。")]
    [Min(0f)] public float contactRadius = 0.4f;

    [Header("Gizmo 表示")]
    [Tooltip("現在参照中の履歴位置を Gizmo で表示します。")]
    public bool showTargetGizmo = true;

    [Tooltip("履歴位置の Gizmo 色です。")]
    public Color targetGizmoColor = Color.cyan;

    [Tooltip("起動時に渡されたスポーン位置を Gizmo で表示します。")]
    public bool showSpawnGizmo = true;

    [Tooltip("スポーン位置の Gizmo 色です。")]
    public Color spawnGizmoColor = Color.yellow;

    public static ShadowChaserSettings Default => new ShadowChaserSettings();
}