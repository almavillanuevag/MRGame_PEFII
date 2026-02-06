using Firebase.Extensions;
using Firebase.Firestore;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TerrainTools;
using Vector3 = UnityEngine.Vector3;

public class DrawNewTrajectory : MonoBehaviour
{
    [Header("Asignar elementos para interacciones")]
    public OVRSkeleton ovrSkeleton; // Mano del terapeuta (izq o der)
    public TMPro.TextMeshProUGUI debugText;

    [Header("Objetos publicos para lectura")]
    // Listas de los puntos
    public List<Vector3> therapistFullTrajectoryWorld = new List<Vector3>();

    // --- Variables para funcionamiento interno --
    bool trackingReady = false;
    Transform palmTransform;
    Transform brushSphere;
    readonly OVRSkeleton.BoneId boneId = OVRSkeleton.BoneId.XRHand_Palm;

    // Para el muestreo
    float sampleInterval = 0.1f;
    float minDistance = 0.01f;
    bool drawing = false;
    Vector3 lastSamplePos;
    Coroutine samplingCoroutine;
    FirebaseFirestore db;

    // Para el render de la linea
    LineRenderer previewLine;
    float lineWidth = 0.005f;

    private void Start()
    {
        // Buscar HandTracking
        StartCoroutine(WaitForSkeletons());

        // Inicializar firestore
        db = FirebaseFirestore.DefaultInstance;

        // Definir el transform del GO
        brushSphere = gameObject.GetComponent<Transform>();

        SetupLineRenderer();
    }

    private void Update()
    {
        if (!trackingReady) return;

        // Si se pierde tracking o el skeleton deja de ser válido, reintentar
        if (ovrSkeleton == null || !ovrSkeleton.IsDataValid)
        {
            trackingReady = false;
            palmTransform = null;
            StartCoroutine(WaitForSkeletons());
            return;
        }

        // Si se pierde el HandTracking, reintentar encontrarlo
        if (palmTransform == null)
        {
            palmTransform = FindPalmTransform();
            if (palmTransform == null)
            {
                if (debugText != null) debugText.text = "No se encontró XRHand_Palm aún...";
                return;
            }
        }

        // Actualizar posicion del pincel
        if (palmTransform == null)
        {
            if (debugText != null) debugText.text = "palmTransform es null";
            return;
        }
        brushSphere.position = palmTransform.position;
        brushSphere.rotation = palmTransform.rotation;
    }

    private IEnumerator WaitForSkeletons()
    {
        if (debugText != null) debugText.text = "Esperando hand tracking...";

        // Esperar skeleton + datos válidos
        while (ovrSkeleton == null || !ovrSkeleton.IsDataValid)
        {
            if (debugText != null) debugText.text = "Esperando hand tracking...";
            yield return null;
        }

        // Esperar bones
        while (ovrSkeleton.Bones == null || ovrSkeleton.Bones.Count == 0)
        {
            Log("Aún no hay bones (null o count=0)...");
            yield return null;
        }

        // Encontrar palm bone
        palmTransform = FindPalmTransform();
        if (palmTransform == null)
        {
            Log("No se encontró XRHand_Palm en este OVRSkeleton.");
            // No marcamos trackingReady, reintentamos
            yield return new WaitForSeconds(0.1f);
            StartCoroutine(WaitForSkeletons());
            yield break;
        }

        trackingReady = true;
        Log("Hand tracking listo");
    }

    private Transform FindPalmTransform()
    {
        if (ovrSkeleton == null || ovrSkeleton.Bones == null) return null;

        foreach (var b in ovrSkeleton.Bones)
        {
            if (b != null && b.Id == boneId)
                return b.Transform;
        }
        return null;
    }

    // --- Funciones para los botones del UI ---

