using System;
using UnityEngine;

[Serializable]
public sealed class PlayerMovementSettings
{
    [Header("基本移動設定")]
    [Tooltip("地上移動と空中移動の基本的な速度・加減速設定です。通常の左右移動の手触りを調整します。")]
    [SerializeField] MoveSettings move = new();

    [Header("ジャンプ設定")]
    [Tooltip("ジャンプ初速、可変ジャンプ、入力救済を含むジャンプ関連設定です。")]
    [SerializeField] JumpSettings jump = new();

    [Header("落下設定")]
    [Tooltip("下降時の重力や最大落下速度、急降下など落下挙動の設定です。")]
    [SerializeField] FallSettings fall = new();

    [Header("接地・壁判定設定")]
    [Tooltip("地面判定や壁判定の距離、半径、対象レイヤーなど判定系の設定です。")]
    [SerializeField] DetectionSettings detection = new();

    [Header("壁アクション設定")]
    [Tooltip("壁滑り、壁捕まり、壁キックなど壁接触中の行動設定です。")]
    [SerializeField] WallSettings wall = new();

    [Header("ダッシュ設定")]
    [Tooltip("ダッシュの速度、時間、残数、入力補助などダッシュ関連設定です。")]
    [SerializeField] DashSettings dash = new();

    [Header("入力補助設定")]
    [Tooltip("通常移動や方向入力のデッドゾーンなど、入力補助に関する設定です。")]
    [SerializeField] InputAssistSettings inputAssist = new();

    public MoveSettings Move => move;
    public JumpSettings Jump => jump;
    public FallSettings Fall => fall;
    public DetectionSettings Detection => detection;
    public WallSettings Wall => wall;
    public DashSettings Dash => dash;
    public InputAssistSettings InputAssist => inputAssist;
}

[Serializable]
public sealed class MoveSettings
{
    [Header("最大横移動速度")]
    [Tooltip("左右移動の最高速度です。通常移動時の到達上限を決めます。大きいほど速く移動できますが、止まりにくさや操作の粗さも出やすくなります。")]
    [Min(0f)]
    [SerializeField] float maxSpeed = 8.2f;

    [Header("地上加速度")]
    [Tooltip("地上で入力方向へ加速する強さです。大きいほど走り出しが素早くなります。")]
    [Min(0f)]
    [SerializeField] float groundAcceleration = 78f;

    [Header("地上反転加速度")]
    [Tooltip("地上で進行方向と逆入力を入れたときの反転加速度です。大きいほど切り返しが鋭くなります。")]
    [Min(0f)]
    [SerializeField] float groundTurnAcceleration = 108f;

    [Header("地上減速度")]
    [Tooltip("地上で移動入力を離したときの減速の強さです。大きいほどピタッと止まりやすくなります。")]
    [Min(0f)]
    [SerializeField] float groundDeceleration = 92f;

    [Header("空中加速度")]
    [Tooltip("空中で入力方向へ加速する強さです。空中制御の効き具合を調整します。")]
    [Min(0f)]
    [SerializeField] float airAcceleration = 28f;

    [Header("空中反転加速度")]
    [Tooltip("空中で進行方向と逆入力を入れたときの反転加速度です。大きいほど空中で向きを変えやすくなります。")]
    [Min(0f)]
    [SerializeField] float airTurnAcceleration = 40f;

    [Header("空中減速度")]
    [Tooltip("空中で移動入力を離したときの減速の強さです。大きいほど慣性が弱くなります。")]
    [Min(0f)]
    [SerializeField] float airDeceleration = 12f;

    public float MaxSpeed => maxSpeed;
    public float GroundAcceleration => groundAcceleration;
    public float GroundTurnAcceleration => groundTurnAcceleration;
    public float GroundDeceleration => groundDeceleration;
    public float AirAcceleration => airAcceleration;
    public float AirTurnAcceleration => airTurnAcceleration;
    public float AirDeceleration => airDeceleration;
}

