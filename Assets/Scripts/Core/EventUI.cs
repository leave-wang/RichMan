using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class EventUI : MonoBehaviour
{
    [Header("Panel")]
    public GameObject panel;
    public TMP_Text eventText;

    [Header("Buttons")]
    public Button okButton;
    public Button buyButton;
    public Button skipButton;

    private System.Action onOK;
    private System.Action onBuy;
    private System.Action onSkip;

    private void Awake()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    public void ShowOK(string message, System.Action okAction)
    {
        if (panel != null)
        {
            panel.SetActive(true);
        }

        if (eventText != null)
        {
            eventText.text = message;
        }

        if (okButton != null)
        {
            okButton.gameObject.SetActive(true);
        }

        if (buyButton != null)
        {
            buyButton.gameObject.SetActive(false);
        }

        if (skipButton != null)
        {
            skipButton.gameObject.SetActive(false);
        }

        onOK = okAction;
        onBuy = null;
        onSkip = null;
    }

    public void ShowBuy(string message, System.Action buyAction, System.Action skipAction)
    {
        if (panel != null)
        {
            panel.SetActive(true);
        }

        if (eventText != null)
        {
            eventText.text = message;
        }

        if (okButton != null)
        {
            okButton.gameObject.SetActive(false);
        }

        if (buyButton != null)
        {
            buyButton.gameObject.SetActive(true);
        }

        if (skipButton != null)
        {
            skipButton.gameObject.SetActive(true);
        }

        onOK = null;
        onBuy = buyAction;
        onSkip = skipAction;
    }

    public void OnClickOK()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }

        System.Action callback = onOK;

        onOK = null;
        onBuy = null;
        onSkip = null;

        callback?.Invoke();
    }

    public void OnClickBuy()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }

        System.Action callback = onBuy;

        onOK = null;
        onBuy = null;
        onSkip = null;

        callback?.Invoke();
    }

    public void OnClickSkip()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }

        System.Action callback = onSkip;

        onOK = null;
        onBuy = null;
        onSkip = null;

        callback?.Invoke();
    }

    public void Hide()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }

        onOK = null;
        onBuy = null;
        onSkip = null;
    }
}