using System;

namespace Game.Input
{
    public sealed class DebugInputReader
    {
        private readonly RawInputSource _rawInputSource;
        private readonly DebugInputBindings _bindings;

        public bool ToggleDebugViewPressed =>
            _rawInputSource.WasKeyPressedThisFrame(_bindings.ToggleDebugViewKey);

        public bool ReloadScenePressed =>
            _rawInputSource.WasKeyPressedThisFrame(_bindings.ReloadSceneKey);

        public bool NextScenePressed =>
            _rawInputSource.WasKeyPressedThisFrame(_bindings.NextSceneKey);

        public bool PreviousScenePressed =>
            _rawInputSource.WasKeyPressedThisFrame(_bindings.PreviousSceneKey);

        public DebugInputReader(RawInputSource rawInputSource, DebugInputBindings bindings)
        {
            _rawInputSource = rawInputSource ?? throw new ArgumentNullException(nameof(rawInputSource));
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        }
    }
}