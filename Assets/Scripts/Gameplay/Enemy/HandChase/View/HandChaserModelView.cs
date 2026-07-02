using System.Collections.Generic;
using UnityEngine;

// HandChaserEnemy の壁状モデル表示を担当する。
// 部屋サイズと進行方向に合わせて、手モデルを複数生成する。
public sealed class HandChaserModelView : MonoBehaviour
{
    [System.Serializable]
    private struct DirectionModelCorrection
    {
        [Tooltip("この方向で生成する手モデルのローカル回転補正です。")]
        public Vector3 rotationEuler;

        [Tooltip("この方向で生成する手モデルのローカルスケールです。")]
        public Vector3 scale;
    }

    [Header("参照")]
    [Tooltip("壁を構成する手モデルPrefabです。Animator付きPrefabを指定できます。")]
    [SerializeField] private Transform handModelPrefab;

    [Tooltip("生成した手モデルの親です。未設定ならこのTransform配下に生成します。")]
    [SerializeField] private Transform modelRoot;

    [Header("配置")]
    [Tooltip("手モデルを1体生成するごとに、線上で何座標分ずらすかです。小さいほどモデル同士が重なります。")]
    [SerializeField] private float modelSpacing = 1.2f;

    [Tooltip("生成する手モデル数の上限です。設定ミスによる大量生成を防ぎます。")]
    [SerializeField] private int maxModelCount = 30;

    [Tooltip("部屋サイズより少し外側まで埋めるための余白です。")]
    [SerializeField] private float fillPadding = 0.5f;

    [Tooltip("奥行き方向に何列重ねるかです。1なら一列だけです。")]
    [SerializeField] private int rowCount = 1;

    [Tooltip("生成モデル全体の配置補正です。X=並び方向、Y=進行方向、Z=奥行き方向です。")]
    [SerializeField] private Vector3 modelPlacementOffset = new Vector3(0.0f, -0.3f, 0.0f);

    [Tooltip("複数列にした時の列ごとのズレです。X=並び方向、Y=進行方向、Z=奥行き方向です。")]
    [SerializeField] private Vector3 rowPlacementOffset = new Vector3(0.15f, 0.15f, -0.2f);

    [Header("モデル補正: 共通")]

    [Tooltip("方向別補正を使わない場合に使う、生成モデルのローカル回転補正です。")]
    [SerializeField] private Vector3 modelRotationEuler;

    [Tooltip("方向別補正を使わない場合に使う、生成モデルのローカルスケールです。")]
    [SerializeField] private Vector3 modelScale = Vector3.one;

    [Header("モデル補正: 方向別")]
    [Tooltip("進行方向ごとのモデル補正を使うかどうかです。ON推奨です。")]
    [SerializeField] private bool useDirectionCorrection = true;

    [Tooltip("Right方向へ進む壁のモデル補正です。左側から右へ迫る時に使います。")]
    [SerializeField]
    private DirectionModelCorrection rightCorrection = new DirectionModelCorrection
    {
        rotationEuler = new Vector3(0.0f, 180.0f, 0.0f),
        scale = Vector3.one
    };

    [Tooltip("Left方向へ進む壁のモデル補正です。右側から左へ迫る時に使います。")]
    [SerializeField]
    private DirectionModelCorrection leftCorrection = new DirectionModelCorrection
    {
        rotationEuler = new Vector3(0.0f, 0.0f, 0.0f),
        scale = Vector3.one
    };

    [Tooltip("Up方向へ進む壁のモデル補正です。下側から上へ迫る時に使います。")]
    [SerializeField]
    private DirectionModelCorrection upCorrection = new DirectionModelCorrection
    {
        rotationEuler = new Vector3(0.0f, 0.0f, -90.0f),
        scale = Vector3.one
    };

    [Tooltip("Down方向へ進む壁のモデル補正です。上側から下へ迫る時に使います。")]
    [SerializeField]
    private DirectionModelCorrection downCorrection = new DirectionModelCorrection
    {
        rotationEuler = new Vector3(0.0f, 0.0f, 90.0f),
        scale = Vector3.one
    };

    [Header("アニメーション")]
    [Tooltip("生成時に再生するAnimatorステート名です。空なら何もしません。")]
    [SerializeField] private string idleAnimationStateName = "Idle";

    [Tooltip("手モデルごとのアニメーション開始位置のズレです。")]
    [SerializeField] private float animationOffsetStep = 0.11f;

