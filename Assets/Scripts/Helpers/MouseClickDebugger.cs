using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class MouseClickDebugger : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("--- MOUSE CLICK DETECTED ---");
            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = Input.mousePosition;

            List<RaycastResult> results = new List<RaycastResult>();
            if (EventSystem.current != null)
            {
                EventSystem.current.RaycastAll(eventData, results);
            }

            if (results.Count > 0)
            {
                Debug.Log($"<color=cyan>[UI RAYCAST] Hit {results.Count} UI element(s):</color>");
                for (int i = 0; i < results.Count; i++)
                {
                    string status = (i == 0) ? "<color=green>[TOP - BLOCKING]</color>" : "[BEHIND]";
                    Debug.Log($"{status} {i}: {results[i].gameObject.name} (Canvas: {results[i].module.gameObject.name})", results[i].gameObject);
                }
            }
            else
            {
                Debug.Log("<color=yellow>[UI RAYCAST] No UI elements hit under mouse.</color>");
            }

            Vector2 mouseWorldPos = Camera.main != null ? Camera.main.ScreenToWorldPoint(Input.mousePosition) : Vector2.zero;
            RaycastHit2D hit2D = Physics2D.Raycast(mouseWorldPos, Vector2.zero);

            if (hit2D.collider != null)
            {
                Debug.Log($"<color=orange>[2D PHYSICS] Hit World Object: {hit2D.collider.gameObject.name}</color>", hit2D.collider.gameObject);
            }
        }
    }
}