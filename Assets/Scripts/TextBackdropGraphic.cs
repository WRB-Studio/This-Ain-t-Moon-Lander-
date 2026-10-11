using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class TextBackdropGraphic : Graphic
{
    [SerializeField] TMP_Text source;
    [SerializeField] Vector2 padding = new(14, 10);
    [SerializeField, Min(0f)] float borderWidth = 1f;
    [SerializeField] Color borderColor = new(1, 1, 1, .18f);
    Rect lastBounds;
    bool lastVisible;

    protected override void OnEnable()
    {
        base.OnEnable();
        Canvas.willRenderCanvases += RefreshBounds;
    }
    protected override void OnDisable()
    {
        Canvas.willRenderCanvases -= RefreshBounds;
        base.OnDisable();
    }
    bool HasText => source && source.isActiveAndEnabled && source.textInfo.characterCount > 0 && !string.IsNullOrWhiteSpace(source.text);
    Rect TextBounds()
    {
        var bounds = source.textBounds;
        Vector2 min = rectTransform.InverseTransformPoint(source.transform.TransformPoint(bounds.min));
        Vector2 max = rectTransform.InverseTransformPoint(source.transform.TransformPoint(bounds.max));
        return Rect.MinMaxRect(min.x - padding.x, min.y - padding.y, max.x + padding.x, max.y + padding.y);
    }
    void RefreshBounds()
    {
        if (CanvasUpdateRegistry.IsRebuildingGraphics()) return;
        bool visible = HasText;
        Rect bounds = visible ? TextBounds() : default;
        if (visible == lastVisible && bounds == lastBounds) return;
        lastVisible = visible; lastBounds = bounds; SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (!HasText) return;
        var rect = TextBounds();
        Quad(mesh, rect, color);
        if (borderWidth <= 0f) return;
        Quad(mesh, new Rect(rect.xMin, rect.yMin, rect.width, borderWidth), borderColor);
        Quad(mesh, new Rect(rect.xMin, rect.yMax - borderWidth, rect.width, borderWidth), borderColor);
        Quad(mesh, new Rect(rect.xMin, rect.yMin, borderWidth, rect.height), borderColor);
        Quad(mesh, new Rect(rect.xMax - borderWidth, rect.yMin, borderWidth, rect.height), borderColor);
    }
    static void Quad(VertexHelper mesh, Rect rect, Color tint)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(new Vector2(rect.xMin, rect.yMin), tint, Vector2.zero);
        mesh.AddVert(new Vector2(rect.xMin, rect.yMax), tint, Vector2.zero);
        mesh.AddVert(new Vector2(rect.xMax, rect.yMax), tint, Vector2.zero);
        mesh.AddVert(new Vector2(rect.xMax, rect.yMin), tint, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
    }
}
