using UnityEngine;

// HandChaserEnemy のヒットボックスを部屋のサイズに応じて自動調整するヘルパークラス。
// HandChaserEnemy が内部で使用します。
public class HandChaserHitboxAdjuster
{
    private readonly Transform enemyTransform;
    private readonly Collider cachedCollider;
    private readonly HandChaserMovement movement;
    private readonly HandChaserModelView wallModelView;

    private Room assignedRoom;
    private RoomManager roomManager;

    private Vector3 initialColliderSize;
    private Vector3 initialColliderCenter;

    private readonly bool enableDebugLog;

    // コンストラクタ
    public HandChaserHitboxAdjuster(Transform enemy, Collider collider, HandChaserMovement movement, HandChaserModelView wallModelView, bool enableDebugLog = false)
    {
        this.enemyTransform = enemy;
        this.cachedCollider = collider;
        this.movement = movement;
        this.wallModelView = wallModelView;
        this.enableDebugLog = enableDebugLog;
    }

    // 初期化処理。部屋とRoomManagerの参照を取得し、初期サイズをキャッシュします。
    public void Initialize()
    {
        // 部屋の自動検索
        if (assignedRoom == null)
        {
            assignedRoom = enemyTransform.GetComponentInParent<Room>();
        }

        // RoomManagerの検索
        if (roomManager == null)
        {
            roomManager = Object.FindFirstObjectByType<RoomManager>();
        }

        // Colliderの初期サイズと中心を保存
        if (cachedCollider != null)
        {
            if (cachedCollider is BoxCollider boxCollider)
            {
                initialColliderSize = boxCollider.size;
                initialColliderCenter = boxCollider.center;
            }
            else if (cachedCollider is SphereCollider sphereCollider)
            {
                initialColliderSize = new Vector3(sphereCollider.radius * 2f, sphereCollider.radius * 2f, sphereCollider.radius * 2f);
                initialColliderCenter = sphereCollider.center;
            }
            else if (cachedCollider is CapsuleCollider capsuleCollider)
            {
                initialColliderSize = new Vector3(capsuleCollider.radius * 2f, capsuleCollider.height, capsuleCollider.radius * 2f);
                initialColliderCenter = capsuleCollider.center;
            }
        }

        if (enableDebugLog && assignedRoom != null)
        {
            Debug.Log($"[HandChaserHitboxAdjuster] Assigned to room: {assignedRoom.RoomId}", enemyTransform);
        }
    }

    // RoomManager のイベントを購読します。
    public void SubscribeToRoomEvents()
    {
        if (roomManager != null)
        {
            roomManager.OnRoomTransitionComplete += OnRoomTransitionComplete;
        }
    }

    // RoomManager のイベント購読を解除します。
    public void UnsubscribeFromRoomEvents()
    {
        if (roomManager != null)
        {
            roomManager.OnRoomTransitionComplete -= OnRoomTransitionComplete;
        }
    }

    // 現在の部屋が自分の所属部屋かチェックし、該当する場合はヒットボックスを調整します。
    public void AdjustIfInCurrentRoom()
    {
        if (roomManager != null && assignedRoom != null)
        {
            Room currentRoom = roomManager.CurrentRoom;
            if (currentRoom != null && currentRoom == assignedRoom)
            {
                AdjustHitboxToRoomSize();

                if (enableDebugLog)
                {
                    Debug.Log($"[HandChaserHitboxAdjuster] Adjusted hitbox in current room: {assignedRoom.RoomId}", enemyTransform);
                }
            }
        }
    }

