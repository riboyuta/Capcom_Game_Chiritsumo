using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[AddComponentMenu("Gameplay/Player/Player Dash Afterimage View")]
public sealed class PlayerDashAfterimageView : MonoBehaviour
{
    [Header("参照: PlayerFacade")]
    [Tooltip("ダッシュ状態を読み取るPlayerFacade。未設定の場合は親階層から自動取得します。")]
    [SerializeField] private PlayerFacade playerFacade;

    [Header("参照: モデルルート")]
    [Tooltip("残像化する3Dモデルのルート。Renderer取得はこの配下の有効なRendererだけを対象にします。未設定の場合は子のAnimatorを使用します。")]
    [SerializeField] private Transform modelRoot;

    [Header("残像の見た目: 色")]
    [Tooltip("残像に適用する色。アルファ値はstartAlphaとfadeCurveで制御されます。")]
    [SerializeField] private Color afterimageColor = new Color(0.42f, 0.34f, 1f, 1f);

    [Header("残像の見た目: 初期透明度")]
    [Tooltip("生成直後の透明度。0で不可視、1で最も濃く表示します。")]
    [SerializeField, Range(0f, 1f)] private float startAlpha = 0.42f;

    [Header("残像の見た目: 表示時間")]
    [Tooltip("残像が生成されてから消えるまでの秒数。短いほど早くフェードアウトします。")]
    [SerializeField, Min(0.01f)] private float lifetime = 0.24f;

    [Header("残像の位置: Zオフセット")]
    [Tooltip("残像のワールドZ位置に加える補正値です。カメラ手前に重なる場合は、環境に応じて正負を調整してください。X/Y位置やダッシュ挙動には影響しません。")]
    [SerializeField] private float afterimageDepthOffset = 0.02f;

    [Header("残像の見た目: サイズ補正")]
    [Tooltip("残像だけに掛けるサイズ倍率です。1で等倍、残像が小さい場合は1より大きく、大きい場合は1より小さく調整してください。")]
    [SerializeField, Min(0.01f)] private float afterimageScaleMultiplier = 1f;

    [Header("残像の生成制御: 生成間隔")]
    [Tooltip("ダッシュ継続中に残像を生成する間隔の秒数。小さいほど残像の密度が高くなります。")]
    [SerializeField, Min(0.01f)] private float spawnInterval = 0.045f;

    [Header("残像の生成制御: 最大数")]
    [Tooltip("同時に保持する残像の最大数。連続ダッシュ時にHierarchy上で増え続けないよう制限します。")]
    [SerializeField, Min(1)] private int maxGhostCount = 6;

