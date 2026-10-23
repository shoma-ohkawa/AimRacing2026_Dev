using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 画面の縁に沿ってバーを並べ、音に合わせて中心方向へ伸ばす。
/// 指定したカメラの正面に毎フレーム自動で貼り付くので、カメラの子にしてもしなくても動く。
///
/// 画面の大きさは Camera.ViewportToWorldPoint から取得しているため、
/// 解像度・アスペクト比・Viewport Rect が何であっても必ず画面にフィットする。
///
/// 必要なコンポーネント: MeshFilter / MeshRenderer
/// マテリアルは SG_ECGLine 製のもの（Vertex Color ノード入り、Depth Test = Always）を使う。
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[ExecuteAlways]
public class EdgeSpectrumBars : MonoBehaviour
{
    public enum FreqLayout
    {
        LeftToRight,    // 端から端へ 低音 → 高音
        LowAtCenter,    // 中央が低音、外側が高音
        HighAtCenter,   // 中央が高音、外側が低音
    }

    [Header("カメラ")]
    [Tooltip("この演出を映すカメラ。空なら親のカメラ、それも無ければ Camera.main")]
    [SerializeField] private Camera targetCamera;
    [Tooltip("カメラからの距離。画面へのフィットは自動で保たれる")]
    [SerializeField] private float distance = 8f;
    [Tooltip("起動時に解像度とアスペクト比をログに出す（実機での不具合調査用）")]
    [SerializeField] private bool logScreenInfo = false;

    [Header("音源")]
    [Tooltip("BGM の AudioSource。空にするとゲーム全体の音に反応する")]
    [SerializeField] private AudioSource source;

    [Header("配置")]
    [Tooltip("画面端からの余白（画面の高さに対する割合）")]
    [Range(0f, 0.2f)]
    [SerializeField] private float margin = 0.01f;
    [Tooltip("枠全体の大きさ。1 で画面ぴったり、小さくすると内側に寄る")]
    [Range(0.3f, 1f)]
    [SerializeField] private float frameScale = 1f;

    [Header("どの縁に出すか")]
    [SerializeField] private bool top = true;
    [SerializeField] private bool bottom = true;
    [SerializeField] private bool left = false;
    [SerializeField] private bool right = false;

    [Header("角を空ける")]
    [Tooltip("上下のバーの両端を削る割合")]
    [Range(0f, 0.45f)]
    [SerializeField] private float topBottomInset = 0f;
    [Tooltip("左右のバーの両端を削る割合")]
    [Range(0f, 0.45f)]
    [SerializeField] private float sideInset = 0f;

