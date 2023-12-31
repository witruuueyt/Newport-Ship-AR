/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public class MeshToTerrain : EditorWindow
{
    public const string version = "2.1.6.1";

    public static MeshToTerrainPhase phase;

    private static int activeIndex;
    private static float progress;
    private static List<MeshToTerrainObject> terrainObjects;

    private List<Texture2D> checkedTexures;
    private Color[] colors;
    private GameObject container;
    private float[,] heightmap;
    private int lastX;
    private Material material;
    private Texture2D mainTexture;
    private Vector3 maxBounds = Vector3.zero;
    private Vector3 minBounds = Vector3.zero;
    private MeshToTerrainPrefs prefs = new MeshToTerrainPrefs();
    private string resultFolder;
    private static Transform lastTransform;
    private static Mesh lastMesh;
    private static int[] lastTriangles;
    private static Vector3[] lastVerticles;
    private static MeshToTerrain wnd;
    private GameObject meshContainer;
    private Vector3 boundsRange;
    private Vector3 meshScale;
    private Vector3 originalBoundsRange;

    private void AverageFillHoles()
    {
        int w = heightmap.GetLength(1);
        int h = heightmap.GetLength(0);

        while (true)
        {
            List<AverageHoleItem> holes = new List<AverageHoleItem>();

            for (int tx = 0; tx < w; tx++)
            {
                for (int ty = 0; ty < h; ty++)
                {
                    if (Math.Abs(heightmap[ty, tx] - float.MinValue) < float.Epsilon)
                    {
                        int countNeighbors;
                        float newValue;

                        GetAverageHoleValue(tx, ty, w, h, out countNeighbors, out newValue);

                        if (countNeighbors >= 3) holes.Add(new AverageHoleItem(tx, ty, newValue));
                    }
                }
            }

            if (holes.Count == 0) break;
            foreach (AverageHoleItem item in holes) heightmap[item.y, item.x] = item.newValue;
        }
    }

#if RTP
    private List<Texture2D> rtpTextures;
#endif

    private void CancelConvert()
    {
        phase = MeshToTerrainPhase.idle;
        if (container != null) DestroyImmediate(container);
        Dispose();
    }

    private bool CheckBounds()
    {
        if (prefs.terrainType == MeshToTerrainSelectTerrainType.newTerrains)
        {
            if (prefs.boundsType == MeshToTerrainBounds.fromGameobject)
            {
                if (!CheckBoundsFromGameObject()) return false;
            }
            else if (prefs.boundsType == MeshToTerrainBounds.selectBounds)
            {
                minBounds = prefs.bounds.min;
                maxBounds = prefs.bounds.max;
            }
            else FindBounds();
        }
        else
        {
            FindBounds();
        }

        return true;
    }

    private bool CheckBoundsFromGameObject()
    {
        if (prefs.boundsGameObject == null)
        {
            DisplayDialog("Boundaries GameObject are not set.\nSelect a GameObject in the scene, which will be the boundaries to generated Terrains.");
            return false;
        }
        Renderer r = prefs.boundsGameObject.GetComponent<Renderer>();
        if (r == null)
        {
            DisplayDialog("Boundaries GameObject does not contain the Renderer component.\nSelect another GameObject.");
            return false;
        }
        minBounds = r.bounds.min;
        maxBounds = r.bounds.max;
        return true;
    }

    private bool CheckOnlySceneObjects()
    {
        GameObject[] sceneGameObjects = (GameObject[])FindObjectsOfType(typeof(GameObject));

        if (prefs.meshes.Any(m => !sceneGameObjects.Contains(m)))
        {
            DisplayDialog("Selected meshes not in the scene.\nPlease add this meshes into the scene.\nIf the meshes in the scene, then make sure you choose from scene tab.");
            return false;
        }
        return true;
    }

    private void CheckReadWriteEnabled(Texture2D texture)
    {
        if (checkedTexures.Contains(texture)) return;

        string assetPath = AssetDatabase.GetAssetPath(texture);
        TextureImporter textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (textureImporter != null && !textureImporter.isReadable)
        {
            textureImporter.isReadable = true;
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        checkedTexures.Add(texture);
    }

    private bool CheckValues()
    {
        if (prefs.meshFindType == MeshToTerrainFindType.gameObjects)
        {
            if (prefs.meshes.Count == 0)
            {
                DisplayDialog("No meshes added.");
                return false;
            }

            if (!CheckOnlySceneObjects()) return false;

            prefs.meshLayer = FindFreeLayer();
            if (prefs.meshLayer == -1)
            {
                prefs.meshLayer = 31;
                DisplayDialog("Can not find the free layer.");
                return false;
            }
        }
        else if (prefs.meshFindType == MeshToTerrainFindType.layers)
        {
            if (prefs.meshLayer == 0)
            {
                DisplayDialog("Cannot use dafault layer.\nPlace the models for conversion to another layer.");
                return false;
            }

            prefs.meshes = FindGameObjectsWithLayer(prefs.meshLayer).ToList();
        }

        if (prefs.terrainType == MeshToTerrainSelectTerrainType.newTerrains)
        {
            if (prefs.textureCaptureMode == MeshToTerrainTextureCaptureMode.camera && !prefs.adjustMeshSize)
            {
                DisplayDialog("Texture Capture Mode - Camera requires Adjust size of meshes - ON.");
                return false;
            }
        }

        if (!CheckBounds()) return false;

        if (prefs.yRange != MeshToTerrainYRange.minimalRange)
        {
            float yRange = maxBounds.y - minBounds.y;
            float halfRange = yRange / 2;
            float center = halfRange + minBounds.y;
            float scale = yRange / (prefs.yRange == MeshToTerrainYRange.fixedValue
                ? prefs.yRangeValue
                : Mathf.Max(maxBounds.x - minBounds.x, maxBounds.z - minBounds.z));

            if (scale < 1)
            {
                maxBounds.y = center + halfRange / scale;
                minBounds.y = center - halfRange / scale;
            }
        }

        boundsRange = maxBounds - minBounds;
        originalBoundsRange = boundsRange;

        if (prefs.terrainType == MeshToTerrainSelectTerrainType.newTerrains) CreateTerrainContainer();
        else if (prefs.terrains.Count == 0)
        {
            DisplayDialog("No terrains added.");
            return false;
        }
        return true;
    }

    private void CreateTerrain(int terrainIndex)
    {
        int x = terrainIndex % prefs.newTerrainCountX;
        int y = terrainIndex / prefs.newTerrainCountX;

        float w = boundsRange.x;
        float h = boundsRange.z;

        float sW = w / prefs.newTerrainCountX;
        float sH = h / prefs.newTerrainCountY;
        float sY = boundsRange.y * 1.5f;
        
        float offX = (w - sW * prefs.newTerrainCountX) / 2;
        float offY = (h - sH * prefs.newTerrainCountY) / 2;

        string terrainName = string.Format("Terrain {0}x{1}", x, y);
        GameObject terrainGO = CreateTerrainGameObject(sW, sY, sH, terrainName);

        terrainGO.name = terrainName;
        terrainGO.transform.parent = container.transform;
        terrainGO.transform.localPosition = new Vector3(x * sW + offX, 0, y * sH + offY);
        prefs.terrains.Add(terrainGO.GetComponent<Terrain>());

        activeIndex++;
        progress = activeIndex / (float)(prefs.newTerrainCountX * prefs.newTerrainCountY);
        if (activeIndex >= prefs.newTerrainCountX * prefs.newTerrainCountY)
        {
            if (prefs.terrains.Count > 1) for (int i = 0; i < prefs.terrains.Count; i++) SetTerrainNeighbors(i);

            activeIndex = 0;
            progress = 0;
            phase = MeshToTerrainPhase.generateHeightmaps;
        }
    }

    private void CreateTerrainContainer()
    {
        const string containerName = "Generated terrains";

        prefs.terrains = new List<Terrain>();

        string cName = containerName;
        int index = 1;
        while (GameObject.Find(cName) != null) cName = containerName + " " + index++;

        container = new GameObject(cName);
        container.transform.position = new Vector3(minBounds.x, minBounds.y, minBounds.z);
    }

    private GameObject CreateTerrainGameObject(float sW, float sY, float sH, string terrainName)
    {
        TerrainData tdata = new TerrainData();
        tdata.SetDetailResolution(prefs.detailResolution, prefs.resolutionPerPatch);
        tdata.alphamapResolution = prefs.alphamapResolution;
        tdata.baseMapResolution = prefs.baseMapResolution;
        tdata.heightmapResolution = prefs.heightmapResolution;
        tdata.size = new Vector3(sW, sY, sH);

        string filename = Path.Combine(resultFolder, terrainName + ".asset");

        AssetDatabase.CreateAsset(tdata, filename);
        GameObject terrainGO = Terrain.CreateTerrainGameObject(tdata);
        return terrainGO;
    }

    private void DisplayDialog(string msg)
    {
        EditorUtility.DisplayDialog("Error", msg, "OK");
    }

    private void Dispose()
    {
        if (meshContainer != null) meshContainer.transform.localScale = Vector3.one;

        FinalizeMeshes();

        if (meshContainer != null)
        {
            DestroyImmediate(meshContainer);
            meshContainer = null;
        }

        checkedTexures = null;
        colors = null;
        heightmap = null;
        lastX = 0;
        lastTransform = null;
        lastMesh = null;
        lastTriangles = null;
        lastVerticles = null;
        material = null;
        mainTexture = null;

        EditorUtility.UnloadUnusedAssetsImmediate();
        GC.Collect();
    }

    private static void FinalizeMeshes()
    {
        foreach (MeshToTerrainObject m in terrainObjects)
        {
            m.gameobject.layer = m.layer;
            if (m.tempCollider != null)
            {
                DestroyImmediate(m.tempCollider);
                m.tempCollider = null;
            }

            if (m.originalParent != null)
            {
                m.gameobject.transform.parent = m.originalParent;
                m.originalParent = null;
            }

            m.gameobject = null;
        }

        terrainObjects = null;
    }

    public static Object FindAndLoad(string filename, Type type)
    {
#if !UNITY_WEBPLAYER
        string[] files = Directory.GetFiles("Assets", filename, SearchOption.AllDirectories);
        if (files.Length > 0) return AssetDatabase.LoadAssetAtPath(files[0], type);
#endif
        return null;
    }

    private void FindBounds()
    {
        minBounds = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        maxBounds = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        Renderer[] renderers = prefs.meshes.SelectMany(m => m.GetComponentsInChildren<Renderer>()).ToArray();

        if (renderers.Length == 0) return;

        foreach (Renderer r in renderers)
        {
            Bounds b = r.bounds;

            Vector3 min = b.min;
            Vector3 max = b.max;

            if (minBounds.x > min.x) minBounds.x = min.x;
            if (minBounds.y > min.y) minBounds.y = min.y;
            if (minBounds.z > min.z) minBounds.z = min.z;

            if (maxBounds.x < max.x) maxBounds.x = max.x;
            if (maxBounds.y < max.y) maxBounds.y = max.y;
            if (maxBounds.z < max.z) maxBounds.z = max.z;
        }
    }

    private int FindFreeLayer()
    {
        bool[] ls = new bool[32];

        for (int i = 0; i < 32; i++) ls[i] = true;
        foreach (GameObject go in (GameObject[])FindObjectsOfType(typeof(GameObject))) ls[go.layer] = false;

        for (int i = 31; i > 0; i--) if (ls[i]) return i;
        return -1;
    }

    private IEnumerable<GameObject> FindGameObjectsWithLayer(int layer)
    {
        return ((MeshFilter[])FindObjectsOfType(typeof(MeshFilter))).Select(m => m.gameObject).Where(go => go.layer == layer);
    }

    private void Finish()
    {
#if RTP
        if (prefs.generateTextures)
        {
            ReliefTerrain reliefTerrain = prefs.terrains[0].GetComponent<ReliefTerrain>();
            ReliefTerrainGlobalSettingsHolder settingsHolder = reliefTerrain.globalSettingsHolder;

            settingsHolder.numLayers = 4;
            settingsHolder.splats = new Texture2D[4];
            settingsHolder.Bumps = new Texture2D[4];
            settingsHolder.Heights = new Texture2D[4];

            for (int i = 0; i < 4; i++)
            {
                settingsHolder.splats[i] = rtpTextures[i * 3];
                settingsHolder.Heights[i] = rtpTextures[i * 3 + 1];
                settingsHolder.Bumps[i] = rtpTextures[i * 3 + 2];
            }

            settingsHolder.GlobalColorMapBlendValues = new Vector3(1, 1, 1);
            settingsHolder._GlobalColorMapNearMIP = 1;
            settingsHolder.GlobalColorMapSaturation = 1;
            settingsHolder.GlobalColorMapSaturationFar = 1;
            settingsHolder.GlobalColorMapBrightness = 1;
            settingsHolder.GlobalColorMapBrightnessFar = 1;

            foreach (Terrain item in prefs.terrains) item.GetComponent<ReliefTerrain>().RefreshTextures();

            settingsHolder.Refresh();
        }
#endif

        if (prefs.adjustMeshSize)
        {
            float w = originalBoundsRange.x;
            float h = originalBoundsRange.z;

            float sW = w / prefs.newTerrainCountX;
            float sH = h / prefs.newTerrainCountY;
            float sY = originalBoundsRange.y * 1.5f;

            float tsw = sW;
            float tsh = sH;

            if (prefs.textureCaptureMode == MeshToTerrainTextureCaptureMode.camera)
            {
                tsw = tsw / prefs.textureWidth * (prefs.textureWidth + 4);
                tsh = tsh / prefs.textureHeight * (prefs.textureHeight + 4);
            }
            else
            {
                tsw = tsw / prefs.textureWidth * (prefs.textureWidth + 2);
                tsh = tsh / prefs.textureHeight * (prefs.textureHeight + 2);
            }

            float offX = (w - sW * prefs.newTerrainCountX) / 2;
            float offY = (h - sH * prefs.newTerrainCountY) / 2;

            for (int x = 0; x < prefs.newTerrainCountX; x++)
            {
                for (int y = 0; y < prefs.newTerrainCountY; y++)
                {
                    Terrain t = prefs.terrains[y * prefs.newTerrainCountX + x];
                    t.transform.localPosition = new Vector3(x * sW + offX, 0, y * sH + offY);
                    t.terrainData.size = new Vector3(sW, sY, sH);

#if !RTP
                    if (prefs.generateTextures)
                    {
#if UNITY_2018_3_OR_NEWER
                        TerrainLayer[] terrainLayers = t.terrainData.terrainLayers;
                        TerrainLayer item = terrainLayers[0];
#else 
                        SplatPrototype[] splatPrototypes = t.terrainData.splatPrototypes;
                        SplatPrototype item = splatPrototypes[0];
#endif

                        item.tileSize = new Vector2(tsw, tsh);
                        item.tileOffset = new Vector2(t.terrainData.size.x / prefs.textureWidth / 1.5f, t.terrainData.size.z / prefs.textureHeight / 1.5f);

#if UNITY_2018_3_OR_NEWER
                        t.terrainData.terrainLayers = terrainLayers;
#else 
                        t.terrainData.splatPrototypes = splatPrototypes;
#endif
                    }
#endif
                }
            }
        }

        if (prefs.terrainType == MeshToTerrainSelectTerrainType.newTerrains) EditorGUIUtility.PingObject(container);
        else foreach (Terrain t in prefs.terrains) EditorGUIUtility.PingObject(t.gameObject);

        Dispose();

        phase = MeshToTerrainPhase.idle;
    }

    private void GetAverageHoleValue(int tx, int ty, int w, int h, out int countNeighbors, out float newValue)
    {
        countNeighbors = 0;
        newValue = float.MinValue;

        int[] dirX = {  0,  1, 1, 1, 0, -1, -1, -1 };
        int[] dirY = { -1, -1, 0, 1, 1,  1,  0, -1 };

        List<float> neighborValues = new List<float>();

        for (int i = 0; i < 8; i++)
        {
            int nx = tx + dirX[i];
            int ny = ty + dirY[i];

            float p;
            if (nx < 0 || nx >= w || ny < 0 || ny >= h) p = float.MinValue;
            else p = heightmap[ny, nx];
            if (Math.Abs(p - float.MinValue) > float.Epsilon)
            {
                countNeighbors++;
                neighborValues.Add(p);
            }
        }

        if (countNeighbors >= 3) newValue = neighborValues.Average();
    }

    private Color GetColor(Vector3 curPoint, float raycastDistance, Vector3 raycastDirection, int mLayer, ref Renderer lastRenderer, ref int[] triangles, ref Vector3[] verticles, ref Vector2[] uv)
    {
        RaycastHit hit;
        if (!Physics.Raycast(curPoint, raycastDirection, out hit, raycastDistance, mLayer)) return prefs.textureEmptyColor;

        Renderer renderer = hit.collider.GetComponent<Renderer>();
        if (renderer == null || renderer.sharedMaterial == null) return prefs.textureEmptyColor;

        if (lastRenderer != renderer)
        {
            lastRenderer = renderer;
            material = renderer.sharedMaterial;
            mainTexture = material.mainTexture as Texture2D;
            if (mainTexture != null) CheckReadWriteEnabled(mainTexture);
        }

        if (mainTexture != null)
        {
            Vector2 tc = hit.textureCoord;
            return mainTexture.GetPixelBilinear(tc.x, tc.y);
        }
        return material.color;
    }

    private static double GetHitPoint(RaycastHit hit, Vector3 curPoint)
    {
        if (lastTransform != hit.transform)
        {
            lastTransform = hit.transform;
            lastMesh = hit.collider.GetComponent<MeshFilter>().sharedMesh;
            lastTriangles = lastMesh.triangles;
            lastVerticles = lastMesh.vertices;
        }

        int ti = hit.triangleIndex * 3;

        int pi1 = lastTriangles[ti];
        int pi2 = lastTriangles[ti + 1];
        int pi3 = lastTriangles[ti + 2];

        Vector3 p1 = lastTransform.TransformPoint(lastVerticles[pi1]);
        Vector3 p2 = lastTransform.TransformPoint(lastVerticles[pi2]);
        Vector3 p3 = lastTransform.TransformPoint(lastVerticles[pi3]);

        Vector3 a = new Vector3(p1.x, 0, p1.z);
        Vector3 b = new Vector3(p2.x, 0, p2.z);
        Vector3 c = new Vector3(p3.x, 0, p3.z);
        Vector3 d = new Vector3(curPoint.x, 0, curPoint.z);

        float ad = (a - d).magnitude;
        float bd = (b - d).magnitude;
        float cd = (c - d).magnitude;
        float ab = (a - b).magnitude;
        float ac = (a - c).magnitude;
        float bc = (b - c).magnitude;

        float wa = bd * cd / (ab * ac);
        float wb = ad * cd / (ab * bc);
        float wc = ad * bd / (ac * bc);

        return (p1 * wa + p2 * wb + p3 * wc).y;
    }

    private void GetResultFolder()
    {
        const string baseResultFolder = "Assets/MTT_Results";
        string baseResultFullPath = Path.Combine(Application.dataPath, "MTT_Results");
        if (!Directory.Exists(baseResultFullPath)) Directory.CreateDirectory(baseResultFullPath);
        string dateStr = DateTime.Now.ToString("yyyy-MM-dd HH-mm");

        int index = 0;
        bool appendIndex = false;
        while (true)
        {
            resultFolder = baseResultFolder + "/" + dateStr;
            string resultFullPath = Path.Combine(baseResultFullPath, dateStr);

            if (appendIndex)
            {
                resultFolder += " " + index;
                resultFullPath += " " + index;
            }

            if (!Directory.Exists(resultFullPath))
            {
                Directory.CreateDirectory(resultFullPath);
                break;
            }

            appendIndex = true;
            index++;
        }
    }

    private SplatPrototype[] GetSplatPrototype(Terrain t)
    {
        Texture2D texture = GetTexture(t);

        float tsx = prefs.textureWidth - 4;
        float tsy = prefs.textureHeight - 4;

        Vector2 tileSize = new Vector2(t.terrainData.size.x + t.terrainData.size.x / tsx * 4,
            t.terrainData.size.z + t.terrainData.size.z / tsy * 4);

        Vector2 tileOffset = new Vector2(t.terrainData.size.x / prefs.textureWidth / 2, t.terrainData.size.z / prefs.textureHeight / 2);

        SplatPrototype sp = new SplatPrototype
        {
            tileSize = tileSize,
            tileOffset = tileOffset,
            texture = texture
        };
        return new []{sp};
    }

    private Texture2D GetTexture(Terrain t)
    {
        Texture2D texture = new Texture2D(prefs.textureWidth, prefs.textureHeight);
        texture.SetPixels(colors);
        texture.Apply();

        string textureFilename = Path.Combine(resultFolder, t.name + ".png");
        File.WriteAllBytes(textureFilename, texture.EncodeToPNG());
        AssetDatabase.Refresh();
        TextureImporter importer = AssetImporter.GetAtPath(textureFilename) as TextureImporter;
        if (importer != null)
        {
            importer.maxTextureSize = Mathf.Max(prefs.textureWidth, prefs.textureHeight);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        texture = (Texture2D) AssetDatabase.LoadAssetAtPath(textureFilename, typeof (Texture2D));
        return texture;
    }

    private float GetValue(int X, int Y)
    {
        X = Mathf.Clamp(X, 0, prefs.heightmapResolution - 1);
        Y = Mathf.Clamp(Y, 0, prefs.heightmapResolution - 1);
        return heightmap[X, Y];
    }

    private double GetValuesAround(int x, int y, int offset, float scale)
    {
        double val = GetValue(x - offset, y - offset) * scale;
        val += GetValue(x, y - offset) * scale;
        val += GetValue(x + offset, y - offset) * scale;
        val += GetValue(x + offset, y) * scale;
        val += GetValue(x + offset, y + offset) * scale;
        val += GetValue(x, y + offset) * scale;
        val += GetValue(x - offset, y + offset) * scale;
        val += GetValue(x - offset, y) * scale;
        return val;
    }

#if RTP
    private void LoadRTPTextures()
    {
        if (rtpTextures != null && rtpTextures.Count == 12) return;

        rtpTextures = new List<Texture2D>
        {
            (Texture2D) FindAndLoad("Dirt.png", typeof (Texture2D)),
            (Texture2D) FindAndLoad("Dirt Height.png", typeof (Texture2D)),
            (Texture2D) FindAndLoad("Dirt Normal.png", typeof (Texture2D)),
            (Texture2D) FindAndLoad("Grass.png", typeof (Texture2D)),
            (Texture2D) FindAndLoad("Grass Height.png", typeof (Texture2D)),
            (Texture2D) FindAndLoad("Grass Normal.png", typeof (Texture2D)),
            (Texture2D) FindAndLoad("GrassRock.png", typeof (Texture2D)),
            (Texture2D) FindAndLoad("GrassRock Height.png", typeof (Texture2D)),
            (Texture2D) FindAndLoad("GrassRock Normal.png", typeof (Texture2D)),
            (Texture2D) FindAndLoad("Cliff.png", typeof (Texture2D)),
            (Texture2D) FindAndLoad("Cliff Height.png", typeof (Texture2D)),
            (Texture2D) FindAndLoad("Cliff Normal.png", typeof (Texture2D))
        };
    }
#endif

    private void OnConvertGUI()
    {
        string phaseTitle = "";
        if (phase == MeshToTerrainPhase.prepare) phaseTitle = "Preparing.";
        else if (phase == MeshToTerrainPhase.createTerrains) phaseTitle = "Create terrains.";
        else if (phase == MeshToTerrainPhase.generateHeightmaps) phaseTitle = "Generate heightmaps.";
        else if (phase == MeshToTerrainPhase.generateTextures) phaseTitle = "Generate textures.";
        else if (phase == MeshToTerrainPhase.finish) phaseTitle = "Finishing.";

        GUILayout.Label(phaseTitle);

        Rect r = EditorGUILayout.BeginVertical();
        if (phase == MeshToTerrainPhase.generateHeightmaps || phase == MeshToTerrainPhase.generateTextures || phase == MeshToTerrainPhase.createTerrains)
        {
            r.height = 16;
            int iProgress = Mathf.FloorToInt(progress * 100);
            EditorGUI.ProgressBar(r, progress, iProgress + "%");
            GUILayout.Space(18);
        }
        else
        {
            GUILayout.Space(38);
        }

        if (GUILayout.Button("Cancel")) CancelConvert();

        EditorGUILayout.EndVertical();
    }

    private void OnDestroy()
    {
        if (prefs != null) prefs.Save();
    }

    private void OnDisable()
    {
        if (prefs != null) prefs.Save();
    }

    private void OnEnable()
    {
        wnd = this;
        prefs = new MeshToTerrainPrefs();
        prefs.Init();
        Repaint();
    }

    private void OnGUI() 
    {
        if (phase == MeshToTerrainPhase.idle) prefs.OnGUI();
        else OnConvertGUI();
    }

    [MenuItem("Window/Infinity Code/Mesh to Terrain/Mesh to Terrain", false, 0)]
    private static void OpenWindow()
    {
        wnd = GetWindow<MeshToTerrain>(false, "Mesh to Terrain");
        Rect rect = wnd.position;

        if (rect.width < 500) rect.width = 500;
        if (rect.height < 400) rect.height = 400;
        rect.x = rect.y = 100;
        wnd.position = rect;
    }

    [MenuItem("GameObject/Convert Mesh to Terrain", false, 40)]
    private static void OpenWindowWithGO()
    {
        List<GameObject> gameObjects = Selection.gameObjects.Where(g => g.GetComponentsInChildren<MeshFilter>().Length > 0).ToList();
        OpenWindow();

        wnd.prefs.meshes = gameObjects;
    }

    [MenuItem("GameObject/Convert Mesh to Terrain", true, 40)]
    private static bool OpenWindowWithGOValidation()
    {
        MeshFilter[] filters = Selection.gameObjects.SelectMany(g => g.GetComponentsInChildren<MeshFilter>()).ToArray();
        return filters.Length > 0;
    }

    private void Prepare()
    {
        activeIndex = 0;
        checkedTexures = new List<Texture2D>();
        colors = null;
        heightmap = null;
        progress = 0;
        terrainObjects = new List<MeshToTerrainObject>();

        if (prefs.terrainType != MeshToTerrainSelectTerrainType.newTerrains) prefs.adjustMeshSize = false;

        GetResultFolder();

        if (!CheckValues())
        {
            Dispose();
            phase = MeshToTerrainPhase.idle;
            return;
        }

        if (prefs.adjustMeshSize)
        {
            meshContainer = new GameObject("__Mesh Container__");
            meshContainer.transform.position = minBounds;
        }

        PrepareMeshes(terrainObjects);

        if (prefs.adjustMeshSize)
        {
            float sx = prefs.heightmapResolution * prefs.newTerrainCountX / boundsRange.x;
            float sz = prefs.heightmapResolution * prefs.newTerrainCountY / boundsRange.z;

            meshScale = new Vector3(sx, (sx + sz) / 2, sz);
            meshContainer.transform.localScale = meshScale;
            boundsRange.Scale(meshScale);
            maxBounds = minBounds + boundsRange;
        }

        if (prefs.terrainType == MeshToTerrainSelectTerrainType.newTerrains) phase = MeshToTerrainPhase.createTerrains;
        else phase = MeshToTerrainPhase.generateHeightmaps;
    }

    private void PrepareGameObject(List<MeshToTerrainObject> objs, GameObject go)
    {
        MeshToTerrainObject m = new MeshToTerrainObject(go);
        objs.Add(m);

        if (go.GetComponent<Collider>() == null)
        {
            MeshCollider collider = go.AddComponent<MeshCollider>();
            collider.convex = false;
            m.tempCollider = collider;
        }

        if (prefs.adjustMeshSize)
        {
#if UNITY_2018_3_OR_NEWER
            if (PrefabUtility.IsPartOfAnyPrefab(go))
            {
                GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
                if (root != null) PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
#endif
            m.originalParent = go.transform.parent;
            go.transform.parent = meshContainer.transform;
        }
    }

    private void PrepareMeshes(List<MeshToTerrainObject> objs)
    {
        if (prefs.meshFindType == MeshToTerrainFindType.gameObjects)
        {
            IEnumerable<GameObject> gos = prefs.meshes.SelectMany(m => m.GetComponentsInChildren<MeshFilter>()).Select(mf => mf.gameObject);
            foreach (GameObject go in gos)
            {
                PrepareGameObject(objs, go);
                go.layer = prefs.meshLayer;
            }
        }
        else if (prefs.meshFindType == MeshToTerrainFindType.layers)
        {
            foreach (GameObject go in FindGameObjectsWithLayer(prefs.meshLayer))
            {
                PrepareGameObject(objs, go);
            }
        }
    }

    public static void RepaintWindow()
    {
        wnd.Repaint();
    }

    private void SetAlphaMaps(Terrain t)
    {
        float[,,] alphamaps = new float[prefs.alphamapResolution, prefs.alphamapResolution, t.terrainData.alphamapLayers];
        for (int x = 0; x < prefs.alphamapResolution; x++)
        {
            for (int y = 0; y < prefs.alphamapResolution; y++)
            {
                alphamaps[x, y, 0] = 1;
            }
        }

        t.terrainData.SetAlphamaps(0, 0, alphamaps);
    }

    private void SetTerrainNeighbors(int i)
    {
        int leftIndex = i % prefs.newTerrainCountX != 0 ? i - 1 : -1;
        int rightIndex = i % prefs.newTerrainCountX != prefs.newTerrainCountX - 1 ? i + 1 : -1;
        int topIndex = i - prefs.newTerrainCountX;
        int bottomIndex = i + prefs.newTerrainCountX;
        Terrain left = prefs.newTerrainCountX > 1 && leftIndex != -1 ? prefs.terrains[leftIndex] : null;
        Terrain right = prefs.newTerrainCountX > 1 && rightIndex != -1 ? prefs.terrains[rightIndex] : null;
        Terrain top = prefs.newTerrainCountY > 1 && topIndex >= 0 ? prefs.terrains[topIndex] : null;
        Terrain bottom = prefs.newTerrainCountY > 1 && bottomIndex < prefs.terrains.Count ? prefs.terrains[bottomIndex] : null;
        prefs.terrains[i].SetNeighbors(left, bottom, right, top);
    }

    private void SetTexturesToTerrain(Terrain t, Texture2D texture)
    {
#if UNITY_2018_3_OR_NEWER

#if !RTP
        float tsx = prefs.textureWidth - 4;
        float tsy = prefs.textureHeight - 4;

        Vector2 tileSize = new Vector2(t.terrainData.size.x + t.terrainData.size.x / tsx * 4,
            t.terrainData.size.z + t.terrainData.size.z / tsy * 4);

        Vector2 tileOffset = new Vector2(t.terrainData.size.x / prefs.textureWidth / 2, t.terrainData.size.z / prefs.textureHeight / 2);

        TerrainLayer tl = new TerrainLayer
        {
            tileSize = tileSize,
            tileOffset = tileOffset,
            diffuseTexture = texture
        };

        string filename = Path.Combine(resultFolder, t.name + ".terrainlayer");

        AssetDatabase.CreateAsset(tl, filename);
        AssetDatabase.Refresh();

        t.terrainData.terrainLayers = new[] { AssetDatabase.LoadAssetAtPath<TerrainLayer>(filename) };

        SetAlphaMaps(t);
#else
        LoadRTPTextures();
        TerrainLayer[] tls = new TerrainLayer[4];

        for (int i = 0; i < 4; i++)
        {
            tls[i] = new TerrainLayer { diffuseTexture = rtpTextures[i * 3] };
        }

        t.terrainData.terrainLayers = tls;
        SetAlphaMaps(t);

        ReliefTerrain reliefTerrain = t.gameObject.GetComponent<ReliefTerrain>() ?? t.gameObject.AddComponent<ReliefTerrain>();
        reliefTerrain.InitArrays();
        reliefTerrain.ColorGlobal = texture;
#endif

#else

#if !RTP
        float tsx = prefs.textureWidth - 4;
        float tsy = prefs.textureHeight - 4;

        Vector2 tileSize = new Vector2(t.terrainData.size.x + t.terrainData.size.x / tsx * 4,
            t.terrainData.size.z + t.terrainData.size.z / tsy * 4);

        Vector2 tileOffset = new Vector2(t.terrainData.size.x / prefs.textureWidth / 2, t.terrainData.size.z / prefs.textureHeight / 2);

        SplatPrototype sp = new SplatPrototype
        {
            tileSize = tileSize,
            tileOffset = tileOffset,
            texture = texture
        };
        t.terrainData.splatPrototypes = new []{sp};
#else
        LoadRTPTextures();
        SplatPrototype[] sps = new SplatPrototype[4];

        for (int i = 0; i < 4; i++)
        {
            sps[i] = new SplatPrototype { texture = rtpTextures[i * 3] };
        }

        t.terrainData.splatPrototypes = sps;

        ReliefTerrain reliefTerrain = t.gameObject.GetComponent<ReliefTerrain>() ?? t.gameObject.AddComponent<ReliefTerrain>();
        reliefTerrain.InitArrays();
        reliefTerrain.ColorGlobal = texture;
#endif

#endif
    }

    private void SmoothHeightmap()
    {
        int h = heightmap.GetLength(0);
        float[,] smoothedHeightmap = new float[h, h];
        int sf = prefs.smoothingFactor;
        int sfStep = 1;
        if (sf > 8)
        {
            sfStep = sf / 8;
            sf = 8;
        }

        for (int i = 0; i < h; i++)
        {
            for (int j = 0; j < h; j++)
            {
                float curV = 0.5f;
                double totalV = curV;
                float origVal = GetValue(i, j);
                double val = origVal * curV;

                if (i == 0 || i == h - 1 || j == 0 || j == h - 1)
                {
                    smoothedHeightmap[i, j] = origVal;
                    continue;
                }

                curV = 0.3f;
                
                for (int v = 1; v <= sf; v++)
                {
                    int v1 = v * sfStep;
                    val += GetValuesAround(i, j, v1, curV);
                    totalV += curV * 8;
                }

                smoothedHeightmap[i, j] = (float)(val / totalV);
            }
        }

        if (sfStep > 1)
        {
            heightmap = smoothedHeightmap;
            smoothedHeightmap = new float[h, h];

            for (int i = 0; i < h; i++)
            {
                for (int j = 0; j < h; j++)
                {
                    float curV = 0.7f;
                    double totalV = curV;
                    double val = GetValue(i, j) * curV;

                    curV = 0.3f;
                    val += GetValuesAround(i, j, 1, curV);
                    totalV += curV * 8;
                    
                    smoothedHeightmap[i, j] = (float) (val / totalV);
                }
            }
        }

        heightmap = smoothedHeightmap;
    }

    private void Update()
    {
        if (phase == MeshToTerrainPhase.idle)
        {
            if (EditorApplication.isCompiling && prefs.showBoundSelector)
            {
                prefs.showBoundSelector = false;

                if (prefs.boundsHelper != null)
                {
                    prefs.boundsHelper.OnBoundChanged = null;
                    prefs.boundsHelper.OnDestroyed = null; 
                    DestroyImmediate(prefs.boundsHelper.gameObject);
                    prefs.boundsHelper = null; 
                }
            }
            return;
        }

        if (phase == MeshToTerrainPhase.prepare) Prepare();
        else if (phase == MeshToTerrainPhase.createTerrains) CreateTerrain(activeIndex);
        else if (phase == MeshToTerrainPhase.generateHeightmaps) UpdateTerrain(prefs.terrains[activeIndex]);
        else if (phase == MeshToTerrainPhase.generateTextures) UpdateTexture(prefs.terrains[activeIndex]);
        else if (phase == MeshToTerrainPhase.finish) Finish();

        Repaint();
    }

    private void UpdateTerrain(Terrain t)
    {
        int mLayer = 1 << prefs.meshLayer;
        float raycastDistance = (maxBounds.y - minBounds.y) * 1.5f;

        Vector3 vScale = t.terrainData.heightmapScale;
        Vector3 beginPoint = t.transform.position;
        Vector3 raycastDirection = Vector3.down;
        if (prefs.direction == MeshToTerrainDirection.normal) beginPoint.y += raycastDistance;
        else
        {
            beginPoint.y = maxBounds.y - raycastDistance;
            raycastDirection = Vector3.up;
        }

        if (heightmap == null)
        {
            heightmap = new float[t.terrainData.heightmapResolution, t.terrainData.heightmapResolution];
            lastX = 0;
        }

        long startTicks = DateTime.Now.Ticks;

        float nodataValue = prefs.holes == MeshToTerrainHoles.minimumValue? 0: float.MinValue;

        for (int tx = lastX; tx < t.terrainData.heightmapResolution; tx++)
        {
            for (int ty = 0; ty < t.terrainData.heightmapResolution; ty++)
            {
                Vector3 curPoint = beginPoint + new Vector3(tx * vScale.x, 0, ty * vScale.z);
                RaycastHit hit;
                if (Physics.Raycast(curPoint, raycastDirection, out hit, raycastDistance, mLayer))
                {
                    if (prefs.direction == MeshToTerrainDirection.normal)
                    {
                        float v = (raycastDistance - hit.distance) / vScale.y;
                        heightmap[ty, tx] = v;
                    }
                    else heightmap[ty, tx] = hit.distance / vScale.y;
                }
                else
                {
                    heightmap[ty, tx] = nodataValue;
                }
            }

            if (new TimeSpan(DateTime.Now.Ticks - startTicks).TotalSeconds >= 1)
            {
                lastX = tx;
                progress = (activeIndex + lastX / (float)t.terrainData.heightmapResolution) / prefs.terrains.Count;
                return;
            }
        }

        lastX = 0;

        if (prefs.holes == MeshToTerrainHoles.neighborAverage) AverageFillHoles();

        if (prefs.useHeightmapSmoothing) SmoothHeightmap();

        t.terrainData.SetHeights(0, 0, heightmap);
        t.Flush();

        heightmap = null;
        lastTransform = null;
        lastMesh = null;
        lastTriangles = null;
        lastVerticles = null;

        activeIndex++;
        progress = activeIndex / (float)prefs.terrains.Count;
        if (activeIndex >= prefs.terrains.Count)
        {
            activeIndex = 0;
            progress = 0;
            phase = prefs.generateTextures ? MeshToTerrainPhase.generateTextures : MeshToTerrainPhase.finish;
        }
    }

    private void UpdateTexture(Terrain t)
    {
        if (prefs.textureCaptureMode == MeshToTerrainTextureCaptureMode.raycast) UpdateTextureRaycast(t);
        else UpdateTextureCamera(t);
    }

    private void UpdateTextureCamera(Terrain t)
    {
        int mLayer = 1 << prefs.meshLayer;
        float raycastDistance = maxBounds.y - minBounds.y + 10;

        Vector3 vScale = t.terrainData.size;
        float tsx = prefs.textureWidth + 1;
        float tsy = prefs.textureHeight + 1;

        vScale.x = vScale.x / tsx;
        vScale.z = vScale.z / tsy;

        Vector3 beginPoint = t.transform.position;
        if (prefs.direction == MeshToTerrainDirection.normal) beginPoint.y += raycastDistance;
        else beginPoint.y = maxBounds.y - raycastDistance;

        Vector3 curPoint = beginPoint + new Vector3(prefs.textureWidth / 2 * vScale.x, 0, prefs.textureHeight / 2 * vScale.z);

        GameObject cameraGO = new GameObject("__Mesh to Terrain Camera__");
        Camera camera = cameraGO.AddComponent<Camera>();
        cameraGO.transform.position = curPoint;
        cameraGO.transform.rotation = Quaternion.Euler(prefs.direction == MeshToTerrainDirection.normal? 90: -90, 0, 0);
        camera.orthographic = true;
        camera.orthographicSize = boundsRange.x / 2 / prefs.newTerrainCountX;
        camera.clearFlags = CameraClearFlags.Color;
        camera.backgroundColor = prefs.textureEmptyColor;
        camera.cullingMask = mLayer;
        camera.targetTexture = new RenderTexture(prefs.textureWidth, prefs.textureHeight, 16);
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = camera.targetTexture;
        camera.Render();

        Texture2D texture = new Texture2D(prefs.textureWidth, prefs.textureHeight);
        texture.ReadPixels(new Rect(0, 0, prefs.textureWidth, prefs.textureHeight), 0, 0);
        texture.Apply();
        RenderTexture.active = currentRT;

        string textureFilename = Path.Combine(resultFolder, t.name + ".png");
        File.WriteAllBytes(textureFilename, texture.EncodeToPNG());
        AssetDatabase.Refresh();
        TextureImporter importer = AssetImporter.GetAtPath(textureFilename) as TextureImporter;
        if (importer != null)
        {
            importer.maxTextureSize = Mathf.Max(prefs.textureWidth, prefs.textureHeight);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        texture = (Texture2D)AssetDatabase.LoadAssetAtPath(textureFilename, typeof(Texture2D));

        DestroyImmediate(cameraGO);

        SetTexturesToTerrain(t, texture);

        activeIndex++;
        progress = activeIndex / (float)prefs.terrains.Count;
        if (activeIndex >= prefs.terrains.Count)
        {
            activeIndex = 0;
            phase = MeshToTerrainPhase.finish;
        }
    }

    private void UpdateTextureRaycast(Terrain t)
    {
        int mLayer = 1 << prefs.meshLayer;
        float raycastDistance = maxBounds.y - minBounds.y + 10;

        Vector3 vScale = t.terrainData.size;
        Vector3 beginPoint = t.transform.position;
        Vector3 raycastDirection = -Vector3.up;
        if (prefs.direction == MeshToTerrainDirection.normal) beginPoint.y += raycastDistance - 5;
        else
        {
            beginPoint.y = maxBounds.y - raycastDistance + 5;
            raycastDirection = Vector3.up;
        }

        float tsx = prefs.textureWidth + 1;
        float tsy = prefs.textureHeight + 1;

        vScale.x = vScale.x / tsx;
        vScale.z = vScale.z / tsy;

        beginPoint += new Vector3(vScale.x / 2, 0, vScale.z / 2);

        Renderer lastRenderer = null;
        Vector2[] uv = null;
        int[] triangles = null;
        Vector3[] verticles = null;

        if (colors == null)
        {
            colors = new Color[prefs.textureWidth * prefs.textureHeight];
            lastX = 0;
        }

        long startTicks = DateTime.Now.Ticks;

        for (int tx = lastX; tx < prefs.textureWidth; tx++)
        {
            for (int ty = 0; ty < prefs.textureHeight; ty++)
            {
                int cPos = ty * prefs.textureWidth + tx;

                Vector3 curPoint = beginPoint + new Vector3(tx * vScale.x, 0, ty * vScale.z);
                colors[cPos] = GetColor(curPoint, raycastDistance, raycastDirection, mLayer, ref lastRenderer, ref triangles, ref verticles, ref uv);
            }

            if (new TimeSpan(DateTime.Now.Ticks - startTicks).TotalSeconds >= 1)
            {
                lastX = tx;
                progress = (activeIndex + lastX / (float) prefs.textureWidth) / prefs.terrains.Count;
                return;
            }
        }

        lastX = 0;

        SetTexturesToTerrain(t, GetTexture(t));

        colors = null;
        EditorUtility.UnloadUnusedAssetsImmediate();
        GC.Collect();

        activeIndex++;
        progress = activeIndex / (float) prefs.terrains.Count;
        if (activeIndex >= prefs.terrains.Count)
        {
            activeIndex = 0;
            phase = MeshToTerrainPhase.finish;
        }
    }

    internal class AverageHoleItem
    {
        public int x;
        public int y;
        public float newValue;

        public AverageHoleItem(int x, int y, float newValue)
        {
            this.x = x;
            this.y = y;
            this.newValue = newValue;
        }
    }
}