using Firebase.Extensions;
using Firebase.Firestore;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

using Vector3 = UnityEngine.Vector3;

public class DrawNewTrajectory : MonoBehaviour 
{
    [Header("Asignar elementos para interacciones")]
    public GameObject debugText;
    public VisualizeTrajectorySpline visualizer;
    public Transform brushPoint; // punto donde dibujas
    public TrajectoryManager previewSpline; // Spline para visualizar

    [Header("UI Flujo Grabación")]
    public GameObject panelIdle;        // Botón "Iniciar"
    public GameObject panelWaiting;     // Texto de "Toma el pincel para comenzar a dibujar"
    public GameObject panelCountdown;   // Texto 3..2..1
    public GameObject panelPost;        // Slider + Guardar/Regrabar

    public TextMeshProUGUI countdownTMP;
    public TextMeshProUGUI tituloTMP;

    [Header("Objetos publicos para lectura")]
    // Listas de los puntos
    public List<Vector3> therapistFullTrajectoryWorld = new List<Vector3>();
    public float radius;
    public string IDTraj;

    // Para el muestreo
    float sampleInterval = 1/60f;
    float minDistance = 0.01f;
    bool drawing = false;
    Vector3 lastSamplePos;
    Coroutine samplingCoroutine;
    FirebaseFirestore db;
    bool isHolding = false;
    Vector3 initialBrushPos;
    Quaternion initialBrushRot;

    // Para el render de la linea
    LineRenderer previewLine;
    float lineWidth = 0.005f;

    // Para iniciar las grabaciones UI
    float countdownSeconds = 3f;
    float recordSeconds = 5f;
    Coroutine flowCoroutine;
    enum DrawState { Idle, Waiting, Countdown, Recording, Post }
    DrawState state = DrawState.Idle;
    


    private void Start() 
    {
        // Inicializar firestore
        db = FirebaseFirestore.DefaultInstance;

        // Configuracion del line render
        SetupLineRenderer();

        // Definir el estado de inactivo
        SetState(DrawState.Idle);
        gameObject.GetComponentInChildren<MeshRenderer>().enabled = false;

        // Ver el spline extrude del preview spline
        previewSpline.splineExtrude.enabled=true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("HandCollider")) return;
        if (isHolding) return;

        isHolding = true;

        // Para comenzar a grabar si ya sujeto el pincel en el estado Waiting
        if (state == DrawState.Waiting)
        {
            // Si ya hay un flujo corriendo, lo cancelamos
            if (flowCoroutine != null) StopCoroutine(flowCoroutine);
            flowCoroutine = StartCoroutine(RecordingFlowCoroutine());
        }
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
                // Deshabilitar el pincel
                gameObject.GetComponentInChildren<MeshRenderer>().enabled = false;

                // Resetear el vector de la trayectoria si ya se guardó
                therapistFullTrajectoryWorld.Clear();
                UpdatePreviewLine();

                if (SelectPatient.Instance != null)
                {
                    SelectPatient.Instance.IDTraj = IDTraj;
                    SelectPatient.Instance.LoadingTrajectories(IDTraj);
                }

                SetState(DrawState.Idle);
            }
            else
                Log("ERROR guardando Firestore: " + task.Exception);
        });

        // Dejar de ver el spline extrude de del NEWTrajectory (la que dibujaron) y ver la de PreviewTrajectory (la que lee desde firestore)
        visualizer.splineExtrude.enabled = false;
        previewSpline.splineExtrude.enabled = true;
    } 

    public void StartRecordingFlow() // Botón iniciar grabación
    {
        SetState(DrawState.Waiting);
        isHolding = false;

        // Almacenar la posicion inicial del pincel
        initialBrushPos = transform.position;
        initialBrushRot = transform.rotation;

        // Activar el pincel
        gameObject.GetComponentInChildren<MeshRenderer>().enabled = true;

        // Borrar la previsualizacion del spline pasado
        previewSpline.ClearSplinePreviewVisual();

    }
    
    public void RetryRecording() // Botón volver a grabar
    {
        // Limpia y vuelve al inicio
        if (flowCoroutine != null) StopCoroutine(flowCoroutine);
        StopDrawing();
        therapistFullTrajectoryWorld.Clear();
        if (previewLine != null) previewLine.positionCount = 0;
        visualizer.splineExtrude.enabled = false;

        // Resetear pincel a la posicion inicial
        transform.SetPositionAndRotation(initialBrushPos, initialBrushRot);
        SetState(DrawState.Waiting);
        isHolding = false;
    }

    public void CancelRecording() // Botón volver a grabar
    {
        // Limpia y vuelve al inicio
        if (flowCoroutine != null) StopCoroutine(flowCoroutine);
        StopDrawing();
        therapistFullTrajectoryWorld.Clear();
        if (previewLine != null) previewLine.positionCount = 0;
        visualizer.splineExtrude.enabled = false;

        transform.SetPositionAndRotation(initialBrushPos, initialBrushRot);
        SetState(DrawState.Idle);

        // Resetear pincel a la posicion inicial y hacerlo invisible
        gameObject.GetComponentInChildren<MeshRenderer>().enabled = false;
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

        therapistFullTrajectoryWorld.Clear();
        drawing = true;

        if (previewLine != null)
            previewLine.positionCount = 0;

        lastSamplePos = brushPoint.position;

        // Guardar el primer punto para que siempre incluya inicio
        therapistFullTrajectoryWorld.Add(lastSamplePos);
        UpdatePreviewLine();

        if (samplingCoroutine != null) StopCoroutine(samplingCoroutine);
        samplingCoroutine = StartCoroutine(SampleCoroutine());
    }

    IEnumerator SampleCoroutine() // Calcula el muestreo de la trayectoria
    {
        while (drawing)
        {
            Vector3 pos = brushPoint.position;
            if (Vector3.Distance(pos, lastSamplePos) >= minDistance)
            {
                therapistFullTrajectoryWorld.Add(pos);
                lastSamplePos = pos;
                UpdatePreviewLine();
            }
            yield return new WaitForSeconds(sampleInterval);
        }
    }

    void StopDrawing() // Botón finalizar de dibujar
    {
        if (!drawing) return;
        drawing = false;
        if (samplingCoroutine != null) StopCoroutine(samplingCoroutine);
        samplingCoroutine = null;

        // Visualizarla
        if (visualizer != null)
            visualizer.BuildSplineFromRecordedTrajectory(therapistFullTrajectoryWorld);

        isHolding = false;

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
        mat.color = Color.cyan;

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

    void SetState(DrawState newState)
    {
        state = newState;

        if (panelIdle != null) panelIdle.SetActive(state == DrawState.Idle);
        if (panelWaiting != null) panelWaiting.SetActive(state == DrawState.Waiting);
        if (panelCountdown != null) panelCountdown.SetActive(state == DrawState.Countdown || state == DrawState.Recording);
        if (panelPost != null) panelPost.SetActive(state == DrawState.Post);
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
