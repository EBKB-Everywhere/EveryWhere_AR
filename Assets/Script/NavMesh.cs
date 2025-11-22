using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(LineRenderer))]
public class NavPathVisualizer : MonoBehaviour
{
    public Transform target;
    private NavMeshAgent agent;
    private LineRenderer lineRenderer;
    private NavMeshPath path;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        lineRenderer = GetComponent<LineRenderer>();
        path = new NavMeshPath();

        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.green;
        lineRenderer.endColor = Color.green;
    }

    void Update()
    {
        if (target == null) return;

        // 1. 경로를 매 프레임 다시 계산
        NavMesh.CalculatePath(transform.position, target.position, NavMesh.AllAreas, path);

        // 2. 라인 갱신
        UpdateLine(path);
    }

    void UpdateLine(NavMeshPath navPath)
    {
        if (navPath.corners.Length < 2) return;

        lineRenderer.positionCount = navPath.corners.Length;
        lineRenderer.SetPositions(navPath.corners);
    }
}