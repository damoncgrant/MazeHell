using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Builds the labyrinth from the Twine game's room grid (setup.rooms).
//
// Usage: put this on an empty "Labyrinth" GameObject in the Labyrinth scene, optionally drag in
// materials / the Player, then right-click the component header > "Build Maze". Everything is
// created as children of this object and saved with the scene. "Clear Maze" removes it again.
//
// Like the Twine game, every room connects to all four neighbours; only the outer edge is walled off.
// Each inner doorway is covered in black mist: stepping into it fades the screen and moves you into the next room.
// Grid layout: row 0 is the north edge, column 0 is the west edge. North = +Z in the world.
//
// Room codes (same as the Twine StoryScript):
//   1 = empty room           2-6 = stone (2 blue, 3 red, 4 green, 5 purple, 6 lime)
//   11 -> 12, 21 -> 22 = teleporter -> compass room          30 = altar
public class MazeBuilder : MonoBehaviour
{
    [Header("Layout (copied from setup.rooms in the Twine StoryScript)")]
    [TextArea(15, 20)]
    [SerializeField] private string grid =
        "1,1,1,1,1,1,1,1,1,1,1,1,1,1,1\n" +
        "1,1,1,1,1,6,1,1,1,1,1,1,1,21,1\n" +
        "22,1,1,1,1,1,1,1,1,1,1,1,1,1,1\n" +
        "1,1,1,1,1,1,1,1,1,1,30,1,1,1,1\n" +
        "1,1,1,1,1,11,1,1,1,1,1,1,1,1,1\n" +
        "1,1,1,1,1,1,1,1,1,1,1,1,5,1,1\n" +
        "1,1,1,1,1,1,1,1,1,1,1,1,1,1,1\n" +
        "1,1,1,1,4,1,1,1,1,1,1,1,1,1,1\n" +
        "1,1,1,1,1,1,1,1,1,1,1,1,1,1,1\n" +
        "1,1,1,1,1,1,1,1,1,1,1,12,1,1,1\n" +
        "1,3,1,1,1,1,1,1,1,1,1,1,1,1,1\n" +
        "1,1,1,1,1,1,1,2,1,1,1,1,1,1,1\n" +
        "1,1,1,1,1,1,1,1,1,1,1,1,1,1,1\n" +
        "1,1,1,1,1,1,1,1,1,1,1,1,1,1,1\n" +
        "1,1,1,1,1,1,1,1,1,1,1,1,1,1,1";

    [Tooltip("Starting room (column x, row y), like $x = 7, $y = 14 in MazeStart.")]
    [SerializeField] private Vector2Int startRoom = new Vector2Int(7, 14);

    [Header("Size (metres)")]
    [SerializeField] private float roomSize = 16f;
    [SerializeField] private float wallHeight = 6f;
    [SerializeField] private float wallThickness = 0.4f;
    [SerializeField] private float doorWidth = 2.5f;
    [SerializeField] private float doorHeight = 3.5f;
    [SerializeField] private bool buildCeiling = true;

    [Header("Look (all optional)")]
    [SerializeField] private Material floorMaterial;
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Material ceilingMaterial;
    [Tooltip("A dim point light in every room.")]
    [SerializeField] private bool roomLights = true;
    [SerializeField] private Color lightColor = new Color(1f, 0.85f, 0.7f);
    [SerializeField] private float lightIntensity = 3f;

    [Header("Doorway mist")]
    [Tooltip("Black mist over every inner doorway. Walking into it fades out and moves you to the next room.")]
    [SerializeField] private bool doorwayMist = true;
    [Tooltip("Optional material for the mist curtain (a transparent black material is made if empty).")]
    [SerializeField] private Material mistMaterial;
    [Range(0f, 1f)] [SerializeField] private float mistOpacity = 0.97f;
    [Tooltip("Drifting dark particles in front of each doorway.")]
    [SerializeField] private bool mistParticles = true;

    [Header("Special rooms (optional prefabs; simple shapes are used if empty)")]
    [SerializeField] private GameObject stonePrefab;
    [SerializeField] private GameObject altarPrefab;
    [SerializeField] private GameObject compassPrefab;

    [Header("Player")]
    [Tooltip("If set, the player is moved into the start room facing north when the maze is built.")]
    [SerializeField] private Transform player;

    [Tooltip("When the game is playing, move the player into the start room facing north as the scene loads.")]
    [SerializeField] private bool spawnPlayerInStartRoom = true;

    private void Start()
    {
        if (!spawnPlayerInStartRoom) return;

        Transform target = player != null ? player : FindFirstObjectByType<FirstPersonController>()?.transform;
        Transform start = FindStartRoom();
        if (target == null || start == null)
        {
            Debug.LogWarning("[MazeBuilder] Couldn't spawn the player: " +
                             (target == null ? "no FirstPersonController in the scene." : "the maze hasn't been built yet."), this);
            return;
        }

        player = target;
        PlacePlayer(start);
    }

