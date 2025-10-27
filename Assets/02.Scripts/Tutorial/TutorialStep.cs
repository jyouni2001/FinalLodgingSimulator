// TutorialStep.cs
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

// 튜토리얼 완료 조건 타입을 정의합니다.
public enum TutorialTriggerType
{
    Input,          // 마우스 클릭, 키보드 입력 등 단순 입력
    KeyPress,       // 특정 키를 눌렀을 때
    BuildUIOpen,    // 건설 UI가 열렸을 때
    BuildButtonClick, // 건설 UI의 특정 버튼을 눌렀을 때
    PlaceObject,     // 특정 오브젝트를 건설했을 때
    RemoveObject
}

[CreateAssetMenu(fileName = "TutorialStep", menuName = "Tutorial/New Tutorial Step")]
public class TutorialStep : ScriptableObject
{
    [Header("다국어 지원 텍스트")]
    public LocalizedString dialogue; // 다국어 지원 대사

    [Header("완료 조건")]
    public TutorialTriggerType triggerType; // 다음 단계로 넘어갈 조건

    // triggerType에 따라 필요한 추가 데이터
    [Tooltip("KeyPress 조건일 때 필요한 키")]
    public KeyCode requiredKey = KeyCode.None;

    [Tooltip("BuildButtonClick 조건일 때 필요한 버튼 이름")]
    public string requiredButtonName;

    [Header("가이드 UI 애니메이션")]
    [Tooltip("이 단계에서 보여줄 가이드 애니메이션의 이름 (없으면 비워두기)")]
    public string guideAnimationName;

    [Header("추가 액션")]
    [Tooltip("이 단계가 완료될 때 카메라 이동 애니메이션을 실행합니다.")]
    public bool triggerCameraMoveOnComplete = false;
}