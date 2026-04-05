using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RailGimmick))]
public class RailGimmickEditor : Editor
{
    private RailGimmick rail;

    private bool showTools = true;
    private bool showGenerators = true;

    // パラメータ群
    private float genScale = 5f;
    private float lineLength = 10f;
    private float lineAngle = 0f;
    private int waveCount = 2;
    private bool flipY = false;

    private void OnEnable()
    {
        rail = (RailGimmick)target;
    }

    public override void OnInspectorGUI()
    {
        // デフォルトのプロパティ（Waypointsの生リストなど）を表示
        DrawDefaultInspector();

        GUILayout.Space(15);
        
        GUIStyle headerStyle = new GUIStyle(EditorStyles.foldout);
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.fontSize = 12;

        // --- 1. 基本ツールパネル ---
        showTools = EditorGUILayout.Foldout(showTools, "✍ 基本ツール (Basic Tools)", true, headerStyle);
        if (showTools)
        {
            EditorGUI.indentLevel++;
            GUILayout.BeginHorizontal();
            
            if (GUILayout.Button("ポイントを1つ末尾に追加", GUILayout.Height(25)))
            {
                Undo.RecordObject(rail, "Add Rail Waypoint");
                if (rail.localWaypoints.Count > 0)
                {
                    if (rail.localWaypoints.Count >= 2)
                    {
                        Vector3 pLast = rail.localWaypoints[rail.localWaypoints.Count - 1];
                        Vector3 pPrev = rail.localWaypoints[rail.localWaypoints.Count - 2];
                        Vector3 dir = (pLast - pPrev).normalized;
                        if (dir == Vector3.zero) dir = Vector3.right;
                        rail.localWaypoints.Add(pLast + dir * 3f);
                    }
                    else
                    {
                        Vector3 lastPoint = rail.localWaypoints[rail.localWaypoints.Count - 1];
                        rail.localWaypoints.Add(lastPoint + new Vector3(3f, 0f, 0f));
                    }
                }
                else
                {
                    rail.localWaypoints.Add(Vector3.zero);
                }
                EditorUtility.SetDirty(rail);
            }

            if (rail.localWaypoints.Count > 2)
            {
                if (GUILayout.Button("末尾のポイントを削除", GUILayout.Height(25)))
                {
                    Undo.RecordObject(rail, "Remove Rail Waypoint");
                    rail.localWaypoints.RemoveAt(rail.localWaypoints.Count - 1);
                    EditorUtility.SetDirty(rail);
                }
            }
            GUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
            GUILayout.Space(10);
        }

        // --- 2. パターンビルダーパネル ---
        showGenerators = EditorGUILayout.Foldout(showGenerators, "🛠 パターンビルダー (Auto Generators)", true, headerStyle);
        if (showGenerators)
        {
            EditorGUI.indentLevel++;
            
            EditorGUILayout.HelpBox("末尾のポイントから、決められた形状のパスを自動で連結生成します。", MessageType.Info);
            
            // 共通オプション
            flipY = EditorGUILayout.Toggle("上下反転生成 (Flip Y)", flipY);
            float signY = flipY ? -1f : 1f;

            GUILayout.Space(10);
            
            // 【直線生成】
            GUILayout.Label("1. 指定長の直線 (Straight Line)", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            lineLength = EditorGUILayout.FloatField("長さ", lineLength);
            lineAngle = EditorGUILayout.FloatField("角度(度)", lineAngle);
            GUILayout.EndHorizontal();
            
            GUILayout.Space(2);
            if (GUILayout.Button("＋ 直線を追加"))
            {
                Undo.RecordObject(rail, "Add Straight Line");
                Vector3 basePos = GetBasePos();
                float rad = lineAngle * Mathf.Deg2Rad;
                // 上下反転オプションを考慮
                Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad) * signY, 0f);
                rail.localWaypoints.Add(basePos + dir * lineLength);
                EditorUtility.SetDirty(rail);
            }

            GUILayout.Space(10);
            
            // 【スケール設定】
            GUILayout.Label("以下のカーブ・波系の共通スケール", EditorStyles.boldLabel);
            genScale = EditorGUILayout.FloatField("生成スケール (Radius/Scale)", genScale);
            
            GUILayout.Space(5);

            // 【波型】
            GUILayout.Label("2. 波型・コブ (Wave Bumps)", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            waveCount = EditorGUILayout.IntField("発生させる波の数", waveCount);
            if (GUILayout.Button("＋ 波型を追加", GUILayout.Width(130))) { GenerateWaveShape(signY); }
            GUILayout.EndHorizontal();
            
            GUILayout.Space(5);

            // 【S字・U字・交差】
            GUILayout.Label("3. カーブ・ループ系 (Curves & Loops)", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("＋ S字上昇坂\n(S-Curve)", GUILayout.Height(35))) { GenerateSCurveShape(signY); }
            if (GUILayout.Button("＋ 折り返し半円\n(U-Turn)", GUILayout.Height(35))) { GenerateUTurnShape(signY); }
            if (GUILayout.Button("＋ 交差一回転\n(Loop-De-Loop)", GUILayout.Height(35))) { GenerateLoopDeLoopShape(signY); }
            GUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }
    }

