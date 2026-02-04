using Firebase.Extensions;
using Firebase.Firestore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class EndGame : MonoBehaviour
{
    [Header("¿Mostrar mensajes?")]
    public bool ShowDebugsLog = true; // Para mostrar los mensajes (si no los quiero poner false)

    [Header("Asignar elementos para interacciones")]
    public Transform PlanetEndPoint; // Punto final donde se situará la nave
    public TMPro.TextMeshProUGUI debugText; // Para Display Debugs (quitar despues, para pruebas)
    public TrajectoryManager TrajectoryManager; // Scrpt de la trayectoria para calcular 
    public GameObject UICanvasWin;
    public FollowHand followHand;


    [Header("Objetos publicos para lectura")]
    public float tMin = 50; // variable para stars ** MODIFICABLE
    public float error3 = 6; // variable para stars ** 
    public float stars0;
    public float radio0 = 0f;
    public float TotalErrors0 = 0;
    public bool OutOfTube = false;
    public bool End = false;
    public int sessionNumber;

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
                if (ShowDebugsLog) debugText.text += "\nSe inicializó Firestore";
            }
            else
            {
                if (ShowDebugsLog) debugText.text += "\nNo se pudo resolver dependencias de Firestore";
            }
        });
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
            ShipMovement shipMovement = other.GetComponentInParent<ShipMovement>();
            shipMovement.ForceRelease();

            SetShipToFinalPosition(other);

            // Eliminar las funciones de retroalimentacion haptica
            followHand.StopHapticFeedbackFunctions();

            // Cargar UI de victoria y fin del juego
            if (ShowDebugsLog) debugText.text += "\nUIcanvas active";
            UICanvasWin.SetActive(true);

            // Calcular las metricas de desempeño solo una vez si ya termino
            if (!End)  
            {
                End = true; // Flag de que ya termino y que no se vuelvan a calcular
                if (ShowDebugsLog) debugText.text += "\n---------END!-------";
                // Obtener el tiempo cuando se colocó la nave en la mano (inicio del juego)
                BeginningTime = shipMovement.BeginningTime;
                // Obtener tiempo en el que llegó al planeta fin
                FinishTime = Time.time;

                // Calcular las metricas
                metrics = CalculatePerformanceMetrics();
                if (ShowDebugsLog) debugText.text +=
                    $"\nErrors: {metrics[0]}\n" +
                    $"Time: {metrics[1]:F1}\n" +
                    $"Inside %: {metrics[2]:F1}\n" +
                    $"Radio: {metrics[3]:F2}\n" +
                    $"Stars: {metrics[4]}";

                // Almacenar las metricas en Firebase firestore database
                SaveSessionToFirestore(metrics);
            }
        }
    }

    void SetShipToFinalPosition(Collider other)
    {
        if (ShowDebugsLog) debugText.text += "\nAterrizaje";

        // Desparentar la nave
        other.transform.SetParent(null);

        // Posicionarlo arriba del planeta
        other.transform.position = PlanetEndPoint.position;
        other.transform.rotation = PlanetEndPoint.rotation;

        // Regresarle propiedades fisicas
        ShipGameObject = other.gameObject;
        Rigidbody rb = ShipGameObject.GetComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero; // Detener cualquier velocidad residual
        rb.angularVelocity = Vector3.zero;
    }

    float[] CalculatePerformanceMetrics()
    {
        if (ShowDebugsLog) debugText.text += "\nCalculando Metricas...";

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

    async void SaveSessionToFirestore(float[] metrics)
    {
        // Validar que tenga acceso a la instancia de SelectPatient
        if (SelectPatient.Instance == null)
        {
            debugText.text += "\nNo hay Instancia de SelectPatient.cs";
            return;
        }
        
        // Acceder a la instancia para obtener el ID del paciente y sesion:
        IDPx = SelectPatient.Instance.IDPx;
        IDSession = SelectPatient.Instance.IDSession;
        sessionNumber = SelectPatient.Instance.CurrentSessionNumber;

        if (ShowDebugsLog) // Mostrar los IDs para corroborar que se leyeron
        {
            debugText.text += "\nIDPx desde EndGame: " + IDPx;
            debugText.text += "\nIDSession desde EndGame: " + IDSession;
            debugText.text += "\nNumero de sesion desde EndGame: " + sessionNumber;
        }

        // Buscar errores
        if (db == null)
        {
            if (ShowDebugsLog) debugText.text += "\nERROR: Firestore no inicializado";
            return;
        }

        if (string.IsNullOrEmpty(IDPx))
        {
            if (ShowDebugsLog) debugText.text += "\nERROR: IDPx inválido";
            return;
        }

        // Continuar si no hay errores
        if (ShowDebugsLog) debugText.text += "\nEnviando datos a Firestore...";

        // Organizar metricas como diccionario para Firestore
        var data = new Dictionary<string, object>
        {
            { "DateTime", Timestamp.GetCurrentTimestamp() },
            { "TotalErrors", (int)metrics[0] },
            { "TotalTime", metrics[1] },
            { "InsideTimePercentage", metrics[2] },
            { "radio", metrics[3] },
            { "stars", (int)metrics[4] }
        };

        // Referenciar documento en firestore y almacenar los datos
        var sessionRef = db.Collection("Pacientes").Document(IDPx).Collection("Sesiones").Document(IDSession);

        await sessionRef.SetAsync(data).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully) if (ShowDebugsLog) debugText.text += "\nDatos guardados en Firestore";
            else if (ShowDebugsLog) debugText.text += "\nERROR Firestore: " + task.Exception;
        });

        // Actualizar contador de sesiones completadas
        await UpdateCompletedSessionsCounter(IDPx, sessionNumber);
    }

    async Task UpdateCompletedSessionsCounter(string idPx, int sessionNumber)
    {
        try
        {
            DocumentReference patientRef = db.Collection("Pacientes").Document(idPx);

            // Actualizar el campo SesionesCompletadas
            await patientRef.UpdateAsync("SesionesCompletadas", sessionNumber);
            if (ShowDebugsLog) debugText.text += "\nSesiones completadas actualizado en firebase";
        }
        catch (Exception ex)
        {
            if (ShowDebugsLog) debugText.text += "\n Error al actualizar contador: "+ ex.Message;
        }
    }

}