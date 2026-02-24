using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class SplineArrowFollow : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Trayectoria Spline a seguir")]
    public SplineContainer _currentSpline; 

    [Tooltip("Prefab de la flecha. Debe tener el componente SplineAnimate de Unity Splines.")]
    public GameObject arrowPrefab;
    public Material arrowMaterial;

    // Variales privadas de funcionamiento interno
    float minOffsetBetweenArrows = 0.04f;
    float arrowSpeed;
    
    readonly List<GameObject> _spawnedArrows = new List<GameObject>();
    readonly List<SplineAnimate> _animators = new List<SplineAnimate>();

    public void LoadSpline(SplineContainer splineContainer)
    {
        if (splineContainer == null)
        {
            Debug.LogWarning("[SplineArrowManager] SplineContainer es null");
            return;
        }

        _currentSpline = splineContainer;
        ClearArrows();
        SpawnArrows();
    }

    public void SetPaused(bool paused) // Pausa o reanuda todas las flechas
    {
        foreach (SplineAnimate anim in _animators)
        {
            if (anim == null) continue;
            if (paused) anim.Pause();
            else anim.Play();
        }
    }

    public void SetSpeed(float speed) // Cambia la velocidad en tiempo de ejecución
    {
        arrowSpeed = speed;
        foreach (SplineAnimate anim in _animators)
        {
            if (anim == null) continue;
            anim.MaxSpeed = speed;
        }
    }

    public void SetColor(Material material) // Cambia el color de todas las flechas existentes en tiempo de ejecución.
    {
        arrowMaterial = material;
        foreach (GameObject arrow in _spawnedArrows)
        {
            if (arrow != null) ApplyMaterialToArrow(arrow);
        }
    }

    private void SpawnArrows()
    {
        if (arrowPrefab == null)
        {
            Debug.LogError("[SplineArrowManager] arrowPrefab no asignado en el inspector.");
            return;
        }

        // 1. Longitud real del spline en unidades de mundo 
        float splineLength = _currentSpline.CalculateLength();

        // 2. Cuántas flechas caben con el offset mínimo deseado 
        //    El offset es normalizado [0,1], por lo que el número de flechas es:
        //    count = floor(1 / minOffset)
        //    Con minOffset=0.03 33 flechas para cualquier longitud de spline.
        //    La separación REAL entre flechas será: splineLength / arrowCount
        int arrowCount = Mathf.Max(1, Mathf.FloorToInt(1f / minOffsetBetweenArrows));

        //  3. Spacing normalizado exacto para distribución perfecta 
        //    Dividimos [0,1] en arrowCount partes iguales.
        float normalizedSpacing = 1f / arrowCount;


        // 4. Instanciar cada flecha con su offset inicial 
        for (int i = 0; i < arrowCount; i++)
        {
            float startOffset = normalizedSpacing * i;   // 0, 0.03, 0.06 ...
            InstantiateArrow(i, startOffset);
        }
    }

    private void InstantiateArrow(int index, float normalizedStartOffset)
    {
        // Instanciar como hijo para mantener la jerarquía ordenada
        GameObject arrow = Instantiate(arrowPrefab, transform);
        arrow.name = $"Arrow_{index:D3}";

        //  Obtener SplineAnimate (el componente del prefab) 
        SplineAnimate splineAnimate = arrow.GetComponent<SplineAnimate>();
        if (splineAnimate == null)
        {
            Debug.LogWarning($"[SplineArrowManager] '{arrowPrefab.name}' no tiene SplineAnimate. ");
        }

        //  Configurar SplineAnimate 
        splineAnimate.Container = _currentSpline;

        splineAnimate.NormalizedTime = normalizedStartOffset;

        // Arrancar la animación
        splineAnimate.Play();

        // material 
        ApplyMaterialToArrow(arrow);

        _spawnedArrows.Add(arrow);
        _animators.Add(splineAnimate);
    }

    private void ApplyMaterialToArrow(GameObject arrow)
    {
        if (arrowMaterial == null)
        {
            Debug.LogWarning("[SplineArrowManager] arrowMaterial no asignado. Se mantiene el material del prefab.");
            return;
        }

        Renderer[] renderers = arrow.GetComponentsInChildren<Renderer>(includeInactive: true);
        foreach (Renderer rend in renderers)
        {
            rend.sharedMaterial = arrowMaterial;
        }
    }

    private void ClearArrows()
    {
        foreach (GameObject arrow in _spawnedArrows)
        {
            if (arrow != null) Destroy(arrow);
        }
        _spawnedArrows.Clear();
        _animators.Clear();
    }

    private void OnDestroy() => ClearArrows();

}