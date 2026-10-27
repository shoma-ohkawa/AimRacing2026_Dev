/*━━━━━━━━━
@file    CurveSignAdjustWindow.cs
@brief   Mapシーンのカーブ標識(CurveSignPoints)のInPoint/OutPointを、道路(RoadSpline)に沿って前後にずらすエディタ拡張
@author  24CU0110 大川翔生
@date    作成日   : 2026/10/27
         最終更新日 : 2026/10/27
━━━━━━━━━*/

using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// カーブ標識の「表示タイミング」を調整するウィンドウです。メニュー Tools/カーブ標識/位置調整 から開きます。
/// InPoint(標識を出すトリガー)とOutPoint(標識を消すトリガー)を、コースの進行方向に沿って前後にずらします。
/// 進行方向は、シーンにある道路(RoadSpline)の接線と、InPoint→OutPointの並びから決めます。
/// RoadSplineが無い、または道路から離れているときは、InPoint→OutPointの向きと、トリガーの面の法線(forward)から推定します。
/// </summary>
public class CurveSignAdjustWindow : EditorWindow
{
    //どのポイントをずらすか
    private enum TargetKind
    {
        Both,       //InPointとOutPointの両方
        InOnly,     //InPoint(標識を出す位置)だけ
        OutOnly,    //OutPoint(標識を消す位置)だけ
    }

    //カーブ1つ分(CurveSignPoint_NNと、その中のInPoint・OutPoint)
    private class CurveEntry
    {
        public Transform m_pContainer;      //CurveSignPoint_NN
        public Transform m_pIn;             //標識を出すトリガー(SignTurnLeft / SignTurnRight)
        public Transform m_pOut;            //標識を消すトリガー(SignGoal など)
        public int m_intensity;             //カーブの強さ 1:緩 2:中 3:急
        public bool m_bTurnLeft;            //左カーブか(false なら右カーブ)
        public bool m_bDirectionValid;      //コースの進行方向を決められたか
        public bool m_bRoad;                //進行方向を道路(RoadSpline)から決めたか(false ならトリガーの面の法線から推定)
        public int m_splineIndex;           //道路の何番目のスプラインに沿うか(m_bRoad のとき)
        public float m_roadDir;             //スプラインのパラメータが増える向きが進行方向なら1、逆なら-1(m_bRoad のとき)
        public float m_inSign;              //InPointのforwardが進行方向なら1、逆なら-1(m_bRoad が false のとき)
        public float m_outSign;             //OutPointのforwardが進行方向なら1、逆なら-1(m_bRoad が false のとき)
        public Vector3 m_inStartPos;        //一覧を作った時点のInPointのワールド位置(累計移動量の基準)
        public Vector3 m_outStartPos;       //一覧を作った時点のOutPointのワールド位置
        public Quaternion m_inStartRot;     //一覧を作った時点のInPointのワールド回転
        public Quaternion m_outStartRot;    //一覧を作った時点のOutPointのワールド回転
    }