    [Header("バー（上下）")]
    [Range(4, 256)]
    [SerializeField] private int barsPerEdge = 80;
    [Tooltip("間隔に対するバーの太さ（1.0 で隙間なし）")]
    [Range(0.05f, 1f)]
    [SerializeField] private float barWidth = 0.45f;
    [Tooltip("最大の長さ（画面の高さに対する割合）")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float maxLength = 0.2f;
    [Range(0f, 0.2f)]
    [SerializeField] private float minLength = 0.012f;

    [Header("バー（左右）")]
    [Range(4, 256)]
    [SerializeField] private int sideBars = 16;
    [Range(0.05f, 1f)]
    [SerializeField] private float sideBarWidth = 0.45f;
    [Tooltip("最大の長さ（画面の【幅】に対する割合）")]
    [Range(0.005f, 0.3f)]
    [SerializeField] private float sideMaxLength = 0.04f;
    [Range(0f, 0.1f)]
    [SerializeField] private float sideMinLength = 0.004f;

    [Header("周波数の並び")]
    [SerializeField] private FreqLayout freqLayout = FreqLayout.HighAtCenter;

    [Header("色")]
    [ColorUsage(true, true)]
    [SerializeField] private Color barColor = Color.white;
    [Tooltip("先端をどれだけ暗くするか。1 で完全に消える")]
    [Range(0f, 1f)]
    [SerializeField] private float tipFade = 0.85f;
    [Tooltip("縁の端のバーを暗くする")]
    [Range(0f, 0.5f)]
    [SerializeField] private float cornerFade = 0f;

    [Header("虹色")]
    [SerializeField] private bool rainbow = false;
    [SerializeField] private float hueSpeed = 0.08f;
    [Tooltip("縁の端から端で色相が何周するか")]
    [SerializeField] private float hueSpread = 0.6f;
    [Range(0f, 1f)]
    [SerializeField] private float saturation = 0.8f;

    [Header("動き")]
    [Tooltip("大きいほど音に鋭く反応する")]
    [SerializeField] private float smooth = 20f;
    [Tooltip("反応の強さ")]
    [SerializeField] private float gain = 1f;
    [SerializeField] private float dbFloor = -70f;
    [SerializeField] private float dbCeil = -12f;

    private const int FFT_RESOLUTION = 512;

    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    private float[] spectrum;
    private float[] heights;

    private Vector3[] verts;
    private Color[] colors;
    private int[] tris;
    private int cursor;

    private void Awake() => Setup();

    private void Start()
    {
        if (!logScreenInfo || !Application.isPlaying) return;

        Camera cam = ResolveCamera();
        AppLog.Log($"[EdgeSpectrumBars] Screen={Screen.width}x{Screen.height} " +
                  $"currentResolution={Screen.currentResolution.width}x{Screen.currentResolution.height} " +
                  $"fullScreen={Screen.fullScreen} mode={Screen.fullScreenMode} " +
                  (cam != null
                      ? $"cam={cam.name} aspect={cam.aspect:F3} fov={cam.fieldOfView:F1} " +
                        $"pixel={cam.pixelWidth}x{cam.pixelHeight} rect={cam.rect} " +
                        $"targetDisplay={cam.targetDisplay}"
                      : "cam=null"));
    }

    private Camera ResolveCamera()
    {
        if (targetCamera != null) return targetCamera;
        var parentCam = GetComponentInParent<Camera>();
        return parentCam != null ? parentCam : Camera.main;
    }

    private void Setup()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();

        if (spectrum == null || spectrum.Length != FFT_RESOLUTION)
            spectrum = new float[FFT_RESOLUTION];

        if (heights == null || heights.Length != barsPerEdge)
            heights = new float[barsPerEdge];

        int barTotal = ((top ? 1 : 0) + (bottom ? 1 : 0)) * barsPerEdge
                     + ((left ? 1 : 0) + (right ? 1 : 0)) * sideBars;
        barTotal = Mathf.Max(1, barTotal);

        if (verts == null || verts.Length != barTotal * 4)
        {
            verts = new Vector3[barTotal * 4];
            colors = new Color[barTotal * 4];
            tris = new int[barTotal * 6];

            for (int b = 0; b < barTotal; b++)
            {
                int v = b * 4;
                int i = b * 6;
                tris[i + 0] = v + 0;
                tris[i + 1] = v + 1;
                tris[i + 2] = v + 2;
                tris[i + 3] = v + 2;
                tris[i + 4] = v + 3;
                tris[i + 5] = v + 0;
            }

            mesh = null;
        }

        if (mesh == null)
        {
            mesh = new Mesh { name = "EdgeSpectrumBars" };
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;
        }

        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
    }

    // カメラのアニメーションが終わったあとに追従したいので LateUpdate
    private void LateUpdate()
    {
        Setup();

        Camera cam = ResolveCamera();
        if (cam == null) return;

        Transform ct = cam.transform;
        transform.position = ct.position + ct.forward * Mathf.Max(0.1f, distance);
        transform.rotation = ct.rotation;
        transform.localScale = Vector3.one;

        if (Application.isPlaying) ReadAudio();
        else FillIdle();

        BuildMesh(cam);
    }

    /// <summary>並び位置 u (0〜1) を、周波数の位置 (0=低音, 1=高音) に変換する</summary>
    private float FreqAt(float u)
    {
        switch (freqLayout)
        {
            case FreqLayout.LowAtCenter: return Mathf.Abs(u * 2f - 1f);
            case FreqLayout.HighAtCenter: return 1f - Mathf.Abs(u * 2f - 1f);
            default: return u;
        }
    }

    private void ReadAudio()
    {
        if (source != null) source.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
        else AudioListener.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);

        float k = 1f - Mathf.Exp(-smooth * Time.deltaTime);

