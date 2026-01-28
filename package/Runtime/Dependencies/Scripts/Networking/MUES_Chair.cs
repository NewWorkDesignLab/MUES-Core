using Fusion;
using System.Collections;
using UnityEngine;

public class MUES_Chair : MUES_AnchoredNetworkBehaviour
{   
    [Tooltip("Base size (X, Z) of the detection box.")]
    public Vector2 detectionBaseSize = new Vector2(0.5f, 0.5f);
    
    [Tooltip("Height of the detection box starting from the transform's pivot.")]
    public float detectionHeight = 1.2f;

    [Tooltip("Layer of the objects (Avatars) that trigger occupancy.")]
    public LayerMask detectionLayer;

    [Networked] public NetworkBool IsOccupied { get; set; } // Whether the chair is currently occupied

    private Vector2 detectionOffset = Vector2.zero; // Offset of the detection box from the chair's pivot
    private readonly Collider[] _results = new Collider[1]; // Reusable array for overlap results

    private MUES_NetworkedTransform _networkedTransform; // Reference to the NetworkedTransform for grab state

    public override void Spawned()
    {
        base.Spawned();

        if (MUES_RoomVisualizer.Instance != null && !MUES_RoomVisualizer.Instance.chairsInScene.Contains(this))
            MUES_RoomVisualizer.Instance.chairsInScene.Add(this);

        _networkedTransform = GetComponent<MUES_NetworkedTransform>();
        
        StartCoroutine(InitChairRoutine());
    }

    /// <summary>
    /// Initializes the chair anchor and sets initial position.
    /// </summary>
    private IEnumerator InitChairRoutine()
    {
        yield return InitAnchorRoutine();

        if (Object.HasStateAuthority || Object.HasInputAuthority)
        {
            WorldToAnchor();
            ConsoleMessage.Send(true, $"Chair - Authority initialized anchor offset: {LocalAnchorOffset}", Color.cyan);
        }
        else
        {
            float timeout = 5f;
            float elapsed = 0f;
            
            while (LocalAnchorOffset == Vector3.zero && 
                   LocalAnchorRotationOffset == Quaternion.identity && 
                   elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            AnchorToWorld();
            ConsoleMessage.Send(true, $"Chair - Non-authority applied anchor offset: {LocalAnchorOffset}", Color.cyan);
        }

        initialized = true;
    }

    public override void Render()
    {
        if (initialized && !Object.HasStateAuthority && !Object.HasInputAuthority && anchorReady)
            AnchorToWorld();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);

        if (MUES_RoomVisualizer.Instance != null && MUES_RoomVisualizer.Instance.chairsInScene != null)
        {
            try
            {
                MUES_RoomVisualizer.Instance.chairsInScene.Remove(this);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Chair - Failed to remove from chairsInScene: {ex.Message}");
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!initialized || !anchorReady) return;

        bool hasAuth = false;
        try
        {
            if (Object == null || !Object.IsValid) return;
            hasAuth = Object.HasStateAuthority || Object.HasInputAuthority;
        }
        catch { return; }

        if (hasAuth)
        {
            bool isBeingGrabbed = _networkedTransform != null && IsNetworkedTransformGrabbed();
            
            if (isBeingGrabbed)
                WorldToAnchor();

            Vector3 localOffset = new Vector3(detectionOffset.x, detectionHeight * 0.5f, detectionOffset.y);
            Vector3 worldOffset = transform.rotation * localOffset; 
            Vector3 center = transform.position + worldOffset;

            Vector3 halfSize = new Vector3(detectionBaseSize.x * 0.5f, detectionHeight * 0.5f, detectionBaseSize.y * 0.5f);

            int hits = Physics.OverlapBoxNonAlloc(center, halfSize, _results, transform.rotation, detectionLayer);

            bool isHit = hits > 0;

            try
            {
                if (IsOccupied != isHit)
                    IsOccupied = isHit;
            }
            catch { }
        }
    }

    /// <summary>
    /// Checks if the NetworkedTransform is currently being grabbed.
    /// </summary>
    private bool IsNetworkedTransformGrabbed()
    {
        if (_networkedTransform == null) return false;
        
        var field = typeof(MUES_NetworkedTransform).GetField("_isBeingGrabbed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (field != null)
            return (bool)field.GetValue(_networkedTransform);
        
        return false;
    }

    private void OnDrawGizmos()
    {
        bool isOccupiedSafe = false;

        if (Object != null && Object.IsValid)
            isOccupiedSafe = IsOccupied;

        Gizmos.color = isOccupiedSafe ? new Color(1f, 0f, 0f, 0.4f) : new Color(0f, 1f, 0f, 0.4f);

        Matrix4x4 oldMatrix = Gizmos.matrix;

        Vector3 localOffset = new Vector3(detectionOffset.x, detectionHeight * 0.5f, detectionOffset.y);
        Vector3 worldOffset = transform.rotation * localOffset;
        Vector3 center = transform.position + worldOffset;
        
        Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
        Vector3 size = new Vector3(detectionBaseSize.x, detectionHeight, detectionBaseSize.y);
        
        Gizmos.DrawCube(Vector3.zero, size);
        Gizmos.DrawWireCube(Vector3.zero, size);
        
        Gizmos.matrix = oldMatrix;
    }
}