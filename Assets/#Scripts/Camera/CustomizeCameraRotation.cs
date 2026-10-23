using UnityEngine;
using Unity.Cinemachine; // Cinemachine 3.x (Unity 6等)
// ※ Cinemachine 2.x の場合は 「using Cinemachine;」 に変更

[ExecuteAlways]
[SaveDuringPlay]
public class CustomizeCameraRotation : CinemachineExtension
{
    public enum RotationMode
    {
        None,           // スプライン追従のまま変更しない
        Absolute,       // 任意の角度に完全固定
        RelativeOffset  // スプラインの向きに対して角度を加算
    }

    [Header("Pitch (X軸 / 縦回転)")]
    public RotationMode pitchMode = RotationMode.Absolute;
    public float pitchValue = 15f;

    [Header("Yaw (Y軸 / 横回転)")]
    public RotationMode yawMode = RotationMode.RelativeOffset;
    public float yawValue = 0f;

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage == CinemachineCore.Stage.Aim)
        {
            Vector3 euler = state.RawOrientation.eulerAngles;

            // --- Pitch (X軸 / 縦) の制御 ---
            if (pitchMode == RotationMode.Absolute)
            {
                euler.x = pitchValue;
            }
            else if (pitchMode == RotationMode.RelativeOffset)
            {
                euler.x += pitchValue;
            }

            // --- Yaw (Y軸 / 横) の制御 ---
            if (yawMode == RotationMode.Absolute)
            {
                euler.y = yawValue;
            }
            else if (yawMode == RotationMode.RelativeOffset)
            {
                euler.y += yawValue;
            }

            state.RawOrientation = Quaternion.Euler(euler);
        }
    }
}
