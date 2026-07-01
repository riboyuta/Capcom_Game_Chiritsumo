using UnityEngine;

public sealed class GameEffectLoopTestInput : MonoBehaviour
{
    [Header("エフェクト")]
    [SerializeField] private GameEffectPlayer effectPlayer;
    [SerializeField] private GameEffectKey key = GameEffectKey.PlayerWallSlideDust;
    [SerializeField] private Transform origin;

    private bool isPlaying;

    private void Reset()
    {
        effectPlayer = GetComponent<GameEffectPlayer>();
        origin = transform;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            var target = origin != null ? origin : transform;

            if (isPlaying)
            {
                effectPlayer.Stop(key, target);
                isPlaying = false;
            }
            else
            {
                effectPlayer.SetActive(key, target, true);
                isPlaying = true;
            }
        }
    }
}