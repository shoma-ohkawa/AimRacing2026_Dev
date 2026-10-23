#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

//保存済み車両設定を変えずTrackの運転入力と走行目標を作るクラス
public static partial class TrackDriveSession
{
    //走行開始からの達成目標で車速や時間を強制する値ではない
    const float TargetRaceSeconds = 160f;
    //直線の最高目標速度で実際の到達速度は車両性能に任せる
    const float StraightSpeedKph = 210f;
    //通常のTrack自動走行の希望下限で急カーブの旋回限界より優先しない
    const float MinimumTrackSpeedKph = 105f;
    //路肩への逸脱時には通常の速度目標より復帰を優先する下限
    const float RecoveryMinimumKph = 25f;
    //全ての自動操舵へ最後に適用する毎秒の入力変化上限
    const float DriverSteerRate = 1.5f;
    //小さな追従誤差への切り返しを連続的にする応答秒数
    const float DriverSteerResponseSeconds = 0.09f;

    //進入処理の上書きも含めた最終入力の急変を抑える関数
    static float SmoothDriverSteering(float current, float requested, float deltaTime)
    {
        //MTだけ操舵速度の急変も抑えATの入力処理を保持する
        if (IsManualTrackDrive) { return SmoothManualSteering(current, requested, deltaTime); }
        float step = Mathf.Max(0f, deltaTime);
        float filtered = Mathf.Lerp(current, Mathf.Clamp(requested, -1f, 1f), 1f - Mathf.Exp(-step / DriverSteerResponseSeconds));
        return Mathf.MoveTowards(current, filtered, DriverSteerRate * step);
    }
    //発進直後の操舵で滑りを要求しない速度
    const float DriverDriftStartKph = 15f;
    //低速ヘアピンでドリフト要求を全量にする速度
    const float DriverDriftFullKph = 30f;
    //ゲーム内の運転目標を見積もる横加速度で車両の摩擦や力は変更しない
    const float CornerAcceleration = 26f;
    //急カーブでは横滑りから壁へ膨らまないよう進入速度を抑える
    const float TightCornerAcceleration = 20f;
    //急カーブで希望下限へ近づける際の計画横加速度上限で車両へ力は加えない
    const float CornerSpeedReserveAcceleration = 24f;
    //道路点の切り替わりで先読み位置が飛ばないための点間距離
    static float routeProgressMeters;

    //車両位置を近傍の道路線分へ投影して連続した走行距離を求める関数
    static void UpdateRouteProgress()
    {
        routeProgressMeters = 0f;
        float best = float.PositiveInfinity;
        for (int step = -1; step <= 1; step += 2)
        {
            int other = nearest + direction * step;
            if (other < 0 || other >= route.Length) { continue; }
            Vector3 segment = route[other] - route[nearest];
            float length = segment.magnitude;
            if (length < 0.001f) { continue; }
            float fraction = Mathf.Clamp01(Vector3.Dot(vehicle.transform.position - route[nearest], segment) / segment.sqrMagnitude);
            float error = (vehicle.transform.position - route[nearest] - segment * fraction).sqrMagnitude;
            if (error >= best) { continue; }
            best = error;
            routeProgressMeters = step * fraction * length;
        }
    }
    //減速開始距離を計画するための想定制動能力
    const float PlannedDeceleration = 7f;
    //急カーブで狙う横滑り角で車体へ回転や横力を直接加えない
    const float MaximumDriftDegrees = 10f;
    //高速で狭いカーブを通る時は横滑りを控え進路追従の余裕を残す
    const float HighSpeedDriftDegrees = 6f;
    //極端なヘアピンだけでなく中程度のカーブでも滑り目標を段階的に作る曲率
    const float DriverDriftCurveStart = 0.008f;
    const float DriverDriftCurveFull = 0.02f;
    //曲がり始めに姿勢を準備する時間で逆カーブの滑りは先に反転させない
    const float DriverDriftPreviewSeconds = 0.25f;
    //入力逆算に使うホイールベース
    const float DriverWheelbase = 2.56f;
    //急カーブで遠方の目標へ直行して内側を切り過ぎないための先読み短縮率
    const float TightCornerLookaheadScale = 0.85f;
    //路肩へ近づくにつれドリフト要求を段階的に減らす横ずれ範囲
    const float DriftFadeStartMeters = 0.75f;
    const float DriftFadeEndMeters = 2.5f;
    //目標滑り角の切替と操舵の急変を抑える状態
    static float desiredSlip;
    static float driverSteer;
    //進入補正と通常追従の切替で目標旋回率を飛ばさない混合率
    static float driftPursuitBlend;
    //拡大し始めた横滑りへ遅れず逆舵を当てる先読み時間
    const float DriverSlipPredictionSeconds = 0.06f;
    //路肩へ外れた時に加速を続けないための道路中心からの横距離
    static float driverRoadError;
    //描画更新が物理より速くても同じ時刻で入力を重ねないための時刻
    static float lastDriverTime = -1f;
    //カウントダウン時間を除外する走行開始時刻
    static float raceStarted = -1f;

