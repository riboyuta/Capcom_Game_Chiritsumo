using UnityEngine;

// 責務:
// - 3D 空間で一定方向へ単純移動する
// - Player タグとの Trigger 接触を検知し、PlayerController の既存死亡入口を 1 回だけ呼ぶ
// - 腕 / 手の平の 2 レイヤー構成で簡易 Sprite アニメを再生する
//
// 非責務:
// - 敵 AI や経路探索は担当しない
// - ダメージ計算や死亡可否判定そのものは担当しない
// - Sprite 生成やアセット読込は担当しない
//
// 依存先:
// - Rigidbody: 物理ベース移動の反映先
// - Collider(Trigger): プレイヤー接触判定に使用
// - PlayerController.RequestHazardDeath(): 既存の死亡入口
// - SpriteRenderer: 腕 / 手の平の見た目反映先
//
// 前提条件:
// - この GameObject は 3D Trigger 判定が機能する構成である
// - プレイヤーは playerTag で識別できる
// - PlayerController は接触先自身または親階層から取得できる
[DisallowMultipleComponent]
public sealed class EnemySimpleHand3D : MonoBehaviour
{
    // =====================================================================
    // Inspector 設定値
    // =====================================================================

    [Header("移動: 速度")]
    [Tooltip("前進移動の速度です。FixedUpdate で moveAxis と掛け合わせて Rigidbody.MovePosition に使います。値を大きくすると速く進み、小さくするとゆっくり進みます。0 にすると停止します。")]
    [SerializeField] private float moveSpeed = 2.0f;

    [Header("移動: 方向軸")]
    [Tooltip("移動方向を表すワールド基準ベクトルです。FixedUpdate の移動方向として使います。OnValidate で正規化されるため、方向だけが意味を持ちます。ゼロベクトルなら移動しません。")]
    [SerializeField] private Vector3 moveAxis = Vector3.right;

    [Header("判定: プレイヤータグ")]
    [Tooltip("接触相手をプレイヤーとして扱うタグ名です。OnTriggerEnter で CompareTag に使います。タグを変えると死亡要求を送る対象が変わります。")]
    [SerializeField] private string playerTag = "Player";

    [Header("判定: ヒット後に無効化")]
    [Tooltip("プレイヤーへ死亡要求が受理された後、この敵を無効化するかどうかです。有効にすると 1 回ヒット後に DisableSelf で非表示かつ非アクティブになり、無効だと残り続けます。")]
    [SerializeField] private bool disableAfterHit = true;

    [Header("見た目: ルート")]
    [Tooltip("見た目階層の基準 Transform です。腕 / 手の平の SpriteRenderer 自動補完に使います。未設定時は Reset / Awake / OnValidate で自分自身を設定します。")]
    [SerializeField] private Transform visualRoot;

    [Header("見た目: 腕 Renderer")]
    [Tooltip("腕 Sprite の表示先です。UpdateSimpleAnimation で armFrames の現在フレームを反映します。未設定時は visualRoot 配下の 'ArmRenderer' を自動探索します。")]
    [SerializeField] private SpriteRenderer armRenderer;

    [Header("見た目: 手の平 Renderer")]
    [Tooltip("手の平 Sprite の表示先です。UpdateSimpleAnimation で palmFrames の現在フレームを反映します。未設定時は visualRoot 配下の 'PalmRenderer' を自動探索します。")]
    [SerializeField] private SpriteRenderer palmRenderer;

    [Header("見た目: 手の平ローカルオフセット")]
    [Tooltip("手の平 Renderer のローカル位置補正です。Update で毎フレーム palmRenderer.transform.localPosition に反映します。値を変えると手の平 Sprite の見た目配置が変わります。")]
    [SerializeField] private Vector3 palmLocalOffset = Vector3.zero;

    [Header("アニメ: 腕フレーム列")]
    [Tooltip("腕 Sprite アニメに使うフレーム列です。UpdateSimpleAnimation で animationFps に応じてループ再生します。配列が空だと腕 Sprite は更新されません。")]
    [SerializeField] private Sprite[] armFrames;

