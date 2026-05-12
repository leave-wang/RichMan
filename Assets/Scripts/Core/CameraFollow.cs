using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Vector3 offset = new Vector3(0, 12, -8);
    public float smoothSpeed = 4f;

    private Transform target;

    public void SetTarget(Transform t)
    {
        target = t;
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPos = target.position + offset;

        transform.position = Vector3.Lerp(
            transform.position, desiredPos, smoothSpeed * Time.deltaTime);

        transform.LookAt(target.position);
    }
}