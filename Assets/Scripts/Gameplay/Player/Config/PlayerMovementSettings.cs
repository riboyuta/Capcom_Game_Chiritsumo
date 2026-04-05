using System;
using UnityEngine;

[Serializable]
public sealed class PlayerMovementSettings
{
    [Header("最大横移動速度")]
    [Tooltip("左右移動の最高速度です。通常移動時の到達上限を決めます。大きいほど速く移動できますが、止まりにくさや操作の粗さも出やすくなります。")]
    [Min(0f)] public float moveMaxSpeed = 8.2f;

    [Header("地上加速度")]
    [Tooltip("地上で入力方向へ加速する強さです。大きいほど走り出しが素早くなります。")]
    [Min(0f)] public float groundAcceleration = 78f;

    [Header("地上反転加速度")]
    [Tooltip("地上で進行方向と逆入力を入れたときの反転加速度です。大きいほど切り返しが鋭くなります。")]
    [Min(0f)] public float groundTurnAcceleration = 108f;

    [Header("地上減速度")]
    [Tooltip("地上で移動入力を離したときの減速の強さです。大きいほどピタッと止まりやすくなります。")]
    [Min(0f)] public float groundDeceleration = 92f;

    [Header("空中加速度")]
    [Tooltip("空中で入力方向へ加速する強さです。空中制御の効き具合を調整します。")]
    [Min(0f)] public float airAcceleration = 28f;

    [Header("空中反転加速度")]
    [Tooltip("空中で進行方向と逆入力を入れたときの反転加速度です。大きいほど空中で向きを変えやすくなります。")]
    [Min(0f)] public float airTurnAcceleration = 40f;

    [Header("空中減速度")]
    [Tooltip("空中で移動入力を離したときの減速の強さです。大きいほど慣性が弱くなります。")]
    [Min(0f)] public float airDeceleration = 12f;

    [Header("ジャンプ初速")]
    [Tooltip("ジャンプ開始時に上方向へ与える速度です。大きいほど高く跳びます。")]
    public float jumpVelocity = 14f;

    [Header("重力倍率")]
    [Tooltip("全体の基本重力倍率です。上昇・下降の速さの土台になります。大きいほど全体に重い挙動になります。")]
    [Min(0f)] public float gravityScale = 3f;

    [Header("可変ジャンプを使う")]
    [Tooltip("有効にすると、ジャンプボタンの押し方でジャンプ高さを変えられます。短押しで低く、長押しで高く跳べるようにする用途です。")]
    public bool useVariableJump = true;

    [Header("ジャンプ長押しの最大有効時間")]
    [Tooltip("可変ジャンプ時に、長押しを上昇へ反映する最大時間です。長いほど高く跳びやすくなります。")]
    [Min(0f)] public float maxJumpHoldTime = 0.12f;

    [Header("上昇中追加重力倍率")]
    [Tooltip("ジャンプ上昇中に掛ける追加重力倍率です。1に近いほど素直な上昇になり、大きいほど頂点が早く来ます。")]
    [Min(0f)] public float riseGravityMultiplier = 1f;

    [Header("ジャンプ短押し時の減衰率")]
    [Tooltip("ジャンプボタンを早く離したとき、上昇速度をどの程度減衰させるかの倍率です。小さいほど短押し時に低く跳びます。")]
    [Range(0f, 1f)] public float jumpCutMultiplier = 0.5f;

    [Header("コヨーテタイムを使う")]
    [Tooltip("足場から離れた直後でも少しの間ジャンプできる猶予を有効にします。操作の救済用です。")]
    public bool useCoyoteTime = true;

    [Header("コヨーテタイム秒数")]
    [Tooltip("接地を失ったあと、ジャンプを受け付け続ける猶予時間です。長いほど優しくなります。")]
    [Min(0f)] public float coyoteTime = 0.10f;

    [Header("ジャンプバッファを使う")]
    [Tooltip("接地直前に押したジャンプ入力を少しの間保持し、着地時に自動でジャンプさせる機能を有効にします。")]
    public bool useJumpBuffer = true;

    [Header("ジャンプ入力保持時間")]
    [Tooltip("ジャンプバッファとして入力を保持する時間です。長いほど先行入力が通りやすくなります。")]
    [Min(0f)] public float jumpBufferTime = 0.10f;

    [Header("落下時追加重力倍率")]
    [Tooltip("下降中に掛ける追加重力倍率です。大きいほど落下が速くなり、着地までのテンポが上がります。")]
    [Min(1f)] public float fallGravityMultiplier = 1.75f;

    [Header("最大落下速度")]
    [Tooltip("通常落下時の下方向速度の上限です。落下が速くなりすぎるのを防ぎます。")]
    [Min(0f)] public float maxFallSpeed = 20f;

    [Header("急降下を使う")]
    [Tooltip("下入力などで通常より速く落下する急降下機能を有効にします。")]
    public bool useFastFall = true;

    [Header("急降下時の落下重力倍率")]
    [Tooltip("急降下中に掛ける重力倍率です。通常落下より強くして、意図的に素早く落ちる感触を作ります。")]
    [Min(1f)] public float fastFallGravityMultiplier = 2.6f;

    [Header("急降下時の最大落下速度")]
    [Tooltip("急降下中の下方向速度の上限です。通常落下より大きく設定して、速い落下を作る用途です。")]
    [Min(0f)] public float fastFallMaxSpeed = 28f;

    [Header("接地判定距離")]
    [Tooltip("足元への接地チェック距離です。短すぎると接地を見失いやすく、長すぎると地面に吸い付きやすくなります。")]
    [Min(0f)] public float groundCheckDistance = 0.1f;

