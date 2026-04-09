using System;
using UnityEngine;

// PlayerController 内部専用の外部制御受け皿システム。
// ギミック固有ロジックは持たず、要求の保持と最小反映だけを担当する。
internal sealed class PlayerExternalControlSystem
{
    // プレイヤー実体の Transform 参照。
    private readonly Transform playerTransform;

    // プレイヤー実体の Rigidbody 参照。
    private readonly Rigidbody playerRigidbody;

    // プレイヤーの継続実行時状態参照。
    private readonly PlayerRuntimeState runtimeState;

    // 行動不能状態を問い合わせるコールバック。
    private readonly Func<bool> isActionLockedProvider;

    // ノックバック状態を問い合わせるコールバック。
    private readonly Func<bool> isKnockbackProvider;

    // 外部制御中かどうか。
    private bool isExternallyControlled;

    // 現在の外部制御モード。
    private ExternalControlMode currentExternalControlMode;

    // 現在の制御 owner。
    private UnityEngine.Object currentOwner;
    // 外部制御開始時に確定する継続入力ブロック。
    private PlayerController.InputBlockFlags persistentInputBlockFlags;

    // そのフレームだけ有効な向き要求。
    private FacingRequest facingRequest;

    // そのフレームだけ有効なワープ要求。
    private WarpRequest warpRequest;

    // そのフレームだけ有効なアンカー姿勢要求。
    private AnchorPoseRequest anchorPoseRequest;

    // そのフレームだけ有効な経路姿勢要求。
    private PathPoseRequest pathPoseRequest;

    // 直近に開始された外部制御の物理方針。
    private ExternalPhysicsPolicy currentPhysicsPolicy;

    // 直近に開始された外部制御の重力方針。
    private ExternalGravityPolicy currentGravityPolicy;

    // 直近に開始された外部制御の見た目方針。
    private ExternalVisualPolicy currentVisualPolicy;

    // 外部セッションに公開する backend 実体。
    private readonly SessionBackend sessionBackend;

    // 現在、外部制御中かを返す。
    public bool IsExternallyControlled => isExternallyControlled;

    // 現在の外部制御モードを返す。
    public ExternalControlMode CurrentExternalControlMode => currentExternalControlMode;

    // 開始時に保持した継続入力ブロックを返す。
    public PlayerController.InputBlockFlags PersistentInputBlockFlags => persistentInputBlockFlags;

    // 外部制御受け皿を構築する。
    public PlayerExternalControlSystem(
        Transform playerTransform,
        Rigidbody playerRigidbody,
        PlayerRuntimeState runtimeState,
        Func<bool> isActionLockedProvider,
        Func<bool> isKnockbackProvider)
    {
        // Transform 参照を保持する。
        this.playerTransform = playerTransform;

        // Rigidbody 参照を保持する。
        this.playerRigidbody = playerRigidbody;

        // RuntimeState 参照を保持する。
        this.runtimeState = runtimeState;

        // ActionLocked 判定プロバイダを保持する。
        this.isActionLockedProvider = isActionLockedProvider;

        // Knockback 判定プロバイダを保持する。
        this.isKnockbackProvider = isKnockbackProvider;

        // セッション backend を初期化する。
        sessionBackend = new SessionBackend(this);

        // 内部状態を初期化する。
        EndControl();
    }

    // 指定要求を受け入れ可能かを判定する。
    public bool CanAcceptExternalControl(in PlayerExternalControlRequest request)
    {
        // owner 未指定は安全側で拒否する。
        if (request.Owner == null)
        {
            return false;
        }

        // 行動不能中は受け付けない。
        if (isActionLockedProvider != null && isActionLockedProvider())
        {
            return false;
        }

        // ノックバック中は受け付けない。
        if (isKnockbackProvider != null && isKnockbackProvider())
        {
            return false;
        }

        // すでに外部制御中なら安全側で再開始を受け付けない。
        if (isExternallyControlled)
        {
            return false;
        }

        // ここまで到達したら受理候補とみなす。
        return true;
    }

