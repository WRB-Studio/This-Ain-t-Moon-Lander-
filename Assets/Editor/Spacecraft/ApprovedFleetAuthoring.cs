using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public static class ApprovedFleetAuthoring
{
    const string Images="Assets/Images/Spacecraft/ApprovedFleet";
    const string Prefabs="Assets/Prefabs/Spacecraft/AssistedFlight";
    static readonly string[] Names={"Courier","Scout","CargoTug","Freighter","Rescue","Maintenance","Interceptor"};
    static readonly float[] Heights={7.5f,9f,8.5f,14f,10f,8.5f,6.5f};
    public static void Build()
    {
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureInterceptorPrefab();
        var reports=new List<string>();
        for(int i=0;i<Names.Length;i++) reports.Add(ConfigureShip(i));
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/SpacecraftFlightSandbox.unity");
        AddInterceptorToScene();
        foreach(var ship in Object.FindObjectsByType<AssistedSpacecraft>(FindObjectsSortMode.None))
            if(!ship.IsClassic && ship.homePad)
            {
                ship.transform.position=ship.homePad.dockPoint.position-(Vector3)ship.homePad.Up*ship.footOffset+(Vector3)ship.homePad.Up*.03f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(ship.transform);
            }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Preview();
        Directory.CreateDirectory("Documentation/AssistedSpacecraft/ApprovedFleet");
        File.WriteAllLines("Documentation/AssistedSpacecraft/ApprovedFleet/Measurements.txt",reports);
        Debug.Log("Role-distinct upright fleet authored: seven new ships including restored Interceptor; existing flight values preserved. No Play Mode tests.");
    }
    static string ConfigureShip(int index)
    {
        string name=Names[index], imagePath=Images+"/"+name+".png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(imagePath);
        importer.textureType=TextureImporterType.Default; importer.isReadable=true; importer.mipmapEnabled=false;
        importer.textureCompression=TextureImporterCompression.Uncompressed; importer.maxTextureSize=2048;
        importer.filterMode=FilterMode.Bilinear; importer.wrapMode=TextureWrapMode.Clamp; importer.SaveAndReimport();
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath); var pixels=texture.GetPixels32();
        int left=texture.width,right=0,bottom=texture.height,top=0;
        bool White(int x,int y) { Color32 p=pixels[y*texture.width+x]; return Mathf.Max(p.r,Mathf.Max(p.g,p.b))>=90; }
        for(int y=0;y<texture.height;y++) for(int x=0;x<texture.width;x++) if(White(x,y))
        { left=Mathf.Min(left,x);right=Mathf.Max(right,x);bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y); }
        float height=Heights[index], ppu=(top-bottom+1)/height;
        var sprite=Sprite.Create(texture,new Rect(left,bottom,right-left+1,top-bottom+1),Vector2.one*.5f,ppu,0,SpriteMeshType.FullRect);
        sprite.name=name;
        string spritePath="Assets/Images/Spacecraft/AssistedFlight/Sprites/"+name+".asset";
        var existing=AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if(existing) { EditorUtility.CopySerialized(sprite,existing); Object.DestroyImmediate(sprite); sprite=existing; }
        else AssetDatabase.CreateAsset(sprite,spritePath);
        float width=sprite.bounds.size.x;
        var root=PrefabUtility.LoadPrefabContents(Prefabs+"/"+name+".prefab");
        try
        {
            var ship=root.GetComponent<AssistedSpacecraft>(); ship.forwardAngle=90f; ship.footOffset=-height*.5f;
            var art=root.transform.Find("Outline art").GetComponent<SpriteRenderer>(); art.sprite=sprite;
            var samples=new List<Vector2>();
            int step=Mathf.Max(1,(top-bottom)/48);
            // Build a per-ship outline from its visible extremes. Feet get their own colliders below.
            for(int y=bottom+(int)((top-bottom)*.06f);y<=top;y+=step)
            {
                int lo=right,hi=left;
                for(int x=left;x<=right;x++) if(White(x,y)) {lo=Mathf.Min(lo,x);hi=Mathf.Max(hi,x);}
                if(lo<=hi)
                {
                    samples.Add(new Vector2((lo-(left+right)*.5f)/ppu,(y-(top+bottom)*.5f)/ppu));
                    samples.Add(new Vector2((hi-(left+right)*.5f)/ppu,(y-(top+bottom)*.5f)/ppu));
                }
            }
            for(int x=left;x<=right;x++) if(White(x,top)) samples.Add(new Vector2((x-(left+right)*.5f)/ppu,height*.5f));
            var points=ConvexHull(samples);
            root.GetComponent<PolygonCollider2D>().points=points;
            string meshPath="Assets/Images/Spacecraft/AssistedFlight/Sprites/"+name+"Hull.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(!mesh) {mesh=new Mesh {name=name+" hull"};AssetDatabase.CreateAsset(mesh,meshPath);}
            root.transform.Find("Black hull").GetComponent<MeshFilter>().sharedMesh=mesh;
            mesh.Clear(); mesh.vertices=points.Select(point=>(Vector3)point).ToArray();
            mesh.triangles=Enumerable.Range(1,points.Length-2).SelectMany(i=>new[] {0,i+1,i}).ToArray(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
            // Detect the two low footplate spans in the generated art instead of assuming their positions.
            var spans=new List<Vector2Int>(); int start=-1;
            for(int x=left;x<=right+1;x++)
            {
                bool foot=false;
                if(x<=right) for(int y=bottom;y<=Mathf.Min(top,bottom+8);y++) if(White(x,y)) {foot=true;break;}
                if(foot && start<0) start=x;
                if(!foot && start>=0) {if(x-start>3) spans.Add(new Vector2Int(start,x-1));start=-1;}
            }
            for(int i=0;i<2;i++)
            {
                var foot=root.transform.Find(i==0?"Rear foot collider":"Front foot collider");
                float x=(i==0?-1f:1f)*width*.33f, footWidth=.5f;
                if(spans.Count>=2)
                {
                    var span=spans[i==0?0:spans.Count-1];
                    x=((span.x+span.y)*.5f-(left+right)*.5f)/ppu;
                    footWidth=(span.y-span.x+1)/ppu;
                }
                foot.localPosition=new Vector3(x,ship.footOffset+.075f);
                foot.GetComponent<BoxCollider2D>().size=new Vector2(Mathf.Max(.3f,footWidth*.9f),.15f);
            }
            var positions=new[] {new Vector2(-width*.48f,0f),new Vector2(width*.48f,0f),
                new Vector2(0f,-height*.5f+.18f),new Vector2(0f,height*.5f+.15f)};
            for(int i=0;i<ship.thrustIndicators.Length;i++)
            {
                ship.thrustIndicators[i].transform.localPosition=positions[i];
                ship.thrustIndicators[i].size=i<2?new Vector2(.45f,.1f):new Vector2(.1f,.35f);
            }
            root.transform.Find("ExitHatch").localPosition=new Vector3(0f,-height*.05f);
            PrefabUtility.SaveAsPrefabAsset(root,Prefabs+"/"+name+".prefab");
            return $"{name}: height={height:0.00}, width={width:0.00}, footplate spans detected={spans.Count}, forwardAngle=90. Flight tuning preserved.";
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
    }
    static void Preview()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/DejaVuSansMono SDF.asset");
        for(int i=0;i<Names.Length;i++)
        {
            float x=(i-3f)*24f;
            var pad=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"/DockingBay.prefab"));pad.transform.position=new Vector3(x,0f);
            var ship=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"/"+Names[i]+".prefab"));
            ship.transform.position=new Vector3(x,Heights[i]*.5f);
            foreach(var behaviour in ship.GetComponentsInChildren<MonoBehaviour>(true)) {behaviour.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);}
            ship.GetComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Static;
            PrefabUtility.RecordPrefabInstancePropertyModifications(ship.GetComponent<Rigidbody2D>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(ship.transform); PrefabUtility.RecordPrefabInstancePropertyModifications(pad.transform);
            var astronaut=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Astronaut.prefab"));
            foreach(var behaviour in astronaut.GetComponentsInChildren<MonoBehaviour>(true)) {behaviour.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);}
            astronaut.GetComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Static; Physics2D.SyncTransforms();
            float floorOffset=astronaut.GetComponents<Collider2D>().First(collider=>!collider.isTrigger).bounds.min.y-astronaut.transform.position.y;
            astronaut.transform.position=new Vector3(x+9f,-floorOffset);
            PrefabUtility.RecordPrefabInstancePropertyModifications(astronaut.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(astronaut.GetComponent<Rigidbody2D>());
            var text=new GameObject(Names[i]+" label").AddComponent<TextMeshPro>();text.font=font;
            text.text=$"{Names[i]}\n{Heights[i]:0.0} Einheiten";text.fontSize=7f;text.alignment=TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta=new Vector2(22f,4f);text.transform.position=new Vector3(x,-5.5f);text.GetComponent<MeshRenderer>().sortingOrder=10;
        }
        var camera=new GameObject("Fleet Scale Camera").AddComponent<Camera>();camera.tag="MainCamera";
        camera.orthographic=true;camera.orthographicSize=49f;camera.transform.position=new Vector3(0f,5f,-10f);
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/ApprovedFleetScalePreview.unity");
        var target=new RenderTexture(2400,1000,24);camera.targetTexture=target;
        RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest {destination=target});
        RenderTexture.active=target;var image=new Texture2D(2400,1000,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,2400,1000),0,0);image.Apply();
        Directory.CreateDirectory("Documentation/AssistedSpacecraft/ApprovedFleet");
        File.WriteAllBytes("Documentation/AssistedSpacecraft/ApprovedFleet/ScaleOverview.png",image.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(image);target.Release();Object.DestroyImmediate(target);
    }
    static Vector2[] ConvexHull(List<Vector2> points)
    {
        var sorted=points.Distinct().OrderBy(p=>p.x).ThenBy(p=>p.y).ToArray();
        float Cross(Vector2 a,Vector2 b,Vector2 c) => (b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
        var lower=new List<Vector2>(); var upper=new List<Vector2>();
        foreach(var p in sorted) {while(lower.Count>=2 && Cross(lower[^2],lower[^1],p)<=0f) lower.RemoveAt(lower.Count-1);lower.Add(p);}
        foreach(var p in sorted.Reverse()) {while(upper.Count>=2 && Cross(upper[^2],upper[^1],p)<=0f) upper.RemoveAt(upper.Count-1);upper.Add(p);}
        lower.RemoveAt(lower.Count-1);upper.RemoveAt(upper.Count-1);lower.AddRange(upper);return lower.ToArray();
    }
    static void EnsureInterceptorPrefab()
    {
        string path=Prefabs+"/Interceptor.prefab";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(path)) return;
        var root=PrefabUtility.LoadPrefabContents(Prefabs+"/Scout.prefab");
        try
        {
            root.name="Interceptor";var ship=root.GetComponent<AssistedSpacecraft>();ship.displayName="Interceptor";
            ship.maxSpeed=20f;ship.acceleration=13f;ship.braking=18f;ship.maneuverAcceleration=13f;ship.turnSpeed=240f;
            ship.fuelCapacity=110f;ship.fuelBurnPerSecond=.75f;root.GetComponent<Rigidbody2D>().mass=.7f;
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
    }
    static void AddInterceptorToScene()
    {
        var sandbox=Object.FindFirstObjectByType<SpacecraftSandbox>();
        var interceptor=Object.FindObjectsByType<AssistedSpacecraft>(FindObjectsSortMode.None).FirstOrDefault(ship=>ship.displayName=="Interceptor" && !ship.IsClassic);
        if(!interceptor)
        {
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"/Interceptor.prefab"));
            root.name="07 Interceptor";interceptor=root.GetComponent<AssistedSpacecraft>();
            var station=GameObject.Find("FlightTestStation");interceptor.homePad=station.transform.Find("DockingBays/Pad 07").GetComponent<ShipDockingPad>();
            PrefabUtility.RecordPrefabInstancePropertyModifications(interceptor);PrefabUtility.RecordPrefabInstancePropertyModifications(root);
        }
        for(int i=1;i<=7;i++)
        {
            var classic=Object.FindObjectsByType<AssistedSpacecraft>(FindObjectsSortMode.None).First(ship=>ship.displayName==$"Lander {i:00}");
            classic.name=$"{i+7:00} Classic Lander{i:00}";PrefabUtility.RecordPrefabInstancePropertyModifications(classic.gameObject);
            var button=sandbox.hudButtons.Find($"Lander {i:00}").GetComponent<Button>();
            while(button.onClick.GetPersistentEventCount()>0) UnityEventTools.RemovePersistentListener(button.onClick,0);
            UnityEventTools.AddIntPersistentListener(button.onClick,sandbox.SelectShip,i+6);
            PrefabUtility.RecordPrefabInstancePropertyModifications(button);
        }
        if(!sandbox.hudButtons.Find("Interceptor"))
        {
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"/SandboxButton.prefab"));
            root.name="Interceptor";root.transform.SetParent(sandbox.hudButtons,false);root.transform.SetSiblingIndex(6);
            var label=root.GetComponentInChildren<TMP_Text>();label.text="Interceptor";PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            UnityEventTools.AddIntPersistentListener(root.GetComponent<Button>().onClick,sandbox.SelectShip,6);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root.GetComponent<Button>());
        }
    }
}
