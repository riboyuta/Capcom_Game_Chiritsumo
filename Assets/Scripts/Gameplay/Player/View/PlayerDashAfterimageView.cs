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
    [Tooltip("useColorGradientが無効な場合に残像へ適用する単色です。アルファ値はstartAlphaとfadeCurveで制御されます。")]
    [SerializeField] private Color afterimageColor = new Color(0.42f, 0.34f, 1f, 1f);

    [Header("残像の見た目: 色変化")]
    [Tooltip("残像本体色を寿命進行度に応じてGradientで変化させるかどうかです。無効時はafterimageColorの単色を使用します。")]
    [SerializeField] private bool useColorGradient = true;

    [Header("残像の見た目: 色Gradient")]
    [Tooltip("残像本体色の寿命ごとの変化です。生成直後の白青から青、青紫、透明へ流れる色跡を作ります。")]
    [SerializeField] private Gradient afterimageColorGradient = CreateDefaultAfterimageColorGradient();

    [Header("残像の発光: 有効化")]
    [Tooltip("残像にEmission発光を適用するかどうかです。無効時は透明度フェードのみを使用します。")]
    [SerializeField] private bool useEmission = true;

    [Header("残像の発光: 色")]
    [Tooltip("useEmissionColorGradientが無効な場合に使うEmission発光色です。HDR Colorとして調整でき、青紫系の高速分身表現に使います。")]
    [SerializeField, ColorUsage(false, true)] private Color emissionColor = new Color(0.36f, 0.2f, 1f, 1f);

    [Header("残像の発光: 色変化")]
    [Tooltip("Emission発光色を寿命進行度に応じてGradientで変化させるかどうかです。無効時はemissionColorの単色を使用します。")]
    [SerializeField] private bool useEmissionColorGradient = true;

    [Header("残像の発光: 色Gradient")]
    [Tooltip("Emission発光色の寿命ごとの変化です。生成直後の白青から青、青紫へ流れ、終端では発光を残さない設定にします。")]
    [SerializeField] private Gradient emissionColorGradient = CreateDefaultEmissionColorGradient();

    [Header("残像の発光: 強さ")]
    [Tooltip("残像のEmission発光の基準強度です。大きいほど発光が強くなり、Bloom設定がある環境ではにじみも強くなります。")]
    [SerializeField, Min(0f)] private float emissionIntensity = 2f;

    [Header("残像の発光: フェード")]
    [Tooltip("残像寿命に応じてEmission発光を弱めるカーブです。横軸は寿命の進行度、縦軸は発光強度の倍率です。")]
    [SerializeField] private AnimationCurve emissionIntensityCurve = CreateDefaultEmissionIntensityCurve();

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
    [SerializeField] private AnimationCurve fadeCurve = CreateDefaultFadeCurve();

    [Header("残像マテリアル: 任意指定")]
    [Tooltip("任意の残像用Material。指定時もMaterial Assetは直接変更せず、実行時コピーをGhost用に調整して破棄時に解放します。未指定の場合は半透明表示用Materialを実行時に作成します。")]
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
    private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private const string EmissionKeyword = "_EMISSION";

    private readonly List<GhostInstance> ghosts = new List<GhostInstance>();

    private Transform poolRoot;
    private Material runtimeMaterial;
    private Material runtimeMaterialSource;
    private bool wasDashActive;
    private float spawnTimer;
    private bool warnedMissingReferences;
    private bool warnedMissingMaterial;
    private bool warnedMissingColorProperties;
    private bool warnedMissingEmissionProperty;

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
            DestroyRuntimeMaterial();
        }
    }

    private void OnValidate()
    {
        startAlpha = Mathf.Clamp01(startAlpha);
        emissionIntensity = Mathf.Max(0f, emissionIntensity);
        lifetime = Mathf.Max(0.01f, lifetime);
        afterimageScaleMultiplier = Mathf.Max(0.01f, afterimageScaleMultiplier);
        spawnInterval = Mathf.Max(0.01f, spawnInterval);
        maxGhostCount = Mathf.Max(1, maxGhostCount);

        if (fadeCurve == null || fadeCurve.length == 0)
        {
            fadeCurve = CreateDefaultFadeCurve();
        }

        if (afterimageColorGradient == null)
        {
            afterimageColorGradient = CreateDefaultAfterimageColorGradient();
        }

        if (emissionColorGradient == null)
        {
            emissionColorGradient = CreateDefaultEmissionColorGradient();
        }

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
                new GradientColorKey(new Color(0.82f, 0.95f, 1f, 1f), 0f),
                new GradientColorKey(new Color(0.1f, 0.72f, 1f, 1f), 0.25f),
                new GradientColorKey(new Color(0.34f, 0.24f, 1f, 1f), 0.65f),
                new GradientColorKey(new Color(0.58f, 0.14f, 1f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.75f, 0.25f),
                new GradientAlphaKey(0.35f, 0.65f),
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
                new GradientColorKey(new Color(0.82f, 0.95f, 1f, 1f), 0f),
                new GradientColorKey(new Color(0.08f, 0.46f, 1f, 1f), 0.35f),
                new GradientColorKey(new Color(0.42f, 0.18f, 1f, 1f), 0.75f),
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

    private void EnsureRuntimeFadeCurve()
    {
        // Scene側に旧デフォルトが保存済みでも、カスタム調整済みでなければ新しい初期フェードを使う。
        if (fadeCurve == null || fadeCurve.length == 0 || IsLegacyDefaultFadeCurve(fadeCurve))
        {
            fadeCurve = CreateDefaultFadeCurve();
        }
    }

    private static bool IsLegacyDefaultFadeCurve(AnimationCurve curve)
    {
        if (curve == null || curve.length != 2)
        {
            return false;
        }

        Keyframe[] keys = curve.keys;
        Keyframe first = keys[0];
        Keyframe second = keys[1];
        return Mathf.Approximately(first.time, 0f)
            && Mathf.Approximately(first.value, 1f)
            && Mathf.Approximately(first.outTangent, 0f)
            && Mathf.Approximately(second.time, 1f)
            && Mathf.Approximately(second.value, 0f)
            && Mathf.Approximately(second.inTangent, 0f);
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
        Material material = EnsureRuntimeMaterial();
        ConfigureRuntimeEmission(material);
        return material;
    }

    private Material EnsureRuntimeMaterial()
    {
        if (runtimeMaterial != null && runtimeMaterialSource == afterimageMaterial)
        {
            return runtimeMaterial;
        }

        DestroyRuntimeMaterial();
        runtimeMaterialSource = afterimageMaterial;

        if (afterimageMaterial != null)
        {
            // 指定Materialも直接変更せず、Ghost専用のRuntimeコピーだけを透明表示向けに調整する。
            runtimeMaterial = new Material(afterimageMaterial)
            {
                name = $"{afterimageMaterial.name} Runtime Dash Afterimage",
                hideFlags = HideFlags.DontSave
            };

            PrepareRuntimeMaterial(runtimeMaterial);
            return runtimeMaterial;
        }

        Shader shader = FindDefaultAfterimageShader();

        if (shader == null)
        {
            runtimeMaterialSource = null;
            return null;
        }

        runtimeMaterial = new Material(shader)
        {
            name = "Runtime Player Dash Afterimage",
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
        WarnMissingColorPropertiesOnce(material);
    }

    private void ConfigureRuntimeEmission(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (!material.HasProperty(EmissionColorId))
        {
            if (useEmission)
            {
                WarnMissingEmissionPropertyOnce(material);
            }

            return;
        }

        // Material本体ではなくRuntimeコピーのEmission設定だけを切り替える。
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
            runtimeMaterialSource = null;
            return;
        }

        DestroyUnityObject(runtimeMaterial);
        runtimeMaterial = null;
        runtimeMaterialSource = null;
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

    private void WarnMissingColorPropertiesOnce(Material material)
    {
        if (warnedMissingColorProperties || material == null || HasAnyMaterialColorProperty(material))
        {
            return;
        }

        warnedMissingColorProperties = true;
        Debug.LogWarning(
            $"{nameof(PlayerDashAfterimageView)} runtime material '{material.name}' does not have _BaseColor, _Color, or _TintColor. Alpha fade may not be visible with this shader.",
            this);
    }

    private void WarnMissingEmissionPropertyOnce(Material material)
    {
        if (warnedMissingEmissionProperty || material == null)
        {
            return;
        }

        warnedMissingEmissionProperty = true;
        Debug.LogWarning(
            $"{nameof(PlayerDashAfterimageView)} runtime material '{material.name}' does not have _EmissionColor. Emission fade will be skipped with this shader.",
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

        ghost.CompleteCapture(capturedCount, GetAfterimageColor(0f, 1f), GetEmissionColor(0f));
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

        // Gradientの透明度と既存Fadeを合成し、色変化を入れても消え方の責務を維持する。
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

    private static bool HasAnyMaterialColorProperty(Material material)
    {
        return material.HasProperty(BaseColorId)
            || material.HasProperty(ColorId)
            || material.HasProperty(TintColorId);
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