    // 外部制御開始を試み、成功時にセッションを返す。
    public bool TryBeginExternalControl(
        in PlayerExternalControlRequest request,
        out PlayerExternalControlSession session)
    {
        // 受理不可なら無効セッションを返す。
        if (!CanAcceptExternalControl(request))
        {
            session = PlayerExternalControlSession.Invalid;
            return false;
        }

        // 外部制御中フラグを立てる。
        isExternallyControlled = true;

        // 現在モードを保持する。
        currentExternalControlMode = request.Mode;

        // 現在 owner を保持する。
        currentOwner = request.Owner;

        // 継続入力ブロックを保持する。
        persistentInputBlockFlags = request.InputBlockFlags;

        // 将来拡張用に各 policy を保持する。
        currentPhysicsPolicy = request.PhysicsPolicy;
        currentGravityPolicy = request.GravityPolicy;
        currentVisualPolicy = request.VisualPolicy;

        // セッションを返す。
        session = new PlayerExternalControlSession(sessionBackend);
        return true;
    }

    // このフレームだけ向き要求を登録する。
    public void RequestFacingThisFrame(int facing)
    {
        // 0 は無効値なので要求を積まない。
        if (facing == 0)
        {
            return;
        }

        // 向き要求を保持する。
        facingRequest.hasRequest = true;
        facingRequest.facing = facing;
    }

    // このフレームだけアンカー姿勢要求を登録する。
    public void RequestAnchorPoseThisFrame(Vector3 position, Quaternion rotation)
    {
        // アンカー姿勢要求を保持する。
        anchorPoseRequest.hasRequest = true;
        anchorPoseRequest.position = position;
        anchorPoseRequest.rotation = rotation;
    }

    // このフレームだけ経路姿勢要求を登録する。
    public void RequestPathPoseThisFrame(Vector3 position, Quaternion rotation)
    {
        // 経路姿勢要求を保持する。
        pathPoseRequest.hasRequest = true;
        pathPoseRequest.position = position;
        pathPoseRequest.rotation = rotation;
    }

    // ワープ要求を登録する。
    public void RequestWarp(Vector3 targetPosition, WarpOptions options)
    {
        // ワープ要求を保持する。
        warpRequest.hasRequest = true;
        warpRequest.position = targetPosition;
        warpRequest.options = options;
    }

    // 1フレーム要求を初期化する。
    public void ResetPerFrameRequests()
    {
        // 向き要求を消す。
        facingRequest = default;

        // ワープ要求を消す。
        warpRequest = default;

        // アンカー姿勢要求を消す。
        anchorPoseRequest = default;

        // 経路姿勢要求を消す。
        pathPoseRequest = default;
    }

    // 解決済みの外部制御要求を実体へ反映する。
    public void ApplyResolvedControl()
    {
        // アンカー姿勢を先に反映する。
        if (anchorPoseRequest.hasRequest)
        {
            ApplyPose(anchorPoseRequest.position, anchorPoseRequest.rotation);
        }

        // 経路姿勢を反映する。
        if (pathPoseRequest.hasRequest)
        {
            ApplyPose(pathPoseRequest.position, pathPoseRequest.rotation);
        }

        // ワープ要求を反映する。
        if (warpRequest.hasRequest)
        {
            ApplyWarp(warpRequest.position, warpRequest.options);
        }

        // 向き要求を反映する。
        if (facingRequest.hasRequest)
        {
            ApplyFacing(facingRequest.facing);
        }

        // 反映後は 1 フレーム要求を消す。
        ResetPerFrameRequests();
    }

    // 外部制御状態を終了し、内部状態を初期化する。
    public void EndControl()
    {
        // 外部制御中フラグを下ろす。
        isExternallyControlled = false;

        // モードを既定値へ戻す。
        currentExternalControlMode = ExternalControlMode.None;

        // owner を解除する。
        currentOwner = null;

        // 継続入力ブロックを解除する。
        persistentInputBlockFlags = PlayerController.InputBlockFlags.None;

        // policy を既定値へ戻す。
        currentPhysicsPolicy = ExternalPhysicsPolicy.Keep;
        currentGravityPolicy = ExternalGravityPolicy.Keep;
        currentVisualPolicy = ExternalVisualPolicy.Keep;

        // 1フレーム要求を消す。
        ResetPerFrameRequests();
    }

    // 向き更新を反映する。
    private void ApplyFacing(int facing)
    {
        // 0 は無効なので無視する。
        if (facing == 0)
        {
            return;
        }

        // 右左の符号へ正規化する。
        runtimeState.facing = facing > 0 ? 1 : -1;
    }

