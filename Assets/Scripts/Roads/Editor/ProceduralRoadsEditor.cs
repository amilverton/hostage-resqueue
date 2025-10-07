using UnityEditor;
using UnityEngine;

namespace Roads.Editor
{
    [CustomEditor(typeof(ProceduralRoads))]
    public class ProceduralRoadsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("Generate Roads"))
            {
                var roads = (ProceduralRoads)target;
                roads.GenerateRoads();
            }
        }
    }
}
