using UnityEngine;
using System.Collections;
using UnityEditor;
using UnityEditor.Callbacks;
using static WIZMOController;

/// <summary>
/// Ovrvision Custom Editor
/// </summary>
[CustomEditor(typeof(WIZMOController))]
[AddComponentMenu("WIZMO/WIZMO Controller")]
public class WIZMOEditor : Editor
{
	private SerializedProperty[] speedProperty = new SerializedProperty[6];
	private SerializedProperty accelProperty;
	private SerializedProperty rmrProperty;
	private SerializedProperty gmrProperty;

	private SerializedProperty wizmoEventObject;
	private Texture2D _icon = null;
	private SerializedProperty appcodePrivateMode;

	void OnEnable()
	{
		speedProperty[0] = serializedObject.FindProperty("speed1_all");
		speedProperty[1] = serializedObject.FindProperty("speed2");
		speedProperty[2] = serializedObject.FindProperty("speed3");
		speedProperty[3] = serializedObject.FindProperty("speed4");
		speedProperty[4] = serializedObject.FindProperty("speed5");
		speedProperty[5] = serializedObject.FindProperty("speed6");
		accelProperty = serializedObject.FindProperty("accel");
		rmrProperty = serializedObject.FindProperty("rotationMotionRatio");
		gmrProperty = serializedObject.FindProperty("gravityMotionRatio");
		appcodePrivateMode = serializedObject.FindProperty("appcodePrivateMode");
		wizmoEventObject = serializedObject.FindProperty("wizmoSystemEventObject");

		if (_icon == null) _icon = Resources.Load<Texture2D>("wizmo_edicon");
	}

