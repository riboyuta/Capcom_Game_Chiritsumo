using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class SafeZoneWireframeView : MonoBehaviour
{
    private const int WireframeSegments = 24;
    private const int CapsuleEdgeCount = 4;

    [Header("表示設定")]
    [Tooltip("セーフゾーンのワイヤーフレームを表示するかです。")]
    [SerializeField] private bool visible = true;

    [Tooltip("ワイヤーフレームの表示色です。")]
    [SerializeField] private Color wireframeColor = new Color(0f, 1f, 0f, 0.8f);

    private Collider targetCollider;
    private Material lineMaterial;

    private void Awake()
    {
        EnsureTargetCollider();
        CreateLineMaterialIfNeeded();
    }

    private void OnEnable()
    {
        EnsureTargetCollider();
        CreateLineMaterialIfNeeded();
    }

    private void OnDisable()
    {
        ReleaseLineMaterial();
    }

    private void OnDestroy()
    {
        ReleaseLineMaterial();
    }

    private void OnValidate()
    {
        EnsureTargetCollider();
    }

    private void OnRenderObject()
    {
        if (!visible)
        {
            return;
        }

        EnsureTargetCollider();

        if (targetCollider == null || !targetCollider.enabled)
        {
            return;
        }

        CreateLineMaterialIfNeeded();

        if (lineMaterial == null)
        {
            return;
        }

        lineMaterial.SetPass(0);

        GL.PushMatrix();
        GL.MultMatrix(transform.localToWorldMatrix);
        GL.Begin(GL.LINES);
        GL.Color(wireframeColor);

        if (targetCollider is BoxCollider boxCollider)
        {
            DrawBoxColliderWireframe(boxCollider);
        }
        else if (targetCollider is SphereCollider sphereCollider)
        {
            DrawSphereColliderWireframe(sphereCollider);
        }
        else if (targetCollider is CapsuleCollider capsuleCollider)
        {
            DrawCapsuleColliderWireframe(capsuleCollider);
        }

        GL.End();
        GL.PopMatrix();
    }

    private void EnsureTargetCollider()
    {
        if (targetCollider != null)
        {
            return;
        }

        targetCollider = GetComponent<Collider>();
    }

    private void CreateLineMaterialIfNeeded()
    {
        if (lineMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("Hidden/Internal-Colored");

        if (shader == null)
        {
            return;
        }

        lineMaterial = new Material(shader);
        lineMaterial.hideFlags = HideFlags.HideAndDontSave;
        lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        lineMaterial.SetInt("_ZWrite", 0);
    }

    private void ReleaseLineMaterial()
    {
        if (lineMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(lineMaterial);
        }
        else
        {
            DestroyImmediate(lineMaterial);
        }

        lineMaterial = null;
    }

    private void DrawBoxColliderWireframe(BoxCollider boxCollider)
    {
        Vector3 center = boxCollider.center;
        Vector3 halfSize = boxCollider.size * 0.5f;

        Vector3 v0 = center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z);
        Vector3 v1 = center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z);
        Vector3 v2 = center + new Vector3(halfSize.x, halfSize.y, -halfSize.z);
        Vector3 v3 = center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z);
        Vector3 v4 = center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z);
        Vector3 v5 = center + new Vector3(halfSize.x, -halfSize.y, halfSize.z);
        Vector3 v6 = center + new Vector3(halfSize.x, halfSize.y, halfSize.z);
        Vector3 v7 = center + new Vector3(-halfSize.x, halfSize.y, halfSize.z);

        GL.Vertex(v0); GL.Vertex(v1);
        GL.Vertex(v1); GL.Vertex(v5);
        GL.Vertex(v5); GL.Vertex(v4);
        GL.Vertex(v4); GL.Vertex(v0);

        GL.Vertex(v3); GL.Vertex(v2);
        GL.Vertex(v2); GL.Vertex(v6);
        GL.Vertex(v6); GL.Vertex(v7);
        GL.Vertex(v7); GL.Vertex(v3);

        GL.Vertex(v0); GL.Vertex(v3);
        GL.Vertex(v1); GL.Vertex(v2);
        GL.Vertex(v5); GL.Vertex(v6);
        GL.Vertex(v4); GL.Vertex(v7);
    }

    private void DrawSphereColliderWireframe(SphereCollider sphereCollider)
    {
        Vector3 center = sphereCollider.center;
        float radius = sphereCollider.radius;

        for (int i = 0; i < WireframeSegments; i++)
        {
            float angle1 = i / (float)WireframeSegments * Mathf.PI * 2f;
            float angle2 = (i + 1) / (float)WireframeSegments * Mathf.PI * 2f;

            Vector3 xy1 = center + new Vector3(Mathf.Cos(angle1) * radius, Mathf.Sin(angle1) * radius, 0f);
            Vector3 xy2 = center + new Vector3(Mathf.Cos(angle2) * radius, Mathf.Sin(angle2) * radius, 0f);

            Vector3 xz1 = center + new Vector3(Mathf.Cos(angle1) * radius, 0f, Mathf.Sin(angle1) * radius);
            Vector3 xz2 = center + new Vector3(Mathf.Cos(angle2) * radius, 0f, Mathf.Sin(angle2) * radius);

            Vector3 yz1 = center + new Vector3(0f, Mathf.Cos(angle1) * radius, Mathf.Sin(angle1) * radius);
            Vector3 yz2 = center + new Vector3(0f, Mathf.Cos(angle2) * radius, Mathf.Sin(angle2) * radius);

            GL.Vertex(xy1); GL.Vertex(xy2);
            GL.Vertex(xz1); GL.Vertex(xz2);
            GL.Vertex(yz1); GL.Vertex(yz2);
        }
    }

    private void DrawCapsuleColliderWireframe(CapsuleCollider capsuleCollider)
    {
        Vector3 center = capsuleCollider.center;
        float radius = capsuleCollider.radius;
        float height = capsuleCollider.height;
        int direction = capsuleCollider.direction;

        float cylinderHeight = Mathf.Max(0f, height - radius * 2f);
        Vector3 axisOffset = Vector3.zero;

        switch (direction)
        {
            case 0:
                axisOffset = new Vector3(cylinderHeight * 0.5f, 0f, 0f);
                break;

            case 1:
                axisOffset = new Vector3(0f, cylinderHeight * 0.5f, 0f);
                break;

            case 2:
                axisOffset = new Vector3(0f, 0f, cylinderHeight * 0.5f);
                break;
        }

        Vector3 top = center + axisOffset;
        Vector3 bottom = center - axisOffset;

        for (int i = 0; i < WireframeSegments; i++)
        {
            float angle1 = i / (float)WireframeSegments * Mathf.PI * 2f;
            float angle2 = (i + 1) / (float)WireframeSegments * Mathf.PI * 2f;

            Vector3 point1 = GetCapsuleCirclePoint(direction, angle1, radius);
            Vector3 point2 = GetCapsuleCirclePoint(direction, angle2, radius);

            GL.Vertex(top + point1);
            GL.Vertex(top + point2);

            GL.Vertex(bottom + point1);
            GL.Vertex(bottom + point2);
        }

        for (int i = 0; i < CapsuleEdgeCount; i++)
        {
            float angle = i / (float)CapsuleEdgeCount * Mathf.PI * 2f;
            Vector3 edgeOffset = GetCapsuleCirclePoint(direction, angle, radius);

            GL.Vertex(top + edgeOffset);
            GL.Vertex(bottom + edgeOffset);
        }
    }

    private Vector3 GetCapsuleCirclePoint(int direction, float angle, float radius)
    {
        float cos = Mathf.Cos(angle) * radius;
        float sin = Mathf.Sin(angle) * radius;

        switch (direction)
        {
            case 0:
                return new Vector3(0f, cos, sin);

            case 1:
                return new Vector3(cos, 0f, sin);

            case 2:
                return new Vector3(cos, sin, 0f);

            default:
                return Vector3.zero;
        }
    }
}