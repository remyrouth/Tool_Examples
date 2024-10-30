using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// A Unity Editor tool for placing grass objects in the scene with various controls and visualization options.
/// This tool allows for intuitive grass placement with a brushing system, similar to terrain painting.
/// 
/// Features:
/// - Adjustable brush size with visual preview
/// - Minimum distance enforcement between grass instances
/// - Preview of valid placement positions
/// - Undo/Redo support
/// - Real-time visualization of brush area and placement points
/// 
/// Usage:
/// 1. Open via Tools > Grass Placement Tool
/// 2. Assign a grass prefab and target surface
/// 3. Hold right mouse button to place grass
/// 4. Use scroll wheel to adjust brush size
/// </summary>
[ExecuteInEditMode]
public class GrassPlacementTool : EditorWindow
{
    // Core references and placement settings
    private GameObject grassPrefab;               // The grass prefab to be instantiated
    private float timeSinceLastSpawn = 0f;       // Timer for controlling spawn rate
    private const float SPAWN_INTERVAL = 0.01f;   // Time between spawn attempts while holding right click
    private const float MIN_DISTANCE = 0.25f;     // Minimum distance required between grass instances
    private bool isPlacing = false;              // Tracks if the right mouse button is being held
    private Transform planeSurface;              // The surface where grass will be placed
    
    // Position tracking and preview system
    private List<Vector3> placedPositions = new List<Vector3>();     // Tracks all placed grass positions
    private List<Vector3> validSpawnPoints = new List<Vector3>();    // Stores currently valid spawn points for preview
    private Vector3 currentMousePosition;                            // Current mouse position in world space
    
    // Brush settings and controls
    private float brushSize = 2f;                            // Current brush diameter
    private const float MIN_BRUSH_SIZE = 0.5f;               // Minimum allowed brush size
    private const float MAX_BRUSH_SIZE = 10f;                // Maximum allowed brush size
    private const float SCROLL_SENSITIVITY = 0.5f;           // Mouse wheel sensitivity for brush size adjustment
    
    // Visualization settings
    private bool showGizmos = true;                          // Toggle for visual helpers
    private Color gizmoColor = new Color(0f, 1f, 0f, 0.3f); // Color of brush area preview
    private Color validSpawnColor = new Color(0f, 1f, 0f, 0.5f); // Color of valid spawn point indicators
    
    // Preview system settings
    private const int PREVIEW_POINTS = 100;                  // Number of preview points to display
    private float previewUpdateTimer = 0f;                   // Timer for controlling preview updates
    private const float PREVIEW_UPDATE_INTERVAL = 0.5f;      // Time between preview point updates

    /// <summary>
    /// Creates and shows the Grass Placement Tool window.
    /// </summary>
    [MenuItem("Tools/Grass Placement Tool")]
    public static void ShowWindow()
    {
        var window = GetWindow<GrassPlacementTool>("Grass Placer");
        window.OnEnable();
    }

