using System;
using UnityEngine;

namespace Game.Input
{
    // 生入力(RawInputSource)をゲーム側で扱いやすい形へ変換する責務を持つ。
    // 例:
    // - キーボードとゲームパッドの移動入力を1つの Move にまとめる
    // - Jump / Dash / Grab の Pressed, Held, Released を毎フレーム更新する
    // - Move から上下左右の入力意図を導出する
    // - Dash 押下フレームにダッシュ方向入力を確定する
    public sealed class PlayerInputReader
    {
        // ゲームパッド移動入力を優先する最小しきい値。
        // 微小ドリフトでキーボード入力が食われないよう、少し強めにしている。
        private const float GamepadMoveDeadZone = 0.20f;

        // 0.5f 以上なら UpHeld、-0.5f 以下なら DownHeld と判定する。
        private const float VerticalIntentThreshold = 0.5f;

        // -0.5f 以下なら LeftHeld、0.5f 以上なら RightHeld と判定する。
        private const float HorizontalIntentThreshold = 0.5f;

        // Unity Input などから取得した生入力の供給元。
        private readonly RawInputSource rawInputSource;

        // Jump / Dash / Grab の入力割り当て情報。
        private readonly PlayerInputBindings bindings;

        // 現在の移動入力。
        // 優先順位は GamepadMoveVector → KeyboardMoveVector。
        public Vector2 Move { get; private set; }

        // Move から導出した上下入力意図。
        public bool UpHeld { get; private set; }
        public bool DownHeld { get; private set; }

        // Move から導出した左右入力意図。
        public bool LeftHeld { get; private set; }
        public bool RightHeld { get; private set; }

        // 方向入力の押下エッジ。
        public bool LeftPressed { get; private set; }
        public bool RightPressed { get; private set; }
        public bool DownPressed { get; private set; }

        // 方向入力の押下エッジ算出に使う前フレーム状態。
        private bool previousLeftHeld;
        private bool previousRightHeld;
        private bool previousDownHeld;

