/*------------------------------------------------------------------
* ファイル名：SoundManager.cs
* 概　　　要：サウンドの管理を行う
* 担　当　者：20CU0213　小林大輝
* 作　成　日：2022/08/29
-------------------------------------------------------------------*/
//更新履歴
/*
* 2022/08/29　小林大輝　サウンド管理用の処理を追加
* 2022/09/01  小林大輝　フェード処理を追加
* 2022/09/28  小林大輝  使用するクリップを保持しどこからでもアクセス可能に
* 2022/10/05　小林大輝　SEのフェードを追加
* 2026/09/13　Claude Code　CrossFadeChangeBGM()がm_playingBGMLen(GetBGMLen()の中身)を更新していなかった
*             バグを修正。クロスフェード経由でBGMが切り替わった場合にGetBGMLen()が古い曲の長さ(または
*             ResetFadeState()直後は0)を返し続け、OpeningState.Tick()の曲間インタールード判定
*             (m_director.time >= GetBGMLen() + m_loopInterval)が異常に早く成立してしまう不具合が
*             あったため(あわせてBGMPitch未設定の抜けも修正)。
*             
* 2026/09/14  青田　デバッグ用に順番に音源を再生する機能を追加。
*                   String配列などを使いSEとVOICEのMixer切り替えを行っていたが、各BGMのMixerを分けるのに伴いAudioClipAdvにAudioMixerGroupを追加して
*                   音源ごとにMixer切り替え可能に変更。
*/

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using DG.Tweening;
using Unity.Mathematics;
using Unity.VisualScripting;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine.UIElements;

//AudioClip以外にも選択可能な音源
//SudioSourceだとシーン上に存在しないといけないため、これを使い値を設定する。
[System.Serializable]
public struct AudioClipAdv
{
    [SerializeField] public AudioClip clip;
    [Range(0, 1)] public float volume;
    [Range(0, 3)] public float pitch;

    [SerializeField] public AudioMixerGroup audioMixer;
    [Range(0, 100)] public int rate;
}

public class SoundManager : SingletonMonoBehaviour<SoundManager>
{
    //BGM用enum
    public enum BGM_Type
    {
        //BGM用
        OP,             //タイトル時のOP1
        Entry,          //名前入力、ATMT選択中
        InGame,         //インゲーム中
        Result          //リザルト
    }

    //SE用enum
    public enum SE_Type
    {
        //SE用
        OPPushSE,       //OP時入力用のSE
        GlitchSE,       //グリッチ用SE     
        Welcome,        //AIMの世界へようこそ
        GuideName,      //名前入力をしてください
        GuideATMT,      //トランスミッションを選択してください
        CursorSE,       //カーソルを動かしたときのSE
        PushGasPedal,   //アクセルペダルを踏んでください
        EntryPushSE,    //エントリー時にアクセルを踏み込んだ時のSE
        DoonSE,         //文字やロゴを表示する際のSE
        AT,             //オートマを選択
        MT,             //マニュアルを選択
        Three,          //3
        Two,            //2
        One,            //1
        GO,             //Go
        CountSignal,    //インゲーム開始時のカウント用SE
        GOSignal,       //インゲーム開始時のスタート用SE
        Goal,           //ゴール
        LapSignal,      //区間タイム表示用SE
        Easy_L,         //緩めの左カーブ
        Turn_L,         //左カーブ
        Acute_L,        //急な左カーブ
        Easy_R,         //緩めの右カーブ
        Turn_R,         //右カーブ
        Acute_R,        //急な右カーブ
        YankieGodHorn,  //デバッグ用クラクション(0キー、Menu/Racing中のみ)
    }

    //3DSE用enum
    public enum SE3D_Type
    {
        //3DSE用
        Audience        //観客のSE
    }

    //フェード
    private int IsFadeBGMType = 0;              //フェード用フラグ（0:none,1:in,2:out,3:cross）
    private int IsFadeSEType = 0;               //フェード用フラグ（0:none,1:in,2:out）
    private int IsFadeSE3DType = 0;             //フェード用フラグ（0:none,1:in,2:out）
    public float FADE_IN_TIME = 1.0f;           //フェードインのみ
    public float FADE_OUT_TIME = 1.0f;          //フェードアウトのみ
    public const float CROSS_FADE_TIME = 1.0f;  //クロスフェード時

