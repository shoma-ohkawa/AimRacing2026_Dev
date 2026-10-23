using UnityEngine;
using UnityEngine.UI;

//メーターのLED画像を点灯と消灯と一定周期の点滅で表示するクラス
[RequireComponent(typeof(Image))]
public class LED_Controller : MonoBehaviour
{
    //透明度を変えてLEDを表示するための画像参照
    private Image m_LED;

    //既存シーンの色設定を保つためのLED色の種類
    private enum LED_Color
    {
        Red,
        Blue,
        Green,
        Yellow,
    }

    //既存のLED画像と対応するInspector上の色設定
    [SerializeField] private LED_Color m_LED_Color;

    //毎フレームの乱数を使わず一定の速さで点滅させる周波数
    [SerializeField, Range(0.5f, 3f)] private float m_blinkFrequencyHz = 2f;

    //短い変速待ちでも最初に点灯させるための点滅開始時刻
    private float m_blinkStartedAt;

    //連続した点滅呼び出しで開始時刻を繰り返し戻さないための状態
    private bool m_isBlinking;

    //LED画像を取得して初期表示を消灯する関数
    public void Init()
    {
        m_LED = GetComponent<Image>();
        LightOut();
    }

    //点滅を終えてLEDを点灯し続ける関数
    public void Lit()
    {
        m_isBlinking = false;
        SetAlpha(1f);
    }

    //回転数が点灯条件を下回ったLEDを消す関数
    public void LightOut()
    {
        m_isBlinking = false;
        SetAlpha(0f);
    }

    //初回は必ず点灯し一定周期でLEDの点灯と消灯を繰り返す関数
    public void Blink()
    {
        //変速直前の短い時間でも赤色警告を見逃しにくくする
        if (!m_isBlinking)
        {
            m_blinkStartedAt = Time.unscaledTime;
            m_isBlinking = true;
        }

        //ゲームの時間倍率に左右されず点滅の半周期ごとに表示を切り替える
        float elapsed = Time.unscaledTime - m_blinkStartedAt;
        float phase = Mathf.Repeat(elapsed * Mathf.Clamp(m_blinkFrequencyHz, 0.5f, 3f), 1f);
        SetAlpha(phase < 0.5f ? 1f : 0f);
    }

    //画像の色を保ったままLED表示に必要な透明度だけを変更する関数
    private void SetAlpha(float alpha)
    {
        if (m_LED == null) { m_LED = GetComponent<Image>(); }
        if (m_LED == null) { return; }

        //担当者が設定した画像の色を変更しないための作業用カラー
        Color color = m_LED.color;
        color.a = Mathf.Clamp01(alpha);
        m_LED.color = color;
    }
}
