using TMPro;
using UnityEngine;
using Firebase.Extensions;
using Firebase.Firestore;
using System.Collections.Generic;

public class SelectPatient : MonoBehaviour
{
    FirebaseFirestore firestore;

    public static SelectPatient Instance;
    public string IDSession;
    public string IDPx; // Aquí se guardará el Document ID seleccionado
    public TMP_Dropdown PatientsDropdown;
    public TextMeshProUGUI debugText; // Para Display Debugs

    public int CurrentSessionNumber;

    // Guarda los Document IDs de los documentos
    private List<string> IDslist = new List<string>();

    private void Awake()
    {
        // Mantener la info generada en todas las sesiones, no destruirlo
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        firestore = FirebaseFirestore.DefaultInstance;
        LoadingPatients(); // puedo poner despues un boton de refresh!
    }

    public void LoadingPatients()
    {
        PatientsDropdown.onValueChanged.RemoveAllListeners();

        firestore.Collection("Pacientes").GetSnapshotAsync().ContinueWithOnMainThread(task =>
             {
                 QuerySnapshot snapshot = task.Result;
                 List<string> DropdownOptions = new List<string>();

                 PatientsDropdown.ClearOptions();
                 IDslist.Clear();

                 foreach (DocumentSnapshot doc in snapshot.Documents)
                 {
                     string documentId = doc.Id;
                     DropdownOptions.Add(documentId);
                     IDslist.Add(documentId);
                 }

                 if (DropdownOptions.Count > 0)
                 {
                     PatientsDropdown.AddOptions(DropdownOptions);
                     PatientsDropdown.onValueChanged.AddListener(OnSelectedPatient);
                     PatientsDropdown.value = 0;

                     // Auto-seleccionar el primero
                     OnSelectedPatient(0);
                 }
                 else
                 {
                     debugText.text += "\nNo se encontraron pacientes.";
                     PatientsDropdown.AddOptions(new List<string> { "No hay IDs disponibles" }); // poner a prueba**
                     IDPx = null;
                 }
             });
    }
    public void OnSelectedPatient(int index)
    {
        if (index < 0 && index >= IDslist.Count)
            debugText.text += "\nIndice Invalido";

        // El IDPx es el Document ID correspondiente al índice seleccionado
        IDPx = IDslist[index];
        debugText.text += "\nDocument ID seleccionado (IDPx): " + IDPx;

        GenerateIDSession();
    }

    void GenerateIDSession() 
    { 
        // Validar que haya seleccionado un IDPx y desplegar si hubo error
        if (string.IsNullOrEmpty(IDPx))
        {
            debugText.text += "\nSelecciona un paciente antes de iniciar.";
            return;
        }

        DocumentReference pacienteRef = firestore.Collection("Pacientes").Document(IDPx);
        pacienteRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            // Validar y desplegar si hubo error
            if (task.IsFaulted || task.IsCanceled)
            {
                debugText.text += "\nError al obtener datos del paciente " + IDPx + task.Exception;
                return;
            }

            DocumentSnapshot snapshot = task.Result;

            // Validar y desplegar si hubo error
            if (!snapshot.Exists)
            {
                debugText.text += "\nError al obtener datos del paciente " + IDPx + task.Exception;
                return;
            }

            // Obtener sesiones completadas 
            long CompletedSessions = 0; 
            if (snapshot.TryGetValue("SesionesCompletadas", out long completed))  CompletedSessions = completed;

            CurrentSessionNumber = (int)CompletedSessions + 1; // contador de sesiones ++

            // Generar el ID de la sesion
            string dateDDMMAA = System.DateTime.Now.ToString("ddMMyy");
            IDSession = $"SessionNum{CurrentSessionNumber:D3}-{dateDDMMAA}";

            debugText.text += "\nID de sesión: " + IDSession + ". Completadas: " + CompletedSessions;
        });
        
    }
    public void Play()
    {
        // Ahorita se estan seleccionando al momento de ponerlo - para despues (creo)
    }
}


