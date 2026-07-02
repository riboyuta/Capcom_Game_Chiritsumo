using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(60)]
[DisallowMultipleComponent]
[AddComponentMenu("Gameplay/Enemy/Shadow Chaser Dash Afterimage View")]
/// ShadowChaserEnemy のダッシュ再生中だけ残像を生成するビュー
/// モデルの現在姿勢をコピーし、敵専用の色とEmissionで残像表示を管理する
public sealed class ShadowChaserDashAfterimageView : MonoBehaviour
{
    // -----------------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------------

    [Header("参照: ShadowChaserEnemy")]
    [Tooltip("残像生成条件を読み取る ShadowChaserEnemy です。未設定の場合は親階層から自動取得します。")]
    [SerializeField] private ShadowChaserEnemy shadowEnemy;

    [Header("参照: モデルルート")]
    [Tooltip("残像化する陰エネミーモデルのルートです。この配下の有効な SkinnedMeshRenderer / MeshRenderer を現在姿勢でコピーします。未設定の場合は子の Animator を使用します。")]
    [SerializeField] private Transform modelRoot;

    [Header("残像の見た目: 色")]
    [Tooltip("useColorGradient が無効な場合に残像へ適用する敵専用の単色です。アルファ値は startAlpha と内部フェードで制御します。")]
    [SerializeField] private Color afterimageColor = new Color(0.45f, 0.02f, 0.08f, 1f);

    [Header("残像の見た目: 色変化")]
    [Tooltip("残像本体色を寿命進行度に応じて Gradient で変化させるかどうかです。陰エネミー用の赤黒い軌跡を作る場合は ON にします。")]
    [SerializeField] private bool useColorGradient = true;

    [Header("残像の見た目: 色Gradient")]
    [Tooltip("残像本体色の寿命ごとの変化です。生成直後の明るい赤から深い赤、終端の黒透明へ変化します。")]
    [SerializeField] private Gradient afterimageColorGradient = CreateDefaultAfterimageColorGradient();

    [Header("残像の発光: 有効化")]
    [Tooltip("残像に Emission 発光を適用するかどうかです。陰エネミーのダッシュ危険行動を強調する場合は ON にします。")]
    [SerializeField] private bool useEmission = true;

    [Header("残像の発光: 色")]
    [Tooltip("useEmissionColorGradient が無効な場合に使う Emission 発光色です。HDR Color として赤系の発光を調整します。")]
    [SerializeField, ColorUsage(false, true)] private Color emissionColor = new Color(1.5f, 0.06f, 0.02f, 1f);

    [Header("残像の発光: 色変化")]
    [Tooltip("Emission 発光色を寿命進行度に応じて Gradient で変化させるかどうかです。赤から暗赤、黒へ落とす場合は ON にします。")]
    [SerializeField] private bool useEmissionColorGradient = true;

    [Header("残像の発光: 色Gradient")]
    [Tooltip("Emission 発光色の寿命ごとの変化です。生成直後の赤から暗赤、終端の黒へ変化します。")]
    [SerializeField] private Gradient emissionColorGradient = CreateDefaultEmissionColorGradient();

    [Header("残像の発光: 強さ")]
    [Tooltip("残像の Emission 発光の基準強度です。大きいほど発光が強くなり、Bloom 設定がある環境ではにじみも強くなります。")]
    [SerializeField, Min(0f)] private float emissionIntensity = 2f;

    [Header("残像の見た目: 初期透明度")]
    [Tooltip("生成直後の透明度です。0 で不可視、1 で最も濃く表示します。陰エネミー用初期値は 0.4 です。")]
    [SerializeField, Range(0f, 1f)] private float startAlpha = 0.4f;

    [Header("残像の見た目: 表示時間")]
    [Tooltip("残像が生成されてから消えるまでの秒数です。短いほど早くフェードアウトします。陰エネミー用初期値は 0.24 秒です。")]
    [SerializeField, Min(0.01f)] private float lifetime = 0.24f;