    //ボリューム関連
    [SerializeField, Range(0, 1)] public float BGM_Volume = 0.5f;
    [Range(0, 1)] public float SE_Volume = 1f;
    [Range(0, 1)] public float SE3D_Volume = 1f;

    public bool Mute = false;

    //音データ管理用
    [EnumIndex(typeof(BGM_Type))] private AudioClipAdv[] BGMs = new AudioClipAdv[Enum.GetValues(typeof(BGM_Type)).Length];
    //各BGMの変更リスト
    [SerializeField] private AudioClipAdv[] SwitchOPList;
    [SerializeField] private AudioClipAdv[] SwitchMenuList;
    [SerializeField] private AudioClipAdv[] SwitchInGameList;
    [SerializeField] private AudioClipAdv[] SwitchResultList;
    [SerializeField, EnumIndex(typeof(SE_Type))] private AudioClipAdv[] SEs;
    [SerializeField, EnumIndex(typeof(SE3D_Type))] private AudioClipAdv[] SE3Ds;

    [SerializeField] private AudioClipAdv[] DebugBGMs;
    [SerializeField] private int m_debugBGMCount = 0;

    //AudioSource
    const int BGMSourceNum = 2;
    const int SESourceNum = 5;
    const int SE3DSourceNum = 3;
    private AudioSource[] BGMSource = new AudioSource[BGMSourceNum];       //BGM再生用のAudioSource
    private AudioSource[] SESource = new AudioSource[SESourceNum];        //SE再生用のAudioSource(複数個用意)
    private AudioSource[] SE3DSource = new AudioSource[SE3DSourceNum];      //3DSE再生用のAudioSource
    //AudioSource毎の音量を保存する
    //これにより丸ごと上書きされなくなる
    private float[] BGMVol = new float[BGMSourceNum];
    private float[] BGMPitch = new float[BGMSourceNum];
    private float[] SEVol = new float[SESourceNum];
    private float[] SEPitch = new float[SESourceNum];
    private float[] SE3DVol = new float[SE3DSourceNum];
    private float[] SE3DPitch = new float[SE3DSourceNum];

    private int currentBGMIndex = 999;  //現在のBGMIndex

    private IEnumerator BGMEvents;
    private IEnumerator SEEvents;
    private IEnumerator SE3DEvents;

    private float m_playingBGMLen;  //再生中のBGMの長さ。BGM切り替えタイミングに使用する。

    private static bool nowSound;

    [SerializeField] private bool m_bDebug = false;

    //開始時の処理
    protected override void Awake()
    {
        //基クラスのAwakeを呼び出し
        base.Awake();

        //オブジェクトに使用しているか確認する
        GameObject soundManager = CheckOtherSoundManager();
        bool checkResult = soundManager != null && soundManager != gameObject;
        if (checkResult) Destroy(gameObject);
        DontDestroyOnLoad(gameObject);

        //使用する分のAudioSourceを作成
        for (int i = 0; i < BGMSource.Length; ++i)
        {
            BGMSource[i] = gameObject.AddComponent<AudioSource>();
        }

        for (int i = 0; i < SESource.Length; ++i)
        {
            SESource[i] = gameObject.AddComponent<AudioSource>();
        }

        for (int i = 0; i < SE3DSource.Length; ++i)
        {
            SE3DSource[i] = gameObject.AddComponent<AudioSource>();
            SE3DSource[i].spatialBlend = 1;     //3Dサウンドに変更
        }

        //切り替え再生リストの先頭をセット
        BGMs[(int)BGM_Type.OP] = SwitchOPList[0];
        BGMs[(int)BGM_Type.Entry] = SwitchMenuList[0];
        BGMs[(int)BGM_Type.InGame] = SwitchInGameList[0];
        BGMs[(int)BGM_Type.Result] = SwitchResultList[0];
    }

