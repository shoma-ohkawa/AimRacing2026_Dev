using UnityEngine;

//道路中央線から進入位置と内側通過点を作る自動運転専用クラス
public static partial class TrackDriveSession
{
    //道路境界の情報がない場所でも大きく路肩へ寄せない横移動の上限
    const float RacingLineOffsetMeters = 1.15f;
    //車幅と横滑り時の張り出しを含め壁から離す最低距離
    const float RacingLineWallMargin = 1.45f;
    //直線の細かな曲率変化をコーナーとして扱わないしきい値
    const float RacingLineCurveThreshold = 0.004f;
    //コーナー前後で走行位置を滑らかに移す距離
    const float RacingLineTransitionMeters = 30f;
    //短い連続カーブで左右の目標位置が急変しないよう平均する前後距離
    const float RacingLineSmoothingMeters = 20f;
    //道路が曲がり始める前から内側通過へ向けて位置を移す最大距離
    const float RacingLineTurnInLeadMeters = 15f;
    //本番の道路や車体を動かさず操舵目標だけを保持する点列
    static Vector3[] racingLine;
    //道路中央から狙う位置と追従誤差を実走で確認する記録値
    static float driverLineOffset;
    static float driverLineError;

    //外側進入から内側通過を経て外側脱出へ連続的に移す関数
    static float CalculateApexOffset(float progress)
    {
        float t = Mathf.Clamp01(progress);
        //MTはカーブ後半まで内側を保ち出口で早く外壁へ向かわないようにする
        if (IsManualTrackDrive)
        {
            float entry = Mathf.SmoothStep(0f, 1f, t / 0.4f);
            float exit = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1f, t));
            return Mathf.Lerp(Mathf.Lerp(-RacingLineOffsetMeters, RacingLineOffsetMeters, entry), 0f, exit);
        }
        //MTは内側通過点を少し手前へ移しATの通過位置は維持する
        float apex = IsManualTrackDrive ? 0.5f : 0.6f;
        float blend = t <= apex ? Mathf.SmoothStep(0f, 1f, t / apex) : Mathf.SmoothStep(1f, 0f, (t - apex) / (1f - apex));
        return Mathf.Lerp(-RacingLineOffsetMeters, RacingLineOffsetMeters, blend);
    }

    //三点の水平曲率を求め実際に狙うラインの減速計画にも使う関数
    static float CalculatePathCurvature(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = Vector3.ProjectOnPlane(b - a, Vector3.up);
        Vector3 bc = Vector3.ProjectOnPlane(c - b, Vector3.up);
        float denominator = ab.magnitude * bc.magnitude * (ab + bc).magnitude;
        if (denominator < 0.001f) { return 0f; }
        return 2f * Vector3.Dot(Vector3.Cross(ab, bc), Vector3.up) / denominator;
    }

    //道路の曲がる区間ごとに安全余裕を残した内側通過ラインを作る関数
    static void BuildRacingLine()
    {
        racingLine = null;
        driverLineOffset = 0f;
        driverLineError = 0f;
        if (!IsHighSpeedTrackDrive || route == null || route.Length < 3) { return; }
        //走行方向順の距離にそろえて逆向きのコースでも同じ計画を使う
        int count = route.Length;
        float[] distance = new float[count];
        float[] curvature = new float[count];
        float[] offsets = new float[count];
        float[] weights = new float[count];
        int savedNearest = nearest;
        int RouteIndex(int i) => direction > 0 ? i : count - 1 - i;
        for (int i = 0; i < count; i++)
        {
            if (i > 0) { distance[i] = distance[i - 1] + Vector3.Distance(route[RouteIndex(i - 1)], route[RouteIndex(i)]); }
            nearest = RouteIndex(i);
            curvature[i] = RoadCurvature(0f);
        }
        nearest = savedNearest;
        //同じ方向へ曲がる区間をまとめ次の逆カーブの要求と滑らかに混ぜる
        for (int start = 0; start < count - 1; start++)
        {
            if (Mathf.Abs(curvature[start]) < RacingLineCurveThreshold) { continue; }
            float sign = Mathf.Sign(curvature[start]);
            int end = start + 1;
            while (end < count - 1 && curvature[end] * sign >= RacingLineCurveThreshold) { end++; }
            float length = distance[end] - distance[start];
            if (length < 8f) { start = end; continue; }
            int first = start;
            int last = end;
            while (first > 0 && distance[start] - distance[first] < RacingLineTransitionMeters) { first--; }
            while (last < count - 1 && distance[last] - distance[end] < RacingLineTransitionMeters) { last++; }
            for (int i = first; i <= last; i++)
            {
                //短いカーブでは先行距離を抑え隣のカーブを横切らない
                float turnInLead = Mathf.Min(RacingLineTurnInLeadMeters, length * 0.3f);
                float progress = (distance[i] - distance[start] + turnInLead) / (length + turnInLead);
                float weight = progress < 0f ? Mathf.SmoothStep(0f, 1f, 1f + (distance[i] - distance[start]) / RacingLineTransitionMeters)
                    : progress > 1f ? Mathf.SmoothStep(1f, 0f, (distance[i] - distance[end]) / RacingLineTransitionMeters) : 1f;
                offsets[i] += sign * CalculateApexOffset(progress) * weight;
                weights[i] += weight;
            }
            start = end - 1;
        }
        //点の個数ではなく距離で重みを付けスプラインの点密度に影響されないようにする
        float[] smoothed = new float[count];
        for (int i = 0; i < count; i++)
        {
            int first = i;
            int last = i;
            while (first > 0 && distance[i] - distance[first] < RacingLineSmoothingMeters) { first--; }
            while (last < count - 1 && distance[last] - distance[i] < RacingLineSmoothingMeters) { last++; }
            float totalWeight = 0f;
            for (int j = first; j <= last; j++)
            {
                float span = distance[Mathf.Min(count - 1, j + 1)] - distance[Mathf.Max(0, j - 1)];
                float weight = Mathf.Max(0f, 1f - Mathf.Abs(distance[j] - distance[i]) / RacingLineSmoothingMeters) * span;
                smoothed[i] += offsets[j] / Mathf.Max(1f, weights[j]) * weight;
                totalWeight += weight;
            }
            smoothed[i] /= Mathf.Max(0.001f, totalWeight);
        }
        racingLine = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            int index = RouteIndex(i);
            Vector3 tangent = route[RouteIndex(Mathf.Min(count - 1, i + 1))] - route[RouteIndex(Mathf.Max(0, i - 1))];
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            float offset = smoothed[i];
            //コースの壁は変更せず目標点だけを壁から遠ざける
            float clearance = RacingLineOffsetMeters;
            foreach (RaycastHit hit in Physics.RaycastAll(route[index] + Vector3.up * 0.7f, right * Mathf.Sign(offset), 3f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.rigidbody != null || Mathf.Abs(hit.normal.y) > 0.5f) { continue; }
                clearance = Mathf.Min(clearance, Mathf.Max(0f, hit.distance - RacingLineWallMargin));
            }
            racingLine[index] = route[index] + right * Mathf.Clamp(offset, -clearance, clearance);
        }
    }

    //道路距離の基準を維持して走行ライン上の先読み目標を取得する関数
    static Vector3 RacingPoint(float distance)
    {
        if (racingLine == null) { return RoutePoint(distance); }
        distance += routeProgressMeters;
        int index = nearest;
        int step = distance < 0f ? -direction : direction;
        float remaining = Mathf.Abs(distance);
        while (index + step >= 0 && index + step < route.Length)
        {
            float length = Vector3.Distance(route[index], route[index + step]);
            if (length > 0f && remaining <= length) { return Vector3.Lerp(racingLine[index], racingLine[index + step], remaining / length); }
            remaining -= length;
            index += step;
        }
        return racingLine[index];
    }
}
