using UnityEngine;
using TMPro;

public class SelectMenuUI : MonoBehaviour
{
    [Header("Asignar dropdowns para interacciones")]
    public TMP_Dropdown patientsDropdown;
    public TMP_Dropdown trajectoriesDropdown;
    public TextMeshProUGUI debugText;

    bool reloadOnBind = true;
    // Reasignar las referencias de UI a la instancia existente de SelectPatient en el cambio de escena
    private void OnEnable() 
    {
        Bind();
    }

    public void Bind()
    {
        if (SelectPatient.Instance == null) return;

        // Reasignar referencias de UI a la instancia persistente
        SelectPatient.Instance.PatientsDropdown = patientsDropdown;
        SelectPatient.Instance.TrajectoriesDropdown = trajectoriesDropdown;
        SelectPatient.Instance.debugText = debugText;

        // Forzar recarga de datos para repintar dropdowns al volver al menú
        if (reloadOnBind)
            SelectPatient.Instance.LoadingPatients();
    }
}