    //毎フレーム更新
    // Update is called once per frame
    void Update()
    {
        if(m_bDebug)
        {
            if(DebugKey.GetKeyDown(KeyCode.K))
            {
                StopBGM();
                PlayDebugBGM(m_debugBGMCount);
            }

            if(DebugKey.GetKeyDown(KeyCode.L))
            {
                ++m_debugBGMCount;
                if(m_debugBGMCount >= DebugBGMs.Length)
                {
                    m_debugBGMCount = 0;
                }
            }
            if(DebugKey.GetKeyDown(KeyCode.J))
            {
                --m_debugBGMCount;
                if (m_debugBGMCount < 0)
                {
                    m_debugBGMCount = DebugBGMs.Length - 1;
                }
            }
        }


        //ボリューム設定
        if (IsFadeBGMType == 0)
        {
            BGMSource[0].volume = BGM_Volume * BGMVol[0];
            BGMSource[0].pitch = BGMPitch[0];
            BGMSource[1].volume = BGM_Volume * BGMVol[1];
            BGMSource[1].pitch = BGMPitch[1];
        }

        int index = 0;
        foreach (AudioSource source in SESource)
        {
            if (IsFadeSEType == 0)
            {
                source.volume = SE_Volume * SEVol[index];
                source.pitch = SEPitch[index];
            }
            ++index;
        }

        index = 0;
        foreach (AudioSource source in SE3DSource)
        {
            if (IsFadeSE3DType == 0)
            {
                source.volume = SE3D_Volume * SE3DVol[index];
                source.pitch = SE3DPitch[index];
            }
            ++index;
        }

        if (BGMSource[0].clip == null)
        {
            //Debug.Log("BGMがありません");
        }

        nowSound = (IsFadeBGMType == 0);
    }

    //BGMの処理
    /// <summary>
    /// BGM再生
    /// </summary>
    /// <returns></returns>
    /// <param name="bgmType"></param>
    /// <param name="loopFlg"></param>
    /// <param name="Vol"></param>
    public void PlayBGM(BGM_Type bgmType, bool loopFlg = true)
    {
        //BGM�Ȃ����
        if ((int)bgmType == 999)
        {
            StopBGM();
            return;
        }

        IsFadeBGMType = 0;          //フェードはなし
        int index = (int)bgmType;   //列挙型を整数型に
        currentBGMIndex = index;    //指定されたインデックスを現在のインデックスに適用

        //インデックスが0より下、または設定されたクリップ以上の場合処理しない
        if (index < 0 || BGMs.Length <= index) return;

        //BGMが同じときは何もしない
        if (BGMSource[0].clip != null && BGMSource[0].clip == BGMs[index].clip) return;
        else if (BGMSource[1].clip != null && BGMSource[1].clip == BGMs[index].clip) return;

        //フェードでBGM開始しない
        if (BGMSource[0].clip == null && BGMSource[1].clip == null)
        {
            BGMSource[0].loop = loopFlg;
            BGMSource[0].clip = BGMs[index].clip;
            m_playingBGMLen = BGMs[index].clip.length * ((BGMs[index].pitch > 0.0f) ? 1.0f / BGMs[index].pitch : 0.0f);  //再生したBGMの長さを保存
            BGMVol[0] = BGMs[index].volume;     //音量設定
            BGMSource[0].outputAudioMixerGroup = BGMs[index].audioMixer;    //ミキサー設定
            BGMPitch[0] = BGMs[index].pitch;    //ピッチ設定
            BGMSource[0].Play();
            AppLog.Log("[Sound] BGM再生: " + bgmType + " / " + BGMs[index].clip.name); //どのクリップが選ばれたか確認用
        }
        else
        {
            //クロスフェード処理
            CrossFadeChangeBGM(index, loopFlg);
        }
    }

    /// <summary>
    /// 現在再生中(またはクロスフェード中)のBGMクリップ名を取得します。何も再生していなければnullです。
    /// </summary>
    public string GetCurrentBGMClipName()
    {
        if (BGMSource[0].clip != null) { return BGMSource[0].clip.name; }
        if (BGMSource[1].clip != null) { return BGMSource[1].clip.name; }
        return null;
    }

