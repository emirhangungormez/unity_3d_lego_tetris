using UnityEngine;

public class GameManager : MonoBehaviour
{
    public GridManager gridManager;
    public GameObject cube;
    
    private Vector2Int currentGridPosition;
    private Vector2Int cubeSize;
    
    void Start()
    {
        if (cube == null)
        {
            cube = GameObject.FindGameObjectWithTag("Cube");
            if (cube == null)
            {
                Debug.LogError("Cube bulunamadı!");
                return;
            }
        }
        
        CalculateCubeSize();
        PlaceCubeAtGrid(0, 0);
    }
    
    void Update()
    {
        if (cube == null) return;
        HandleCubeMovement();
    }
    
    void CalculateCubeSize()
    {
        Vector3 localScale = cube.transform.localScale;
        cubeSize = new Vector2Int(
            Mathf.RoundToInt(localScale.x),
            Mathf.RoundToInt(localScale.z)
        );
        Debug.Log($"Cube boyutu: {cubeSize.x}x{cubeSize.y}");
    }
    
    void PlaceCubeAtGrid(int x, int y)
    {
        currentGridPosition = new Vector2Int(x, y);
        cube.transform.position = gridManager.GetGridPosition(currentGridPosition, cubeSize);
        Debug.Log($"Cube ({x},{y}) -> {cube.transform.position}");
    }
    
    void HandleCubeMovement()
    {
        Vector2Int newPosition = currentGridPosition;
        
        if (Input.GetKeyDown(KeyCode.UpArrow)) newPosition.y += 1;
        else if (Input.GetKeyDown(KeyCode.DownArrow)) newPosition.y -= 1;
        else if (Input.GetKeyDown(KeyCode.RightArrow)) newPosition.x += 1;
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) newPosition.x -= 1;
        
        if (newPosition != currentGridPosition && gridManager.IsValidPosition(newPosition, cubeSize))
        {
            MoveCubeToGrid(newPosition.x, newPosition.y);
        }
    }
    
    void MoveCubeToGrid(int x, int y)
    {
        currentGridPosition = new Vector2Int(x, y);
        cube.transform.position = gridManager.GetGridPosition(currentGridPosition, cubeSize);
    }
}