    [Header("接地判定レイヤー")]
    [Tooltip("接地判定の対象にするレイヤーです。床や地形だけを含め、不要なオブジェクトを含めないように設定します。")]
    public LayerMask groundLayerMask = ~0;

    [Header("壁滑りを使う")]
    [Tooltip("壁に接触したときに落下速度を抑える壁滑り機能を有効にします。")]
    public bool useWallSlide = true;

    [Header("壁判定距離")]
    [Tooltip("左右方向への壁チェック距離です。短すぎると壁検出が不安定になり、長すぎると遠い壁に反応しやすくなります。")]
    [Min(0f)] public float wallCheckDistance = 0.15f;

    [Header("壁判定半径")]
    [Tooltip("壁判定に使う球やカプセルの半径相当の値です。大きいほど壁を拾いやすくなります。")]
    [Min(0f)] public float wallCheckRadius = 0.2f;

    [Header("壁滑り最大落下速度")]
    [Tooltip("壁滑り中の下方向速度の上限です。小さいほどゆっくり滑り落ちます。")]
    [Min(0f)] public float wallSlideMaxSpeed = 3.0f;

    [Header("壁キックを使う")]
    [Tooltip("壁から反発して跳ぶ壁キック機能を有効にします。")]
    public bool useWallKick = true;

    [Header("壁キック横速度")]
    [Tooltip("壁キック時に壁から離れる横方向速度です。大きいほど壁から強く跳ね返ります。")]
    [Min(0f)] public float wallJumpHorizontalVelocity = 8.0f;

    [Header("壁キック上速度")]
    [Tooltip("壁キック時に与える上方向速度です。大きいほど高く跳ね上がります。")]
    public float wallJumpVerticalVelocity = 10.5f;

    [Header("壁キック後入力ロック時間")]
    [Tooltip("壁キック直後に移動入力を無効化する時間です。短すぎると壁から離れにくく、長すぎると操作不能感が出ます。")]
    [Min(0f)] public float wallJumpControlLockTime = 0.09f;

    [Header("壁キック後再付着ロック時間")]
    [Tooltip("壁キック直後に同じ壁へ再び張り付くのを防ぐ時間です。壁に吸い戻されるのを防ぐために使います。")]
    [Min(0f)] public float wallReattachLockTime = 0.12f;

    [Header("壁入力しきい値")]
    [Tooltip("壁方向へ入力していると見なす最小入力値です。小さすぎると誤判定しやすく、大きすぎると意図した壁操作が出にくくなります。")]
    [Range(0f, 1f)] public float wallInputThreshold = 0.12f;

    [Header("前ステを使う")]
    [Tooltip("前方向へ素早く移動するステップ機能を有効にします。")]
    public bool useStep = true;

    [Header("前ステ速度")]
    [Tooltip("前ステップ中の移動速度です。大きいほど一気に前へ進みます。")]
    [Min(0f)] public float stepSpeed = 18f;

    [Header("前ステ時間")]
    [Tooltip("前ステップ状態を維持する時間です。長いほど移動距離が伸びやすくなります。")]
    [Min(0f)] public float stepDuration = 0.13f;

    [Header("前ステ中重力倍率")]
    [Tooltip("前ステップ中に適用する重力倍率です。0に近いほど浮くような感触になり、1以上で通常に近づきます。")]
    [Min(0f)] public float stepGravityMultiplier = 0.00f;

    [Header("前ステ終了時に開始時Y速度を復元")]
    [Tooltip("有効にすると、前ステップ開始時の縦速度を終了時に戻します。空中前ステ後の落下感や上昇感を保ちたいときに使います。")]
    public bool restoreStepStartVerticalVelocity = false;

    [Header("前ステクールダウン")]
    [Tooltip("前ステップを再使用できるまでの待ち時間です。短いほど連発しやすくなります。")]
    [Min(0f)] public float stepCooldown = 0.58f;

    [Header("空中前ステを許可")]
    [Tooltip("有効にすると、空中でも前ステップを使用できます。")]
    public bool allowAirStep = true;

    [Header("前ステ中の方向転換を許可")]
    [Tooltip("有効にすると、前ステップ中でも左右入力で向きや進行方向を変えられます。無効にすると開始方向を維持します。")]
    public bool allowTurnDuringStep = true;

    [Header("前ステ入力バッファを使う")]
    [Tooltip("前ステップ入力を少しの間保持し、条件成立時に自動で発動できるようにします。先行入力の救済用です。")]
    public bool useStepBuffer = true;

    [Header("前ステ入力保持時間")]
    [Tooltip("前ステップ入力をバッファとして保持する時間です。長いほど先行入力が通りやすくなります。")]
    [Min(0f)] public float stepBufferTime = 0.06f;

    [Header("前ステ中無敵")]
    [Tooltip("有効にすると、前ステップ中を無敵扱いにする想定のフラグです。実際に無敵処理へ反映するかは呼び出し側実装に依存します。")]
    public bool stepInvulnerable = false;

    [Header("レール滑走速度")]
    [Tooltip("レール上を滑る速度です。大きいほど速くレール上を移動します。")]
    [Min(0f)] public float grindSpeed = 15f;

    [Header("レールジャンプ上速度")]
    [Tooltip("レールからジャンプ離脱する際の上方向の初速です。")]
    public float grindJumpVerticalVelocity = 12f;

    [Header("レール再吸着ロック時間")]
    [Tooltip("レールジャンプ直後に再びレールに吸着してしまうのを防ぐための無効化時間です。")]
    [Min(0f)] public float railReattachLockTime = 0.2f;

    [Header("レール乗車制限角度")]
    [Tooltip("空中から直接レールに飛び乗る際、レールの傾きがこれ以上（垂直寄り）だと乗れずに弾かれる角度です（度）。")]
    [Range(0f, 90f)] public float maxAttachSlopeAngle = 45f;
}