using System.Collections.Generic;
using UnityEngine;

// Pinta la camiseta SIN depender de la textura: separa los triángulos del torso y los brazos
// (según los pesos de los huesos) en un segundo material con el color del equipo.
// Requisitos: el mesh debe tener Read/Write activado y el rig huesos con nombres tipo spine/chest/arm.
public static class ShirtMeshSplit
{
    static readonly Dictionary<Mesh, Mesh> splitCache = new Dictionary<Mesh, Mesh>();
    static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();

    // huesos que forman la camiseta (torso + parte alta del brazo) y los que NO
    static readonly string[] include = { "spine", "chest", "clavicle", "shoulder", "upperarm", "upper_arm", "arm", "torso" };
    static readonly string[] exclude = { "armature", "forearm", "lowerarm", "lower_arm", "hand", "head", "neck", "hip", "pelvis", "leg", "thigh", "foot", "toe", "finger", "thumb" };

    static bool IsShirtBone(string lower)
    {
        foreach (string e in exclude) if (lower.Contains(e)) return false;
        foreach (string i in include) if (lower.Contains(i)) return true;
        return false;
    }

    public static bool Apply(GameObject player, Color color)
    {
        bool any = false;
        foreach (SkinnedMeshRenderer smr in player.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            Mesh src = smr.sharedMesh;
            if (src == null) continue;
            Mesh split;
            if (!splitCache.TryGetValue(src, out split))
            {
                split = Split(smr, src);
                splitCache[src] = split;
            }
            if (split == null) continue;
            Material[] mats = smr.sharedMaterials;
            if (mats.Length < 1) continue;
            smr.sharedMesh = split;
            smr.sharedMaterials = new Material[] { mats[0], GetShirtMaterial(mats[0], color) };
            any = true;
        }
        return any;
    }

    static Mesh Split(SkinnedMeshRenderer smr, Mesh src)
    {
        if (!src.isReadable)
        {
            Debug.LogError("ShirtMeshSplit: el mesh '" + src.name + "' no es legible. Selecciona el modelo del jugador en Project, " +
                           "marca 'Read/Write Enabled' en el Inspector y pulsa Apply.");
            return null;
        }
        if (src.subMeshCount != 1)
        {
            Debug.LogWarning("ShirtMeshSplit: el mesh tiene " + src.subMeshCount + " submeshes; se usará el método de textura.");
            return null;
        }
        Transform[] bones = smr.bones;
        bool[] isShirt = new bool[bones.Length];
        int count = 0;
        List<string> names = new List<string>();
        for (int i = 0; i < bones.Length; i++)
        {
            string n = bones[i] != null ? bones[i].name : "(null)";
            names.Add(n);
            if (bones[i] != null && IsShirtBone(n.ToLower())) { isShirt[i] = true; count++; }
        }
        if (count == 0)
        {
            Debug.LogWarning("ShirtMeshSplit: no encontré huesos de torso/brazos. Huesos del modelo: " + string.Join(", ", names.ToArray()));
            return null;
        }
        BoneWeight[] bw = src.boneWeights;
        if (bw.Length != src.vertexCount) return null;

        bool[] shirtV = new bool[src.vertexCount];
        int shirtVerts = 0;
        for (int v = 0; v < bw.Length; v++)
        {
            float w = 0f;
            if (Ok(bw[v].boneIndex0, isShirt)) w += bw[v].weight0;
            if (Ok(bw[v].boneIndex1, isShirt)) w += bw[v].weight1;
            if (Ok(bw[v].boneIndex2, isShirt)) w += bw[v].weight2;
            if (Ok(bw[v].boneIndex3, isShirt)) w += bw[v].weight3;
            if (w > 0.5f) { shirtV[v] = true; shirtVerts++; }
        }
        if (shirtVerts == 0) return null;

        int[] tris = src.GetTriangles(0);
        List<int> shirtT = new List<int>();
        List<int> restT = new List<int>();
        for (int i = 0; i < tris.Length; i += 3)
        {
            int s = (shirtV[tris[i]] ? 1 : 0) + (shirtV[tris[i + 1]] ? 1 : 0) + (shirtV[tris[i + 2]] ? 1 : 0);
            List<int> dst = s >= 2 ? shirtT : restT;
            dst.Add(tris[i]); dst.Add(tris[i + 1]); dst.Add(tris[i + 2]);
        }
        Mesh m = Object.Instantiate(src);
        m.name = src.name + "_camiseta";
        m.subMeshCount = 2;
        m.SetTriangles(restT, 0);
        m.SetTriangles(shirtT, 1);
        Debug.Log("ShirtMeshSplit: camiseta = " + shirtT.Count / 3 + " triángulos (huesos usados: " + count + ").");
        return m;
    }

    static bool Ok(int idx, bool[] isShirt) { return idx >= 0 && idx < isShirt.Length && isShirt[idx]; }

    static Material GetShirtMaterial(Material baseMat, Color color)
    {
        string key = baseMat.GetInstanceID() + "_" + ColorUtility.ToHtmlStringRGB(color);
        Material mat;
        if (matCache.TryGetValue(key, out mat)) return mat;
        mat = new Material(baseMat);
        mat.name = "Camiseta_" + ColorUtility.ToHtmlStringRGB(color);
        foreach (string p in new string[] { "_MainTex", "_BaseMap", "baseColorTexture" })
            if (mat.HasProperty(p)) mat.SetTexture(p, null);
        foreach (string p in new string[] { "_Color", "_BaseColor", "baseColorFactor" })
            if (mat.HasProperty(p)) mat.SetColor(p, color);
        // el material original trae la textura como EMISIÓN: se quita y se usa el mismo tono del equipo
        foreach (string p in new string[] { "_EmissionMap", "emissiveTexture" })
            if (mat.HasProperty(p)) mat.SetTexture(p, null);
        foreach (string p in new string[] { "_EmissionColor", "emissiveFactor" })
            if (mat.HasProperty(p)) mat.SetColor(p, color);
        if (mat.HasProperty("_EmissionColor")) mat.EnableKeyword("_EMISSION");
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.2f);
        matCache[key] = mat;
        return mat;
    }
}
