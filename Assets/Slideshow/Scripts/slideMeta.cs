using UnityEngine;

public class slideMeta : MonoBehaviour {
    public int slideID;
    public string note;
    public bool is3D = false;

    public Mesh MeshA;
    public Mesh MeshB;

    public void swapLayers() {
        if (!is3D) return;

        MeshFilter mf = GetComponent<MeshFilter>();

        if (mf.mesh.name == MeshA.name)
        {
            mf.mesh = MeshB;
        }
        else
        {
            mf.mesh = MeshA;
        }
        Resources.UnloadUnusedAssets();
    }

    public void setDepth(float newDepth) {
        if (!is3D) return;
        transform.localScale = new Vector3(1f, 1f, newDepth);
    }
}
