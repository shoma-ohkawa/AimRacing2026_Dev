/**
 * @file    IWheelFFBBackend.cs
 * @brief   ハンコンFFBの出力先(バックエンド)を差し替えるための共通インターフェース
 *
 * G923_FFBController は「何をどれくらいの強さで出すか」だけを計算し、
 * 「どのAPIで出すか」はこのインターフェースの実装に任せる。
 *
 *  ・LogitechFFBBackend    : Logitech Steering Wheel SDK (G923 / G29 など)
 *  ・DirectInputFFBBackend : DirectInput8 ネイティブプラグイン (Thrustmaster など他社製FFBホイール)
 *  ・XInputFFBBackend      : XInput (左右2モーターの振動のみ。Spring/Damperは仕様上出せない)
 *
 * 値のスケールは既存コードに合わせて Logitech SDK と同じ「パーセント」で統一する。
 *  Spring offset      : -100 ～ 100
 *  Spring saturation  :    0 ～ 100
 *  Spring coefficient :    0 ～ 100
 *  Damper             :    0 ～ 100
 *  Constant force     : -100 ～ 100
 *  Road effect        :    0 ～ 100
 */

using System;
using UnityEngine;

public enum EWheelFFBBackend
{
    Auto,
    LogitechSDK,
    DirectInput,
    XInput,
}

public enum EWheelRoadEffect
{
    BumpyRoad,
    DirtRoad,
    Surface,
}

/// <summary>
/// バックエンドごとの出力補正。メーカーによって同じ数値でも体感が違うので、ここで個別に合わせる。
/// </summary>
[Serializable]
public class WheelFFBBackendTuning
{
    [Range(0.0f, 2.0f), Tooltip("Spring(センタリング)の倍率")]
    public float springScale = 1.0f;

    [Range(0.0f, 2.0f), Tooltip("Damper(重さ)の倍率")]
    public float damperScale = 1.0f;

    [Range(0.0f, 2.0f), Tooltip("ConstantForce(左右揺れ・衝突)の倍率")]
    public float constantScale = 1.0f;

    [Range(0.0f, 2.0f), Tooltip("路面凹凸エフェクトの倍率")]
    public float roadEffectScale = 1.0f;

    [Tooltip("ConstantForceの左右を反転する。メーカーで符号の向きが逆の場合に使う")]
    public bool invertConstantForce = false;

    public static WheelFFBBackendTuning CreateXInputDefault()
    {
        // XInputは振動モーターのみなので、少し強めに出さないと体感しにくい
        return new WheelFFBBackendTuning
        {
            springScale = 0.0f,
            damperScale = 0.0f,
            constantScale = 1.5f,
            roadEffectScale = 1.5f,
            invertConstantForce = false,
        };
    }

    public int ScaleSigned(int value, float scale)
    {
        return Mathf.Clamp(Mathf.RoundToInt(value * scale), -100, 100);
    }

    public int ScaleUnsigned(int value, float scale)
    {
        return Mathf.Clamp(Mathf.RoundToInt(value * scale), 0, 100);
    }
}

public interface IWheelFFBBackend
{
    EWheelFFBBackend Type { get; }
    string Name { get; }

    /// <summary>接続中デバイスの名前(ログ用)</summary>
    string DeviceName { get; }

    /// <summary>Spring / Damper(ハンドルの重さ・センタリング)を出せるか</summary>
    bool SupportsConditionEffects { get; }

    /// <summary>ゲーム側から回転角を設定できるか</summary>
    bool SupportsOperatingRange { get; }

    bool IsConnected { get; }

    /// <summary>初期化して接続を試みる。接続できたらtrue</summary>
    bool Initialize();

    /// <summary>毎フレーム呼ぶ。falseならAPI自体が使えない状態(=切断扱い)</summary>
    bool Update();

    /// <summary>USB再接続後などの再初期化</summary>
    bool Reinitialize();

    void Shutdown();

    /// <summary>回転角(左右合計)を設定する。非対応バックエンドは何もせずtrueを返す</summary>
    bool ApplyOperatingRange(int degrees);

    void PlaySpring(int offset, int saturation, int coefficient);
    void StopSpring();

    void PlayDamper(int coefficient);
    void StopDamper();

    void PlayConstant(int force);
    void StopConstant();

    /// <param name="surfaceWaveType">Surfaceのときだけ使う。0:Sine 1:Square 2:Triangle</param>
    void PlayRoadEffect(EWheelRoadEffect effect, int magnitude, int surfaceWaveType, int periodMs);
    void StopRoadEffects();

    void StopAll();
}