    private const string k_MenuPath = "Tools/カーブ標識/位置調整";
    private const string k_WindowTitle = "カーブ標識の位置調整";
    private const string k_UndoName = "カーブ標識の位置調整";
    private const string k_RootName = "CurveSignPoints";                //Mapシーンにある、CurveSign.prefabのインスタンス名
    private const string k_RoadName = "RoadSpline";                     //Mapシーンにある、道路のSplineContainerの名前(TrackDriveSessionと同じ)
    private const string k_TagTurnLeft = "SignTurnLeft";                //InPoint(左カーブ)のタグ
    private const string k_TagTurnRight = "SignTurnRight";              //InPoint(右カーブ)のタグ
    private const string k_IntensityProperty = "curveIntensity";        //CurveSign.csの、カーブの強さのフィールド名
    private const int k_PointsPerCurve = 2;                             //1つのカーブに含まれるCurveSignの数(InPointとOutPoint)
    private const int k_NearestResolution = 16;                         //道路への最近点を探すときの、曲線1区間あたりの分割数
    private const int k_NearestIterations = 4;                          //道路への最近点を探すときの、絞り込みの回数
    private const float k_MaxRoadDistanceMeters = 40.0f;                //道路の中心線から、これ以内にあるポイントだけを道路に沿わせる(m)
    private const float k_MinParamDelta = 0.0001f;                      //InPointとOutPointのスプライン上の位置の差がこれ未満なら、並びを決められない
    private const float k_HalfLoop = 0.5f;                              //閉じたスプラインのパラメータの半周
    private const float k_FullLoop = 1.0f;                              //閉じたスプラインのパラメータの一周
    private const float k_MinTangentSqr = 0.000001f;                    //これ未満の長さの接線は、向きとして使わない
    private const float k_DefaultStepMeters = 1.0f;                     //ずらす量の初期値(m)
    private const float k_MinStepMeters = 0.01f;                        //ずらす量の下限(m)
    private const float k_MaxStepMeters = 100.0f;                       //ずらす量の上限(m)
    private const float k_MinSeparationMeters = 0.1f;                   //(面の法線から推定するとき)InPointとOutPointが、法線方向にこれ以上離れていれば、向きを決められる
    private const float k_FramePadding = 20.0f;                         //シーンビューで一覧から選んだときに、カーブの周りへ足す余白(m)
    private const float k_ArrowSizeScale = 1.2f;                        //シーンビューの矢印ハンドルの大きさ(ハンドルの標準サイズへの倍率)
    private const float k_ArrowSnapMeters = 0.1f;                       //シーンビューで矢印をドラッグするときの刻み(m)
    private const float k_DefaultPlaneWidth = 20.0f;                    //BoxColliderが無いときに描くトリガー面の幅(m)
    private const float k_DefaultPlaneHeight = 10.0f;                   //BoxColliderが無いときに描くトリガー面の高さ(m)
    private const float k_ColumnNameWidth = 150.0f;                     //一覧の「名前」列の幅
    private const float k_ColumnShortWidth = 70.0f;                     //一覧の短い列の幅
    private const float k_ColumnOffsetWidth = 90.0f;                    //一覧の累計移動量の列の幅
    private const float k_PresetButtonWidth = 44.0f;                    //ずらす量のプリセットボタンの幅
    private const int k_IntensityWeak = 1;                              //緩やかなカーブ(青い標識)
    private const int k_IntensityMiddle = 2;                            //中くらいのカーブ(黄色い標識)
    private const int k_IntensityStrong = 3;                            //急なカーブ(赤い標識)

    private static readonly float[] k_StepPresets = { 0.5f, 1.0f, 5.0f, 10.0f };   //ずらす量のプリセット(m)
    private static readonly Color k_ColorWeak = new Color(0.25f, 0.55f, 1.0f);     //緩のカーブの色(青)
    private static readonly Color k_ColorMiddle = new Color(1.0f, 0.85f, 0.2f);    //中のカーブの色(黄)
    private static readonly Color k_ColorStrong = new Color(1.0f, 0.3f, 0.25f);    //急のカーブの色(赤)
    private static readonly Color k_ColorUnknown = new Color(0.7f, 0.7f, 0.7f);    //強さが決められないカーブの色(灰)
    private static readonly Color k_ColorSelectedRow = new Color(0.4f, 0.65f, 1.0f, 0.6f);   //一覧の、選択中の行の背景色

    private readonly List<CurveEntry> m_curves = new List<CurveEntry>();   //一覧のカーブ
    private SplineContainer m_pRoad;                                       //道路(RoadSpline)。見つからなければ null
    private int m_selectedIndex = -1;                                      //選択中のカーブ(-1 なら未選択)
    private int m_skippedCount;                                            //InPoint・OutPointの組が見つからず、一覧に入れなかったカーブの数
    private int m_estimatedCount;                                          //道路からではなく、面の法線から向きを推定したカーブの数
    private float m_stepMeters = k_DefaultStepMeters;                      //1回でずらす量(m)
    private TargetKind m_target = TargetKind.Both;                         //ずらす対象
    private bool m_bDrawInScene = true;                                    //シーンビューにカーブを描くか
    private Vector2 m_scroll;                                              //一覧のスクロール位置

