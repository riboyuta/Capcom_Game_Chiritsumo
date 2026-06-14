using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(AudioEventBinder))]
[CanEditMultipleObjects]
public sealed class AudioEventBinderEditor : Editor
{
    private static readonly Regex AudioEventEmitRegex =
        new Regex(@"\bAudioEvent\.Emit(?:At)?\s*\(\s*[^,]+,\s*""([^""]+)""", RegexOptions.Compiled);

    private static readonly Regex DirectEmitRegex =
        new Regex(@"\bEmit(?:At)?\s*\(\s*""([^""]+)""", RegexOptions.Compiled);

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Binding Tools", EditorStyles.boldLabel);

        GUIContent addMissingBindingsLabel = new GUIContent(
            "未登録イベントを Bindings に追加",
            "同じ GameObject のスクリプトから AudioEvent.Emit(this, \"Event\") を探し、Bindings に未登録のイベント名だけを追加します。");

        if (GUILayout.Button(addMissingBindingsLabel))
        {
            AddMissingBindingsForTargets();
        }

        GUIContent enableConfiguredActionsLabel = new GUIContent(
            "Audio ID 設定済み Actions を有効化",
            "選択中の AudioEventBinder で、Audio ID が入っているのに Enabled がオフの Action をまとめてオンにします。");

        if (GUILayout.Button(enableConfiguredActionsLabel))
        {
            EnableConfiguredActionsForTargets();
        }

        GUIContent refreshAudioIdsLabel = new GUIContent(
            "Audio ID 候補を更新",
            "AudioDef アセットを再スキャンして、Action の Audio ドロップダウン候補を更新します。");

        if (GUILayout.Button(refreshAudioIdsLabel))
        {
            AudioDefChoiceCache.Refresh();
            Repaint();
        }