    private Vector3 GetBasePos()
    {
        if (rail.localWaypoints.Count > 0)
            return rail.localWaypoints[rail.localWaypoints.Count - 1];
        return Vector3.zero;
    }

    private void GenerateWaveShape(float signY)
    {
        Undo.RecordObject(rail, "Generate Wave");
        Vector3 basePos = GetBasePos();
        int pointsPerWave = 6;
        int totalPoints = waveCount * pointsPerWave;
        // 波1つの横幅はスケール*3とする
        float waveWidth = genScale * 3f; 
        
        for (int i = 1; i <= totalPoints; i++)
        {
            float t = (float)i / pointsPerWave; // 何波目か (0.0 ~ waveCount)
            float x = t * waveWidth;
            // sin波 (1周 2PI)
            float y = Mathf.Sin(t * Mathf.PI * 2f) * genScale * signY;
            rail.localWaypoints.Add(basePos + new Vector3(x, y, 0f));
        }
        EditorUtility.SetDirty(rail);
    }

    private void GenerateSCurveShape(float signY)
    {
        Undo.RecordObject(rail, "Generate S-Curve");
        Vector3 basePos = GetBasePos();
        int points = 8;
        float width = genScale * 3f;
        float height = genScale * 2f; // 高さ
        
        for (int i = 1; i <= points; i++)
        {
            float t = (float)i / points;
            float x = t * width;
            // Cosine補間でなめらかなS字 0 -> 1 (Easing In-Out)
            float f = (1f - Mathf.Cos(t * Mathf.PI)) * 0.5f;
            float y = f * height * signY;
            rail.localWaypoints.Add(basePos + new Vector3(x, y, 0f));
        }
        EditorUtility.SetDirty(rail);
    }

    private void GenerateUTurnShape(float signY)
    {
        Undo.RecordObject(rail, "Generate U-Turn");
        Vector3 basePos = GetBasePos();
        int points = 8;
        float r = genScale;
        
        // 中心座標を少し右奥に取ることで壁をU字で駆け上がるような形に
        float startAngle = -Mathf.PI * 0.5f;
        float sweepAngle = Mathf.PI;
        
        // 反転時
        if (signY < 0f)
        {
            startAngle = Mathf.PI * 0.5f;
            sweepAngle = -Mathf.PI;
        }

        for (int i = 1; i <= points; i++)
        {
            float t = (float)i / points;
            float angle = startAngle + sweepAngle * t;
            
            float x = Mathf.Cos(angle) * r;
            float y = Mathf.Sin(angle) * r + (r * signY); 
            
            rail.localWaypoints.Add(basePos + new Vector3(x, y, 0f));
        }
        
        // Uターンが完全に逆向きになったあと、直線的に脱出する部分を少し伸ばす
        float endAngle = startAngle + sweepAngle;
        rail.localWaypoints.Add(basePos + new Vector3(Mathf.Cos(endAngle)*r - r*2.0f, Mathf.Sin(endAngle)*r + (r*signY), 0f));

        EditorUtility.SetDirty(rail);
    }

    private void GenerateLoopDeLoopShape(float signY)
    {
        Undo.RecordObject(rail, "Generate Loop-De-Loop");
        float r = genScale;
        Vector3 basePos = GetBasePos();
        int circlePoints = 12;

        float entryOffset = r * 1.5f;
        rail.localWaypoints.Add(basePos + new Vector3(entryOffset, 0f, 0f));
        
        // ループ部分
        for (int i = 1; i <= circlePoints; i++)
        {
            float angle = -Mathf.PI * 0.5f + (Mathf.PI * 2f * i / circlePoints);
            // 反転時は時計回りにループする
            if (signY < 0f) angle = Mathf.PI * 0.5f - (Mathf.PI * 2f * i / circlePoints);

            float x = entryOffset + Mathf.Cos(angle) * r;
            float y = Mathf.Sin(angle) * r + (r * signY);
            rail.localWaypoints.Add(basePos + new Vector3(x, y, 0f));
        }
        
        rail.localWaypoints.Add(basePos + new Vector3(entryOffset + r * 2.0f, 0f, 0f));
        rail.localWaypoints.Add(basePos + new Vector3(entryOffset + r * 3.5f, 0f, 0f));
        EditorUtility.SetDirty(rail);
    }

    private void OnSceneGUI()
    {
        if (rail == null || rail.localWaypoints == null) return;

        // Scene上に各ポイントの操作ハンドルを描画する
        for (int i = 0; i < rail.localWaypoints.Count; i++)
        {
            Vector3 worldPos = rail.transform.TransformPoint(rail.localWaypoints[i]);
            
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.fontStyle = FontStyle.Bold;
            Handles.Label(worldPos + Vector3.up * 0.5f, $"P{i}", style);

            EditorGUI.BeginChangeCheck();
            Vector3 newWorldPos = Handles.PositionHandle(worldPos, Quaternion.identity);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(rail, "Move Rail Waypoint");
                rail.localWaypoints[i] = rail.transform.InverseTransformPoint(newWorldPos);
                EditorUtility.SetDirty(rail); 
            }
        }
    }
}
