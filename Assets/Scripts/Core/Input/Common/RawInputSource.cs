using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Game.Input
{
    // Reader 側が使うゲームパッドの低レベルボタン識別子
    // Xbox 前提だが、実装は Gamepad 抽象を読む
    public enum RawGamepadButton
    {
        A = 0,             // Aボタン（下）
        B,                 // Bボタン（右）
        X,                 // Xボタン（左）
        Y,                 // Yボタン（上）

        Start,             // Start / Menu ボタン
        View,              // View / Back ボタン

        LeftShoulder,      // LB：左肩ボタン
        RightShoulder,     // RB：右肩ボタン

        LeftStickPress,    // L3：左スティック押し込み
        RightStickPress,   // R3：右スティック押し込み

        LeftTrigger,       // LT：左トリガー
        RightTrigger,      // RT：右トリガー

        DpadUp,            // 十字キー上
        DpadDown,          // 十字キー下
        DpadLeft,          // 十字キー左
        DpadRight,         // 十字キー右

        Count              // 要素数（実ボタンではない）
    }

    // 1ボタン分のフレーム状態
    [Serializable]
    public readonly struct RawButtonFrameState
    {
        // このフレームで押され続けているか
        public bool Held { get; }

        // このフレームで押された瞬間か
        public bool PressedThisFrame { get; }

        // このフレームで離された瞬間か
        public bool ReleasedThisFrame { get; }

        public RawButtonFrameState(bool held, bool pressedThisFrame, bool releasedThisFrame)
        {
            Held = held;
            PressedThisFrame = pressedThisFrame;
            ReleasedThisFrame = releasedThisFrame;
        }
    }

    // 低レベル入力のスナップショット取得専用
    // 意味入力は持たない
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class RawInputSource : MonoBehaviour
    {
        [Header("Stick Deadzone")]
        [SerializeField, Range(0.0f, 0.95f)]
        private float _leftStickDeadzone = 0.20f;

        [SerializeField, Range(0.0f, 0.95f)]
        private float _rightStickDeadzone = 0.20f;

        private static readonly int s_keyArraySize = ComputeKeyArraySize();
        private const int GamepadButtonCount = (int)RawGamepadButton.Count;

        // キーボードの現在/前フレーム状態
        private bool[] _currentKeyHeld = new bool[s_keyArraySize];
        private bool[] _previousKeyHeld = new bool[s_keyArraySize];

        // ゲームパッドの現在/前フレーム状態
        private bool[] _currentGamepadHeld = new bool[GamepadButtonCount];
        private bool[] _previousGamepadHeld = new bool[GamepadButtonCount];

        // 毎フレーム計算して保持する値
        private Vector2 _keyboardMoveVector;
        private Vector2 _gamepadLeftStickVector;
        private Vector2 _gamepadRightStickVector;
        private Vector2 _gamepadDpadVector;
        private Vector2 _gamepadMoveVector;
        private float _leftTriggerValue;
        private float _rightTriggerValue;

        // 最後に更新したフレーム番号
        public int SnapshotFrame { get; private set; } = -1;

        // このフレームでキーボードが存在したか
        public bool HasKeyboard { get; private set; }

        // このフレームでゲームパッドが存在したか
        public bool HasGamepad { get; private set; }

        // X:+右 / Y:+上 のデジタル移動
        public Vector2 KeyboardMoveVector => _keyboardMoveVector;

        // デッドゾーン適用後の左スティック
        public Vector2 GamepadLeftStickVector => _gamepadLeftStickVector;

        // デッドゾーン適用後の右スティック
        public Vector2 GamepadRightStickVector => _gamepadRightStickVector;

        // X:+右 / Y:+上 の DPad ベクトル
        public Vector2 GamepadDpadVector => _gamepadDpadVector;

        // 左スティック + DPad を合成した移動ベクトル
        public Vector2 GamepadMoveVector => _gamepadMoveVector;

        // 0..1 の左トリガー値
        public float LeftTriggerValue => _leftTriggerValue;

        // 0..1 の右トリガー値
        public float RightTriggerValue => _rightTriggerValue;

        private void Update()
        {
            // 低レベル入力を毎フレーム1回だけ読む
            UpdateKeyboardSnapshot();
            UpdateGamepadSnapshot();
            SnapshotFrame = Time.frameCount;
        }

        private void OnDisable()
        {
            // 無効化時は状態を初期化する
            ClearAllSnapshots();
            SnapshotFrame = -1;
        }

        // ---------------------------------------------------------------------
        // Public API : Keyboard
        // ---------------------------------------------------------------------

        public RawButtonFrameState GetKeyState(Key key)
        {
            if (!TryGetKeyIndex(key, out int index))
            {
                return default;
            }

            bool held = _currentKeyHeld[index];
            bool prev = _previousKeyHeld[index];
            return new RawButtonFrameState(
                held,
                pressedThisFrame: held && !prev,
                releasedThisFrame: !held && prev);
        }

        public bool IsKeyHeld(Key key)
        {
            if (!TryGetKeyIndex(key, out int index))
            {
                return false;
            }

            return _currentKeyHeld[index];
        }

        public bool WasKeyPressedThisFrame(Key key)
        {
            if (!TryGetKeyIndex(key, out int index))
            {
                return false;
            }

            return _currentKeyHeld[index] && !_previousKeyHeld[index];
        }

        public bool WasKeyReleasedThisFrame(Key key)
        {
            if (!TryGetKeyIndex(key, out int index))
            {
                return false;
            }

            return !_currentKeyHeld[index] && _previousKeyHeld[index];
        }

        // ---------------------------------------------------------------------
        // Public API : Gamepad
        // ---------------------------------------------------------------------

        public RawButtonFrameState GetGamepadButtonState(RawGamepadButton button)
        {
            if (!TryGetGamepadButtonIndex(button, out int index))
            {
                return default;
            }

            bool held = _currentGamepadHeld[index];
            bool prev = _previousGamepadHeld[index];
            return new RawButtonFrameState(
                held,
                pressedThisFrame: held && !prev,
                releasedThisFrame: !held && prev);
        }

        public bool IsGamepadButtonHeld(RawGamepadButton button)
        {
            if (!TryGetGamepadButtonIndex(button, out int index))
            {
                return false;
            }

            return _currentGamepadHeld[index];
        }

        public bool WasGamepadButtonPressedThisFrame(RawGamepadButton button)
        {
            if (!TryGetGamepadButtonIndex(button, out int index))
            {
                return false;
            }

            return _currentGamepadHeld[index] && !_previousGamepadHeld[index];
        }

        public bool WasGamepadButtonReleasedThisFrame(RawGamepadButton button)
        {
            if (!TryGetGamepadButtonIndex(button, out int index))
            {
                return false;
            }

            return !_currentGamepadHeld[index] && _previousGamepadHeld[index];
        }

        // ---------------------------------------------------------------------
        // Snapshot Update
        // ---------------------------------------------------------------------

        private void UpdateKeyboardSnapshot()
        {
            // 前回状態と今回状態を入れ替え、今回側を空にする
            Swap(ref _currentKeyHeld, ref _previousKeyHeld);
            Array.Clear(_currentKeyHeld, 0, _currentKeyHeld.Length);

            Keyboard keyboard = Keyboard.current;
            HasKeyboard = keyboard != null;

            if (!HasKeyboard)
            {
                _keyboardMoveVector = Vector2.zero;
                return;
            }

            // 接続中キーボードの全キー状態を配列に反映
            var allKeys = keyboard.allKeys;
            for (int i = 0; i < allKeys.Count; i++)
            {
                KeyControl keyControl = allKeys[i];
                int index = (int)keyControl.keyCode;

                if ((uint)index < (uint)_currentKeyHeld.Length)
                {
                    _currentKeyHeld[index] = keyControl.isPressed;
                }
            }

            // WASD + 矢印キーから移動ベクトル生成
            _keyboardMoveVector = ReadKeyboardMoveVector();
        }

        private void UpdateGamepadSnapshot()
        {
            // 前回状態と今回状態を入れ替え、今回側を空にする
            Swap(ref _currentGamepadHeld, ref _previousGamepadHeld);
            Array.Clear(_currentGamepadHeld, 0, _currentGamepadHeld.Length);

            Gamepad gamepad = Gamepad.current;
            HasGamepad = gamepad != null;

            if (!HasGamepad)
            {
                _gamepadLeftStickVector = Vector2.zero;
                _gamepadRightStickVector = Vector2.zero;
                _gamepadDpadVector = Vector2.zero;
                _gamepadMoveVector = Vector2.zero;
                _leftTriggerValue = 0.0f;
                _rightTriggerValue = 0.0f;
                return;
            }

            // ボタン状態を enum 配列へ詰める
            SetGamepadButtonState(RawGamepadButton.A, gamepad.buttonSouth.isPressed);
            SetGamepadButtonState(RawGamepadButton.B, gamepad.buttonEast.isPressed);
            SetGamepadButtonState(RawGamepadButton.X, gamepad.buttonWest.isPressed);
            SetGamepadButtonState(RawGamepadButton.Y, gamepad.buttonNorth.isPressed);

            SetGamepadButtonState(RawGamepadButton.Start, gamepad.startButton.isPressed);
            SetGamepadButtonState(RawGamepadButton.View, gamepad.selectButton.isPressed);

            SetGamepadButtonState(RawGamepadButton.LeftShoulder, gamepad.leftShoulder.isPressed);
            SetGamepadButtonState(RawGamepadButton.RightShoulder, gamepad.rightShoulder.isPressed);

            SetGamepadButtonState(RawGamepadButton.LeftStickPress, gamepad.leftStickButton.isPressed);
            SetGamepadButtonState(RawGamepadButton.RightStickPress, gamepad.rightStickButton.isPressed);

            SetGamepadButtonState(RawGamepadButton.LeftTrigger, gamepad.leftTrigger.isPressed);
            SetGamepadButtonState(RawGamepadButton.RightTrigger, gamepad.rightTrigger.isPressed);

            SetGamepadButtonState(RawGamepadButton.DpadUp, gamepad.dpad.up.isPressed);
            SetGamepadButtonState(RawGamepadButton.DpadDown, gamepad.dpad.down.isPressed);
            SetGamepadButtonState(RawGamepadButton.DpadLeft, gamepad.dpad.left.isPressed);
            SetGamepadButtonState(RawGamepadButton.DpadRight, gamepad.dpad.right.isPressed);

            // アナログ値を保持
            _leftTriggerValue = gamepad.leftTrigger.ReadValue();
            _rightTriggerValue = gamepad.rightTrigger.ReadValue();

            // スティックにデッドゾーン適用
            _gamepadLeftStickVector = ApplyRadialDeadzone(gamepad.leftStick.ReadValue(), _leftStickDeadzone);
            _gamepadRightStickVector = ApplyRadialDeadzone(gamepad.rightStick.ReadValue(), _rightStickDeadzone);

            // 左スティック + DPad を移動入力として合成
            _gamepadDpadVector = ReadDpadVector();
            _gamepadMoveVector = Vector2.ClampMagnitude(_gamepadLeftStickVector + _gamepadDpadVector, 1.0f);
        }

        // ---------------------------------------------------------------------
        // Read Helpers
        // ---------------------------------------------------------------------

        private Vector2 ReadKeyboardMoveVector()
        {
            int x = ReadDigitalAxis(
                positiveA: Key.D,
                positiveB: Key.RightArrow,
                negativeA: Key.A,
                negativeB: Key.LeftArrow);

            int y = ReadDigitalAxis(
                positiveA: Key.W,
                positiveB: Key.UpArrow,
                negativeA: Key.S,
                negativeB: Key.DownArrow);

            return new Vector2(x, y);
        }

        private Vector2 ReadDpadVector()
        {
            int x =
                (_currentGamepadHeld[(int)RawGamepadButton.DpadRight] ? 1 : 0) -
                (_currentGamepadHeld[(int)RawGamepadButton.DpadLeft] ? 1 : 0);

            int y =
                (_currentGamepadHeld[(int)RawGamepadButton.DpadUp] ? 1 : 0) -
                (_currentGamepadHeld[(int)RawGamepadButton.DpadDown] ? 1 : 0);

            return new Vector2(x, y);
        }

        private int ReadDigitalAxis(Key positiveA, Key positiveB, Key negativeA, Key negativeB)
        {
            bool positive = IsKeyHeldInternal(positiveA) || IsKeyHeldInternal(positiveB);
            bool negative = IsKeyHeldInternal(negativeA) || IsKeyHeldInternal(negativeB);

            // 両方押し or 両方未押下なら 0
            if (positive == negative)
            {
                return 0;
            }

            return positive ? 1 : -1;
        }

        private bool IsKeyHeldInternal(Key key)
        {
            return TryGetKeyIndex(key, out int index) && _currentKeyHeld[index];
        }

        private void SetGamepadButtonState(RawGamepadButton button, bool held)
        {
            _currentGamepadHeld[(int)button] = held;
        }

        private static Vector2 ApplyRadialDeadzone(Vector2 value, float deadzone)
        {
            float magnitude = value.magnitude;
            if (magnitude <= deadzone)
            {
                return Vector2.zero;
            }

            if (magnitude <= Mathf.Epsilon)
            {
                return Vector2.zero;
            }

            // デッドゾーン外の入力を 0..1 に再正規化
            float normalizedMagnitude = Mathf.Clamp01((magnitude - deadzone) / (1.0f - deadzone));
            return (value / magnitude) * normalizedMagnitude;
        }

        // ---------------------------------------------------------------------
        // Validation / Utility
        // ---------------------------------------------------------------------

        private static bool TryGetKeyIndex(Key key, out int index)
        {
            index = (int)key;
            if (key == Key.None)
            {
                return false;
            }

            return (uint)index < (uint)s_keyArraySize;
        }

        private static bool TryGetGamepadButtonIndex(RawGamepadButton button, out int index)
        {
            index = (int)button;
            return index >= 0 && index < GamepadButtonCount;
        }

        private void ClearAllSnapshots()
        {
            Array.Clear(_currentKeyHeld, 0, _currentKeyHeld.Length);
            Array.Clear(_previousKeyHeld, 0, _previousKeyHeld.Length);

            Array.Clear(_currentGamepadHeld, 0, _currentGamepadHeld.Length);
            Array.Clear(_previousGamepadHeld, 0, _previousGamepadHeld.Length);

            _keyboardMoveVector = Vector2.zero;
            _gamepadLeftStickVector = Vector2.zero;
            _gamepadRightStickVector = Vector2.zero;
            _gamepadDpadVector = Vector2.zero;
            _gamepadMoveVector = Vector2.zero;
            _leftTriggerValue = 0.0f;
            _rightTriggerValue = 0.0f;

            HasKeyboard = false;
            HasGamepad = false;
        }

        private static void Swap(ref bool[] a, ref bool[] b)
        {
            bool[] tmp = a;
            a = b;
            b = tmp;
        }

        private static int ComputeKeyArraySize()
        {
            Array values = Enum.GetValues(typeof(Key));
            int max = 0;

            for (int i = 0; i < values.Length; i++)
            {
                int value = (int)values.GetValue(i);
                if (value > max)
                {
                    max = value;
                }
            }

            // Key enum の最大値に合わせて配列サイズを決める
            return max + 1;
        }
    }
}