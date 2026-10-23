/**
 * @file    ChairRideOperator.cs
 * @brief   椅子の乗降位置を設定するクラス     
 * @author  22CU0225 豊田達也
 * @date    2024/09/06 作成
 */
using UnityEngine;

/// <summary>
/// newしないとWIZMO制御できず
/// </summary>
[System.Serializable]
public class ChairRideOperator
{
    // 乗車位置
    public void Ride(WIZMOController _controller)
    {
        _controller.accel = 0.1f;
        _controller.speed1_all = 0.1f;
        _controller.roll = 0f;
        _controller.pitch = 0f;
        _controller.yaw = 0f;
        _controller.heave = 0f;
        _controller.sway = 0f;
        _controller.surge = 0f;
    }

    public void Drive(WIZMOController _controller)
	{
		_controller.accel = 0.1f;
		_controller.speed1_all = 0.1f;
		_controller.roll = 0f;
		_controller.pitch = 0f;
		_controller.yaw = 0f;
		_controller.heave = 0.5f;
		_controller.sway = 0f;
		_controller.surge = 0f;
	}

	// 降車位置
	public void RideOff(WIZMOController _controller)
    {
        _controller.accel = 0.1f;
        _controller.speed1_all = 0.1f;
        _controller.roll = 0f;
        _controller.pitch = 0f;
        _controller.yaw = 0.0f;
        // _controller.yaw = 0.0f;
        _controller.heave = 0f;
        _controller.sway = 0f;
        _controller.surge = 0f;
    }
}
