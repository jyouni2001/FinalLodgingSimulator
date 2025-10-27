using UnityEngine;
using UnityEngine.UI;

public class EscUI : MonoBehaviour
{
    [SerializeField] private Button m_SaveButton;
    [SerializeField] private Button m_SettingButton;
    [SerializeField] private Button m_ExitButton;

    [SerializeField] private GameObject PreExitUI;
    [SerializeField] private Button m_CheckYesButton;
    [SerializeField] private Button m_CheckNoButton;

    [SerializeField] private GameObject SettingUI;

    [SerializeField] private Button btn_StatisticUI;
    [SerializeField] private GameObject m_StatisticUI;

    [SerializeField] private GameObject m_SaveCheckingUI;
    private void Start()
    {
        if (PreExitUI != null)
        {
            PreExitUI.SetActive(false);
        }
        m_SaveButton.onClick.AddListener(Btn_Save);
        m_SettingButton.onClick.AddListener(Btn_Setting);
        m_ExitButton.onClick.AddListener(Btn_Exit);

        m_CheckYesButton.onClick.AddListener(Btn_CheckedExit);
        m_CheckNoButton.onClick.AddListener(Btn_CheckedNo);
        btn_StatisticUI.onClick.AddListener(Btn_StatisticUI);
    }

    private void Btn_Save()
    {
        SaveManager.Instance.SaveGame();
        UIHelper.ShowAndHide(this, m_SaveCheckingUI, 1f);        
    }

    private void Btn_Setting()
    {
        SettingUI.SetActive(true);
    }

    private void Btn_Exit()
    {
       PreExitUI.SetActive(true);       
    }

    private void Btn_CheckedNo()
    {
        PreExitUI.SetActive(false);
    }

    private void Btn_CheckedExit()
    {
        Application.Quit();
    }

    private void Btn_StatisticUI()
    {
        m_StatisticUI.SetActive(!m_StatisticUI.activeSelf);
    }
}
