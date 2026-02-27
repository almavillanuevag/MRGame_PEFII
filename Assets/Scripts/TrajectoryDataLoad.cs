using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections;
using System.Collections.Generic;
using Firebase.Firestore;
using Firebase.Extensions;
using TMPro;

public class TrajectoryDataLoad : MonoBehaviour
{
    [Header("Asignar desde el inspector para interaccíones")]
    public TableInitialPlacement tableInitialPlacement;
    public TextMeshPro noTrajTMP;

    [Header("Modo solo visualizador (opcional)")]
    public bool isVisualizer = false;
    public Material tubeMaterial;

    [Header("Log opcional para pruebas y errores")]
    public TextMeshProUGUI debugText;

    [Header("Variables públicas para lectura")]
    public int knotCount = 15;
    public SplineContainer KnotsSpline;
    public SplineExtrude splineExtrude;
    public float radio;

    public enum LoadState { Idle, Loading, Ready, NoData, Failed }
    public LoadState State { get; private set; } = LoadState.Idle;

    // Variables privadas para funcionamiento interno
    string _lastTraj;
    string _lastPx;
    Coroutine _loadCoroutine;
    FirebaseFirestore db;

    private void Awake()
    {
        db = FirebaseFirestore.DefaultInstance;

        if (KnotsSpline == null) KnotsSpline = GetComponent<SplineContainer>();
        if (splineExtrude == null) splineExtrude = GetComponent<SplineExtrude>();
    }

    private void Start()
    {
        string idPx = SelectPatient.Instance.IDPx;
        string idTraj = SelectPatient.Instance.IDTraj;
        StartCoroutine(LoadingTrajectorySpline(idPx, idTraj));
    }

    private void Update()
    {
        //// Solo para visualizar la trayectoria seleccionada (en el menu inicial del terapeuta)
        if (isVisualizer)
        {
            // Ponerle un color azul
            if (tubeMaterial != null)
            {
                MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                    meshRenderer = gameObject.AddComponent<MeshRenderer>();

                meshRenderer.material = tubeMaterial;
            }

            // Ver si cambio el paciente o la trayectoria
            string traj = SelectPatient.Instance.IDTraj;
            string px = SelectPatient.Instance.IDPx;
            if (!string.IsNullOrEmpty(px) && !string.IsNullOrEmpty(traj))
            {
                if (px != _lastPx || traj != _lastTraj)
                {
                    _lastPx = px;
                    _lastTraj = traj;

                    ClearSplinePreviewVisual();
                    if (_loadCoroutine != null) StopCoroutine(_loadCoroutine);
                    _loadCoroutine = StartCoroutine(LoadingTrajectorySpline(px, traj));
                }
            }
            return;
        }
    }

    public void ClearSplinePreviewVisual()
    {
        if (KnotsSpline != null)
        {
            var spline = KnotsSpline.Spline; 
            spline.Clear();
        }

        if (splineExtrude != null) 
        { 
            splineExtrude.enabled = false; 
            splineExtrude.Rebuild(); 
        }
        var mr = GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;

        State = LoadState.Idle;
    }

    IEnumerator LoadingTrajectorySpline(string idPx, string idTraj)
    {
        // Esperar a que exista SelectPatient y que ya haya selección válida
        while (SelectPatient.Instance == null)
            yield return null;

        // Si no hay trayectorias desplegar un mensaje en el UI
        if(string.IsNullOrEmpty(SelectPatient.Instance.IDTraj))
        {
            EnterNoTrajectoriesMode();
        }

        while (string.IsNullOrEmpty(SelectPatient.Instance.IDPx))
            yield return null;

        while (string.IsNullOrEmpty(SelectPatient.Instance.IDTraj))
            yield return null;

        if (idPx == null || idTraj == null)
        {
            idPx = SelectPatient.Instance.IDPx;
            idTraj = SelectPatient.Instance.IDTraj;
        }

        State = LoadState.Loading;

        HideNoTrajMessage();

        Log($"Cargando trayectoria seleccionada: {idTraj} (Paciente: {idPx})");

        DocumentReference docRef =
            db.Collection("Pacientes")
              .Document(idPx)
              .Collection("Trayectorias")
              .Document(idTraj);

        docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Log("ERROR al leer trayectoria: " + task.Exception);
                State = LoadState.Failed;
                return;
            }

            var snap = task.Result;
            if (!snap.Exists)
            {
                Log("ERROR: No existe el documento de trayectoria.");
                State = LoadState.Failed;
                return;
            }

            // Leer el radio guardado
            double savedRadiusD = 0.0;
            bool hasRadius = snap.TryGetValue("Radio", out savedRadiusD);

            if (hasRadius && splineExtrude != null)
            {
                float savedRadius = (float)savedRadiusD;

                // Clamp de seguridad
                savedRadius = Mathf.Clamp(savedRadius, 0.001f, 0.5f);

                splineExtrude.Radius = savedRadius;
                radio = savedRadius;

                Log($"Radio cargado desde Firestore: {savedRadius:0.000} m");
            }
            else
            {
                // Si no hay radio (trayectorias viejas), mantiene el default actual
                if (splineExtrude != null) radio = splineExtrude.Radius;
                Log("Aviso: No se encontró 'Radio' en Firestore. Usando radio por defecto.");
            }

