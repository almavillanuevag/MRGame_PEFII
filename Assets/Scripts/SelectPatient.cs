using Firebase.Extensions;
using Firebase.Firestore;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SelectPatient : MonoBehaviour
{
    [Header("Asignar elementos para interacciones")]
    public TMP_Dropdown PatientsDropdown;
    public TMP_Dropdown TrajectoriesDropdown;
    public TextMeshProUGUI debugText; // Para Display Debugs


    [Header("Elementos publicos para lectura")]
    public static SelectPatient Instance;
    public string IDSession;
    public string IDPx; // Aquí se guardará el Document ID seleccionado
    public string IDTraj; // Aquí se guardará el Document ID de la trayectoria dentro del paciente
    public int CurrentSessionNumber;
    public int CurrentTrajectory;

    // -- Variables para funcionamiento interno --
    FirebaseFirestore db;
    private List<string> IDsPxlist = new List<string>();    // Guardar los Document IDs de los documentos
    private List<string> IDsTrajlist = new List<string>();

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
        db = FirebaseFirestore.DefaultInstance;
        LoadingPatients(); // puedo poner despues un boton de refresh! ------------------------------------------------ ** PENDIENTE    
    }

    public void LoadingPatients()
    {
        PatientsDropdown.onValueChanged.RemoveAllListeners();

        db.Collection("Pacientes").GetSnapshotAsync().ContinueWithOnMainThread(task =>
             {
                 QuerySnapshot snapshot = task.Result;
                 List<string> DropdownOptions = new List<string>();

                 PatientsDropdown.ClearOptions();
                 IDsPxlist.Clear();

                 foreach (DocumentSnapshot doc in snapshot.Documents)
                 {
                     string documentId = doc.Id;
                     DropdownOptions.Add(documentId);
                     IDsPxlist.Add(documentId);
                 }

                 if (DropdownOptions.Count > 0)
                 {
                     PatientsDropdown.AddOptions(DropdownOptions);
                     PatientsDropdown.onValueChanged.AddListener(OnSelectedPatient);
                     PatientsDropdown.value = 0;

                     // Elegir paciente actual (IDPx) si existe; si no, primero
                     int idxToSelect = 0;
                     if (!string.IsNullOrEmpty(IDPx))
                     {
                         int found = IDsPxlist.IndexOf(IDPx);
                         if (found >= 0) idxToSelect = found;
                     }

                     PatientsDropdown.value = idxToSelect;
                     PatientsDropdown.RefreshShownValue();

                     // Auto seleccionar el primero la primera vez que se comienza a jugar.
                     OnSelectedPatient(idxToSelect);
                 }
                 else
                 {
                     if (debugText != null) debugText.text += "\nNo se encontraron pacientes.";
                     PatientsDropdown.AddOptions(new List<string> { "No hay IDs disponibles" }); // poner a prueba**
                     IDPx = null;
                 }
             });
    }
    public void OnSelectedPatient(int index)
    {
        if (index < 0 || index >= IDsPxlist.Count)
            return;

        // El IDPx es el Document ID correspondiente al índice seleccionado
        IDPx = IDsPxlist[index];

        GenerateIDSession();
        IDTraj = null;
        if (TrajectoriesDropdown != null)
        {
            TrajectoriesDropdown.gameObject.SetActive(true);
            LoadingTrajectories(); 
        }
    }

    void GenerateIDSession() 
    { 
        // Validar que haya seleccionado un IDPx y desplegar si hubo error
        if (string.IsNullOrEmpty(IDPx))
        {
            if(debugText !=null) debugText.text += "\nSelecciona un paciente antes de iniciar.";
            return;
        }

        DocumentReference pacienteRef = db.Collection("Pacientes").Document(IDPx);
        pacienteRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            // Validar y desplegar si hubo error
            if (task.IsFaulted || task.IsCanceled)
            {
                if(debugText !=null) debugText.text += "\nError al obtener datos del paciente " + IDPx + task.Exception;
                return;
            }

            DocumentSnapshot snapshot = task.Result;

            // Validar y desplegar si hubo error
            if (!snapshot.Exists)
            {
                if(debugText !=null) debugText.text += "\nError al obtener datos del paciente " + IDPx + task.Exception;
                return;
            }

            // Obtener sesiones completadas 
            long CompletedSessions = 0; 
            if (snapshot.TryGetValue("SesionesCompletadas", out long completed))  CompletedSessions = completed;

            CurrentSessionNumber = (int)CompletedSessions + 1; // contador de sesiones ++

            // Generar el ID de la sesion
            string dateDDMMAA = System.DateTime.Now.ToString("ddMMyy");
            IDSession = $"SessionNum{CurrentSessionNumber:D3}-{dateDDMMAA}";

            if(debugText !=null) debugText.text += "\nID de sesión: " + IDSession + ". Completadas: " + CompletedSessions;
        });
        
    }

    public void LoadingTrajectories(string selectIdTraj = null)
    {
        if (TrajectoriesDropdown == null) { Log("TrajectoriesDropdown no asignado."); return; }
        if (string.IsNullOrEmpty(IDPx)) { Log("IDPx vacío. No puedo cargar trayectorias."); return; }

        TrajectoriesDropdown.onValueChanged.RemoveAllListeners();

        var trajectoryRef = db.Collection("Pacientes").Document(IDPx).Collection("Trayectorias");

        trajectoryRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Log("Error cargando trayectorias: " + task.Exception);
                return;
            }

            QuerySnapshot snapshot = task.Result;

            TrajectoriesDropdown.ClearOptions();
            IDsTrajlist.Clear();

            List<string> options = new List<string>();
            foreach (DocumentSnapshot doc in snapshot.Documents)
            {
                options.Add(doc.Id);
                IDsTrajlist.Add(doc.Id);
            }

            if (options.Count > 0)
            {
                TrajectoriesDropdown.AddOptions(options);
                TrajectoriesDropdown.onValueChanged.AddListener(OnSelectedTrajectory);
                TrajectoriesDropdown.value = 0;
                TrajectoriesDropdown.RefreshShownValue();

                int idxToSelect = 0;
                if (!string.IsNullOrEmpty(selectIdTraj))
                {
                    int found = IDsTrajlist.IndexOf(selectIdTraj);
                    if (found >= 0) idxToSelect = found;
                }

                TrajectoriesDropdown.value = idxToSelect;
                TrajectoriesDropdown.RefreshShownValue();
                OnSelectedTrajectory(idxToSelect);
            }
            else
            {
                Log("No se encontraron trayectorias para este paciente.");
                TrajectoriesDropdown.AddOptions(new List<string> { "No hay trayectorias disponibles" });
                TrajectoriesDropdown.RefreshShownValue();
                IDTraj = null;
            }
        });
    }

    public void OnSelectedTrajectory(int index)
    {
        if (index < 0 || index >= IDsTrajlist.Count)
        {
            if (debugText != null) debugText.text += "\nIndice Invalido";
            return;
        }
            

        // El IDPx es el Document ID correspondiente al índice seleccionado
        IDTraj = IDsTrajlist[index];

        if (debugText != null) debugText.text += "\nDocument ID seleccionado (IDPx): " + IDPx;
    }

    void Log(string msg)
    {
        Debug.Log(msg);
        if (debugText != null) debugText.text += "\n" + msg;
    }
}