    [Header("モデル回転")]
    [Tooltip("生成した手モデルを回転させ続けるかどうかです。")]
    [SerializeField] private bool rotateSpawnedModels = true;

    [Tooltip("1秒あたりの回転角度です。正の値で指定軸方向、負の値で逆回転します。")]
    [SerializeField] private float modelRotationSpeed = 90.0f;

    [Tooltip("回転軸です。例: Z軸なら (0, 0, 1) です。")]
    [SerializeField] private Vector3 modelRotationAxis = Vector3.forward;

    [Tooltip("Time.timeScaleの影響を受けずに回転させるかどうかです。")]
    [SerializeField] private bool useUnscaledRotationTime;

    [Header("デバッグ")]
    [Tooltip("生成ログを出すかどうかです。")]
    [SerializeField] private bool enableDebugLog;

    // Bounds比較時の許容誤差（浮動小数点の誤差を吸収）
    private const float BoundsComparisonThreshold = 0.0001f;

    // モデル間隔の最小値（0除算やモデル重複を防ぐ）
    private const float MinimumSpacing = 0.1f;

    // 生成済みの手モデルを保持するリスト
    private readonly List<Transform> spawnedModels = new();

    // 生成済みの手モデルのAnimatorを保持するリスト（アニメーション制御用）
    private readonly List<Animator> spawnedAnimators = new();

    // 前回ビルド時のBounds（再ビルドの重複を避けるため）
    private Bounds lastRoomBounds;

    // 前回ビルド時の進行方向
    private MoveDirection lastDirection;

    // 一度でもビルドしたかどうかのフラグ
    private bool hasBuilt;

    // 現在の表示状態。生成直後のモデルにも同じ状態を適用する。
    private bool currentVisible = true;

    private void Reset()
    {
        modelRoot = transform;
    }

    private void Update()
    {
        RotateSpawnedModels();
    }