    [MenuItem(k_MenuPath)]
    private static void Open()
    {
        CurveSignAdjustWindow window = GetWindow<CurveSignAdjustWindow>(k_WindowTitle);
        window.Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGui;
        Selection.selectionChanged += OnSelectionChanged;
        Undo.undoRedoPerformed += OnUndoRedo;
        RefreshCurves();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGui;
        Selection.selectionChanged -= OnSelectionChanged;
        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    //----------------------------------------------------------------
    // 一覧の作成
    //----------------------------------------------------------------

    //シーンにあるCurveSignから、カーブの一覧を作り直す。作り直した時点の位置が、累計移動量の基準になる
    private void RefreshCurves()
    {
        m_curves.Clear();
        m_selectedIndex = -1;
        m_skippedCount = 0;
        m_estimatedCount = 0;
        m_pRoad = FindRoad();

        CurveSign[] signs = FindObjectsByType<CurveSign>(FindObjectsInactive.Include);
        GameObject root = GameObject.Find(k_RootName); //見つからなければ、シーン全体のCurveSignを対象にする
        Dictionary<Transform, List<CurveSign>> groups = new Dictionary<Transform, List<CurveSign>>(); //コンテナごとのCurveSign
        for (int i = 0; i < signs.Length; ++i) //CurveSignを1つずつコンテナへ振り分けるループ
        {
            Transform container = signs[i].transform.parent;
            if (container == null) { continue; }
            if (root != null && !container.IsChildOf(root.transform)) { continue; }
            if (!groups.TryGetValue(container, out List<CurveSign> list))
            {
                list = new List<CurveSign>();
                groups.Add(container, list);
            }
            list.Add(signs[i]);
        }

        foreach (KeyValuePair<Transform, List<CurveSign>> pair in groups) //コンテナごとにカーブ1つ分を作るループ
        {
            CurveEntry entry = CreateEntry(pair.Key, pair.Value);
            if (entry == null)
            {
                ++m_skippedCount;
                continue;
            }
            if (!entry.m_bRoad) { ++m_estimatedCount; }
            m_curves.Add(entry);
        }

        m_curves.Sort((_a, _b) => string.CompareOrdinal(_a.m_pContainer.name, _b.m_pContainer.name));
        OnSelectionChanged();
    }

    //シーンから道路(RoadSpline)を探す。無ければ null
    private static SplineContainer FindRoad()
    {
        SplineContainer[] containers = FindObjectsByType<SplineContainer>(FindObjectsInactive.Include);
        for (int i = 0; i < containers.Length; ++i) //名前が一致する道路を探すループ
        {
            if (containers[i].name == k_RoadName) { return containers[i]; }
        }
        return null;
    }

    //コンテナの中のCurveSignから、カーブ1つ分を作る。InPoint・OutPointの組にならないときは null
    //引数 _container: CurveSignPoint_NN のTransform
    //引数 _signs    : そのコンテナの直下にあるCurveSign
    private CurveEntry CreateEntry(Transform _container, List<CurveSign> _signs)
    {
        if (_signs.Count != k_PointsPerCurve) { return null; }

        CurveEntry entry = new CurveEntry();
        entry.m_pContainer = _container;
        for (int i = 0; i < _signs.Count; ++i) //InPointとOutPointを見分けるループ
        {
            Transform point = _signs[i].transform;
            if (point.CompareTag(k_TagTurnLeft) || point.CompareTag(k_TagTurnRight))
            {
                entry.m_pIn = point;
                entry.m_bTurnLeft = point.CompareTag(k_TagTurnLeft);
                entry.m_intensity = ReadIntensity(_signs[i]);
            }
            else
            {
                entry.m_pOut = point;
            }
        }
        if (entry.m_pIn == null || entry.m_pOut == null) { return null; }

        entry.m_inStartPos = entry.m_pIn.position;
        entry.m_outStartPos = entry.m_pOut.position;
        entry.m_inStartRot = entry.m_pIn.rotation;
        entry.m_outStartRot = entry.m_pOut.rotation;

        if (!TryDecideDirectionByRoad(entry))
        {
            DecideDirectionByPlane(entry);
        }
        return entry;
    }

    //道路(RoadSpline)のスプライン上の位置から、進行方向を決める。決められたら true
    //InPointより後ろにOutPointがある向きを、進行方向とする(閉じたスプラインでは、近い側の差で見る)
    //引数 _entry: 向きを決めるカーブ
    private bool TryDecideDirectionByRoad(CurveEntry _entry)
    {
        if (m_pRoad == null) { return false; }

        IReadOnlyList<Spline> splines = m_pRoad.Splines;
        int bestIndex = -1;                         //InPointに最も近いスプライン
        float bestDistance = float.MaxValue;        //そのスプラインまでの距離
        float inT = 0.0f;                           //InPointの、そのスプライン上の位置(0〜1)
        for (int i = 0; i < splines.Count; ++i) //InPointに最も近いスプラインを探すループ
        {
            float distance = DistanceToSpline(splines[i], _entry.m_pIn.position, out float t);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
                inT = t;
            }
        }
        if (bestIndex < 0 || bestDistance > k_MaxRoadDistanceMeters) { return false; }

        Spline spline = splines[bestIndex];
        float outDistance = DistanceToSpline(spline, _entry.m_pOut.position, out float outT);
        if (outDistance > k_MaxRoadDistanceMeters) { return false; }

        float delta = outT - inT; //スプライン上での、InPointからOutPointまでの差
        if (spline.Closed)
        {
            if (delta > k_HalfLoop) { delta -= k_FullLoop; }
            else if (delta < -k_HalfLoop) { delta += k_FullLoop; }
        }
        if (Mathf.Abs(delta) < k_MinParamDelta) { return false; }

        _entry.m_bRoad = true;
        _entry.m_splineIndex = bestIndex;
        _entry.m_roadDir = Mathf.Sign(delta);
        _entry.m_bDirectionValid = true;
        return true;
    }

