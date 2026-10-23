using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Microsoft.VisualBasic;
using System;
public class Entry : MonoBehaviour
{
    public G29 g29;
    public bool Decision = false;      //アンロードが終了しているかのフラグ
    public Image MapStart;                           //自分の選択している場所
    public Image MapEnd;                           //自分の選択している場所
    public Image Entry_Image;
    public Vector3 MySelect;
    public Vector3 FirstPos;                           //1列1行目の場所
    public Vector3 LastPos;                            //20列20行目の場所
    private float SelectX = 0.0f;                     //横の移動感覚
    private float SelectY = 0.0f;                     //縦の移動感覚
    private Vector2 NowSelect;                         //現在選択している文字列の場所
    public TextMeshProUGUI Name;                　     //Unity側の変更を加えるテキスト
    const int NEM_Vertical = 3;                        //NameEntryMapの縦
    const int NEM_Beside = 20;                         //NameEntryMapの横
    //public int NEMD_Number = 0;                        //NameEntryMapDakutenの場所選択用
    uint OldCloss;
    float OldAccel;
    int OldBatu;
    int OldMaru;

    //名前保存用
    public string[] MyName = { "", "", "", "", "", "", "", "" };
    static private string Input_Name = "";
    public sbyte MyNameNumber = 0;

    //名前入力のmap
    public string[,] NameEntryMap = new string[NEM_Vertical, NEM_Beside]
    {
        {"Ａ","Ｂ","Ｃ","Ｄ","Ｅ","Ｆ","Ｇ","Ｈ","Ｉ","Ｊ","Ｋ","Ｌ","Ｍ","Ｎ","Ｏ","Ｐ","Ｑ","Ｒ","Ｓ","Ｔ" },
        {"Ｕ","Ｖ","Ｗ","Ｘ","Ｙ","Ｚ","１","２","３","４","５","６","７","８","９","０","＜","＞","＋","ー" },
        {"＊","÷","＝","；","：","／","＼","　","｜","・","＠","！","？","＆","★","（","）","＾","◇","▽" }
    };
        //{
        //    {"あ","い","う","え","お","か","き","く","け","こ","さ","し","す","せ","そ","た","ち","つ","て","と" },
        //    {"な","に","ぬ","ね","の","は","ひ","ふ","へ","ほ","ま","み","む","め","も","や","ゆ","よ","゛","゜" },
        //    {"ら","り","る","れ","ろ","わ","を","ん","  ","っ","ぁ","ぃ","ぅ","ぇ","ぉ","ゃ","ゅ","ょ","ー","" }
        //};
    //public string[] NEM_Dakuten =
    //    { "が","ぎ","ぐ","げ","ご","ざ","じ","ず","ぜ","ぞ","だ","ぢ","づ","で","ど",
    //    "ば","び","ぶ","べ","ぼ","ぱ","ぴ","ぷ","ぺ","ぽ" };

    // Start is called before the first frame update
    void Start()
    {
        g29 = GameObject.Find("G29").GetComponent<G29>();
        MySelect = GameObject.Find("MySelect").transform.position;
        FirstPos = MySelect;
        LastPos = MapEnd.transform.position;
        SelectX = (LastPos.x - FirstPos.x) / 19;
        SelectY = (FirstPos.y - LastPos.y) / 2;
        NowSelect = new Vector2(0, 0);
        MyName = new string[]{ "", "", "", "", "", "", "", "" };
        Input_Name = "";
        MyNameNumber = 0;
        // 松下追加---------------------------
        Name.transform.position += new Vector3(360.0f, 56.0f, 0.0f);
        Name.alignment = TMPro.TextAlignmentOptions.Left;
        // -----------------------------------
    }



    // Update is called once per frame
    void Update()
    {
        MapStart.transform.eulerAngles += new Vector3(0, 0, -6);
        InputCloss();
        InputPedal();
    }

    void InputCloss()
    {
        //入力キーの処理
        //選択している文字がなにか分かるようにする
        if (Input.GetKeyDown(KeyCode.RightArrow) || (g29.rec.rgdwPOV[0] == 9000 && OldCloss != 9000)) 
        {
            MySelect.x += SelectX;
            NowSelect.x++;
            SoundManager.Instance.PlaySE(SoundManager.SE_Type.CursorSE);
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || (g29.rec.rgdwPOV[0] == 27000 && OldCloss != 27000))
        {
            MySelect.x -= SelectX;
            NowSelect.x--;
            SoundManager.Instance.PlaySE(SoundManager.SE_Type.CursorSE);
        }
        else if (Input.GetKeyDown(KeyCode.UpArrow) || (g29.rec.rgdwPOV[0] == 0 && OldCloss != 0))
        {
            MySelect.y += SelectY;
            NowSelect.y--;
            SoundManager.Instance.PlaySE(SoundManager.SE_Type.CursorSE);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow) || (g29.rec.rgdwPOV[0] == 18000 && OldCloss != 18000))
        {
            MySelect.y -= SelectY;
            NowSelect.y++;
            SoundManager.Instance.PlaySE(SoundManager.SE_Type.CursorSE);
        }