[Serializable]
public sealed class JumpSettings
{
    [Header("ジャンプ初速")]
    [Tooltip("ジャンプ開始時に上方向へ与える速度です。大きいほど高く跳びます。")]
    [SerializeField] float jumpVelocity = 14f;

    [Header("基本重力倍率")]
    [Tooltip("全体の基本重力倍率です。上昇・下降の速さの土台になります。大きいほど全体に重い挙動になります。")]
    [Min(0f)]
    [SerializeField] float gravityScale = 3f;

    [Header("可変ジャンプを使う")]
    [Tooltip("有効にすると、ジャンプボタンの押し方でジャンプ高さを変えられます。短押しで低く、長押しで高く跳べるようにする用途です。")]
    [SerializeField] bool useVariableJump = true;

    [Header("ジャンプ長押しの最大有効時間")]
    [Tooltip("可変ジャンプ時に、長押しを上昇へ反映する最大時間です。長いほど高く跳びやすくなります。")]
    [Min(0f)]
    [SerializeField] float maxJumpHoldTime = 0.12f;

    [Header("上昇中追加重力倍率")]
    [Tooltip("ジャンプ上昇中に掛ける追加重力倍率です。1に近いほど素直な上昇になり、大きいほど頂点が早く来ます。")]
    [Min(0f)]
    [SerializeField] float riseGravityMultiplier = 1f;

    [Header("ジャンプ短押し時の減衰率")]
    [Tooltip("ジャンプボタンを早く離したとき、上昇速度をどの程度減衰させるかの倍率です。小さいほど短押し時に低く跳びます。")]
    [Range(0f, 1f)]
    [SerializeField] float jumpCutMultiplier = 0.5f;

    [Header("コヨーテタイムを使う")]
    [Tooltip("足場から離れた直後でも少しの間ジャンプできる猶予を有効にします。操作の救済用です。")]
    [SerializeField] bool useCoyoteTime = true;

    [Header("コヨーテタイム秒数")]
    [Tooltip("接地を失ったあと、ジャンプを受け付け続ける猶予時間です。長いほど優しくなります。")]
    [Min(0f)]
    [SerializeField] float coyoteTime = 0.10f;

    [Header("ジャンプバッファを使う")]
    [Tooltip("接地直前に押したジャンプ入力を少しの間保持し、着地時に自動でジャンプさせる機能を有効にします。")]
    [SerializeField] bool useJumpBuffer = true;

    [Header("ジャンプ入力保持時間")]
    [Tooltip("ジャンプバッファとして入力を保持する時間です。長いほど先行入力が通りやすくなります。")]
    [Min(0f)]
    [SerializeField] float jumpBufferTime = 0.10f;

    public float JumpVelocity => jumpVelocity;
    public float GravityScale => gravityScale;
    public bool UseVariableJump => useVariableJump;
    public float MaxJumpHoldTime => maxJumpHoldTime;
    public float RiseGravityMultiplier => riseGravityMultiplier;
    public float JumpCutMultiplier => jumpCutMultiplier;
    public bool UseCoyoteTime => useCoyoteTime;
    public float CoyoteTime => coyoteTime;
    public bool UseJumpBuffer => useJumpBuffer;
    public float JumpBufferTime => jumpBufferTime;
}

[Serializable]
public sealed class FallSettings
{
    [Header("落下時追加重力倍率")]
    [Tooltip("下降中に掛ける追加重力倍率です。大きいほど落下が速くなり、着地までのテンポが上がります。")]
    [Min(1f)]
    [SerializeField] float gravityMultiplier = 1.75f;

    [Header("最大落下速度")]
    [Tooltip("通常落下時の下方向速度の上限です。落下が速くなりすぎるのを防ぎます。")]
    [Min(0f)]
    [SerializeField] float maxSpeed = 20f;

    [Header("急降下を使う")]
    [Tooltip("下入力などで通常より速く落下する急降下機能を有効にします。")]
    [SerializeField] bool useFastFall = true;