    public void PlayDebugBGM(int bgmIndex, bool loopFlg = true)
    {
        IsFadeBGMType = 0;          //フェードはなし
        currentBGMIndex = bgmIndex;    //指定されたインデックスを現在のインデックスに適用

        //インデックスが0より下、または設定されたクリップ以上の場合処理しない
        if (bgmIndex < 0 || DebugBGMs.Length <= bgmIndex) return;

        //BGMが同じときは何もしない
        if (BGMSource[0].clip != null && BGMSource[0].clip == DebugBGMs[bgmIndex].clip) return;
        else if (BGMSource[1].clip != null && BGMSource[1].clip == DebugBGMs[bgmIndex].clip) return;

        //フェードでBGM開始しない
        if (BGMSource[0].clip == null && BGMSource[1].clip == null)
        {
            BGMSource[0].loop = loopFlg;
            BGMSource[0].clip = DebugBGMs[bgmIndex].clip;
            m_playingBGMLen = DebugBGMs[bgmIndex].clip.length * ((DebugBGMs[bgmIndex].pitch > 0.0f) ? 1.0f / DebugBGMs[bgmIndex].pitch : 0.0f);  //再生したBGMの長さを保存
            BGMVol[0] = DebugBGMs[bgmIndex].volume;     //音量設定
            BGMSource[0].outputAudioMixerGroup = DebugBGMs[bgmIndex].audioMixer;    //ミキサー設定
            BGMPitch[0] = DebugBGMs[bgmIndex].pitch;    //ピッチ設定
            BGMSource[0].Play();
        }
        else
        {
            //クロスフェード処理
            CrossFadeChangeBGM(bgmIndex, loopFlg);
        }
    }

    /// <summary>
    /// BGMのフェードイン処理
    /// </summary>
    /// <returns></returns>
    /// <param name="bgmType">AudioClipの番号</param>
    /// <param name="fadeTime">フェードにかかる時間</param>
    /// <param name="Vol">音量</param>
    /// <param name="loopFlg">ループ設定。ループしない場合だけfalse指定</param>

    public void FadeInBGM(BGM_Type bgmType, float fadeTime = 1, bool loopFlg = true)
    {
        if (IsFadeBGMType != 1)
        {
            IsFadeBGMType = 1;
            int index = (int)bgmType;
            currentBGMIndex = index;
            FADE_IN_TIME = fadeTime;

            if (BGMSource[0].clip == null && BGMSource[1].clip == null)
            {
                BGMSource[0].volume = 0;                                            //音量を0に
                BGMSource[0].clip = BGMs[index].clip;                               //クリップを設定
                BGMSource[0].loop = loopFlg;                                        //ループを設定
                BGMVol[0] = BGMs[index].volume;                                     //音量設定
                BGMPitch[0] = BGMs[index].pitch;                                    //ピッチ設定
                BGMSource[0].Play();                                                //再生
                BGMSource[0].DOFade(endValue: BGM_Volume, duration: FADE_IN_TIME);  //フェードを掛ける

                //フェード時間まで処理を待つ 
                BGMEvents = DelayProcess(FADE_IN_TIME, () =>
                {
                    BGMEvents = null;
                    IsFadeBGMType = 0;                                              //フェードタイプを0に
                    return;
                });
                StartCoroutine(BGMEvents);
            }
        }
    }

    /// <summary>
    /// BGMのフェードアウト処理
    /// </summary>
    /// <returns></returns>
    /// <param name="fadeTime">フェードにかかる時間</param>
    /// <param name="vol">フェード後の音量（基本0）</param>
    /// <returns></returns>
    public void FadeOutBGM(float fadeTime, float vol = 0.0f)
    {
        if (IsFadeBGMType != 2)
        {
            IsFadeBGMType = 2;
            FADE_OUT_TIME = fadeTime;

            //分けられていた処理を統合するためにIndex化
            int BGMIndex = (BGMSource[0].clip != null) ? 0 : ((BGMSource[1].clip != null) ? 1 : -1);

            if (BGMIndex != -1)
            {
                BGMSource[BGMIndex].DOFade(vol, FADE_OUT_TIME).SetEase(Ease.Linear);         //フェード

                BGMEvents = DelayProcess(FADE_OUT_TIME, () =>                       //フェード時間まで処理を待つ
                {
                    BGMSource[BGMIndex].Stop();                                            //停止
                    BGMSource[BGMIndex].clip = null;                                       //clipをnullに
                    BGMEvents = null;
                    IsFadeBGMType = 0;                                              //フェードのフラグを切る
                    return;
                });
                StartCoroutine(BGMEvents);
            }
        }
    }

