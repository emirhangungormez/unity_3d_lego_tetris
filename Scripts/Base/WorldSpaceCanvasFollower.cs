using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class WorldSpaceCanvasFollower : MonoBehaviour
{
    [Tooltip("Camera transform to follow. If null, Camera.main will be used at Start/LateUpdate.")]
    public Transform targetCamera;

    [Tooltip("Offset from the camera in local camera space. Use (0,0,2) to place canvas 2 units in front of camera.")]
    public Vector3 offset = new Vector3(0f, 0f, 2f);

    [Tooltip("If true the canvas rotation will match the camera rotation exactly.")]
    public bool matchCameraRotation = true;

    [Tooltip("If true and matchCameraRotation is false, the canvas will face the camera (LookAt).")]
    public bool faceCamera = true;

    void Start()
    {
        if (targetCamera == null && Camera.main != null)
            targetCamera = Camera.main.transform;
    }

    void LateUpdate()
    {
        if (targetCamera == null)
        {
            if (Camera.main != null)
            {
                targetCamera = Camera.main.transform;
            }
            else return;
        }

        FollowCamera();
    }

    void FollowCamera()
    {
        // Compute world position from camera local offset to avoid extra allocations
        Vector3 worldPos = targetCamera.TransformPoint(offset);
        transform.position = worldPos;

        if (matchCameraRotation)
        {
            transform.rotation = targetCamera.rotation;
        }
        else if (faceCamera)
        {
            // Make the canvas face the camera: its forward should point opposite camera.forward
            Vector3 dir = transform.position - targetCamera.position;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
