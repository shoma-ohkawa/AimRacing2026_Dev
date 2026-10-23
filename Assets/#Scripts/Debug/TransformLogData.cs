// 車の位置と時間を保存して、記録したとおりに再生するプログラム
// 作成者：23CU0325_土田修平
// 作成日：2025/05/29
// 追記日：2025/06/01 位置・回転・時間を配列に保存するように変更

using UnityEngine;

public class TransformLogData : MonoBehaviour
{
 //   [SerializeField]
 //   private GameObject _car2025;
 //   public int _index = 0;		  // 配列のインデックス

	//private bool isRecording = false;   // リプレイの記録中かどうか

	//// 保存した構造体配列
	//protected struct SavePosAndTime
	//{
	//	public Vector3[] _transform; // 位置
	//	public Vector3[] _rotation;  // 回転
	//	public float[] _timer;       // 時間
	//}
	//// 構造体宣言
	//protected SavePosAndTime savePosAndTime;

 //   // 数フレームに一回記録するようにする
 //   private int frameCounter = 1;

	//void Start()
	//{
	//	savePosAndTime._transform = new Vector3[100000];
	//	savePosAndTime._rotation = new Vector3[100000];
	//	savePosAndTime._timer = new float[100000];
	//}

	//void FixedUpdate()
 //   {
	//	// スペースキーで記録開始する
	//	if (Input.GetKeyDown(KeyCode.Space)) { isRecording = !isRecording; }

	//	// 記録してログに表示
 //       if(isRecording)
	//	{
	//		FrameCounter();
	//	}

	//}

	//// フレームの位置・回転・時間を配列に保存してログに表示する関数
 //   void FrameCounter()
 //   {
	//	// 毎フレームはデータが多くなるので記録回数を減らす
	//	if (frameCounter % 20 == 0)
	//	{
	//		savePosAndTime._transform[_index] = transform.position; // 位置
	//		AppLog.Log("Transform: " + savePosAndTime._transform[_index]);

	//		savePosAndTime._rotation[_index] = transform.rotation.eulerAngles; // 回転
	//		AppLog.Log("Rotation: " + savePosAndTime._rotation[_index]);

	//		savePosAndTime._timer[_index] += Time.time;　// 時間
	//		AppLog.Log("Time:" + savePosAndTime._timer[_index]);
	//	}
	//	frameCounter++;
	//	_index++;
	//}
}