        EditorGUILayout.HelpBox(
            "同じ GameObject 上の Component から、AudioEvent.Emit(this, \"Event\") / AudioEvent.EmitAt(this, \"Event\", position) を探します。追加されるのは Event Name だけです。実際に鳴らす Audio ID は Bindings の Action 側で設定してください。音が鳴らない場合は、Action の Enabled がオンになっているか確認してください。",
            MessageType.Info);
    }

    private void AddMissingBindingsForTargets()
    {
        int totalAdded = 0;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] is not AudioEventBinder binder)
            {
                continue;
            }

            totalAdded += AddMissingBindings(binder);
        }

        Debug.Log($"[AudioEventBinderEditor] Added {totalAdded} missing audio event binding(s).");
    }

    private void EnableConfiguredActionsForTargets()
    {
        int totalEnabled = 0;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] is not AudioEventBinder binder)
            {
                continue;
            }

            totalEnabled += EnableConfiguredActions(binder);
        }

        Debug.Log($"[AudioEventBinderEditor] Enabled {totalEnabled} configured audio action(s).");
    }

    private static int AddMissingBindings(AudioEventBinder binder)
    {
        if (binder == null)
        {
            return 0;
        }

        SortedSet<string> discoveredEvents = DiscoverEventNames(binder);
        if (discoveredEvents.Count == 0)
        {
            return 0;
        }

        SerializedObject binderObject = new SerializedObject(binder);
        SerializedProperty bindings = binderObject.FindProperty("bindings");
        if (bindings == null || !bindings.isArray)
        {
            return 0;
        }

        HashSet<string> existingEvents = CollectExistingEventNames(bindings);
        int added = 0;

        Undo.RecordObject(binder, "Add Missing Audio Event Bindings");

        foreach (string eventName in discoveredEvents)
        {
            if (existingEvents.Contains(eventName))
            {
                continue;
            }

            AddBinding(bindings, eventName);
            existingEvents.Add(eventName);
            added++;
        }

        if (added <= 0)
        {
            return 0;
        }

        binderObject.ApplyModifiedProperties();
        binder.RebuildLookup();
        EditorUtility.SetDirty(binder);
        PrefabUtility.RecordPrefabInstancePropertyModifications(binder);

        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(binder.gameObject.scene);
        }

        return added;
    }

    private static int EnableConfiguredActions(AudioEventBinder binder)
    {
        if (binder == null)
        {
            return 0;
        }

        SerializedObject binderObject = new SerializedObject(binder);
        SerializedProperty bindings = binderObject.FindProperty("bindings");
        if (bindings == null || !bindings.isArray)
        {
            return 0;
        }

        Undo.RecordObject(binder, "Enable Configured Audio Actions");

        int enabledCount = 0;
        for (int i = 0; i < bindings.arraySize; i++)
        {
            SerializedProperty binding = bindings.GetArrayElementAtIndex(i);
            SerializedProperty actions = binding.FindPropertyRelative("actions");
            if (actions == null || !actions.isArray)
            {
                continue;
            }

            for (int j = 0; j < actions.arraySize; j++)
            {
                SerializedProperty action = actions.GetArrayElementAtIndex(j);
                SerializedProperty enabled = action.FindPropertyRelative("enabled");
                SerializedProperty audioId = action.FindPropertyRelative("audioId");

                if (enabled == null
                    || audioId == null
                    || enabled.boolValue
                    || string.IsNullOrWhiteSpace(audioId.stringValue))
                {
                    continue;
                }

                enabled.boolValue = true;
                enabledCount++;
            }
        }

        if (enabledCount <= 0)
        {
            return 0;
        }

        binderObject.ApplyModifiedProperties();
        binder.RebuildLookup();
        EditorUtility.SetDirty(binder);
        PrefabUtility.RecordPrefabInstancePropertyModifications(binder);

        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(binder.gameObject.scene);
        }

        return enabledCount;
    }

    private static SortedSet<string> DiscoverEventNames(AudioEventBinder binder)
    {
        SortedSet<string> eventNames = new SortedSet<string>(StringComparer.Ordinal);
        Component[] components = binder.GetComponents<Component>();

        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null || component == binder)
            {
                continue;
            }

            CollectDirectEmitCalls(component, eventNames);
        }

        return eventNames;
    }

    private static void CollectDirectEmitCalls(Component component, ISet<string> eventNames)
    {
        if (component is not MonoBehaviour monoBehaviour)
        {
            return;
        }

        MonoScript script = MonoScript.FromMonoBehaviour(monoBehaviour);
        if (script == null)
        {
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(script);
        if (string.IsNullOrEmpty(assetPath))
        {
            return;
        }

        string absolutePath = Path.GetFullPath(assetPath);
        if (!File.Exists(absolutePath))
        {
            return;
        }

        string source = File.ReadAllText(absolutePath);
        CollectMatches(AudioEventEmitRegex.Matches(source), eventNames);
        CollectMatches(DirectEmitRegex.Matches(source), eventNames);
    }

    private static void CollectMatches(MatchCollection matches, ISet<string> eventNames)
    {
        for (int i = 0; i < matches.Count; i++)
        {
            AddEventName(matches[i].Groups[1].Value, eventNames);
        }
    }

    private static HashSet<string> CollectExistingEventNames(SerializedProperty bindings)
    {
        HashSet<string> existingEvents = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < bindings.arraySize; i++)
        {
            SerializedProperty element = bindings.GetArrayElementAtIndex(i);
            SerializedProperty eventName = element.FindPropertyRelative("eventName");
            if (eventName == null)
            {
                continue;
            }

            AddEventName(eventName.stringValue, existingEvents);
        }

        return existingEvents;
    }

    private static void AddBinding(SerializedProperty bindings, string eventName)
    {
        int index = bindings.arraySize;
        bindings.InsertArrayElementAtIndex(index);

        SerializedProperty element = bindings.GetArrayElementAtIndex(index);
        SerializedProperty eventNameProperty = element.FindPropertyRelative("eventName");
        SerializedProperty actionsProperty = element.FindPropertyRelative("actions");

        if (eventNameProperty != null)
        {
            eventNameProperty.stringValue = eventName;
        }

        if (actionsProperty != null && actionsProperty.isArray)
        {
            actionsProperty.arraySize = 0;
        }
    }

    private static void AddEventName(string eventName, ISet<string> eventNames)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return;
        }

        eventNames.Add(eventName.Trim());
    }
}

