using UnityEngine;
using UnityEngine.UI;

public class InitializeFirstButton : MonoBehaviour
{
    [SerializeField] private Button m_WallButton;
    [SerializeField] private Button m_FloorButton;
    [SerializeField] private Button m_FurnitureButton;
    [SerializeField] private Button m_DecoButton;

    void Start()
    {
        if (m_FurnitureButton != null && m_FurnitureButton.transition == Selectable.Transition.SpriteSwap)
        {
            // WallButton을 Pressed 상태로 초기화
            m_FurnitureButton.image.sprite = m_FurnitureButton.spriteState.pressedSprite;
        }

        // 다른 버튼 클릭 시 WallButton을 Normal로 되돌리는 이벤트 설정
        m_FloorButton.onClick.AddListener(ResetWallToNormal);
        m_WallButton.onClick.AddListener(ResetWallToNormal);
        m_DecoButton.onClick.AddListener(ResetWallToNormal);
    }

    private void ResetWallToNormal()
    {
        if (m_FurnitureButton != null && m_FurnitureButton.transition == Selectable.Transition.SpriteSwap)
        {
            m_FurnitureButton.image.sprite = m_FurnitureButton.spriteState.disabledSprite;
        }
    }
}