    // 生成済みの手モデルを回転させる
    private void RotateSpawnedModels()
    {
        if (!rotateSpawnedModels)
        {
            return;
        }

        if (spawnedModels.Count <= 0)
        {
            return;
        }

        Vector3 axis = modelRotationAxis;
        if (axis.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        axis.Normalize();

        float deltaTime = useUnscaledRotationTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float angle = modelRotationSpeed * deltaTime;
        if (Mathf.Approximately(angle, 0.0f))
        {
            return;
        }

        for (int i = 0; i < spawnedModels.Count; i++)
        {
            Transform model = spawnedModels[i];
            if (model == null)
            {
                continue;
            }

            if (!model.gameObject.activeInHierarchy)
            {
                continue;
            }

            model.Rotate(axis, angle, Space.Self);
        }
    }

    // 生成時にアニメーションを初期再生する
    // 各手モデルごとに開始位置をずらして、壁全体で動きに変化を付ける
    private void PlayInitialAnimationIfActive(Animator animator, int index, int row)
    {
        if (animator == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(idleAnimationStateName))
        {
            return;
        }

        // 非アクティブ状態の Animator には Play / Update できない。
        if (!animator.gameObject.activeInHierarchy)
        {
            return;
        }

        // 列と行の位置からアニメーション開始位置を算出
        int animationIndex = index + row * 3;
        float normalizedTime = CalculateAnimationOffset(animationIndex);

        // 正規化時間を指定して再生開始
        animator.Play(idleAnimationStateName, 0, normalizedTime);
        animator.Update(0.0f);
    }

    // 部屋サイズと進行方向に合わせて、手モデルを再生成する。
    public void Rebuild(Bounds roomBounds, MoveDirection direction)
    {
        RebuildInternal(roomBounds, null, direction, isFromCollider: false);
    }

    // 調整済みのBoxColliderを基準に、手モデルを再生成する。
    // RoomBoundsの生サイズではなく、実際の即死判定範囲に見た目を合わせる。
    public void RebuildFromCollider(BoxCollider boxCollider, MoveDirection direction)
    {
        if (boxCollider == null)
        {
            return;
        }

        Bounds colliderLocalBounds = new Bounds(boxCollider.center, boxCollider.size);
        RebuildInternal(colliderLocalBounds, boxCollider, direction, isFromCollider: true);
    }

    // 手モデルの再ビルドの共通処理
    // Rebuild と RebuildFromCollider の両方から呼ばれる統合メソッド
    private void RebuildInternal(Bounds bounds, BoxCollider boxCollider, MoveDirection direction, bool isFromCollider)
    {
        // 必須設定チェック
        if (handModelPrefab == null)
        {
            if (enableDebugLog)
            {
                Debug.LogWarning("[HandChaserModelView] handModelPrefab が未設定です。", this);
            }
            return;
        }

        // modelRoot未設定時は自身のTransformを使用
        if (modelRoot == null)
        {
            modelRoot = transform;
        }

        // 前回と同じ条件なら再ビルドをスキップ
        if (hasBuilt && lastDirection == direction && ApproximatelySameBounds(lastRoomBounds, bounds))
        {
            return;
        }

        // 既存のモデルを全削除
        ClearSpawnedModels();

        // 今回のビルド条件を記録
        lastRoomBounds = bounds;
        lastDirection = direction;
        hasBuilt = true;

        // 壁を埋めるのに必要な長さと、生成するモデル数を計算
        float fillLength = GetFillLength(bounds, direction);
        int modelCount = CalculateModelCount(fillLength);

        // 実際にモデルを生成配置
        SpawnModels(fillLength, modelCount, direction, boxCollider);

        // デバッグログ出力
        LogRebuild(direction, bounds, boxCollider, fillLength, modelCount, isFromCollider);
    }

    // リビルド実行時のデバッグログ出力
    private void LogRebuild(MoveDirection direction, Bounds bounds, BoxCollider boxCollider, float fillLength, int modelCount, bool isFromCollider)
    {
        if (!enableDebugLog)
        {
            return;
        }

        if (isFromCollider)
        {
            Debug.Log(
                $"[HandChaserModelView] Rebuilt models from collider. " +
                $"direction={direction}, colliderSize={boxCollider.size}, colliderCenter={boxCollider.center}, " +
                $"fillLength={fillLength}, step={modelSpacing}, count={modelCount}, rowCount={rowCount}, " +
                $"viewLossyScale={transform.lossyScale}, modelRootLossyScale={modelRoot.lossyScale}",
                this);
        }
        else
        {
            Debug.Log(
                $"[HandChaserModelView] Rebuilt models from roomBounds. " +
                $"direction={direction}, roomSize={bounds.size}, fillLength={fillLength}, count={modelCount}, rowCount={rowCount}",
                this);
        }
    }

    // 生成済みの手モデルをすべて削除
    // 再ビルド前や、外部からのクリーンアップ時に使用
    public void ClearSpawnedModels()
    {
        // 逆順でループして削除（リストのインデックスずれを防ぐ）
        for (int i = spawnedModels.Count - 1; i >= 0; i--)
        {
            if (spawnedModels[i] != null)
            {
                Destroy(spawnedModels[i].gameObject);
            }
        }

        // リストとフラグをリセット
        spawnedModels.Clear();
        spawnedAnimators.Clear();
        hasBuilt = false;
    }

    public void SetVisible(bool visible)
    {
        currentVisible = visible;

        for (int i = 0; i < spawnedModels.Count; i++)
        {
            if (spawnedModels[i] != null)
            {
                spawnedModels[i].gameObject.SetActive(visible);
            }
        }

        // 非表示状態で生成された場合、生成直後の Animator.Play は失敗することがある。
        // 表示されたタイミングで、手ごとに開始位置をずらして再生し直す。
        if (visible)
        {
            PlayAnimationWithOffset(idleAnimationStateName);
        }
    }

    // 指定したアニメーションを全モデルで再生
    public void PlayAnimation(string stateName)
    {
        PlayAnimationWithOffset(stateName);
    }

    // 各手モデルに開始位置のオフセットを付けてアニメーションを再生
    // 壁全体で波のような動きを演出する
    private void PlayAnimationWithOffset(string stateName)
    {
        if (string.IsNullOrEmpty(stateName))
        {
            return;
        }

        for (int i = 0; i < spawnedAnimators.Count; i++)
        {
            Animator animator = spawnedAnimators[i];
            if (animator == null)
            {
                continue;
            }

            // 非アクティブ状態の Animator には Play / Update できない。
            if (!animator.gameObject.activeInHierarchy)
            {
                continue;
            }

            // インデックスに応じて開始位置をずらす
            float normalizedTime = CalculateAnimationOffset(i);
            animator.Play(stateName, 0, normalizedTime);
            animator.Update(0.0f);
        }
    }

    // アニメーション開始位置の正規化時間を計算
    // インデックス * オフセット幅で段階的にずらす
    private float CalculateAnimationOffset(int index)
    {
        return Mathf.Repeat(index * animationOffsetStep, 1.0f);
    }

    // 壁を埋めるために必要な長さを計算
    // 横方向の壁は高さ、縦方向の壁は幅を使用し、余白も追加
    private float GetFillLength(Bounds bounds, MoveDirection direction)
    {
        Vector3 lineAxis = GetLineAxis(direction);
        float baseLength = GetBoundsSizeAlongAxis(bounds, lineAxis);
        return baseLength + fillPadding * 2.0f;
    }

    // 進行方向を取得
    private Vector3 GetMoveAxis(MoveDirection direction)
    {
        switch (direction)
        {
            case MoveDirection.Right:
                return Vector3.right;

            case MoveDirection.Left:
                return Vector3.left;

            case MoveDirection.Up:
                return Vector3.up;

            case MoveDirection.Down:
                return Vector3.down;

            default:
                return Vector3.right;
        }
    }

    // 壁としてモデルを並べる方向を取得
    // 左右に進む壁なら縦方向、上下に進む壁なら横方向に並べる。
    private Vector3 GetLineAxis(MoveDirection direction)
    {
        switch (direction)
        {
            case MoveDirection.Right:
            case MoveDirection.Left:
                return Vector3.up;

            case MoveDirection.Up:
            case MoveDirection.Down:
                return Vector3.right;

            default:
                return Vector3.up;
        }
    }

    // Boundsのサイズを指定軸方向で取得
    private float GetBoundsSizeAlongAxis(Bounds bounds, Vector3 axis)
    {
        Vector3 absoluteAxis = new Vector3(
            Mathf.Abs(axis.x),
            Mathf.Abs(axis.y),
            Mathf.Abs(axis.z));

        return bounds.size.x * absoluteAxis.x +
               bounds.size.y * absoluteAxis.y +
               bounds.size.z * absoluteAxis.z;
    }

    // 壁基準の配置補正を、このオブジェクトのローカル座標に変換する
    // offset.x = 並び方向
    // offset.y = 進行方向
    // offset.z = 奥行き方向
    private Vector3 ConvertPlacementOffsetToLocal(Vector3 offset, MoveDirection direction)
    {
        Vector3 lineAxis = GetLineAxis(direction);
        Vector3 moveAxis = GetMoveAxis(direction);
        Vector3 depthAxis = Vector3.forward;

        return lineAxis * offset.x +
               moveAxis * offset.y +
               depthAxis * offset.z;
    }

    // 配置に使う安全なステップ量を取得
    // modelSpacing は「空き間隔」ではなく「1体ごとにずらす座標量」として扱う。
    private float GetSafeModelStep()
    {
        return Mathf.Max(MinimumSpacing, Mathf.Abs(modelSpacing));
    }

    // 必要なモデル数を算出
    // ステップ量と最大数でクランプして異常値を防ぐ
    private int CalculateModelCount(float fillLength)
    {
        float safeStep = GetSafeModelStep();
        int count = Mathf.CeilToInt(fillLength / safeStep) + 1;
        int safeMaxCount = Mathf.Max(1, maxModelCount);
        return Mathf.Clamp(count, 1, safeMaxCount);
    }

    // 計算された範囲とモデル数に基づいて、実際に手モデルを生成配置
    // 複数行にも対応
    private void SpawnModels(float fillLength, int modelCount, MoveDirection direction, BoxCollider boxCollider)
    {
        int safeRowCount = Mathf.Max(1, rowCount);
        float step = GetSafeModelStep();

        // 端から端へLerpで均等配置するのではなく、
        // 1体ごとに step 座標ずつずらして配置する。
        // これにより、step をモデルの見た目サイズより小さくすれば一部分を重ねられる。
        float start = -step * (modelCount - 1) * 0.5f;

        DirectionModelCorrection correction = GetCorrection(direction);

        // 行ごとにループ（奥行き方向の層）
        for (int row = 0; row < safeRowCount; row++)
        {
            // 各行のモデルを一定座標ずつずらして配置
            for (int i = 0; i < modelCount; i++)
            {
                float linePosition = start + step * i;

                // 位置計算（BoxColliderがあれば精密配置、なければ単純配置）
                Vector3 localPosition = GetModelPosition(linePosition, direction, boxCollider);

                Vector3 modelOffset = ConvertPlacementOffsetToLocal(modelPlacementOffset, direction);
                Vector3 rowOffset = ConvertPlacementOffsetToLocal(rowPlacementOffset, direction) * row;

                localPosition += modelOffset + rowOffset;

                // モデルをインスタンス化して配置
                Transform model = CreateHandModel(localPosition, correction);
                spawnedModels.Add(model);

                // Animatorがあれば登録して初期再生
                RegisterAnimator(model, i, row);
            }
        }
    }

    // 手モデルのインスタンス化と初期設定
    // 位置・回転・スケールを方向補正に基づいて設定
    private Transform CreateHandModel(Vector3 localPosition, DirectionModelCorrection correction)
    {
        Transform model = Instantiate(handModelPrefab, modelRoot);
        model.localPosition = localPosition;
        model.localRotation = Quaternion.Euler(correction.rotationEuler);
        model.localScale = correction.scale;

        // 生成時点の表示状態を反映する。
        // hideUntilActivated 中にRebuildされた場合、生成直後に見えてしまうのを防ぐ。
        model.gameObject.SetActive(currentVisible);

        return model;
    }

    // モデル内のAnimatorを探して登録し、初期アニメーションを再生
    private void RegisterAnimator(Transform model, int index, int row)
    {
        Animator animator = model.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            spawnedAnimators.Add(animator);
            PlayInitialAnimationIfActive(animator, index, row);
        }
    }

