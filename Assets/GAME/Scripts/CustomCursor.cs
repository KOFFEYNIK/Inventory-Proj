using UnityEngine;

public class CustomCursor : MonoBehaviour
{
    public Texture2D cursorTexture;
    public Vector2 hotspot = Vector2.zero;
    public CursorMode cursorMode = CursorMode.Auto;
    public bool useCustomCursor = true;

    private void Awake()
    {
        if (useCustomCursor)
            ApplyCursor();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void OnEnable()
    {
        if (useCustomCursor)
            ApplyCursor();
    }

    private void OnDisable()
    {
        ResetCursor();
    }

    private void ApplyCursor()
    {
        Cursor.SetCursor(cursorTexture, hotspot, cursorMode);
    }

    private void ResetCursor()
    {
        Cursor.SetCursor(null, Vector2.zero, cursorMode);
    }
}
