using System.Collections;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class DisplaySectorAnimation : MonoBehaviour
{
    private Image _image = null;
    private TextMeshProUGUI _tmp = null;

    [SerializeField]
    private float _flashingCycle = 1;
    [SerializeField]
    private float _flashingTime = 1;

    [SerializeField]
    private Vector3 _toPos = Vector3.zero;
    [SerializeField]
    private Vector3 _imageToScale = new Vector3(1f, 1f, 1f); //移動後のImageのScale
    [SerializeField]
    private Vector3 _textToScale = new Vector3(1f, 1f, 1f); //移動後のTextのScale
    [SerializeField, Range(0f, 1f)]
    private float _toAlpha = 1f; //移動後のImage/Text共通の透明度
    [SerializeField]
    private float _animationSpeed = 1;

    private float _timeRate = 0;

    private int animationState = 0;

    private bool _isAnimation = false;
    void Start()
    {
        _image = GetComponentInChildren<Image>();
        _tmp = GetComponentInChildren<TextMeshProUGUI>();


        Color color = new Color(1f, 1f, 1f, 0f);
        _image.color = color;
        _tmp.color = color;
    }

    public void AnimationStart()
    {
        _isAnimation = true;
    }

    private void Update()
    {
        if (_isAnimation == true)
        {
            UpdateAnimation();
        }
    }

    private void UpdateAnimation()
    {
        switch (animationState)
        {
            case 0:

                _timeRate += Time.deltaTime;

                float repeatValue = Mathf.Repeat(_timeRate, _flashingCycle);

                SetAlpha(repeatValue >= _flashingCycle * 0.5f ? 1f : 0f);

                if (_timeRate >= _flashingTime)
                {
                    SetAlpha(1f);
                    _timeRate = 0;
                    animationState = 1;
                }

                break;
            case 1:
                _timeRate += _animationSpeed * Time.deltaTime;

                transform.localPosition = Vector3.Lerp(transform.localPosition, _toPos, _timeRate);
                _image.rectTransform.localScale = Vector3.Lerp(_image.rectTransform.localScale, _imageToScale, _timeRate);
                _tmp.rectTransform.localScale = Vector3.Lerp(_tmp.rectTransform.localScale, _textToScale, _timeRate);
                SetAlpha(Mathf.Lerp(_image.color.a, _toAlpha, _timeRate));

                if (_timeRate >= 1)
                {
                    transform.localPosition = _toPos;
                    _image.rectTransform.localScale = _imageToScale;
                    _tmp.rectTransform.localScale = _textToScale;
                    SetAlpha(_toAlpha);
                    _timeRate = 0;
                    animationState = -1;
                }
                break;

            case -1:
                _isAnimation = false;
                break;
        }
    }

    /// <summary>
    /// ImageとTextのアルファ値をまとめて設定する関数
    /// </summary>
    /// <param name="_alpha">設定するアルファ値(0〜1)</param>
    private void SetAlpha(float _alpha)
    {
        Color imageColor = _image.color;
        imageColor.a = _alpha;
        _image.color = imageColor;

        Color textColor = _tmp.color;
        textColor.a = _alpha;
        _tmp.color = textColor;
    }

    /// <summary>
    /// Playせずに、Editor上で終了位置(_toPos/_imageToScale/_textToScale/_toAlpha)へ即座に移動して見た目を確認する関数
    /// </summary>
    [ContextMenu("Editor: 終了位置をプレビュー")]
    private void PreviewEndPose()
    {
        transform.localPosition = _toPos;

        Image previewImage = GetComponentInChildren<Image>(true);
        TextMeshProUGUI previewText = GetComponentInChildren<TextMeshProUGUI>(true);
        if (previewImage != null)
        {
            Color imageColor = previewImage.color;
            imageColor.a = _toAlpha;
            previewImage.color = imageColor;
            previewImage.rectTransform.localScale = _imageToScale;
        }
        if (previewText != null)
        {
            Color textColor = previewText.color;
            textColor.a = _toAlpha;
            previewText.color = textColor;
            previewText.rectTransform.localScale = _textToScale;
        }
    }

    /// <summary>
    /// Scene上でドラッグして決めた現在のImage/TextのTransform/アルファ値を、終了状態(_toPos/_imageToScale/_textToScale/_toAlpha)として保存する関数
    /// </summary>
    [ContextMenu("Editor: 現在位置を終了位置として保存")]
    private void SaveCurrentAsEndPose()
    {
        _toPos = transform.localPosition;

        Image previewImage = GetComponentInChildren<Image>(true);
        TextMeshProUGUI previewText = GetComponentInChildren<TextMeshProUGUI>(true);
        if (previewImage != null)
        {
            _imageToScale = previewImage.rectTransform.localScale;
            _toAlpha = previewImage.color.a;
        }
        if (previewText != null) { _textToScale = previewText.rectTransform.localScale; }
    }
}