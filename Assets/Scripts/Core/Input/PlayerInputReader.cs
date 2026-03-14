using System;
using UnityEngine;

namespace Game.Input
{

    /// RawInputSource の低レベル入力を、プレイヤー用の意味入力へ変換する。
    /// デバイス API には一切触れない。
    
    public sealed class PlayerInputReader
    {
        private const float MoveInputEpsilon = 0.0001f;
        private const float VerticalIntentThreshold = 0.5f;

        private readonly RawInputSource _rawInputSource;
        private readonly PlayerInputBindings _bindings;

        public Vector2 Move { get; private set; }

        public bool UpHeld { get; private set; }
        public bool DownHeld { get; private set; }

        public bool JumpPressed { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool JumpReleased { get; private set; }

        public bool StepPressed { get; private set; }
        public bool StepHeld { get; private set; }
        public bool StepReleased { get; private set; }

        public PlayerInputReader(RawInputSource rawInputSource, PlayerInputBindings bindings)
        {
            _rawInputSource = rawInputSource ?? throw new ArgumentNullException(nameof(rawInputSource));
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        }

        /// このフレームのプレイヤー意味入力を更新する。
        /// RawInputSource が先に Update 済みである前提。
        public void Update()
        {
            Move = ResolveMove();

            UpHeld = Move.y >= VerticalIntentThreshold;
            DownHeld = Move.y <= -VerticalIntentThreshold;

            RawButtonFrameState jumpState = ResolveActionState(_bindings.Jump);
            JumpPressed = jumpState.PressedThisFrame;
            JumpHeld = jumpState.Held;
            JumpReleased = jumpState.ReleasedThisFrame;

            RawButtonFrameState stepState = ResolveActionState(_bindings.Step);
            StepPressed = stepState.PressedThisFrame;
            StepHeld = stepState.Held;
            StepReleased = stepState.ReleasedThisFrame;
        }

        private Vector2 ResolveMove()
        {
            Vector2 gamepadMove = _rawInputSource.GamepadMoveVector;
            if (gamepadMove.sqrMagnitude > MoveInputEpsilon)
            {
                return gamepadMove;
            }

            return _rawInputSource.KeyboardMoveVector;
        }

        private RawButtonFrameState ResolveActionState(PlayerActionBinding binding)
        {
            bool primaryCurrent = _rawInputSource.IsKeyHeld(binding.PrimaryKeyboardKey);
            bool primaryPressed = _rawInputSource.WasKeyPressedThisFrame(binding.PrimaryKeyboardKey);
            bool primaryReleased = _rawInputSource.WasKeyReleasedThisFrame(binding.PrimaryKeyboardKey);

            bool secondaryCurrent = _rawInputSource.IsKeyHeld(binding.SecondaryKeyboardKey);
            bool secondaryPressed = _rawInputSource.WasKeyPressedThisFrame(binding.SecondaryKeyboardKey);
            bool secondaryReleased = _rawInputSource.WasKeyReleasedThisFrame(binding.SecondaryKeyboardKey);

            bool gamepadCurrent = _rawInputSource.IsGamepadButtonHeld(binding.GamepadButton);
            bool gamepadPressed = _rawInputSource.WasGamepadButtonPressedThisFrame(binding.GamepadButton);
            bool gamepadReleased = _rawInputSource.WasGamepadButtonReleasedThisFrame(binding.GamepadButton);

            bool primaryPrevious = ReconstructPreviousHeld(primaryCurrent, primaryPressed, primaryReleased);
            bool secondaryPrevious = ReconstructPreviousHeld(secondaryCurrent, secondaryPressed, secondaryReleased);
            bool gamepadPrevious = ReconstructPreviousHeld(gamepadCurrent, gamepadPressed, gamepadReleased);

            bool currentHeld = primaryCurrent || secondaryCurrent || gamepadCurrent;
            bool previousHeld = primaryPrevious || secondaryPrevious || gamepadPrevious;

            return new RawButtonFrameState(
                held: currentHeld,
                pressedThisFrame: currentHeld && !previousHeld,
                releasedThisFrame: !currentHeld && previousHeld);
        }

        /// current / pressed / released から前フレームの held を復元する。
        private static bool ReconstructPreviousHeld(
            bool currentHeld,
            bool pressedThisFrame,
            bool releasedThisFrame)
        {
            if (currentHeld)
            {
                // 現在押されているなら、
                // 今フレーム押されたのでなければ前フレームも押されていた。
                return !pressedThisFrame;
            }

            // 現在押されていないなら、
            // 今フレーム離された時だけ前フレームは押されていた。
            return releasedThisFrame;
        }
    }
}