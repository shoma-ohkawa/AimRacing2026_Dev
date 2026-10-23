using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

//MT自動走行の変更範囲とATの計算維持を検査するクラス
public static class TrackManualDriveValidation
{
    //MTの速度計画と操舵の連続性を本番関数で確認する関数
    [MenuItem("Tools/検証/Validate MT Auto Driving")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
        //試験後にユーザーが選んだ自動走行モードへ必ず戻すための元の値
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var type = typeof(TrackDriveSession);
        var profile = type.GetField("profile", flags);
        var challenge = type.GetField("driftChallenge", flags);
        object savedProfile = profile.GetValue(null);
        object savedChallenge = challenge.GetValue(null);
        try
        {
            //ATの既存計算は今回のMT用処理を通らないことを回帰検査する
            profile.SetValue(null, 2);
            challenge.SetValue(null, true);
            TrackDriftValidation.Run();
            profile.SetValue(null, 3);
            //高速走行用の希望下限と通常の曲率計算を混同せず別々に検査する
            var cruise = type.GetMethod("CalculateManualCruiseSpeed", flags);
            float Cruise(float curve) => (float)cruise.Invoke(null, new object[] { curve, 22f });
            Require(Mathf.Abs(Cruise(0.05f) * 3.6f - 100f) < 0.001f, "高速試走の希望下限");
            Require(Cruise(0.02f) * 3.6f > 105f, "緩いカーブを下限速度に固定しない");
            Require(Cruise(0.05f) == Cruise(-0.05f), "高速試走の左右対称性");
            //MTだけ中高速カーブで進入し危険な状態では追加の後輪制動を禁止する
            var entry = type.GetMethod("CanInitiateHairpin", flags);
            bool Entry(float speed, float curve, float slip, int grounded) =>
                (bool)entry.Invoke(null, new object[] { speed, 140f, curve, 0f, slip, 0f, grounded });
            Require(Entry(140f, 0.02f, 0f, 4) && Entry(140f, -0.02f, 0f, 4), "左右の中高速ドリフト進入");
            Require(!Entry(100f, 0.02f, 0f, 4), "速度不足で追加減速しない");
            Require(!Entry(140f, 0.005f, 0f, 4), "直線で後輪制動しない");
            Require(!Entry(140f, 0.02f, 8f, 4) && !Entry(140f, 0.02f, 0f, 3), "滑り過ぎと未接地で追加しない");
            //曲率補助は不足した舵だけを補い切り返しと十分な操舵を変更しない
            var pursuitMethod = type.GetMethod("CalculateManualPursuit", flags);
            float Pursuit(float angle, float curve) => (float)pursuitMethod.Invoke(null, new object[] { angle, curve });
            Require(Pursuit(2f, 0.035f) > 2f && Pursuit(2f, 0.035f) < 5.2f, "急カーブの操舵不足を先に補う");
            Require(Pursuit(-2f, -0.035f) == -Pursuit(2f, 0.035f), "曲率補助の左右対称性");
            Require(Pursuit(-2f, 0.035f) == -2f && Pursuit(8f, 0.035f) == 8f, "回避と十分な操舵を維持");
            var reset = type.GetMethod("ResetManualDriver", flags);
            var steering = type.GetMethod("SmoothDriverSteering", flags);
            var corner = type.GetMethod("CalculateManualCornerSpeed", flags);
            var apex = type.GetMethod("CalculateApexOffset", flags);
            var departure = type.GetMethod("PredictManualLineError", flags);
            float Error(float offset, float velocity) => (float)departure.Invoke(null, new object[] { offset, velocity });
            //内側ラインへ近づく横移動ではドリフト目標を消さず逸脱時だけ余裕を減らす
            Require(Mathf.Abs(Error(-0.5f, 2f) - 0.5f) < 0.00001f, "ラインへ復帰する横速度を除外");
            Require(Mathf.Abs(Error(-0.5f, -2f) - 1.3f) < 0.00001f, "ラインから離れる横速度を予測");
            Require(Error(-0.5f, -2f) == Error(0.5f, 2f), "逸脱予測の左右対称性");
            float Steer(float value) => (float)steering.Invoke(null, new object[] { 0f, value, 0.02f });
            float Corner(float curve) => (float)corner.Invoke(null, new object[] { curve, 22f });
            float Apex(float progress) => (float)apex.Invoke(null, new object[] { progress });
            //左右どちらでも同じ滑らかさで操舵し大きな入力を一度に渡さない
            reset.Invoke(null, null);
            float right = Steer(1f);
            reset.Invoke(null, null);
            Require(Mathf.Abs(right + Steer(-1f)) < 0.00001f, "操舵の左右対称性");
            Require(right > 0f && right < 0.045f, "最初の操舵量を制限");
            Require(Corner(0.04f) == Corner(-0.04f), "通過速度の左右対称性");
            Require(Mathf.Abs(Corner(0.02f) * 3.6f - 119.3985f) < 0.01f, "通常カーブのMT速度計画");
            Require(Corner(0.05f) * Corner(0.05f) * 0.05f <= 22.001f, "希望最低速度で指定横加速度を超えない");
            //急カーブや低グリップ相当の計画でも指定した横加速度を上回らないことを確認する
            foreach (float curve in new[] { -0.1f, -0.04f, 0.02f, 0.04f, 0.1f })
            {
                foreach (float acceleration in new[] { 0f, 5f, 15f, 22f, 26f, 40f })
                {
                    float speed = (float)corner.Invoke(null, new object[] { curve, acceleration });
                    Require(speed * speed * Mathf.Abs(curve) <= Mathf.Min(acceleration, 26f) + 0.001f, "MT計画の横加速度上限");
                }
            }
            //直前の入力速度を残した状態で左右両方向の切り返しを再現する
            var steerVelocity = type.GetField("manualSteerVelocity", flags);
            foreach (float direction in new[] { -1f, 1f })
            {
                foreach (float step in new[] { 0.01f, 0.02f, 0.04f })
                {
                    reset.Invoke(null, null);
                    steerVelocity.SetValue(null, direction * 2f);
                    float current = direction * 0.5f;
                    float next = (float)steering.Invoke(null, new object[] { current, -direction, step });
                    Require((next - current) * direction < 0f, "逆ハンドルで古い操舵方向へ進まない");
                    Require(Mathf.Abs(next - current) <= 2.25f * step + 0.00001f, "切り返しの操作速度上限");
                    for (int tick = 0; tick < 100; tick++)
                    {
                        current = next;
                        next = (float)steering.Invoke(null, new object[] { current, -direction, step });
                        Require(Mathf.Abs(next) <= 1.00001f, "切り返しで入力範囲を超えない");
                        Require((next - current) * direction <= 0.00001f, "切り返し中に入力が逆戻りしない");
                    }
                }
            }
            Require(Apex(0f) < 0f && Apex(0.5f) > 0f && Apex(0.8f) > 0f && Apex(1f) == 0f, "内側を出口まで維持");
            Debug.Log("[TrackManualDriveValidation] PASS MT操舵・速度計画・内側通過・AT回帰検査");
        }
        finally
        {
            //試験で変更した選択と操舵の記憶を残さない
            profile.SetValue(null, savedProfile);
            challenge.SetValue(null, savedChallenge);
            type.GetMethod("ResetManualDriver", flags).Invoke(null, null);
        }
    }

    //期待した計算と違う結果を検証失敗として通知する関数
    static void Require(bool condition, string description)
    {
        if (!condition) { throw new InvalidOperationException("[TrackManualDriveValidation] FAIL " + description); }
    }
}
