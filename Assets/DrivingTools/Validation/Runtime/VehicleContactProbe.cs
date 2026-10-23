using UnityEngine;

//実走検証中だけ車体へ追加して壁への接触回数と衝撃力を記録する補助コンポーネント
public sealed class VehicleContactProbe : MonoBehaviour
{
    //道路との接地を除いた壁への新しい接触回数
    public int WallContacts { get; private set; }
    //壁との接触で受けた力積の最大値
    public float WallImpulse { get; private set; }

    //接触面が縦向きの場合だけ壁への接触として記録する関数
    private void OnCollisionEnter(Collision _collision)
    {
        for (int i = 0; i < _collision.contactCount; i++)
        {
            //路面や緩い斜面への接地を壁衝突と取り違えないため法線を判定する
            if (Mathf.Abs(_collision.GetContact(i).normal.y) >= 0.5f) { continue; }
            WallContacts++;
            WallImpulse = Mathf.Max(WallImpulse, _collision.impulse.magnitude);
            Debug.Log($"[Sep11Contact] collider={_collision.collider.name} impulse={_collision.impulse.magnitude:F2} position={transform.position}");
            //壁の名前だけで原因を断定せず各タイヤが実際に踏んでいる面も記録する
            LogSuspensionContact(_collision.GetContact(i));
            return;
        }
    }

    //車体の接触面と四輪の接地面を記録して地形とサスペンションを切り分ける関数
    private void LogSuspensionContact(ContactPoint _contact)
    {
        Debug.Log($"[Sep14SuspensionContact] bodyPoint={_contact.point} bodyNormal={_contact.normal}");
        //衝突やばねへ力を加えず現在の接地情報だけを読む
        foreach (WheelCollider wheel in GetComponentsInChildren<WheelCollider>())
        {
            bool grounded = wheel.GetGroundHit(out WheelHit hit);
            string groundName = grounded && hit.collider != null ? hit.collider.name : "接地なし";
            wheel.GetWorldPose(out Vector3 position, out Quaternion rotation);
            Debug.Log($"[Sep14SuspensionContact] wheel={wheel.name} ground={groundName} point={hit.point} normal={hit.normal}"
                + $" force={hit.force:F1} wheelCenter={position} sprungMass={wheel.sprungMass:F1}");
        }
    }
}
