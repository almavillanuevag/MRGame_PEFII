using Firebase.Extensions;
using Firebase.Firestore;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Vector3 = UnityEngine.Vector3;
/* 
 * Notas a mejorar:
 *  - Hacerlo para ambas manos al estilo de la nave: un collider trigger que cuando choque con la pelota se pegue y comience a grabar
 *  - Ponerle un UI flotante a la pelota que de las instrucciones
 *  - Intentarlo con otra forma de comenzar a dibujar (botones medio incomodo)
 *  - Buscar un asset mas bonito para pincel
 */

public class DrawNewTrajectory : MonoBehaviour 
{
    [Header("Asignar elementos para interacciones")]
    public OVRSkeleton ovrSkeleton; // Mano del terapeuta (derecha por mientras)                              **  CORREGIR!
    public GameObject debugText;
    public VisualizeTrajectorySpline visualizer;

    [Header("Objetos publicos para lectura")]
    // Listas de los puntos
    public List<Vector3> therapistFullTrajectoryWorld = new List<Vector3>();
    public float radius;
    public string IDTraj;

    // --- Variables para funcionamiento interno --
    bool trackingReady = false;
    Transform palmTransform;
    Transform brushSphere;
    readonly OVRSkeleton.BoneId boneId = OVRSkeleton.BoneId.XRHand_Palm;
    

    // Para el muestreo
    float sampleInterval = 1/60f;
    float minDistance = 0.01f;
    bool drawing = false;
    Vector3 lastSamplePos;
    Coroutine samplingCoroutine;
    FirebaseFirestore db;
    int CurrentTrajectory;

    // Para el render de la linea
    LineRenderer previewLine;
    float lineWidth = 0.005f;

    private void Start()
    {
        debugText.SetActive(false);

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
        TextMeshProUGUI log = debugText.GetComponent<TextMeshProUGUI>();

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
                if (debugText != null) log.text = "No se encontró XRHand_Palm aún...";
                return;
            }
        }

        // Actualizar posicion del pincel
        if (palmTransform == null)
        {
            if (debugText != null) log.text = "palmTransform es null";
            return;
        }
        brushSphere.position = palmTransform.position;
        brushSphere.rotation = palmTransform.rotation;
    }

    private IEnumerator WaitForSkeletons()
    {
        TextMeshProUGUI log = debugText.GetComponent<TextMeshProUGUI>();
        if (debugText != null) log.text = "Esperando hand tracking...";

        // Esperar skeleton + datos válidos
        while (ovrSkeleton == null || !ovrSkeleton.IsDataValid)
        {
            if (debugText != null) log.text = "Esperando hand tracking...";
            yield return null;
        }

        // Esperar bones
        while (ovrSkeleton.Bones == null || ovrSkeleton.Bones.Count == 0)
        {
            yield return null;
        }

        // Encontrar palm bone
        palmTransform = FindPalmTransform();
        if (palmTransform == null)
        {
            // No marcamos trackingReady, reintentamos
            yield return new WaitForSeconds(0.1f);
            StartCoroutine(WaitForSkeletons());
            yield break;
        }

        trackingReady = true;
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

    // ---- Funciones para los botones del UI ----
    public void StartDrawing() // Botón comenzar a dibujar
    {
        if (!trackingReady)
        {
            Log("No se puede dibujar: hand tracking no está listo");
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

    
    public void StopDrawing() // Botón finalizar de dibujar
    {
        if (!drawing) return;
        drawing = false;
        if (samplingCoroutine != null) StopCoroutine(samplingCoroutine);
        samplingCoroutine = null;

        // Visualizarla
        if (visualizer != null)
            visualizer.BuildSplineFromRecordedTrajectory();
    }

    public void SaveToFirestore()
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

        var patientRef = db.Collection("Pacientes").Document(idPx);

        if (therapistFullTrajectoryWorld.Count < 2)
        {
            Log("ERROR: Trayectoria insuficiente para guardar.");
            return;
        }

        // obtener el radio seleccionado y redondearlo (min de 1mm)
        float radiusRaw = visualizer.tubeRadius;
        float radiusRounded = Mathf.Round(radiusRaw * 1000f) / 1000f;
        visualizer.splineExtrude.Radius = radiusRounded;
        visualizer.splineExtrude.Rebuild();

        radius = visualizer.tubeRadius;

        db.RunTransactionAsync(async transaction =>
        {
            // Leer contador actual
            var snap = await transaction.GetSnapshotAsync(patientRef);
            long count = 0;
            if(snap.Exists && snap.TryGetValue("TrayectoriasCompletadas", out long c))
                count = c;

            // Incrementar el contador en firebase
            long next = count+1;
            transaction.Update(patientRef, new Dictionary<string, object>
            {
                { "TrayectoriasCompletadas", next }
            });

            // Generar el IDTraj
            string date = System.DateTime.Now.ToString("ddMMyy");
            string idTraj = $"Trayectoria{next:D3}-{date}";

            // Ordenar puntos Vector3 en diccionario array de maps (formato compatible con firebase) para guardarlos
            var pointsList = new List<Dictionary<string, object>>(therapistFullTrajectoryWorld.Count);
            foreach (var p in therapistFullTrajectoryWorld)
            {
                pointsList.Add(new Dictionary<string, object>
                {
                    { "x", p.x },
                    { "y", p.y },
                    { "z", p.z }
                });
            }

            var docData = new Dictionary<string, object>
            {
                { "TrayectoriaCompleta", pointsList },
                { "Radio", (double)radius},
                { "CantidadPuntos", therapistFullTrajectoryWorld.Count },
                { "Tiempo", Timestamp.GetCurrentTimestamp() }
            };

            var trajRef = patientRef.Collection("Trayectorias").Document(idTraj);
            transaction.Set(trajRef, docData);

            return idTraj; // Regresar el IDTraj para seleccionarlo en el dropdown

        }).ContinueWithOnMainThread(task =>        
        {
            if (task.IsCompletedSuccessfully)
            {
                // Almacenar el ID de la trajectoria en la instancia de SelectPatient
                IDTraj = task.Result;
                Log("Trayectoria guardada en Firestore: ID " + IDTraj);

                if (SelectPatient.Instance != null)
                {
                    SelectPatient.Instance.IDTraj = IDTraj;
                    SelectPatient.Instance.LoadingTrajectories(IDTraj);
                }     
            }
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

    public List<Vector3> GetFullTrajectory() // Para leerlo desde el visualizador
    { 
        return therapistFullTrajectoryWorld;
    }

    private void Log(string msg)
    {
        Debug.Log(msg);
        if (debugText != null)
        {
            debugText.SetActive(true);
            TextMeshProUGUI log = debugText.GetComponent<TextMeshProUGUI>();
            log.text = msg;
        }
    }

}
