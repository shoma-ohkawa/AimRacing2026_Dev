/**
 * @file    SimpleSpringJoint.cs
 * @brief   単純なバネ機能の実装
 * @author  22cu0219 鈴木友也
 * @date    2024/05/29  作成
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 単純なバネのデータを管理する構造体
/// </summary>
[System.Serializable]
public struct SimpleSpringJoint
{
    [SerializeField]
    float m_spring;     // ターゲット位置に到達するためのバネ力
    [SerializeField]
    float m_damper;     // バネの振動を減衰させる力(ダンパー力)
    [SerializeField]
    float m_targetPosition; // ジョイント(接続部分)が到達しようとする位置


    public float Spring
    {
        get
        {
            return m_spring;
        }
        set
        {
            // 0以上に補正
            m_spring = Mathf.Max(0, value);
        }
    }
    public float Damper
    {
        get
        {
            return m_damper;
        }
        set
        {
            // 0以上に補正
            m_damper = Mathf.Max(0, value);
        }
    }
    public float TargetPosition
    {
        get
        {
            return m_targetPosition;
        }
        set
        {
            // 0～1の間に補正
            m_targetPosition = Mathf.Clamp01(value);
        }
    }
}
