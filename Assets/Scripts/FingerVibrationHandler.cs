using Bhaptics.SDK2;
using Bhaptics.SDK2.Glove;
using UnityEngine;

// Handler de vibración háptica para dedos individuales
public class FingerVibrationHandler : MonoBehaviour
{

    [Tooltip("Escala de intensidad de vibración")]
    public float velocityScale = 1f;

    [Tooltip("Intensidad mínima para activar vibración")]
    public float minimumVelocity = 0.01f;

    public TMPro.TextMeshPro debugText;
    PositionType handPosition;
    bool isVibrating = false;
    public int fingerIndex; // 0=Thumb, 1=Index, 2=Middle, 3=Ring, 4=Pinky
    public bool isLeftHand;

    private string[] fingerNames = { "Pulgar", "Índice", "Medio", "Anular", "Meñique" };

    void Start()
    {
        // Determinar posición de la mano para bHaptics
        handPosition = isLeftHand ? PositionType.GloveL : PositionType.GloveR;
        debugText.text += "Handler inicializado " + fingerNames[fingerIndex] + (isLeftHand ? "Izquierda" : "Derecha");

    }

    void OnCollisionEnter(Collision collision)
    {
        // Ignorar colisiones con otros dedos
        if (collision.gameObject.name.Contains("FingerCollider"))
        {
            return; // No vibrar al tocar otros dedos
        }

        debugText.text += $"\nColisión: " + fingerNames[fingerIndex];   

        // Calcular intensidad basada en velocidad
        float velocity = collision.relativeVelocity.magnitude;

        if (velocity < minimumVelocity)
        {
            debugText.text += "\nVelocidad muy baja "+ velocity +" no vibrar";
            return;
        }

        VibrateFinger(collision.relativeVelocity);
    }

    void OnCollisionStay(Collision collision)
    {
        // Ignorar colisiones con otros dedos
        if (collision.gameObject.name.Contains("FingerCollider")) { return; }

        // Mantener vibración si hay presión
        if (!isVibrating)
        {
            VibrateFinger(collision.relativeVelocity);
        }
    }

    void OnCollisionExit(Collision collision)
    {
        // Ignorar colisiones con otros dedos
        if (collision.gameObject.name.Contains("FingerCollider")) { return;}

        debugText.text += "\nSalida: "+ fingerNames[fingerIndex];
        StopVibration();
    }


    private void VibrateFinger(Vector3 collisionVelocity)
    {
        if (BhapticsPhysicsGlove.Instance == null)
        {
            debugText.text += "BhapticsPhysicsGlove.Instance es nulo";
            return;
        }

        // Escalar velocidad
        Vector3 scaledVelocity = collisionVelocity * velocityScale;
        // Enviar feedback háptico
        BhapticsPhysicsGlove.Instance.SendEnterHaptic(handPosition, fingerIndex, scaledVelocity);
        isVibrating = true;
    }

    // Detiene vibración en el dedo
    private void StopVibration()
    {
        if (BhapticsPhysicsGlove.Instance == null)
        {
            debugText.text += "BhapticsPhysicsGlove.Instance es nulo";
            return;
        }

        BhapticsPhysicsGlove.Instance.SendExitHaptic(handPosition, fingerIndex);
        isVibrating = false;
    }
}