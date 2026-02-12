using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections;
using System.Collections.Generic;
using Firebase.Firestore;
using Firebase.Extensions;

public class TrajectoryManager : MonoBehaviour
{
    [Header("Asignar elementos para interacciones")]
    public Transform Ship;
    public TMPro.TextMeshProUGUI debugText;
    public TableInitialPlacement tableInitialPlacement;
    public Material tubeMaterial;

    [Header("Spline Visual")]
    public int knotCount = 15;
    public SplineContainer KnotsSpline;
    public SplineExtrude splineExtrude;
    public bool isVisualizer = false;

    [Header("Tubo (juego)")]
    public float radio;
    public float distance = 0;
    public float erroresMax = 3f;
    public float radioMax = 0.2f;
    public float radioSUM = 0.01f;
    public float errors = 0;
    public float TotalError = -1;

    bool OutOfTube = false;
    Color Green = new Color(0, 1, 0, 0.5f);
    Color Red = new Color(1f, 0, 0, 0.5f);
    string _lastTraj;
    string _lastPx;
    private Coroutine _loadCoroutine;

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
        if (KnotsSpline == null || splineExtrude == null || Ship == null) return;


        radio = splineExtrude.Radius;


        // Para cuando solo quieras visualizar la trayectoria seleccionada
        if (isVisualizer)
        {
            // Ponerle un color azulin
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

        // Gameplay 
        

        distance = ShipDistanceFromSpline(Ship.position, KnotsSpline);

        if (distance <= radio)
        {
            GetComponent<MeshRenderer>().material.color = Green;
            if (OutOfTube) OutOfTube = false;
        }
        else
        {
            GetComponent<MeshRenderer>().material.color = Red;

            if (!OutOfTube)
            {
                errors++;
                TotalError++;

                if (errors >= erroresMax && radio <= radioMax)
                {
                    splineExtrude.Radius += radioSUM;
                    radio = splineExtrude.Radius;
                    splineExtrude.Rebuild();
                    errors = 0;
                }
                OutOfTube = true;
            }
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
    }

    IEnumerator LoadingTrajectorySpline(string idPx, string idTraj)
    {
        // Esperar a que exista SelectPatient y que ya haya selección válida
        while (SelectPatient.Instance == null)
            yield return null;

        while (string.IsNullOrEmpty(SelectPatient.Instance.IDPx))
            yield return null;

        while (string.IsNullOrEmpty(SelectPatient.Instance.IDTraj))
            yield return null;

        if (idPx == null || idTraj== null) 
        {
            idPx = SelectPatient.Instance.IDPx;
            idTraj = SelectPatient.Instance.IDTraj;
        }

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
                return;
            }

            var snap = task.Result;
            if (!snap.Exists)
            {
                Log("ERROR: No existe el documento de trayectoria.");
                return;
            }

            // Leer el radio guardado
            double savedRadiusD = 0.0;
            bool hasRadius = snap.TryGetValue("Radio", out savedRadiusD);

            if (hasRadius && splineExtrude != null)
            {
                float savedRadius = (float)savedRadiusD;

                // (Opcional pero recomendado) Clamp de seguridad
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

            List<Vector3> fullWorld = ParsePoints(raw);
            if (fullWorld == null || fullWorld.Count < 2)
            {
                Log("ERROR: Trayectoria insuficiente para construir spline.");
                return;
            }

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
        });

        yield break;
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

    float ShipDistanceFromSpline(Vector3 shipPos, SplineContainer splineContainer)
    {
        float steps = 100f;
        float minDist = Mathf.Infinity;

        for (float i = 0; i <= steps; i++)
        {
            float t = i / steps;
            float3 p = splineContainer.EvaluatePosition(t);
            Vector3 splineDot = new Vector3(p.x, p.y, p.z);

            float d = Vector3.Distance(shipPos, splineDot);
            if (d < minDist) minDist = d;
        }

        return minDist;
    }

    void Log(string msg)
    {
        Debug.Log(msg);
        if (debugText != null) debugText.text += "\n" + msg;
    }
}
