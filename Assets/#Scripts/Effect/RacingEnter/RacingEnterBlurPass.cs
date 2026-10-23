/*━━━━━━━━━
@file    RacingEnterBlurPass.cs
@brief   ポストプロセスの前に、描画済みの画像の全体をガウスぼかしするCustom Pass
@author  24CU0110 大川翔生
@date    作成日   : 2026/10/15
         最終更新日 : 2026/10/15
━━━━━━━━━*/

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

//Depth of Fieldは深度を基準にぼかすため、空・雲・透明物などが残る。このパスは描画済みの画像そのものをぼかすので、画面全体に均一にかかる
//ポストプロセスの後(AfterPostProcess)に入れると、DLSSのアップスケール後でバッファの大きさが食い違い、映る範囲がずれたため、前に入れている
//RacingEnterEffectが全体のCustom Passとして登録・解除する。ぼかし半径は毎フレーム外から渡す
public class RacingEnterBlurPass : CustomPass
{
    private const float k_HalfResolutionScale = 0.5f; //ぼかしの作業用バッファの大きさ(画面に対する割合)。GaussianBlurが対応するのは0.5か1.0
    private const int k_SampleCount = 25; //ぼかしのサンプル数。半径が大きいので多めにして、ゴースト状のすじを抑える(奇数)
    private const float k_MinRadius = 0.5f; //これより小さい半径では、ぼかし自体を行わない

    private RTHandle m_pHalfResBuffer; //ぼかしの作業用バッファ(画面の半分の大きさ)
    private float m_radius; //ぼかし半径(作業用バッファのpx)

    public float Radius { set { m_radius = value; } }

    //Sceneビューには掛けない(Gameビューのカメラだけ)
    protected override bool executeInSceneView => false;

    //作業用バッファを作る。最初に実行される時に1回だけ呼ばれる
    //_renderContext: 描画コンテキスト / _cmd: コマンドバッファ
    protected override void Setup(ScriptableRenderContext _renderContext, CommandBuffer _cmd)
    {
        m_pHalfResBuffer = RTHandles.Alloc(
            Vector2.one * k_HalfResolutionScale,
            TextureXR.slices,
            dimension: TextureXR.dimension,
            colorFormat: GraphicsFormat.R16G16B16A16_SFloat, //空のなだらかな色が縞にならないよう、精度の高い形式にする
            useDynamicScale: true,
            name: "RacingEnterBlurHalfRes");
    }

    //カメラの色バッファをその場でぼかす
    //_ctx: Custom Passの実行情報
    protected override void Execute(CustomPassContext _ctx)
    {
        if (m_radius < k_MinRadius) { return; }
        if (_ctx.hdCamera.camera.cameraType != CameraType.Game) { return; } //プレビュー用などのカメラには掛けない
        if (m_pHalfResBuffer == null) { return; }

        CustomPassUtils.GaussianBlur(_ctx, _ctx.cameraColorBuffer, _ctx.cameraColorBuffer, m_pHalfResBuffer, k_SampleCount, m_radius);
    }

    //作業用バッファを解放する
    protected override void Cleanup()
    {
        ReleaseBuffer();
    }

    //登録を解除した後に呼ぶ。Cleanupは全体のCustom Passでは呼ばれないことがあるため、自分で解放する
    public void ReleaseBuffer()
    {
        if (m_pHalfResBuffer == null) { return; }

        m_pHalfResBuffer.Release();
        m_pHalfResBuffer = null;
    }
}
