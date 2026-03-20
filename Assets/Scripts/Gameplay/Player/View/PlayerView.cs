using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerView : MonoBehaviour
{
    private enum AnimState
    {
        Idle,
        WalkLoop,
        Land,
        JumpStart,
        JumpRise,
        JumpToFall,
        Fall,
        WallSlide,
        WallJump,
        Step
    }

    [Header("参照: PlayerController")]
    [SerializeField] private PlayerController playerController;

    [Header("参照: SpriteRenderer")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("参照: ViewRoot")]
    [SerializeField] private Transform viewRoot;

    [Header("閾値")]
    [SerializeField] private float walkThreshold = 0.05f;
    [SerializeField] private float riseThreshold = 0.05f;

    [Header("ベース見た目設定")]
    [SerializeField] private Vector3 baseLocalOffset = Vector3.zero;
    [SerializeField] private Vector3 baseScale = Vector3.one;

    [Header("地上: Idle")]
    [SerializeField] private SpriteSequenceClip idleClip = new SpriteSequenceClip
    {
        playbackMode = SpriteSequencePlaybackMode.HoldLastFrame,
        minimumDuration = 0f
    };

    [Header("地上: WalkLoop")]
    [SerializeField] private SpriteSequenceClip walkLoopClip = new SpriteSequenceClip
    {
        fps = 16f,
        playbackMode = SpriteSequencePlaybackMode.Loop,
        minimumDuration = 0f
    };

    [Header("地上: Land")]
    [SerializeField] private SpriteSequenceClip landClip = new SpriteSequenceClip
    {
        fps = 10f,
        playbackMode = SpriteSequencePlaybackMode.OneShot,
        minimumDuration = 0.08f
    };

    [Header("空中: JumpStart")]
    [SerializeField] private SpriteSequenceClip jumpStartClip = new SpriteSequenceClip
    {
        fps = 24f,
        playbackMode = SpriteSequencePlaybackMode.OneShot,
        minimumDuration = 0.10f
    };

    [Header("空中: JumpRise")]
    [SerializeField] private SpriteSequenceClip jumpRiseClip = new SpriteSequenceClip
    {
        fps = 0f,
        playbackMode = SpriteSequencePlaybackMode.HoldLastFrame,
        minimumDuration = 0f
    };

    [Header("空中: JumpToFall")]
    [SerializeField] private SpriteSequenceClip jumpToFallClip = new SpriteSequenceClip
    {
        fps = 24f,
        playbackMode = SpriteSequencePlaybackMode.OneShot,
        minimumDuration = 0.08f
    };

    [Header("空中: Fall")]
    [SerializeField] private SpriteSequenceClip fallClip = new SpriteSequenceClip
    {
        fps = 0f,
        playbackMode = SpriteSequencePlaybackMode.HoldLastFrame,
        minimumDuration = 0f
    };

    [Header("壁: WallSlide")]
    [SerializeField] private SpriteSequenceClip wallSlideClip = new SpriteSequenceClip
    {
        fps = 0f,
        playbackMode = SpriteSequencePlaybackMode.HoldLastFrame,
        minimumDuration = 0f
    };

    [Header("壁: WallJump")]
    [SerializeField] private SpriteSequenceClip wallJumpClip = new SpriteSequenceClip
    {
        fps = 24f,
        playbackMode = SpriteSequencePlaybackMode.OneShot,
        minimumDuration = 0.10f
    };

    [Header("特殊: Step")]
    [SerializeField] private SpriteSequenceClip stepClip = new SpriteSequenceClip
    {
        fps = 0f,
        playbackMode = SpriteSequencePlaybackMode.HoldLastFrame,
        minimumDuration = 0f
    };

    [Header("デバッグ(Runtime)")]
    [SerializeField] private AnimState currentAnimState;
    [SerializeField] private AnimState desiredAnimState;
    [SerializeField] private float currentStateElapsed;
    [SerializeField] private float currentStateLockRemaining;
    [SerializeField] private string currentClipName = "None";
    [SerializeField] private int currentFrameIndex;

    private readonly SpriteSequencePlayer sequencePlayer = new SpriteSequencePlayer();

    private SpriteSequenceClip currentClip;
    private bool hasState;
    private bool wasGrounded = true;
    private bool airborneFromJump;

    private void Awake()
    {
        if (playerController == null)
        {
            playerController = GetComponentInParent<PlayerController>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (viewRoot == null)
        {
            viewRoot = transform;
        }

        if (playerController == null || spriteRenderer == null || viewRoot == null)
        {
            Debug.LogError("PlayerView references are missing.", this);
            enabled = false;
            return;
        }

        sequencePlayer.SetRenderer(spriteRenderer);

        if (baseScale == Vector3.zero)
        {
            baseScale = viewRoot.localScale;
        }
    }

    private void Update()
    {
        PlayerController.VisualState state = playerController.CurrentVisualState;
        UpdateAirborneContext(state);

        desiredAnimState = ResolveDesiredState(state);
        TickStateTransition(state, desiredAnimState, Time.deltaTime);

        sequencePlayer.Tick(Time.deltaTime);
        currentFrameIndex = sequencePlayer.CurrentFrame;

        ApplyVisualCorrection(state);
    }

    private void UpdateAirborneContext(PlayerController.VisualState state)
    {
        if (state.justJumped)
        {
            airborneFromJump = true;
        }

        if (!wasGrounded && state.isGrounded)
        {
            airborneFromJump = false;
        }

        if (wasGrounded && !state.isGrounded && !state.justJumped)
        {
            airborneFromJump = false;
        }

        wasGrounded = state.isGrounded;
    }

    private void TickStateTransition(PlayerController.VisualState state, AnimState desired, float deltaTime)
    {
        if (!hasState)
        {
            ForceEnterState(desired);
            return;
        }

        currentStateElapsed += Mathf.Max(0f, deltaTime);
        currentStateLockRemaining = Mathf.Max(0f, (currentClip != null ? currentClip.minimumDuration : 0f) - currentStateElapsed);

        if (desired == currentAnimState)
        {
            return;
        }

        if (currentStateLockRemaining > 0f && !CanInterruptDuringLock(currentAnimState, desired))
        {
            return;
        }

        ForceEnterState(desired);
    }

    private void ForceEnterState(AnimState next)
    {
        hasState = true;
        currentAnimState = next;
        currentStateElapsed = 0f;

        currentClip = GetClip(next);
        currentStateLockRemaining = currentClip != null ? currentClip.minimumDuration : 0f;
        currentClipName = currentClip != null ? next.ToString() : "None";

        sequencePlayer.Play(currentClip);
        currentFrameIndex = sequencePlayer.CurrentFrame;
    }

    private bool CanInterruptDuringLock(AnimState current, AnimState desired)
    {
        if (desired == AnimState.Step)
        {
            return true;
        }

        if (desired == AnimState.WallJump && current == AnimState.JumpStart)
        {
            return true;
        }

        if (desired == AnimState.WallSlide && IsAirNormalState(current))
        {
            return true;
        }

        return false;
    }

    private static bool IsAirNormalState(AnimState state)
    {
        return state == AnimState.JumpStart || state == AnimState.JumpRise || state == AnimState.JumpToFall || state == AnimState.Fall;
    }

    private AnimState ResolveDesiredState(PlayerController.VisualState state)
    {
        if (state.isStepping && IsClipEnabled(stepClip))
        {
            return AnimState.Step;
        }

        if (state.justWallJumped && IsClipEnabled(wallJumpClip))
        {
            return AnimState.WallJump;
        }

        if (state.isWallSliding && IsClipEnabled(wallSlideClip))
        {
            return AnimState.WallSlide;
        }

        if (state.justJumped && !state.justWallJumped && IsClipEnabled(jumpStartClip))
        {
            return AnimState.JumpStart;
        }

        if (state.justLanded && IsClipEnabled(landClip))
        {
            return AnimState.Land;
        }

        if (state.justCrossedApex && IsClipEnabled(jumpToFallClip))
        {
            return AnimState.JumpToFall;
        }

        if (!state.isGrounded && state.velocityY > riseThreshold)
        {
            if (IsClipEnabled(jumpRiseClip))
            {
                return AnimState.JumpRise;
            }

            if (airborneFromJump && IsClipEnabled(jumpStartClip))
            {
                return AnimState.JumpStart;
            }

            return hasState ? currentAnimState : AnimState.Idle;
        }

        if (!state.isGrounded && !state.isWallSliding && !state.isStepping)
        {
            if (IsClipEnabled(fallClip))
            {
                return AnimState.Fall;
            }

            if (airborneFromJump && IsClipEnabled(jumpToFallClip))
            {
                return AnimState.JumpToFall;
            }

            return hasState ? currentAnimState : AnimState.Idle;
        }

        if (state.isGrounded && Mathf.Abs(state.velocityX) >= walkThreshold && IsClipEnabled(walkLoopClip))
        {
            return AnimState.WalkLoop;
        }

        if (IsClipEnabled(idleClip))
        {
            return AnimState.Idle;
        }

        return hasState ? currentAnimState : AnimState.Idle;
    }

    private void ApplyVisualCorrection(PlayerController.VisualState state)
    {
        bool facingFlipX = state.facing < 0;
        bool extraFlipX = currentClip != null && currentClip.extraFlipX;
        bool extraFlipY = currentClip != null && currentClip.extraFlipY;

        spriteRenderer.flipX = facingFlipX ^ extraFlipX;
        spriteRenderer.flipY = extraFlipY;

        float scaleMultiplier = currentClip != null ? currentClip.scaleMultiplier : 1f;
        Vector3 localOffset = currentClip != null ? currentClip.localOffset : Vector3.zero;

        viewRoot.localScale = baseScale * scaleMultiplier;
        viewRoot.localPosition = baseLocalOffset + localOffset;
    }

    private SpriteSequenceClip GetClip(AnimState state)
    {
        switch (state)
        {
            case AnimState.Idle:
                return idleClip;
            case AnimState.WalkLoop:
                return walkLoopClip;
            case AnimState.Land:
                return landClip;
            case AnimState.JumpStart:
                return jumpStartClip;
            case AnimState.JumpRise:
                return jumpRiseClip;
            case AnimState.JumpToFall:
                return jumpToFallClip;
            case AnimState.Fall:
                return fallClip;
            case AnimState.WallSlide:
                return wallSlideClip;
            case AnimState.WallJump:
                return wallJumpClip;
            case AnimState.Step:
                return stepClip;
            default:
                return idleClip;
        }
    }

    private static bool IsClipEnabled(SpriteSequenceClip clip)
    {
        return clip != null && clip.enabled;
    }
}