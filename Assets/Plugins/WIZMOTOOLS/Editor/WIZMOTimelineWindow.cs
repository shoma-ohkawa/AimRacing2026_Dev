using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Timeline;


public class WIZMOTimelineWindow : EditorWindow
{
    EditorWindow getWindow;
    static Rect rect = new Rect(10, 10, 100, 100);

    // Use this for initialization
    void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    //MenuItem については 第8章「MenuItem」 をご覧ください。
    [MenuItem("Window/Example")]
    static void Open()
    {
        //var exampleWindow = CreateInstance<SIMVRTimelineWindow>();
        //exampleWindow.Show();
        // GetWindow<timel>();
        var asm = System.Reflection.Assembly.Load("UnityEditor.Timeline");
        var typeLightingWindow = asm.GetType("UnityEditor.Timeline.TimelineWindow");


 

        var getWindow = EditorWindow.GetWindow(typeLightingWindow);
        //Debug.Log(typeLightingWindow);
        //GetWindow<UnityEditor.Timeline.TimelineWindow>();
        // getWindow.


    }

   

    void OnGUI()
    {
        getWindow.BeginWindows();
        rect = GUI.Window(1, rect, WindowCallback, "window");
        getWindow.EndWindows();

        var ev = Event.current;
        if (ev.type == EventType.ContextClick)
        {
            // MyEditorWindowの背景でマウスを右クリックするとここに来る。 
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("MyEditorWindow.OnGUI()"), false, MenuClicked);
            menu.ShowAsContext();
            ev.Use();
        }
    }

    static void WindowCallback(int id)
    {
        var ev = Event.current;
        if (ev.type == EventType.MouseDown && ev.button == 1)
        {
            // MyEditorWindow内のPopup Window上でマウスを右クリックするとここに来る。
            //var menu = new GenericMenu();
            //menu.AddItem(new GUIContent("MyEditorWindow.WindowCallback()"), false, MenuClicked);
            //menu.ShowAsContext();
            //ev.Use();
        }

        Debug.Log("yahoo!");
    }

    void MenuClicked()
    {
    }
}
