using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Prompt : MonoBehaviour
{
    Text _contentText;
    Button _leftBtn;
    Button _rightBtn;
    Action _leftCallback;
    Action _rightCallback;

    private void Awake()
    {
        _contentText = transform.Find("Content/ContentText").GetComponent<Text>();
        _leftBtn = transform.Find("Content/Btns/LeftBtn").GetComponent<Button>();
        _rightBtn = transform.Find("Content/Btns/RightBtn").GetComponent<Button>();
        _leftBtn.onClick.AddListener(OnClickLeftBtn);
        _rightBtn.onClick.AddListener(OnClickRightBtn);
        SetActive(false);
    }

    void OnClickLeftBtn()
    {
        if(_leftCallback != null)
        {
            _leftCallback.Invoke();
        }
        SetActive(false);
    }

    void OnClickRightBtn()
    {
        if (_rightCallback != null)
        {
            _rightCallback.Invoke();
        }
        SetActive(false);
    }

    private void OnDestroy()
    {
        
    }

    private void OnEnable()
    {
        
    }

    private void OnDisable()
    {
        _leftCallback = null;
        _rightCallback = null;
    }

    public void ShowOneBtn(string content, Action btnCallback)
    {
        SetActive(true);
        _contentText.text = content;
        _leftCallback = btnCallback;
        _leftBtn.gameObject.SetActive(true);
        _rightBtn.gameObject.SetActive(false);
    }

    public void ShowTwoBtn(string content, Action leftBtnCallback, Action rightBtnCallback)
    {
        SetActive(true);
        _contentText.text = content;
        _leftCallback = leftBtnCallback;
        _rightCallback = rightBtnCallback;
        _leftBtn.gameObject.SetActive(true);
        _rightBtn.gameObject.SetActive(true);
    }

    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }
}