    //道路が使えないときの推定。トリガーの面の法線(forward)が、InPoint→OutPointの向きと同じか逆かで、進行方向を決める
    //曲がりの大きいカーブでは、InPoint→OutPointの直線と道路の向きがずれるので、推定が外れることがある
    //引数 _entry: 向きを決めるカーブ
    private static void DecideDirectionByPlane(CurveEntry _entry)
    {
        Vector3 inToOut = _entry.m_pOut.position - _entry.m_pIn.position; //InPointからOutPointへ向かうベクトル
        float inDot = Vector3.Dot(_entry.m_pIn.forward, inToOut);
        float outDot = Vector3.Dot(_entry.m_pOut.forward, inToOut);
        _entry.m_bRoad = false;
        _entry.m_inSign = Mathf.Sign(inDot);
        _entry.m_outSign = Mathf.Sign(outDot);
        _entry.m_bDirectionValid = Mathf.Abs(inDot) >= k_MinSeparationMeters && Mathf.Abs(outDot) >= k_MinSeparationMeters;
    }

    //スプラインまでの距離と、最も近い位置(0〜1)を求める
    //引数 _spline  : 調べるスプライン
    //引数 _position: 調べるワールド位置
    //引数 _t       : (出力)スプライン上の最も近い位置
    private float DistanceToSpline(Spline _spline, Vector3 _position, out float _t)
    {
        Transform roadTransform = m_pRoad.transform;
        SplineUtility.GetNearestPoint(_spline, roadTransform.InverseTransformPoint(_position), out float3 nearest, out _t, k_NearestResolution, k_NearestIterations);
        return Vector3.Distance(_position, roadTransform.TransformPoint(nearest));
    }

    //CurveSignのcurveIntensity(private)を読む。読めなければ 0
    //引数 _sign: 読む対象のCurveSign
    private static int ReadIntensity(CurveSign _sign)
    {
        SerializedObject serialized = new SerializedObject(_sign);
        SerializedProperty property = serialized.FindProperty(k_IntensityProperty);
        return property != null ? property.intValue : 0;
    }

    //----------------------------------------------------------------
    // 移動
    //----------------------------------------------------------------

    //ポイントの位置での、進行方向の単位ベクトル(道路から決めたときは道路の接線、そうでなければトリガーの面の法線)
    //引数 _entry: ポイントが属するカーブ
    //引数 _point: InPointまたはOutPoint
    private Vector3 GetTravelAxis(CurveEntry _entry, Transform _point)
    {
        if (_entry.m_bRoad)
        {
            Vector3 tangent = GetRoadTangent(_entry, _point.position);
            if (tangent.sqrMagnitude >= k_MinTangentSqr) { return tangent.normalized * _entry.m_roadDir; }
        }
        float sign = _point == _entry.m_pIn ? _entry.m_inSign : _entry.m_outSign;
        return _point.forward * (_entry.m_bRoad ? 1.0f : sign);
    }

    //ワールド位置に最も近い道路の位置での、接線(スプラインのパラメータが増える向き)
    //引数 _entry   : 道路のスプラインを決めているカーブ
    //引数 _position: 調べるワールド位置
    private Vector3 GetRoadTangent(CurveEntry _entry, Vector3 _position)
    {
        Spline spline = m_pRoad.Splines[_entry.m_splineIndex];
        DistanceToSpline(spline, _position, out float t);
        float3 tangent = spline.EvaluateTangent(t);
        return m_pRoad.transform.TransformDirection(tangent);
    }

