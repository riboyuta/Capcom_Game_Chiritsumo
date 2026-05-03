using UnityEngine;
using System.Collections;

public class HitStopManager : MonoBehaviour
{
    public static HitStopManager Instance;

    void Awake()
    {
        Instance = this; 
    }


    public void DoHitStop(float duration) //引数で指定した秒間待つ。
    {
        StartCoroutine(HitStop(duration));
    }


    IEnumerator HitStop(float duration)
    {
        Time.timeScale = 0f; //ここの値によって速度を決めている。スローも可能である。
        yield return new WaitForSecondsRealtime(duration);　
        Time.timeScale = 1f;
    }
}