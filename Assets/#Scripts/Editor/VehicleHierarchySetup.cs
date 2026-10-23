using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

//車両の駆動部品をHierarchyへ作成して参照を保存するクラス
public static class VehicleHierarchySetup
{
    const string MapScenePath = "Assets/!Scenes/Map.unity";

    //メニューから現在開いている車両へ駆動部品を設定する関数
    [MenuItem("Tools/Vehicle/Setup Vehicle Parts")]
    public static void SetupOpenScene()
    {
        VehicleController vehicleController = UnityEngine.Object.FindFirstObjectByType<VehicleController>(FindObjectsInactive.Include);
        if (vehicleController == null) { throw new InvalidOperationException("VehicleControllerが現在のSceneにありません。"); }
        SetupVehicleParts(vehicleController);
        EditorSceneManager.MarkSceneDirty(vehicleController.gameObject.scene);
        EditorSceneManager.SaveScene(vehicleController.gameObject.scene);
        Debug.Log("[VehicleSetup] 車両部品のHierarchyと参照を保存しました。");
    }

    //コマンド検証時にMapを開いて車両部品を設定する関数
    public static void SetupMapSceneForBatch()
    {
        Scene mapScene = EditorSceneManager.OpenScene(MapScenePath, OpenSceneMode.Single);
        VehicleController vehicleController = UnityEngine.Object.FindFirstObjectByType<VehicleController>(FindObjectsInactive.Include);
        if (vehicleController == null) { throw new InvalidOperationException("Map.unityにVehicleControllerがありません。"); }
        SetupVehicleParts(vehicleController);
        EditorSceneManager.MarkSceneDirty(mapScene);
        if (!EditorSceneManager.SaveScene(mapScene)) { throw new InvalidOperationException("Map.unityを保存できませんでした。"); }
        AssetDatabase.SaveAssets();
        Debug.Log("[VehicleSetup] Map.unityへ車両部品のHierarchyと参照を保存しました。");
    }

    //車両配下へ各部品を作成してVehicleControllerとWheelControllerへ割り当てる関数
    static void SetupVehicleParts(VehicleController vehicleController)
    {
        Transform partsRoot = FindOrCreateChild(vehicleController.transform, "VehicleParts");
        Car_Engine engine = FindOrCreateComponent<Car_Engine>(partsRoot, "Engine");
        Clutch clutch = FindOrCreateComponent<Clutch>(partsRoot, "Clutch");
        Transmission transmission = FindOrCreateComponent<Transmission>(partsRoot, "Transmission");
        Differential differential = FindOrCreateComponent<Differential>(partsRoot, "Differential");
        Brake brake = FindOrCreateComponent<Brake>(partsRoot, "Brake");
        Steering steering = FindOrCreateComponent<Steering>(partsRoot, "Steering");
        WheelController2026 wheelController = vehicleController.GetComponentInChildren<WheelController2026>(true);
        if (wheelController == null) { throw new InvalidOperationException("車両配下にWheelController2026がありません。"); }

        SerializedObject vehicleObject = new SerializedObject(vehicleController);
        vehicleObject.FindProperty("m_wheelController").objectReferenceValue = wheelController;
        vehicleObject.FindProperty("m_engine").objectReferenceValue = engine;
        vehicleObject.FindProperty("m_clutch").objectReferenceValue = clutch;
        vehicleObject.FindProperty("m_mission").objectReferenceValue = transmission;
        vehicleObject.FindProperty("m_differential").objectReferenceValue = differential;
        vehicleObject.FindProperty("m_brake").objectReferenceValue = brake;
        vehicleObject.FindProperty("m_steering").objectReferenceValue = steering;
        vehicleObject.FindProperty("m_sideHitMaxBounceSpeed").floatValue = 0.15f;
        vehicleObject.FindProperty("m_maxDepenetrationVelocity").floatValue = 0.5f;
        vehicleObject.FindProperty("m_glancingImpactTangentialRedirect").floatValue = 0.12f;
        vehicleObject.FindProperty("m_directImpactTangentialRedirect").floatValue = 0.04f;
        vehicleObject.FindProperty("m_sideHitMinSpeedRatio").floatValue = 0.18f;
        vehicleObject.FindProperty("m_collisionGameplayAssist").floatValue = 0.65f;
        vehicleObject.FindProperty("m_offCenterSpeedRatio").floatValue = 0.3f;
        vehicleObject.FindProperty("m_fullAssistSideOffset").floatValue = 0.9f;
        vehicleObject.FindProperty("m_frontalStopIncidence").floatValue = 0.985f;
        vehicleObject.FindProperty("m_frontStopMaxSideOffset").floatValue = 0.35f;
        vehicleObject.FindProperty("m_frontStopNormalThreshold").floatValue = 0.9f;
        vehicleObject.FindProperty("m_wallStuckRecoveryDelay").floatValue = 0.2f;
        vehicleObject.FindProperty("m_wallStuckMaximumSpeedKph").floatValue = 5f;
        vehicleObject.FindProperty("m_wallStuckPositionNudge").floatValue = 0.05f;
        vehicleObject.FindProperty("m_wallStuckReleaseSpeedKph").floatValue = 5f;
        vehicleObject.FindProperty("m_wallStuckOutwardAcceleration").floatValue = 0.5f;
        vehicleObject.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject wheelObject = new SerializedObject(wheelController);
        wheelObject.FindProperty("m_differential").objectReferenceValue = differential;
        wheelObject.FindProperty("m_brake").objectReferenceValue = brake;
        wheelObject.FindProperty("m_steering").objectReferenceValue = steering;
        wheelObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(vehicleController);
        EditorUtility.SetDirty(wheelController);
    }

    //指定した名前の子を取得し存在しない場合だけ作成する関数
    static Transform FindOrCreateChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null) { return child; }
        GameObject childObject = new GameObject(childName);
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    //指定した子へ駆動部品コンポーネントを一つだけ作成する関数
    static T FindOrCreateComponent<T>(Transform partsRoot, string objectName) where T : Component
    {
        Transform child = FindOrCreateChild(partsRoot, objectName);
        T component = child.GetComponent<T>();
        if (component == null) { component = child.gameObject.AddComponent<T>(); }
        return component;
    }
}