    // ヒットボックスを初期サイズに戻します。
    public void ResetHitboxSize()
    {
        if (cachedCollider == null)
        {
            return;
        }

        if (cachedCollider is BoxCollider boxCollider)
        {
            boxCollider.size = initialColliderSize;
            boxCollider.center = initialColliderCenter;
        }
        else if (cachedCollider is SphereCollider sphereCollider)
        {
            sphereCollider.radius = initialColliderSize.x * 0.5f;
            sphereCollider.center = initialColliderCenter;
        }
        else if (cachedCollider is CapsuleCollider capsuleCollider)
        {
            capsuleCollider.radius = initialColliderSize.x * 0.5f;
            capsuleCollider.height = initialColliderSize.y;
            capsuleCollider.center = initialColliderCenter;
        }

        if (enableDebugLog)
        {
            Debug.Log($"[HandChaserHitboxAdjuster] Reset hitbox to initial size: {initialColliderSize}", enemyTransform);
        }
    }

    private void OnRoomTransitionComplete(Room newRoom)
    {
        // 自分が属する部屋に入った時だけヒットボックスを調整
        if (newRoom == assignedRoom)
        {
            AdjustHitboxToRoomSize();

            if (enableDebugLog)
            {
                Debug.Log($"[HandChaserHitboxAdjuster] Room transition complete. Adjusted hitbox for room: {newRoom.RoomId}", enemyTransform);
            }
        }
    }

    private void AdjustHitboxToRoomSize()
    {
        if (cachedCollider == null || movement == null || assignedRoom == null || assignedRoom.RoomBounds == null)
        {
            return;
        }

        // 移動方向がCustomの場合は調整しない
        if (movement.Direction == MoveDirection.Custom)
        {
            if (enableDebugLog)
            {
                Debug.Log("[HandChaserHitboxAdjuster] Direction is Custom. Skipping hitbox adjustment.", enemyTransform);
            }
            return;
        }

        Bounds roomBounds = assignedRoom.RoomBounds.WorldBounds;
        MoveDirection direction = movement.Direction;

        if (cachedCollider is BoxCollider boxCollider)
        {
            // 先に即死判定用のBoxColliderを部屋サイズに合わせる。
            AdjustBoxCollider(boxCollider, roomBounds, direction);

            // モデル表示はRoomBoundsではなく、調整後のBoxColliderを基準にする。
            if (wallModelView != null)
            {
                wallModelView.RebuildFromCollider(boxCollider, direction);
            }

            if (enableDebugLog)
            {
                Debug.Log(
                    $"[HandChaserHitboxAdjuster] ModelView rebuilt from collider. " +
                    $"roomSize={roomBounds.size}, colliderSize={boxCollider.size}, colliderCenter={boxCollider.center}, direction={direction}",
                    enemyTransform);
            }
        }
        else if (cachedCollider is CapsuleCollider capsuleCollider)
        {
            AdjustCapsuleCollider(capsuleCollider, roomBounds, direction);

            if (enableDebugLog)
            {
                Debug.LogWarning(
                    "[HandChaserHitboxAdjuster] CapsuleCollider はモデル生成基準に未対応です。壁モデル生成を使う場合は BoxCollider 推奨です。",
                    enemyTransform);
            }
        }
    }

