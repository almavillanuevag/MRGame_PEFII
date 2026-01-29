using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

public class FirebaseAdministrator : MonoBehaviour
{
    FirebaseFirestore db;

    string IDPx = SelectPatient.Instance.IDPx;
    public void Start()
    {
        db = FirebaseFirestore.DefaultInstance; // Inicializar firestore
    }

    // ver si mantengo esta clase
}
