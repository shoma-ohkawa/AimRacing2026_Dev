/*━━━━━━━━━
@file    RacingEnterEffect.cs
@brief   Racingに入った時のポストプロセス演出を再生する
@author  24CU0110 大川翔生
@date    作成日   : 2026/10/15
         最終更新日 : 2026/10/15
━━━━━━━━━*/

using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

//演出は「開始時が最大で、0へ収まっていく」形に統一する。パターンは各パラメーターの組み合わせで決まる
//ビネット・色収差・ブルーム・露出・彩度は、演出専用のグローバルVolumeを実行時に作り、その重みを1から0へ動かす(シーンのVolumeには手を入れない)
//ぼかしは、ポストプロセスの前に描画済みの画像全体をぼかすCustom Pass(RacingEnterBlurPass)を実行時に登録し、そのぼかし半径を動かす
//作ったVolumeとCustom Passは、演出の終了・Stop・破棄のどれでも取り除く(シーンのファイルは変更しない)
public class RacingEnterEffect : MonoBehaviour
{
    //---- 共通 ----
    private const string k_VolumeObjectName = "RacingEnterEffectVolume"; //演出専用Volumeの名前
    private const int k_VolumeLayer = 0; //演出専用Volumeのレイヤー(カメラのVolume Layer MaskはDefaultのみ)
    private const float k_VolumePriority = 1000f; //演出専用Volumeの優先度(シーンのVolumeは0と100)
    private const float k_DecayExponent = 1.5f; //収まり方の強さ。大きいほど最初に大きく動き、後半は緩やかに収まる(小さいほど効果が長く見える)

    //---- VignetteOpen: 暗くて色の薄い視界が、明るく鮮やかに開く ----
    private const float k_VignetteOpenDuration = 1.8f; //再生秒数
    private const float k_VignetteOpenIntensity = 0.9f; //開始時のビネットの強さ
    private const float k_VignetteOpenSmoothness = 0.75f; //ビネットの縁のなめらかさ(大きいほど暗がりが中央まで広がる)
    private const float k_VignetteOpenSaturation = -60f; //開始時の彩度の加算量(マイナスで色が薄くなる)
    private const float k_VignetteOpenExposure = -1f; //開始時の露出の加算量(EV。マイナスで暗くなる)
    private const float k_VignetteOpenHoldRatio = 0.25f; //暗がりを保つ時間の割合(0以上1未満)

    //---- ChromaticFlash: 白く飛んだ画面が、色ずれと光のにじみを残して収まる ----
    private const float k_FlashDuration = 1.2f; //再生秒数
    private const float k_FlashChromatic = 1f; //開始時の色収差の強さ
    private const float k_FlashBloom = 1f; //開始時のブルームの強さ
    private const float k_FlashBloomScatter = 1f; //ブルームの広がり(大きいほど光が広くにじむ)
    private const float k_FlashExposure = 3.5f; //開始時の露出の加算量(EV)
    private const float k_FlashHoldRatio = 0.1f; //最も明るい状態を保つ時間の割合(0以上1未満)

    //---- LaunchCombo: 中央が白く光り、周辺が暗い状態から、色ずれとともに収まる ----
    private const float k_ComboDuration = 1.6f; //再生秒数
    private const float k_ComboVignetteIntensity = 0.7f; //開始時のビネットの強さ
    private const float k_ComboVignetteSmoothness = 0.6f; //ビネットの縁のなめらかさ
    private const float k_ComboChromatic = 0.8f; //開始時の色収差の強さ
    private const float k_ComboBloom = 0.8f; //開始時のブルームの強さ
    private const float k_ComboBloomScatter = 0.9f; //ブルームの広がり
    private const float k_ComboExposure = 2f; //開始時の露出の加算量(EV)
    private const float k_ComboSaturation = 25f; //開始時の彩度の加算量(プラスで色が濃くなる)
    private const float k_ComboHoldRatio = 0.15f; //最も強い状態を保つ時間の割合(0以上1未満)

    //---- BlurFocus: 画面全体がぼけた状態から、ピントが合って鮮明になる ----
    private const float k_BlurDuration = 2.5f; //再生秒数
    private const float k_BlurMaxRadius = 32f; //開始時のぼかし半径(画面の半分の大きさのバッファでのpx。フル解像度では約2倍に見える)
    private const float k_BlurDecayExponent = 1f; //ぼかしの収まり方。1で一定の速さ(大きいと最初に大きく鮮明になる)
    private const float k_BlurHoldRatio = 0.1f; //最もぼけた状態を保つ時間の割合(0以上1未満)
    private const string k_BlurPassName = "RacingEnterBlur"; //ぼかしのCustom Passの名前(Profilerに表示される)
    private const float k_BlurPassPriority = 0f; //ぼかしのCustom Passの優先度(他のCustom Passは使っていない)
    private const CustomPassInjectionPoint k_BlurInjectionPoint = CustomPassInjectionPoint.BeforePostProcess; //ぼかしを入れる位置。AfterPostProcessはDLSSのアップスケール後で、バッファの大きさが食い違い、映る範囲がずれた