    [Header("急降下時の落下重力倍率")]
    [Tooltip("急降下中に掛ける重力倍率です。通常落下より強くして、意図的に素早く落ちる感触を作ります。")]
    [Min(1f)]
    [SerializeField] float fastFallGravityMultiplier = 2.6f;

    [Header("急降下時の最大落下速度")]
    [Tooltip("急降下中の下方向速度の上限です。通常落下より大きく設定して、速い落下を作る用途です。")]
    [Min(0f)]
    [SerializeField] float fastFallMaxSpeed = 28f;

    public float GravityMultiplier => gravityMultiplier;
    public float MaxSpeed => maxSpeed;
    public bool UseFastFall => useFastFall;
    public float FastFallGravityMultiplier => fastFallGravityMultiplier;
    public float FastFallMaxSpeed => fastFallMaxSpeed;
}

[Serializable]
public sealed class DetectionSettings
{
    [Header("接地判定距離")]
    [Tooltip("足元への接地チェック距離です。短すぎると接地を見失いやすく、長すぎると地面に吸い付きやすくなります。")]
    [Min(0f)]
    [SerializeField] float groundCheckDistance = 0.1f;

    [Header("接地判定レイヤー")]
    [Tooltip("接地判定の対象にするレイヤーです。床や地形だけを含め、不要なオブジェクトを含めないように設定します。")]
    [SerializeField] LayerMask groundLayerMask = ~0;

    [Header("壁判定距離")]
    [Tooltip("左右方向への壁チェック距離です。短すぎると壁検出が不安定になり、長すぎると遠い壁に反応しやすくなります。")]
    [Min(0f)]
    [SerializeField] float wallCheckDistance = 0.15f;

    [Header("壁判定半径")]
    [Tooltip("壁判定に使う球やカプセルの半径相当の値です。大きいほど壁を拾いやすくなります。")]
    [Min(0f)]
    [SerializeField] float wallCheckRadius = 0.2f;

    [Header("壁入力しきい値")]
    [Tooltip("壁方向へ入力していると見なす最小入力値です。小さすぎると誤判定しやすく、大きすぎると意図した壁操作が出にくくなります。")]
    [Range(0f, 1f)]
    [SerializeField] float wallInputThreshold = 0.12f;

    public float GroundCheckDistance => groundCheckDistance;
    public LayerMask GroundLayerMask => groundLayerMask;
    public float WallCheckDistance => wallCheckDistance;
    public float WallCheckRadius => wallCheckRadius;
    public float WallInputThreshold => wallInputThreshold;
}

[Serializable]
public sealed class WallSettings
{
    [Header("壁滑りを使う")]
    [Tooltip("壁に接触したときに落下速度を抑える壁滑り機能を有効にします。")]
    [SerializeField] bool useWallSlide = true;

    [Header("壁滑り最大落下速度")]
    [Tooltip("壁滑り中の下方向速度の上限です。小さいほどゆっくり滑り落ちます。")]
    [Min(0f)]
    [SerializeField] float wallSlideMaxSpeed = 3.0f;

    [Header("壁捕まりを使う")]
    [Tooltip("空中で壁に接触中に Grab 入力を保持しているとき、壁に捕まる機能を有効にします。")]
    [SerializeField] bool useWallGrab = true;

    [Header("壁捕まり中の縦速度")]
    [Tooltip("壁捕まり中に維持する縦速度です。0でその場維持、負値でゆっくり下降、正値で上昇します。")]
    [SerializeField] float wallGrabVerticalSpeed = 0f;

    [Header("壁キックを使う")]
    [Tooltip("壁から反発して跳ぶ壁キック機能を有効にします。")]
    [SerializeField] bool useWallKick = true;

    [Header("壁キック横速度")]
    [Tooltip("壁キック時に壁から離れる横方向速度です。大きいほど壁から強く跳ね返ります。")]
    [Min(0f)]
    [SerializeField] float wallJumpHorizontalVelocity = 8.0f;

