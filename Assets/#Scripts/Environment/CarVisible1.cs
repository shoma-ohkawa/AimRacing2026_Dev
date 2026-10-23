using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms;

namespace AIM
{
    public class CarVisible1 : MonoBehaviour
    {
        public GameObject obj;
        //タイマー
        private float timeCnt = 0.0f;

        // Start is called before the first frame update
        void Start()
        {
            obj.SetActive(true);
        }

        // Update is called once per frame
        void Update()
        {
            //時間をカウント
            timeCnt++;

            if (timeCnt == 5400) timeCnt = 0;
            else if (timeCnt <= 1800) obj.SetActive(true);
            else if (timeCnt <= 5400) obj.SetActive(false);
            else obj.SetActive(false);
        }
    }
}