    //1つのパターンを組み立てるパラメーター。0のものは使わない
    private struct EffectParam
    {
        public float m_duration; //再生秒数
        public float m_decayExponent; //収まり方の強さ。大きいほど最初に大きく動き、後半は緩やかに収まる
        public float m_blurRadius; //開始時のぼかし半径。0のときはぼかさない
        public float m_vignetteIntensity; //開始時のビネットの強さ
        public float m_vignetteSmoothness; //ビネットの縁のなめらかさ
        public float m_chromatic; //開始時の色収差の強さ
        public float m_bloom; //開始時のブルームの強さ
        public float m_bloomScatter; //ブルームの広がり。0のときは上書きしない
        public float m_exposure; //開始時の露出の加算量(EV)
        public float m_saturation; //開始時の彩度の加算量
        public float m_holdRatio; //開始時の強さのまま保つ時間の割合(0以上1未満)
    }

    private Coroutine m_pCoroutine; //再生中の演出
    private Volume m_pVolume; //演出専用のVolume
    private VolumeProfile m_pProfile; //演出専用Volumeのプロファイル(実行時に作る)
    private RacingEnterBlurPass m_pBlurPass; //全体をぼかすCustom Pass。ぼかしのパターンのときだけ登録する

    //演出を最初から再生する。再生中なら止めて入れ替える
    //_pattern: 再生するパターン。Noneなら何も再生しない
    public void Play(RacingEnterEffectPattern _pattern)
    {
        Stop();
        if (_pattern == RacingEnterEffectPattern.None) { return; }

        m_pCoroutine = StartCoroutine(PlayRoutine(GetParam(_pattern)));
    }

    //演出を止め、作ったVolumeとCustom Passを取り除く
    public void Stop()
    {
        if (m_pCoroutine != null)
        {
            StopCoroutine(m_pCoroutine);
            m_pCoroutine = null;
        }
        Release();
    }

    private void OnDestroy()
    {
        Release();
    }

    //パターンに対応するパラメーターを返す
    //_pattern: パターン
    private static EffectParam GetParam(RacingEnterEffectPattern _pattern)
    {
        EffectParam param = new EffectParam();
        param.m_decayExponent = k_DecayExponent;

        switch (_pattern)
        {
            case RacingEnterEffectPattern.VignetteOpen:
                param.m_duration = k_VignetteOpenDuration;
                param.m_vignetteIntensity = k_VignetteOpenIntensity;
                param.m_vignetteSmoothness = k_VignetteOpenSmoothness;
                param.m_saturation = k_VignetteOpenSaturation;
                param.m_exposure = k_VignetteOpenExposure;
                param.m_holdRatio = k_VignetteOpenHoldRatio;
                break;

            case RacingEnterEffectPattern.ChromaticFlash:
                param.m_duration = k_FlashDuration;
                param.m_chromatic = k_FlashChromatic;
                param.m_bloom = k_FlashBloom;
                param.m_bloomScatter = k_FlashBloomScatter;
                param.m_exposure = k_FlashExposure;
                param.m_holdRatio = k_FlashHoldRatio;
                break;

            case RacingEnterEffectPattern.LaunchCombo:
                param.m_duration = k_ComboDuration;
                param.m_vignetteIntensity = k_ComboVignetteIntensity;
                param.m_vignetteSmoothness = k_ComboVignetteSmoothness;
                param.m_chromatic = k_ComboChromatic;
                param.m_bloom = k_ComboBloom;
                param.m_bloomScatter = k_ComboBloomScatter;
                param.m_exposure = k_ComboExposure;
                param.m_saturation = k_ComboSaturation;
                param.m_holdRatio = k_ComboHoldRatio;
                break;

            case RacingEnterEffectPattern.BlurFocus:
                param.m_duration = k_BlurDuration;
                param.m_decayExponent = k_BlurDecayExponent;
                param.m_blurRadius = k_BlurMaxRadius;
                param.m_holdRatio = k_BlurHoldRatio;
                break;
        }

        return param;
    }

    //Volumeで動かす項目が1つでもあるか
    //_param: 調べるパラメーター
    private static bool HasVolumeEffect(EffectParam _param)
    {
        return _param.m_vignetteIntensity > 0f
            || _param.m_chromatic > 0f
            || _param.m_bloom > 0f
            || !Mathf.Approximately(_param.m_exposure, 0f)
            || !Mathf.Approximately(_param.m_saturation, 0f);
    }

