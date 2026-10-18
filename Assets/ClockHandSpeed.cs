
using UnityEngine;

public class ClockRotation : MonoBehaviour
{
    [Header("旋转速度（转/分钟 RPM）")]
    public float RPM = 1f;

    void Update()
    {
        float angle = RPM * 360f / 60f * Time.deltaTime;
        transform.Rotate(0f, 0f, -angle);
    }
}
