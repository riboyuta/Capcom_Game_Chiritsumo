#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RoomEnemySetting))]
public sealed class RoomEnemySettingEditor : Editor
{
    private SerializedProperty enemyType;
    private SerializedProperty enemyRoot;
    private SerializedProperty applySystemDefault;
    private SerializedProperty logMissingTarget;

    private void OnEnable()
    {
        enemyType = serializedObject.FindProperty("enemyType");
        enemyRoot = serializedObject.FindProperty("enemyRoot");
        applySystemDefault = serializedObject.FindProperty("applySystemDefault");
        logMissingTarget = serializedObject.FindProperty("logMissingTarget");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Room Enemy Setting", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.PropertyField(enemyType);
        EditorGUILayout.PropertyField(enemyRoot);
        EditorGUILayout.PropertyField(applySystemDefault);
        EditorGUILayout.PropertyField(logMissingTarget);

        EditorGUILayout.Space(8);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif