using UnityEngine;
using System.Collections;

public static class UIHelper
{
    public static void ShowAndHide(MonoBehaviour monoBehaviour, GameObject uiObject, float seconds)
    {
        if (monoBehaviour != null && uiObject != null)
        {
            monoBehaviour.StartCoroutine(ShowAndHideCoroutine(uiObject, seconds));
        }
    }

    private static IEnumerator ShowAndHideCoroutine(GameObject uiObject, float seconds)
    {
        uiObject.SetActive(true);
        yield return new WaitForSecondsRealtime(seconds);
        uiObject.SetActive(false);
    }
}
