/**
 * @file	TwoChoice.cs
 * @brief	UIで二択の時に使える機能
 * @author	作成者：不明
 *			更新者：22CU0219 鈴木友也
 */

using UnityEngine;
using UnityEngine.UI;
using System;
using DG.Tweening;

public class TwoChoice : MonoBehaviour
{
	enum ChoiceType
	{ 
		Left = -1,
		None = 0,
		Right = 1,
	}

	//プロパティ───────────────────────────────────────
	[Header("leftアイコン")]
	[SerializeField] Transform leftIcon;
	[Header("rightアイコン")]
	[SerializeField] Transform rightIcon;
	[Header("最小拡大率")]
	[SerializeField] float minSize;
	[Header("最大拡大率")]
	[SerializeField] float maxSize;
	[Header("拡縮にかかる時間")]
	[SerializeField] float scaleDuration = 0.2f;
	[Header("leftアイコンのカラー画像(選択時)")]
	[SerializeField] Sprite leftColorSprite;
	[Header("leftアイコンのモノクロ画像(非選択時)")]
	[SerializeField] Sprite leftMonoSprite;
	[Header("rightアイコンのカラー画像(選択時)")]
	[SerializeField] Sprite rightColorSprite;
	[Header("rightアイコンのモノクロ画像(非選択時)")]
	[SerializeField] Sprite rightMonoSprite;
	[Header("最終確認時の移動先(画面中央等)")]
	[SerializeField] Transform confirmPosition;
	[Header("最終確認時の拡大率")]
	[SerializeField] float confirmSize = 2f;
	[Header("最終確認演出にかかる時間")]
	[SerializeField] float confirmDuration = 0.3f;
	[Header("leftアイコンの説明文")]
	[SerializeField] GameObject leftIconsDescription;
	[Header("rightアイコンの説明文")]
	[SerializeField] GameObject rightIconsDescription;
	[Header("ハンドルアイコン(最終確認中は非選択側と一緒に隠す)")]
	[SerializeField] GameObject handleIcon;
	[Header("デフォルトで選択している方")]
	Image leftImage;
	Image rightImage;

	[Header("leftアイコン内のソース画像(拡大時に非表示にする、AT/MTの子Image)")]
	[SerializeField] Image leftSourceImage;
	[Header("rightアイコン内のソース画像(拡大時に非表示にする、AT/MTの子Image)")]
	[SerializeField] Image rightSourceImage;

	[Header("開始までの待機時間")]
	[SerializeField] float waitTime = 1f;
	float activeTime;

	[Header("「アクセル踏んで」までの時間")]
	[SerializeField] float pushGasTime = 1f;
	float lastSelectTime;

	[SerializeField,ShowInInspector]
	ChoiceType nowChoiceNum = 0;
	ChoiceType prevChoiceNum = 0;

	Vector3 leftOriginalPosition;  //最終確認キャンセル時に戻す元位置
	Vector3 rightOriginalPosition;

	[Header("入場演出(サイズ0→等倍)にかかる時間")]
	[SerializeField] float entranceDuration = 0.2f;
	[Header("最終決定時に一度拡大する倍率")]
	[SerializeField] float exitExpandScale = 3f;
	[Header("最終決定時、上記倍率まで拡大するのにかかる時間")]
	[SerializeField] float exitExpandDuration = 0.2f;
	[Header("拡大後、縮小を始めるまでの保持(Keep)時間")]
	[SerializeField] float exitExpandKeepDuration = 0.5f;

