using Bhaptics.SDK2;
using Bhaptics.SDK2.Glove;
using Oculus.Interaction;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class FingerHapticFeedback : MonoBehaviour
{
    [Header("Elementos para interacciones (asignados en FollowHand.cs)")]
    public SplineContainer trajectorySpline; // Spline de la trayectoria
    public ShipMovement shipMovementR; // Script para obtener flag de que ya tomo la nave (comenzó el juego)
    public ShipMovement shipMovementL;
    public TMPro.TextMeshProUGUI debugText; // Para logs
    public int fingerIndex; // Configuración del Dedo
                            // 0=Thumb, 1=Index, 2=Middle, 3=Ring, 4=Pinky
    public bool isLeftHand;

    // -- Variables para uso interno --
    PositionType handPosition;
    SplineExtrude splineExtrude;
    Vector3 distanceVectorFromSpline;
    float distanceFromSpline;
    float fingerMargin = 1.5f;
    bool isVibrating = false;

    // Configuración del render visual (quitar despues)
    float lineWidth = 0.005f; 
    LineRenderer lineR; 

    void Start()
    {
        handPosition = isLeftHand ? PositionType.GloveL : PositionType.GloveR;

        if (trajectorySpline == null)
        {
            Log($"{gameObject.name}: trajectorySpline no asignado");
            return;
        }
        
        SetupLineRenderer();
    }

    void Update()
    {
        // No comenzar a vibrar hasta que comience el juego (la mano tome la nave)
        if (shipMovementR.StartGame && shipMovementL.StartGame)
            return;

        if (trajectorySpline == null || lineR == null) 
            return;

        if (splineExtrude == null && trajectorySpline != null)
            splineExtrude = trajectorySpline.GetComponent<SplineExtrude>(); // Almacenar el spline 

        float tubeRadius = splineExtrude.Radius;
        float fingerMarginRadius = tubeRadius * fingerMargin;

        // Calcular puntos donde se encuentran
        Vector3 fingerWorldPos = transform.position;
        Vector3 fingerLocalPos = trajectorySpline.transform.InverseTransformPoint(fingerWorldPos);

        distanceVectorFromSpline = CalculateDistanceFromSpline(fingerWorldPos, fingerLocalPos);
        distanceFromSpline = distanceVectorFromSpline.magnitude;

        // Cambio de color si supera el radio
        if (distanceFromSpline > fingerMarginRadius)
        { 
            lineR.material.color = Color.red;
            VibrateFinger(distanceVectorFromSpline);
        }
        else
        {
            lineR.material.color = Color.green;
            StopVibration();
        }
    }

    
    void SetupLineRenderer() // Configuración para que se vea en las Oculus
    {
        lineR = gameObject.GetComponent<LineRenderer>();
        if (lineR == null) lineR = gameObject.AddComponent<LineRenderer>();

        // Configuración básica de la línea
        lineR.startWidth = lineWidth;
        lineR.endWidth = lineWidth;
        lineR.positionCount = 2;
        lineR.useWorldSpace = true;

        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard"); // Fallback
        if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"); // Fallback seguro para líneas

        Material mat = new Material(sh);

        // Ajustes para que la línea brille y se vea sobre todo
        mat.color = Color.green;

        // Asignar material
        lineR.material = mat;

        // Asegurar que no proyecte sombras raras
        lineR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineR.receiveShadows = false;
    }

    Vector3 CalculateDistanceFromSpline(Vector3 fingerWorldPos, Vector3 fingerLocalPos)
    {
        float3 nearestLocal;
        float t;

        SplineUtility.GetNearestPoint(trajectorySpline.Spline, 
            new float3(fingerLocalPos.x, fingerLocalPos.y, fingerLocalPos.z), 
            out nearestLocal, 
            out t);

        Vector3 nearestWorldPos = trajectorySpline.transform.TransformPoint((Vector3)nearestLocal);

        // Calcular distancia real
        Vector3 distanceVector = fingerWorldPos - nearestWorldPos;

        // Actualizar la línea visual
        lineR.SetPosition(0, fingerWorldPos);   // Punto A: Dedo
        lineR.SetPosition(1, nearestWorldPos);  // Punto B: Tubo

        return distanceVector;
    }
    private void VibrateFinger(Vector3 collisionVelocity)
    {
        if (BhapticsPhysicsGlove.Instance == null)
        {
            debugText.text += "\nBhapticsPhysicsGlove.Instance es nulo";
            return;
        }

        // Escalar velocidad
        Vector3 scaledVelocity = collisionVelocity;
        // Enviar feedback háptico
        BhapticsPhysicsGlove.Instance.SendEnterHaptic(handPosition, fingerIndex, scaledVelocity);
        isVibrating = true;
    }

    private void StopVibration() // Detiene vibración en el dedo
    {
        if (BhapticsPhysicsGlove.Instance == null)
        {
            debugText.text += "\nBhapticsPhysicsGlove.Instance es nulo";
            return;
        }

        BhapticsPhysicsGlove.Instance.SendExitHaptic(handPosition, fingerIndex);
        isVibrating = false;
    }
    private void Log(string msg)
    {
        if (debugText != null)
            debugText.text += "\n" + msg;
    }
}