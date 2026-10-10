using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class StoryEditorWindow
{
    [SerializeField] bool showGrid = true;
    Vector2 dragRemainder;
    float GridStep => float.IsNaN(project.gridSize) || float.IsInfinity(project.gridSize) ? 20 : Mathf.Clamp(project.gridSize, 5, 200);
    Vector2 SnapPoint(Vector2 point) => project.snapToGrid ? new Vector2(Mathf.Round(point.x / GridStep) * GridStep, Mathf.Round(point.y / GridStep) * GridStep) : point;
    void GridControls()
    {
        GUILayout.Space(8); GUILayout.Label("Raster", EditorStyles.boldLabel);
        showGrid = EditorGUILayout.Toggle("Raster anzeigen", showGrid);
        bool snap = EditorGUILayout.Toggle(new GUIContent("Am Raster einrasten", "Beim Aktivieren werden die vorhandenen Elemente ausgerichtet. Rückgängig mit Strg+Z."), project.snapToGrid);
        float size = Mathf.Clamp(EditorGUILayout.FloatField("Rasterweite", project.gridSize), 5, 200);
        if (float.IsNaN(size) || float.IsInfinity(size)) size = 20;
        if (snap != project.snapToGrid || size != project.gridSize)
        {
            Undo.RegisterCompleteObjectUndo(project, "Raster einstellen"); project.snapToGrid = snap; project.gridSize = size;
            if (snap) SnapLayout(null); Dirty();
        }
        using (new EditorGUI.DisabledScope(!project.snapToGrid || markedCards.Count == 0))
            if (GUILayout.Button("Auswahl am Raster ausrichten")) { Undo.RegisterCompleteObjectUndo(project, "Auswahl ausrichten"); SnapLayout(markedCards); Dirty(); }
        if (!StoryView && GUILayout.Button("Gesprächsboard anordnen"))
        { Undo.RegisterCompleteObjectUndo(project, "Gesprächsboard anordnen"); StoryBoardLayout.Arrange(project, Sections[chapter]); if (project.snapToGrid) SnapLayout(null); Dirty(); focusBoard = true; }
    }
    void SnapLayout(HashSet<(string id, int answer)> selection)
    {
        if (!project.snapToGrid) return;
        var originalAnswers = new Dictionary<StoryChoice, Vector2>();
        foreach (var node in project.nodes)
            for (int a = 0; a < node.choices.Count; a++) originalAnswers[node.choices[a]] = node.position + (node.choices[a].positioned ? node.choices[a].position : AnswerOffset(a));
        foreach (var node in project.nodes)
        {
            bool parent = selection == null || selection.Contains((node.id, node.triggerOnly ? -2 : -1));
            if (parent) node.position = SnapPoint(node.position);
            if (node.overviewOnly) continue;
            for (int a = 0; a < node.choices.Count; a++)
                if (parent || selection.Contains((node.id, a)))
                { var choice = node.choices[a]; choice.positioned = true; choice.position = SnapPoint(originalAnswers[choice]) - node.position; }
        }
        foreach (var decoration in project.decorations)
            if (selection == null || selection.Contains((decoration.id, -3)))
            {
                decoration.position = SnapPoint(decoration.position);
                if (decoration.kind == StoryDecorationKind.Line) decoration.end = SnapPoint(decoration.end);
                else decoration.size = Vector2.Max(Vector2.one * Mathf.Max(20, project.gridSize), SnapPoint(decoration.size));
            }
    }
}