    [Header("残像の位置: Zオフセット")]
    [Tooltip("残像のワールド Z 位置に加える補正値です。モデルや背景との重なりを避けるために使い、X/Y 位置や追跡挙動には影響しません。")]
    [SerializeField] private float afterimageDepthOffset = 1f;

    [Header("残像の見た目: サイズ補正")]
    [Tooltip("残像だけに掛けるサイズ倍率です。1 で等倍、陰エネミー用初期値は危険行動を強調するため 1.4 です。")]
    [SerializeField, Min(0.01f)] private float afterimageScaleMultiplier = 1.4f;

    [Header("残像の生成制御: 生成間隔")]
    [Tooltip("ダッシュ履歴再生中に残像を生成する間隔の秒数です。小さいほど残像の密度が高くなります。陰エネミー用初期値は 0.045 秒です。")]
    [SerializeField, Min(0.01f)] private float spawnInterval = 0.045f;

    [Header("残像の生成制御: 最大数")]
    [Tooltip("同時に保持する残像の最大数です。連続ダッシュ再生時に Hierarchy 上で増え続けないよう制限します。陰エネミー用初期値は 5 です。")]
    [SerializeField, Min(1)] private int maxGhostCount = 5;

    private const string PoolRootName = "ShadowChaserDashAfterimagePool";

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
    private static readonly int BlendId = Shader.PropertyToID("_Blend");
    private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
    private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
    private static readonly int CullId = Shader.PropertyToID("_Cull");
    private static readonly int ModeId = Shader.PropertyToID("_Mode");
    private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private const string EmissionKeyword = "_EMISSION";

    private readonly List<GhostInstance> ghosts = new List<GhostInstance>();

    private Transform poolRoot;
    private Material runtimeMaterial;
    private AnimationCurve fadeCurve;
    private AnimationCurve emissionIntensityCurve;
    private bool wasDashSnapshotActive;
    private float spawnTimer;

    // -----------------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------------

    private void Awake()
    {
        ResolveReferences();
        EnsureRuntimeGradients();
        EnsureRuntimeFadeCurve();
        EnsureRuntimeEmissionCurve();
        EnsureRuntimeMaterial();
    }

    private void OnEnable()
    {
        ResetSpawnState();
        DeactivateGhosts();
    }

