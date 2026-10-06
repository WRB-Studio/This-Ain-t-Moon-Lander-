using UnityEngine;
using UnityEngine.UI;

public class RadioSignalGraphic : Graphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        AddStroke(mesh, new Vector2(-9f, -1f), new Vector2(-9f, 1f), 2f);
        foreach (float radius in new[] { 6f, 12f, 18f })
            for (int segment = 0; segment < 10; segment++)
            {
                float a = Mathf.Lerp(-0.85f, 0.85f, segment / 10f);
                float b = Mathf.Lerp(-0.85f, 0.85f, (segment + 1) / 10f);
                AddStroke(mesh, new Vector2(-9f + Mathf.Cos(a) * radius, Mathf.Sin(a) * radius),
                    new Vector2(-9f + Mathf.Cos(b) * radius, Mathf.Sin(b) * radius), 1.6f);
            }
    }
    void AddStroke(VertexHelper mesh, Vector2 from, Vector2 to, float thickness)
    {
        Vector2 normal = new Vector2(-(to - from).y, (to - from).x).normalized * thickness * 0.5f;
        int start = mesh.currentVertCount;
        mesh.AddVert(from - normal, color, Vector2.zero);
        mesh.AddVert(from + normal, color, Vector2.zero);
        mesh.AddVert(to + normal, color, Vector2.zero);
        mesh.AddVert(to - normal, color, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }
}
