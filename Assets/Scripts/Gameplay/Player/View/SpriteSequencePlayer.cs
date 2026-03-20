using UnityEngine;

// スプライト連番再生の再生モード。
public enum SpriteSequencePlaybackMode
{
    // 終端まで行ったら先頭へ戻って繰り返す。
    Loop,

    // 終端まで行ったら再生停止する。
    // 最終フレームで止まる実装になっている。
    OneShot,

    // 終端まで行ったら最終フレームを維持したまま停止する。
    // 現在実装上は OneShot と同じ終端挙動。
    HoldLastFrame
}

[System.Serializable]
public sealed class SpriteSequenceClip
{
    [Header("有効化")]
    [Tooltip("OFF の場合、このクリップは PlayerView の状態解決対象から除外されます。")]
    public bool enabled = true;

    [Header("スプライト列")]
    [Tooltip("再生に使用するスプライト配列です。startFrame から endFrame の範囲が実際の再生対象になります。")]
    // 再生元となるスプライト列。
    public Sprite[] sprites;

    [Header("開始フレーム")]
    [Tooltip("再生開始フレーム番号です。配列範囲外でも実行時に有効範囲へ補正されます。")]
    // 再生開始フレーム。
    [Min(0)]
    public int startFrame;

    [Header("終了フレーム")]
    [Tooltip("再生終了フレーム番号です。配列範囲外でも実行時に有効範囲へ補正されます。endFrame が startFrame より小さい場合は startFrame に補正されます。")]
    // 再生終了フレーム。
    [Min(0)]
    public int endFrame;

    [Header("再生速度(FPS)")]
    [Tooltip("1秒あたりに進めるフレーム数です。0 以下の場合はアニメーションを進めず、startFrame で固定表示します。")]
    // 再生速度。
    [Min(0f)]
    public float fps = 12f;

    [Header("再生モード")]
    [Tooltip("終端到達後の挙動を指定します。Loop は先頭へ戻って継続、OneShot / HoldLastFrame は最終フレームで停止します。")]
    // 終端到達後の再生モード。
    public SpriteSequencePlaybackMode playbackMode = SpriteSequencePlaybackMode.Loop;

    [Header("最低維持時間(秒)")]
    [Tooltip("この状態に入ったあと、最低限維持する秒数です。0 の場合は即時切り替え可能です。")]
    [Min(0f)]
    public float minimumDuration = 0f;

    [Header("追加反転(X)")]
    [Tooltip("通常向きに対して追加で X 反転するかを指定します。")]
    public bool extraFlipX = false;

    [Header("追加反転(Y)")]
    [Tooltip("クリップ固有の Y 反転を指定します。")]
    public bool extraFlipY = false;

    [Header("スケール倍率")]
    [Tooltip("ViewRoot の baseScale に乗算する倍率です。")]
    [Min(0f)]
    public float scaleMultiplier = 1f;

    [Header("ローカルオフセット")]
    [Tooltip("ViewRoot の baseLocalOffset に加算するオフセットです。")]
    public Vector3 localOffset = Vector3.zero;
}

// PlayerView 専用の最小スプライト連番再生クラス。
// 責務:
// - SpriteSequenceClip の内容を検証する
// - 現在フレームを進行させる
// - SpriteRenderer に現在フレームの Sprite を反映する
//
// 非責務:
// - Idle / Run / Jump など「どの Clip を再生するか」の決定
// - ブレンド、クロスフェード、高度な遷移制御
[System.Serializable]
public sealed class SpriteSequencePlayer
{
    // 描画先の SpriteRenderer。
    private SpriteRenderer spriteRenderer;

    // 現在再生中の Clip。
    private SpriteSequenceClip currentClip;

    // 現在表示しているフレーム番号。
    private int currentFrame;

    // Validate 後の安全な開始フレーム。
    private int validatedStartFrame;

    // Validate 後の安全な終了フレーム。
    private int validatedEndFrame;

    // 次フレームへ進めるまでの経過時間。
    private float frameTimer;

