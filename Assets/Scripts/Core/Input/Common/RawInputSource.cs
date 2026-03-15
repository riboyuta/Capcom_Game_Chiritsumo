using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Game.Input
{
    // Reader 側が使うゲームパッドの低レベルボタン識別子。
    // Xbox 風の命名だが、実際には Unity Input System の Gamepad 抽象を読む。
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

        Count              // 要素数。実ボタンではない。
    }

    // 1ボタン分のフレーム状態を表す値オブジェクト。
    [Serializable]
    public readonly struct RawButtonFrameState
    {
        // このフレームで押され続けているか。
        public bool Held { get; }

        // このフレームで押された瞬間か。
        public bool PressedThisFrame { get; }

        // このフレームで離された瞬間か。
        public bool ReleasedThisFrame { get; }

        public RawButtonFrameState(bool held, bool pressedThisFrame, bool releasedThisFrame)
        {
            Held = held;
            PressedThisFrame = pressedThisFrame;
            ReleasedThisFrame = releasedThisFrame;
        }
    }

    // 低レベル入力のスナップショット取得専用クラス。
    // 責務:
    // - キーボード / ゲームパッドの生状態を毎フレーム読む
    // - 現在フレーム / 前フレーム状態を保持する
    // - 他の Reader が使いやすい API を提供する
    //
    // 非責務:
    // - Jump / Step / Submit などの意味入力解決
    // - プレイヤー操作やUI操作そのもの
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class RawInputSource : MonoBehaviour
    {
        [Header("Stick Deadzone")]

        // 左スティックの放置ノイズを無視する半径。
        [SerializeField, Range(0.0f, 0.95f)]
        private float _leftStickDeadzone = 0.20f;

        // 右スティックの放置ノイズを無視する半径。
        [SerializeField, Range(0.0f, 0.95f)]
        private float _rightStickDeadzone = 0.20f;

        // Key enum の最大値に合わせた配列サイズ。
        private static readonly int s_keyArraySize = ComputeKeyArraySize();

        // RawGamepadButton の有効数。
        private const int GamepadButtonCount = (int)RawGamepadButton.Count;

        // キーボードの現在 / 前フレーム状態。
        // インデックスは Key enum の int 値を使う。
        private bool[] _currentKeyHeld = new bool[s_keyArraySize];
        private bool[] _previousKeyHeld = new bool[s_keyArraySize];

        // ゲームパッドの現在 / 前フレーム状態。
        // インデックスは RawGamepadButton enum の int 値を使う。
        private bool[] _currentGamepadHeld = new bool[GamepadButtonCount];
        private bool[] _previousGamepadHeld = new bool[GamepadButtonCount];

        // 毎フレーム計算して保持する低レベル値。
        private Vector2 _keyboardMoveVector;
        private Vector2 _gamepadLeftStickVector;
        private Vector2 _gamepadRightStickVector;
        private Vector2 _gamepadDpadVector;
        private Vector2 _gamepadMoveVector;
        private float _leftTriggerValue;
        private float _rightTriggerValue;

        // 最後にスナップショット更新したフレーム番号。
        public int SnapshotFrame { get; private set; } = -1;

        // このフレームでキーボードが存在したか。
        public bool HasKeyboard { get; private set; }

        // このフレームでゲームパッドが存在したか。
        public bool HasGamepad { get; private set; }

        // X:+右 / Y:+上 のデジタル移動ベクトル。
        public Vector2 KeyboardMoveVector => _keyboardMoveVector;

        // デッドゾーン適用後の左スティック。
        public Vector2 GamepadLeftStickVector => _gamepadLeftStickVector;

        // デッドゾーン適用後の右スティック。
        public Vector2 GamepadRightStickVector => _gamepadRightStickVector;

        // X:+右 / Y:+上 の DPad ベクトル。
        public Vector2 GamepadDpadVector => _gamepadDpadVector;

        // 左スティック + DPad を合成した移動ベクトル。
        public Vector2 GamepadMoveVector => _gamepadMoveVector;

        // 0..1 の左トリガー入力値。
        public float LeftTriggerValue => _leftTriggerValue;

        // 0..1 の右トリガー入力値。
        public float RightTriggerValue => _rightTriggerValue;

        private void Update()
        {
            // 低レベル入力は毎フレーム1回だけ収集する。
            // 他の Reader はこのスナップショットを参照する。
            UpdateKeyboardSnapshot();
            UpdateGamepadSnapshot();
            SnapshotFrame = Time.frameCount;
        }

        private void OnDisable()
        {
            // 無効化時は押下状態を持ち越さないよう初期化する。
            ClearAllSnapshots();
            SnapshotFrame = -1;
        }

        // ---------------------------------------------------------------------
        // Public API : Keyboard
        // ---------------------------------------------------------------------

        // 指定キーの Held / Pressed / Released をまとめて返す。
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

        // 指定キーが現在押されているか。
        public bool IsKeyHeld(Key key)
        {
            if (!TryGetKeyIndex(key, out int index))
            {
                return false;
            }

            return _currentKeyHeld[index];
        }

        // 指定キーがこのフレームで押された瞬間か。
        public bool WasKeyPressedThisFrame(Key key)
        {
            if (!TryGetKeyIndex(key, out int index))
            {
                return false;
            }

            return _currentKeyHeld[index] && !_previousKeyHeld[index];
        }

        // 指定キーがこのフレームで離された瞬間か。
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

        // 指定ゲームパッドボタンの Held / Pressed / Released をまとめて返す。
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

        // 指定ゲームパッドボタンが現在押されているか。
        public bool IsGamepadButtonHeld(RawGamepadButton button)
        {
            if (!TryGetGamepadButtonIndex(button, out int index))
            {
                return false;
            }

            return _currentGamepadHeld[index];
        }

        // 指定ゲームパッドボタンがこのフレームで押された瞬間か。
        public bool WasGamepadButtonPressedThisFrame(RawGamepadButton button)
        {
            if (!TryGetGamepadButtonIndex(button, out int index))
            {
                return false;
            }

            return _currentGamepadHeld[index] && !_previousGamepadHeld[index];
        }

        // 指定ゲームパッドボタンがこのフレームで離された瞬間か。
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
            // 前フレーム配列と現在フレーム配列を入れ替えることで、
            // コピーコストを抑えつつ previous を保持する。
            Swap(ref _currentKeyHeld, ref _previousKeyHeld);

            // 新しい current 側を空にする。
            Array.Clear(_currentKeyHeld, 0, _currentKeyHeld.Length);

            Keyboard keyboard = Keyboard.current;
            HasKeyboard = keyboard != null;

            if (!HasKeyboard)
            {
                _keyboardMoveVector = Vector2.zero;
                return;
            }

            // 接続中キーボードの全キー状態を current 配列へ詰める。
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

            // WASD + 矢印キーからデジタル移動ベクトルを生成する。
            _keyboardMoveVector = ReadKeyboardMoveVector();
        }

        private void UpdateGamepadSnapshot()
        {
            // 前フレーム配列と現在フレーム配列を入れ替える。
            Swap(ref _currentGamepadHeld, ref _previousGamepadHeld);

            // 新しい current 側を空にする。
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

            // Gamepad の各ボタン状態を enum 配列へ詰める。
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

            // トリガーはデジタル状態だけでなくアナログ値も保持する。
            _leftTriggerValue = gamepad.leftTrigger.ReadValue();
            _rightTriggerValue = gamepad.rightTrigger.ReadValue();

            // 左右スティックへデッドゾーンを適用する。
            _gamepadLeftStickVector = ApplyRadialDeadzone(gamepad.leftStick.ReadValue(), _leftStickDeadzone);
            _gamepadRightStickVector = ApplyRadialDeadzone(gamepad.rightStick.ReadValue(), _rightStickDeadzone);

            // DPad をデジタルベクトルとして読む。
            _gamepadDpadVector = ReadDpadVector();

            // 左スティック + DPad を移動入力として合成する。
            // 最終的な長さは 1.0 を超えないように制限する。
            _gamepadMoveVector = Vector2.ClampMagnitude(_gamepadLeftStickVector + _gamepadDpadVector, 1.0f);
        }

        // ---------------------------------------------------------------------
        // Read Helpers
        // ---------------------------------------------------------------------

        // WASD + 矢印キーからキーボード移動ベクトルを作る。
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

        // DPad の4方向ボタンからデジタルベクトルを作る。
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

        // 正負2方向の入力から -1 / 0 / +1 のデジタル軸値を作る。
        private int ReadDigitalAxis(Key positiveA, Key positiveB, Key negativeA, Key negativeB)
        {
            bool positive = IsKeyHeldInternal(positiveA) || IsKeyHeldInternal(positiveB);
            bool negative = IsKeyHeldInternal(negativeA) || IsKeyHeldInternal(negativeB);

            // 両方押し、または両方未押下は 0。
            if (positive == negative)
            {
                return 0;
            }

            return positive ? 1 : -1;
        }

        // 配列アクセス前提の内部用キー状態取得。
        private bool IsKeyHeldInternal(Key key)
        {
            return TryGetKeyIndex(key, out int index) && _currentKeyHeld[index];
        }

        // enum で指定したゲームパッドボタン状態を書き込む。
        private void SetGamepadButtonState(RawGamepadButton button, bool held)
        {
            _currentGamepadHeld[(int)button] = held;
        }

        // スティック入力へ放射状デッドゾーンを適用する。
        // デッドゾーン外は 0..1 に再正規化する。
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

            // デッドゾーンを除いた残りの範囲を 0..1 に圧縮して使う。
            float normalizedMagnitude = Mathf.Clamp01((magnitude - deadzone) / (1.0f - deadzone));
            return (value / magnitude) * normalizedMagnitude;
        }

        // ---------------------------------------------------------------------
        // Validation / Utility
        // ---------------------------------------------------------------------

        // Key enum を配列インデックスへ安全に変換する。
        private static bool TryGetKeyIndex(Key key, out int index)
        {
            index = (int)key;
            if (key == Key.None)
            {
                return false;
            }

            return (uint)index < (uint)s_keyArraySize;
        }

        // RawGamepadButton enum を配列インデックスへ安全に変換する。
        private static bool TryGetGamepadButtonIndex(RawGamepadButton button, out int index)
        {
            index = (int)button;
            return index >= 0 && index < GamepadButtonCount;
        }

        // すべてのスナップショット状態を初期化する。
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

        // 2つの配列参照を入れ替える。
        // previous <- old current
        // current  <- old previous
        private static void Swap(ref bool[] a, ref bool[] b)
        {
            bool[] tmp = a;
            a = b;
            b = tmp;
        }

        // Key enum の最大値を走査して必要配列サイズを決める。
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

            // Key enum の最大値に合わせて配列サイズを決める。
            return max + 1;
        }
    }
}