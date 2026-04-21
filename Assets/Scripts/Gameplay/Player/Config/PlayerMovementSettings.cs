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

    [Header("頂点判定速度しきい値")]
    [Tooltip("ジャンプ頂点付近とみなす縦速度の絶対値しきい値です。値を大きくすると頂点扱いの時間が長くなり、空中のため感が強くなります。")]
    [Min(0f)]
    [SerializeField] float apexThreshold = 2.0f;

    [Header("頂点付近の重力倍率")]
    [Tooltip("ジャンプ頂点付近で適用する重力倍率です。1より小さいほど頂点で少しためが生まれ、滞空感が強くなります。")]
    [Min(0f)]
    [SerializeField] float apexGravityMultiplier = 0.75f;

    [Header("頂点付近の横制御倍率")]
    [Tooltip("ジャンプ頂点付近で空中横制御へ掛ける倍率です。1より大きいほど頂点付近で左右の微調整がしやすくなります。")]
    [Min(0f)]
    [SerializeField] float apexHorizontalControlMultiplier = 1.25f;

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
    public float ApexThreshold => apexThreshold;
    public float ApexGravityMultiplier => apexGravityMultiplier;
    public float ApexHorizontalControlMultiplier => apexHorizontalControlMultiplier;
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

    [Header("壁捕まり継続可能時間")]
    [Tooltip("着地してから次に着地するまでに、壁へ捕まり続けられる合計時間です。0以下になると着地するまで壁捕まりできません。壁キックは可能です。")]
    [Min(0f)]
    [SerializeField] float wallGrabMaxHoldTime = 7.0f;

    [Header("壁捕まり通常時の消費量/秒")]
    [Tooltip("壁に掴まって静止しているとき、1秒あたりに消費する壁捕まりリソース量です。")]
    [Min(0f)]
    [SerializeField] float wallGrabIdleDrainPerSecond = 1.0f;

    [Header("壁捕まり上下移動時の消費量/秒")]
    [Tooltip("壁に掴まって上下移動しているとき、1秒あたりに消費する壁捕まりリソース量です。通常時より大きくします。")]
    [Min(0f)]
    [SerializeField] float wallGrabClimbDrainPerSecond = 1.8f;

    [Header("壁捕まり真上ジャンプ時の消費量")]
    [Tooltip("壁捕まり状態から真上ジャンプした瞬間に消費する壁捕まりリソース量です。")]
    [Min(0f)]
    [SerializeField] float wallGrabJumpCost = 2.5f;

    [Header("壁捕まり中の縦速度")]
    [Tooltip("壁捕まり中に維持する縦速度です。0でその場維持、負値でゆっくり下降、正値で上昇します。")]
    [SerializeField] float wallGrabVerticalSpeed = 0f;

    [Header("壁捕まり開始距離")]
    [Tooltip("壁に捕まるための最大距離です。壁からこの距離以上離れると捕まりを開始できません。")]
    [SerializeField] float wallGrabEnterDistance = 0.04f;

    [Header("壁捕まり維持距離")]
    [Tooltip("壁に捕まった状態を維持できる最大距離です。壁からこの距離以上離れると捕まり状態が解除されます。")]
    [SerializeField] float wallGrabExitDistance = 0.06f;

    [Header("壁登りの上速度")]
    [Tooltip("壁捕まり中に上入力しているときの上方向速度です。大きいほど速く登ります。")]
    [SerializeField] float wallClimbUpSpeed = 3.0f;

    [Header("壁登りの下速度")]
    [Tooltip("壁捕まり中に下入力しているときの下方向速度です。大きいほど速く降ります。")]
    [SerializeField] float wallClimbDownSpeed = 2.5f;

    [Header("壁登り入力しきい値")]
    [Tooltip("壁登り中に入力として認識する最小値です。小さすぎると誤判定しやすく、大きすぎると意図した操作が出にくくなります。")]
    [SerializeField] float wallClimbInputThreshold = 0.1f;

    [Header("壁捕まりジャンプ上速度")]
    [Tooltip("壁捕まり中に真上へジャンプするときの上方向速度です。通常ジャンプや壁キックとは別に調整します。")]
    [SerializeField] float wallGrabJumpVerticalVelocity = 12.5f;

    [Header("壁捕まりジャンプ後横入力ロック時間")]
    [Tooltip("壁捕まりジャンプ直後に横方向の移動入力を無効化する時間です。短すぎると壁から離れにくく、長すぎると操作不能感が出ます。")]
    [SerializeField] float wallGrabJumpHorizontalLockTime = 0.08f;

    [Header("壁捕まりジャンプ後再付着ロック時間")]
    [Tooltip("壁捕まりジャンプ直後に再び壁へ捕まるのを防ぐ時間です。短すぎると壁に吸い戻されやすく、長すぎると操作不能感が出ます。")]
    [Min(0f)]
    [SerializeField] float wallGrabJumpReattachLockTime = 0.18f;

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

    [Header("壁離脱後ジャンプ猶予時間")]
    [Tooltip("壁から離れた直後でも壁キックを受け付ける猶予時間です。短いほどシビアで、長いほど取りこぼし軽減になります。")]
    [Min(0f)]
    [SerializeField] float wallDetachGraceTime = 0.08f;

    [Header("崖乗り上げを使う")]
    [Tooltip("壁捕まり中に上方向へ移動して崖の頂上に達したとき、自動的に崖の上に乗り上げる機能を有効にします。")]
    [SerializeField] bool useLedgeClimb = true;

    [Header("崖検出前方距離")]
    [Tooltip("崖の頂上を検出するための前方チェック距離です。")]
    [Min(0f)]
    [SerializeField] float ledgeDetectForwardDistance = 0.4f;

    [Header("崖検出上方距離")]
    [Tooltip("頭上に障害物がないかチェックする距離です。")]
    [Min(0f)]
    [SerializeField] float ledgeDetectUpDistance = 0.6f;

    [Header("崖上地面検出距離")]
    [Tooltip("崖の上に立てる地面があるか検出する距離です。")]
    [Min(0f)]
    [SerializeField] float ledgeGroundCheckDistance = 0.5f;

    [Header("崖乗り上げ時間")]
    [Tooltip("崖に乗り上げるアニメーション時間です。短いほど素早く、長いほど滑らかになります。")]
    [Min(0.01f)]
    [SerializeField] float ledgeClimbDuration = 0.35f;

    [Header("崖乗り上げ前方オフセット")]
    [Tooltip("崖に乗り上げた後の前方移動距離です。")]
    [Min(0f)]
    [SerializeField] float ledgeClimbForwardOffset = 0.8f;

    [Header("崖乗り上げ上方オフセット")]
    [Tooltip("崖に乗り上げた後の上方移動距離です。")]
    [Min(0f)]
    [SerializeField] float ledgeClimbUpOffset = 0.3f;

    public bool UseWallSlide => useWallSlide;
    public float WallSlideMaxSpeed => wallSlideMaxSpeed;
    public bool UseWallGrab => useWallGrab;
    public float WallGrabMaxHoldTime => wallGrabMaxHoldTime;
    public float WallGrabIdleDrainPerSecond => wallGrabIdleDrainPerSecond;
    public float WallGrabClimbDrainPerSecond => wallGrabClimbDrainPerSecond;
    public float WallGrabJumpCost => wallGrabJumpCost;
    public float WallGrabVerticalSpeed => wallGrabVerticalSpeed;
    public float WallGrabEnterDistance => wallGrabEnterDistance;
    public float WallGrabExitDistance => wallGrabExitDistance;
    public float WallClimbUpSpeed => wallClimbUpSpeed;
    public float WallClimbDownSpeed => wallClimbDownSpeed;
    public float WallClimbInputThreshold => wallClimbInputThreshold;
    public float WallGrabJumpVerticalVelocity => wallGrabJumpVerticalVelocity;
    public float WallGrabJumpHorizontalLockTime => wallGrabJumpHorizontalLockTime;

    public float WallGrabJumpReattachLockTime => wallGrabJumpReattachLockTime;
    
    public bool UseWallKick => useWallKick;
    public float WallJumpHorizontalVelocity => wallJumpHorizontalVelocity;
    public float WallJumpVerticalVelocity => wallJumpVerticalVelocity;
    public float WallJumpControlLockTime => wallJumpControlLockTime;
    public float WallReattachLockTime => wallReattachLockTime;
    public float WallDetachGraceTime => wallDetachGraceTime;
    public bool UseLedgeClimb => useLedgeClimb;
    public float LedgeDetectForwardDistance => ledgeDetectForwardDistance;
    public float LedgeDetectUpDistance => ledgeDetectUpDistance;
    public float LedgeGroundCheckDistance => ledgeGroundCheckDistance;
    public float LedgeClimbDuration => ledgeClimbDuration;
    public float LedgeClimbForwardOffset => ledgeClimbForwardOffset;
    public float LedgeClimbUpOffset => ledgeClimbUpOffset;
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

    [Header("上ダッシュ終了時の最大上向き速度")]
    [Tooltip("上方向または斜め上方向のダッシュ終了時に許可する上向き速度の上限です。値を小さくすると終端の伸びが抑えられ、値を大きくすると上方向へ抜けやすくなります。")]
    [Min(0f)]
    [SerializeField] float upwardDashEndVerticalSpeedClamp = 7.0f;

    [Header("ダッシュ終了後のジャンプカット無効時間")]
    [Tooltip("ダッシュ終了直後に可変ジャンプの早離しカットを無効化する時間です。値を大きくすると上ダッシュ終端の伸びが安定しやすくなりますが、長すぎると通常ジャンプ復帰が鈍く感じられます。")]
    [Min(0f)]
    [SerializeField] float dashEndJumpCutLockTime = 0.05f;

    [Header("ダッシュ角補正を使う")]
    [Tooltip("ダッシュ中に壁角へ引っかったとき、少しだけ上へずらして通しやすくする補正を有効にします。")]
    [SerializeField] bool useDashCornerCorrection = true;

    [Header("ダッシュ角補正の上移動距離")]
    [Tooltip("ダッシュ角補正が発動したときに上方向へずらす距離です。大きいほど角を越えやすくなりますが、補正感も強くなります。")]
    [Min(0f)]
    [SerializeField] float dashCornerCorrectionUpDistance = 0.12f;

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
    [Tooltip("有効にすると、ゲームパッドのダッシュ方向入力を8方向へ丸めます。")]
    [SerializeField] bool useEightWayInput = true;

    [Header("ダッシュ方向入力のデッドゾーン")]
    [Tooltip("ダッシュ方向としてゲームパッド入力を受け付ける最小値です。小さいほど弱い倒しでも方向入力として扱います。")]
    [Range(0f, 1f)]
    [SerializeField] float directionDeadZone = 0.15f;

    [Header("ダッシュ斜め入力補助角度")]
    [Tooltip("8方向スナップ時に斜め方向を少し選びやすくする補助角度です。大きいほど斜めが出しやすくなります。")]
    [Range(0f, 22.5f)]
    [SerializeField] float diagonalAssistAngle = 8.0f;

    public bool UseDash => useDash;
    public float Speed => speed;
    public float Duration => duration;
    public float GravityMultiplier => gravityMultiplier;
    public bool RestoreStartVerticalVelocity => restoreStartVerticalVelocity;
    public float UpwardDashEndVerticalSpeedClamp => upwardDashEndVerticalSpeedClamp;
    public float DashEndJumpCutLockTime => dashEndJumpCutLockTime;
    public bool UseDashCornerCorrection => useDashCornerCorrection;
    public float DashCornerCorrectionUpDistance => dashCornerCorrectionUpDistance;
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

    [Header("角補正を使う")]
    [Tooltip("上昇中に天井角へ引っかかったとき、横へ少しずらして通しやすくする補正を有効にします。")]
    [SerializeField] bool useCornerCorrection = true;

    [Header("角補正の横移動距離")]
    [Tooltip("角補正が発動したときに、横方向へずらす最大距離です。大きいほど引っかかりは減りますが、補正感も強くなります。")]
    [Min(0f)]
    [SerializeField] float cornerCorrectionDistance = 0.10f;

    [Header("角補正の上方向チェック距離")]
    [Tooltip("頭付近の角へ引っかかっているか確認する上方向のチェック距離です。小さすぎると補正が発動しづらく、大きすぎると意図しない補正が出やすくなります。")]
    [Min(0f)]
    [SerializeField] float cornerCorrectionUpCheckDistance = 0.18f;

    public float MoveInputGamepadDeadZone => moveInputGamepadDeadZone;
    public bool UseCornerCorrection => useCornerCorrection;
    public float CornerCorrectionDistance => cornerCorrectionDistance;
    public float CornerCorrectionUpCheckDistance => cornerCorrectionUpCheckDistance;
}