    private void LateUpdate()
    {
        ResolveReferences();

        if (!HasRequiredReferences())
        {
            ResetSpawnState();
            DeactivateGhosts();
            return;
        }

        if (!shadowEnemy.IsActive() || !shadowEnemy.HasSnapshot)
        {
            ResetSpawnState();
            DeactivateGhosts();
            return;
        }

        bool isDashing = shadowEnemy.CurrentSnapshot.isDashing;
        if (!isDashing)
        {
            ResetSpawnState();
            TickGhosts(Time.deltaTime);
            return;
        }

        Material ghostMaterial = ResolveGhostMaterial();
        if (ghostMaterial == null)
        {
            ResetSpawnState();
            TickGhosts(Time.deltaTime);
            return;
        }

        if (!wasDashSnapshotActive)
        {
            SpawnAfterimage(ghostMaterial);
            spawnTimer = spawnInterval;
        }
        else
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f)
            {
                SpawnAfterimage(ghostMaterial);
                spawnTimer = spawnInterval;
            }
        }

        TickGhosts(Time.deltaTime);
        wasDashSnapshotActive = true;
    }

    private void OnDisable()
    {
        ResetSpawnState();
        DeactivateGhosts();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < ghosts.Count; i++)
        {
            ghosts[i].Dispose();
        }

        ghosts.Clear();

        if (poolRoot != null)
        {
            DestroyUnityObject(poolRoot.gameObject);
            poolRoot = null;
        }

        DestroyRuntimeMaterial();
    }

    private void OnValidate()
    {
        startAlpha = Mathf.Clamp01(startAlpha);
        emissionIntensity = Mathf.Max(0f, emissionIntensity);
        lifetime = Mathf.Max(0.01f, lifetime);
        afterimageScaleMultiplier = Mathf.Max(0.01f, afterimageScaleMultiplier);
        spawnInterval = Mathf.Max(0.01f, spawnInterval);
        maxGhostCount = Mathf.Max(1, maxGhostCount);

        EnsureRuntimeGradients();
        EnsureRuntimeFadeCurve();
        EnsureRuntimeEmissionCurve();
    }

    // -----------------------------------------------------------------------------
    // Main Logic
    // -----------------------------------------------------------------------------

    private void SpawnAfterimage(Material ghostMaterial)
    {
        // ModelRoot配下の有効Rendererだけを、ShadowChaserModelView更新後の姿勢でGhost化する。
        Renderer[] sourceRenderers = modelRoot.GetComponentsInChildren<Renderer>(false);
        if (sourceRenderers.Length == 0)
        {
            return;
        }

        GhostInstance ghost = GetReusableGhost();
        ghost.Begin(lifetime);

        int capturedCount = 0;
        for (int i = 0; i < sourceRenderers.Length; i++)
        {
            Renderer sourceRenderer = sourceRenderers[i];
            if (!CanCaptureRenderer(sourceRenderer))
            {
                continue;
            }

            SkinnedMeshRenderer skinnedMeshRenderer = sourceRenderer as SkinnedMeshRenderer;
            if (skinnedMeshRenderer != null)
            {
                if (skinnedMeshRenderer.sharedMesh == null)
                {
                    continue;
                }

                GhostPart part = ghost.GetPart(capturedCount);
                part.CaptureSkinned(skinnedMeshRenderer, ghostMaterial, afterimageDepthOffset, afterimageScaleMultiplier);
                capturedCount++;
                continue;
            }

            MeshRenderer meshRenderer = sourceRenderer as MeshRenderer;
            if (meshRenderer != null)
            {
                MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                GhostPart part = ghost.GetPart(capturedCount);
                part.CaptureMesh(meshRenderer, meshFilter.sharedMesh, ghostMaterial, afterimageDepthOffset, afterimageScaleMultiplier);
                capturedCount++;
            }
        }

        if (capturedCount == 0)
        {
            ghost.Cancel();
            return;
        }

        ghost.CompleteCapture(capturedCount, GetAfterimageColor(0f, 1f), GetEmissionColor(0f));
    }

    private void TickGhosts(float deltaTime)
    {
        for (int i = 0; i < ghosts.Count; i++)
        {
            ghosts[i].Tick(
                deltaTime,
                fadeCurve,
                useColorGradient,
                afterimageColor,
                afterimageColorGradient,
                startAlpha,
                useEmission,
                useEmissionColorGradient,
                emissionColor,
                emissionColorGradient,
                emissionIntensity,
                emissionIntensityCurve);
        }
    }

    // -----------------------------------------------------------------------------
    // Query Helpers
    // -----------------------------------------------------------------------------

    private bool HasRequiredReferences()
    {
        return shadowEnemy != null && modelRoot != null;
    }

    private bool CanCaptureRenderer(Renderer sourceRenderer)
    {
        // 非表示Hoodや旧Sprite表示を巻き込まないよう、現在有効な3D Rendererだけを対象にする。
        if (sourceRenderer == null || !sourceRenderer.enabled || !sourceRenderer.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (sourceRenderer is SpriteRenderer)
        {
            return false;
        }

        return sourceRenderer is SkinnedMeshRenderer || sourceRenderer is MeshRenderer;
    }

    private Color GetAfterimageColor(float normalizedAge, float fade)
    {
        return EvaluateAfterimageColor(
            useColorGradient,
            afterimageColorGradient,
            afterimageColor,
            startAlpha,
            normalizedAge,
            fade);
    }

    private Color GetEmissionColor(float normalizedAge)
    {
        return EvaluateEmissionColor(
            useEmission,
            useEmissionColorGradient,
            emissionColorGradient,
            emissionColor,
            emissionIntensity,
            emissionIntensityCurve,
            normalizedAge);
    }

    // -----------------------------------------------------------------------------
    // Internal Helpers
    // -----------------------------------------------------------------------------

    // 実行時に必要な参照を取得し、使用前の状態に整える
    private void ResolveReferences()
    {
        if (shadowEnemy == null)
        {
            shadowEnemy = GetComponentInParent<ShadowChaserEnemy>();
        }

        if (modelRoot == null)
        {
            Animator animator = GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                modelRoot = animator.transform;
            }
        }
    }

    private void ResetSpawnState()
    {
        spawnTimer = 0f;
        wasDashSnapshotActive = false;
    }

    private void DeactivateGhosts()
    {
        for (int i = 0; i < ghosts.Count; i++)
        {
            ghosts[i].Deactivate();
        }
    }

    private GhostInstance GetReusableGhost()
    {
        // 連続ダッシュ再生でHierarchyが増え続けないよう、最大数内でGhostを使い回す。
        EnsurePoolRoot();

        int limit = Mathf.Max(1, maxGhostCount);
        for (int i = 0; i < ghosts.Count; i++)
        {
            if (!ghosts[i].Active)
            {
                return ghosts[i];
            }
        }

        if (ghosts.Count < limit)
        {
            GhostInstance created = new GhostInstance(poolRoot, $"{nameof(ShadowChaserDashAfterimageView)}Ghost_{ghosts.Count + 1}");
            ghosts.Add(created);
            return created;
        }

        GhostInstance oldest = ghosts[0];
        for (int i = 1; i < ghosts.Count; i++)
        {
            if (ghosts[i].NormalizedAge > oldest.NormalizedAge)
            {
                oldest = ghosts[i];
            }
        }

        return oldest;
    }

    private void EnsurePoolRoot()
    {
        // Ghostは陰エネミーの子にしない。残像が敵移動に追従しないようワールド側で保持する。
        if (poolRoot != null)
        {
            return;
        }

        GameObject root = new GameObject(PoolRootName);
        root.hideFlags = HideFlags.DontSave;
        poolRoot = root.transform;
        poolRoot.position = Vector3.zero;
        poolRoot.rotation = Quaternion.identity;
        poolRoot.localScale = Vector3.one;
    }

    private Material ResolveGhostMaterial()
    {
        Material material = EnsureRuntimeMaterial();
        ConfigureRuntimeEmission(material);
        return material;
    }

    private Material EnsureRuntimeMaterial()
    {
        if (runtimeMaterial != null)
        {
            return runtimeMaterial;
        }

        Shader shader = FindDefaultAfterimageShader();
        if (shader == null)
        {
            return null;
        }

        runtimeMaterial = new Material(shader)
        {
            name = "Runtime Shadow Chaser Dash Afterimage",
            hideFlags = HideFlags.DontSave
        };

        PrepareRuntimeMaterial(runtimeMaterial);
        return runtimeMaterial;
    }

    private static Shader FindDefaultAfterimageShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null)
        {
            return shader;
        }

        shader = Shader.Find("Standard");
        if (shader != null)
        {
            return shader;
        }

        shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null)
        {
            return shader;
        }

        shader = Shader.Find("Unlit/Color");
        if (shader != null)
        {
            return shader;
        }

        return Shader.Find("Sprites/Default");
    }

    private void PrepareRuntimeMaterial(Material material)
    {
        ConfigureTransparentMaterial(material);
        ApplyMaterialColor(material, GetAfterimageColor(0f, 1f));
        ConfigureRuntimeEmission(material);
    }

    private void ConfigureRuntimeEmission(Material material)
    {
        if (material == null || !material.HasProperty(EmissionColorId))
        {
            return;
        }

        if (useEmission)
        {
            material.EnableKeyword(EmissionKeyword);
            material.SetColor(EmissionColorId, GetEmissionColor(0f));
        }
        else
        {
            material.SetColor(EmissionColorId, Color.black);
            material.DisableKeyword(EmissionKeyword);
        }
    }

    private void DestroyRuntimeMaterial()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        DestroyUnityObject(runtimeMaterial);
        runtimeMaterial = null;
    }

    private void EnsureRuntimeFadeCurve()
    {
        if (fadeCurve == null || fadeCurve.length == 0)
        {
            fadeCurve = CreateDefaultFadeCurve();
        }
    }

    private void EnsureRuntimeGradients()
    {
        if (afterimageColorGradient == null)
        {
            afterimageColorGradient = CreateDefaultAfterimageColorGradient();
        }

        if (emissionColorGradient == null)
        {
            emissionColorGradient = CreateDefaultEmissionColorGradient();
        }
    }

    private void EnsureRuntimeEmissionCurve()
    {
        if (emissionIntensityCurve == null || emissionIntensityCurve.length == 0)
        {
            emissionIntensityCurve = CreateDefaultEmissionIntensityCurve();
        }
    }

    private static AnimationCurve CreateDefaultFadeCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 1f, -2.2f, -2.2f),
            new Keyframe(0.25f, 0.45f, -2.2f, -0.75f),
            new Keyframe(0.65f, 0.15f, -0.75f, -0.43f),
            new Keyframe(1f, 0f, -0.43f, -0.43f));
    }

    private static Gradient CreateDefaultAfterimageColorGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.14f, 0.08f, 1f), 0f),
                new GradientColorKey(new Color(0.58f, 0.03f, 0.05f, 1f), 0.38f),
                new GradientColorKey(Color.black, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.7f, 0.38f),
                new GradientAlphaKey(0f, 1f)
            });

        return gradient;
    }

    private static Gradient CreateDefaultEmissionColorGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1.5f, 0.06f, 0.02f, 1f), 0f),
                new GradientColorKey(new Color(0.6f, 0.02f, 0.02f, 1f), 0.55f),
                new GradientColorKey(Color.black, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });

        return gradient;
    }

    private static AnimationCurve CreateDefaultEmissionIntensityCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 1f, -1.7f, -1.7f),
            new Keyframe(0.35f, 0.55f, -1.4f, -1f),
            new Keyframe(1f, 0f, -0.55f, -0.55f));
    }

    private static Color EvaluateAfterimageColor(
        bool useColorGradient,
        Gradient colorGradient,
        Color fallbackColor,
        float startAlpha,
        float normalizedAge,
        float fade)
    {
        Color color = useColorGradient && colorGradient != null
            ? colorGradient.Evaluate(Mathf.Clamp01(normalizedAge))
            : fallbackColor;

        // Gradientの透明度と内部Fadeを合成し、色変化を入れても消え方の責務を維持する。
        color.a *= Mathf.Clamp01(startAlpha) * Mathf.Clamp01(fade);
        return color;
    }

    private static Color EvaluateEmissionColor(
        bool useEmission,
        bool useEmissionColorGradient,
        Gradient emissionColorGradient,
        Color fallbackColor,
        float emissionIntensity,
        AnimationCurve emissionIntensityCurve,
        float normalizedAge)
    {
        if (!useEmission || emissionIntensity <= 0f)
        {
            return Color.black;
        }

        normalizedAge = Mathf.Clamp01(normalizedAge);
        float curveMultiplier = emissionIntensityCurve != null
            ? Mathf.Max(0f, emissionIntensityCurve.Evaluate(normalizedAge))
            : Mathf.Max(0f, 1f - normalizedAge);

        Color emissionBase = useEmissionColorGradient && emissionColorGradient != null
            ? emissionColorGradient.Evaluate(normalizedAge)
            : fallbackColor;

        return emissionBase * Mathf.Max(0f, emissionIntensity) * curveMultiplier;
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;

        if (material.HasProperty(SurfaceId))
        {
            material.SetFloat(SurfaceId, 1f);
        }

        if (material.HasProperty(BlendId))
        {
            material.SetFloat(BlendId, 0f);
        }

        if (material.HasProperty(ModeId))
        {
            material.SetFloat(ModeId, 3f);
        }

        if (material.HasProperty(SrcBlendId))
        {
            material.SetInt(SrcBlendId, (int)BlendMode.SrcAlpha);
        }

        if (material.HasProperty(DstBlendId))
        {
            material.SetInt(DstBlendId, (int)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty(ZWriteId))
        {
            material.SetInt(ZWriteId, 0);
        }

        if (material.HasProperty(CullId))
        {
            material.SetInt(CullId, (int)CullMode.Off);
        }

        if (material.HasProperty(AlphaClipId))
        {
            material.SetFloat(AlphaClipId, 0f);
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    private static void ApplyMaterialColor(Material material, Color color)
    {
        if (material.HasProperty(BaseColorId))
        {
            material.SetColor(BaseColorId, color);
        }

        if (material.HasProperty(ColorId))
        {
            material.SetColor(ColorId, color);
        }

        if (material.HasProperty(TintColorId))
        {
            material.SetColor(TintColorId, color);
        }
    }

    private static void DestroyUnityObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private sealed class GhostInstance
    {
        private readonly GameObject root;
        private readonly List<GhostPart> parts = new List<GhostPart>();
        private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        private float lifetime = 0.01f;
        private float age;

        public GhostInstance(Transform parent, string name)
        {
            root = new GameObject(name);
            root.hideFlags = HideFlags.DontSave;
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            root.SetActive(false);
        }

        public bool Active => root.activeSelf;

        public float NormalizedAge => lifetime <= 0f ? 1f : age / lifetime;

        public void Begin(float newLifetime)
        {
            lifetime = Mathf.Max(0.01f, newLifetime);
            age = 0f;
            root.SetActive(true);

            for (int i = 0; i < parts.Count; i++)
            {
                parts[i].Deactivate();
            }
        }

        public GhostPart GetPart(int index)
        {
            while (parts.Count <= index)
            {
                parts.Add(new GhostPart(root.transform, $"Part_{parts.Count + 1}"));
            }

            return parts[index];
        }

        public void CompleteCapture(int activePartCount, Color initialColor, Color initialEmission)
        {
            for (int i = activePartCount; i < parts.Count; i++)
            {
                parts[i].Deactivate();
            }

            ApplyVisualProperties(initialColor, initialEmission);
        }

        public void Cancel()
        {
            Deactivate();
        }

        public void Tick(
            float deltaTime,
            AnimationCurve fadeCurve,
            bool useColorGradient,
            Color afterimageColor,
            Gradient afterimageColorGradient,
            float startAlpha,
            bool useEmission,
            bool useEmissionColorGradient,
            Color emissionColor,
            Gradient emissionColorGradient,
            float emissionIntensity,
            AnimationCurve emissionIntensityCurve)
        {
            // 各Ghostは生成時の姿勢を保ったまま、MaterialPropertyBlockで個別に透明度と発光を下げる。
            if (!Active)
            {
                return;
            }

            age += deltaTime;
            float normalizedAge = Mathf.Clamp01(age / lifetime);
            if (normalizedAge >= 1f)
            {
                Color finalColor = EvaluateAfterimageColor(
                    useColorGradient,
                    afterimageColorGradient,
                    afterimageColor,
                    startAlpha,
                    1f,
                    0f);
                ApplyVisualProperties(finalColor, Color.black);
                Deactivate();
                return;
            }

            float fade = fadeCurve != null ? Mathf.Clamp01(fadeCurve.Evaluate(normalizedAge)) : 1f - normalizedAge;
            Color color = EvaluateAfterimageColor(
                useColorGradient,
                afterimageColorGradient,
                afterimageColor,
                startAlpha,
                normalizedAge,
                fade);
            Color emission = EvaluateEmissionColor(
                useEmission,
                useEmissionColorGradient,
                emissionColorGradient,
                emissionColor,
                emissionIntensity,
                emissionIntensityCurve,
                normalizedAge);

            ApplyVisualProperties(color, emission);
        }

        public void Deactivate()
        {
            root.SetActive(false);
            age = lifetime;

            for (int i = 0; i < parts.Count; i++)
            {
                parts[i].Deactivate();
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < parts.Count; i++)
            {
                parts[i].Dispose();
            }

            parts.Clear();
            DestroyUnityObject(root);
        }

        private void ApplyVisualProperties(Color color, Color emission)
        {
            propertyBlock.Clear();
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            propertyBlock.SetColor(TintColorId, color);
            propertyBlock.SetColor(EmissionColorId, emission);

            for (int i = 0; i < parts.Count; i++)
            {
                parts[i].SetPropertyBlock(propertyBlock);
            }
        }
    }

    private sealed class GhostPart
    {
        private readonly GameObject gameObject;
        private readonly Transform transform;
        private readonly MeshFilter meshFilter;
        private readonly MeshRenderer meshRenderer;

        private Material[] materialSlots;
        private Mesh bakedMesh;

        public GhostPart(Transform parent, string name)
        {
            gameObject = new GameObject(name);
            gameObject.hideFlags = HideFlags.DontSave;
            transform = gameObject.transform;
            transform.SetParent(parent, false);

            meshFilter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = true;

            gameObject.SetActive(false);
        }

        public void CaptureSkinned(SkinnedMeshRenderer sourceRenderer, Material material, float depthOffset, float scaleMultiplier)
        {
            // BakeMesh側でスケールを含め、Ghost Transformでは二重にScaleを掛けない。
            Mesh mesh = EnsureBakedMesh(sourceRenderer.name);
            mesh.Clear();
            sourceRenderer.BakeMesh(mesh, true);
            meshFilter.sharedMesh = mesh;

            CopySkinnedTransform(sourceRenderer.transform, depthOffset, scaleMultiplier);
            ApplyMaterial(material, sourceRenderer.sharedMaterials.Length);
            gameObject.SetActive(true);
        }

        public void CaptureMesh(MeshRenderer sourceRenderer, Mesh sourceMesh, Material material, float depthOffset, float scaleMultiplier)
        {
            meshFilter.sharedMesh = sourceMesh;

            CopyMeshTransform(sourceRenderer.transform, depthOffset, scaleMultiplier);
            ApplyMaterial(material, sourceRenderer.sharedMaterials.Length);
            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            meshRenderer.SetPropertyBlock(null);
            meshFilter.sharedMesh = null;
            gameObject.SetActive(false);
        }

        public void SetPropertyBlock(MaterialPropertyBlock propertyBlock)
        {
            if (gameObject.activeSelf)
            {
                meshRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        public void Dispose()
        {
            if (bakedMesh != null)
            {
                DestroyUnityObject(bakedMesh);
                bakedMesh = null;
            }

            DestroyUnityObject(gameObject);
        }

        private Mesh EnsureBakedMesh(string sourceName)
        {
            if (bakedMesh != null)
            {
                return bakedMesh;
            }

            bakedMesh = new Mesh
            {
                name = $"{sourceName}_ShadowChaserDashAfterimage"
            };
            bakedMesh.MarkDynamic();
            return bakedMesh;
        }

        private void CopySkinnedTransform(Transform sourceTransform, float depthOffset, float scaleMultiplier)
        {
            transform.SetPositionAndRotation(ApplyDepthOffset(sourceTransform.position, depthOffset), sourceTransform.rotation);
            transform.localScale = Vector3.one * scaleMultiplier;
        }

        private void CopyMeshTransform(Transform sourceTransform, float depthOffset, float scaleMultiplier)
        {
            transform.SetPositionAndRotation(ApplyDepthOffset(sourceTransform.position, depthOffset), sourceTransform.rotation);
            transform.localScale = sourceTransform.lossyScale * scaleMultiplier;
        }

        private static Vector3 ApplyDepthOffset(Vector3 position, float depthOffset)
        {
            position.z += depthOffset;
            return position;
        }

        private void ApplyMaterial(Material material, int sourceMaterialCount)
        {
            int materialCount = Mathf.Max(1, sourceMaterialCount);
            if (materialSlots == null || materialSlots.Length != materialCount)
            {
                materialSlots = new Material[materialCount];
            }

            for (int i = 0; i < materialSlots.Length; i++)
            {
                materialSlots[i] = material;
            }

            meshRenderer.sharedMaterials = materialSlots;
        }
    }
}
