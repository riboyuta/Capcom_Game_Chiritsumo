public sealed partial class PlayerController
{
    // PlayerView が参照する見た目用の確定状態。
    // 毎 FixedUpdate の最後に 1 回だけ更新し、
    // その物理フレーム中は同じ値を読む前提にする。
    public VisualState CurrentVisualState => visualState;

    // 現在の見た目用スナップショット。
    // Controller 内部の生状態をそのまま外へ出さず、
    // View が必要な情報だけをまとめて保持する。
    private VisualState visualState;

    // その物理フレームでのみ有効な単発フラグを初期化する。
    // 例:
    // - 着地した瞬間
    // - ジャンプした瞬間
    // - 壁ジャンプした瞬間
    // - 頂点を越えた瞬間
    //
    // これらは継続状態ではなく「瞬間イベント」なので、
    // 毎物理フレームの先頭でリセットしてから必要に応じて立てる。
    private void ResetVisualOneShotFlags()
    {
        justLandedThisFrame = false;
        locomotionSystem?.ResetOneShotFlags();
        justCrossedApexThisFrame = false;
    }

    // その物理フレームの最終状態から、見た目向けスナップショットを確定する。
    //
    // 引数 previousVelocityY:
    // - この物理フレーム開始前、または更新前の Y 速度
    // - 頂点通過判定(上昇→下降へ切り替わった瞬間)に使う
    //
    // このメソッドは「移動処理が全部終わったあと」に呼ぶ前提。
    private void FinalizeVisualState(float previousVelocityY)
    {
        // Rigidbody が取れていれば、その時点の最終速度を読む。
        // 万一 null の場合は安全側で 0 扱いにする。
        float currentVelocityY = rb != null ? rb.linearVelocity.y : 0f;
        float currentVelocityX = rb != null ? rb.linearVelocity.x : 0f;
        // 死亡演出中は開始時点で固定した向きを使い、入力で見た目が反転しないようにする。
        int visualFacing = runtimeState.isDeathFacingFixed ? runtimeState.fixedDeathFacing : runtimeState.facing;
        // 非接地中に、Y速度が
        //   前フレーム/更新前: 正(上昇中)
        //   今フレーム終端  : 0以下(下降開始)
        // へ変わったら「ジャンプ頂点を越えた瞬間」とみなす。
        if (!runtimeState.isGrounded && previousVelocityY > 0f && currentVelocityY <= 0f)
        {
            justCrossedApexThisFrame = true;
        }

        // View が読みやすい形で、その物理フレームの見た目状態を 1 つの値に固める。
        visualState = new VisualState(
            runtimeState.isGrounded,
            runtimeState.isWallSliding,
            runtimeState.isDashing,
            justLandedThisFrame,
            locomotionSystem != null && locomotionSystem.JustJumpedThisFrame,
            locomotionSystem != null && locomotionSystem.JustWallJumpedThisFrame,
            justCrossedApexThisFrame,
            runtimeState.wallSide,
            visualFacing,
            currentVelocityX,
            currentVelocityY
        );
    }

    // PlayerView 用の見た目スナップショット。
    // 「Controller が持つ多数の内部状態」をそのまま公開せず、
    // View が参照してよい情報だけを固定形で渡すための値オブジェクト。
    public readonly struct VisualState
    {
        // 継続状態 -------------------------

        // 現在接地中か。
        public readonly bool isGrounded;

        // 現在壁滑り中か。
        public readonly bool isWallSliding;

        // 現在ダッシュ中か。
        public readonly bool isDashing;

        // 単発イベント状態 -----------------

        // この物理フレームで着地した瞬間か。
        public readonly bool justLanded;

        // この物理フレームで通常ジャンプした瞬間か。
        public readonly bool justJumped;

        // この物理フレームで壁ジャンプした瞬間か。
        public readonly bool justWallJumped;

        // この物理フレームでジャンプ頂点を越えた瞬間か。
        public readonly bool justCrossedApex;

        // 補助情報 -------------------------

        // 壁接触方向。
        // 例:
        // -1 = 左壁
        //  0 = 壁なし
        //  1 = 右壁
        // ※ 実際の意味は Controller 側の wallSide 定義に従う。
        public readonly int wallSide;

        // 向いている方向。
        // 例:
        // -1 = 左向き
        //  1 = 右向き
        // ※ 実際の意味は Controller 側の facing 定義に従う。
        public readonly int facing;

        // その物理フレーム終端時点の X 速度。
        public readonly float velocityX;

        // その物理フレーム終端時点の Y 速度。
        public readonly float velocityY;

        // 見た目用スナップショットを生成するコンストラクタ。
        public VisualState(
            bool isGrounded,
            bool isWallSliding,
            bool isDashing,
            bool justLanded,
            bool justJumped,
            bool justWallJumped,
            bool justCrossedApex,
            int wallSide,
            int facing,
            float velocityX,
            float velocityY)
        {
            this.isGrounded = isGrounded;
            this.isWallSliding = isWallSliding;
            this.isDashing = isDashing;
            this.justLanded = justLanded;
            this.justJumped = justJumped;
            this.justWallJumped = justWallJumped;
            this.justCrossedApex = justCrossedApex;
            this.wallSide = wallSide;
            this.facing = facing;
            this.velocityX = velocityX;
            this.velocityY = velocityY;
        }
    }
}