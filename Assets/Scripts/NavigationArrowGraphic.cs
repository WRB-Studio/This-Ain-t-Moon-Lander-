using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class NavigationArrowGraphic : Graphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var rect = rectTransform.rect;
        var center = rect.center;
        float size = Mathf.Min(rect.width, rect.height) * .4f;
        mesh.AddVert(center + new Vector2(size, 0), color, Vector2.zero);
        mesh.AddVert(center + new Vector2(-size, size * .7f), color, Vector2.zero);
        mesh.AddVert(center + new Vector2(-size * .45f, 0), color, Vector2.zero);
        mesh.AddVert(center + new Vector2(-size, -size * .7f), color, Vector2.zero);
        mesh.AddTriangle(0, 2, 1); mesh.AddTriangle(0, 3, 2);
    }
}
