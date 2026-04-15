using UnityEngine;

public class modifySlide : MonoBehaviour
{
    public Mesh MeshA;
    public Mesh MeshB;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
    }

    public void swapLayers() {
        MeshFilter mf = GetComponent<MeshFilter>();

        if (mf.mesh == MeshA) {
            mf.mesh = MeshB;
        } else {
            mf.mesh = MeshA;
        }
        Resources.UnloadUnusedAssets();
    }

    public void setDepth(float newDepth) {
        transform.localScale = new Vector3(1f, 1f, newDepth);
    }
}