    /// <summary>
    /// BGMのクロスフェード処理
    /// </summary>
    /// <returns></returns>
    /// <param name="index">AudioClipの番号</param>
    /// <param name="loopFlg">ループ設定。ループしない場合だけfalse指定</param>
    /// <returns></returns>
    private void CrossFadeChangeBGM(int index, bool loopFlg)
    {
        IsFadeBGMType = 3;     //クロスフェードを適用

        if (BGMSource[0].clip != null)
        {
            //[0]が再生されている場合、[0]の音量を徐々に下げて、[1]を新しい曲として再生
            BGMSource[1].volume = 0;                                        //[1]の音量を0に
            BGMSource[1].clip = BGMs[index].clip;                           //[1]にclipを設定
            BGMSource[1].loop = loopFlg;                                    //[1]のloopを設定
            m_playingBGMLen = BGMs[index].clip.length * ((BGMs[index].pitch > 0.0f) ? 1.0f / BGMs[index].pitch : 0.0f);  //再生したBGMの長さを保存
            BGMVol[1] = BGMs[index].volume;                                 //音量設定
            BGMPitch[1] = BGMs[index].pitch;                                //ピッチ設定
            BGMSource[1].Play();                                            //[1]を再生
            AppLog.Log("[Sound] BGMクロスフェード再生: " + BGMs[index].clip.name); //どのクリップが選ばれたか確認用
            BGMSource[0].DOFade(0, CROSS_FADE_TIME).SetEase(Ease.Linear);   //[0]をフェードさせる

            BGMEvents = DelayProcess(CROSS_FADE_TIME, () =>                 //フェード時間まで処理を待つ
            {
                BGMSource[0].Stop();                                        //[0]を停止
                BGMSource[0].clip = null;                                   //[0]のclipをnullに
                BGMEvents = null;
                IsFadeBGMType = 0;                                          //フェードのフラグを切る
                return;
            });
            StartCoroutine(BGMEvents);
        }
        else
        {
            //[1]が再生されている場合、[1]の音量を徐々に下げて、[0]を新しい曲として再生
            BGMSource[0].volume = 0;                                        //[0]の音量を0に
            BGMSource[0].clip = BGMs[index].clip;                           //[0]にclipを設定
            BGMSource[0].loop = loopFlg;                                    //[0]のloopを設定
            m_playingBGMLen = BGMs[index].clip.length * ((BGMs[index].pitch > 0.0f) ? 1.0f / BGMs[index].pitch : 0.0f);  //再生したBGMの長さを保存
            BGMVol[0] = BGMs[index].volume;                                 //音量設定
            BGMPitch[0] = BGMs[index].pitch;                                //ピッチ設定
            BGMSource[0].Play();                                            //[0]を再生
            AppLog.Log("[Sound] BGMクロスフェード再生: " + BGMs[index].clip.name); //どのクリップが選ばれたか確認用
            BGMSource[1].DOFade(0, CROSS_FADE_TIME).SetEase(Ease.Linear);   //[1]をフェードさせる

            BGMEvents = DelayProcess(CROSS_FADE_TIME, () =>                 //フェード時間まで処理を待つ
            {
                BGMSource[1].Stop();                                        //[1]を停止
                BGMSource[1].clip = null;                                   //[1]のclipをnullに
                BGMEvents = null;
                IsFadeBGMType = 0;                                          //フェードのフラグを切る
                return;
            });
            StartCoroutine(BGMEvents);
        }
    }