    //準備→毎フレームの更新→後片付けを行う。実時間で進めるので、ポーズ(timeScale 0)でも止まらない
    //_param: 再生するパターンのパラメーター
    private IEnumerator PlayRoutine(EffectParam _param)
    {
        bool bHasVolume = HasVolumeEffect(_param); //Volumeで動かす項目があるか
        bool bHasBlur = _param.m_blurRadius > 0f; //全体のぼかしがあるか
        if (!bHasVolume && !bHasBlur) { m_pCoroutine = null; yield break; }

        if (bHasVolume) { CreateVolume(_param); }
        if (bHasBlur) { RegisterBlurPass(); }

        float elapsed = 0f; //演出の経過秒数
        while (elapsed < _param.m_duration)
        {
            Apply(_param, elapsed);
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        Release();
        m_pCoroutine = null;
    }

    //演出専用のグローバルVolumeを作る。必要な項目だけを上書きし、重みで強さを動かす
    //_param: 上書きする値を持つパラメーター
    private void CreateVolume(EffectParam _param)
    {
        GameObject volumeObject = new GameObject(k_VolumeObjectName);
        volumeObject.layer = k_VolumeLayer;
        volumeObject.transform.SetParent(transform, false);

        m_pProfile = ScriptableObject.CreateInstance<VolumeProfile>();

        if (_param.m_vignetteIntensity > 0f)
        {
            Vignette vignette = m_pProfile.Add<Vignette>();
            vignette.intensity.Override(_param.m_vignetteIntensity);
            vignette.smoothness.Override(_param.m_vignetteSmoothness);
        }
        if (_param.m_chromatic > 0f)
        {
            ChromaticAberration chromatic = m_pProfile.Add<ChromaticAberration>();
            chromatic.intensity.Override(_param.m_chromatic);
        }
        if (_param.m_bloom > 0f)
        {
            Bloom bloom = m_pProfile.Add<Bloom>();
            bloom.intensity.Override(_param.m_bloom);
            if (_param.m_bloomScatter > 0f) { bloom.scatter.Override(_param.m_bloomScatter); }
        }
        if (!Mathf.Approximately(_param.m_exposure, 0f) || !Mathf.Approximately(_param.m_saturation, 0f))
        {
            ColorAdjustments colorAdjustments = m_pProfile.Add<ColorAdjustments>();
            if (!Mathf.Approximately(_param.m_exposure, 0f)) { colorAdjustments.postExposure.Override(_param.m_exposure); }
            if (!Mathf.Approximately(_param.m_saturation, 0f)) { colorAdjustments.saturation.Override(_param.m_saturation); }
        }

        m_pVolume = volumeObject.AddComponent<Volume>();
        m_pVolume.isGlobal = true;
        m_pVolume.priority = k_VolumePriority;
        m_pVolume.weight = 0f;
        m_pVolume.sharedProfile = m_pProfile;
    }

    //全体をぼかすCustom Passを、ポストプロセスの前に登録する。全てのGameカメラに掛かる
    private void RegisterBlurPass()
    {
        m_pBlurPass = new RacingEnterBlurPass();
        m_pBlurPass.name = k_BlurPassName;
        m_pBlurPass.targetDepthBuffer = CustomPass.TargetBuffer.None; //ぼかしは深度を使わない
        CustomPassVolume.RegisterGlobalCustomPass(k_BlurInjectionPoint, m_pBlurPass, k_BlurPassPriority);
    }

    //経過時間に合わせて、Volumeの重みとぼかし半径を更新する。保持の時間が過ぎてから、1から0へ収まる
    //_param: 再生中のパラメーター / _elapsed: 経過秒数
    private void Apply(EffectParam _param, float _elapsed)
    {
        float progress = Mathf.Clamp01(_elapsed / _param.m_duration); //進み具合 0〜1
        float holdProgress = Mathf.Clamp01((progress - _param.m_holdRatio) / (1f - _param.m_holdRatio)); //保持を除いた進み具合 0〜1
        float strength = Mathf.Pow(1f - holdProgress, _param.m_decayExponent); //強さ 1〜0

        if (m_pVolume != null) { m_pVolume.weight = strength; }
        if (m_pBlurPass != null) { m_pBlurPass.Radius = _param.m_blurRadius * strength; }
    }

    //作ったVolumeとCustom Passを取り除く
    private void Release()
    {
        if (m_pVolume != null)
        {
            m_pVolume.weight = 0f; //Destroyは現在のフレームの終わりまで遅れるため、先に重みを戻す
            Destroy(m_pVolume.gameObject);
            m_pVolume = null;
        }

        if (m_pProfile != null)
        {
            for (int i = m_pProfile.components.Count - 1; i >= 0; --i)
            {
                Destroy(m_pProfile.components[i]); //プロファイルを破棄しても、中のコンポーネントは残るため個別に破棄する
            }
            Destroy(m_pProfile);
            m_pProfile = null;
        }

        if (m_pBlurPass != null)
        {
            CustomPassVolume.UnregisterGlobalCustomPass(m_pBlurPass);
            m_pBlurPass.ReleaseBuffer();
            m_pBlurPass = null;
        }
    }
}