	public void Start()
	{
		nowChoiceNum = 0;       //-1：左 0:未選択　1：右
		prevChoiceNum = 0;
		leftImage = leftIcon.gameObject.GetComponent<Image>();
		rightImage = rightIcon.gameObject.GetComponent<Image>();

		leftOriginalPosition = leftIcon.position;   //最終確認キャンセル時の復帰先として保持
		rightOriginalPosition = rightIcon.position;

		leftIcon.transform.localScale = Vector3.zero;  //拡大アニメーションの開始値(サイズ0)
		rightIcon.transform.localScale = Vector3.zero;
		leftIcon.DOScale(1f, entranceDuration);  //サイズ0→等倍(初期値)へ拡大する、シーン入場演出
		rightIcon.DOScale(1f, entranceDuration);
		leftImage.sprite = leftMonoSprite;   //選択前はどちらもモノクロから始める
		rightImage.sprite = rightMonoSprite;
		leftIconsDescription.SetActive(false);
		rightIconsDescription.SetActive(false);

		// スタート時間保持
		activeTime = Time.time;
	}
	private void Update()
	{ 
		if (Time.time - activeTime > waitTime)
		{
			Choice();
		}
	}

	public void Choice()
	{
		// 選択が切り替わった時だけ見た目を更新する(Updateから毎フレーム呼ばれるためTween多重発火を防ぐ)
		if (prevChoiceNum != nowChoiceNum)
		{
			// 選択番号によって画像を拡大
			switch (nowChoiceNum)
			{
				case ChoiceType.Left:
					//leftを選択中扱いにする分、rightは必ずminSize側・モノクロ側へ連動させる
					leftIcon.DOKill();
					rightIcon.DOKill();
					leftIcon.DOScale(maxSize, scaleDuration);
					rightIcon.DOScale(minSize, scaleDuration);
					leftIconsDescription.SetActive(true);
					rightIconsDescription.SetActive(false);

					leftImage.sprite = leftColorSprite;
					rightImage.sprite = rightMonoSprite;
					break;
				case ChoiceType.Right:
					//rightを選択中扱いにする分、leftは必ずminSize側・モノクロ側へ連動させる
					leftIcon.DOKill();
					rightIcon.DOKill();
					rightIcon.DOScale(maxSize, scaleDuration);
					leftIcon.DOScale(minSize, scaleDuration);
					rightIconsDescription.SetActive(true);
					leftIconsDescription.SetActive(false);

					rightImage.sprite = rightColorSprite;
					leftImage.sprite = leftMonoSprite;
					break;
			}

			// AT/MTのボイスはMenuState側(1回目のアクセル押下時)で再生するためここでは鳴らさない
			SoundManager.Instance.PlaySE(SoundManager.SE_Type.CursorSE);

			prevChoiceNum = nowChoiceNum;
			lastSelectTime = Time.time;
		}


		if(lastSelectTime - Time.time > pushGasTime && nowChoiceNum != 0)
		{
			SoundManager.Instance.PlaySE(SoundManager.SE_Type.PushGasPedal);
			lastSelectTime = Time.time;
		}
	}

	/// <summary>
	/// 選択中のアイコンを最終確認位置(画面中央等)へ移動し、confirmSizeまで拡大します。
	/// 未選択(nowChoiceNum == None)の場合は何もしません。
	/// </summary>
	public void ShowFinalConfirmation()
	{
		if (nowChoiceNum == ChoiceType.None) { return; } //未選択では何もしない

		Transform selectedIcon = (nowChoiceNum == ChoiceType.Left) ? leftIcon : rightIcon;
		Transform otherIcon = (nowChoiceNum == ChoiceType.Left) ? rightIcon : leftIcon;

		if (confirmPosition != null)
		{
			selectedIcon.DOKill();
			selectedIcon.DOScale(confirmSize, confirmDuration);
			selectedIcon.DOMove(confirmPosition.position, confirmDuration);
		}

		otherIcon.DOKill(); //非選択側はTweenを止めてから非表示にする
		otherIcon.gameObject.SetActive(false);

		leftIconsDescription.SetActive(false); //1回目のアクセル確定後は説明文を両方とも非表示にする
		rightIconsDescription.SetActive(false);

		if (handleIcon != null) { handleIcon.SetActive(false); } //非選択側と一緒にハンドルアイコンも隠す
	}

