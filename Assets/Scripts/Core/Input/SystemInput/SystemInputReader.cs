using System;
using UnityEngine;

namespace Game.Input
{
    public sealed class SystemInputReader
    {
        private readonly RawInputSource _rawInputSource;
        private readonly SystemInputBindings _bindings;

        public bool UpPressed { get; private set; }
        public bool UpHeld { get; private set; }
        public bool UpReleased { get; private set; }

        public bool DownPressed { get; private set; }
        public bool DownHeld { get; private set; }
        public bool DownReleased { get; private set; }

        public bool LeftPressed { get; private set; }
        public bool LeftHeld { get; private set; }
        public bool LeftReleased { get; private set; }

        public bool RightPressed { get; private set; }
        public bool RightHeld { get; private set; }
        public bool RightReleased { get; private set; }

        public bool SubmitPressed { get; private set; }
        public bool SubmitHeld { get; private set; }
        public bool SubmitReleased { get; private set; }

        public bool CancelPressed { get; private set; }
        public bool CancelHeld { get; private set; }
        public bool CancelReleased { get; private set; }

        public bool PausePressed { get; private set; }
        public bool PauseHeld { get; private set; }
        public bool PauseReleased { get; private set; }

        public SystemInputReader(RawInputSource rawInputSource, SystemInputBindings bindings)
        {
            _rawInputSource = rawInputSource ?? throw new ArgumentNullException(nameof(rawInputSource));
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        }

        public void Update()
        {
            RawButtonFrameState upState = ResolveActionState(_bindings.Up);
            UpPressed = upState.PressedThisFrame;
            UpHeld = upState.Held;
            UpReleased = upState.ReleasedThisFrame;

            RawButtonFrameState downState = ResolveActionState(_bindings.Down);
            DownPressed = downState.PressedThisFrame;
            DownHeld = downState.Held;
            DownReleased = downState.ReleasedThisFrame;

            RawButtonFrameState leftState = ResolveActionState(_bindings.Left);
            LeftPressed = leftState.PressedThisFrame;
            LeftHeld = leftState.Held;
            LeftReleased = leftState.ReleasedThisFrame;

            RawButtonFrameState rightState = ResolveActionState(_bindings.Right);
            RightPressed = rightState.PressedThisFrame;
            RightHeld = rightState.Held;
            RightReleased = rightState.ReleasedThisFrame;

            RawButtonFrameState submitState = ResolveActionState(_bindings.Submit);
            SubmitPressed = submitState.PressedThisFrame;
            SubmitHeld = submitState.Held;
            SubmitReleased = submitState.ReleasedThisFrame;

            RawButtonFrameState cancelState = ResolveActionState(_bindings.Cancel);
            CancelPressed = cancelState.PressedThisFrame;
            CancelHeld = cancelState.Held;
            CancelReleased = cancelState.ReleasedThisFrame;

            RawButtonFrameState pauseState = ResolveActionState(_bindings.Pause);
            PausePressed = pauseState.PressedThisFrame;
            PauseHeld = pauseState.Held;
            PauseReleased = pauseState.ReleasedThisFrame;
        }

        private RawButtonFrameState ResolveActionState(InputActionBinding binding)
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