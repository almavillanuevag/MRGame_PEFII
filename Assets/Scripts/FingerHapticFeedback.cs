using UnityEngine;
using Bhaptics.SDK2;
using Bhaptics.SDK2.Glove;
using Unity.Mathematics;
using UnityEngine.Splines;

public class FingerHapticFeedback : MonoBehaviour
{
    [Header("Elementos para interacciones (asignados en FollowHand.cs)")]
    public SplineContainer trajectorySpline; // Spline de la trayectoria
    public ShipMovement shipMovementR;
    public ShipMovement shipMovementL;
    public TMPro.TextMeshProUGUI debugText; // Para logs
    public int fingerIndex; // Configuración del Dedo
                            // 0=Thumb, 1=Index, 2=Middle, 3=Ring, 4=Pinky
    public bool isLeftHand;

    [Header("¿Visualizar Render?")]
    // Configuración del render visual
    LineRenderer lineR;
    
    float lineWidth = 0.005f;
    public bool viewRender = true;

    // -- Variables para uso interno --
    PositionType handPosition;
    SplineExtrude splineExtrude;
    Vector3 distanceVectorFromSpline;
    float distanceFromSpline;
    float fingerMargin = 1.2f;

    void Start()
    {
        handPosition = isLeftHand ? PositionType.GloveL : PositionType.GloveR;

        if (trajectorySpline == null)
        {
            Log($"{gameObject.name}: trajectorySpline no asignado");
            return;
        }

        if (viewRender)
            SetupLineRenderer();
    }

    void Update()
    {
        // No comenzar a vibrar hasta que el juego comience (alguna mano tome la nave)
        if (shipMovementR.StartGame && shipMovementL.StartGame)
            return;

        // Identificar la mano que colisionó con la nave y solo ejecutar este código en los colliders de la mano activa
        if (isLeftHand)
        {
            if (shipMovementL.HandGrab != 1)
                return;
        }
        else
        {
            if (shipMovementR.HandGrab != 2)
                return;
        }

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

        // Retroalimentacion haptica si supera el radio
        if (distanceFromSpline > fingerMarginRadius)
        {
            if (viewRender)
                lineR.material.color = Color.red;

            float outside = Mathf.Max(0f, distanceFromSpline - fingerMarginRadius);
            float maxOutside = tubeRadius * 0.6f; // ajusta a tu gusto
            float intensity = Mathf.Clamp01(outside / maxOutside);

            VibrateFinger(distanceVectorFromSpline, intensity);
        }
        else
        {
            if (viewRender)
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
        if (viewRender)
        {
            lineR.SetPosition(0, fingerWorldPos);   // Punto A: Dedo
            lineR.SetPosition(1, nearestWorldPos);  // Punto B: Tubo
        }
        
        return distanceVector;
    }

    public void VibrateFinger(Vector3 distanceVector, float intensity01)
    {
        if (BhapticsPhysicsGlove.Instance == null)
        {
            debugText.text += "\nBhapticsPhysicsGlove.Instance es nulo";
            return;
        }

        intensity01 = Mathf.Clamp01(intensity01);

        // Dirección hacia donde está el error + magnitud controlada (0..1)
        Vector3 scaledVelocity = (distanceVector.sqrMagnitude > 1e-6f)
            ? distanceVector.normalized * intensity01
            : Vector3.zero;

        BhapticsPhysicsGlove.Instance.SendEnterHaptic(handPosition, fingerIndex, scaledVelocity);
    }

    public void StopVibration() // Detiene vibración en el dedo
    {
        if (BhapticsPhysicsGlove.Instance == null)
        {
            debugText.text += "\nBhapticsPhysicsGlove.Instance es nulo";
            return;
        }

        BhapticsPhysicsGlove.Instance.SendExitHaptic(handPosition, fingerIndex);
    }
    private void Log(string msg)
    {
        if (debugText != null)
            debugText.text += "\n" + msg;
    }
}