        //20行目から右にいこうとしたら1列目に戻す
        if (NowSelect.x > NEM_Beside - 1)
        {
            NowSelect.x -= NEM_Beside;
            MySelect.x = FirstPos.x;
            SoundManager.Instance.PlaySE(SoundManager.SE_Type.CursorSE);
        }
        //1列目から左にいこうとしたら20行目に移動
        else if (NowSelect.x < 0)
        {
            NowSelect.x += NEM_Beside;
            MySelect.x = LastPos.x;
            SoundManager.Instance.PlaySE(SoundManager.SE_Type.CursorSE);
        }
        //1列目から上にいこうとしたら3列目に移動
        else if (NowSelect.y < 0)
        {
            NowSelect.y += NEM_Vertical;
            MySelect.y = LastPos.y;
            SoundManager.Instance.PlaySE(SoundManager.SE_Type.CursorSE);
        }
        //3列目から下にいこうとしたら1列目に戻す
        else if (NowSelect.y > NEM_Vertical - 1)
        {
            NowSelect.y -= NEM_Vertical;
            MySelect.y = FirstPos.y;
            SoundManager.Instance.PlaySE(SoundManager.SE_Type.CursorSE);
        }

        OldCloss = g29.rec.rgdwPOV[0];
        GameObject.Find("MySelect").transform.position = new Vector3(MySelect.x, MySelect.y, MySelect.z);
    }
    void InputPedal()
    {
        //MyNameNumberが0以上の間
        //「←」を選んだら文字を消す処理
        //if (Input.GetKeyDown(KeyCode.Backspace) || (g29.rec.rgbButtons[0] == 128 && OldBatu != 128))
        //{
        //    if (--MyNameNumber < 0) MyNameNumber = 0;
        //    MyName[MyNameNumber] = "";
        //    Input_Name = Name.text.Remove(MyNameNumber, 1);
        //    Name.text = Input_Name;
        //    SoundManager.Instance.PlaySE(SoundManager.SE_Type.CursorSE);
        //}

        //2023/0831/坂本郁樹修正
        //「←」を選んだら文字を消す処理
        if (Input.GetKeyDown(KeyCode.Backspace) || (g29.rec.rgbButtons[0] == 128 && OldBatu != 128))
        {
            if (--MyNameNumber < 0)
            {
                MyNameNumber = 0;
                return;
            }
            MyName[MyNameNumber] = "";
            Input_Name = Name.text.Remove(MyNameNumber, 1);
            //テキストに代入
            Name.text = Input_Name;
            SoundManager.Instance.PlaySE(SoundManager.SE_Type.CursorSE);
        }

        //文字を選択した際の処理
        //else if (Input.GetKeyDown(KeyCode.Space) || (g29.rec.rgbButtons[2] == 128 && OldMaru != 128))
        //{
        //    //何の文字か判別し代入
        //    MyName[MyNameNumber] = NameEntryMap[(int)NowSelect.y, (int)NowSelect.x];
        //    //if(NowSelect.y)

        //    //文字の入力が7文字以上になったら新しい文字を消す
        //    if (MyNameNumber >= 5)
        //    {
        //        MyName[MyNameNumber] = "";
        //    }
        //    //表示する文字列に追加
        //    Input_Name += MyName[MyNameNumber];
        //    SoundManager.Instance.PlaySE(SoundManager.SE_Type.CursorSE);

        //    //テキストに代入
        //    Name.text = Input_Name;
        //    //現在の文字が入っていたら次の言葉が入るようにする。
        //    if (MyName[MyNameNumber] != "")
        //    {
        //        MyNameNumber++;
        //    }
        //}

        //2023/0831/坂本郁樹修正
        //文字を選択した際の処理
        else if (Input.GetKeyDown(KeyCode.Space) || (g29.rec.rgbButtons[2] == 128 && OldMaru != 128))
        {
            //文字の入力が5より小さい場合は
            //何の文字か判別し代入
            if (MyNameNumber < 5)
            {
                MyName[MyNameNumber] = NameEntryMap[(int)NowSelect.y, (int)NowSelect.x];

                //表示する文字列に追加
                Input_Name += MyName[MyNameNumber];
                SoundManager.Instance.PlaySE(SoundManager.SE_Type.CursorSE);

                //テキストに代入
                Name.text = Input_Name;

                //現在の文字が入っていたら次の言葉が入るようにする。
                MyNameNumber++;
            }
        }

        if (MyName[0]!="")
        {
            //名前の入力が完了した時の処理
            if (Input.GetKeyDown(KeyCode.Return) || (((g29.rec.lY / (float)-Int16.MaxValue + 1.0f) * 0.5f) >= 0.6f) && OldAccel < 0.6f)
            {
                Decision = true;
                SoundManager.Instance.PlaySE(SoundManager.SE_Type.EntryPushSE);
            }
        }
        
        OldAccel = (g29.rec.lY / (float)-Int16.MaxValue + 1.0f) * 0.5f;
        OldBatu = g29.rec.rgbButtons[0];
        OldMaru = g29.rec.rgbButtons[2];
    }
    static public string GetPlayerName()
    {
        return Input_Name;
    }
}
