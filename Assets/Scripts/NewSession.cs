using Firebase.Extensions;
using Firebase.Firestore;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NewSession : MonoBehaviour
{
    [Header("Asignar elementos para interacciones")]
    // Imagenes de estrella
    public GameObject star1;
    public GameObject star2;
    public GameObject star3;
    public GameObject star4;
    public GameObject star5;
    public TextMeshProUGUI debugText; // Para Display Debugs
    // Acceder a otros scripts
    public EndGame EndGame;
    public TrajectoryManager TrajectoryManager;

    [Header("Objetos publicos para lectura")]
    public float radio;

    // Variables para uso interno
    FirebaseFirestore db;

    public void Start()
    {
        db = FirebaseFirestore.DefaultInstance; // Inicializar firestore 
        DisplayStars();
    }

    void DisplayStars()
    {
        // Desactivar todas las estrellas por si acaso
        star1.SetActive(false);
        star2.SetActive(false);
        star3.SetActive(false);
        star4.SetActive(false);
        star5.SetActive(false);

        int stars = (int)EndGame.metrics[4];
        if (stars >= 1) star1.SetActive(true);
        if (stars >= 2) star2.SetActive(true);
        if (stars >= 3) star3.SetActive(true);
        if (stars >= 4) star4.SetActive(true);
        if (stars >= 5) star5.SetActive(true);
    }

    public void PlayAgain()
    {
        // Generar nuevo ID para la proxima sesión
        GenerateNewSessionID();
    }

    void GenerateNewSessionID()
    {
        if (SelectPatient.Instance == null || string.IsNullOrEmpty(SelectPatient.Instance.IDPx))
        {
            debugText.text = "Error: No hay instancia de SelectPatient o ID de paciente.";
            return;
        }

        string IDPx = SelectPatient.Instance.IDPx;

        DocumentReference PatientRef = db.Collection("Pacientes").Document(IDPx);
        PatientRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                debugText.text = "Error al obtener datos del paciente: " + task.Exception;
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
            CreateAndReload();
        });
    }

    void CreateAndReload()
    {
        int sessionNumber = SelectPatient.Instance.CurrentSessionNumber;
        string dateDDMMAA = DateTime.Now.ToString("ddMMyy");

        // Generar el nuevo ID de la proxima sesion y actualizarlo
        string newIDSession = $"SessionNum{sessionNumber:D3}-{dateDDMMAA}";
        SelectPatient.Instance.IDSession = newIDSession;

        debugText.text += $"\n Próxima sesión: {newIDSession}";

        // Recargar escena
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