	public override void OnInspectorGUI()
	{
		WIZMOController obj = target as WIZMOController;
		serializedObject.Update();

		if (_icon != null)
		{
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUILayout.Label(new GUIContent(_icon));
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
		}

		EditorGUILayout.LabelField("Application Pass Code");
		if (!obj.isOpened()) {
			EditorGUILayout.BeginHorizontal();
			if(!appcodePrivateMode.boolValue)
				obj.AppPassCode = EditorGUILayout.PasswordField("App Code", obj.AppPassCode);
			else
				obj.AppPassCode = EditorGUILayout.TextField("App Code", obj.AppPassCode);
			appcodePrivateMode.boolValue = EditorGUILayout.Toggle("", appcodePrivateMode.boolValue, new GUILayoutOption[] {GUILayout.Width(20.0f)});
			EditorGUILayout.EndHorizontal();
		}
		else
		{
			if (obj.AppPassCode == "")
				EditorGUILayout.HelpBox("Code : Unlimited Code", MessageType.Info, true);
			else
				if(appcodePrivateMode.boolValue)
					EditorGUILayout.HelpBox("Code : " + obj.AppPassCode, MessageType.Info, true);
				else
					EditorGUILayout.HelpBox("Code : *********", MessageType.Info, true);
		}
		if (!obj.isOpened())
			obj.SerialNumberAssign = EditorGUILayout.TextField("Assign No.", obj.SerialNumberAssign);
		else
		{
			if (obj.SerialNumberAssign != "")
				EditorGUILayout.HelpBox("Assign No. : " + obj.SerialNumberAssign, MessageType.Info, true);
		}

		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Machine Status");

		if (EditorApplication.isPlaying) {
			if (obj.isOpened())
			{
				EditorGUILayout.HelpBox("WIZMO System Opened", MessageType.Info, true);
				if (GUILayout.Button("< Close WIZMO system > "))
					obj.CloseSIMVR();
			}
			else
			{
				EditorGUILayout.HelpBox("WIZMO System Closed", MessageType.Error, true);
				if (GUILayout.Button("< Open the WIZMO system >"))
					obj.OpenSIMVR();
			}
		}else
		{
			EditorGUILayout.HelpBox("UnityEditor is not playing.", MessageType.Info, true);
		}

		EditorGUILayout.Space();

		obj.useOpenAtStart = EditorGUILayout.Toggle("Enable Auto Open", obj.useOpenAtStart);
		obj.axisProcesser = EditorGUILayout.Toggle("Enable Axis Processer", obj.axisProcesser);

		EditorGUILayout.Space();
		if (obj.axisProcesser)
		{
			EditorGUILayout.LabelField("WIZMO Controller");
			obj.roll = EditorGUILayout.Slider("Roll", obj.roll, -1.0f, 1.0f);
			obj.pitch = EditorGUILayout.Slider("Pitch", obj.pitch, -1.0f, 1.0f);
			obj.yaw = EditorGUILayout.Slider("Yaw", obj.yaw, -1.0f, 1.0f);
			obj.heave = EditorGUILayout.Slider("Heave", obj.heave, -1.0f, 1.0f);
			obj.sway = EditorGUILayout.Slider("Sway", obj.sway, -1.0f, 1.0f);
			obj.surge = EditorGUILayout.Slider("Surge", obj.surge, -1.0f, 1.0f);
		}
		else
		{
			EditorGUILayout.LabelField("WIZMO Direct Position");
			obj.axis1 = EditorGUILayout.Slider("Axis 1", obj.axis1, 0.0f, 1.0f);
			obj.axis2 = EditorGUILayout.Slider("Axis 2", obj.axis2, 0.0f, 1.0f);
			obj.axis3 = EditorGUILayout.Slider("Axis 3", obj.axis3, 0.0f, 1.0f);
			obj.axis4 = EditorGUILayout.Slider("Axis 4", obj.axis4, 0.0f, 1.0f);
			obj.axis5 = EditorGUILayout.Slider("Axis 5", obj.axis5, 0.0f, 1.0f);
			obj.axis6 = EditorGUILayout.Slider("Axis 6", obj.axis6, 0.0f, 1.0f);
		}

		obj.isOrigin = EditorGUILayout.Toggle("Enable Origin", obj.isOrigin);

		EditorGUILayout.Space();

		if (GUILayout.Button("Set Default Position", GUILayout.Width(250)))
		{
			obj.roll = 0.0f;
			obj.pitch = 0.0f;
			obj.yaw = 0.0f;
			obj.heave = 0.0f;
			obj.sway = 0.0f;
			obj.surge = 0.0f;
			obj.axis1 = 0.5f;
			obj.axis2 = 0.5f;
			obj.axis3 = 0.5f;
			obj.axis4 = 0.5f;
			obj.axis5 = 0.5f;
			obj.axis6 = 0.5f;
			obj.isOrigin = false;
			obj.fanSpeed = 0;
		}

		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Machine Configures");
		EditorGUILayout.Space();

		obj.speedGainMode = (SpeedGainMode)EditorGUILayout.EnumPopup("Speed Gain Mode", obj.speedGainMode);

		if (obj.speedGainMode == SpeedGainMode.NORMAL)
		{
			speedProperty[0].floatValue = EditorGUILayout.Slider("Speed", speedProperty[0].floatValue, 0.0f, 1.0f);
		}
		else if (obj.speedGainMode == SpeedGainMode.MANUAL)
		{
			speedProperty[0].floatValue = EditorGUILayout.Slider("Speed1", speedProperty[0].floatValue, 0.0f, 1.0f);
			speedProperty[1].floatValue = EditorGUILayout.Slider("Speed2", speedProperty[1].floatValue, 0.0f, 1.0f);
			speedProperty[2].floatValue = EditorGUILayout.Slider("Speed3", speedProperty[2].floatValue, 0.0f, 1.0f);
			speedProperty[3].floatValue = EditorGUILayout.Slider("Speed4", speedProperty[3].floatValue, 0.0f, 1.0f);
			speedProperty[4].floatValue = EditorGUILayout.Slider("Speed5", speedProperty[4].floatValue, 0.0f, 1.0f);
			speedProperty[5].floatValue = EditorGUILayout.Slider("Speed6", speedProperty[5].floatValue, 0.0f, 1.0f);
		}

		EditorGUI.BeginDisabledGroup(obj.speedGainMode == SpeedGainMode.VARIABLE);
		accelProperty.floatValue = EditorGUILayout.Slider("Accelerate", accelProperty.floatValue, 0.0f, 1.0f);
		EditorGUI.EndDisabledGroup();

		EditorGUILayout.Space();
		rmrProperty.floatValue = EditorGUILayout.Slider("Rotation Motion Ratio", rmrProperty.floatValue, 0.0f, 1.0f);
		gmrProperty.floatValue = EditorGUILayout.Slider("Gravity Motion Ratio", gmrProperty.floatValue, 0.0f, 1.0f);

		if (GUILayout.Button("Set Default Configures", GUILayout.Width(250)))
		{
			obj.speedGainMode = SpeedGainMode.NORMAL;

			speedProperty[0].floatValue = 0.667f;
			speedProperty[1].floatValue = 0.667f;
			speedProperty[2].floatValue = 0.667f;
			speedProperty[3].floatValue = 0.667f;
			speedProperty[4].floatValue = 0.667f;
			speedProperty[5].floatValue = 0.667f;
			accelProperty.floatValue = 0.5f;

			rmrProperty.floatValue = 0.8f;
			gmrProperty.floatValue = 0.8f;
		}

		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Machine Error Callback Event");

		EditorGUILayout.PropertyField(wizmoEventObject);

		EditorGUILayout.Space();
		EditorGUILayout.LabelField(" WIZMOTOOLS - WIZAPPLY CO.,LTD. ");

		//changed param
		if (GUI.changed)
		{
			//Todo
			serializedObject.ApplyModifiedProperties();
		}

		EditorUtility.SetDirty(target);	//editor set
	}
}