    //変速の連打を防ぎクラッチ接続後の加速を待つ最短間隔
    const float DriverShiftInterval = 0.7f;
    //加速に使う回転域へ戻してから再びアップするまでの回転差
    const float DriverDownshiftRatio = 0.6f;
    //次段で過回転しない範囲に余裕を持たせる回転比
    const float DriverSafeDownshiftRatio = 0.88f;
    //空転だけでシフトアップしないための車速由来回転比
    const float DriverUpshiftRatio = 0.91f;
    //下げた直後にアップ条件へ戻らないために残す回転数の余裕
    const float DriverShiftHysteresis = 0.08f;
    //進路が安定した出口で駆動を早く戻す目標速度付近のアクセル量
    const float DriverCruiseThrottle = 0.8f;
    //速度誤差をアクセル量へ換算する一キロ毎時当たりの割合
    const float DriverThrottleGain = 0.1f;
    //滑り過ぎた時だけ追加する逆舵の割合で走行ラインの追従を優先する
    const float DriverCountersteerGain = 0.15f;
    //路肩へ流れる時に実際の移動方向を進路追従へ反映する最大割合
    const float TravelRecoveryBlend = 0.5f;
    //横移動の勢いから路肩への接近を先読みする時間
    const float RoadDeparturePredictionSeconds = 0.4f;
    //旋回率の誤差を前輪角へ戻す際の補正割合
    const float DriverYawFeedbackGain = 0.4f;
    //次の変速要求を送れるゲーム内時刻
    static float nextDriverShift;
    //速度計画で最も厳しかった地点までの距離を記録する値
    static float limitingCornerDistance;
    //現在地点の曲率と走行段階をログで照合する値
    static float driverCurvature;
    static int driverPhase;

    //次の段の回転数も確認しアップ直後の不要なダウン要求を防ぐ関数
    static bool ShouldDriverDownshift(float currentRPM, float lowerRPM, float limit)
    {
        //上限回転が不正な場合は安全側で変速要求を出さない
        if (limit <= 0f) { return false; }
        return currentRPM < limit * DriverDownshiftRatio
            && lowerRPM < limit * Mathf.Min(DriverSafeDownshiftRatio, DriverUpshiftRatio - DriverShiftHysteresis);
    }

    //旋回に必要な減速を残し直線と出口で部分アクセルが長引かない要求を求める関数
    static float CalculateDriverThrottle(float speed, float target)
    {
        return Mathf.Clamp01(DriverCruiseThrottle + (target - speed) * DriverThrottleGain);
    }

    //通常検証とTrackの積極的な自動走行を区別する関数
    static bool IsHighSpeedTrackDrive => driftChallenge && profile >= 2 && profile < 4;

    //今回の速度計画と操舵調整をMT自動走行だけに適用する判定
    static bool IsManualTrackDrive => IsHighSpeedTrackDrive && profile == 3;

    //Trackの自動変速と急カーブの進入制御を使い通常のゲームフローから走行する関数
    #if UNITY_EDITOR
    [MenuItem("Tools/自動走行/Track AT")]
    #endif
    public static void RunTrackAutomatic() { RunFromWindow(2, false, true, false, false, true); }

    //Trackの手動変速要求と急カーブの進入制御を使い通常のゲームフローから走行する関数
    #if UNITY_EDITOR
    [MenuItem("Tools/自動走行/Track MT")]
    #endif
    public static void RunTrackManual() { RunFromWindow(3, false, true, false, false, true); }

