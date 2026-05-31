#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RoomEnemySystem))]
public sealed class RoomEnemySystemEditor : Editor
{
    private SerializedProperty editingEnemyType;
    private SerializedProperty applyDefaultsOnStart;
    private SerializedProperty searchRoot;

    private SerializedProperty defaultHandChaserSettings;
    private SerializedProperty defaultShadowChaserSettings;
    private SerializedProperty defaultSonarChargerSettings;

    private SerializedProperty logApplyResult;

    private void OnEnable()
    {
        editingEnemyType = serializedObject.FindProperty("editingEnemyType");
        applyDefaultsOnStart = serializedObject.FindProperty("applyDefaultsOnStart");
        searchRoot = serializedObject.FindProperty("searchRoot");

        defaultHandChaserSettings = serializedObject.FindProperty("defaultHandChaserSettings");
        defaultShadowChaserSettings = serializedObject.FindProperty("defaultShadowChaserSettings");
        defaultSonarChargerSettings = serializedObject.FindProperty("defaultSonarChargerSettings");

        logApplyResult = serializedObject.FindProperty("logApplyResult");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("RoomEnemySystem", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.PropertyField(applyDefaultsOnStart);
        EditorGUILayout.PropertyField(searchRoot);
        EditorGUILayout.PropertyField(logApplyResult);

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("デフォルト設定編集", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(editingEnemyType);

        EditorGUILayout.Space(6);

        RoomEnemyType selectedType = (RoomEnemyType)editingEnemyType.enumValueIndex;

        switch (selectedType)
        {
            case RoomEnemyType.HandChaser:
                DrawHandChaserSettings();
                break;

            case RoomEnemyType.ShadowChaser:
                DrawShadowChaserSettings();
                break;

            case RoomEnemyType.SonarCharger:
                DrawSonarChargerSettings();
                break;

            case RoomEnemyType.None:
            default:
                EditorGUILayout.HelpBox(
                    "表示する敵設定が選択されていません。",
                    MessageType.Info);
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawHandChaserSettings()
    {
        if (defaultHandChaserSettings != null)
        {
            EditorGUILayout.PropertyField(defaultHandChaserSettings, true);
        }
    }

    private void DrawShadowChaserSettings()
    {
        if (defaultShadowChaserSettings != null)
        {
            EditorGUILayout.PropertyField(defaultShadowChaserSettings, true);
        }
    }

    private void DrawSonarChargerSettings()
    {
        if (defaultSonarChargerSettings != null)
        {
            EditorGUILayout.PropertyField(defaultSonarChargerSettings, true);
        }
    }
}
#endif