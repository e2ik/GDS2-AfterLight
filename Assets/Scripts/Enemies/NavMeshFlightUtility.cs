using UnityEngine;
using UnityEngine.AI;

namespace Enemies
{
    public static class NavMeshFlightUtility
    {
        public static bool TrySamplePoint(Vector2 point, float maxDistance, out Vector2 result)
        {
            Vector3 point3D = new Vector3(point.x, point.y, 0f);

            if (NavMesh.SamplePosition(point3D, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
            {
                result = new Vector2(hit.position.x, hit.position.y);
                return true;
            }

            result = point;
            return false;
        }
        
        public static bool TryCalculatePath(Vector2 from, Vector2 to, NavMeshPath path)
        {
            Vector3 fromPos = new Vector3(from.x, from.y, 0f);
            Vector3 toPos = new Vector3(to.x, to.y, 0f);
            bool pathExisits = NavMesh.CalculatePath(fromPos, toPos, NavMesh.AllAreas, path);
            return pathExisits && path.status == NavMeshPathStatus.PathComplete;
        }

        public static Vector2 GetSteeringDirection(NavMeshPath path, ref int cornerIndex, Vector2 currentPos,
            float cornerReachDistance)
        {
            if (path.corners.Length == 0)
                return Vector2.zero;

            cornerIndex = Mathf.Clamp(cornerIndex, 0, path.corners.Length - 1);
            Vector2 target = path.corners[cornerIndex];

            if (Vector2.Distance(currentPos, target) <= cornerReachDistance && cornerIndex < path.corners.Length - 1)
                cornerIndex++;

            target = path.corners[cornerIndex];
            return (target - currentPos).normalized;
        }
    }
}