    // モデルの配置位置を取得
    private Vector3 GetModelPosition(float linePosition, MoveDirection direction, BoxCollider boxCollider)
    {
        if (boxCollider == null)
        {
            // Collider無し：単純な線上配置
            return GetLocalPositionFromLine(linePosition, direction);
        }

        // Collider有り：Collider範囲に沿った精密配置
        Vector3 colliderLocalPosition = GetColliderLocalPosition(boxCollider, linePosition, direction);
        return ConvertOwnerLocalToModelRootLocal(colliderLocalPosition);
    }

    // 進行方向に応じたモデル補正を取得
    // 各方向で手の向きや位置を調整して、自然に見えるようにする
    private DirectionModelCorrection GetCorrection(MoveDirection direction)
    {
        // 方向別補正を使用しない場合は共通設定を返す
        if (!useDirectionCorrection)
        {
            return new DirectionModelCorrection
            {
                rotationEuler = modelRotationEuler,
                scale = modelScale
            };
        }

        // 各進行方向に対応した補正を返す
        switch (direction)
        {
            case MoveDirection.Right:
                return rightCorrection;

            case MoveDirection.Left:
                return leftCorrection;

            case MoveDirection.Up:
                return upCorrection;

            case MoveDirection.Down:
                return downCorrection;

            default:
                return new DirectionModelCorrection
                {
                    rotationEuler = modelRotationEuler,
                    scale = modelScale
                };
        }
    }



