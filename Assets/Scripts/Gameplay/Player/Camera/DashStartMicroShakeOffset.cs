using UnityEngine;

/// <summary>
/// ダッシュ開始時だけ使う極小カメラシェイクのオフセット計算を担当する。
/// Transform は直接変更せず、PlayerCameraController が最終位置へ加算する値だけを返す。
/// </summary>
internal sealed class DashStartMicroShakeOffset
{
    private const float DirectionSqrMagnitudeThreshold = 0.0001f;

    private Vector2 direction;
    private float elapsed;
    private float duration;
    private float amplitude;

    internal Vector3 CurrentOffset { get; private set; }
    internal bool IsActive { get; private set; }

    internal void Play(Vector2 dashDirection, float shakeDuration, float shakeAmplitude)
    {
        duration = Mathf.Max(0f, shakeDuration);
        amplitude = Mathf.Max(0f, shakeAmplitude);

        if (duration <= 0f || amplitude <= 0f || dashDirection.sqrMagnitude <= DirectionSqrMagnitudeThreshold)
        {
            Clear();
            return;
        }

        direction = dashDirection.normalized;
        elapsed = 0f;
        IsActive = true;

        // 入力成立の手応えとして、最初だけダッシュ方向の逆側へ極小に押す。
        CurrentOffset = new Vector3(-direction.x, -direction.y, 0f) * amplitude;
    }

    internal void Tick(float deltaTime)
    {
        if (!IsActive)
        {
            return;
        }

        elapsed += Mathf.Max(0f, deltaTime);

        float t = duration <= 0f
            ? 1f
            : Mathf.Clamp01(elapsed / duration);

        if (t >= 1f)
        {
            Clear();
            return;
        }

        float smoothStep = t * t * (3f - 2f * t);
        float damping = 1f - smoothStep;
        float oscillation = Mathf.Cos(t * Mathf.PI * 2f);

        CurrentOffset = new Vector3(-direction.x, -direction.y, 0f)
            * amplitude
            * damping
            * oscillation;
    }

    internal void Clear()
    {
        direction = Vector2.zero;
        elapsed = 0f;
        duration = 0f;
        amplitude = 0f;
        CurrentOffset = Vector3.zero;
        IsActive = false;
    }
}
