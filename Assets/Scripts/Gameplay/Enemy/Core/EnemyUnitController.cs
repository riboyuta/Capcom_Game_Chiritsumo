using System.Collections.Generic;
using UnityEngine;

// 手1本ぶんの主体クラス。
// 攻撃命令の受付、参照の保持、状態更新の入口を担当する。
public sealed partial class EnemyUnitController : MonoBehaviour
{
    public enum EnemyUnitState
    {
        Idle,
        Windup,
        Attack,
        Recovery
    }

    [Header("参照")]
    [Header("Root Transform")]
    [Tooltip("通常時の位置同期先となる Transform です。全体圧に合わせて移動します。")]
    [SerializeField] private Transform root;

    [Header("Palm Transform")]
    [Tooltip("攻撃時に先端として動かす Transform です。EnemyAttackController に渡して使用します。")]
    [SerializeField] private Transform palm;

    [Header("攻撃コントローラー")]
    [Tooltip("Grab / Smash 攻撃の実行を担当する EnemyAttackController です。")]
    [SerializeField] private EnemyAttackController attackController;

    [Header("本体ヒットボックス")]
    [Tooltip("将来の本体接触判定に使用する 3D Collider です。現段階では未使用です。")]
    [SerializeField] private Collider bodyHitbox;

    [Header("Animator")]
    [Tooltip("状態に応じた見た目更新に使用する Animator です。不要なら未設定でも動作します。")]
    [SerializeField] private Animator animator;

    [Header("設定")]
    [Header("設定データ")]
    [Tooltip("この手ユニットが参照する設定データです。状態時間や攻撃時間の取得に使用します。")]
    [SerializeField] private EnemyConfig config;

    [Header("腕セグメント描画")]
    [Header("セグメント親オブジェクト")]
    [Tooltip("腕セグメントをぶら下げる親 Transform です。通常は Root の子を設定します。")]
    [SerializeField] private Transform armSegmentsRoot;

    [Header("セグメントプレハブ")]
    [Tooltip("腕1節ぶんの見た目プレハブです。SpriteRenderer を持つオブジェクトを設定します。")]
    [SerializeField] private GameObject armSegmentPrefab;

    [Header("セグメント1節の長さ")]
    [Tooltip("腕1節ぶんの基準長さです。Root と Palm の距離から必要本数を計算するために使います。")]
    [SerializeField] private float armSegmentLength = 0.5f;

    [Header("セグメント最大数")]
    [Tooltip("生成・表示する腕セグメントの最大数です。極端な距離で増えすぎないように制限します。")]
    [SerializeField] private int maxArmSegmentCount = 16;

    [Header("Root側オフセット")]
    [Tooltip("Root 側から何メートル空けてセグメント配置を始めるかを設定します。")]
    [SerializeField] private float rootSegmentOffset = 0.0f;

    [Header("Palm側オフセット")]
    [Tooltip("Palm 側の手前で何メートル止めるかを設定します。手の見た目との重なり調整に使います。")]
    [SerializeField] private float palmSegmentOffset = 0.0f;

    // 腕セグメント用に生成したオブジェクトのリスト。プール管理に使用する。
    private readonly List<Transform> armSegmentInstances = new();

    // 現在の状態（Idle / Windup / Attack / Recovery）
    private EnemyUnitState state = EnemyUnitState.Idle;
    // 現在の状態に入ってからの経過時間
    private float stateTimer;
    // 全体圧から指定された X 座標。Root の位置同期に使用する。
    private float pressureX;
    // 予約された攻撃のターゲット位置（ワールド座標）
    private Vector3 reservedTargetWorld;
    // 予約された攻撃の種類（Grab / Smash）
    private EnemyAttackController.EnemyAttackType reservedAttackType;

    // この手ユニットが現在攻撃中または攻撃準備中かを返す
    public bool IsBusy => state != EnemyUnitState.Idle;
    // 現在の状態を外部から参照するためのプロパティ
    public EnemyUnitState CurrentState => state;

    // Root Transform への参照を外部に公開
    public Transform Root => root;
    // Palm Transform への参照を外部に公開
    public Transform Palm => palm;
    // EnemyConfig への参照を外部に公開
    public EnemyConfig Config => config;

    // 初期化処理。必要な参照が設定されているかチェックし、攻撃コントローラーに設定を渡す。
    private void Awake()
    {
        // 各参照が未設定の場合、ログを出力して問題箇所を特定しやすくする
        if (root == null)
        {
            Debug.Log("EnemyUnitController: root が未設定です。通常時の位置同期ができません。");
        }

        if (palm == null)
        {
            Debug.Log("EnemyUnitController: palm が未設定です。攻撃先端を制御できません。");
        }

        if (attackController == null)
        {
            Debug.Log("EnemyUnitController: attackController が未設定です。攻撃を開始できません。");
        }

        if (config == null)
        {
            Debug.Log("EnemyUnitController: config が未設定です。状態時間や攻撃時間を取得できません。");
        }

        if (armSegmentsRoot == null)
        {
            Debug.Log("EnemyUnitController: armSegmentsRoot が未設定です。腕セグメントを描画できません。");
        }

        if (armSegmentPrefab == null)
        {
            Debug.Log("EnemyUnitController: armSegmentPrefab が未設定です。腕セグメントを生成できません。");
        }

        // AttackController に設定データと Palm を渡す
        if (attackController != null)
        {
            attackController.SetConfig(config);
            attackController.SetPalm(palm);
        }
    }

    // 全体圧から送られてきた X 座標を保持する。
    public void SetPressureX(float pressureX)
    {
        this.pressureX = pressureX;
    }

    // Grab 攻撃を予約する。Idle 状態でなければ予約できない。
    // targetWorldPosition: 攻撃ターゲットのワールド座標
    // 戻り値: 予約に成功したら true、既に忙しい場合は false
    public bool TryStartGrabAttack(Vector3 targetWorldPosition)
    {
        // Idle 状態以外では攻撃を開始できない
        if (state != EnemyUnitState.Idle)
        {
            return false;
        }

        // 攻撃情報を予約
        reservedTargetWorld = targetWorldPosition;
        reservedAttackType = EnemyAttackController.EnemyAttackType.Grab;
        // Windup 状態に遷移して攻撃準備を開始
        ChangeState(EnemyUnitState.Windup);
        return true;
    }

    // Smash 攻撃を予約する。Idle 状態でなければ予約できない。
    // targetWorldPosition: 攻撃ターゲットのワールド座標
    // 戻り値: 予約に成功したら true、既に忙しい場合は false
    public bool TryStartSmashAttack(Vector3 targetWorldPosition)
    {
        // Idle 状態以外では攻撃を開始できない
        if (state != EnemyUnitState.Idle)
        {
            return false;
        }

        // 攻撃情報を予約
        reservedTargetWorld = targetWorldPosition;
        reservedAttackType = EnemyAttackController.EnemyAttackType.Smash;
        // Windup 状態に遷移して攻撃準備を開始
        ChangeState(EnemyUnitState.Windup);
        return true;
    }
}