	/// <summary>
	/// 最終確認をキャンセルし、選択自体は維持したまま見た目だけ選択直後の状態へ戻します。
	/// (アイコンを元の位置・サイズへ戻し、非表示にしていた側も再表示する)
	/// </summary>
	public void CancelFinalConfirmation()
	{
		if (nowChoiceNum == ChoiceType.None) { return; } //未選択では何もしない

		Transform selectedIcon = (nowChoiceNum == ChoiceType.Left) ? leftIcon : rightIcon;
		Transform otherIcon = (nowChoiceNum == ChoiceType.Left) ? rightIcon : leftIcon;
		Image otherImage = (nowChoiceNum == ChoiceType.Left) ? rightImage : leftImage;
		Vector3 selectedOriginalPosition = (nowChoiceNum == ChoiceType.Left) ? leftOriginalPosition : rightOriginalPosition;

		// 非表示側はアルファ0から再表示し、瞬時にではなくフェードで戻す
		otherImage.DOKill();
		Color otherColor = otherImage.color;
		otherColor.a = 0f;
		otherImage.color = otherColor;
		otherIcon.gameObject.SetActive(true); //先にアクティブ化してからでないとフェードが反映されない
		otherImage.DOFade(1f, scaleDuration); //拡縮にかかる時間(scaleDuration)を再利用する

		// prevChoiceNumは変更せず、Choice()の再実行(DOKillによる位置Tweenの中断)を避けてここで直接戻す
		selectedIcon.DOKill();
		selectedIcon.DOScale(maxSize, confirmDuration);   //最終確認前の選択中サイズへ戻す
		selectedIcon.DOMove(selectedOriginalPosition, confirmDuration); //最終確認前の位置へ戻す

		GameObject selectedDescription = (nowChoiceNum == ChoiceType.Left) ? leftIconsDescription : rightIconsDescription;
		selectedDescription.SetActive(true); //ShowFinalConfirmationで消した説明文を選択中の側だけ再表示する

		if (handleIcon != null) { handleIcon.SetActive(true); } //非選択側と一緒に隠していたハンドルアイコンも再表示する
	}

	/// <summary>
	/// 最終確認後(2回目のアクセル確定時)に呼び出します。選択中のアイコンのソース画像を非表示にしたうえで、
	/// 一旦exitExpandScale倍まで拡大(exitExpandDuration)し、exitExpandKeepDuration秒だけ保持したのち、
	/// シーン入場時の拡大アニメーション(サイズ0→等倍、entranceDuration)の逆再生としてサイズ0まで縮小します。
	/// </summary>
	public void PlayExitAnimation()
	{
		if (nowChoiceNum == ChoiceType.None) { return; } //未選択では何もしない

		Transform selectedIcon = (nowChoiceNum == ChoiceType.Left) ? leftIcon : rightIcon;
		Image selectedSourceImage = (nowChoiceNum == ChoiceType.Left) ? leftSourceImage : rightSourceImage;

		if (selectedSourceImage != null) { selectedSourceImage.gameObject.SetActive(false); } //拡大開始と同時にAT/MTのソース画像を非表示にする

		selectedIcon.DOKill();
		selectedIcon.DOScale(exitExpandScale, exitExpandDuration)   //まず一度拡大する
			.OnComplete(() => selectedIcon.DOScale(0f, entranceDuration).SetDelay(exitExpandKeepDuration));  //拡大完了後、Keep時間だけ保持してからサイズ0まで縮小する
	}

	//選択結果を返す
	//0...未選択
	//-1...1つ目が選択された
	//1...2つ目が選択された
	public int GetChoice()
	{
		return (int)nowChoiceNum;
	}

	public void SetRight()
	{
		nowChoiceNum = ChoiceType.Right;
	}
	public void SetLeft()
	{
		nowChoiceNum = ChoiceType.Left;
	}
}