    private Transform FindStartRoom()
    {
        Transform root = transform.Find(RootName);
        if (root == null) return null;
        string prefix = $"Room {startRoom.x},{startRoom.y} ";
        foreach (Transform room in root)
            if (room.name.StartsWith(prefix)) return room;
        return null;
    }

    static readonly Dictionary<int, (string name, Color color)> Stones = new Dictionary<int, (string, Color)>
    {
        { 2, ("blue",   new Color(0.2f, 0.4f, 1f)) },
        { 3, ("red",    new Color(1f, 0.15f, 0.15f)) },
        { 4, ("green",  new Color(0.1f, 0.8f, 0.2f)) },
        { 5, ("purple", new Color(0.6f, 0.2f, 0.9f)) },
        { 6, ("lime",   new Color(0.7f, 1f, 0.2f)) },
    };
    static readonly Dictionary<int, int> Teleports = new Dictionary<int, int> { { 11, 12 }, { 21, 22 } };
    const int AltarRoom = 30;

    const string RootName = "Generated Maze";

    [ContextMenu("Build Maze")]
    public void Build()
    {
        int[,] rooms = ParseGrid();
        int rows = rooms.GetLength(0), cols = rooms.GetLength(1);

        Clear();
        var root = new GameObject(RootName).transform;
        root.SetParent(transform, false);

        var roomTransforms = new Transform[rows, cols];
        sharedMist = doorwayMist ? (mistMaterial != null ? mistMaterial : MakeMistMaterial()) : null;
        sharedParticleMat = doorwayMist && mistParticles ? MakeParticleMaterial() : null;

        for (int y = 0; y < rows; y++)
        for (int x = 0; x < cols; x++)
        {
            Transform room = new GameObject($"Room {x},{y} ({rooms[y, x]})").transform;
            room.SetParent(root, false);
            room.localPosition = RoomCenter(x, y, rows);
            roomTransforms[y, x] = room;

            // Floor and ceiling
            Box(room, "Floor", new Vector3(0, -0.05f, 0), new Vector3(roomSize, 0.1f, roomSize), floorMaterial);
            if (buildCeiling)
                Box(room, "Ceiling", new Vector3(0, wallHeight + 0.05f, 0), new Vector3(roomSize, 0.1f, roomSize), ceilingMaterial);

            // Each room builds its north and west walls; the last row/column also build south/east.
            // A wall gets a doorway unless it's on the outer edge.
            Wall(room, Vector3.forward, doorway: y > 0);            // north
            Wall(room, Vector3.left, doorway: x > 0);               // west
            if (y == rows - 1) Wall(room, Vector3.back, doorway: false);   // south edge
            if (x == cols - 1) Wall(room, Vector3.right, doorway: false);  // east edge

            if (roomLights)
            {
                var light = new GameObject("Light").AddComponent<Light>();
                light.transform.SetParent(room, false);
                light.transform.localPosition = new Vector3(0, wallHeight - 0.4f, 0);
                light.type = LightType.Point;
                light.range = roomSize * 0.85f;
                light.intensity = lightIntensity;
                light.color = lightColor;
                light.shadows = LightShadows.None;
            }
        }

        // Special rooms
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < cols; x++)
        {
            int code = rooms[y, x];
            Transform room = roomTransforms[y, x];

            if (Stones.TryGetValue(code, out var stone))
                BuildStone(room, stone.name, stone.color);
            else if (code == AltarRoom)
                BuildAltar(room);
            else if (Teleports.TryGetValue(code, out int destCode))
                BuildTeleporter(room, FindRoom(rooms, roomTransforms, destCode));
            else if (Teleports.ContainsValue(code))
                BuildCompassRoom(room);
        }

        // Start room: leaving it follows MazeStart's "hiddenContinue" link into the Maze passage.
        Transform start = roomTransforms[startRoom.y, startRoom.x];
        var startZone = RoomTrigger(start, "Start Zone");
        startZone.gameObject.AddComponent<StoryInteractable>()
            .Configure(StoryInteractable.Activation.Exit, links: new[] { "hiddenContinue", "continue" }, singleUse: true);

