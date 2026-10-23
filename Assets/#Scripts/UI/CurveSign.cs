/*------------------------------------------------------------------
* �t�@�C�����FCurveSign
* �T�v�F�]���}�[�N�̃G�t�F�N�g��\��
* �S���ҁF�F�F�N
* �쐬���F07/14
-------------------------------------------------------------------*/
//�X�V����
/*
* 2022/07/14  �F�F�N�@�@�]���}�[�N�̃G�t�F�N�g�̎���
* 2022/07/15  �F�F�N�@�@fade�G�t�F�N�g�폜
* 2022/08/20�@�����a���@Image�ɒǉ����������x�U�蕪��
* 2024/08/06  22cu0219 ��ؗF��@�ϐ����ύX�ARange�̒ǉ�
*/

//-----------------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class CurveSign : MonoBehaviour
{
    //�ϐ��錾--------------------------------------------------------
    [SerializeField] public bool isCollision = false;    //�S�[���̔��菈��
    public Image image;           //Unity��Image��ݒ肷�邽�߂̕ϐ�
    private float FadeAlpha = 0;    //�����x������ϐ�
    private int type;            //1:TurnLeft 2:TurntRight 3:SignGoal

    // �����ǉ�-------------------------------------------------------
    private GameObject signImage;

    [Range(1,3)]
    [SerializeField] private int curveIntensity;    // �J�[�u�̋��� 1:�� 2:�} 3:��
    //----------------------------------------------------------------

    //�֐�����--------------------------------------------------------
    //�v���C���[���S�[����ʂ����ۂɃt���O��true�ɂ���
    private void OnTriggerEnter(Collider collider)
    {
        //��������Collider�̃^�O��Player�Ȃ�t���O��true��
        if (this.tag == "SignTurnLeft")
        {
            type = 1;
            FadeAlpha = 1.0f;
        }
        else if (this.tag == "SignTurnRight")
        {
            type = 2;
            FadeAlpha = 1.0f;
        }
        else if (this.tag == "SignGoal")
        {
            type = 3;
            FadeAlpha = 0.0f;
        }

        if (collider.tag == "Player")
        {
            isCollision = true;

            if (type == 1)
            {
                if (curveIntensity == 1) SoundManager.Instance.PlaySE(SoundManager.SE_Type.Easy_L);
                else if (curveIntensity == 2) SoundManager.Instance.PlaySE(SoundManager.SE_Type.Turn_L);
                else if (curveIntensity == 3) SoundManager.Instance.PlaySE(SoundManager.SE_Type.Acute_L);
            }
            else if (type == 2)
            {
                if (curveIntensity == 1) SoundManager.Instance.PlaySE(SoundManager.SE_Type.Easy_R);
                else if (curveIntensity == 2) SoundManager.Instance.PlaySE(SoundManager.SE_Type.Turn_R);
                else if (curveIntensity == 3) SoundManager.Instance.PlaySE(SoundManager.SE_Type.Acute_R);
            }
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        // �����ǉ�--------------------------------------------
        switch (curveIntensity)
        {
            case 1:
                signImage = GameObject.Find("SignBlue");
                break;

            case 2:
                signImage = GameObject.Find("SignYellow");
                break;

            case 3:
                signImage = GameObject.Find("SignRed");
                break;

            default:
                break;
        }

        // NULL�`�F�b�N���s��
        // �G���[��f���Ă����Ƃ���
        // 2023/07/13
        if (signImage != null)
        {
            image = signImage.GetComponent<Image>();
        }
        //-----------------------------------------------------

        //������
        image.color = new Color(255, 255, 255, FadeAlpha);
    }

    // Update is called once per frame
    void Update()
    {
        if(isCollision && this.gameObject)
        {
           CollisionFlag();
        }
    }
    private void CollisionFlag()
    {
        if (type == 3)        //Goal����
        {
            //�摜��turnright�̏ꍇ�A�\������Ȃ�����turnleft�ɖ߂�܂�
            if (image.gameObject.transform.eulerAngles.y == 180)
            {
                Vector3 newAngle = image.gameObject.transform.eulerAngles;
                newAngle.y = 180;
                image.gameObject.transform.eulerAngles -= newAngle;
            }
            image.color = new Color(255, 255, 255, FadeAlpha);
            Destroy(this.gameObject);
        }
        else
        {
            if (type == 2)
            {
                // ���E�؂�ւ�
                Vector3 newAngle = image.gameObject.transform.eulerAngles;
                newAngle.y = 180;
                image.gameObject.transform.eulerAngles += newAngle;
            }

            //�摜�̐F�𔒂ɂ��ă��l��FadeAlpha�ŊǗ�
            image.color = new Color(255, 255, 255, FadeAlpha);

            Destroy(this.gameObject);
        }
    }
}
