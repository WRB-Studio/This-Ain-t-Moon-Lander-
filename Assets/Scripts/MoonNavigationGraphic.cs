using UnityEngine;
using UnityEngine.UI;

// Geometry stays sharp at any HUD scale, including the discovery pulse.
public class MoonNavigationGraphic : Image
{
    [SerializeField, Min(0.5f)] float outlineWidth = 2.5f;
    [SerializeField] bool earth;

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var rect = rectTransform.rect;
        float radius = Mathf.Min(rect.width, rect.height) * .43f;
        var center = rect.center;
        var background = new Color(0, 0, 0, color.a);
        int start = mesh.currentVertCount;
        mesh.AddVert(center, background, Vector2.zero);
        for (int i = 0; i <= 64; i++)
        {
            float angle = i * Mathf.PI * 2 / 64;
            mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, background, Vector2.zero);
            if (i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
        }
        Circle(mesh, center, radius, outlineWidth);
        if (earth)
        {
            Circle(mesh, center, radius, outlineWidth * .65f, new Vector2(.45f, 1));
            Circle(mesh, center, radius, outlineWidth * .65f, new Vector2(1, .35f));
            return;
        }
        Circle(mesh, center + new Vector2(-.32f, .3f) * radius, radius * .22f, outlineWidth * .65f);
        Circle(mesh, center + new Vector2(.35f, -.14f) * radius, radius * .28f, outlineWidth * .65f);
        Circle(mesh, center + new Vector2(-.25f, -.44f) * radius, radius * .12f, outlineWidth * .65f);
    }

    void Circle(VertexHelper mesh, Vector2 center, float radius, float width, Vector2? proportions = null)
    {
        int start = mesh.currentVertCount;
        for (int i = 0; i <= 64; i++)
        {
            float angle = i * Mathf.PI * 2 / 64;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            direction = Vector2.Scale(direction, proportions ?? Vector2.one);
            mesh.AddVert(center + direction * (radius - width * .5f), color, Vector2.zero);
            mesh.AddVert(center + direction * (radius + width * .5f), color, Vector2.zero);
            if (i == 0) continue;
            int previous = start + (i - 1) * 2;
            mesh.AddTriangle(previous, previous + 1, previous + 3);
            mesh.AddTriangle(previous, previous + 3, previous + 2);
        }
    }
}