    [Header("壁キック上速度")]
    [Tooltip("壁キック時に与える上方向速度です。大きいほど高く跳ね上がります。")]
    [SerializeField] float wallJumpVerticalVelocity = 10.5f;

    [Header("壁キック後入力ロック時間")]
    [Tooltip("壁キック直後に移動入力を無効化する時間です。短すぎると壁から離れにくく、長すぎると操作不能感が出ます。")]
    [Min(0f)]
    [SerializeField] float wallJumpControlLockTime = 0.09f;

    [Header("壁キック後再付着ロック時間")]
    [Tooltip("壁キック直後に同じ壁へ再び張り付くのを防ぐ時間です。壁に吸い戻されるのを防ぐために使います。")]
    [Min(0f)]
    [SerializeField] float wallReattachLockTime = 0.12f;

    public bool UseWallSlide => useWallSlide;
    public float WallSlideMaxSpeed => wallSlideMaxSpeed;
    public bool UseWallGrab => useWallGrab;
    public float WallGrabVerticalSpeed => wallGrabVerticalSpeed;
    public bool UseWallKick => useWallKick;
    public float WallJumpHorizontalVelocity => wallJumpHorizontalVelocity;
    public float WallJumpVerticalVelocity => wallJumpVerticalVelocity;
    public float WallJumpControlLockTime => wallJumpControlLockTime;
    public float WallReattachLockTime => wallReattachLockTime;
}

[Serializable]
public sealed class DashSettings
{
    [Header("ダッシュを使う")]
    [Tooltip("前方向へ素早く移動するダッシュ機能を有効にします。")]
    [SerializeField] bool useDash = true;

    [Header("ダッシュ速度")]
    [Tooltip("ダッシュ中の移動速度です。大きいほど一気に前へ進みます。")]
    [Min(0f)]
    [SerializeField] float speed = 18f;

    [Header("ダッシュ時間")]
    [Tooltip("ダッシュ状態を維持する時間です。長いほど移動距離が伸びやすくなります。")]
    [Min(0f)]
    [SerializeField] float duration = 0.13f;

    [Header("ダッシュ中重力倍率")]
    [Tooltip("ダッシュ中に適用する重力倍率です。0に近いほど浮くような感触になり、1以上で通常に近づきます。")]
    [Min(0f)]
    [SerializeField] float gravityMultiplier = 0.00f;

    [Header("ダッシュ終了時に開始時Y速度を復元")]
    [Tooltip("有効にすると、ダッシュ開始時の縦速度を終了時に戻します。空中ダッシュ後の落下感や上昇感を保ちたいときに使います。")]
    [SerializeField] bool restoreStartVerticalVelocity = false;

    [Header("空中ダッシュを許可")]
    [Tooltip("有効にすると、空中でもダッシュを使用できます。")]
    [SerializeField] bool allowAirDash = true;

    [Header("ダッシュ最大残数")]
    [Tooltip("同時に保持できるダッシュ残数の最大値です。リソース式ダッシュの上限として使います。")]
    [Min(1)]
    [SerializeField] int maxCharges = 1;

    [Header("接地時ダッシュ回復を使う")]
    [Tooltip("有効にすると、接地中にダッシュ残数が最大未満なら最大まで回復します。")]
    [SerializeField] bool useGroundRefill = true;

    [Header("ダッシュ再入力ロック時間")]
    [Tooltip("同一入力や連続判定の暴発を防ぐための最小ロック時間です。クールダウン用途ではなく再入力抑制用途です。")]
    [Min(0f)]
    [SerializeField] float retryLockTime = 0.02f;

    [Header("地上ダッシュ連続クールタイム")]
    [Tooltip("地上からダッシュした直後、次の地上ダッシュを許可するまでの待機時間です。空中ダッシュの開始条件には使いません。")]
    [Min(0f)]
    [SerializeField] float groundCooldownTime = 0.12f;

