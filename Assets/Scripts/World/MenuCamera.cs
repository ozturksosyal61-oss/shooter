using UnityEngine;

/// <summary>Main-menu camera: slow orbit around a point of the live city, gentle bob.</summary>
public class MenuCamera : MonoBehaviour
{
    public Transform focus;
    public float radius = 26f;
    public float height = 15f;
    public float degreesPerSecond = 4f;
    public float lookHeight = 1.5f;

    float angle = 30f;

    void LateUpdate()
    {
        if (!focus) return;
        angle += degreesPerSecond * Time.unscaledDeltaTime;
        float bob = Mathf.Sin(Time.unscaledTime * 0.25f) * 1.2f;
        Vector3 c = focus.position;
        Vector3 pos = c + Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, height + bob, -radius);
        transform.SetPositionAndRotation(pos, Quaternion.LookRotation(c + Vector3.up * lookHeight - pos));
    }
}