    private void AdjustBoxCollider(BoxCollider boxCollider, Bounds roomBounds, MoveDirection direction)
    {
        Vector3 newSize = boxCollider.size;
        Vector3 newCenter = boxCollider.center;
        Vector3 newPosition = enemyTransform.position;

        switch (direction)
        {
            case MoveDirection.Right:
                // 右進行: 左壁から出現
                newSize.y = roomBounds.size.y;
                newCenter.y = roomBounds.center.y - newPosition.y;
                newPosition.x = roomBounds.min.x - (newCenter.x + newSize.x * 0.5f);
                break;

            case MoveDirection.Left:
                // 左進行: 右壁から出現
                newSize.y = roomBounds.size.y;
                newCenter.y = roomBounds.center.y - newPosition.y;
                newPosition.x = roomBounds.max.x - (newCenter.x - newSize.x * 0.5f);
                break;

            case MoveDirection.Up:
                // 上進行: 下壁から出現
                newSize.x = roomBounds.size.x;
                newCenter.x = roomBounds.center.x - newPosition.x;
                newPosition.y = roomBounds.min.y - (newCenter.y + newSize.y * 0.5f);
                break;

            case MoveDirection.Down:
                // 下進行: 上壁から出現
                newSize.x = roomBounds.size.x;
                newCenter.x = roomBounds.center.x - newPosition.x;
                newPosition.y = roomBounds.max.y - (newCenter.y - newSize.y * 0.5f);
                break;
        }

        // 位置を更新
        enemyTransform.position = newPosition;

        // ヒットボックスを再計算（位置が変わったので center を再計算）
        switch (direction)
        {
            case MoveDirection.Right:
            case MoveDirection.Left:
                newCenter.y = roomBounds.center.y - newPosition.y;
                break;

            case MoveDirection.Up:
            case MoveDirection.Down:
                newCenter.x = roomBounds.center.x - newPosition.x;
                break;
        }

        boxCollider.size = newSize;
        boxCollider.center = newCenter;

        if (enableDebugLog)
        {
            Debug.Log($"[HandChaserHitboxAdjuster] Adjusted position={newPosition}, BoxCollider: size={newSize}, center={newCenter} for direction {direction}", enemyTransform);
        }
    }

    private void AdjustCapsuleCollider(CapsuleCollider capsuleCollider, Bounds roomBounds, MoveDirection direction)
    {
        Vector3 newCenter = capsuleCollider.center;
        Vector3 newPosition = enemyTransform.position;

        switch (direction)
        {
            case MoveDirection.Right:
                // 右進行: 左壁から出現
                capsuleCollider.height = roomBounds.size.y;
                capsuleCollider.direction = 1; // Y軸
                newCenter.y = roomBounds.center.y - newPosition.y;

                float capsuleRadiusX = capsuleCollider.radius;
                newPosition.x = roomBounds.min.x - (newCenter.x + capsuleRadiusX);
                break;

            case MoveDirection.Left:
                // 左進行: 右壁から出現
                capsuleCollider.height = roomBounds.size.y;
                capsuleCollider.direction = 1; // Y軸
                newCenter.y = roomBounds.center.y - newPosition.y;

                capsuleRadiusX = capsuleCollider.radius;
                newPosition.x = roomBounds.max.x - (newCenter.x - capsuleRadiusX);
                break;

            case MoveDirection.Up:
                // 上進行: 下壁から出現
                capsuleCollider.height = roomBounds.size.x;
                capsuleCollider.direction = 0; // X軸
                newCenter.x = roomBounds.center.x - newPosition.x;

                float capsuleRadiusY = capsuleCollider.radius;
                newPosition.y = roomBounds.min.y - (newCenter.y + capsuleRadiusY);
                break;

            case MoveDirection.Down:
                // 下進行: 上壁から出現
                capsuleCollider.height = roomBounds.size.x;
                capsuleCollider.direction = 0; // X軸
                newCenter.x = roomBounds.center.x - newPosition.x;

                capsuleRadiusY = capsuleCollider.radius;
                newPosition.y = roomBounds.max.y - (newCenter.y - capsuleRadiusY);
                break;
        }

        // 位置を更新
        enemyTransform.position = newPosition;

        // center を再計算
        switch (direction)
        {
            case MoveDirection.Right:
            case MoveDirection.Left:
                newCenter.y = roomBounds.center.y - newPosition.y;
                break;

            case MoveDirection.Up:
            case MoveDirection.Down:
                newCenter.x = roomBounds.center.x - newPosition.x;
                break;
        }

        capsuleCollider.center = newCenter;

        if (enableDebugLog)
        {
            Debug.Log($"[HandChaserHitboxAdjuster] Adjusted position={newPosition}, CapsuleCollider: height={capsuleCollider.height}, center={newCenter} for direction {direction}", enemyTransform);
        }
    }
}
