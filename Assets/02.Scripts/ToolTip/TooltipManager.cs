using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager instance { get; private set; }

    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI tooltipText;
    [SerializeField] private TextMeshProUGUI tooltipText2;

    [SerializeField] private RectTransform tooltipRect => tooltipPanel.GetComponent<RectTransform>();

    [SerializeField] private Vector2 offset = new Vector2(15f, 0f);

    private void Awake()
    {
        if(instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(instance);
        }

        if(tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }
    private void Update()
    {
        // 툴팁이 활성화되어 있으면 마우스 위치를 따라다니도록 함
        if (tooltipPanel != null && tooltipPanel.activeSelf)
        {
            Vector2 mousePosition = Input.mousePosition;

            float tooltipWidth = tooltipRect.rect.width;
            float tooltipHeight = tooltipRect.rect.height;

            Vector2 newPosition = mousePosition + offset;

            if (newPosition.x + tooltipWidth > Screen.width)
            {
                // 넘어간 만큼 왼쪽으로 이동시킵니다.
                newPosition.x = Screen.width - tooltipWidth;
            }

            if (newPosition.y - tooltipHeight < 0)
            {
                // 넘어가지 않도록 마우스 위쪽으로 위치를 변경합니다.
                newPosition.y = mousePosition.y + tooltipHeight;
            }

            tooltipRect.position = mousePosition + offset;
        }
    }

    public void ShowTooltip(string content, string content2)
    {
        if (tooltipPanel == null || tooltipText == null) return;

        tooltipText.text = content;
        tooltipText2.text = content2;

        tooltipPanel.SetActive(true);
        // Content Size Fitter가 즉시 적용되도록 강제 업데이트
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);
    }

    public void HideTooltip()
    {
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }
}
