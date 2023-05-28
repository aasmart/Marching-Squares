using System;
using System.Collections.Generic;
using UnityEngine;

public struct ContourCell {
    public List<Tuple<Vector2, Vector2>> Edges;

    public ContourCell(List<Tuple<Vector2, Vector2>> edges) {
        Edges = edges;
    }
}