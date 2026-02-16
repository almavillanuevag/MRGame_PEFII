using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.UI;

public class VisualizeTrajectorySpline : MonoBehaviour
{
    [Header("Asignar elementos para interacciones")]
    public TextMeshProUGUI debugText;
    public Slider radiusSlider;
    public TextMeshProUGUI radiusLabel;
    public Material tubeMaterial;

    [Header("Variables publicas para lectura")]
    public SplineContainer splineContainer;
    public SplineExtrude splineExtrude;
    public float tubeRadius = 0.05f; // valor default

    // --- Variables para funcionamiento interno --
    int knotCount = 15;


    private void Start()
    {
        SetupRadiusSlider();
        InitializeSplineComponents();
    }

    private void SetupRadiusSlider()
    {
        if (radiusSlider == null) return;

        radiusSlider.minValue = 0.05f;
        radiusSlider.maxValue = 0.2f;

        // Valor inicial del slider = radio actual
        radiusSlider.value = tubeRadius;

        // Evitar listeners duplicados
        radiusSlider.onValueChanged.RemoveListener(SetRadiusFromSlider);
        radiusSlider.onValueChanged.AddListener(SetRadiusFromSlider);

        if (radiusLabel != null)
            radiusLabel.text = $"Radio: {tubeRadius*100:F3} cm";
    }

    private void InitializeSplineComponents()
    {
        // Asegurar que existe SplineContainer
        splineContainer = gameObject.GetComponent<SplineContainer>();
        if (splineContainer == null)
        {
            splineContainer = gameObject.AddComponent<SplineContainer>();
            Log("SplineContainer creado automáticamente");
        }

        // Asegurar que existe SplineExtrude
        splineExtrude = gameObject.GetComponent<SplineExtrude>();
        if (splineExtrude == null)
        {
            splineExtrude = gameObject.AddComponent<SplineExtrude>();
            Log("SplineExtrude creado automáticamente");
        }

        // Configurar SplineExtrude
        splineExtrude.Container = splineContainer;
        splineExtrude.Radius = tubeRadius;
        splineExtrude.Sides = 12; 
        splineExtrude.SegmentsPerUnit = 15; 
        splineExtrude.Capped = true; 
        splineExtrude.enabled = false; // Deshabilitar hasta que se cree el spline

        // Asignar material si está disponible
        if (tubeMaterial != null)
        {
            MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = gameObject.AddComponent<MeshRenderer>();

            meshRenderer.material = tubeMaterial;
        }
        else Log("ADVERTENCIA: No hay material asignado. El tubo puede no ser visible.");
    }

    public void SetRadiusFromSlider(float value)
    {
        tubeRadius = value;
        UpdateVisuals();
        if (radiusLabel != null)
            radiusLabel.text = $"Radio: {value*100:F3} cm";
    }
    
    public void BuildSplineFromRecordedTrajectory(List<Vector3> fullTrajectory) // Llamar esto al terminar la grabación
    {
        List<Vector3> full = fullTrajectory;
        if (full == null || full.Count < 2)
        {
            Log("Trayectoria insuficiente para generar spline.");
            return;
        }

        List<Vector3> resampled = ResampleByArcLength(full, knotCount);
        BuildSpline(resampled);

        // Habilitar el componente de extrude 
        splineExtrude.enabled = true;

        UpdateVisuals();
    }

    void BuildSpline(List<Vector3> knotsWorld)
    {
        var spline = splineContainer.Spline;
        spline.Clear();

        foreach (Vector3 worldPt in knotsWorld)
        {
            Vector3 local = splineContainer.transform.InverseTransformPoint(worldPt);
            float3 posL = new float3(local.x, local.y, local.z);

            BezierKnot knot = new BezierKnot(
                posL,
                float3.zero,
                float3.zero,
                quaternion.identity
            );

            spline.Add(knot);
        }

        // Suavizado automático
        try
        {
            for (int i = 0; i < spline.Count; i++)
                spline.SetTangentMode(i, TangentMode.AutoSmooth);
        }
        catch { }
    }

    void UpdateVisuals()
    {
        if (splineExtrude != null)
        {
            splineExtrude.Radius = tubeRadius;
            splineExtrude.Rebuild();
        }
        else Log("Error al obtener el splineExtrude == null");
    }

    List<Vector3> ResampleByArcLength(List<Vector3> pts, int targetCount)
    {
        List<Vector3> result = new List<Vector3>();
        if (pts.Count < 2) return result;

        float[] cumulative = new float[pts.Count];
        cumulative[0] = 0f;

        for (int i = 1; i < pts.Count; i++)
            cumulative[i] = cumulative[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);

        float totalLength = cumulative[cumulative.Length - 1];
        if (totalLength <= Mathf.Epsilon) return result;

        for (int k = 0; k < targetCount; k++)
        {
            float d = totalLength * k / (targetCount - 1);

            int i = 0;
            while (i < cumulative.Length - 1 && cumulative[i + 1] < d)
                i++;

            float segLen = cumulative[i + 1] - cumulative[i];
            float t = (segLen > 0f) ? (d - cumulative[i]) / segLen : 0f;

            Vector3 p = Vector3.Lerp(pts[i], pts[i + 1], t);
            result.Add(p);
        }

        return result;
    }

    private void Log(string msg)
    {
        Debug.Log(msg);
        if (debugText != null) debugText.text += "\n" + msg;
    }
}