    // Botón comenzar a dibujar
    public void StartDrawing()
    {
        if (!trackingReady)
        {
            Log("No se puede dibujar: hand tracking no listo.");
            return;
        }

        therapistFullTrajectoryWorld.Clear();
        drawing = true;

        if (previewLine != null)
            previewLine.positionCount = 0;

        lastSamplePos = brushSphere.position;

        // Guardar el primer punto para que siempre incluya inicio
        therapistFullTrajectoryWorld.Add(lastSamplePos);
        UpdatePreviewLine();

        if (samplingCoroutine != null) StopCoroutine(samplingCoroutine);
        samplingCoroutine = StartCoroutine(SampleCoroutine());

    }

    // Botón finalizar de dibujar
    public void StopDrawing()
    {
        if (!drawing) return;
        drawing = false;
        if (samplingCoroutine != null) StopCoroutine(samplingCoroutine);
        samplingCoroutine = null;

        SaveToFirestore();
    }

    private void SaveToFirestore()
    {
        if (db == null)
        {
            Log("ERROR: Firestore no inicializado.");
            return;
        }

        // obtener el id del paciente
        string idPx = (SelectPatient.Instance != null) ? SelectPatient.Instance.IDPx : null;

        if (string.IsNullOrEmpty(idPx))
        {
            Log("ERROR: IDPx no disponible (SelectPatient.Instance.IDPx vacío).");
            return;
        }

        if (therapistFullTrajectoryWorld.Count < 2)
        {
            Log("ERROR: Trayectoria insuficiente para guardar.");
            return;
        }

        // Convertir Vector3 -> Firestore (array de maps)
        List<Dictionary<string, object>> points = new List<Dictionary<string, object>>(therapistFullTrajectoryWorld.Count);
        foreach (var p in therapistFullTrajectoryWorld)
        {
            points.Add(new Dictionary<string, object>
            {
                { "x", p.x },
                { "y", p.y },
                { "z", p.z }
            });
        }

        Dictionary<string, object> docData = new Dictionary<string, object>
        {
            { "TrayectoriaCompleta", points },
            { "CantidadPuntos", therapistFullTrajectoryWorld.Count },
            { "FrecuenciaMuestreo", sampleInterval },
            { "Tiempo", Timestamp.GetCurrentTimestamp() }
        };

        DocumentReference docRef = db.Collection("Pacientes")
                                     .Document(idPx)
                                     .Collection("Trayectorias")
                                     .Document(); // autoID

        docRef.SetAsync(docData).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully)
                Log("Trayectoria guardada en Firestore");
            else
                Log("ERROR guardando Firestore: " + task.Exception);
        });
    }

    void SetupLineRenderer() // Configuración para que se vea en las Oculus
    {
        previewLine = gameObject.AddComponent<LineRenderer>();

        // Configuración básica de la línea
        previewLine.startWidth = lineWidth;
        previewLine.endWidth = lineWidth;
        previewLine.positionCount = 0;
        previewLine.useWorldSpace = true;

        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard"); // Fallback
        if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"); // Fallback seguro para líneas

        Material mat = new Material(sh);

        // Ajustes para que la línea brille y se vea
        mat.color = Color.green;

        // Asignar material
        previewLine.material = mat;

        // Que no proyecte sombras raras
        previewLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        previewLine.receiveShadows = false;
    }

    private IEnumerator SampleCoroutine()
    {
        while (drawing)
        {
            Vector3 pos = brushSphere.position;
            if (Vector3.Distance(pos, lastSamplePos) >= minDistance)
            {
                therapistFullTrajectoryWorld.Add(pos);
                lastSamplePos = pos;
                UpdatePreviewLine();
            }
            yield return new WaitForSeconds(sampleInterval);
        }
    }

    private void UpdatePreviewLine()
    {
        if (previewLine == null) return;

        previewLine.positionCount = therapistFullTrajectoryWorld.Count;
        for (int i = 0; i < therapistFullTrajectoryWorld.Count; i++)
            previewLine.SetPosition(i, therapistFullTrajectoryWorld[i]);
    }

    private void Log(string msg)
    {
        Debug.Log(msg);
        if (debugText != null) debugText.text += "\n" + msg;
    }

}