    [Header("アニメ: 手の平フレーム列")]
    [Tooltip("手の平 Sprite アニメに使うフレーム列です。UpdateSimpleAnimation で animationFps に応じてループ再生します。配列が空だと手の平 Sprite は更新されません。")]
    [SerializeField] private Sprite[] palmFrames;

    [Header("アニメ: 再生FPS")]
    [Tooltip("簡易 Sprite アニメの再生速度です。UpdateSimpleAnimation で経過時間からフレーム番号を計算するのに使います。大きくすると速く切り替わり、小さくすると遅くなります。0 以下だと先頭フレーム固定になります。")]
    [SerializeField, Min(0f)] private float animationFps = 8.0f;

    [Header("デバッグ: 通常ログ表示")]
    [Tooltip("プレイヤーへの死亡要求が受理 / 拒否されたことを Console に出すかどうかです。ヒット時の流れ確認に使う観測用であり、ゲーム挙動そのものは変わりません。")]
    [SerializeField] private bool enableDebugLog;

    [Header("デバッグ: 参照不足ログ表示")]
    [Tooltip("Player タグの相手に PlayerController が見つからなかったとき、警告ログを出すかどうかです。接触構成ミスの確認用であり、調整用ではありません。")]
    [SerializeField] private bool logMissingReferences;

    // =====================================================================
    // 実行時状態
    // =====================================================================

    // 移動反映先の Rigidbody。
    // Reset / Awake で取得し、FixedUpdate で MovePosition に使う。
    private Rigidbody rb;

    // 自身が無効化済みかどうか。
    // true の間は移動・アニメ・接触判定を止める。
    private bool isDisabled;

    // すでにプレイヤーへヒットしたかどうか。
    // 多重接触で RequestHazardDeath を重複送信しないために使う。
    private bool hasHitPlayer;

    // 簡易 Sprite アニメの累積時間。
    // animationFps と組み合わせて現在フレーム番号を求める。
    private float animationTimer;

    // =====================================================================
    // 初期化 / 検証
    // =====================================================================

    // Inspector 追加直後や Reset 時に、見た目ルート・Rigidbody・Renderer 参照を補完する。
    private void Reset()
    {
        visualRoot = transform;
        rb = GetComponent<Rigidbody>();
        TryAutoAssignRenderers();
    }