    // 姿勢更新を反映する。
    private void ApplyPose(Vector3 position, Quaternion rotation)
    {
        // Rigidbody がある場合は物理 API で位置と向きを反映する。
        if (playerRigidbody != null)
        {
            playerRigidbody.MovePosition(position);
            playerRigidbody.MoveRotation(rotation);
            return;
        }

        // Rigidbody がない場合は Transform へ直接反映する。
        if (playerTransform != null)
        {
            playerTransform.SetPositionAndRotation(position, rotation);
        }
    }

    // ワープを反映する。
    private void ApplyWarp(Vector3 targetPosition, WarpOptions options)
    {
        // Rigidbody がある場合は position を直接更新する。
        if (playerRigidbody != null)
        {
            playerRigidbody.position = targetPosition;

            // 指定時のみ速度をクリアする。
            if (options.ClearVelocity)
            {
                playerRigidbody.linearVelocity = Vector3.zero;
            }
        }
        else if (playerTransform != null)
        {
            // Rigidbody がない場合は Transform へ直接反映する。
            playerTransform.position = targetPosition;
        }

        // 指定時のみ向きを更新する。
        if (options.UpdateFacing)
        {
            ApplyFacing(options.Facing);
        }
    }

    // このフレームだけ有効な向き要求。
    private struct FacingRequest
    {
        // 要求が存在するか。
        public bool hasRequest;

        // 要求する向き。
        public int facing;
    }

    // このフレームだけ有効なワープ要求。
    private struct WarpRequest
    {
        // 要求が存在するか。
        public bool hasRequest;

        // 要求する座標。
        public Vector3 position;

        // ワープ補助オプション。
        public WarpOptions options;
    }

    // このフレームだけ有効なアンカー姿勢要求。
    private struct AnchorPoseRequest
    {
        // 要求が存在するか。
        public bool hasRequest;

        // 要求する座標。
        public Vector3 position;

        // 要求する回転。
        public Quaternion rotation;
    }

    // このフレームだけ有効な経路姿勢要求。
    private struct PathPoseRequest
    {
        // 要求が存在するか。
        public bool hasRequest;

        // 要求する座標。
        public Vector3 position;

        // 要求する回転。
        public Quaternion rotation;
    }

    // PlayerExternalControlSession へ公開する backend 実装。
    private sealed class SessionBackend : IPlayerExternalControlSessionBackend
    {
        // 親システム参照。
        private readonly PlayerExternalControlSystem system;

        // backend を構築する。
        public SessionBackend(PlayerExternalControlSystem system)
        {
            // 親参照を保持する。
            this.system = system;
        }

        // backend が有効かを返す。
        public bool IsValid => system != null && system.IsExternallyControlled;

        // このフレームのアンカー姿勢要求を受け付ける。
        public void RequestAnchorPoseThisFrame(Vector3 position, Quaternion rotation)
        {
            // 無効時は何もしない。
            if (!IsValid)
            {
                return;
            }

            // 親システムへ要求を中継する。
            system.RequestAnchorPoseThisFrame(position, rotation);
        }

        // このフレームの経路姿勢要求を受け付ける。
        public void RequestPathPoseThisFrame(Vector3 position, Quaternion rotation)
        {
            // 無効時は何もしない。
            if (!IsValid)
            {
                return;
            }

            // 親システムへ要求を中継する。
            system.RequestPathPoseThisFrame(position, rotation);
        }

        // このフレームの向き要求を受け付ける。
        public void RequestFacingThisFrame(int facing)
        {
            // 無効時は何もしない。
            if (!IsValid)
            {
                return;
            }

            // 親システムへ要求を中継する。
            system.RequestFacingThisFrame(facing);
        }

        // 射出要求を受け付ける。
        public void RequestLaunch(Vector3 direction, float speed, float maxFlightDistance, LayerMask collisionLayers)
        {
            // 今回は launch 完成実装を行わないため、無効時も有効時も何もしない。
        }

        // 外部制御終了を受け付ける。
        public void EndControl()
        {
            // 無効時は何もしない。
            if (!IsValid)
            {
                return;
            }

            // 親システムで終了処理を実行する。
            system.EndControl();
        }
    }
}