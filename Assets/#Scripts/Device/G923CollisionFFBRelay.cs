using UnityEngine;

/// <summary>
/// Vehicle側の衝突をManager常駐のG923_FFBControllerへ渡すための中継スクリプト。
/// G923_FFBControllerをManagerへ移動した後も、衝突FFBを使うためにVehicleへ付ける。
/// </summary>
public class G923CollisionFFBRelay : MonoBehaviour
{
    [SerializeField]
    private G923_FFBController m_g923Controller;

    private void Awake()
    {
        ResolveController();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (m_g923Controller == null)
        {
            ResolveController();
        }

        if (m_g923Controller == null)
        {
            return;
        }

        m_g923Controller.NotifyVehicleCollision(collision, transform);
    }

    private void ResolveController()
    {
        if (m_g923Controller != null)
        {
            return;
        }

        m_g923Controller = G923_FFBController.Instance;
    }
}