        if (player != null)
            PlacePlayer(start);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Maze");
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
        Debug.Log($"[MazeBuilder] Built {rows * cols} rooms ({cols} x {rows}).", this);
    }

    [ContextMenu("Clear Maze")]
    public void Clear()
    {
        Transform old = transform.Find(RootName);
        if (old == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying) { Undo.DestroyObjectImmediate(old.gameObject); return; }
#endif
        Destroy(old.gameObject);
    }

    // ------------------------------------------------------------------

    private Vector3 RoomCenter(int x, int y, int rows)
    {
        // Row 0 is the north edge, so higher rows are further south (smaller Z).
        return new Vector3(x * roomSize, 0f, (rows - 1 - y) * roomSize);
    }

    private void Wall(Transform room, Vector3 side, bool doorway)
    {
        var holder = new GameObject(side == Vector3.forward ? "Wall N" : side == Vector3.back ? "Wall S" :
                                    side == Vector3.left ? "Wall W" : "Wall E").transform;
        holder.SetParent(room, false);
        holder.localPosition = side * (roomSize / 2f);
        holder.localRotation = Quaternion.LookRotation(side);   // local X runs along the wall

        float length = roomSize + wallThickness; // overlap at the corners
        if (!doorway)
        {
            Box(holder, "Solid", new Vector3(0, wallHeight / 2f, 0), new Vector3(length, wallHeight, wallThickness), wallMaterial);
            return;
        }

        float sideWidth = (length - doorWidth) / 2f;
        float sideOffset = (doorWidth + sideWidth) / 2f;
        Box(holder, "Left", new Vector3(-sideOffset, wallHeight / 2f, 0), new Vector3(sideWidth, wallHeight, wallThickness), wallMaterial);
        Box(holder, "Right", new Vector3(sideOffset, wallHeight / 2f, 0), new Vector3(sideWidth, wallHeight, wallThickness), wallMaterial);
        float lintel = wallHeight - doorHeight;
        if (lintel > 0.01f)
            Box(holder, "Top", new Vector3(0, doorHeight + lintel / 2f, 0), new Vector3(doorWidth, lintel, wallThickness), wallMaterial);

        if (doorwayMist)
            BuildMist(holder);
    }

    private Material sharedMist, sharedParticleMat;

    private void BuildMist(Transform doorway)
    {
        // The curtain: a thin black box filling the doorway, so you can't see into the next room.
        var curtain = GameObject.CreatePrimitive(PrimitiveType.Cube);
        curtain.name = "Mist";
        curtain.transform.SetParent(doorway, false);
        curtain.transform.localPosition = new Vector3(0, doorHeight / 2f, 0);
        curtain.transform.localScale = new Vector3(doorWidth, doorHeight, wallThickness * 0.5f);
        DestroyImmediate(curtain.GetComponent<Collider>());
        var renderer = curtain.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = sharedMist;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // The trigger: a bit deeper than the wall, so it fires as you step into the mist.
        var trigger = curtain.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(1f, 1f, (wallThickness + 1f) / (wallThickness * 0.5f)); // in local (scaled) units
        var body = curtain.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        curtain.AddComponent<MazeDoorway>();

        if (sharedParticleMat != null)
            BuildMistParticles(doorway);
    }

    private void BuildMistParticles(Transform doorway)
    {
        var go = new GameObject("Mist Particles");
        go.transform.SetParent(doorway, false);
        go.transform.localPosition = new Vector3(0, doorHeight / 2f, 0);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = 4f;
        main.startSpeed = 0.15f;
        main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startColor = new Color(0f, 0f, 0f, 0.55f);
        main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var emission = ps.emission;
        emission.rateOverTime = 6f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(doorWidth, doorHeight, 1.2f);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.black, 0f), new GradientColorKey(Color.black, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.3f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        var psRenderer = go.GetComponent<ParticleSystemRenderer>();
        psRenderer.sharedMaterial = sharedParticleMat;
        psRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private Material MakeMistMaterial()
    {
        // URP Unlit, transparent, black.
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.name = "Doorway Mist";
        mat.SetFloat("_Surface", 1f);   // transparent
        mat.SetFloat("_Blend", 0f);     // alpha
        mat.SetFloat("_Cull", 0f);      // both sides
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetColor("_BaseColor", new Color(0f, 0f, 0f, mistOpacity));
        return mat;
    }

    private static Material MakeParticleMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        var mat = new Material(shader);
        mat.name = "Mist Particles";
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        // Soft round particles: use Unity's built-in default particle texture if available.
#if UNITY_EDITOR
        var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
        if (tex != null) mat.SetTexture("_BaseMap", tex);
#endif
        mat.SetColor("_BaseColor", Color.black);
        return mat;
    }

    private static Transform Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        go.isStatic = true;
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }

    private Transform RoomTrigger(Transform room, string name)
    {
        var zone = new GameObject(name);
        zone.transform.SetParent(room, false);
        zone.transform.localPosition = new Vector3(0, wallHeight / 2f, 0);
        var box = zone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        // Slightly smaller than the room, so you're properly inside before it fires.
        box.size = new Vector3(roomSize - 1f, wallHeight, roomSize - 1f);
        var body = zone.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        return zone.transform;
    }

    private void BuildStone(Transform room, string stoneName, Color color)
    {
        GameObject stone = stonePrefab != null ? Instantiate(stonePrefab) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
        stone.name = $"{char.ToUpper(stoneName[0])}{stoneName.Substring(1)} Stone";
        stone.transform.SetParent(room, false);
        stone.transform.localPosition = new Vector3(0, 0.25f, 0);
        if (stonePrefab == null)
        {
            stone.transform.localScale = Vector3.one * 0.35f;
            stone.GetComponent<MeshRenderer>().sharedMaterial = GlowMaterial(color);
        }

        stone.AddComponent<StoryInteractable>().Configure(
            StoryInteractable.Activation.Interact,
            goToPassage: "StonePickup",
            setBoolVar: stoneName + "Stone",
            prompt: $"Pick up the {stoneName} stone",
            singleUse: true,
            hideAfterUse: true);
    }

    private void BuildAltar(Transform room)
    {
        GameObject altar = altarPrefab != null ? Instantiate(altarPrefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
        altar.name = "Altar";
        altar.transform.SetParent(room, false);
        if (altarPrefab == null)
        {
            altar.transform.localPosition = new Vector3(0, 0.5f, 0);
            altar.transform.localScale = new Vector3(1.6f, 1f, 0.9f);
        }
        else altar.transform.localPosition = Vector3.zero;

        // First E shows the Altar passage; once all 5 stones are held it has the "placeStones" link.
        altar.AddComponent<StoryInteractable>().Configure(
            StoryInteractable.Activation.Interact,
            links: new[] { "placeStones" },
            goToPassage: "Altar",
            promptWhenNoLink: "Examine the altar");
    }

    private void BuildTeleporter(Transform room, Transform destination)
    {
        if (destination == null)
        {
            Debug.LogWarning($"[MazeBuilder] Teleporter in {room.name} has no destination room.", this);
            return;
        }
        var zone = RoomTrigger(room, "Teleporter");
        zone.gameObject.AddComponent<MazeTeleporter>().destination = destination;
    }

    private void BuildCompassRoom(Transform room)
    {
        GameObject compass;
        if (compassPrefab != null)
        {
            compass = Instantiate(compassPrefab);
            compass.transform.SetParent(room, false);
        }
        else
        {
            // A stand with an arrow on top pointing north (+Z).
            compass = new GameObject("Compass");
            compass.transform.SetParent(room, false);
            var stand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stand.name = "Stand";
            stand.transform.SetParent(compass.transform, false);
            stand.transform.localPosition = new Vector3(0, 0.5f, 0);
            stand.transform.localScale = new Vector3(0.4f, 0.5f, 0.4f);
            var needle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            needle.name = "Needle (points north)";
            needle.transform.SetParent(compass.transform, false);
            needle.transform.localPosition = new Vector3(0, 1.05f, 0.1f);
            needle.transform.localScale = new Vector3(0.06f, 0.04f, 0.5f);
            needle.GetComponent<MeshRenderer>().sharedMaterial = GlowMaterial(Color.red);
        }
        compass.name = "Compass";

        var zone = RoomTrigger(room, "Compass Zone");
        zone.gameObject.AddComponent<MazeCompassRoom>();
    }

    private Transform FindRoom(int[,] rooms, Transform[,] transforms, int code)
    {
        for (int y = 0; y < rooms.GetLength(0); y++)
        for (int x = 0; x < rooms.GetLength(1); x++)
            if (rooms[y, x] == code) return transforms[y, x];
        return null;
    }

    private void PlacePlayer(Transform start)
    {
        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        player.SetPositionAndRotation(start.position + Vector3.up * 1.1f, Quaternion.LookRotation(Vector3.forward));
        if (controller != null) controller.enabled = true;
    }

    private static Material GlowMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 2f);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return mat;
    }

    private int[,] ParseGrid()
    {
        var lines = new List<int[]>();
        foreach (string raw in grid.Split('\n'))
        {
            string line = raw.Trim().Trim('[', ']', ',').Trim();
            if (line.Length == 0) continue;
            var nums = new List<int>();
            foreach (string part in line.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(part.Trim('[', ']'), out int n)) nums.Add(n);
            if (nums.Count > 0) lines.Add(nums.ToArray());
        }

        int rows = lines.Count, cols = lines[0].Length;
        var result = new int[rows, cols];
        for (int y = 0; y < rows; y++)
        {
            if (lines[y].Length != cols)
                throw new Exception($"[MazeBuilder] Row {y} has {lines[y].Length} rooms, expected {cols}.");
            for (int x = 0; x < cols; x++)
                result[y, x] = lines[y][x];
        }
        return result;
    }
}