    // BoxColliderのローカル座標上で、線上の位置を算出
    // 横方向の壁ならY軸、縦方向の壁ならX軸に配置
    private Vector3 GetColliderLocalPosition(BoxCollider boxCollider, float linePosition, MoveDirection direction)
    {
        Vector3 lineAxis = GetLineAxis(direction);
        return boxCollider.center + lineAxis * linePosition;
    }

    // オーナー（HandChaserEnemy本体）のローカル座標を、modelRootのローカル座標に変換
    // スケールや階層構造が異なる場合に必要な座標変換
    private Vector3 ConvertOwnerLocalToModelRootLocal(Vector3 ownerLocalPosition)
    {
        Vector3 worldPosition = transform.TransformPoint(ownerLocalPosition);
        return modelRoot.InverseTransformPoint(worldPosition);
    }

    // 線上の位置から、シンプルなローカル座標を計算
    // Collider無しの場合に使用する簡易配置
    private Vector3 GetLocalPositionFromLine(float linePosition, MoveDirection direction)
    {
        Vector3 lineAxis = GetLineAxis(direction);
        return lineAxis * linePosition;
    }

    // 2つのBoundsがほぼ同じかどうかを判定
    // 浮動小数点の誤差を考慮した比較
    private bool ApproximatelySameBounds(Bounds a, Bounds b)
    {
        return Vector3.SqrMagnitude(a.center - b.center) < BoundsComparisonThreshold &&
               Vector3.SqrMagnitude(a.size - b.size) < BoundsComparisonThreshold;
    }
}