        // ジャンプ入力のフレーム状態。
        public bool JumpPressed { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool JumpReleased { get; private set; }

        // ダッシュ入力のフレーム状態。
        public bool DashPressed { get; private set; }
        public bool DashHeld { get; private set; }
        public bool DashReleased { get; private set; }

        // つかみ入力のフレーム状態。
        public bool GrabPressed { get; private set; }
        public bool GrabHeld { get; private set; }
        public bool GrabReleased { get; private set; }

        // ダッシュ押下フレームに確定した方向入力。
        // 例:
        // - 右上入力 + DashPressed なら (1, 1)
        // - 無入力 + DashPressed なら (0, 0)
        public Vector2 DashDirectionInput { get; private set; }

        public PlayerInputReader(RawInputSource rawInputSource, PlayerInputBindings bindings)
        {
            // null だと以後の入力解決が成立しないので、コンストラクタで即検出する。
            this.rawInputSource = rawInputSource ?? throw new ArgumentNullException(nameof(rawInputSource));
            this.bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        }

        // 毎フレーム呼ばれて、公開プロパティ群を最新状態へ更新する。
        public void Update()
        {
            // 移動入力を解決する。
            Move = ResolveMove();

            // Move から上下左右の入力意図を導出する。
            UpHeld = Move.y >= VerticalIntentThreshold;

            bool leftHeld = Move.x <= -HorizontalIntentThreshold;
            bool rightHeld = Move.x >= HorizontalIntentThreshold;
            bool downHeld = Move.y <= -VerticalIntentThreshold;

            // 方向入力の立ち上がり(Pressed)を判定する。
            LeftPressed = leftHeld && !previousLeftHeld;
            RightPressed = rightHeld && !previousRightHeld;
            DownPressed = downHeld && !previousDownHeld;

            LeftHeld = leftHeld;
            RightHeld = rightHeld;
            DownHeld = downHeld;

            // 次フレームの edge 判定に向けて現在状態を保持する。
            previousLeftHeld = leftHeld;
            previousRightHeld = rightHeld;
            previousDownHeld = downHeld;

            // Jump アクションの統合状態を解決する。
            RawButtonFrameState jumpState = ResolveActionState(bindings.Jump);
            JumpPressed = jumpState.PressedThisFrame;
            JumpHeld = jumpState.Held;
            JumpReleased = jumpState.ReleasedThisFrame;

            // Dash アクションの統合状態を解決する。
            RawButtonFrameState dashState = ResolveActionState(bindings.Dash);
            DashPressed = dashState.PressedThisFrame;
            DashHeld = dashState.Held;
            DashReleased = dashState.ReleasedThisFrame;

            // Dash を押した瞬間の方向入力を確定する。
            // 実際にダッシュを開始するかどうかは PlayerController 側の責務。
            DashDirectionInput = DashPressed
                ? ResolveDashDirectionIntent(leftHeld, rightHeld, UpHeld, downHeld)
                : Vector2.zero;

            // Grab アクションの統合状態を解決する。
            RawButtonFrameState grabState = ResolveActionState(bindings.Grab);
            GrabPressed = grabState.PressedThisFrame;
            GrabHeld = grabState.Held;
            GrabReleased = grabState.ReleasedThisFrame;
        }

        // 移動入力を 1 つの Vector2 にまとめる。
        // 仕様:
        // - ゲームパッド入力が十分に入っていればゲームパッド優先
        // - そうでなければキーボード入力を使う
        private Vector2 ResolveMove()
        {
            Vector2 gamepadMove = rawInputSource.GamepadMoveVector;
            float deadZoneSqr = GamepadMoveDeadZone * GamepadMoveDeadZone;

            // ゲームパッドの入力が十分に入っているなら、そちらを採用する。
            if (gamepadMove.sqrMagnitude > deadZoneSqr)
            {
                return gamepadMove;
            }

            // ゲームパッド入力が実質ゼロならキーボード入力へフォールバックする。
            return rawInputSource.KeyboardMoveVector;
        }

        // 押下中の方向意図から、ダッシュ開始時に使う方向を作る。
        // ここでは正規化しない。
        // 正規化や無入力時の補完(向き方向へ水平ダッシュなど)は PlayerController 側で扱う。
        private static Vector2 ResolveDashDirectionIntent(
            bool leftHeld,
            bool rightHeld,
            bool upHeld,
            bool downHeld)
        {
            float x = leftHeld ? -1f : rightHeld ? 1f : 0f;
            float y = downHeld ? -1f : upHeld ? 1f : 0f;
            return new Vector2(x, y);
        }

        // 1つのアクション(binding)について、
        // キーボード主キー / 副キー / ゲームパッドボタンを統合し、
        // Pressed / Held / Released を 1 つの状態として返す。
        private RawButtonFrameState ResolveActionState(InputActionBinding binding)
        {
            // 主キーの現在状態と、このフレームでの押下 / 離しを取得。
            bool primaryCurrent = rawInputSource.IsKeyHeld(binding.PrimaryKeyboardKey);
            bool primaryPressed = rawInputSource.WasKeyPressedThisFrame(binding.PrimaryKeyboardKey);
            bool primaryReleased = rawInputSource.WasKeyReleasedThisFrame(binding.PrimaryKeyboardKey);

            // 副キーの現在状態と、このフレームでの押下 / 離しを取得。
            bool secondaryCurrent = rawInputSource.IsKeyHeld(binding.SecondaryKeyboardKey);
            bool secondaryPressed = rawInputSource.WasKeyPressedThisFrame(binding.SecondaryKeyboardKey);
            bool secondaryReleased = rawInputSource.WasKeyReleasedThisFrame(binding.SecondaryKeyboardKey);

            // ゲームパッドボタンの現在状態と、このフレームでの押下 / 離しを取得。
            bool gamepadCurrent = rawInputSource.IsGamepadButtonHeld(binding.GamepadButton);
            bool gamepadPressed = rawInputSource.WasGamepadButtonPressedThisFrame(binding.GamepadButton);
            bool gamepadReleased = rawInputSource.WasGamepadButtonReleasedThisFrame(binding.GamepadButton);

            // 各入力経路について「前フレームで held だったか」を再構築する。
            bool primaryPrevious = ReconstructPreviousHeld(primaryCurrent, primaryPressed, primaryReleased);
            bool secondaryPrevious = ReconstructPreviousHeld(secondaryCurrent, secondaryPressed, secondaryReleased);
            bool gamepadPrevious = ReconstructPreviousHeld(gamepadCurrent, gamepadPressed, gamepadReleased);

            // 現在フレームで、どれか1つでも押されていれば Held。
            bool currentHeld = primaryCurrent || secondaryCurrent || gamepadCurrent;

            // 前フレームで、どれか1つでも押されていれば PreviousHeld。
            bool previousHeld = primaryPrevious || secondaryPrevious || gamepadPrevious;

            return new RawButtonFrameState(
                held: currentHeld,
                pressedThisFrame: currentHeld && !previousHeld,
                releasedThisFrame: !currentHeld && previousHeld);
        }

        // current / pressedThisFrame / releasedThisFrame から
        // 前フレームで Held だったかを推定する補助関数。
        private static bool ReconstructPreviousHeld(
            bool currentHeld,
            bool pressedThisFrame,
            bool releasedThisFrame)
        {
            if (currentHeld)
            {
                return !pressedThisFrame;
            }

            return releasedThisFrame;
        }
    }
}