using UnityEngine;

public enum SonarChargerDirectionMode
{
    None,
    FlipScaleX,
    FaceMoveDirection2D
}

[DisallowMultipleComponent]
public sealed class SonarChargerView : MonoBehaviour
{
    [Header("見た目Root")]
    [Tooltip("揺れ、向き変更、表示制御を適用する見た目Rootです。Root本体ではなく子のVisualRootを推奨します。")]
    [SerializeField] private Transform visualRoot;

    [Header("表示制御Renderer")]
    [Tooltip("表示/非表示を切り替えるRenderer群です。未設定時は子を含めて自動取得します。MeshRenderer / SkinnedMeshRenderer も対象です。")]
    [SerializeField] private Renderer[] controlledRenderers;

    [Header("向き制御方式")]
    [Tooltip("見た目の向き制御方式です。モデル仕様なら FaceMoveDirection2D を使います。")]
    [SerializeField] private SonarChargerDirectionMode directionMode = SonarChargerDirectionMode.FaceMoveDirection2D;

    [Header("モデル正面補正")]
    [Tooltip("進行方向に正面を向ける時、モデル側の基準回転を補正します。モデルの正面軸が合わない場合に調整します。")]
    [SerializeField] private Vector3 modelForwardRotationOffsetEuler = Vector3.zero;

    [Tooltip("進行方向へ向きを変える速度です。0以下なら即時回転します。")]
    [SerializeField] private float faceDirectionTurnSpeed = 720.0f;

    [Tooltip("この値未満の方向ベクトルは無視します。微小移動で向きが暴れないようにします。")]
    [SerializeField] private float minDirectionSqrMagnitude = 0.0001f;

    [Header("Animator")]
    [Tooltip("モデルのAnimatorです。未設定なら子階層から自動取得します。アニメーションを使わない場合は未設定で構いません。")]
    [SerializeField] private Animator animator;

    [Tooltip("Animatorに状態名でCrossFadeするかです。")]
    [SerializeField] private bool useAnimatorStateCrossFade = false;

    [Tooltip("Animator CrossFadeの補間時間です。")]
    [SerializeField] private float animatorCrossFadeDuration = 0.05f;

    [Header("Animator State名")]
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private string followStateName = "Move";
    [SerializeField] private string alertStateName = "Alert";
    [SerializeField] private string chargeStateName = "Charge";
    [SerializeField] private string reboundStateName = "Rebound";
    [SerializeField] private string stunStateName = "Stun";

    private Transform ownerRoot;

    private Vector3 initialVisualLocalPosition;
    private Vector3 initialVisualLocalScale;
    private Quaternion initialVisualLocalRotation;
    private Quaternion targetVisualLocalRotation;

    private bool hasCapturedInitialState;
    private string currentAnimatorStateName;

    public void Initialize(Transform owner)
    {
        ownerRoot = owner;
        InitializeReferences();
        CaptureInitialState();
    }

    public void CaptureInitialState()
    {
        if (hasCapturedInitialState)
        {
            return;
        }

        if (visualRoot != null)
        {
            initialVisualLocalPosition = visualRoot.localPosition;
            initialVisualLocalScale = visualRoot.localScale;
            initialVisualLocalRotation = visualRoot.localRotation;
            targetVisualLocalRotation = initialVisualLocalRotation;
        }

        hasCapturedInitialState = true;
    }

    public void ResetToInitialState()
    {
        if (visualRoot != null)
        {
            visualRoot.localPosition = initialVisualLocalPosition;
            visualRoot.localScale = initialVisualLocalScale;
            visualRoot.localRotation = initialVisualLocalRotation;
            targetVisualLocalRotation = initialVisualLocalRotation;
        }

        currentAnimatorStateName = null;
        PlayIdle();
    }

    public void TickAlert(float stateElapsedTime, SonarChargerSettings settings)
    {
        if (visualRoot == null || settings == null)
        {
            return;
        }

        if (ShouldSkipVisualOffset())
        {
            return;
        }

        float amplitude = CalculateAlertAmplitude(stateElapsedTime, settings);
        float frequency = settings.alertShakeFrequency;

        Vector3 offset = CalculateShakeOffset(stateElapsedTime, frequency, amplitude);
        visualRoot.localPosition = initialVisualLocalPosition + offset;
    }

