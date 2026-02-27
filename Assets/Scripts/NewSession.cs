using Firebase.Extensions;
using Firebase.Firestore;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NewSession : MonoBehaviour
{
    [Header("Log opcional para pruebas y errores")]
    public TextMeshProUGUI debugText; // Para Display Debugs

    [Header("Objetos publicos para lectura")]
    public float radio;

    // Variables para uso interno
    FirebaseFirestore db;
    float radiusDecrement = 0.02f;
    float radiusMin = 0.03f;

    public void Start()
    {
        db = FirebaseFirestore.DefaultInstance; // Inicializar firestore 
    }

    public void PlayAgain(float currentRadius)
    {
        // Validar que existan los IDs de trayectoria y pacientes y declarar variables de firebase
        if (SelectPatient.Instance == null || string.IsNullOrEmpty(SelectPatient.Instance.IDPx))
            return;

        string IDPx = SelectPatient.Instance.IDPx;
        string IDTraj = SelectPatient.Instance.IDTraj;

        DocumentReference TrajectoryRef = db.Collection("Pacientes")
                                            .Document(IDPx)
                                            .Collection("Trayectorias")
                                            .Document(IDTraj);


        // Leer radio actual de la trayectoria en la partida
        Log($"Radio actual de trayectoria: {currentRadius:F3} m");

        bool isMinReached = Mathf.Approximately(currentRadius, radiusMin)
                                || currentRadius <= radiusMin;

        if (isMinReached)
        {
            // Radio mínimo alcanzado, seguir jugando con el valor minimo
            Log("Radio mínimo alcanzado.");
            // Recargar escena y generar id para proxima sesión
            GenerateNewSessionAndReload(IDPx);
        }
        else
        {
            //  Aplicar decremento del radio
            float newRadius = Mathf.Max(currentRadius - radiusDecrement, radiusMin);
            Log($"Nuevo radio: {newRadius:F3} m (decremento: -{radiusDecrement})");

            // Actualizar en firebase
            TrajectoryRef.UpdateAsync("Radio", (double)newRadius)
                .ContinueWithOnMainThread(updateTask =>
                {
                    if (updateTask.IsFaulted || updateTask.IsCanceled)
                    {
                        Log("ERROR actualizando Radio en Firestore: " + updateTask.Exception);
                        return;
                    }

                    Log($"Radio actualizado a {newRadius:F3} m en Trayectoria '{IDTraj}'.");
                });

            // Recargar escena y generar id para proxima sesión
            GenerateNewSessionAndReload(IDPx);
        }
    }

    void GenerateNewSessionAndReload(string IDPx) 
    {
        DocumentReference PatientRef = db.Collection("Pacientes")
                                         .Document(IDPx);

        PatientRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Log("Error al obtener datos del paciente: " + task.Exception);
                return;
            }

            DocumentSnapshot snapshot = task.Result;

            long CompletedSessions = 0;
            if (snapshot.Exists && snapshot.TryGetValue("SesionesCompletadas", out long completed))
            {
                CompletedSessions = completed;
            }
            int nextSessionNumber = (int)CompletedSessions + 1;

            SelectPatient.Instance.CurrentSessionNumber = nextSessionNumber;

            // Asignar ID y recargar escena
            int sessionNumber = SelectPatient.Instance.CurrentSessionNumber;
            string dateDDMMAA = DateTime.Now.ToString("ddMMyy");

            // Generar el nuevo ID de la proxima sesion y actualizarlo
            string newIDSession = $"SessionNum{sessionNumber:D3}-{dateDDMMAA}";
            SelectPatient.Instance.IDSession = newIDSession;

            if (debugText != null) debugText.text += $"\n Próxima sesión: {newIDSession}";

            // Recargar escena
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        });
    }

    void Log(string msg)
    {
        Debug.Log(msg);
        if (debugText != null) debugText.text += "\n" + msg;
    }
}
