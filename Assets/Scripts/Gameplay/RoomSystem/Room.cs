using UnityEngine;

[DisallowMultipleComponent]
public sealed class Room : MonoBehaviour
{
    [Header("部屋識別")]
    [Tooltip("デバッグ表示やログ出力に使う部屋IDです。重複しない名前を推奨します。")]
    [SerializeField] private string roomId = "Room_01";

    [Header("部屋境界")]
    [Tooltip("この部屋のカメラ境界と部屋矩形を表す CameraBounds です。")]
    [SerializeField] private CameraBounds roomBounds;

    [Header("隣接部屋: 左右")]
    [Tooltip("この部屋の左へ抜けた時の遷移先です。未設定なら左遷移はできません。")]
    [SerializeField] private Room leftRoom;

    [Tooltip("この部屋の右へ抜けた時の遷移先です。未設定なら右遷移はできません。")]
    [SerializeField] private Room rightRoom;

    [Header("隣接部屋: 上下")]
    [Tooltip("この部屋の上へ抜けた時の遷移先です。未設定なら上遷移はできません。")]
    [SerializeField] private Room upRoom;

    [Tooltip("この部屋の下へ抜けた時の遷移先です。未設定なら下遷移はできません。")]
    [SerializeField] private Room downRoom;

    [Header("復帰位置: 左右")]
    [Tooltip("左側からこの部屋へ入った時、または左遷移に対応する復帰位置です。未使用なら未設定で構いません。")]
    [SerializeField] private Transform respawnFromLeft;

    [Tooltip("右側からこの部屋へ入った時、または右遷移に対応する復帰位置です。未使用なら未設定で構いません。")]
    [SerializeField] private Transform respawnFromRight;

    [Header("復帰位置: 上下")]
    [Tooltip("上側からこの部屋へ入った時、または上遷移に対応する復帰位置です。未使用なら未設定で構いません。")]
    [SerializeField] private Transform respawnFromUp;

    [Tooltip("下側からこの部屋へ入った時、または下遷移に対応する復帰位置です。未使用なら未設定で構いません。")]
    [SerializeField] private Transform respawnFromDown;

    [Header("カメラ注視位置")]
    [Tooltip("この部屋で使うカメラ注視オフセットです。Xで左右寄せ、Yで上下寄せを調整します。")]
    [SerializeField] private Vector2 roomFocusOffset = Vector2.zero;

    [Header("カメラ追従スムーズ上書き")]
    [Tooltip("有効にすると、この部屋にいる間だけカメラ追従の smoothTimeX / smoothTimeY を上書きします。")]
    [SerializeField] private bool overrideFollowSmoothing = false;

    [Tooltip("overrideFollowSmoothing が有効な時に使う X 軸追従スムーズ時間です。")]
    [SerializeField] private float smoothTimeX = 0.08f;

    [Tooltip("overrideFollowSmoothing が有効な時に使う Y 軸追従スムーズ時間です。")]
    [SerializeField] private float smoothTimeY = 0.12f;

    [Header("カメラサイズ上書き")]
    [Tooltip("有効にすると、この部屋にいる間だけ orthographicSize を上書きします。")]
    [SerializeField] private bool overrideOrthographicSize = false;

    [Tooltip("overrideOrthographicSize が有効な時に使う orthographicSize です。")]
    [SerializeField] private float orthographicSize = 7f;

    [Header("カメラサイズ補間時間上書き")]
    [Tooltip("有効にすると、この部屋にいる間だけ orthographicSize の補間時間を上書きします。")]
    [SerializeField] private bool overrideOrthographicSizeSmoothTime = false;

    [Tooltip("overrideOrthographicSizeSmoothTime が有効な時に使う補間時間です。")]
    [SerializeField] private float orthographicSizeSmoothTime = 0.10f;

    public string RoomId => roomId;
    public CameraBounds RoomBounds => roomBounds;
    public Vector2 RoomFocusOffset => roomFocusOffset;

    public Room LeftRoom => leftRoom;
    public Room RightRoom => rightRoom;
    public Room UpRoom => upRoom;
    public Room DownRoom => downRoom;

    public Transform RespawnFromLeft => respawnFromLeft;
    public Transform RespawnFromRight => respawnFromRight;
    public Transform RespawnFromUp => respawnFromUp;
    public Transform RespawnFromDown => respawnFromDown;

    public bool HasFollowSmoothingOverride => overrideFollowSmoothing;
    public float SmoothTimeX => smoothTimeX;
    public float SmoothTimeY => smoothTimeY;

    public bool HasOrthographicSizeOverride => overrideOrthographicSize;
    public float OrthographicSize => orthographicSize;

    public bool HasOrthographicSizeSmoothTimeOverride => overrideOrthographicSizeSmoothTime;
    public float OrthographicSizeSmoothTime => orthographicSizeSmoothTime;


}