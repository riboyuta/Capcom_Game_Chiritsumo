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

    private SerializedProperty handMovementSettings;

    private void OnEnable()
    {
        enemyType = serializedObject.FindProperty("enemyType");
        enemyRoot = serializedObject.FindProperty("enemyRoot");
        applySystemDefault = serializedObject.FindProperty("applySystemDefault");
        logMissingTarget = serializedObject.FindProperty("logMissingTarget");

        handMovementSettings = serializedObject.FindProperty("handMovementSettings");
    }

    private void DrawSelectedEnemyOverrides()
    {
        RoomEnemyType selectedType = (RoomEnemyType)enemyType.enumValueIndex;

        switch (selectedType)
        {
            case RoomEnemyType.HandChaser:
                DrawHandChaserOverrides();
                break;

            case RoomEnemyType.ShadowChaser:
                // ShadowChaser の Room 別上書き項目を追加する場合はここに書く
                break;

            case RoomEnemyType.SonarCharger:
                // SonarCharger の Room 別上書き項目を追加する場合はここに書く
                break;

            case RoomEnemyType.None:
            default:
                break;
        }
    }

    private void DrawHandChaserOverrides()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("HandChaser 移動設定", EditorStyles.boldLabel);

        if (handMovementSettings == null)
        {
            return;
        }

        SerializedProperty moveSpeed =
            handMovementSettings.FindPropertyRelative("moveSpeed");

        SerializedProperty moveDirection =
            handMovementSettings.FindPropertyRelative("moveDirection");

        SerializedProperty customMoveAxis =
            handMovementSettings.FindPropertyRelative("customMoveAxis");

        EditorGUILayout.PropertyField(moveSpeed);
        EditorGUILayout.PropertyField(moveDirection);

        MoveDirection selectedDirection = (MoveDirection)moveDirection.enumValueIndex;

        if (selectedDirection == MoveDirection.Custom)
        {
            EditorGUILayout.PropertyField(customMoveAxis);
        }
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

        DrawSelectedEnemyOverrides();

        EditorGUILayout.Space(8);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif