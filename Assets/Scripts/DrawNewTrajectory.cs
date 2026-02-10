using Firebase.Extensions;
using Firebase.Firestore;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vector3 = UnityEngine.Vector3;
/* 
 * Notas a mejorar:
 *  - Intentarlo con otra forma de comenzar a dibujar (botones medio incomodo)
 *  - Ponerle un UI flotante a la pelota que de las instrucciones
 *  - Buscar un asset mas bonito para pincel
 */

public class DrawNewTrajectory : MonoBehaviour 
{
    [Header("Asignar elementos para interacciones")]
    public GameObject debugText;
    public VisualizeTrajectorySpline visualizer;

    [Header("UI Flujo Grabación")]
    public GameObject panelIdle;        // Botón "Iniciar"
    public GameObject panelCountdown;   // Texto 3..2..1
    public GameObject panelPost;        // Slider + Guardar/Regrabar

    public TextMeshProUGUI countdownTMP;
    public TextMeshProUGUI tituloTMP;

    [Header("Objetos publicos para lectura")]
    // Listas de los puntos
    public List<Vector3> therapistFullTrajectoryWorld = new List<Vector3>();
    public float radius;
    public string IDTraj;

    // --- Variables para funcionamiento interno --
    bool trackingReady = false;
    Transform palmTransform;
    Transform brushSphere;
    OVRSkeleton ovrSkeleton;
    readonly OVRSkeleton.BoneId boneId = OVRSkeleton.BoneId.XRHand_Palm;

    // Para el muestreo
    float sampleInterval = 1/60f;
    float minDistance = 0.01f;
    bool drawing = false;
    Vector3 lastSamplePos;
    Coroutine samplingCoroutine;
    FirebaseFirestore db;
    bool isHolding = false;

    // Para el render de la linea
    LineRenderer previewLine;
    float lineWidth = 0.005f;

    // Para iniciar las grabaciones UI
    float countdownSeconds = 3f;
    float recordSeconds = 5f;
    enum DrawState { Idle, Countdown, Recording, Post }
    DrawState state = DrawState.Idle;
    Coroutine flowCoroutine;


    private void Start()
    {
        // Buscar HandTracking
        StartCoroutine(WaitForSkeletons());

        // Inicializar firestore
        db = FirebaseFirestore.DefaultInstance;

        // Definir el transform del GO
        brushSphere = gameObject.GetComponent<Transform>();

        // Configuracion del line render
        SetupLineRenderer();

        // 
        SetState(DrawState.Idle);
        gameObject.GetComponent<MeshRenderer>().enabled = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("HandCollider")) return;
        if (isHolding) return;

        // Encontrar skeleton de esa mano
        OVRSkeleton skeleton = other.GetComponentInChildren<OVRSkeleton>();
        if (skeleton == null)
        {
            Debug.LogWarning("HandCollider detectado, pero no se encontró OVRSkeleton en hijos.");
            return;
        }

        ovrSkeleton = skeleton;

        // Encontrar palm una vez 
        palmTransform = FindPalmTransform();
        if (palmTransform == null)
        {
            // Si aún no está listo, lo resolvemos en Update/Coroutine
            trackingReady = false;
            StartCoroutine(WaitForSkeletons());
        }
        else trackingReady = true;
        isHolding = true;
    }

    void Update()
    {
        if (!isHolding) return;
        if(!trackingReady) return;

        if (ovrSkeleton == null || !ovrSkeleton.IsDataValid)
        {
            trackingReady = false;
            palmTransform = null;
            StartCoroutine(WaitForSkeletons());
            return;
        }

        if (palmTransform == null)
        {
            palmTransform = FindPalmTransform();
            if (palmTransform == null) return;
        }

        // Actualizar posicion del pincel
        transform.position = palmTransform.position;
        transform.rotation = palmTransform.rotation;
    }

    // --- Funciones publicas para asignar a botones UI -- 

    public void SaveToFirestore()// Botón guardar firestore 
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
            if (snap.Exists && snap.TryGetValue("TrayectoriasCompletadas", out long c))
                count = c;

            // Incrementar el contador en firebase
            long next = count + 1;
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
                SetState(DrawState.Idle);
                gameObject.GetComponent<MeshRenderer>().enabled = false;

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

    public void StartRecordingFlow() // Botón iniciar grabación
    {
        gameObject.GetComponent<MeshRenderer>().enabled = true;
        if (!trackingReady)
        {
            Log("Sujeta el pincel y vuelve a intentar");
            return;
        }

        // Si ya hay un flujo corriendo, lo cancelamos
        if (flowCoroutine != null) StopCoroutine(flowCoroutine);
        flowCoroutine = StartCoroutine(RecordingFlowCoroutine());
    }
    
    public void RetryRecording() // Botón volver a grabar
    {
        // Limpia y vuelve al inicio
        if (flowCoroutine != null) StopCoroutine(flowCoroutine);
        StopDrawing();
        therapistFullTrajectoryWorld.Clear();
        if (previewLine != null) previewLine.positionCount = 0;
        flowCoroutine = StartCoroutine(RecordingFlowCoroutine());
    }

    // --- Flijo de COROUTINE ---
    private IEnumerator RecordingFlowCoroutine()
    {
        // Cuenta regresiva de 3 segundos
        SetState(DrawState.Countdown);

        float t = countdownSeconds;
        while (t > 0f)
        {
            if (countdownTMP != null)
                countdownTMP.text = $"{Mathf.CeilToInt(t)}"; // Posiciónate en el punto inicial.Grabación inicia en: 

            t -= Time.deltaTime;
            yield return null;
        }

        //  Comenzar a grabar
        SetState(DrawState.Recording);

        StartDrawing();

        // Grabar por 5 segundos (poner tambien una cuenta regresiva)
        float t1 = recordSeconds;
        while (t1 > 0f)
        {
            if (countdownTMP != null)
            {
                tituloTMP.text = $"Grabando trayectoria. \nTiempo restante:  ";
                countdownTMP.text = $"{Mathf.CeilToInt(t1)}"; // 
            }
                

            t1 -= Time.deltaTime;
            yield return null;
        }

        // Dejar de grabar
        StopDrawing();

        // Poner el estado de que ya termino
        SetState(DrawState.Post);

        flowCoroutine = null;
    }


    // --- Funciones internas privadas --- 
    void StartDrawing() 
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

    void StopDrawing() // Botón finalizar de dibujar
    {
        if (!drawing) return;
        drawing = false;
        if (samplingCoroutine != null) StopCoroutine(samplingCoroutine);
        samplingCoroutine = null;

        // Visualizarla
        if (visualizer != null)
            visualizer.BuildSplineFromRecordedTrajectory();
    }

    IEnumerator SampleCoroutine() // Calcula el muestreo de la trayectoria
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

    void UpdatePreviewLine()
    {
        if (previewLine == null) return;

        previewLine.positionCount = therapistFullTrajectoryWorld.Count;
        for (int i = 0; i < therapistFullTrajectoryWorld.Count; i++)
            previewLine.SetPosition(i, therapistFullTrajectoryWorld[i]);
    }

    IEnumerator WaitForSkeletons()
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

    Transform FindPalmTransform()
    {
        if (ovrSkeleton == null || ovrSkeleton.Bones == null) return null;

        foreach (var b in ovrSkeleton.Bones)
        {
            if (b != null && b.Id == boneId)
                return b.Transform;
        }
        return null;
    }
    void SetState(DrawState newState)
    {
        state = newState;

        if (panelIdle != null) panelIdle.SetActive(state == DrawState.Idle);
        if (panelCountdown != null) panelCountdown.SetActive(state == DrawState.Countdown || state == DrawState.Recording);
        if (panelPost != null) panelPost.SetActive(state == DrawState.Post);
    }

    // --- Para leerlo desde el visualizador ---
    public List<Vector3> GetFullTrajectory() 
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