    /// <summary>
    /// いずれかのBGM用AudioSourceが再生中かどうか。
    /// PlayAndSwitchBGMを同じ種別へ二重に呼ぶと、内部のSwitchBGMが「次回用に登録された曲」を
    /// 別の曲へ切り替えてしまい、2回目の呼び出しが同一クリップ判定に失敗して無関係な曲への
    /// クロスフェードを起こすことがある。既に何か再生中なら呼び出し元は再生開始を省略すること。
    /// </summary>
    public bool IsBGMPlaying()
    {
        return BGMSource[0].isPlaying || BGMSource[1].isPlaying;
    }

    /// <summary>
    /// BGM完全停止
    /// </summary>
    public void StopBGM()
    {
        if (BGMSource[0].clip != null)
        {
            BGMSource[0].Stop();
            BGMSource[0].clip = null;
        }
        if (BGMSource[1].clip != null)
        {
            BGMSource[1].Stop();
            BGMSource[1].clip = null;
        }
        if (BGMEvents != null) StopCoroutine(BGMEvents);
        return;
    }

    /// <summary>
    /// BGM一時停止
    /// </summary>
    public void MuteBGM()
    {
        BGMSource[0].Stop();
        BGMSource[1].Stop();
    }

    /// <summary>
    /// 一時停止したBGMを再生（再開）
    /// </summary>
    public void ResumeBGM()
    {
        BGMSource[0].Play();
        BGMSource[1].Play();
    }

    //SEの処理
    /// <summary>
    /// SE再生
    /// </summary>
    /// <param name="seType"></param>
    public void PlaySE(SE_Type seType, float Vol = 1)
    {
        int index = (int)seType;
        SE_Volume = Vol;
        if (index < 0 || SEs.Length <= index) return;

        int sourceIndex = 0;    //foreach内の回数
        //再生中ではないAudioSourceを使ってSEを鳴らす。
        foreach (AudioSource source in SESource)
        {
            if (false == source.isPlaying)
            {
                source.clip = SEs[index].clip;             //音源設定
                SEVol[sourceIndex] = SEs[index].volume;    //音量設定
                source.outputAudioMixerGroup = SEs[index].audioMixer;   //ミキサー設定
                SEPitch[sourceIndex] = SEs[index].pitch;   //ピッチ設定
                source.Play();
                return;
            }
            ++sourceIndex;
        }
    }

    /// <summary>
    /// SEフェードイン処理
    /// </summary>
    /// <param name="seType"></param>
    /// <param name="Vol"></param>
    /// <param name="loopFlg"></param>
    public void FadeInSE(SE_Type seType, float fadeTime, bool loopFlg = true)
    {
        if (IsFadeSEType != 1)
        {
            IsFadeSEType = 1;
            int index = (int)seType;
            FADE_IN_TIME = fadeTime;
            if (index < 0 || SEs.Length <= index) return;

            int sourceIndex = 0;
            //再生中ではないAudioSourceを使ってSEを鳴らす。
            foreach (AudioSource source in SESource)
            {
                if (false == source.isPlaying)
                {
                    source.volume = 0;                                            //音量を0に
                    source.clip = SEs[index].clip;                                //クリップを設定
                    source.loop = loopFlg;                                        //ループを設定
                    SEVol[sourceIndex] = SEs[index].volume;                       //音量設定
                    SEPitch[sourceIndex] = SEs[index].pitch;                      //ピッチ設定
                    source.Play();                                                //再生
                    source.DOFade(SE_Volume, FADE_IN_TIME).SetEase(Ease.Linear);  //フェードを掛ける

                    SEEvents = DelayProcess(FADE_IN_TIME, () =>                   //フェード時間まで処理を待つ
                    {
                        SEEvents = null;
                        IsFadeSEType = 0;
                    });
                    StartCoroutine(SEEvents);
                }
                ++sourceIndex;
            }
        }
    }