        for (int i = 0; i < barsPerEdge; i++)
        {
            float u = (float)i / (barsPerEdge - 1);
            float f = FreqAt(u);

            int idx = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Pow(f, 2.2f) * (FFT_RESOLUTION / 2 - 1)),
                0, FFT_RESOLUTION - 1);

            float db = 20f * Mathf.Log10(spectrum[idx] + 1e-7f);
            float target = Mathf.Clamp01(Mathf.InverseLerp(dbFloor, dbCeil, db)) * gain;

            heights[i] = Mathf.Lerp(heights[i], target, k);
        }
    }

    /// <summary>編集中は音が鳴らないので、形が見えるだけの波を入れておく</summary>
    private void FillIdle()
    {
        float t = Time.realtimeSinceStartup;
        for (int i = 0; i < barsPerEdge; i++)
        {
            float u = (float)i / (barsPerEdge - 1);
            heights[i] = (Mathf.Sin((u * 6f + t * 0.5f) * Mathf.PI * 2f) * 0.5f + 0.5f) * 0.6f;
        }
    }

    private void BuildMesh(Camera cam)
    {
        // 画角やアスペクト比から計算せず、カメラに「実際に映る範囲」を直接聞く。
        // これで解像度・Viewport Rect・射影行列がどうなっていても必ず画面に合う。
        float d = Mathf.Max(0.1f, distance);
        Vector3 bl = transform.InverseTransformPoint(cam.ViewportToWorldPoint(new Vector3(0f, 0f, d)));
        Vector3 tr = transform.InverseTransformPoint(cam.ViewportToWorldPoint(new Vector3(1f, 1f, d)));

        float halfW = Mathf.Abs(tr.x - bl.x) * 0.5f * frameScale;
        float halfH = Mathf.Abs(tr.y - bl.y) * 0.5f * frameScale;

        if (halfW < 1e-4f || halfH < 1e-4f) return;

        float m = halfH * 2f * margin;

        // 上下は画面の高さ基準、左右は画面の幅基準で長さを決める
        float maxLenV = halfH * 2f * maxLength;
        float minLenV = halfH * 2f * minLength;
        float maxLenH = halfW * 2f * sideMaxLength;
        float minLenH = halfW * 2f * sideMinLength;

        // 角を空けるぶんだけ、並べる範囲を縮める
        float spanH = halfW * (1f - topBottomInset);
        float spanV = halfH * (1f - sideInset);

        float hueBase = Time.realtimeSinceStartup * hueSpeed;

        cursor = 0;

        if (top) BuildEdge(new Vector2(0f, halfH - m), Vector2.right, Vector2.down,
                              spanH, barsPerEdge, barWidth, maxLenV, minLenV, hueBase);
        if (bottom) BuildEdge(new Vector2(0f, -halfH + m), Vector2.right, Vector2.up,
                              spanH, barsPerEdge, barWidth, maxLenV, minLenV, hueBase);
        if (left) BuildEdge(new Vector2(-halfW + m, 0f), Vector2.up, Vector2.right,
                              spanV, sideBars, sideBarWidth, maxLenH, minLenH, hueBase);
        if (right) BuildEdge(new Vector2(halfW - m, 0f), Vector2.up, Vector2.left,
                              spanV, sideBars, sideBarWidth, maxLenH, minLenH, hueBase);

        mesh.Clear();
        mesh.vertices = verts;
        mesh.colors = colors;
        mesh.triangles = tris;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);   // カリング対策
    }

    /// <param name="origin">縁の中央</param>
    /// <param name="along">縁に沿う方向（バーの太さはこの向き）</param>
    /// <param name="inward">中心へ向かう方向（バーが伸びる向き）</param>
    /// <param name="halfSpan">バーを並べる範囲の半分</param>
    private void BuildEdge(Vector2 origin, Vector2 along, Vector2 inward,
                           float halfSpan, int count, float widthRatio,
                           float maxLen, float minLen, float hueBase)
    {
        count = Mathf.Max(2, count);

        float step = halfSpan * 2f / count;
        float halfW = step * widthRatio * 0.5f;

        // 縁ごとに三角形の表裏が反転するので、向きに応じて頂点の左右を入れ替える
        float winding = (along.x * inward.y - along.y * inward.x) < 0f ? 1f : -1f;
        Vector2 side = along * (halfW * winding);

        for (int i = 0; i < count; i++)
        {
            float u = (float)i / (count - 1);

            // 上下と左右で本数が違っても、同じスペクトルを共有できるようにサンプリングする
            int hi = Mathf.Clamp(Mathf.RoundToInt(u * (barsPerEdge - 1)), 0, barsPerEdge - 1);

            Vector2 center = origin + along * (-halfSpan + step * (i + 0.5f));
            float len = minLen + heights[hi] * maxLen;

            float fade = 1f;
            if (cornerFade > 0.0001f)
            {
                fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / cornerFade));
                fade *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - u) / cornerFade));
            }

            Color c = barColor;
            if (rainbow)
            {
                float hue = Mathf.Repeat(hueBase + u * hueSpread, 1f);
                Color hsv = Color.HSVToRGB(hue, saturation, 1f);
                c = new Color(barColor.r * hsv.r, barColor.g * hsv.g, barColor.b * hsv.b, 1f);
            }

            Color cRoot = c * fade;                    // 画面端側
            Color cTip = c * fade * (1f - tipFade);   // 中心側
            cRoot.a = 1f;
            cTip.a = 1f;

            Vector2 r0 = center - side;
            Vector2 r1 = center + side;
            Vector2 t0 = r0 + inward * len;
            Vector2 t1 = r1 + inward * len;

            int v = cursor * 4;
            verts[v + 0] = r0; colors[v + 0] = cRoot;
            verts[v + 1] = r1; colors[v + 1] = cRoot;
            verts[v + 2] = t1; colors[v + 2] = cTip;
            verts[v + 3] = t0; colors[v + 3] = cTip;

            cursor++;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        mesh = null;
        Setup();
    }
#endif
}