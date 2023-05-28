using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MarchingSquares : MonoBehaviour {
    [SerializeField] private MeshFilter _meshFilter;
    
    [SerializeField] private int _width = 16;
    [SerializeField] private int _height = 16;
    [SerializeField] private float _resolution = 1.0f;

    [SerializeField] private CellVertex[,] _inputMatrix;
    [SerializeField] private ContourCell[,] _cells;
    [SerializeField] private float _isoValue = 0.5f;
    [SerializeField] private float _perlinNoiseScale = 1.0f;
    [SerializeField] private bool _useInterpolation;
    
    [Header("Debug")] [SerializeField] private bool _drawSquares;
    
    private int _gridRows;
    private int _gridColumns;
    private float _gridStepX;
    private float _gridStepY;
    
    // Mesh stuff
    private List<int> _meshTriangles;
    private List<Vector3> _meshVertices;
    private Dictionary<Vector3, int> _pointVertexIndex;

    // Start is called before the first frame update
    private void Start() {
        CreateMarchingSquares();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void CreateMarchingSquares() {
        _gridRows = (int)(_height / _resolution);
        _gridColumns = (int)(_width / _resolution);
        _gridStepX = _width / (float)_gridColumns;
        _gridStepY = _height / (float)_gridRows;
        
        _meshTriangles = new List<int>();
        _meshVertices = new List<Vector3>();
        _pointVertexIndex = new Dictionary<Vector3, int>();
        
        InstantiateGrid();
        SetVertexPosition();
        ApplyPerlinNoise();
        UpdateBinaryMatrix();
        MarchSquares();
        BuildMesh();
    }

    private void InstantiateGrid() {
        _inputMatrix = new CellVertex[_gridRows, _gridColumns];
        _cells = new ContourCell[_gridRows - 1, _gridColumns - 1];
    }

    private void SetVertexPosition() {
        for (var row = 0; row < _gridRows; row++) {
            for (var col = 0; col < _gridColumns; col++) {
                _inputMatrix[row, col].Position =
                    new Vector2(col * _gridStepX, row * _gridStepY);
            }
        }
    }

    private void ApplyPerlinNoise() {
        for (var row = 0; row < _gridRows; row++) {
            for (var col = 0; col < _gridColumns; col++) {
                _inputMatrix[row, col].Weight = Mathf.PerlinNoise(
                    col * _perlinNoiseScale,
                    row * _perlinNoiseScale
                );
            }
        }
    }

    private void UpdateBinaryMatrix() {
        for (var row = 0; row < _gridRows; row++) {
            for (var col = 0; col < _gridColumns; col++)
                _inputMatrix[row, col].BinaryValue = _inputMatrix[row, col].Weight > _isoValue;
        }
    }

    private int GetCellCase(int[] vertices) {
        if (vertices.Length != 4)
            throw new Exception("There must be four vertices");

        // Convert the vertices into a decimal
        var code = 0;
        foreach (var vertex in vertices)
            code = (code << 1) | vertex;

        return 15-code;
    }

    private void AddMeshTriangle(int[] triIndices, int a, int b, int c) {
        _meshTriangles.Add(triIndices[a]);
        _meshTriangles.Add(triIndices[b]);
        _meshTriangles.Add(triIndices[c]);
    }

    private void AddMeshCell(Vector2[] points) {
        var triIndices = new List<int>();
        
        // Searches the map 
        foreach (var point in points) {
            if (!_pointVertexIndex.TryGetValue(point, out var index)) {
                _meshVertices.Add(point);
                _pointVertexIndex.Add(point, _meshVertices.Count - 1);
                index = _meshVertices.Count - 1;
            }
            
            triIndices.Add(index);
        }
        
        /* Adds triangles based on vertex count. Different cases
         handle the different weird shapes*/
        if(triIndices.Count >= 3)
            AddMeshTriangle(triIndices.ToArray(), 0, 1, 2);
        if(triIndices.Count is >= 4 and < 6)
            AddMeshTriangle(triIndices.ToArray(), 0, 2, 3);
        if(triIndices.Count == 5)
            AddMeshTriangle(triIndices.ToArray(), 0, 3, 4);
        if(triIndices.Count >= 6)
            AddMeshTriangle(triIndices.ToArray(), 3, 4, 5);
    }

    /// <summary>
    /// Lookup table for a cell's contour based on its case
    /// </summary>
    /// <param name="cellCase">The cells case from 0 to 15</param>
    /// <param name="row">The row of the cell</param>
    /// <param name="column">The column of the cell</param>
    private void SetCellFromCase(int cellCase, int row, int column) {
        var cell = new ContourCell(new List<Tuple<Vector2, Vector2>>());

        var topLeft = _inputMatrix[row, column];
        var topRight = _inputMatrix[row, column + 1];
        var bottomRight = _inputMatrix[row + 1, column + 1];
        var bottomLeft = _inputMatrix[row + 1, column];

        Vector2 GetPoint(CellVertex pointA, CellVertex pointB) =>
            !_useInterpolation
                ? GetEndpointByMidpoint(pointA.Position, pointB.Position)
                : GetEndpointByInterpolation(pointA.Position, pointB.Position, GetT(pointA, pointB, _isoValue));

        switch (cellCase) {
            case 0:
                AddMeshCell(new []{topLeft.Position, topRight.Position, bottomRight.Position, bottomLeft.Position});
                break;
            case 1:
                cell.Edges.Add(
                    Tuple.Create(
                        GetPoint(bottomLeft, topLeft),
                        GetPoint(bottomLeft, bottomRight)
                    )    
                );
                AddMeshCell(new []{topLeft.Position, topRight.Position, bottomRight.Position, cell.Edges[0].Item2, cell.Edges[0].Item1});
                break;
            case 2:
                cell.Edges.Add(
                    Tuple.Create(
                        GetPoint(bottomRight, topRight),
                        GetPoint(bottomRight, bottomLeft)
                    )    
                );
                AddMeshCell(new []{topLeft.Position, topRight.Position, cell.Edges[0].Item1, cell.Edges[0].Item2, bottomLeft.Position});
                
                break;
            case 3:
                cell.Edges.Add(
                    Tuple.Create(
                        GetPoint(bottomLeft, topLeft),
                        GetPoint(bottomRight, topRight)
                    )    
                );
                AddMeshCell(new []{topLeft.Position, topRight.Position, cell.Edges[0].Item2, cell.Edges[0].Item1});

                break;
            case 4:
                cell.Edges.Add(
                    Tuple.Create(
                        GetPoint(topRight, topLeft),
                        GetPoint(topRight, bottomRight)
                    )    
                );
                AddMeshCell(new []{topLeft.Position, cell.Edges[0].Item1, cell.Edges[0].Item2, bottomRight.Position, bottomLeft.Position});
                
                break;
            case 5:
                cell.Edges.Add(
                    Tuple.Create(
                        GetPoint(topRight, topLeft),
                        GetPoint(bottomLeft, topLeft)
                    )
                );
                cell.Edges.Add(
                    Tuple.Create(
                        GetPoint(bottomLeft, bottomRight),
                        GetPoint(topRight, bottomRight)
                    )
                );
                
                AddMeshCell(new []{
                    cell.Edges[0].Item1, topLeft.Position, cell.Edges[0].Item2, 
                    cell.Edges[1].Item2, bottomRight.Position, cell.Edges[1].Item1,
                });
                
                break;
            case 6:
                cell.Edges.Add(
                    Tuple.Create(
                        GetPoint(topRight, topLeft),
                        GetPoint(bottomRight, bottomLeft)
                    )
                );
                AddMeshCell(new []{topLeft.Position, cell.Edges[0].Item1, cell.Edges[0].Item2, bottomLeft.Position});
                
                break;
            case 7:
                cell.Edges.Add(
                    Tuple.Create(
                        GetPoint(topRight, topLeft),
                        GetPoint(bottomLeft, topLeft)
                    )
                );
                AddMeshCell(new []{cell.Edges[0].Item2, topLeft.Position, cell.Edges[0].Item1});

                break;
            case 8:
                cell.Edges.Add(
                    Tuple.Create(
                        GetPoint(topLeft, topRight),
                        GetPoint(topLeft, bottomLeft)
                    )
                );
                AddMeshCell(new []{cell.Edges[0].Item1, topRight.Position, bottomRight.Position, bottomLeft.Position, cell.Edges[0].Item2, });
                
                break;
            case 9:
                cell.Edges.Add(
                    Tuple.Create(
                        GetPoint(topLeft, topRight),
                        GetPoint(bottomLeft, bottomRight)
                    )
                );
                AddMeshCell(new []{cell.Edges[0].Item1, topRight.Position, bottomRight.Position, cell.Edges[0].Item2});
                
                break;
            case 10:
                cell.Edges.Add(
                    Tuple.Create(
                        GetPoint(topLeft, bottomLeft),
                        GetPoint(bottomRight, bottomLeft)
                    )
                );
                cell.Edges.Add(
                    Tuple.Create(
                        GetPoint(topLeft, topRight),
                        GetPoint(bottomRight, topRight)
                    )
                );
                AddMeshCell(new []{
                    cell.Edges[1].Item1, topRight.Position, cell.Edges[1].Item2, 
                    cell.Edges[0].Item2, bottomLeft.Position, cell.Edges[0].Item1,
                });
                
                break;
            case 11:                                                                                   
                cell.Edges.Add(                                                                        
                    Tuple.Create(                                                                      
                        GetPoint(topLeft, topRight),                                                   
                        GetPoint(bottomRight, topRight)                                                
                    )                                                                                  
                );                                                                                     
                                                                                           
                AddMeshCell(new []{cell.Edges[0].Item1, topRight.Position, cell.Edges[0].Item2});      
                                                                                           
                break;
            case 12:
                cell.Edges.Add(
                    Tuple.Create(
                        GetPoint(topLeft, bottomLeft),
                        GetPoint(topRight, bottomRight)
                    )    
                );
                AddMeshCell(new []{bottomRight.Position, bottomLeft.Position, cell.Edges[0].Item1, cell.Edges[0].Item2});

                break;
            case 13:
                cell.Edges.Add(
                    Tuple.Create(
                        GetPoint(topRight, bottomRight),
                        GetPoint(bottomLeft, bottomRight)
                    )    
                );

                AddMeshCell(new[] { cell.Edges[0].Item1, bottomRight.Position, cell.Edges[0].Item2 });

                break;
            case 14:
                cell.Edges.Add(
                    Tuple.Create(
                        GetPoint(topLeft, bottomLeft),
                        GetPoint(bottomRight, bottomLeft)
                    )    
                );
                AddMeshCell(new []{cell.Edges[0].Item2, bottomLeft.Position, cell.Edges[0].Item1});

                break;
            case 15:
                break;
            default:
                Debug.LogError("Invalid case");
                break;
        }

        _cells[row, column] = cell;
    }

    private void MarchSquares() {
        for (var row = 0; row < _gridRows - 1; row++) {
            for (var col = 0; col < _gridColumns - 1; col++) {
                // Get the square's binary vertices
                var topLeft = _inputMatrix[row, col].BinaryValue ? 1 : 0;
                var topRight = _inputMatrix[row, col + 1].BinaryValue ? 1 : 0;
                var bottomRight = _inputMatrix[row + 1, col + 1].BinaryValue ? 1 : 0;
                var bottomLeft = _inputMatrix[row + 1, col].BinaryValue ? 1 : 0;

                // Join vertices into array
                int[] vertices = { topLeft, topRight, bottomRight, bottomLeft };

                var cellCase = GetCellCase(vertices);

                SetCellFromCase(cellCase, row, col);
            }
        }
    }

    private Vector2 GetEndpointByMidpoint(Vector2 pointA, Vector2 pointB) {
        var x = pointA.x + (pointB.x - pointA.x) / 2.0f;
        var y = pointA.y + (pointB.y - pointA.y) / 2.0f;

        return new Vector2(x, y);
    }
    
    private Vector2 GetEndpointByInterpolation(Vector2 pointA, Vector2 pointB, float t) {
        var x = (1 - t) * pointA.x + t * pointB.x;
        var y = (1 - t) * pointA.y + t * pointB.y;

        return new Vector2(x, y);
    }

    private float GetT(CellVertex vertexA, CellVertex vertexB, float isoValue) {
        var v2 = Mathf.Max(vertexA.Weight, vertexB.Weight);
        var v1 = Mathf.Min(vertexA.Weight, vertexB.Weight);

        return (isoValue - v1) / (v2 - v1);
    }

    private void BuildMesh() {
        var mesh = new Mesh();

        mesh.vertices = _meshVertices.ToArray();
        mesh.triangles = _meshTriangles.ToArray();

        _meshFilter.mesh = mesh;
    }

    private void OnDrawGizmos() {
        for (var row = 0; row < _gridRows; row++) {
            for (var col = 0; col < _gridColumns; col++) {
                var pos = _inputMatrix[row, col].Position;
                
                Gizmos.color = pos == Vector2.zero ? Color.green : Color.blue;
                if(_drawSquares && row < _gridRows - 1 && col < _gridColumns - 1)
                    Gizmos.DrawWireCube(
                        pos + new Vector2(_gridStepX * 0.5f, _gridStepY * 0.5f),
                        new Vector3(_gridStepX, _gridStepY, 0.1f)
                    );

                var color = Color.white * (_inputMatrix[row, col].Weight - _isoValue);
                color.a = 1;
                Gizmos.color = color;
                
                Gizmos.DrawSphere(pos, 0.1f * _resolution);
            }
        }
        
        Gizmos.color = Color.white;
        for (var row = 0; row < _gridRows -1; row++) {
            for (var col = 0; col < _gridColumns - 1; col++) {
                var cell = _cells[row, col];
                
                foreach (var edge in cell.Edges) {
                    Gizmos.DrawLine(edge.Item1, edge.Item2);
                }
            }
        }
    }
}
