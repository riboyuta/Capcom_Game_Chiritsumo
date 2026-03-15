using System;

namespace Game.Input
{
    public sealed class DebugInputReader
    {
        

        private bool IsDebugInputEnabled =
#if UNITY_EDITOR 
            true;
#else
            false;
#endif

        private readonly RawInputSource _rawInputSource;
        private readonly DebugInputBindings _bindings;

        public bool ToggleDebugViewPressed
        {
            get
            {
                if (!IsDebugInputEnabled)
                {
                    return false;
                }

                return _rawInputSource.WasKeyPressedThisFrame(_bindings.ToggleDebugViewKey);
            }
        }

        public bool ReloadScenePressed
        {
            get
            {
                if (!IsDebugInputEnabled)
                {
                    return false;
                }

                return _rawInputSource.WasKeyPressedThisFrame(_bindings.ReloadSceneKey);
            }
        }

        public bool NextScenePressed
        {
            get
            {
                if (!IsDebugInputEnabled)
                {
                    return false;
                }

                return _rawInputSource.WasKeyPressedThisFrame(_bindings.NextSceneKey);
            }
        }

        public bool PreviousScenePressed
        {
            get
            {
                if (!IsDebugInputEnabled)
                {
                    return false;
                }

                return _rawInputSource.WasKeyPressedThisFrame(_bindings.PreviousSceneKey);
            }
        }

        public DebugInputReader(RawInputSource rawInputSource, DebugInputBindings bindings)
        {
            _rawInputSource = rawInputSource ?? throw new ArgumentNullException(nameof(rawInputSource));
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        }
    }
}
