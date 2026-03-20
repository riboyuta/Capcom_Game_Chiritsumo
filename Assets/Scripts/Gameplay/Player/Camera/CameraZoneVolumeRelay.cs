using UnityEngine;

// 同一 GameObject への多重アタッチを防ぐ。
// ZoneVolume の中継用途なので 1 つで十分。
[DisallowMultipleComponent]

// Trigger 判定に使う BoxCollider を必須にする。
[RequireComponent(typeof(BoxCollider))]
public sealed class CameraZoneVolumeRelay : MonoBehaviour
{
    // 通知先となる親の CameraZone。
    // 通常は Awake / Reset / OnValidate で親から自動解決する。
    private CameraZone ownerZone;

    // この ZoneVolume 自身の BoxCollider。
    // Trigger 判定専用として扱う。
    private BoxCollider boxCollider;

    private void Reset()
    {
        // コンポーネント追加直後や Reset 時に参照を補完する。
        ResolveReferences();
    }

    private void Awake()
    {
        // 実行開始時に必要参照を補完する。
        ResolveReferences();
    }

    private void OnValidate()
    {
        // Inspector 変更時にも参照補完と Trigger 設定を維持する。
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        // 自身の BoxCollider を未取得なら補完する。
        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider>();
        }

        // ZoneVolume は物理衝突ではなく侵入判定専用なので Trigger に固定する。
        if (boxCollider != null)
        {
            boxCollider.isTrigger = true;
        }

        // ownerZone が未設定なら親から CameraZone を探して補完する。
        if (ownerZone == null && transform.parent != null)
        {
            ownerZone = transform.parent.GetComponent<CameraZone>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 通知先が解決できていないなら何もしない。
        if (ownerZone == null)
        {
            return;
        }

        // Trigger Enter を親の CameraZone へ中継する。
        ownerZone.NotifyEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        // 通知先が解決できていないなら何もしない。
        if (ownerZone == null)
        {
            return;
        }

        // Trigger Exit を親の CameraZone へ中継する。
        ownerZone.NotifyExit(other);
    }
}