    //三つの道路点から符号付き曲率を求める関数
    static float RoadCurvature(float distance)
    {
        Vector3 a = RoutePoint(distance - 10f);
        Vector3 b = Ahead(distance);
        Vector3 c = Ahead(distance + 10f);
        Vector3 ab = Vector3.ProjectOnPlane(b - a, Vector3.up);
        Vector3 bc = Vector3.ProjectOnPlane(c - b, Vector3.up);
        Vector3 ac = ab + bc;
        float denominator = ab.magnitude * bc.magnitude * ac.magnitude;
        if (denominator < 0.001f) { return 0f; }
        return 2f * Vector3.Dot(Vector3.Cross(ab, bc), Vector3.up) / denominator;
    }

    //車の直下のカーブも計測できるよう後方の道路点を取得する関数
    static Vector3 RoutePoint(float distance)
    {
        distance += routeProgressMeters;
        //道路点の間を補間し点間隔による曲率と操舵の段差をなくす
        int index = nearest;
        float traveled = 0f;
        int step = distance < 0f ? -direction : direction;
        float requested = Mathf.Abs(distance);
        while (index + step >= 0 && index + step < route.Length)
        {
            float length = Vector3.Distance(route[index], route[index + step]);
            if (traveled + length >= requested && length > 0f)
            {
                return Vector3.Lerp(route[index], route[index + step], (requested - traveled) / length);
            }
            traveled += length;
            index += step;
        }
        return route[index];
    }

