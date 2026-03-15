using System;
using UnityEngine;

namespace Game.Input
{
    // 生入力(RawInputSource)をゲーム側で扱いやすい形へ変換する責務を持つ。
    // 例:
    // - キーボードとゲームパッドの移動入力を1つの Move にまとめる
    // - Jump / Step の Pressed, Held, Released を毎フレーム更新する
    // - 上下入力の意図(UpHeld / DownHeld)を Move.y から導出する
    public sealed class PlayerInputReader
    {
        // アナログスティックの微小な揺れを無視するための閾値。
        // sqrMagnitude で比較するので、かなり小さい値を使っている。
        private const float MoveInputEpsilon = 0.0001f;

        // 上方向 / 下方向の「意図あり」とみなす縦入力のしきい値。
        // 0.5f 以上なら UpHeld、-0.5f 以下なら DownHeld と判定する。
        private const float VerticalIntentThreshold = 0.5f;

        // Unity Input などから取得した生入力の供給元。
        // このクラスは生入力の取得処理そのものではなく、
        // 取得済みデータの解釈と統合を担当する。
        private readonly RawInputSource _rawInputSource;

        // ジャンプ / ステップなどの入力割り当て情報。
        // どのキー・どのゲームパッドボタンが何のアクションかを保持する。
        private readonly PlayerInputBindings _bindings;

        // 現在の移動入力。
        // 優先順位は GamepadMoveVector → KeyboardMoveVector。
        public Vector2 Move { get; private set; }

        // Move.y から導出した上下意図。
        // 「押された瞬間」ではなく「その方向へ入れ続けているか」を表す。
        public bool UpHeld { get; private set; }
        public bool DownHeld { get; private set; }

        // ジャンプ入力のフレーム状態。
        public bool JumpPressed { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool JumpReleased { get; private set; }

        // ステップ入力のフレーム状態。
        public bool StepPressed { get; private set; }
        public bool StepHeld { get; private set; }
        public bool StepReleased { get; private set; }

        public PlayerInputReader(RawInputSource rawInputSource, PlayerInputBindings bindings)
        {
            // null だと以後の入力解決が成立しないので、コンストラクタで即検出する。
            _rawInputSource = rawInputSource ?? throw new ArgumentNullException(nameof(rawInputSource));
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        }

        // 毎フレーム呼ばれて、公開プロパティ群を最新状態へ更新する。
        public void Update()
        {
            // 移動入力を解決する。
            Move = ResolveMove();

            // 縦入力の意図を Move.y から導出する。
            // ここでは押下エッジではなく、しきい値を超えて保持されているかだけを見る。
            UpHeld = Move.y >= VerticalIntentThreshold;
            DownHeld = Move.y <= -VerticalIntentThreshold;

            // Jump アクションの統合状態を解決する。
            RawButtonFrameState jumpState = ResolveActionState(_bindings.Jump);
            JumpPressed = jumpState.PressedThisFrame;
            JumpHeld = jumpState.Held;
            JumpReleased = jumpState.ReleasedThisFrame;

            // Step アクションの統合状態を解決する。
            RawButtonFrameState stepState = ResolveActionState(_bindings.Step);
            StepPressed = stepState.PressedThisFrame;
            StepHeld = stepState.Held;
            StepReleased = stepState.ReleasedThisFrame;
        }

        // 移動入力を 1 つの Vector2 にまとめる。
        // 仕様:
        // - ゲームパッド入力が十分に入っていればゲームパッド優先
        // - そうでなければキーボード入力を使う
        private Vector2 ResolveMove()
        {
            Vector2 gamepadMove = _rawInputSource.GamepadMoveVector;

            // ゲームパッドの入力が微小ノイズでないなら、そちらを採用する。
            if (gamepadMove.sqrMagnitude > MoveInputEpsilon)
            {
                return gamepadMove;
            }

            // ゲームパッド入力が実質ゼロならキーボード入力へフォールバックする。
            return _rawInputSource.KeyboardMoveVector;
        }

        // 1つのアクション(binding)について、
        // キーボード主キー / 副キー / ゲームパッドボタンを統合し、
        // Pressed / Held / Released を 1 つの状態として返す。
        private RawButtonFrameState ResolveActionState(InputActionBinding binding)
        {
            // 主キーの現在状態と、このフレームでの押下 / 離しを取得。
            bool primaryCurrent = _rawInputSource.IsKeyHeld(binding.PrimaryKeyboardKey);
            bool primaryPressed = _rawInputSource.WasKeyPressedThisFrame(binding.PrimaryKeyboardKey);
            bool primaryReleased = _rawInputSource.WasKeyReleasedThisFrame(binding.PrimaryKeyboardKey);

            // 副キーの現在状態と、このフレームでの押下 / 離しを取得。
            bool secondaryCurrent = _rawInputSource.IsKeyHeld(binding.SecondaryKeyboardKey);
            bool secondaryPressed = _rawInputSource.WasKeyPressedThisFrame(binding.SecondaryKeyboardKey);
            bool secondaryReleased = _rawInputSource.WasKeyReleasedThisFrame(binding.SecondaryKeyboardKey);

            // ゲームパッドボタンの現在状態と、このフレームでの押下 / 離しを取得。
            bool gamepadCurrent = _rawInputSource.IsGamepadButtonHeld(binding.GamepadButton);
            bool gamepadPressed = _rawInputSource.WasGamepadButtonPressedThisFrame(binding.GamepadButton);
            bool gamepadReleased = _rawInputSource.WasGamepadButtonReleasedThisFrame(binding.GamepadButton);

            // 各入力経路について「前フレームで held だったか」を再構築する。
            // 生の Previous 状態を持っていない前提でも、
            // current / pressedThisFrame / releasedThisFrame の組み合わせから復元できる。
            bool primaryPrevious = ReconstructPreviousHeld(primaryCurrent, primaryPressed, primaryReleased);
            bool secondaryPrevious = ReconstructPreviousHeld(secondaryCurrent, secondaryPressed, secondaryReleased);
            bool gamepadPrevious = ReconstructPreviousHeld(gamepadCurrent, gamepadPressed, gamepadReleased);

            // 現在フレームで、どれか1つでも押されていれば Held。
            bool currentHeld = primaryCurrent || secondaryCurrent || gamepadCurrent;

            // 前フレームで、どれか1つでも押されていれば PreviousHeld。
            bool previousHeld = primaryPrevious || secondaryPrevious || gamepadPrevious;

            // 統合結果を返す。
            // - PressedThisFrame  : 今フレーム押されていて、前フレームは押されていない
            // - ReleasedThisFrame : 今フレーム押されておらず、前フレームは押されていた
            return new RawButtonFrameState(
                held: currentHeld,
                pressedThisFrame: currentHeld && !previousHeld,
                releasedThisFrame: !currentHeld && previousHeld);
        }

        // current / pressedThisFrame / releasedThisFrame から
        // 前フレームで Held だったかを推定する311補助関数。
        //
        // ケース:
        // 1. currentHeld == true
        //    - 今押されていて、このフレームで押したなら前フレームは false
        //    - 今押されていて、このフレームで押していないなら前フレームも true
        //
        // 2. currentHeld == false
        //    - 今離されていて、このフレームで離したなら前フレームは true
        //    - 今離されていて、このフレームで離していないなら前フレームも false
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