    [Header("残像の生成制御: フェード")]
    [Tooltip("残像のフェードカーブ。横軸は寿命の進行度、縦軸は透明度の倍率です。")]
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("残像マテリアル: 任意指定")]
    [Tooltip("任意の残像用Material。未指定の場合は実行時に半透明表示用Materialを作成し、破棄時に解放します。")]
    [SerializeField] private Material afterimageMaterial;

    private const string PoolRootName = "PlayerDashAfterimagePool";

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

    private readonly List<GhostInstance> ghosts = new List<GhostInstance>();

    private Transform poolRoot;
    private Material runtimeMaterial;
    private bool wasDashActive;
    private float spawnTimer;
    private bool warnedMissingReferences;
    private bool warnedMissingMaterial;

    private void Awake()
    {
        ResolveReferences();
        EnsureRuntimeMaterial();
    }

    private void OnEnable()
    {
        spawnTimer = 0f;
        // PlayerFacade内部の初期化順に依存しないよう、OnEnableでは状態を読まない。
        wasDashActive = false;

        for (int i = 0; i < ghosts.Count; i++)
        {
            ghosts[i].Deactivate();
        }
    }

    private void Update()
    {
        if (!ResolveReferences())
        {
            WarnMissingReferencesOnce();
            return;
        }

        Material ghostMaterial = ResolveGhostMaterial();
        if (ghostMaterial == null)
        {
            WarnMissingMaterialOnce();
            return;
        }

        bool isDashActive = playerFacade.IsDashActive;
        bool didDashStart = playerFacade.JustDashStartedThisFrame || (!wasDashActive && isDashActive);

        // ダッシュ方向は将来の方向別演出に備えて読むだけにし、移動挙動には影響させない。
        _ = playerFacade.DashDirection;

        // ダッシュ本体は変更せず、Facadeの表示向け状態だけで残像生成のタイミングを決める。
        if (didDashStart)
        {
            SpawnAfterimage(ghostMaterial);
            spawnTimer = spawnInterval;
        }
        else if (isDashActive)
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f)
            {
                SpawnAfterimage(ghostMaterial);
                spawnTimer = spawnInterval;
            }
        }
        else
        {
            spawnTimer = 0f;
        }

        TickGhosts(Time.deltaTime);
        wasDashActive = isDashActive;
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

        if (runtimeMaterial != null)
        {
            DestroyUnityObject(runtimeMaterial);
            runtimeMaterial = null;
        }
    }

    private void OnValidate()
    {
        startAlpha = Mathf.Clamp01(startAlpha);
        lifetime = Mathf.Max(0.01f, lifetime);
        afterimageScaleMultiplier = Mathf.Max(0.01f, afterimageScaleMultiplier);
        spawnInterval = Mathf.Max(0.01f, spawnInterval);
        maxGhostCount = Mathf.Max(1, maxGhostCount);

        if (fadeCurve == null || fadeCurve.length == 0)
        {
            fadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        }
    }

    private bool ResolveReferences()
    {
        // Prefab未編集で導入できるよう、未設定時だけ既存階層から参照を補完する。
        if (playerFacade == null)
        {
            playerFacade = GetComponentInParent<PlayerFacade>();
        }

        if (modelRoot == null)
        {
            Animator animator = GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                modelRoot = animator.transform;
            }
        }

        return playerFacade != null && modelRoot != null;
    }

    private void WarnMissingReferencesOnce()
    {
        if (warnedMissingReferences)
        {
            return;
        }

        warnedMissingReferences = true;
        Debug.LogWarning(
            $"{nameof(PlayerDashAfterimageView)} requires a {nameof(PlayerFacade)} and a modelRoot reference.",
            this);
    }

    private Material ResolveGhostMaterial()
    {
        return afterimageMaterial != null ? afterimageMaterial : EnsureRuntimeMaterial();
    }

    private Material EnsureRuntimeMaterial()
    {
        // .matアセットを増やさないため、未指定時だけ実行時Materialを作ってOnDestroyで破棄する。
        if (afterimageMaterial != null)
        {
            return afterimageMaterial;
        }

        if (runtimeMaterial != null)
        {
            return runtimeMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            return null;
        }

        runtimeMaterial = new Material(shader)
        {
            name = "Runtime Player Dash Afterimage"
        };

        ConfigureTransparentMaterial(runtimeMaterial);
        ApplyMaterialColor(runtimeMaterial, GetTintColor(1f));
        return runtimeMaterial;
    }

    private void WarnMissingMaterialOnce()
    {
        if (warnedMissingMaterial)
        {
            return;
        }

        warnedMissingMaterial = true;
        Debug.LogWarning(
            $"{nameof(PlayerDashAfterimageView)} could not create a runtime afterimage material.",
            this);
    }

    private void SpawnAfterimage(Material ghostMaterial)
    {
        // ModelRoot配下の有効Rendererだけを現在姿勢のGhostとして切り出す。
        Renderer[] sourceRenderers = modelRoot.GetComponentsInChildren<Renderer>(false);
        if (sourceRenderers.Length == 0)
        {
            return;
        }

        GhostInstance ghost = GetReusableGhost(ghostMaterial);
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

        ghost.CompleteCapture(capturedCount, GetTintColor(1f));
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

    private GhostInstance GetReusableGhost(Material ghostMaterial)
    {
        // 連続ダッシュでHierarchyが増え続けないよう、最大数内でGhostを使い回す。
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
            GhostInstance created = new GhostInstance(poolRoot, $"{nameof(PlayerDashAfterimageView)}Ghost_{ghosts.Count + 1}");
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
        // Ghostはプレイヤーの子にしない。残像がプレイヤー移動に追従しないようワールド側で保持する。
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

    private void TickGhosts(float deltaTime)
    {
        for (int i = 0; i < ghosts.Count; i++)
        {
            ghosts[i].Tick(deltaTime, fadeCurve, GetTintColor(1f));
        }
    }

    private Color GetTintColor(float alphaMultiplier)
    {
        Color color = afterimageColor;
        color.a = afterimageColor.a * startAlpha * alphaMultiplier;
        return color;
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

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
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

        public void CompleteCapture(int activePartCount, Color initialColor)
        {
            for (int i = activePartCount; i < parts.Count; i++)
            {
                parts[i].Deactivate();
            }

            ApplyColor(initialColor);
        }

        public void Cancel()
        {
            Deactivate();
        }

        public void Tick(float deltaTime, AnimationCurve fadeCurve, Color baseColor)
        {
            // 各Ghostは生成時の姿勢を保ったまま、MaterialPropertyBlockで個別に透明度だけ下げる。
            if (!Active)
            {
                return;
            }

            age += deltaTime;
            float normalizedAge = Mathf.Clamp01(age / lifetime);
            if (normalizedAge >= 1f)
            {
                Deactivate();
                return;
            }

            float fade = fadeCurve != null ? Mathf.Clamp01(fadeCurve.Evaluate(normalizedAge)) : 1f - normalizedAge;
            Color color = baseColor;
            color.a = baseColor.a * fade;
            ApplyColor(color);
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

        private void ApplyColor(Color color)
        {
            propertyBlock.Clear();
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            propertyBlock.SetColor(TintColorId, color);

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
                name = $"{sourceName}_DashAfterimage"
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