    /// <summary>
    /// SEフェードアウト処理
    /// </summary>
    /// <param name="fadeTime"></param>
    public void FadeOutSE(float fadeTime)
    {
        if (IsFadeSEType != 2)
        {
            IsFadeSEType = 2;

            foreach (AudioSource source in SESource)
            {
                if (true == source.isPlaying && true == source.loop)
                {
                    source.DOFade(0, FADE_OUT_TIME).SetEase(Ease.Linear);       //SEをフェード

                    SEEvents = DelayProcess(FADE_OUT_TIME, () =>                //フェード時間まで処理を待つ
                    {
                        source.Stop();                                          //SEを停止
                        source.clip = null;                                     //SEのclipをnullに
                        source.loop = false;                                    //SEのloopをfalseに
                        SEEvents = null;
                        IsFadeSEType = 0;                                       //フェードのフラグを切る
                    });
                    StartCoroutine(SEEvents);
                }
            }
        }
    }

    /// <summary>
    /// SE停止
    /// </summary>
    public void StopSE()
    {
        //すべてのSE用AudioSourceを停止
        foreach (AudioSource source in SESource)
        {
            source.Stop();
            source.clip = null;
            StopCoroutine(SEEvents);
        }
    }

    /// <summary>
    /// 3DSE再生
    /// </summary>
    /// <param name="se3DType"></param>
    /// <param name="Vol"></param>
    /// <param name="loopFlg"></param>
    public void Play3DSE(SE3D_Type se3DType, bool loopFlg = true)
    {
        int index = (int)se3DType;
        if (index < 0 || SE3Ds.Length <= index) return;

        int sourceIndex = 0;
        //再生中ではないAudioSourceを使ってSEを鳴らす
        foreach (AudioSource source in SE3DSource)
        {
            if (false == source.isPlaying)
            {
                source.clip = SE3Ds[index].clip;
                SE3DVol[sourceIndex] = SE3Ds[index].volume;     //音量設定
                SE3DPitch[sourceIndex] = SE3Ds[index].pitch;    //ピッチ設定
                source.Play();
                return;
            }
            ++sourceIndex;
        }
    }

    /// <summary>
    /// 3DSEフェードイン
    /// </summary>
    /// <param name="se3DType"></param>
    /// <param name="fadeTime"></param>
    /// <param name="Vol"></param>
    /// <param name="loopFlg"></param>
    public void FadeIn3DSE(SE3D_Type se3DType, float fadeTime, bool loopFlg = true)
    {
        if (IsFadeSE3DType != 1)
        {
            IsFadeSE3DType = 1;
            int index = (int)se3DType;
            FADE_IN_TIME = fadeTime;
            if (index < 0 || SE3Ds.Length <= index) return;

            int sourceIndex = 0;
            //再生中ではないAudioSourceを使って3DSEを鳴らす
            foreach (AudioSource source in SE3DSource)
            {
                if (false == source.isPlaying)
                {
                    source.volume = 0;
                    source.clip = SE3Ds[index].clip;
                    source.loop = loopFlg;
                    SE3DVol[sourceIndex] = SE3Ds[index].volume;     //音量設定
                    SE3DPitch[sourceIndex] = SE3Ds[index].pitch;    //ピッチ設定
                    source.Play();
                    source.DOFade(endValue: SE3D_Volume, duration: FADE_IN_TIME).SetEase(Ease.Linear);

                    SE3DEvents = DelayProcess(FADE_IN_TIME, () =>             //フェード時間まで処理を待つ
                    {
                        SE3DEvents = null;
                        IsFadeSE3DType = 0;
                    });
                    StartCoroutine(SE3DEvents);
                }

                ++sourceIndex;
            }
        }
    }

    /// <summary>
    /// SE3Dフェードアウト処理
    /// </summary>
    /// <param name="fadeTime"></param>
    public void FadeOut3DSE(float fadeTime)
    {
        if (IsFadeSE3DType != 2)
        {
            IsFadeSE3DType = 2;

            foreach (AudioSource source in SE3DSource)
            {
                if (true == source.isPlaying && true == source.loop)
                {
                    source.DOFade(0, FADE_OUT_TIME).SetEase(Ease.Linear);       //SEをフェード

                    SE3DEvents = DelayProcess(FADE_OUT_TIME, () =>              //フェード時間まで処理を待つ
                    {
                        source.Stop();                                          //SEを停止
                        source.clip = null;                                     //SEのclipをnullに
                        source.loop = false;                                    //SEのloopをfalseに
                        SE3DEvents = null;
                        IsFadeSEType = 0;                                       //フェードのフラグを切る
                    });
                    StartCoroutine(SE3DEvents);
                }
            }
        }
    }

