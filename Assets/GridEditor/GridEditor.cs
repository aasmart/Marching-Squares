using System;
using UnityEditor;
using UnityEngine;

namespace GridEditor {
    [CustomEditor(typeof(MarchingSquares))]
    public class GridEditor : Editor
    {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();
            
            if (GUILayout.Button("Render Grid")) {
                var grid = target as MarchingSquares;

                if (!grid) return;

                grid.CreateMarchingSquares();
            }
        }

        private void OnValidate() {
            var grid = target as MarchingSquares;

            if (!grid) return;

            grid.CreateMarchingSquares();
        }
    }
}