    [Header("ダッシュ終了時の床吸着を使う")]
    [Tooltip("有効にすると、ダッシュ終了時に短距離だけ足元の地面へ寄せて、終端の浮きを抑えます。")]
    [SerializeField] bool useGroundSnap = true;

    [Header("ダッシュ終了時の床吸着距離")]
    [Tooltip("ダッシュ終了時に足元を探す最大距離です。短いほど自然で、長いほど吸い付きが強くなります。")]
    [Min(0f)]
    [SerializeField] float groundSnapDistance = 0.2f;

    [Header("ダッシュ中の方向転換を許可")]
    [Tooltip("有効にすると、ダッシュ中でも左右入力で向きや進行方向を変えられます。無効にすると開始方向を維持します。")]
    [SerializeField] bool allowTurnDuringDash = true;

    [Header("ダッシュ入力バッファを使う")]
    [Tooltip("ダッシュ入力を少しの間保持し、条件成立時に自動で発動できるようにします。先行入力の救済用です。")]
    [SerializeField] bool useDashBuffer = true;

    [Header("ダッシュ入力保持時間")]
    [Tooltip("ダッシュ入力をバッファとして保持する時間です。長いほど先行入力が通りやすくなります。")]
    [Min(0f)]
    [SerializeField] float dashBufferTime = 0.06f;

    [Header("ダッシュ中無敵")]
    [Tooltip("有効にすると、ダッシュ中を無敵扱いにする想定のフラグです。実際に無敵処理へ反映するかは呼び出し側実装に依存します。")]
    [SerializeField] bool invulnerable = false;

    [Header("ダッシュ方向入力を8方向にスナップする")]
    [Tooltip("有効にすると、ゲームパッドのダッシュ方向入力を8方向へ丸めます。セレステ寄りの入力感にしたいときに使います。")]
    [SerializeField] bool useEightWayInput = true;

    [Header("ダッシュ方向入力のデッドゾーン")]
    [Tooltip("ダッシュ方向としてゲームパッド入力を受け付ける最小値です。小さいほど弱い倒しでも方向入力として扱います。")]
    [Range(0f, 1f)]
    [SerializeField] float directionDeadZone = 0.25f;

    [Header("ダッシュ斜め入力補助角度")]
    [Tooltip("8方向スナップ時に斜め方向を少し選びやすくする補助角度です。大きいほど斜めが出しやすくなります。")]
    [Range(0f, 22.5f)]
    [SerializeField] float diagonalAssistAngle = 8.0f;

    public bool UseDash => useDash;
    public float Speed => speed;
    public float Duration => duration;
    public float GravityMultiplier => gravityMultiplier;
    public bool RestoreStartVerticalVelocity => restoreStartVerticalVelocity;
    public bool AllowAirDash => allowAirDash;
    public int MaxCharges => maxCharges;
    public bool UseGroundRefill => useGroundRefill;
    public float RetryLockTime => retryLockTime;
    public float GroundCooldownTime => groundCooldownTime;
    public bool UseGroundSnap => useGroundSnap;
    public float GroundSnapDistance => groundSnapDistance;
    public bool AllowTurnDuringDash => allowTurnDuringDash;
    public bool UseDashBuffer => useDashBuffer;
    public float DashBufferTime => dashBufferTime;
    public bool Invulnerable => invulnerable;
    public bool UseEightWayInput => useEightWayInput;
    public float DirectionDeadZone => directionDeadZone;
    public float DiagonalAssistAngle => diagonalAssistAngle;
}

[Serializable]
public sealed class InputAssistSettings
{
    [Header("移動入力のゲームパッドデッドゾーン")]
    [Tooltip("通常移動でゲームパッド入力を採用する最小値です。微小ドリフトでキーボード入力が食われるのを防ぎます。")]
    [Range(0f, 1f)]
    [SerializeField] float moveInputGamepadDeadZone = 0.20f;

    public float MoveInputGamepadDeadZone => moveInputGamepadDeadZone;
}