    /// <summary>
    /// SE3D停止
    /// </summary>
    public void Stop3DSE()
    {
        //すべてのSE用AudioSourceを停止
        foreach (AudioSource source in SE3DSource)
        {
            source.Stop();
            source.clip = null;
            StopCoroutine(SE3DEvents);
        }
    }

    public void StopSoundCorutine()
    {
        StopCoroutine(BGMEvents);
        StopCoroutine(SEEvents);
        StopCoroutine(SE3DEvents);
    }

    /// <summary>
    /// フェード関連の状態をすべて初期状態へ戻します。Result→Splash等、セッションの節目でのキャッシュリフレッシュ用です。
    /// フェードが遷移の割り込み(EmergencyReset等)で中断され、フラグが立ったまま残るのを防ぎます。
    /// </summary>
    public void ResetFadeState()
    {
        if (BGMEvents != null) { StopCoroutine(BGMEvents); BGMEvents = null; }
        if (SEEvents != null) { StopCoroutine(SEEvents); SEEvents = null; }
        if (SE3DEvents != null) { StopCoroutine(SE3DEvents); SE3DEvents = null; }

        IsFadeBGMType = 0;
        IsFadeSEType = 0;
        IsFadeSE3DType = 0;

        currentBGMIndex = 999;
        m_playingBGMLen = 0f;
    }

    //オブジェクトがあるか確認用
    private GameObject CheckOtherSoundManager()
    {
        return GameObject.FindGameObjectWithTag("SoundManager");
    }

    public IEnumerator DelayProcess(float delaySecond, UnityAction callback)
    {
        yield return new WaitForSeconds(delaySecond);
        callback?.Invoke();
    }

    private void OnDestroy()
    {
        ////サウンドの停止
        //SoundManager.Instance.StopBGM();
        //SoundManager.Instance.StopSE();
        //SoundManager.Instance.Stop3DSE();
    }

    //2023/0921河合追記
    public static bool NowSound()
    {
        return nowSound;
    }

    //BGMの切り替え
    public void SwitchBGM(BGM_Type _bgmType)
    {
        //0.OP,1.Entry,2.InGame,3.Result
        //_bgmTypeによって使う配列を変える
        //配列は=でシャローコピーになるらしい
        AudioClipAdv[] TargetArray = ((int)_bgmType == 0) ? SwitchOPList : ((int)_bgmType == 1) ? SwitchMenuList : ((int)_bgmType == 2) ? SwitchInGameList : SwitchResultList;
        int BGMNum = TargetArray.Length;

        //確率リスト
        List<int> RateList = new List<int>(); 
        for(int i = 0;i< BGMNum; ++i)
        {
            RateList.Add(TargetArray[i].rate);
        }
        //ソートを行うと小さい順に並ぶ
        RateList.Sort();

        //1～100の値を生成
        //int型の場合は第二引数未満らしい
        int RandNum = UnityEngine.Random.Range(1, 101);

        //長くはあるけどどうせソートする処理を追加するならforの数はそう変わらないと思う
        for (int i = 0; i < BGMNum; ++i)
        {
            if(RandNum <= RateList[i])
            {
                for (int n = 0; n < BGMNum; ++n) 
                {
                    if(RateList[i] == TargetArray[n].rate)
                    {
                        BGMs[(int)_bgmType] = TargetArray[n];
                        break;
                    }
                }
                break;
            }
        }
    }

    //再生とともに次のBGMを用意変更
    public void PlayAndSwitchBGM(BGM_Type _bgmType, bool _loop = false)
    {
        PlayBGM(_bgmType, _loop);
        SwitchBGM(_bgmType);
    }

    //再生中BGMの長さを取得
    public float GetBGMLen()
    {
        return m_playingBGMLen;
    }
}
