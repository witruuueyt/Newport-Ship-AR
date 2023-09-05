/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using UnityEngine;

public class MeshToTerrainObject
{
    public GameObject gameobject;
    public int layer;
    public MeshCollider tempCollider;
    public Transform originalParent;

    public MeshToTerrainObject(GameObject gameObject)
    {
        gameobject = gameObject;
        layer = gameObject.layer;
    }
}