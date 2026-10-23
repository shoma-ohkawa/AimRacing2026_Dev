using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayAudienceSE : MonoBehaviour
{

    //プレイヤーがゴールを通った際にフラグをtrueにする
    private void OnTriggerEnter(Collider collider)
    {
        //当たったColliderのタグがPlayerならフラグをtrueに
        if (collider.tag == "Player")
        {
            SoundManager.Instance.FadeIn3DSE(SoundManager.SE3D_Type.Audience,10);
        }
    }
}