    public void ResetVisualOffset()
    {
        if (visualRoot == null)
        {
            return;
        }

        visualRoot.localPosition = initialVisualLocalPosition;
    }

    public void ApplyDirection(Vector3 direction)
    {
        if (visualRoot == null)
        {
            return;
        }

        direction.z = 0.0f;

        if (direction.sqrMagnitude < minDirectionSqrMagnitude)
        {
            return;
        }

        switch (directionMode)
        {
            case SonarChargerDirectionMode.None:
                return;

            case SonarChargerDirectionMode.FlipScaleX:
                ApplyScaleDirection(direction.x);
                return;

            case SonarChargerDirectionMode.FaceMoveDirection2D:
                ApplyFaceMoveDirection2D(direction);
                return;
        }
    }

    public void SetVisible(bool visible)
    {
        if (controlledRenderers == null)
        {
            return;
        }

        for (int i = 0; i < controlledRenderers.Length; i++)
        {
            if (controlledRenderers[i] != null)
            {
                controlledRenderers[i].enabled = visible;
            }
        }
    }

    public void PlayIdle()
    {
        CrossFadeAnimatorState(idleStateName);
    }

    public void PlayFollow()
    {
        CrossFadeAnimatorState(followStateName);
    }

    public void PlayAlert()
    {
        CrossFadeAnimatorState(alertStateName);
    }

    public void PlayCharge()
    {
        CrossFadeAnimatorState(chargeStateName);
    }

    public void PlayRebound()
    {
        CrossFadeAnimatorState(reboundStateName);
    }

    public void PlayStun()
    {
        CrossFadeAnimatorState(stunStateName);
    }

    private void InitializeReferences()
    {
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        if (controlledRenderers == null || controlledRenderers.Length == 0)
        {
            controlledRenderers = GetComponentsInChildren<Renderer>(true);
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
    }

    private bool ShouldSkipVisualOffset()
    {
        // Root本体を揺らすと当たり判定や境界判定も揺れるため、Root指定時は揺らさない。
        return ownerRoot != null && visualRoot == ownerRoot;
    }

    private float CalculateAlertAmplitude(float stateElapsedTime, SonarChargerSettings settings)
    {
        float amplitude = settings.alertShakeAmplitude;

        if (settings.growShakeTowardCharge && settings.alertTime > 0.0f)
        {
            float t = Mathf.Clamp01(stateElapsedTime / settings.alertTime);
            amplitude *= Mathf.Lerp(0.35f, 1.0f, t);
        }

        return amplitude;
    }

    private Vector3 CalculateShakeOffset(float time, float frequency, float amplitude)
    {
        float x = Mathf.Sin(time * frequency) * amplitude;
        float y = Mathf.Cos(time * frequency * 0.73f) * amplitude;
        return new Vector3(x, y, 0.0f);
    }

    private void ApplyScaleDirection(float xDirection)
    {
        Vector3 scale = initialVisualLocalScale;
        scale.x = Mathf.Abs(initialVisualLocalScale.x) * Mathf.Sign(xDirection);
        visualRoot.localScale = scale;
    }

    private void ApplyFaceMoveDirection2D(Vector3 direction)
    {
        Vector3 normalizedDirection = direction.normalized;

        // XY平面上の進行方向を角度に変換する。
        // 右 = 0度、上 = 90度、左 = 180度、下 = -90度。
        float angleZ = Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg;

        Quaternion offsetRotation = Quaternion.Euler(modelForwardRotationOffsetEuler);

        targetVisualLocalRotation =
            initialVisualLocalRotation *
            Quaternion.Euler(0.0f, 0.0f, angleZ) *
            offsetRotation;

        if (faceDirectionTurnSpeed <= 0.0f)
        {
            visualRoot.localRotation = targetVisualLocalRotation;
            return;
        }

        visualRoot.localRotation = Quaternion.RotateTowards(
            visualRoot.localRotation,
            targetVisualLocalRotation,
            faceDirectionTurnSpeed * Time.deltaTime);
    }

    private void CrossFadeAnimatorState(string stateName)
    {
        if (!useAnimatorStateCrossFade || animator == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(stateName))
        {
            return;
        }

        if (currentAnimatorStateName == stateName)
        {
            return;
        }

        currentAnimatorStateName = stateName;
        animator.CrossFadeInFixedTime(stateName, Mathf.Max(0.0f, animatorCrossFadeDuration));
    }
}