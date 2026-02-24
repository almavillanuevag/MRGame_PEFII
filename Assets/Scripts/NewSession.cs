using Firebase.Extensions;
using Firebase.Firestore;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NewSession : MonoBehaviour
{
    [Header("Asignar elementos para interacciones")]
    public TrajectoryManager TrajectoryManager;

    [Header("Log opcional para pruebas y errores")]
    public TextMeshProUGUI debugText; // Para Display Debugs

    [Header("Objetos publicos para lectura")]
    public float radio;

    // Variables para uso interno
    FirebaseFirestore db;

    public void Start()
    {
        db = FirebaseFirestore.DefaultInstance; // Inicializar firestore 
    }

    public void PlayAgain()
    {
        // Generar nuevo ID para la proxima sesión
        if (SelectPatient.Instance == null || string.IsNullOrEmpty(SelectPatient.Instance.IDPx))
        {
            if (debugText != null) debugText.text = "Error: No hay instancia de SelectPatient o ID de paciente.";
            return;
        }

        string IDPx = SelectPatient.Instance.IDPx;

        DocumentReference PatientRef = db.Collection("Pacientes").Document(IDPx);
        PatientRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                if (debugText != null) debugText.text = "Error al obtener datos del paciente: " + task.Exception;
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
}