    //ポイントを、進行方向に沿ってずらす。Undoに対応し、プレハブのインスタンスの上書きとして保存される
    //道路から向きを決めたカーブでは、道路に沿ってずらし、道路の中心線からの横のずれを保つ。面の向きも道路に合わせて回る
    //引数 _entry : ポイントが属するカーブ
    //引数 _point : ずらすポイント
    //引数 _meters: ずらす量(m)。プラスで進行方向(標識が遅くなる)、マイナスで逆方向(標識が早くなる)
    private void ShiftPoint(CurveEntry _entry, Transform _point, float _meters)
    {
        Undo.RecordObject(_point, k_UndoName);
        if (_entry.m_bRoad)
        {
            ShiftAlongRoad(_entry, _point, _meters);
        }
        else
        {
            _point.position += GetTravelAxis(_entry, _point) * _meters;
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(_point);
        EditorSceneManager.MarkSceneDirty(_point.gameObject.scene);
    }

    //道路の接線に沿って進んだあと、道路の中心線へ戻し、元の横のずれを保った位置に置く
    //引数 _entry : ポイントが属するカーブ
    //引数 _point : ずらすポイント
    //引数 _meters: ずらす量(m)
    private void ShiftAlongRoad(CurveEntry _entry, Transform _point, float _meters)
    {
        Transform roadTransform = m_pRoad.transform;
        Spline spline = m_pRoad.Splines[_entry.m_splineIndex];
        Vector3 position = _point.position;

        DistanceToSpline(spline, position, out float t0);
        Vector3 center0 = roadTransform.TransformPoint(spline.EvaluatePosition(t0));
        Vector3 tangent0 = GetTravelAxis(_entry, _point);
        Vector3 lateral = position - center0; //道路の中心線からの横(と上下)のずれ

        Vector3 moved = position + tangent0 * _meters; //まず接線の向きへまっすぐ進める
        DistanceToSpline(spline, moved, out float t1);
        Vector3 center1 = roadTransform.TransformPoint(spline.EvaluatePosition(t1));
        Vector3 tangent1 = GetRoadTangent(_entry, center1).normalized * _entry.m_roadDir;
        Quaternion turn = Quaternion.FromToRotation(tangent0, tangent1); //進んだ先で、道路の向きが変わった分の回転

        _point.position = center1 + turn * lateral;
        _point.rotation = turn * _point.rotation;
    }

    //カーブ1つ分を、対象の設定に従ってずらす
    //引数 _entry : ずらすカーブ
    //引数 _meters: ずらす量(m)。プラスで遅く、マイナスで早く
    private void ShiftEntry(CurveEntry _entry, float _meters)
    {
        if (!_entry.m_bDirectionValid) { return; }

        if (m_target != TargetKind.OutOnly)
        {
            ShiftPoint(_entry, _entry.m_pIn, _meters);
        }
        if (m_target != TargetKind.InOnly)
        {
            ShiftPoint(_entry, _entry.m_pOut, _meters);
        }
    }

    //選択中のカーブをずらす
    //引数 _meters: ずらす量(m)
    private void ShiftSelected(float _meters)
    {
        if (m_selectedIndex < 0 || m_selectedIndex >= m_curves.Count) { return; }

        Undo.IncrementCurrentGroup();
        ShiftEntry(m_curves[m_selectedIndex], _meters);
        Undo.SetCurrentGroupName(k_UndoName);
        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
    }

    //すべてのカーブをずらす(確認ダイアログつき)
    //引数 _meters: ずらす量(m)
    private void ShiftAll(float _meters)
    {
        string message = m_curves.Count + " 個のカーブすべてを " + _meters.ToString("+0.##;-0.##") + " m ずらします。よろしいですか?";
        if (!EditorUtility.DisplayDialog(k_WindowTitle, message, "ずらす", "やめる")) { return; }

        Undo.IncrementCurrentGroup();
        for (int i = 0; i < m_curves.Count; ++i) //カーブを1つずつずらすループ
        {
            ShiftEntry(m_curves[i], _meters);
        }
        Undo.SetCurrentGroupName(k_UndoName);
        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
    }

    //カーブ1つ分を、一覧を作った時点の位置と向きへ戻す
    //引数 _entry: 戻すカーブ
    private static void ResetEntry(CurveEntry _entry)
    {
        Undo.RecordObject(_entry.m_pIn, k_UndoName);
        Undo.RecordObject(_entry.m_pOut, k_UndoName);
        _entry.m_pIn.SetPositionAndRotation(_entry.m_inStartPos, _entry.m_inStartRot);
        _entry.m_pOut.SetPositionAndRotation(_entry.m_outStartPos, _entry.m_outStartRot);
        PrefabUtility.RecordPrefabInstancePropertyModifications(_entry.m_pIn);
        PrefabUtility.RecordPrefabInstancePropertyModifications(_entry.m_pOut);
        EditorSceneManager.MarkSceneDirty(_entry.m_pIn.gameObject.scene);
    }

    //----------------------------------------------------------------
    // ウィンドウ
    //----------------------------------------------------------------

    private void OnGUI()
    {
        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("再生中は調整できません。停止してから使ってください。", MessageType.Info);
            return;
        }

        DrawToolbar();
        DrawList();
        DrawButtons();
    }