    /// <summary>
    /// Initializes the tool when the window is enabled.
    /// Sets up necessary event subscriptions and initializes the preview system.
    /// </summary>
    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.update += OnEditorUpdate;
        UpdateValidSpawnPoints();
    }

    /// <summary>
    /// Cleans up event subscriptions when the window is disabled.
    /// </summary>
    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.update -= OnEditorUpdate;
    }

    /// <summary>
    /// Handles continuous updates in the editor.
    /// Updates preview points and handles continuous grass placement while the right mouse button is held.
    /// </summary>
    private void OnEditorUpdate()
    {
        // Update preview points periodically
        previewUpdateTimer += Time.deltaTime;
        if (previewUpdateTimer >= PREVIEW_UPDATE_INTERVAL)
        {
            UpdateValidSpawnPoints();
            previewUpdateTimer = 0f;
            SceneView.RepaintAll();
        }

        // Handle continuous grass placement
        if (isPlacing)
        {
            timeSinceLastSpawn += Time.deltaTime;
            if (timeSinceLastSpawn >= SPAWN_INTERVAL)
            {
                TryPlaceGrass();
                timeSinceLastSpawn = 0f;
            }
        }
    }

    /// <summary>
    /// Draws the tool's UI window with all configuration options.
    /// </summary>
    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();

        GUILayout.Label("Grass Placement Settings", EditorStyles.boldLabel);
        
        grassPrefab = (GameObject)EditorGUILayout.ObjectField("Grass Prefab", grassPrefab, typeof(GameObject), false);
        planeSurface = (Transform)EditorGUILayout.ObjectField("Surface to Place On", planeSurface, typeof(Transform), true);
        
        EditorGUILayout.Space();
        GUILayout.Label("Brush Settings", EditorStyles.boldLabel);
        brushSize = EditorGUILayout.Slider("Brush Size", brushSize, MIN_BRUSH_SIZE, MAX_BRUSH_SIZE);

        EditorGUILayout.Space();
        GUILayout.Label("Visualization", EditorStyles.boldLabel);
        showGizmos = EditorGUILayout.Toggle("Show Gizmos", showGizmos);
        gizmoColor = EditorGUILayout.ColorField("Brush Area Color", gizmoColor);
        validSpawnColor = EditorGUILayout.ColorField("Valid Spawn Point Color", validSpawnColor);

        if (EditorGUI.EndChangeCheck())
        {
            UpdateValidSpawnPoints();
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Controls:\n" +
            "- Hold right mouse button to place grass\n" +
            "- Scroll wheel to adjust brush size\n" +
            "- Current brush size: " + brushSize.ToString("F1"),
            MessageType.Info
        );

        if (GUILayout.Button("Clear All Grass"))
        {
            ClearAllGrass();
        }
    }

    /// <summary>
    /// Updates the list of valid spawn points within the brush area.
    /// Checks for minimum distance requirements and surface collision.
    /// </summary>
    private void UpdateValidSpawnPoints()
    {
        if (!planeSurface) return;

        validSpawnPoints.Clear();

        // Generate random points within brush area
        for (int i = 0; i < PREVIEW_POINTS; i++)
        {
            float randomRadius = Random.Range(0f, brushSize);
            float randomAngle = Random.Range(0f, 2f * Mathf.PI);
            
            Vector3 randomOffset = new Vector3(
                randomRadius * Mathf.Cos(randomAngle),
                0f,
                randomRadius * Mathf.Sin(randomAngle)
            );

            Vector3 pointToCheck = currentMousePosition + randomOffset;
            Ray ray = new Ray(pointToCheck + Vector3.up * 10f, Vector3.down);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                bool tooClose = false;
                foreach (Vector3 pos in placedPositions)
                {
                    if (Vector3.Distance(hit.point, pos) < MIN_DISTANCE)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    validSpawnPoints.Add(hit.point);
                }
            }
        }
    }

    /// <summary>
    /// Handles Scene GUI events and visualization.
    /// Draws the brush preview, handles input, and updates the tool's state.
    /// </summary>
    /// <param name="sceneView">The current SceneView being rendered</param>
    private void OnSceneGUI(SceneView sceneView)
    {
        if (!showGizmos || !planeSurface) return;

        // Handle mouse input and raycast to surface
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        RaycastHit hit;

        // Handle scroll wheel for brush size
        if (Event.current.type == EventType.ScrollWheel)
        {
            float scrollDelta = -Event.current.delta.y * SCROLL_SENSITIVITY;
            brushSize = Mathf.Clamp(brushSize + scrollDelta, MIN_BRUSH_SIZE, MAX_BRUSH_SIZE);
            UpdateValidSpawnPoints();
            Event.current.Use();
            Repaint();
        }

        if (Physics.Raycast(ray, out hit))
        {
            currentMousePosition = hit.point;

            // Draw brush area
            Handles.color = gizmoColor;
            Handles.DrawWireDisc(currentMousePosition, Vector3.up, brushSize);
            
            // Draw semi-transparent brush area
            Handles.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.1f);
            Handles.DrawSolidDisc(currentMousePosition, Vector3.up, brushSize);

            // Draw valid spawn points
            Handles.color = validSpawnColor;
            foreach (Vector3 point in validSpawnPoints)
            {
                Handles.DrawSolidDisc(point, Vector3.up, 0.1f);
                Color radiusColor = validSpawnColor;
                radiusColor.a *= 0.2f;
                Handles.color = radiusColor;
                Handles.DrawWireDisc(point, Vector3.up, MIN_DISTANCE);
                Handles.color = validSpawnColor;
            }

            // Draw brush size text
            Handles.BeginGUI();
            Vector3 screenPoint = HandleUtility.WorldToGUIPoint(currentMousePosition);
            Rect labelRect = new Rect(screenPoint.x - 50, screenPoint.y - 40, 100, 20);
            GUI.Label(labelRect, $"Size: {brushSize:F1}", new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            });
            Handles.EndGUI();
        }

        // Handle right mouse button for placement
        if (Event.current.type == EventType.MouseDown && Event.current.button == 1)
        {
            isPlacing = true;
            Event.current.Use();
        }
        else if (Event.current.type == EventType.MouseUp && Event.current.button == 1)
        {
            isPlacing = false;
            Event.current.Use();
        }

        sceneView.Repaint();
    }

    /// <summary>
    /// Attempts to place a grass instance at a random position within the brush area.
    /// Checks for minimum distance requirements and handles proper object placement.
    /// </summary>
    private void TryPlaceGrass()
    {
        if (!grassPrefab || !planeSurface) return;

        float randomRadius = Random.Range(0f, brushSize);
        float randomAngle = Random.Range(0f, 2f * Mathf.PI);
        
        Vector3 randomOffset = new Vector3(
            randomRadius * Mathf.Cos(randomAngle),
            0f,
            randomRadius * Mathf.Sin(randomAngle)
        );

        Vector3 spawnPoint = currentMousePosition + randomOffset;
        Ray ray = new Ray(spawnPoint + Vector3.up * 10f, Vector3.down);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            bool tooClose = false;
            foreach (Vector3 pos in placedPositions)
            {
                if (Vector3.Distance(hit.point, pos) < MIN_DISTANCE)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                GameObject grass = PrefabUtility.InstantiatePrefab(grassPrefab) as GameObject;
                grass.transform.position = hit.point;
                grass.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                grass.transform.parent = planeSurface;
                
                placedPositions.Add(hit.point);
                UpdateValidSpawnPoints();
                
                Undo.RegisterCreatedObjectUndo(grass, "Place Grass");
            }
        }
    }

    /// <summary>
    /// Removes all grass instances from the scene that have the "Grass" tag.
    /// Supports undo/redo operations.
    /// </summary>
    private void ClearAllGrass()
    {
        GameObject[] existingGrass = GameObject.FindGameObjectsWithTag("Grass");
        foreach (GameObject grass in existingGrass)
        {
            Undo.DestroyObjectImmediate(grass);
        }
        placedPositions.Clear();
        UpdateValidSpawnPoints();
    }

    /// <summary>
    /// Cleans up event subscriptions when the window is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.update -= OnEditorUpdate;
    }
}
