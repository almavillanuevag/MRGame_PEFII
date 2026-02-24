using UnityEngine;

public class UIFaceUser : MonoBehaviour
{
    [Header("Asignar cámara")]
    public Transform _cameraTransform;

    void LateUpdate()
    {
        // Rotación continua para que el canvas siempre mire al usuario
        if (_cameraTransform != null)
            FaceUser();
    }

    private void FaceUser()
    {
        if (_cameraTransform == null) return;
        Vector3 direction = _cameraTransform.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f) return;

        // Cara frontal del canvas apunte al usuario
        Quaternion look = Quaternion.LookRotation(-direction, Vector3.up);
        Quaternion pitch = Quaternion.Euler(0f, 0f, 0f);

        transform.rotation = look * pitch;
    }
}