[CustomPropertyDrawer(typeof(AudioEventBinder.AudioEventAction))]
public sealed class AudioEventActionDrawer : PropertyDrawer
{
    private static readonly GUIContent AudioLabel = new GUIContent(
        "Audio",
        "AudioDef に登録されている音声を選びます。保存される値は AudioManager が使う Audio ID です。");

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        InitializeNewActionDefaults(property);

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        Rect lineRect = new Rect(position.x, position.y, position.width, lineHeight);

        property.isExpanded = EditorGUI.Foldout(lineRect, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;

        DrawPropertyLine(ref lineRect, spacing, property.FindPropertyRelative("enabled"));
        DrawAudioIdLine(ref lineRect, spacing, property.FindPropertyRelative("audioId"));
        DrawPropertyLine(ref lineRect, spacing, property.FindPropertyRelative("playMode"));
        DrawPropertyLine(ref lineRect, spacing, property.FindPropertyRelative("overrideVolume"));
        DrawPropertyLine(ref lineRect, spacing, property.FindPropertyRelative("volume"));
        DrawPropertyLine(ref lineRect, spacing, property.FindPropertyRelative("delay"));
        DrawPropertyLine(ref lineRect, spacing, property.FindPropertyRelative("cooldown"));
        DrawPropertyLine(ref lineRect, spacing, property.FindPropertyRelative("fadeDuration"));
        DrawPropertyLine(ref lineRect, spacing, property.FindPropertyRelative("spatialOrigin"));

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int lineCount = property.isExpanded ? 10 : 1;
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        return (lineHeight * lineCount) + (spacing * (lineCount - 1));
    }

    private static void DrawPropertyLine(ref Rect lineRect, float spacing, SerializedProperty property)
    {
        MoveToNextLine(ref lineRect, spacing);
        EditorGUI.PropertyField(lineRect, property);
    }

    private static void InitializeNewActionDefaults(SerializedProperty property)
    {
        SerializedProperty enabled = property.FindPropertyRelative("enabled");
        SerializedProperty audioId = property.FindPropertyRelative("audioId");

        if (enabled == null || audioId == null)
        {
            return;
        }

        // Inspector の配列追加では bool が false で生成される場合がある。
        // まだ Audio ID 未設定の空 Action だけオンにして、設定済みActionの意図的な無効化は尊重する。
        if (!enabled.boolValue && string.IsNullOrWhiteSpace(audioId.stringValue))
        {
            enabled.boolValue = true;
        }
    }

    private static void DrawAudioIdLine(ref Rect lineRect, float spacing, SerializedProperty audioIdProperty)
    {
        MoveToNextLine(ref lineRect, spacing);

        AudioDefChoice[] choices = AudioDefChoiceCache.GetChoices();
        string currentId = audioIdProperty.stringValue ?? "";
        int selectedIndex = BuildAudioOptions(choices, currentId, out GUIContent[] options, out bool currentIdIsMissing);

        bool oldShowMixedValue = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = audioIdProperty.hasMultipleDifferentValues;

        EditorGUI.BeginChangeCheck();
        int newIndex = EditorGUI.Popup(lineRect, AudioLabel, selectedIndex, options);
        if (EditorGUI.EndChangeCheck())
        {
            audioIdProperty.stringValue = ResolveSelectedAudioId(choices, newIndex, currentId, currentIdIsMissing);
        }

        EditorGUI.showMixedValue = oldShowMixedValue;
    }

    private static void MoveToNextLine(ref Rect lineRect, float spacing)
    {
        lineRect.y += EditorGUIUtility.singleLineHeight + spacing;
    }

