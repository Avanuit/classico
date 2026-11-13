using UnityEngine;

// Se agrega solo desde MatchSetup si el prefab de la pelota no lo trae.
[RequireComponent(typeof(Rigidbody))]
public class BallSetup : MonoBehaviour
{
    void Awake()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.mass = 0.43f;
        rb.drag = 0.2f;
        rb.angularDrag = 0.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        // Speculative funciona tanto con el balón libre como cuando está en pausa (kinematic)
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            PhysicMaterial mat = new PhysicMaterial("Ball");
            mat.bounciness = 0.55f; mat.dynamicFriction = 0.4f; mat.staticFriction = 0.4f;
            mat.bounceCombine = PhysicMaterialCombine.Maximum;
            col.material = mat;
        }
    }
}
