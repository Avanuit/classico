using System.Collections.Generic;
using UnityEngine;

// Animación de correr por código para modelos SIN animaciones: balancea piernas y brazos según la velocidad.
// Si el modelo tiene un Animator con controlador, este script no hace nada.
// Funciona por NOMBRE de hueso (thigh/upleg, calf/leg, arm, forearm). Revisa la Console si algo no se mueve.
public class ProceduralRun : MonoBehaviour
{
    public PlayerAI ai;
    [HideInInspector] public int stance;      // 0 libre, 1 fusil, 2 pistola, 3 granada (lo escribe PlayerCombat)
    [Tooltip("Actívalo si el modelo está en T-pose (brazos extendidos a los lados)")]
    public bool tPoseArmsDown = false;
    public float armDownAngle = 70f;
    public float legSwing = 45f, armSwing = 35f, kneeBend = 55f, cadence = 3.1f;

    class Bone { public Transform t; public Quaternion initLocal; public int kind; public float side; }
    readonly List<Bone> bones = new List<Bone>();
    float phase;
    Animator anim;

    void Start()
    {
        anim = GetComponentInChildren<Animator>();
        Vector3 right = Vector3.Cross(Vector3.up, ai != null ? ai.FaceDir : transform.forward);
        foreach (Transform t in GetComponentsInChildren<Transform>())
        {
            string n = t.name.ToLower();
            if (n.Contains("armature")) continue;
            int kind = -1;
            if (n.Contains("upleg") || n.Contains("thigh") || n.Contains("upperleg") || n.Contains("upper_leg")) kind = 0;
            else if (n.Contains("calf") || n.Contains("shin") || n.Contains("lowerleg") || n.Contains("lower_leg") || n.Contains("leg")) kind = 1;
            else if (n.Contains("forearm") || n.Contains("lowerarm") || n.Contains("lower_arm")) kind = 3;
            else if (n.Contains("upperarm") || n.Contains("upper_arm") || n.Contains("arm")) kind = 2;
            if (kind < 0) continue;
            Bone b = new Bone();
            b.t = t; b.initLocal = t.localRotation; b.kind = kind;
            b.side = Mathf.Sign(Vector3.Dot(t.position - transform.position, right));
            bones.Add(b);
        }
        if (bones.Count == 0) Debug.LogWarning("ProceduralRun: no encontré huesos de piernas/brazos en " + name);
    }

    void LateUpdate()
    {
        if (ai == null || bones.Count == 0) return;
        if (anim != null && anim.runtimeAnimatorController != null) return;

        float v = ai.Velocity.magnitude;
        float k = Mathf.Clamp01(v / 5f);
        phase += v * cadence * Time.deltaTime;
        Vector3 fwd = ai.FaceDir;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);

        for (int i = 0; i < bones.Count; i++)
        {
            Bone b = bones[i];
            b.t.localRotation = b.initLocal;
            float ph = phase + (b.side > 0f ? 0f : Mathf.PI);
            float sw = Mathf.Sin(ph) * k;

            if (b.kind == 2 && tPoseArmsDown)
                b.t.rotation = Quaternion.AngleAxis(armDownAngle * (b.side > 0f ? -1f : 1f), fwd) * b.t.rotation;

            float ang = 0f, sideAng = 0f;
            bool rightArm = b.side > 0f;
            switch (b.kind)
            {
                case 0: ang = -sw * legSwing; break;
                case 1: ang = Mathf.Max(0f, Mathf.Sin(ph - 1.2f)) * kneeBend * k; break;
                case 2:
                    ang = sw * armSwing;
                    if (stance == 1) ang = rightArm ? -78f : -62f;          // fusil: ambos brazos al frente
                    else if (stance == 2) ang = rightArm ? -80f : -25f;     // pistola: brazo derecho extendido
                    else if (stance == 3) ang = rightArm ? -35f : sw * armSwing * 0.5f;
                    else if (stance == 4) { ang = 0f; sideAng = rightArm ? 155f : -155f; }                 // campeón: brazos en V
                    else if (stance == 5) { if (rightArm) ang = -165f; else { ang = 0f; sideAng = -28f; } }  // puño derecho arriba
                    else if (stance == 6) { if (!rightArm) ang = -165f; else { ang = 0f; sideAng = 28f; } }  // puño izquierdo arriba
                    break;
                case 3:
                    ang = -40f * k;
                    if (stance == 1) ang = rightArm ? -12f : -38f;
                    else if (stance == 2) ang = rightArm ? -6f : -30f;
                    else if (stance == 3 && rightArm) ang = -50f;
                    else if (stance >= 4) ang = 0f;
                    break;
            }
            if (Mathf.Abs(ang) > 0.01f) b.t.rotation = Quaternion.AngleAxis(ang, right) * b.t.rotation;
            if (Mathf.Abs(sideAng) > 0.01f) b.t.rotation = Quaternion.AngleAxis(sideAng, fwd) * b.t.rotation;
        }
    }
}