    private static int BuildAudioOptions(
        AudioDefChoice[] choices,
        string currentId,
        out GUIContent[] options,
        out bool currentIdIsMissing)
    {
        int foundIndex = FindChoiceIndex(choices, currentId);
        currentIdIsMissing = !string.IsNullOrEmpty(currentId) && foundIndex < 0;

        int extraMissingOption = currentIdIsMissing ? 1 : 0;
        options = new GUIContent[1 + extraMissingOption + choices.Length];
        options[0] = new GUIContent("<未設定>");

        int choiceStartIndex = 1;
        if (currentIdIsMissing)
        {
            options[1] = new GUIContent($"[AudioDef未登録] {currentId}");
            choiceStartIndex = 2;
        }

        for (int i = 0; i < choices.Length; i++)
        {
            options[choiceStartIndex + i] = new GUIContent(choices[i].DisplayName);
        }

        if (currentIdIsMissing)
        {
            return 1;
        }

        return foundIndex >= 0 ? choiceStartIndex + foundIndex : 0;
    }

    private static string ResolveSelectedAudioId(
        AudioDefChoice[] choices,
        int selectedIndex,
        string currentId,
        bool currentIdIsMissing)
    {
        if (selectedIndex <= 0)
        {
            return "";
        }

        int choiceIndex = selectedIndex - 1;
        if (currentIdIsMissing)
        {
            if (selectedIndex == 1)
            {
                return currentId;
            }

            choiceIndex = selectedIndex - 2;
        }

        return choiceIndex >= 0 && choiceIndex < choices.Length ? choices[choiceIndex].Id : currentId;
    }

    private static int FindChoiceIndex(AudioDefChoice[] choices, string audioId)
    {
        if (string.IsNullOrEmpty(audioId))
        {
            return -1;
        }

        for (int i = 0; i < choices.Length; i++)
        {
            if (choices[i].Id == audioId)
            {
                return i;
            }
        }

        return -1;
    }
}

internal readonly struct AudioDefChoice
{
    public AudioDefChoice(string id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
    }

    public string Id { get; }
    public string DisplayName { get; }
}

internal static class AudioDefChoiceCache
{
    private const double CacheLifetimeSeconds = 2.0;

    private static AudioDefChoice[] cachedChoices = Array.Empty<AudioDefChoice>();
    private static double nextRefreshTime;

    public static AudioDefChoice[] GetChoices()
    {
        if (EditorApplication.timeSinceStartup >= nextRefreshTime)
        {
            Refresh();
        }

        return cachedChoices;
    }

    public static void Refresh()
    {
        string[] guids = AssetDatabase.FindAssets("t:AudioDef");
        List<AudioDefChoice> choices = new List<AudioDefChoice>();
        HashSet<string> registeredIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AudioDef audioDef = AssetDatabase.LoadAssetAtPath<AudioDef>(path);
            if (audioDef == null || audioDef.entries == null)
            {
                continue;
            }

            for (int j = 0; j < audioDef.entries.Length; j++)
            {
                AudioDef.Entry entry = audioDef.entries[j];
                if (entry == null || string.IsNullOrWhiteSpace(entry.id))
                {
                    continue;
                }

                string id = entry.id.Trim();
                if (!registeredIds.Add(id))
                {
                    continue;
                }

                choices.Add(new AudioDefChoice(id, BuildDisplayName(entry, audioDef)));
            }
        }

        choices.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal));
        cachedChoices = choices.ToArray();
        nextRefreshTime = EditorApplication.timeSinceStartup + CacheLifetimeSeconds;
    }

    private static string BuildDisplayName(AudioDef.Entry entry, AudioDef audioDef)
    {
        string clipName = entry.clip != null ? entry.clip.name : "<Clip未設定>";
        string loopSuffix = entry.loop ? " / Loop" : "";
        return $"{entry.channel} / {clipName} / {entry.id}{loopSuffix}";
    }
}
