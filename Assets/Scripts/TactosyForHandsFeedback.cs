using Bhaptics.SDK2;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using Meta.XR.MRUtilityKit;


public class TactosyForHandsFeedback : MonoBehaviour
{
    [Header("Asignar elementos para interacciones")]
    public Transform Ship;
    public SplineContainer Trajectory;
    public ShipMovement shipMovementR;
    public ShipMovement shipMovementL;

    // Del spline
    float radio;
    SplineExtrude splineExtrude;

    float distance;
    int[] motors = new int[6] { 100, 100, 100, 100, 100, 100};



    private void Start()
    {
        if (Trajectory != null) 
            splineExtrude = Trajectory.GetComponent<SplineExtrude>();
    }

    private void Update()
    {
        if (Trajectory == null || splineExtrude == null) return;
        radio = splineExtrude.Radius;

        if (Ship == null) return;

        distance = ShipDistanceFromSpline(Ship.position, Trajectory);


        if (distance >= radio)
        {
            // No comenzar a vibrar hasta que el juego comience (alguna mano tome la nave)
            if (shipMovementR.StartGame && shipMovementL.StartGame)
                return;

            // Identificar la mano que colisionó con la nave y solo ejecutar este código en los colliders de la mano activa
            if (shipMovementL.HandGrab == 1)
            {
                // Es la mano izquierda
                SendHaptics(distance, radio, (int)PositionType.ForearmL);

            }
            if (shipMovementR.HandGrab == 2)
            {
                // Es la mano derecha
                SendHaptics(distance, radio, (int)PositionType.ForearmR);
            }
        }
    }

    float ShipDistanceFromSpline(Vector3 shipPos, SplineContainer splineContainer)
    {
        float steps = 100f;
        float minDist = Mathf.Infinity;

        for (float i = 0; i <= steps; i++)
        {
            float t = i / steps;
            float3 p = splineContainer.EvaluatePosition(t);
            Vector3 splineDot = new Vector3(p.x, p.y, p.z);

            float d = Vector3.Distance(shipPos, splineDot);
            if (d < minDist) minDist = d;
        }
        return minDist;
    }

    void SendHaptics(float distanceToSpline, float currentRadius, int hand)
    {
        float maxOutsideForFull = 1f;
        int pulseMs = 100; // pulso de 100 ms

        // Qué tanto se salio del tubo
        float outside = Mathf.Max(0f, distanceToSpline - currentRadius);

        // Normalizar con respecto a maxOutsideForFull ( 10 cm lejos es la intensidad 100)
        float t = Mathf.Clamp01(outside / Mathf.Max(0.0001f, maxOutsideForFull));

        int intensity = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1f, 100f, t)), 1, 100);
        int[] motors = new int[6] { intensity, intensity, intensity, intensity, intensity, intensity };

        // Mandar retroalimentacion haptica
        BhapticsLibrary.PlayMotors(hand, motors, pulseMs);
    }

}

