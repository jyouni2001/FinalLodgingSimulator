using UnityEngine;
using UnityEngine.InputSystem;

public class ShaderTime : MonoBehaviour
{
    [SerializeField] private Renderer m_renderer;
    private void Awake()
    {
        m_renderer = GetComponent<Renderer>();
        
    }

    private void Update()
    {
        m_renderer.sharedMaterial.SetFloat("_UnscaledTime", Time.unscaledTime);
    }
}