    //上部の設定(一覧の更新、ずらす量、対象、シーンビューの表示)
    private void DrawToolbar()
    {
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("一覧を作り直す(現在の位置を基準にする)"))
            {
                RefreshCurves();
            }
            m_bDrawInScene = GUILayout.Toggle(m_bDrawInScene, "シーンビューに表示", GUILayout.Width(k_ColumnNameWidth));
        }

        if (m_curves.Count == 0)
        {
            EditorGUILayout.HelpBox("カーブが見つかりません。Map シーンを開いて、「一覧を作り直す」を押してください。", MessageType.Warning);
        }
        else
        {
            if (m_pRoad == null)
            {
                EditorGUILayout.HelpBox("道路(" + k_RoadName + ")が見つかりません。進行方向は、トリガーの面の向きから推定します。外れることがあるので、シーンビューの矢印で確認してください。", MessageType.Warning);
            }
            else if (m_estimatedCount > 0)
            {
                EditorGUILayout.HelpBox("道路から向きを決められず、トリガーの面の向きから推定したカーブが " + m_estimatedCount + " 個あります(一覧の「向き」が「推定」)。矢印で確認してください。", MessageType.Warning);
            }
            if (m_skippedCount > 0)
            {
                EditorGUILayout.HelpBox("InPoint と OutPoint の組が見つからず、一覧に入れなかったカーブが " + m_skippedCount + " 個あります。", MessageType.Warning);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            m_stepMeters = Mathf.Clamp(EditorGUILayout.FloatField("ずらす量 (m)", m_stepMeters), k_MinStepMeters, k_MaxStepMeters);
            for (int i = 0; i < k_StepPresets.Length; ++i) //プリセットのボタンを並べるループ
            {
                if (GUILayout.Button(k_StepPresets[i].ToString("0.##"), GUILayout.Width(k_PresetButtonWidth)))
                {
                    m_stepMeters = k_StepPresets[i];
                    GUI.FocusControl(null);
                }
            }
        }
        m_target = (TargetKind)EditorGUILayout.EnumPopup("対象", m_target);
    }

    //カーブの一覧。行を押すと、そのカーブを選んでシーンビューで映す
    private void DrawList()
    {
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("カーブ", EditorStyles.miniBoldLabel, GUILayout.Width(k_ColumnNameWidth));
            GUILayout.Label("向き", EditorStyles.miniBoldLabel, GUILayout.Width(k_ColumnShortWidth));
            GUILayout.Label("左右・強さ", EditorStyles.miniBoldLabel, GUILayout.Width(k_ColumnShortWidth));
            GUILayout.Label("In→Out(m)", EditorStyles.miniBoldLabel, GUILayout.Width(k_ColumnShortWidth));
            GUILayout.Label("In 移動量(m)", EditorStyles.miniBoldLabel, GUILayout.Width(k_ColumnOffsetWidth));
            GUILayout.Label("Out 移動量(m)", EditorStyles.miniBoldLabel, GUILayout.Width(k_ColumnOffsetWidth));
        }

        m_scroll = EditorGUILayout.BeginScrollView(m_scroll);
        for (int i = 0; i < m_curves.Count; ++i) //カーブを1行ずつ描くループ
        {
            CurveEntry entry = m_curves[i];
            Color previous = GUI.backgroundColor;
            if (i == m_selectedIndex)
            {
                GUI.backgroundColor = k_ColorSelectedRow;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(entry.m_pContainer.name, EditorStyles.miniButtonLeft, GUILayout.Width(k_ColumnNameWidth)))
                {
                    SelectCurve(i);
                }

                GUILayout.Label(DirectionLabel(entry), GUILayout.Width(k_ColumnShortWidth));
                GUILayout.Label((entry.m_bTurnLeft ? "左" : "右") + " " + IntensityLabel(entry.m_intensity), GUILayout.Width(k_ColumnShortWidth));
                GUILayout.Label(Vector3.Distance(entry.m_pIn.position, entry.m_pOut.position).ToString("0.0"), GUILayout.Width(k_ColumnShortWidth));
                if (entry.m_bDirectionValid)
                {
                    float inOffset = Vector3.Dot(entry.m_pIn.position - entry.m_inStartPos, GetTravelAxis(entry, entry.m_pIn));
                    float outOffset = Vector3.Dot(entry.m_pOut.position - entry.m_outStartPos, GetTravelAxis(entry, entry.m_pOut));
                    GUILayout.Label(inOffset.ToString("+0.0;-0.0;0.0"), GUILayout.Width(k_ColumnOffsetWidth));
                    GUILayout.Label(outOffset.ToString("+0.0;-0.0;0.0"), GUILayout.Width(k_ColumnOffsetWidth));
                }
                else
                {
                    GUILayout.Label("向きが決められません", GUILayout.Width(k_ColumnOffsetWidth * 2));
                }
            }
            GUI.backgroundColor = previous;
        }
        EditorGUILayout.EndScrollView();
    }

    //下部のボタン(ずらす、元に戻す)
    private void DrawButtons()
    {
        EditorGUILayout.Space();
        bool hasSelection = m_selectedIndex >= 0 && m_selectedIndex < m_curves.Count;
        bool canShift = hasSelection && m_curves[m_selectedIndex].m_bDirectionValid;

        EditorGUILayout.LabelField(hasSelection ? "選択中: " + m_curves[m_selectedIndex].m_pContainer.name : "選択中: なし(一覧から選ぶか、シーンビューで選ぶ)", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(!canShift))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("早める  -" + m_stepMeters.ToString("0.##") + " m"))
                {
                    ShiftSelected(-m_stepMeters);
                }
                if (GUILayout.Button("遅らせる  +" + m_stepMeters.ToString("0.##") + " m"))
                {
                    ShiftSelected(m_stepMeters);
                }
            }
        }
        using (new EditorGUI.DisabledScope(!hasSelection))
        {
            if (GUILayout.Button("選択中のカーブを、基準の位置へ戻す"))
            {
                Undo.IncrementCurrentGroup();
                ResetEntry(m_curves[m_selectedIndex]);
                Undo.SetCurrentGroupName(k_UndoName);
                Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("すべてのカーブ(確認ダイアログが出ます)", EditorStyles.miniBoldLabel);
        using (new EditorGUI.DisabledScope(m_curves.Count == 0))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("すべて 早める  -" + m_stepMeters.ToString("0.##") + " m"))
                {
                    ShiftAll(-m_stepMeters);
                }
                if (GUILayout.Button("すべて 遅らせる  +" + m_stepMeters.ToString("0.##") + " m"))
                {
                    ShiftAll(m_stepMeters);
                }
            }
        }
        EditorGUILayout.HelpBox("「早める」は、標識が出る(または消える)位置を、手前(進行方向と逆)へずらします。\n"
            + "「向き」が「道」のカーブは、道路に沿ってずらし、面の向きも道路に合わせて回ります。\n"
            + "ずらしたあとは、シーンを保存してください。Ctrl+Z で元に戻せます。", MessageType.None);
    }

    //一覧のカーブを選び、Hierarchy で選択して、シーンビューで映す
    //引数 _index: 一覧の番号
    private void SelectCurve(int _index)
    {
        m_selectedIndex = _index;
        CurveEntry entry = m_curves[_index];
        Selection.activeTransform = entry.m_pContainer;

        SceneView view = SceneView.lastActiveSceneView;
        if (view == null) { return; }
        Bounds bounds = new Bounds(entry.m_pIn.position, Vector3.zero);
        bounds.Encapsulate(entry.m_pOut.position);
        bounds.Expand(k_FramePadding);
        view.Frame(bounds, false);
    }

    //Hierarchy の選択が変わったとき、選んだ物が属するカーブを選択中にする
    private void OnSelectionChanged()
    {
        Transform selected = Selection.activeTransform;
        if (selected != null)
        {
            for (int i = 0; i < m_curves.Count; ++i) //選んだ物が、どのカーブの中かを探すループ
            {
                if (selected == m_curves[i].m_pContainer || selected.IsChildOf(m_curves[i].m_pContainer))
                {
                    m_selectedIndex = i;
                    break;
                }
            }
        }
        Repaint();
    }

    //Undo・Redo の後、表示を更新する
    private void OnUndoRedo()
    {
        Repaint();
        SceneView.RepaintAll();
    }

    //向きの決め方の表示名
    //引数 _entry: 表示するカーブ
    private static string DirectionLabel(CurveEntry _entry)
    {
        if (!_entry.m_bDirectionValid) { return "不明"; }
        return _entry.m_bRoad ? "道" : "推定";
    }

    //強さの表示名
    //引数 _intensity: カーブの強さ(1〜3)
    private static string IntensityLabel(int _intensity)
    {
        switch (_intensity)
        {
            case k_IntensityWeak: return "緩";
            case k_IntensityMiddle: return "中";
            case k_IntensityStrong: return "急";
            default: return "?";
        }
    }

    //強さに応じた色(標識の色に合わせる)
    //引数 _intensity: カーブの強さ(1〜3)
    private static Color IntensityColor(int _intensity)
    {
        switch (_intensity)
        {
            case k_IntensityWeak: return k_ColorWeak;
            case k_IntensityMiddle: return k_ColorMiddle;
            case k_IntensityStrong: return k_ColorStrong;
            default: return k_ColorUnknown;
        }
    }

    //----------------------------------------------------------------
    // シーンビュー
    //----------------------------------------------------------------

    //シーンビューに、各カーブのIn→Outの線とトリガーの面を描く。選択中のカーブには、ドラッグできる矢印を出す
    //引数 _sceneView: 描画先のシーンビュー
    private void OnSceneGui(SceneView _sceneView)
    {
        if (!m_bDrawInScene || Application.isPlaying) { return; }

        for (int i = 0; i < m_curves.Count; ++i) //カーブを1つずつ描くループ
        {
            CurveEntry entry = m_curves[i];
            if (entry.m_pIn == null || entry.m_pOut == null) { continue; }

            Handles.color = IntensityColor(entry.m_intensity);
            Handles.DrawLine(entry.m_pIn.position, entry.m_pOut.position);
            DrawTriggerPlane(entry.m_pIn);
            DrawTriggerPlane(entry.m_pOut);
            Handles.Label(entry.m_pIn.position, entry.m_pContainer.name + " In " + (entry.m_bTurnLeft ? "左" : "右") + IntensityLabel(entry.m_intensity));
            Handles.Label(entry.m_pOut.position, "Out");
        }

        if (m_selectedIndex >= 0 && m_selectedIndex < m_curves.Count)
        {
            CurveEntry selected = m_curves[m_selectedIndex];
            if (selected.m_pIn != null && selected.m_pOut != null && selected.m_bDirectionValid)
            {
                DrawArrow(selected, selected.m_pIn);
                DrawArrow(selected, selected.m_pOut);
            }
        }
    }

    //トリガーの面(BoxColliderの大きさ)を枠で描く
    //引数 _point: InPointまたはOutPoint
    private static void DrawTriggerPlane(Transform _point)
    {
        BoxCollider box = _point.GetComponent<BoxCollider>();
        Vector3 center = box != null ? box.center : Vector3.zero;
        Vector3 size = box != null ? box.size : new Vector3(k_DefaultPlaneWidth, k_DefaultPlaneHeight, 0.0f);

        Matrix4x4 previous = Handles.matrix;
        Handles.matrix = Matrix4x4.TRS(_point.position, _point.rotation, Vector3.one);
        Handles.DrawWireCube(center, size);
        Handles.matrix = previous;
    }

    //進行方向に沿ってドラッグできる矢印を出し、ドラッグした量だけポイントをずらす
    //引数 _entry: ポイントが属するカーブ
    //引数 _point: InPointまたはOutPoint
    private void DrawArrow(CurveEntry _entry, Transform _point)
    {
        Vector3 axis = GetTravelAxis(_entry, _point);
        float size = HandleUtility.GetHandleSize(_point.position) * k_ArrowSizeScale;

        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.Slider(_point.position, axis, size, Handles.ArrowHandleCap, k_ArrowSnapMeters);
        if (EditorGUI.EndChangeCheck())
        {
            float meters = Vector3.Dot(moved - _point.position, axis);
            ShiftPoint(_entry, _point, meters);
            Repaint();
        }
    }
}
