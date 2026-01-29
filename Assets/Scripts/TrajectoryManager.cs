using UnityEngine;
using UnityEngine.Splines;


public class TrajectoryManager : MonoBehaviour
{
    [Header("Asignar elementos para interacciones")]
    public Transform Ship; // Poner la nave para conocer su posicion

    [Header("Objetos publicos para lectura")]
    public float radio;
    public float distance = 0;
    public float erroresMax = 3f;   // nmero max de errores antes de aumentar el radio
    public float radioMax = 0.2f;
    public float radioSUM = 0.005f;
    public float erroresActuales = 0;
    public float TotalErrores = -1;
    public TMPro.TextMeshProUGUI debugText; // Para Display Debugs

    // Variables internas
    bool fueraTubo = false; // flag para que solo sume 1 error a la 
    Color Green = new Color(0, 1, 0, 0.5f);
    Color Red = new Color(1f, 0, 0, 0.5f);

    SplineContainer KnotsSpline;
    SplineExtrude splineExtrude;


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
        distance = DistanciaASpline(Ship.position, KnotsSpline); // Medir distancia de la trayectoria vs la pos de la 

        if (distance <= radio)
        { // Verificar si la Ship esta dentro o fuera comparandolo con el radio
            GetComponent<MeshRenderer>().material.color = Green; // dentro verde
            if (fueraTubo)
            {
                fueraTubo = false;
            }
        }
        else
        {
            GetComponent<MeshRenderer>().material.color = Red;   // fuera rojo
            // Gamificar radio dinamico -> si te equivocas mucho aumenta el radio tubo
            if (!fueraTubo)
            {
                erroresActuales++;
                TotalErrores++;
                // Comparar numero si ha tenido 3 errores y si no se ha llegado al radio maximo
                if (erroresActuales >= erroresMax && radio <= radioMax)
                {
                    splineExtrude.Radius += radioSUM;
                    radio = splineExtrude.Radius;
                    splineExtrude.Rebuild();
                    erroresActuales = 0; // resetear 
                }
                fueraTubo = true; // activamos flag para no sumar más hasta que vuelva a entrar
            }
        }
        //debugText.text = "radio: " + radio + ". Errores: " + TotalErrores;
    }

    // Comparar punto del spline mas cercano a la Ship contra la pos de Ship -> minDist
    float DistanciaASpline(Vector3 Ship, SplineContainer spline)
    {
        float pasos = 100f;
        float minDist = Mathf.Infinity;

        for (float i = 0; i <= pasos; i++)
        {
            float t = i / pasos;
            Vector3 puntoSpline = (Vector3)spline.EvaluatePosition(t);

            float distance = Vector3.Distance(Ship, puntoSpline);
            if (distance < minDist) { minDist = distance; }
        }
        return minDist;
    }
}

