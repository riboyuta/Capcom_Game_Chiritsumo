using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Input
{
    [Serializable]
    public sealed class DebugInputBindings
    {
        [Header("Debug Input Config")]
        [SerializeField]
        private Key _toggleDebugViewKey = Key.F1;

        [SerializeField]
        private Key _reloadSceneKey = Key.F8;

        [SerializeField]
        private Key _nextSceneKey = Key.F9;

        [SerializeField]
        private Key _previousSceneKey = Key.F10;

        public Key ToggleDebugViewKey => _toggleDebugViewKey;
        public Key ReloadSceneKey => _reloadSceneKey;
        public Key NextSceneKey => _nextSceneKey;
        public Key PreviousSceneKey => _previousSceneKey;
    }
}
