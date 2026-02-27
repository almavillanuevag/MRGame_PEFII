using Firebase.Extensions;
using Firebase.Firestore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class EndGame : MonoBehaviour
{
    [Header("Asignar elementos para interacciones")]
    public Transform PlanetEndPoint; // Punto final donde se situará la nave
    public TrajectoryManager TrajectoryManager; // Scrpt de la trayectoria para calcular 
    public FollowHand followHand;
    public NewSession newSession;
    

    [Header("Asignar elementos de UI")]
    public GameObject UICanvasWin;
    public GameObject UICanvasLoadingNext;
    public GameObject star1;
    public GameObject star2;
    public GameObject star3;
    public GameObject star4;
    public GameObject star5;

    [Header("Log opcional para pruebas y errores")]
    public TextMeshProUGUI debugText; // Para Display Debugs

    [Header("Objetos publicos para lectura")]
    public float tMin = 50; // variable para stars ** MODIFICABLE
    public float error3 = 6; // variable para stars ** 
    public float stars0;
    public float radio0 = 0f;
    public float TotalErrors0 = 0;
    public bool OutOfTube = false;
    public bool End = false;
    public int sessionNumber;
    public ShipMovement shipMovement;
    public float[] metrics;

    // Tiempos
    public float TimeOut = 0f;
    public float OutBegin = 0;
    public float TotalTime0 = 0f;
    public float BeginningTime = 0f; // time cuando comenzó el juego
    public float InsideTimePercentage0 = 0f;

    // Variables internas
    GameObject ShipGameObject;
    float distance;
    float FinishTime;
    FirebaseFirestore db;
    string IDPx;
    string IDSession;



    private void Start()
    {
        // Inicializar firestore
        Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            var dependencyStatus = task.Result;
            if (dependencyStatus == Firebase.DependencyStatus.Available)
            {
                // Si todo está bien, inicializamos Firestore
                db = FirebaseFirestore.DefaultInstance;
                if (debugText != null) debugText.text += "\nSe inicializó Firestore";
            }
            else
            {
                if (debugText != null) debugText.text += "\nNo se pudo resolver dependencias de Firestore";
            }
        });

        UICanvasWin.SetActive(false);
        UICanvasLoadingNext.SetActive(false);

    }

    private void Update()
    {
        // Calcular time fuera del tubo constantemente para métricas 
        distance = TrajectoryManager.distance;
        radio0 = TrajectoryManager.radio;

        // Verificar si la nave está dentro o fuera
        if (distance <= radio0)
        {
            if (OutOfTube) // Si estaba fuera y ahora adentro
            {
                // Sumar el time que estuvo fuera
                TimeOut += Time.time - OutBegin;
                OutOfTube = false;
            }
        }
        else
        {
            if (!OutOfTube) // Si estaba dentro y ahora está fuera
            {
                TotalErrors0++;
                OutBegin = Time.time; // Marcar cuando salió
                OutOfTube = true;
            }
        }

    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ship"))
        {
            // Posicionar la nave en el punto final y regresarle propiedades fisicas
            shipMovement = other.GetComponentInParent<ShipMovement>();
            shipMovement.ForceRelease();
            SetShipToFinalPosition(other);

            // Eliminar las funciones de retroalimentacion haptica
            followHand.StopHapticFeedbackFunctions();

            // Calcular las metricas de desempeño solo una vez si ya termino
            if (End) return;
            End = true;  // Flag de que ya termino y que no se vuelvan a calcular

            // Obtener el tiempo cuando se colocó la nave en la mano (inicio del juego)
            BeginningTime = shipMovement.BeginningTime;
            // Obtener tiempo en el que llegó al planeta fin
            FinishTime = Time.time;

            // Calcular las metricas
            shipMovement.StopRecordingTrajectory();
            metrics = CalculatePerformanceMetrics();

            // Comenzar la secuencia de finalizar
            StartCoroutine(WinSequence(metrics));
        }
    }

    IEnumerator WinSequence(float[] m)
    {
        // Guardar sesión (async, esperamos su finalización)
        bool savedDone = false;
        SaveSessionToFirestore(m, () => savedDone = true);
        yield return new WaitUntil(() => savedDone);

        // Mostrar UI de victoria con estrellas
        UICanvasWin.SetActive(true);
        DisplayStars();

        // Tiempo proporcional a estrellas: stars*2 + 1
        int stars = (int)m[4];
        float wait = stars * 2f + 1f;
        yield return new WaitForSeconds(wait);

        // Ocultar victoria, mostrar "Cargando siguiente nivel"
        UICanvasLoadingNext.SetActive(true);

        yield return new WaitForSeconds(3f);

        // Ejecutar NewSession para la lógica de progresión 
        newSession.PlayAgain(metrics[3]);
    }
    void SetShipToFinalPosition(Collider other)
    {
        if (debugText != null) debugText.text += "\nAterrizaje";
        ShipGameObject = other.gameObject;

        // Desparentar la nave
        ShipGameObject.transform.SetParent(null);

        // Posicionarlo arriba del planeta
        ShipGameObject.transform.position = PlanetEndPoint.position;
        ShipGameObject.transform.rotation = PlanetEndPoint.rotation;

        // Dejarlo fijo en el planeta: quitarle los colliders
        ShipGameObject.GetComponent<BoxCollider>().enabled = false;
        ShipGameObject.GetComponent<CapsuleCollider>().enabled = false;
        // Regresarle propiedades fisicas
        Rigidbody rb = ShipGameObject.GetComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero; // Detener cualquier velocidad residual
        rb.angularVelocity = Vector3.zero;
    }

    float[] CalculatePerformanceMetrics()
    {
        if (debugText != null) debugText.text += "\nCalculando Metricas...";

        if (OutOfTube) // Cuando que cuando vuelve a jugar se calcule el tiempo de esa partida
        {
            TimeOut += Time.time - OutBegin;
        }
        TotalTime0 = FinishTime - BeginningTime; // Tiempo total desde el inicio
        InsideTimePercentage0 = TotalTime0 > 0 ? ((TotalTime0 - TimeOut) / TotalTime0) * 100 : 0;

        stars0 = 1; // Calcular estrellas ** PENDIENTE A DEFINIR POR EL TERAPEUTA
        if (InsideTimePercentage0 >= tMin)
        {
            stars0++;
            if (TotalErrors0 <= error3)
            {
                stars0++;
                if (InsideTimePercentage0 >= tMin + 20)
                {
                    stars0++;
                    if (TotalErrors0 <= error3 - 3)
                    {
                        stars0++;
                    }
                }
            }
        }
        // Crear arreglo de métricas
        float[] metrics = new float[5];
        metrics[0] = TotalErrors0;
        metrics[1] = TotalTime0;
        metrics[2] = InsideTimePercentage0;
        metrics[3] = radio0;
        metrics[4] = stars0;

        return metrics;
    }

    async void SaveSessionToFirestore(float[] metrics, Action onDone)
    {
        // Validar que tenga acceso a la instancia de SelectPatient
        if (SelectPatient.Instance == null)
        {
            debugText.text += "\nNo hay Instancia de SelectPatient.cs";
            onDone?.Invoke();
            return;
        }
        
        // Acceder a la instancia para obtener el ID del paciente y sesion:
        IDPx = SelectPatient.Instance.IDPx;
        IDSession = SelectPatient.Instance.IDSession;
        sessionNumber = SelectPatient.Instance.CurrentSessionNumber;

        // Buscar errores
        if (db == null || string.IsNullOrEmpty(IDPx))
        {
            if (debugText != null) debugText.text += "\nERROR: Firestore no inicializado";
            onDone?.Invoke();
            return;
        }
        // Configurar la informacion antes de guardarla
        var idTraj = SelectPatient.Instance.IDTraj;

        List<Vector3> patientTrajectory = null;
        patientTrajectory = shipMovement.patientTrajectory;

        // Organizar metricas como diccionario para Firestore
        var data = new Dictionary<string, object>
        {
            { "Trayectoria", idTraj },
            { "DateTime", Timestamp.GetCurrentTimestamp() },
            { "TotalErrors", (int)metrics[0] },
            { "TotalTime", Mathf.Round(metrics[1] * 1000f) / 1000f },
            { "InsideTimePercentage", Mathf.Round(metrics[2] * 1000f) / 1000f },
            { "radio", Mathf.Round(metrics[3] * 1000f) / 1000f},
            { "stars", (int)metrics[4] }
        };

        if (patientTrajectory != null && patientTrajectory.Count > 0)
        {
            var pointsList = new List<Dictionary<string, object>>(patientTrajectory.Count);
            foreach (var p in patientTrajectory)
            {
                pointsList.Add(new Dictionary<string, object>
                {
                    { "x", p.x },
                    { "y", p.y },
                    { "z", p.z }
                });
            }

            data.Add("TrayectoriaPaciente", pointsList);
            data.Add("CantidadPuntosPaciente", patientTrajectory.Count);
        }

        // Referenciar documento en firestore y almacenar los datos
        var sessionRef = db.Collection("Pacientes").Document(IDPx).Collection("Sesiones").Document(IDSession);

        await sessionRef.SetAsync(data).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully) if (debugText != null) debugText.text += "\nDatos guardados en Firestore";
            else if (debugText != null) debugText.text += "\nERROR Firestore: " + task.Exception;
        });

        // Actualizar contador de sesiones completadas
        await UpdateCompletedSessionsCounter(IDPx, sessionNumber);

        onDone?.Invoke();
    }

    async Task UpdateCompletedSessionsCounter(string idPx, int sessionNumber)
    {
        try
        {
            DocumentReference patientRef = db.Collection("Pacientes").Document(idPx);

            // Actualizar el campo SesionesCompletadas
            await patientRef.UpdateAsync("SesionesCompletadas", sessionNumber);
            if (debugText != null) debugText.text += "\nSesiones completadas actualizado en firebase";
        }
        catch (Exception ex)
        {
            if (debugText != null) debugText.text += "\n Error al actualizar contador: "+ ex.Message;
        }
    }

    void DisplayStars()
    {
        // Desactivar todas las estrellas por si acaso
        star1.SetActive(false);
        star2.SetActive(false);
        star3.SetActive(false);
        star4.SetActive(false);
        star5.SetActive(false);

        int stars = (int)metrics[4];
        if (stars >= 1) star1.SetActive(true);
        if (stars >= 2) star2.SetActive(true);
        if (stars >= 3) star3.SetActive(true);
        if (stars >= 4) star4.SetActive(true);
        if (stars >= 5) star5.SetActive(true);
    }

}