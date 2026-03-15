using System;
using UnityEngine;

namespace Game.Input
{
    // システム系入力を読むクラス。
    // ここで扱うのはプレイヤー移動ではなく、
    // UI 操作やメニュー操作などに使う共通入力。
    //
    // 責務:
    // - RawInputSource から生入力を取得する
    // - InputActionBinding に従って複数入力経路を統合する
    // - Pressed / Held / Released のフレーム状態へ変換する
    public sealed class SystemInputReader
    {
        // 生入力の供給元。
        // 実際のキー / ボタン状態取得はこのオブジェクトへ委譲する。
        private readonly RawInputSource _rawInputSource;

        // システム入力の割り当て定義。
        // Up / Down / Submit / Cancel / Pause などの対応を保持する。
        private readonly SystemInputBindings _bindings;

        // 上入力のフレーム状態。
        public bool UpPressed { get; private set; }
        public bool UpHeld { get; private set; }
        public bool UpReleased { get; private set; }

        // 下入力のフレーム状態。
        public bool DownPressed { get; private set; }
        public bool DownHeld { get; private set; }
        public bool DownReleased { get; private set; }

        // 左入力のフレーム状態。
        public bool LeftPressed { get; private set; }
        public bool LeftHeld { get; private set; }
        public bool LeftReleased { get; private set; }

        // 右入力のフレーム状態。
        public bool RightPressed { get; private set; }
        public bool RightHeld { get; private set; }
        public bool RightReleased { get; private set; }

        // 決定入力のフレーム状態。
        public bool SubmitPressed { get; private set; }
        public bool SubmitHeld { get; private set; }
        public bool SubmitReleased { get; private set; }

        // キャンセル入力のフレーム状態。
        public bool CancelPressed { get; private set; }
        public bool CancelHeld { get; private set; }
        public bool CancelReleased { get; private set; }

        // ポーズ入力のフレーム状態。
        public bool PausePressed { get; private set; }
        public bool PauseHeld { get; private set; }
        public bool PauseReleased { get; private set; }

        public SystemInputReader(RawInputSource rawInputSource, SystemInputBindings bindings)
        {
            // 必須依存なので、生成時点で null を弾く。
            _rawInputSource = rawInputSource ?? throw new ArgumentNullException(nameof(rawInputSource));
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        }

        // 毎フレーム呼ばれて、すべてのシステム入力状態を更新する。
        public void Update()
        {
            // 上入力を解決する。
            RawButtonFrameState upState = ResolveActionState(_bindings.Up);
            UpPressed = upState.PressedThisFrame;
            UpHeld = upState.Held;
            UpReleased = upState.ReleasedThisFrame;

            // 下入力を解決する。
            RawButtonFrameState downState = ResolveActionState(_bindings.Down);
            DownPressed = downState.PressedThisFrame;
            DownHeld = downState.Held;
            DownReleased = downState.ReleasedThisFrame;

            // 左入力を解決する。
            RawButtonFrameState leftState = ResolveActionState(_bindings.Left);
            LeftPressed = leftState.PressedThisFrame;
            LeftHeld = leftState.Held;
            LeftReleased = leftState.ReleasedThisFrame;

            // 右入力を解決する。
            RawButtonFrameState rightState = ResolveActionState(_bindings.Right);
            RightPressed = rightState.PressedThisFrame;
            RightHeld = rightState.Held;
            RightReleased = rightState.ReleasedThisFrame;

            // 決定入力を解決する。
            RawButtonFrameState submitState = ResolveActionState(_bindings.Submit);
            SubmitPressed = submitState.PressedThisFrame;
            SubmitHeld = submitState.Held;
            SubmitReleased = submitState.ReleasedThisFrame;

            // キャンセル入力を解決する。
            RawButtonFrameState cancelState = ResolveActionState(_bindings.Cancel);
            CancelPressed = cancelState.PressedThisFrame;
            CancelHeld = cancelState.Held;
            CancelReleased = cancelState.ReleasedThisFrame;

            // ポーズ入力を解決する。
            RawButtonFrameState pauseState = ResolveActionState(_bindings.Pause);
            PausePressed = pauseState.PressedThisFrame;
            PauseHeld = pauseState.Held;
            PauseReleased = pauseState.ReleasedThisFrame;
        }

        // 1つのアクション(binding)について、
        // 主キー / 副キー / ゲームパッド入力を統合し、
        // そのアクション全体の Pressed / Held / Released を返す。
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

            // 各入力経路ごとに「前フレームで押されていたか」を再構築する。
            // Previous 状態を直接保持していなくても、
            // current / pressedThisFrame / releasedThisFrame の組み合わせから導出できる。
            bool primaryPrevious = ReconstructPreviousHeld(primaryCurrent, primaryPressed, primaryReleased);
            bool secondaryPrevious = ReconstructPreviousHeld(secondaryCurrent, secondaryPressed, secondaryReleased);
            bool gamepadPrevious = ReconstructPreviousHeld(gamepadCurrent, gamepadPressed, gamepadReleased);

            // 現在フレームで、いずれかの入力経路が押されていれば Held。
            bool currentHeld = primaryCurrent || secondaryCurrent || gamepadCurrent;

            // 前フレームで、いずれかの入力経路が押されていれば previousHeld。
            bool previousHeld = primaryPrevious || secondaryPrevious || gamepadPrevious;

            // 統合されたボタン状態を返す。
            // - pressedThisFrame  : 今フレーム押されていて、前フレームは押されていない
            // - releasedThisFrame : 今フレーム押されておらず、前フレームは押されていた
            return new RawButtonFrameState(
                held: currentHeld,
                pressedThisFrame: currentHeld && !previousHeld,
                releasedThisFrame: !currentHeld && previousHeld);
        }

        // current / pressedThisFrame / releasedThisFrame から
        // 「前フレームで held だったか」を復元する補助関数。
        //
        // ケース整理:
        // 1. currentHeld == true
        //    - このフレームで押されたなら、前フレームは false
        //    - このフレームで押されていないなら、前フレームも true
        //
        // 2. currentHeld == false
        //    - このフレームで離されたなら、前フレームは true
        //    - このフレームで離されていないなら、前フレームも false
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