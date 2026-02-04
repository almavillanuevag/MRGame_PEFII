using UnityEngine;
using UnityEngine.Splines;


public class TrajectoryManager : MonoBehaviour
{
    [Header("Asignar elementos para interacciones")]
    public Transform Ship; // Poner la nave para conocer su posicion
    public TMPro.TextMeshProUGUI debugText; // Para Display Debugs


    [Header("Objetos publicos para lectura")]
    public float radio;
    public float distance = 0;
    public float erroresMax = 3f;   // nmero max de errores antes de aumentar el radio
    public float radioMax = 0.2f;
    public float radioSUM = 0.005f;
    public float errors = 0;
    public float TotalError = -1;
    public SplineContainer KnotsSpline;
    public SplineExtrude splineExtrude;

    // Variables internas
    bool OutOfTube = false; // flag para que solo sume 1 error a la 
    Color Green = new Color(0, 1, 0, 0.5f);
    Color Red = new Color(1f, 0, 0, 0.5f);

    


    private void Start()
    {
        // Obtener los puntos del spline
        KnotsSpline = GetComponent<SplineContainer>();

        // Obtener el radio
        splineExtrude = GetComponent<SplineExtrude>();
        radio = splineExtrude.Radius;

    }

    private void Update()
    {
        radio = splineExtrude.Radius; // actualizar el radio por si acaso se cambio en ajustes

        // Retroalimentacion VISUAL -> Color
        distance = ShipDistanceFromSpline(Ship.position, KnotsSpline); // Medir distancia de la trayectoria vs la pos de la 

        if (distance <= radio)
        { // Verificar si la Ship esta dentro o fuera comparandolo con el radio
            GetComponent<MeshRenderer>().material.color = Green; // dentro verde
            if (OutOfTube)
            {
                OutOfTube = false;
            }
        }
        else
        {
            GetComponent<MeshRenderer>().material.color = Red;   // fuera rojo
            // Gamificar radio dinamico -> si te equivocas mucho aumenta el radio tubo
            if (!OutOfTube)
            {
                errors++;
                TotalError++;
                // Comparar numero si ha tenido 3 errores y si no se ha llegado al radio maximo
                if (errors >= erroresMax && radio <= radioMax)
                {
                    splineExtrude.Radius += radioSUM;
                    radio = splineExtrude.Radius;
                    splineExtrude.Rebuild();
                    errors = 0; // resetear 
                }
                OutOfTube = true; // activamos flag para no sumar más hasta que vuelva a entrar
            }
        }
        //debugText.text = "radio: " + radio + ". Errores: " + TotalError;
    }

    // Comparar punto del spline mas cercano a la Ship contra la pos de Ship -> minDist
    float ShipDistanceFromSpline(Vector3 Ship, SplineContainer spline)
    {
        float steps = 100f;
        float minDist = Mathf.Infinity;

        for (float i = 0; i <= steps; i++)
        {
            float t = i / steps;
            Vector3 SplineDot= (Vector3)spline.EvaluatePosition(t);

            float distance = Vector3.Distance(Ship, SplineDot);
            if (distance < minDist) { minDist = distance; }
        }
        return minDist;
    }
}

