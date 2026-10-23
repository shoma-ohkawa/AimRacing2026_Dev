using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms;

using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace AIM
{
    public class GarageGlitchTitle : MonoBehaviour
    {
        //グリッチコントローラー
        public Volume m_Volume;
        VolumeProfile profile;

        public GameObject obj;
        //タイマー
        private float timeCnt = 0.0f;

        // Start is called before the first frame update
        void Start()
        {
            timeCnt = 0.0f;
            obj.SetActive(true);

            // オーバーライドされたグリッジボリュームを取得
            profile = m_Volume.sharedProfile;
            if (!profile.TryGet<IE.RichFX.Glitch>(out var glitch))
            {
                glitch = profile.Add<IE.RichFX.Glitch>(false);
            }
            glitch.block.value = 0f;
            glitch.drift.value = 0f;
            glitch.jitter.value = 0f;
            glitch.jump.value = 0f;
            glitch.shake.value = 0f;
        }

        // Update is called once per frame
        void Update()
        {
            //時間をカウント
            timeCnt++;

            if (600 <= timeCnt && timeCnt <= 603) obj.SetActive(true);
            else if (603 < timeCnt) timeCnt = 0;
            else obj.SetActive(false);

            if (!profile.TryGet<IE.RichFX.Glitch>(out var glitch))
            {
                glitch = profile.Add<IE.RichFX.Glitch>(false);
            }
            if(600 <= timeCnt && timeCnt <= 601)
            {
                //glitch.block.value = 0.5f;
                glitch.drift.value = 0.5f;
                glitch.jitter.value = 0.5f;
            }
            else if (602 <= timeCnt && timeCnt <= 603)
            {
                //glitch.block.value = 1f;
                glitch.drift.value = 1f;
                glitch.jitter.value = 1f;
            }
            else if (603 < timeCnt)
            {
                //glitch.block.value = 0f;
                glitch.drift.value = 0f;
                glitch.jitter.value = 0f;
            }
        }
    }
}