            // Leer el vector de trayectoria
            List<object> raw;
            if (!snap.TryGetValue("TrayectoriaCompleta", out raw))
            {
                // fallback por si cambiaste el nombre del campo
                if (!snap.TryGetValue("TherapistFullTrajectory", out raw))
                {
                    Log("ERROR: No se encontró 'TrayectoriaCompleta' ni 'TherapistFullTrajectory'.");
                    return;
                }
            }

            // validar su longitud
            List<Vector3> fullWorld = ParsePoints(raw);
            if (fullWorld == null || fullWorld.Count < 2)
            {
                State = LoadState.Failed;
                Log("ERROR: Trayectoria insuficiente para construir spline.");
                return;
            }

            // Visualizar el line render de la trayectoria completa  --------------------------------------------- ** PENDIENTE!
            //CreateTrajectoryLineRender(fullWorld);

            List<Vector3> resampled = ResampleByArcLength(fullWorld, knotCount);
            BuildSplineFromWorldKnots(resampled);

            if (splineExtrude != null)
            {
                splineExtrude.enabled = true;
                splineExtrude.Rebuild();
                radio = splineExtrude.Radius;
            }

            Log($"Spline cargada Puntos: {fullWorld.Count} | Knots: {resampled.Count}");
            StartCoroutine(PlaceOnTableAfterSplineReady());
            var mr = GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = true;
            State = LoadState.Ready;
        });

        yield break;
    }

    void EnterNoTrajectoriesMode()
    {
        // Mensaje en UI
        if (noTrajTMP != null)
        {
            noTrajTMP.gameObject.SetActive(true);
            noTrajTMP.text = "No hay trayectorias, crear una nueva";
        }

        // Importante: NO mostrar tubo
        if (splineExtrude != null) splineExtrude.enabled = false;

        // Oculta renderer del tubo (por si hay uno)
        var mr = GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;

        // AUN ASÍ, posiciona el spline “default” en mesa (y con eso nave/planeta si dependen de tu TableInitialPlacement)
        StartCoroutine(PlaceOnTableAfterSplineReady());

        State = LoadState.NoData;
    }

    void HideNoTrajMessage()
    {
        if (noTrajTMP != null) noTrajTMP.gameObject.SetActive(false);
    }

    IEnumerator PlaceOnTableAfterSplineReady()
    {
        // Esperar a que Unity actualice transforms y bounds del extrude
        yield return null;
        yield return new WaitForEndOfFrame();

        if (tableInitialPlacement == null)
            tableInitialPlacement = GetComponent<TableInitialPlacement>();

        if (tableInitialPlacement != null)
            tableInitialPlacement.SetTrajectoryOnTable();
        else
            Log("ERROR: No existe TableInitialPlacement en este GameObject.");
    }

    List<Vector3> ParsePoints(List<object> raw)
    {
        List<Vector3> pts = new List<Vector3>(raw.Count);

        foreach (var elem in raw)
        {
            if (elem is Dictionary<string, object> map)
            {
                float x = ToFloat(map, "x");
                float y = ToFloat(map, "y");
                float z = ToFloat(map, "z");
                pts.Add(new Vector3(x, y, z));
            }
        }
        return pts;
    }

    float ToFloat(Dictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out object val) || val == null) return 0f;
        if (val is double d) return (float)d;
        if (val is float f) return f;
        if (val is long l) return l;
        if (val is int i) return i;

        if (float.TryParse(val.ToString(), out float parsed)) return parsed;
        return 0f;
    }

    List<Vector3> ResampleByArcLength(List<Vector3> pts, int targetCount)
    {
        List<Vector3> result = new List<Vector3>();
        if (pts == null || pts.Count < 2) return result;
        if (targetCount < 2) targetCount = 2;

        float[] cum = new float[pts.Count];
        cum[0] = 0f;
        for (int i = 1; i < pts.Count; i++)
            cum[i] = cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);

        float total = cum[cum.Length - 1];
        if (total < 1e-6f)
        {
            result.Add(pts[0]);
            result.Add(pts[pts.Count - 1]);
            return result;
        }

        for (int k = 0; k < targetCount; k++)
        {
            float d = total * k / (targetCount - 1);

            int idx = 0;
            while (idx < cum.Length - 1 && cum[idx + 1] < d) idx++;

            float segStart = cum[idx];
            float segEnd = cum[idx + 1];
            float alpha = (segEnd > segStart) ? (d - segStart) / (segEnd - segStart) : 0f;

            result.Add(Vector3.Lerp(pts[idx], pts[idx + 1], alpha));
        }

        return result;
    }

    void BuildSplineFromWorldKnots(List<Vector3> knotsWorld)
    {
        if (KnotsSpline == null) KnotsSpline = GetComponent<SplineContainer>();

        var spline = KnotsSpline.Spline;
        spline.Clear();

        for (int i = 0; i < knotsWorld.Count; i++)
        {
            Vector3 local = KnotsSpline.transform.InverseTransformPoint(knotsWorld[i]);
            float3 posL = new float3(local.x, local.y, local.z);
            spline.Add(new BezierKnot(posL, float3.zero, float3.zero, quaternion.identity));
        }

        try
        {
            for (int i = 0; i < spline.Count; i++)
                spline.SetTangentMode(i, TangentMode.AutoSmooth);
        }
        catch { }
    }

    void Log(string msg)
    {
        Debug.Log(msg);
        if (debugText != null) debugText.text += "\n" + msg;
    }
}
