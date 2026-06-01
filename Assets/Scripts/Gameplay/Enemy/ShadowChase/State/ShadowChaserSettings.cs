using System;
using UnityEngine;

// ShadowChaser 全体のデフォルト調整値。
// 参照情報ではなく、RoomEnemySystem から管理したい調整パラメータだけを持つ。
[Serializable]
public sealed class ShadowChaserSettings
{
    [Header("追尾")]
    [Tooltip("何秒前のプレイヤー位置を追うかです。")]
    [Min(0f)] public float delayTime = 0.4f;

    [Tooltip("履歴の前後 2 点を補間して滑らかにするかです。")]
    public bool useInterpolation = true;

    [Tooltip("目標位置から大きく離れた時に即座に補正する距離です。")]
    [Min(0f)] public float snapDistance = 0.3f;

    [Tooltip("通常追尾中も目標位置へ滑らかに寄せるかです。")]
    public bool smoothFollow = true;

    [Tooltip("通常追尾中の吸い付き強さです。大きいほど素早く追従します。")]
    [Min(0.01f)] public float followSmoothSharpness = 40.0f;

    [Header("開始設定")]
    [Tooltip("シーン開始時に自動起動するかです。")]
    public bool isActiveOnStart = false;

    [Header("スポーンシーケンス")]
    [Tooltip("スポーンシーケンスを使うかです。false なら即座に追尾開始します。")]
    public bool useSpawnSequence = true;

    [Tooltip("起動から出現演出開始までの待機時間です。")]
    [Min(0f)] public float spawnDelay = 0.2f;

    [Tooltip("出現演出の長さです。")]
    [Min(0.001f)] public float spawnDuration = 0.35f;

    [Tooltip("出現演出の開始位置オフセットです。")]
    public Vector3 spawnOffset = new Vector3(0f, -1f, 0f);

    [Tooltip("出現演出開始時のスケールです。")]
    public Vector3 spawnStartScale = new Vector3(0.6f, 0.6f, 1f);

    [Tooltip("待機中は Renderer を非表示にするかです。")]
    public bool hideDuringSpawnDelay = true;

    [Tooltip("出現演出完了後に追尾を開始するかです。")]
    public bool startFollowAfterSpawn = true;

    [Header("CatchUp")]
    [Tooltip("スポーン後、履歴レールへ滑らかに合流する処理を使うかです。")]
    public bool useCatchUp = true;

    [Tooltip("CatchUp にかける最短時間です。")]
    [Min(0.001f)] public float catchUpDuration = 0.2f;

    [Tooltip("CatchUp 中に現在位置から目標位置へ吸い付く強さです。")]
    [Min(0.01f)] public float catchUpFollowSharpness = 12.0f;

    [Tooltip("CatchUp 完了とみなす位置差です。")]
    [Min(0f)] public float catchUpCompleteDistance = 0.08f;

    [Tooltip("CatchUp の進行カーブです。")]
    public AnimationCurve catchUpCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("接触死")]
    [Tooltip("この半径内にプレイヤー中心が入ったら即死扱いにします。")]
    [Min(0f)] public float contactRadius = 0.4f;

    [Header("向き反転")]
    [Tooltip("snapshot の facing に応じて左右反転するかです。")]
    public bool applyFacingToVisual = true;

    [Header("Gizmo 表示")]
    [Tooltip("参照中の目標位置を Gizmo で表示します。")]
    public bool showTargetGizmo = true;

    [Tooltip("目標位置の Gizmo 色です。")]
    public Color targetGizmoColor = Color.cyan;

    [Tooltip("現在の要求スポーン位置を Gizmo で表示します。")]
    public bool showRequestedSpawnGizmo = true;

    [Tooltip("要求スポーン位置の Gizmo 色です。")]
    public Color requestedSpawnGizmoColor = Color.yellow;

    [Tooltip("CatchUp 中の現在目標位置を Gizmo で表示します。")]
    public bool showCatchUpTargetGizmo = true;

    [Tooltip("CatchUp 目標位置の Gizmo 色です。")]
    public Color catchUpTargetGizmoColor = Color.green;

    public static ShadowChaserSettings Default => new ShadowChaserSettings();
}