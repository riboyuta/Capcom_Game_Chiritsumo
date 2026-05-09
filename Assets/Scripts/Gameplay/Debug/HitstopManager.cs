using UnityEngine;
using System.Collections;

public class HitStopManager : MonoBehaviour
{

    [Header("デバッグ用：ヒットストップの時間")]
    [Tooltip("ヒットストップで止める時間を設定できます。スクリプト側で個別に引数の設定もできるよ")]
    [SerializeField] private float stopTimeInspecter = 0.8f;

    [Header("デバッグ用：ヒットストップのスロー値")]
    [Tooltip("ヒットストップでどれだけゆっくりになるかの設定。０で停止。スクリプト側で個別に引数の設定もできるよ")]
    [SerializeField] private float stopStrengthInspecter = 0.0f;


    public static HitStopManager Instance;

    Coroutine current;




    //===========================-------
    //　　　　　 基本的な動き
    //===========================-------

    void Awake()
    {
        Instance = this;
    }


    public void DoHitStop(float duration, float strength)
    {
        if (current != null)
        {
            StopCoroutine(current);
        }

        current = StartCoroutine(HitStop(duration, strength));
    }


    IEnumerator HitStop(float duration, float strength)
    {
        Time.timeScale = strength;
        Time.fixedDeltaTime = 0.02f * Time.timeScale; //0.02はデフォルト値

        yield return new WaitForSecondsRealtime(duration);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f; 

        current = null;
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    // デバッグ用
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            DoHitStop();
        }
                
    }

    //===========================-------
    //　　　　インスペクターラップ
    //===========================-------

    public void DoHitStop()
    {
        DoHitStop(stopTimeInspecter, stopStrengthInspecter);
    }

}