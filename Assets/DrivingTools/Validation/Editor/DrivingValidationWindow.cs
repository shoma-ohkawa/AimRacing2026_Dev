using UnityEditor;
using UnityEngine;

//モードと入力条件を選んで走行検証を開始するウィンドウ
public class DrivingValidationWindow : EditorWindow
{
    static readonly string[] k_ProfileLabels = { "Normal", "Sport", "Track" };
    //NormalとSportとTrackの各ATとMTを走行条件番号へ変換する対応表
    static readonly int[,] k_ProfileNumbers = { { 0, 1 }, { 4, 5 }, { 2, 3 } };

    //選択中の走行モードで初期表示はTrack
    int profileIndex = 2;
    bool isMT;
    bool earlyTurn;
    bool driftChallenge;
    bool previousTuning;
    bool leverTest;

    [MenuItem("Tools/検証/走行検証パネル")]
    public static void Open()
    {
        GetWindow<DrivingValidationWindow>("走行検証");
    }

    void OnEnable()
    {
        //再生中の入力状態を画面へ反映する
        EditorApplication.update += Repaint;
    }

    void OnDisable()
    {
        EditorApplication.update -= Repaint;
    }

    void OnGUI()
    {
        bool isTrack = profileIndex == 2;

        EditorGUILayout.LabelField("走行条件を指定した検証", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("通常の自動走行は Tools → 自動走行 → Track AT / Track MT を使用してください。", MessageType.Info);
        profileIndex = EditorGUILayout.Popup("Profile", profileIndex, k_ProfileLabels);
        isMT = EditorGUILayout.ToggleLeft("MT(未チェックはAT)", isMT);

        using (new EditorGUI.DisabledScope(!isTrack))
        {
            earlyTurn = EditorGUILayout.ToggleLeft("Early Turn(旋回目標を早めに見る)", isTrack && earlyTurn);
            driftChallenge = EditorGUILayout.ToggleLeft("High Entry(高めの進入速度で限界確認)", isTrack && driftChallenge);
            previousTuning = EditorGUILayout.ToggleLeft("Previous Tuning(今回変更分を旧設定相当に)", isTrack && previousTuning);
        }
        using (new EditorGUI.DisabledScope(!(isTrack && isMT)))
        {
            leverTest = EditorGUILayout.ToggleLeft("Lever Test(実クラッチの踏み込み・回転合わせ)", isTrack && isMT && leverTest);
        }

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Run(Splash/MenuからAT・MT選択まで自動進行し実走開始)"))
            {
                int profileNumber = k_ProfileNumbers[profileIndex, isMT ? 1 : 0];
                TrackDriveSession.RunFromWindow(profileNumber, isTrack && earlyTurn, isTrack && driftChallenge,
                    isTrack && previousTuning, isTrack && isMT && leverTest);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("個別操作", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("実走中の入力: " + (TrackDriveSession.IsActive ? "有効" : "無効"));
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Enable Course Input")) { TrackDriveSession.Start(); }
            if (GUILayout.Button("Disable Course Input")) { TrackDriveSession.Stop(); }
        }
        if (GUILayout.Button("Send Menu Keyboard Inputs Only(Menu操作のみ再現)")) { TrackDriveSession.SendMenuInputs(); }
        if (GUILayout.Button("Toggle Vehicle Scene View")) { TrackDriveSession.ToggleVehicleView(); }
    }
}