    //横加速度と制動距離から現在の安全側の目標速度を計算する関数
    static float PlannedSpeed(float minimumSpeed)
    {
        float target = StraightSpeedKph;
        limitingCornerDistance = -1f;
        //直前のカーブも含め遠方だけを見て急カーブへ加速しない
        for (float distance = 0f; distance <= 250f; distance += 10f)
        {
            float curvature = Mathf.Abs(CalculatePathCurvature(RacingPoint(distance - 5f), RacingPoint(distance), RacingPoint(distance + 5f)));
            //理想ラインへまだ到達していない車でも曲がれるよう道路中央の曲率も下限に使う
            curvature = Mathf.Max(curvature, Mathf.Abs(RoadCurvature(distance)));
            float tightness = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.012f, 0.03f, curvature));
            //検証用の速度計画だけを変更し車両へ加速度や速度を直接与えない
            float tightAcceleration = TrackDrivePreferences.GetBool(HairpinTestKey, false) ? HairpinPlannedAcceleration : TightCornerAcceleration;
            float acceleration = Mathf.Lerp(CornerAcceleration, tightAcceleration, tightness);
            float corner = CalculateCornerSpeed(curvature, acceleration, minimumSpeed);
            //MTの高速試験は希望下限を設け実際に維持できた速度を走行ログで検証する
            if (IsManualTrackDrive)
            {
                //高速走行試験の希望下限を入力目標にし逸脱時の安全減速は後段で残す
                corner = CalculateManualCruiseSpeed(curvature, acceleration);
            }
            //入力遅れの距離を差し引いてカーブ手前で制動を始める
            float available = Mathf.Max(0f, distance - Mathf.Abs(vehicle.KPH) / 3.6f * 0.3f - 5f);
            float allowed = Mathf.Sqrt(corner * corner + 2f * PlannedDeceleration * available);
            if (allowed * 3.6f < target)
            {
                target = allowed * 3.6f;
                limitingCornerDistance = distance;
            }
        }
        //速度計画だけでは追従誤差を検知できないため路肩へ近づいた時は回復速度を優先する
        //中心からずれていても道路へ戻っている途中なら際限なく減速しない
        Vector3 tangent = Vector3.ProjectOnPlane(Ahead(5f) - RoutePoint(-5f), Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, tangent);
        float lateral = Vector3.Dot(vehicle.transform.position - route[nearest], right);
        float outwardSpeed = Mathf.Sign(lateral) * Vector3.Dot(vehicleBody.linearVelocity, right);
        float recovery = Mathf.InverseLerp(1.25f, 2.5f, driverRoadError) * Mathf.InverseLerp(0f, 2f, outwardSpeed);
        float recoverySpeed = Mathf.Max(RecoveryMinimumKph, Mathf.Abs(vehicle.KPH) - 10f * recovery);
        return recovery > 0f ? Mathf.Min(target, recoverySpeed) : target;
    }

    //通常の急カーブ目標を希望下限にそろえ逸脱時の減速は別途適用する関数
    static float CalculateCornerSpeed(float curvature, float acceleration, float requestedMinimumKph)
    {
        //横加速度と曲率から求めた通過速度はメートル毎秒で扱う
        float bend = Mathf.Max(0.0001f, Mathf.Abs(curvature));
        float planned = Mathf.Sqrt(Mathf.Max(0f, acceleration) / bend);
        float feasible = Mathf.Sqrt(CornerSpeedReserveAcceleration / bend);
        float minimum = Mathf.Min(Mathf.Max(0f, requestedMinimumKph) / 3.6f, feasible);
        return Mathf.Clamp(planned, minimum, StraightSpeedKph / 3.6f);
    }

    //結果画面と同じ時計を読み待機解除から計時開始までの差を除く関数
    static float ReadOfficialRaceSeconds()
    {
        TimeKeeper timer = Object.FindFirstObjectByType<TimeKeeper>();
        if (timer == null) { return -1f; }
        string[] parts = timer.RetrieveSavedTotalTime().Split(':');
        if (parts.Length != 2) { return -1f; }
        if (!float.TryParse(parts[0], out float minutes)) { return -1f; }
        if (!float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float seconds)) { return -1f; }
        return minutes * 60f + seconds;
    }

    //走行方向と車体の横滑りを見ながら小さなドリフト角を維持する操舵を作る関数
    static float CalculateDriverInput(float speed, float deltaTime)
    {
        float lookahead = Mathf.Clamp(7f + speed / 3.6f * 0.45f, 8f, 32f);
        //急カーブと切り返しでは近い道路点へ追従して旋回の遅れを減らす
        float bend = Mathf.Max(Mathf.Abs(RoadCurvature(5f)), Mathf.Abs(RoadCurvature(lookahead)));
        float tightness = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.012f, 0.03f, bend));
        lookahead *= Mathf.Lerp(1f, TightCornerLookaheadScale, tightness);
        if (earlyTurn) { lookahead += speed / 3.6f * 0.25f; }
        Vector3 offset = Vector3.ProjectOnPlane(RacingPoint(lookahead) - vehicle.transform.position, Vector3.up);
        Vector3 forward = Vector3.ProjectOnPlane(vehicle.transform.forward, Vector3.up).normalized;
        Vector3 velocity = Vector3.ProjectOnPlane(vehicleBody.linearVelocity, Vector3.up);
        Vector3 travel = velocity.magnitude > 3f ? velocity.normalized : forward;
        //先の逆カーブへ入る前に滑りの向きを反転させない
        float curvature = RoadCurvature(5f);
        driverCurvature = curvature;
        float actualSlip = Vector3.SignedAngle(forward, travel, Vector3.up);
        Vector3 tangent = Vector3.ProjectOnPlane(Ahead(5f) - RoutePoint(-5f), Vector3.up).normalized;
        Vector3 roadRight = Vector3.Cross(Vector3.up, tangent);
        float lateral = Vector3.Dot(vehicle.transform.position - RoutePoint(0f), roadRight);
        float roadError = Mathf.Abs(lateral);
        driverRoadError = roadError;
        //計画した内側通過と意図しない路肩への逸脱を区別する
        driverLineOffset = Vector3.Dot(RacingPoint(0f) - RoutePoint(0f), roadRight);
        driverLineError = lateral - driverLineOffset;
        //壁に近づいてからでは遅いため外向きの横速度も余裕判定へ含める
        float outwardSpeed = Mathf.Sign(lateral) * Vector3.Dot(velocity, roadRight);
        float predictedRoadError = Mathf.Abs(driverLineError) + Mathf.Max(0f, outwardSpeed) * RoadDeparturePredictionSeconds;
        //MTは道路中央ではなく狙うラインから離れる速度で滑り維持の余裕を判断する
        if (IsManualTrackDrive)
        {
            Vector3 lineTangent = Vector3.ProjectOnPlane(RacingPoint(5f) - RacingPoint(-5f), Vector3.up).normalized;
            float lineVelocity = Vector3.Dot(velocity, Vector3.Cross(Vector3.up, lineTangent));
            predictedRoadError = PredictManualLineError(driverLineError, lineVelocity);
        }
        //道路中心から外れた時や滑り過ぎた時はドリフト要求を解除して回復を優先する
        bool allowDrift = IsHighSpeedTrackDrive && vehicle.KPH > 0f && vehicle.GroundedWheelCount >= 3;
        //横ずれや過大な滑りで要求を突然切らず切り返し中の操舵変動を抑える
        float driftRoom = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(DriftFadeStartMeters, DriftFadeEndMeters, predictedRoadError));
        driftRoom *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.5f, 2.5f, roadError));
        driftRoom *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(18f, 25f, Mathf.Abs(actualSlip)));
        float speedBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(DriverDriftStartKph, DriverDriftFullKph, speed));
        float previewCurve = RoadCurvature(Mathf.Clamp(speed / 3.6f * DriverDriftPreviewSeconds, 5f, 12f));
        //同じ方向のカーブだけ先読みし切り返し前に逆方向のドリフトを始めない
        float driftCurve = curvature * previewCurve >= 0f && Mathf.Abs(previewCurve) > Mathf.Abs(curvature) ? previewCurve : curvature;
        float driftAngle = Mathf.Lerp(MaximumDriftDegrees, HighSpeedDriftDegrees, Mathf.InverseLerp(80f, 110f, speed));
        //MTでは高速でも小さな横滑り角を残し車体だけの首振りにならないよう狙う
        if (IsManualTrackDrive)
        {
            //狭い急カーブでは横滑りの張り出しを減らし95km/h以上の通過余地を残す
            float narrow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.03f, 0.045f, Mathf.Abs(driftCurve)));
            driftAngle = Mathf.Lerp(ManualDriftDegrees, ManualDriftReleaseDegrees, narrow);
        }
        float slipTarget = allowDrift ? -Mathf.Sign(driftCurve) * driftAngle * speedBlend
            * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(DriverDriftCurveStart, DriverDriftCurveFull, Mathf.Abs(driftCurve))) * driftRoom : 0f;
        desiredSlip = Mathf.MoveTowards(desiredSlip, slipTarget, 24f * deltaTime);
        //車体の向きだけでなく移動方向を道路の先へ戻して外へ膨らむ誤差を抑える
        //車体基準の進路追従を維持して逆舵だけで外側へ流れ続ける状態を防ぐ
        //横滑りで外側へ進む時だけ速度方向も参照し車体が内向きでも回復操舵を失わない
        float travelWeight = Mathf.InverseLerp(0.75f, 2f, roadError) * Mathf.InverseLerp(0f, 2f, outwardSpeed);
        Vector3 pursuitForward = Vector3.Slerp(forward, travel, travelWeight * TravelRecoveryBlend).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, pursuitForward);
        float pursuit = Mathf.Atan2(2f * DriverWheelbase * Vector3.Dot(offset, right), Mathf.Max(1f, offset.sqrMagnitude)) * Mathf.Rad2Deg;
        //MTの高速急カーブだけ曲率から必要な舵を先に補い後から大きく切り足す操作を減らす
        if (IsManualTrackDrive) { pursuit = CalculateManualPursuit(pursuit, curvature); }
        //必要な旋回率を追従角から求めてカーブの先読みと横滑り補正を二重に加えない
        float yawTarget = velocity.magnitude * Mathf.Tan(pursuit * Mathf.Deg2Rad) / DriverWheelbase;
        //進入後は車体が内側を向いただけで逆旋回を要求せず移動方向から旋回を維持する
        bool maintainingDrift = TrackDrivePreferences.GetBool(HairpinTestKey, false) && hairpinStarted >= 0f
            && Time.time - hairpinStarted < HairpinSustainSeconds && Mathf.Abs(driverLineError) < 1.2f;
        //MTの滑り維持は短い後輪制動の終了と切り離し同じカーブの間は追従を続ける
        if (IsManualTrackDrive)
        {
            maintainingDrift = allowDrift && Mathf.Abs(desiredSlip) > 3f && Mathf.Abs(driverLineError) < 1.2f;
        }
        driftPursuitBlend = Mathf.MoveTowards(driftPursuitBlend, maintainingDrift ? 1f : 0f, deltaTime / 0.3f);
        Vector3 travelRight = Vector3.Cross(Vector3.up, travel);
        float pathCurvature = 2f * Vector3.Dot(offset, travelRight) / Mathf.Max(1f, offset.sqrMagnitude);
        float driftYaw = CalculateDriftYawTarget(velocity.magnitude, pathCurvature, actualSlip, desiredSlip);
        yawTarget = Mathf.Lerp(yawTarget, driftYaw, driftPursuitBlend);
        float yawError = yawTarget - Vector3.Dot(vehicleBody.angularVelocity, Vector3.up);
        //目標内では横滑りを残し目標を超えた分にだけ追加の逆舵を与える
        //毎秒ラジアンの誤差を車速と軸距から前輪角へ換算し高速の切り返しを減衰させる
        float yawCorrection = Mathf.Atan(DriverWheelbase * yawError / Mathf.Max(5f, velocity.magnitude)) * Mathf.Rad2Deg;
        float predictedSlip = actualSlip + ReadHandlingValue("m_trackSlipRateDegrees") * DriverSlipPredictionSeconds;
        float correction = Mathf.Clamp((predictedSlip - desiredSlip) * 0.15f + yawCorrection * DriverYawFeedbackGain, -5f, 5f);
        float excess = Mathf.Max(0f, Mathf.Abs(actualSlip) - Mathf.Max(3f, Mathf.Abs(desiredSlip)));
        float counter = Mathf.Sign(actualSlip) * excess * DriverCountersteerGain;
        //MTは滑り角補正と追加逆舵を二重に加えず進路の旋回率で回復させる
        if (IsManualTrackDrive)
        {
            correction = Mathf.Clamp((predictedSlip - desiredSlip) * ManualSlipFeedbackGain + yawCorrection * ManualYawFeedbackGain, -5f, 5f);
            counter = 0f;
        }
        return CalibratedInput(pursuit + correction + counter, speed);
    }

    //進入と維持と立ち上がりを分けてATとMTに共通のペダル操作を作る関数
    static void UpdateDriftPedals(float speed, float target)
    {
        driverPhase = 0;
        if (!IsHighSpeedTrackDrive || vehicle.IsPullUp) { return; }
        float slip = Mathf.Abs(vehicle.ESCSlipAngle);
        bool corner = Mathf.Abs(desiredSlip) > 2f;
        //進入前の制動を最優先しドリフト維持のために必要な減速を取り消さない
        if (vehicle.Brake > 0f)
        {
            driverPhase = 1;
            return;
        }
        if (corner)
        {
            driverPhase = slip > 3f ? 3 : 2;
            //目標速度の付近では駆動を残し大きく滑った時だけ踏み増しを抑える
            float maintenance = Mathf.Clamp01(0.55f + (target - speed) * 0.06f);
            float recovery = Mathf.InverseLerp(12f, 18f, slip);
            vehicle.Accel = Mathf.Lerp(Mathf.Max(vehicle.Accel, maintenance), Mathf.Min(vehicle.Accel, 0.25f), recovery);
        }
        else if (slip > 3f)
        {
            //切り返し直後は急な全開で姿勢を崩さず回復に合わせて加速する
            driverPhase = 4;
            vehicle.Accel *= Mathf.Lerp(1f, 0.6f, Mathf.InverseLerp(6f, 18f, slip));
        }
    }

    //車速から次段の回転数を予測しコーナー出口の再加速に適したMT操作を行う関数
    static void UpdateDriverGear(float speed)
    {
        if (leverTest || vehicle.IsPullUp || vehicle.Transmission.Type != Transmission.TransmissionType.Manual) { return; }
        if (vehicle.Transmission.IsGearChanging || Time.time < nextDriverShift || vehicle.ActiveGear < 1) { return; }
        int gear = vehicle.ActiveGear;
        float limit = vehicle.Engine.OverRevRPM;
        float roadRPM = vehicle.Transmission.GetSynchronizedRPM(gear, speed);
        bool braking = vehicle.Brake > 0.05f;
        bool up = !braking && roadRPM > limit * DriverUpshiftRatio && gear < vehicle.Transmission.MaxForwardGear;
        //低い段の予測回転数を使い過回転による強制減速と往復変速を避ける
        float lowerRPM = gear > 1 ? vehicle.Transmission.GetSynchronizedRPM(gear - 1, speed) : limit;
        //制動中は回転が落ち切る前に安全な低い段を選び出口の駆動に備える
        bool cornerDown = braking && lowerRPM < limit * (DriverUpshiftRatio - DriverShiftHysteresis);
        bool down = gear > 1 && (ShouldDriverDownshift(roadRPM, lowerRPM, limit) || cornerDown);
        if (!up && !down) { return; }
        if (vehicle.RequestPaddleShift(up)) { nextDriverShift = Time.time + DriverShiftInterval; }
    }
}