    // 現在再生中かどうか。
    // false のとき Tick では進行しない。
    private bool isPlaying;

    // 外部参照用の読み取り専用プロパティ。
    public SpriteSequenceClip CurrentClip => currentClip;
    public int CurrentFrame => currentFrame;

    // 描画先 Renderer を設定する。
    public void SetRenderer(SpriteRenderer targetRenderer)
    {
        spriteRenderer = targetRenderer;
    }

    // 指定 Clip の再生を開始する。
    public void Play(SpriteSequenceClip clip)
    {
        currentClip = clip;
        frameTimer = 0f;

        // Clip が無効なら再生停止し、表示もクリアする。
        if (!TryValidateClip(clip, out validatedStartFrame, out validatedEndFrame))
        {
            isPlaying = false;

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = null;
            }

            currentFrame = 0;
            return;
        }

        // 検証済み開始フレームから再生開始。
        currentFrame = validatedStartFrame;
        isPlaying = true;
        ApplySprite();
    }

    // 時間経過に応じて再生を進める。
    public void Tick(float deltaTime)
    {
        // 再生中でない、または Clip 未設定なら何もしない。
        if (!isPlaying || currentClip == null)
        {
            return;
        }

        // FPS が 0 以下ならアニメーションは進めず、
        // startFrame 固定表示とする。
        if (currentClip.fps <= 0f)
        {
            currentFrame = validatedStartFrame;
            ApplySprite();
            return;
        }

        float frameDuration = 1f / currentClip.fps;

        // 負の deltaTime は無視して安全側で扱う。
        frameTimer += Mathf.Max(0f, deltaTime);

        // 低フレームレート時でも取りこぼさないよう、
        // 必要回数だけフレームを進める。
        while (frameTimer >= frameDuration && isPlaying)
        {
            frameTimer -= frameDuration;
            StepFrame();
        }
    }

    // 1フレーム分だけ再生を進める。
    private void StepFrame()
    {
        // まだ終端前なら普通に次フレームへ進める。
        if (currentFrame < validatedEndFrame)
        {
            currentFrame++;
            ApplySprite();
            return;
        }

        // 終端に到達していた場合は再生モードごとの処理を行う。
        switch (currentClip.playbackMode)
        {
            case SpriteSequencePlaybackMode.Loop:
                currentFrame = validatedStartFrame;
                isPlaying = true;
                break;

            case SpriteSequencePlaybackMode.OneShot:
            case SpriteSequencePlaybackMode.HoldLastFrame:
                currentFrame = validatedEndFrame;
                isPlaying = false;
                break;
        }

        ApplySprite();
    }

    // 現在フレームの Sprite を Renderer へ反映する。
    private void ApplySprite()
    {
        if (spriteRenderer == null || currentClip == null || currentClip.sprites == null)
        {
            return;
        }

        // 念のため配列外参照を防ぐ。
        if (currentFrame < 0 || currentFrame >= currentClip.sprites.Length)
        {
            return;
        }

        spriteRenderer.sprite = currentClip.sprites[currentFrame];
    }

    // Clip の再生範囲を安全に検証し、使える開始/終了フレームへ補正する。
    private static bool TryValidateClip(SpriteSequenceClip clip, out int startFrame, out int endFrame)
    {
        startFrame = 0;
        endFrame = 0;

        // Clip 自体、配列、要素数のどれかが無効なら再生不可。
        if (clip == null || clip.sprites == null || clip.sprites.Length == 0)
        {
            return false;
        }

        int maxIndex = clip.sprites.Length - 1;

        // 指定範囲を配列サイズ内へ丸める。
        startFrame = Mathf.Clamp(clip.startFrame, 0, maxIndex);
        endFrame = Mathf.Clamp(clip.endFrame, 0, maxIndex);

        // end が start より前なら、安全側で start に揃える。
        if (endFrame < startFrame)
        {
            endFrame = startFrame;
        }

        return true;
    }
}