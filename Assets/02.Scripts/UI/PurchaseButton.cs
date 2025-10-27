using System;
using System.Text;
using TMPro;
using UnityEngine;

public class PurchaseButton : MonoBehaviour
{
    public StringBuilder sb;
    public TextMeshProUGUI moneyText;

    public static PurchaseButton Instance { get; set; }

    private void Awake()
    {
        sb = new StringBuilder();

        Instance = this;
    }

    // 땅 구매 시 필요한 돈 텍스트 화
    public void ChangeMoneyWhenPurchaseLand(int a)
    {
        switch (a)
        {
            /*case 1:
                sb.Clear();
                sb.Append("1000");
                moneyText.text = sb.ToString();
                break;*/
            case 2:
                sb.Clear();
                sb.Append("3000");
                moneyText.text = sb.ToString();
                break;
            case 3:
                sb.Clear();
                sb.Append("5000");
                moneyText.text = sb.ToString();
                break;
        }
    }
}