    // 実行開始時に Rigidbody と見た目参照を補完する。
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        TryAutoAssignRenderers();
    }

    // Inspector 変更時に不正値補正と参照補完を行う。
    // moveSpeed / animationFps の下限保証と、moveAxis の正規化をここで行う。
    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        animationFps = Mathf.Max(0f, animationFps);

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        if (moveAxis.sqrMagnitude > 0f)
        {
            moveAxis = moveAxis.normalized;
        }

        TryAutoAssignRenderers();
    }

    // =====================================================================
    // 毎フレーム更新
    // =====================================================================

    // 物理更新で一定方向への単純移動を行う。
    // Rigidbody が無い、または無効化済みなら何もしない。
    private void FixedUpdate()
    {
        if (isDisabled || rb == null)
        {
            return;
        }

        Vector3 next = rb.position + moveAxis * (moveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(next);
    }

    // 見た目更新を行う。
    // 無効化済みならアニメ進行と見た目補正を止める。
    private void Update()
    {
        if (isDisabled)
        {
            return;
        }

        UpdateSimpleAnimation();
        ApplyPalmLocalOffset();
    }

    // =====================================================================
    // Trigger 判定
    // =====================================================================

    // プレイヤー接触を検知し、死亡要求を 1 回だけ送る。
    // 受理された場合だけ hasHitPlayer を立て、必要なら自身を無効化する。
    private void OnTriggerEnter(Collider other)
    {
        if (isDisabled || hasHitPlayer)
        {
            return;
        }

        if (!other.CompareTag(playerTag))
        {
            return;
        }

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null)
        {
            player = other.GetComponentInParent<PlayerController>();
        }

        if (player == null)
        {
            if (logMissingReferences)
            {
                Debug.LogWarning($"[EnemySimpleHand3D] {other.name} has tag '{playerTag}' but no PlayerController.", this);
            }
            return;
        }

        bool accepted = player.RequestHazardDeath();

        if (!accepted)
        {
            if (enableDebugLog)
            {
                Debug.Log($"[EnemySimpleHand3D] RequestHazardDeath rejected for '{other.name}'.", this);
            }
            return;
        }

        hasHitPlayer = true;

        if (enableDebugLog)
        {
            Debug.Log($"[EnemySimpleHand3D] Hit player '{other.name}', RequestHazardDeath accepted=true", this);
        }

        if (disableAfterHit)
        {
            DisableSelf();
        }
    }

    // =====================================================================
    // 無効化処理
    // =====================================================================

    // 自身を無効化する。
    // 接触後の再判定を防ぐため Collider を止め、その後 GameObject を非アクティブにする。
    private void DisableSelf()
    {
        isDisabled = true;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        gameObject.SetActive(false);
    }

    // =====================================================================
    // 簡易 Sprite アニメ
    // =====================================================================

    // 腕 / 手の平のフレームを animationFps に応じて更新する。
    // 0 FPS 以下ならアニメを止め、両方とも先頭フレーム固定にする。
    private void UpdateSimpleAnimation()
    {
        if (animationFps <= 0f)
        {
            ApplyFrame(armRenderer, armFrames, 0);
            ApplyFrame(palmRenderer, palmFrames, 0);
            return;
        }

        animationTimer += Time.deltaTime;

        int armIndex = GetLoopFrameIndex(armFrames, animationTimer, animationFps);
        int palmIndex = GetLoopFrameIndex(palmFrames, animationTimer, animationFps);

        ApplyFrame(armRenderer, armFrames, armIndex);
        ApplyFrame(palmRenderer, palmFrames, palmIndex);
    }

    // ループアニメ用の現在フレーム番号を返す。
    // 配列未設定なら -1 を返し、呼び出し側で更新しない。
    private static int GetLoopFrameIndex(Sprite[] frames, float time, float fps)
    {
        if (frames == null || frames.Length == 0)
        {
            return -1;
        }

        int index = Mathf.FloorToInt(time * fps) % frames.Length;
        if (index < 0)
        {
            index += frames.Length;
        }

        return index;
    }

    // 指定フレームを SpriteRenderer に反映する。
    // Renderer 未設定、配列未設定、添字不正なら何もしない。
    private static void ApplyFrame(SpriteRenderer target, Sprite[] frames, int index)
    {
        if (target == null || frames == null || frames.Length == 0)
        {
            return;
        }

        if (index < 0 || index >= frames.Length)
        {
            return;
        }

        target.sprite = frames[index];
    }

    // 手の平 Renderer のローカル位置補正を反映する。
    // 毎フレーム固定値を書き戻すことで、アニメや他処理で位置がずれても見た目を保つ。
    private void ApplyPalmLocalOffset()
    {
        if (palmRenderer == null)
        {
            return;
        }

        palmRenderer.transform.localPosition = palmLocalOffset;
    }

    // =====================================================================
    // 参照補完
    // =====================================================================

    // visualRoot 配下の既定名から Renderer を自動補完する。
    // 既存設定を壊さないため、未設定時だけ探索する。
    private void TryAutoAssignRenderers()
    {
        if (visualRoot == null)
        {
            return;
        }

        if (armRenderer == null)
        {
            armRenderer = visualRoot.Find("ArmRenderer")?.GetComponent<SpriteRenderer>();
        }

        if (palmRenderer == null)
        {
            palmRenderer = visualRoot.Find("PalmRenderer")?.GetComponent<SpriteRenderer>();
        }
    }
}