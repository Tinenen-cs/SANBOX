using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Tilemaps;
using Mossmere;

[InitializeOnLoad]
public static class SandboxWorkshop
{
    const string Pack = "Assets/TopDown 2D RPG BE3/";
    const string Scene = "Assets/Scenes/Mossmere.unity";
    static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    static Dictionary<string, Sprite> sprites;
    static Material material;
    static readonly Vector2[] west = { new(0,0), new(-6,-2), new(-11,-6), new(-17,-8), new(-24,-7), new(-29,-11) };
    static readonly Vector2[] grove = { new(-11,-6), new(-13,0), new(-17,5), new(-19,12), new(-23,15), new(-23,17) };
    static readonly Vector2[] north = { new(0,0), new(3,6), new(2,11), new(7,15), new(10,19), new(12,21) };
    static readonly Vector2[] lake = { new(0,0), new(6,-3), new(11,-6), new(17,-8), new(22,-7), new(22,-1) };
    static readonly Vector2[] loop = { new(-17,-8), new(-10,-15), new(0,-17), new(10,-14), new(17,-8) };
    static readonly Vector2[][] routes = { west, grove, north, lake, loop };
    static readonly Vector2[] clearings = { new(0,0), new(-21,-8), new(-22,13), new(7,16), new(14,-8) };

    static SandboxWorkshop()
    {
        EditorApplication.update += Watch;
        if(Environment.GetCommandLineArgs().Contains("-sandboxPlayTest"))
            EditorApplication.delayCall += () =>
            {
                if(SessionState.GetBool("Mossmere.BatchTestStarted",false))return;
                SessionState.SetBool("Mossmere.BatchTestStarted",true);
                File.WriteAllText(Path.Combine(Root,"workshop.command"),Environment.GetCommandLineArgs().Contains("-sandboxRegenerate")?"generate-test":"test");
            };
    }
    static void Watch()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string command = Path.Combine(Root, "workshop.command");
        if (!File.Exists(command)) return;
        string task = File.ReadAllText(command).Trim();
        File.Delete(command);
        try
        {
            if (task == "generate") Generate();
            if (task == "generate-test") { Generate(); File.WriteAllText(command,"test"); }
            if (task == "test") { EditorSceneManager.OpenScene(Scene); File.WriteAllText(Path.Combine(Root,"playcheck.run"),"run"); EditorApplication.isPlaying = true; }
            if (task == "build") Build();
            File.WriteAllText(Path.Combine(Root, "QA", task + "-editor.txt"), "OK " + DateTime.Now);
        }
        catch (Exception e) { File.WriteAllText(Path.Combine(Root,"QA",task+"-error.txt"), e.ToString()); Debug.LogException(e); }
    }

    [MenuItem("Mossmere/Generate expanded demo")]
    public static void Generate()
    {
        Directory.CreateDirectory(Path.Combine(Root,"QA"));
        sprites = new Dictionary<string,Sprite>();
        foreach (string sheet in new[] { "Tiles", "Objects", "Player", "NPC" })
            foreach (var s in AssetDatabase.LoadAllAssetsAtPath(Pack+"Sprites/"+sheet+".png").OfType<Sprite>()) sprites[sheet+"/"+s.name] = s;
        material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
        string matPath = "Assets/Sandbox/PixelUnlit.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(matPath) == null) AssetDatabase.CreateAsset(material,matPath);
        else { UnityEngine.Object.DestroyImmediate(material); material = AssetDatabase.LoadAssetAtPath<Material>(matPath); }

        // Start from the supplied demo; retain its original house, tree and prop assemblies as templates.
        EditorSceneManager.OpenScene(Pack+"Demo/Demo.unity");
        var demoRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        var originals = new GameObject("Original demo assemblies (reference)");
        foreach (var obj in demoRoots) { obj.transform.SetParent(originals.transform,true); }
        originals.SetActive(false);
        var world = new GameObject("Mossmere • expanded Goldmetal demo");
        var grid = new GameObject("Terrain").AddComponent<Grid>();
        grid.transform.SetParent(world.transform);
        Tilemap waterMap = Map(grid,"Water",-30);
        Tilemap grassMap = Map(grid,"Island",-20);
        Tilemap pathMap = Map(grid,"Winding footpaths",-10);
        var obstacles = new GameObject("Shore and lake collisions");
        obstacles.transform.SetParent(world.transform);
        var tiles = new Dictionary<string,Tile>();
        Tile TileFor(string name)
        {
            if (tiles.TryGetValue(name,out var existing)) return existing;
            string path = "Assets/Sandbox/Terrain_"+name.Replace(' ','_')+".asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null) { tile = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(tile,path); }
            tile.sprite = sprites["Tiles/"+name]; tile.colliderType = Tile.ColliderType.None;
            tiles[name] = tile; EditorUtility.SetDirty(tile); return tile;
        }
        for (int y=-30;y<=30;y++) for(int x=-42;x<=42;x++)
        {
            Vector2 p = new(x+.5f,y+.5f);
            var cell = new Vector3Int(x,y,0);
            waterMap.SetTile(cell,TileFor("Water 4"));
            if (Land(p))
            {
                grassMap.SetTile(cell,TileFor("Gress "+Edge(p,Land)));
                bool pier = Pier(p);
                if (PathAt(p) || pier) pathMap.SetTile(cell,TileFor("Soil "+Edge(p, q => PathAt(q)||Pier(q))));
            }
            else if (Mathf.Abs(x)<38 && Mathf.Abs(y)<28) Block(obstacles.transform,"Water boundary",p,Vector2.one);
        }
        var props = new GameObject("Village, woodland and meadows"); props.transform.SetParent(world.transform);
        House(originals, props.transform,"House A Set",new Vector2(-5,4));
        House(originals, props.transform,"House B Set",new Vector2(3,6));
        House(originals, props.transform,"House A Set",new Vector2(7,0));
        House(originals, props.transform,"House B Set",new Vector2(-24,14));

        UnityEngine.Random.InitState(188718);
        var treePositions = new List<Vector2>();
        for(int i=0;i<1300 && treePositions.Count<180;i++)
        {
            Vector2 p = new(UnityEngine.Random.Range(-33f,33f),UnityEngine.Random.Range(-23f,23f));
            if(!Land(p) || !Land(p+Vector2.up*3) || Lake(p) || DistanceToRoutes(p)<2.7f || clearings.Any(c=>Vector2.Distance(p,c)<5.3f) || treePositions.Any(t=>Vector2.Distance(t,p)<1.5f)) continue;
            if(Mathf.PerlinNoise(p.x*.13f+24,p.y*.13f+19)<.36f) continue;
            Tree(originals,props.transform,p,UnityEngine.Random.Range(.75f,1.05f)); treePositions.Add(p);
        }
        // Uneven groves around the clearing edges; leave the paths open.
        foreach(var p in new[] {new Vector2(-7,7),new Vector2(-8,5),new Vector2(-3,8),new Vector2(8,8),new Vector2(10,4),new Vector2(-27,11),new Vector2(-25,10),new Vector2(4,20),new Vector2(6,22)})
            Tree(originals,props.transform,p,.85f);

        for(int i=0;i<310;i++)
        {
            Vector2 p = new(UnityEngine.Random.Range(-32f,32f),UnityEngine.Random.Range(-22f,22f));
            if(!Land(p)||DistanceToRoutes(p)<1.5f||clearings.Any(c=>Vector2.Distance(c,p)<2))continue;
            string plant = i%4==0?"Plant 0":(i%3==0?"Flower B0":"Flower A0");
            var o=Prop(props.transform,plant,p,false);
            var anim=o.AddComponent<WorldDetails>(); anim.frames=new[] { sprites["Objects/"+plant],sprites["Objects/"+plant.Replace("0","1")] }; anim.rate=1.5f;
        }
        foreach(var p in new[] {new Vector2(-6,1),new Vector2(-4,1),new Vector2(5,3),new Vector2(-26,13),new Vector2(-18,-11),new Vector2(17,-10),new Vector2(8,18)}) Prop(props.transform,"Box",p,true);
        foreach(var p in new[] {new Vector2(-24,-11),new Vector2(-22,-12),new Vector2(-17,10),new Vector2(-15,11),new Vector2(6,18),new Vector2(9,21),new Vector2(14,-5),new Vector2(17,-4)}) Prop(props.transform,"Rock",p,true);
        Prop(props.transform,"Desk",new Vector2(1,3),true);
        Prop(props.transform,"Desk",new Vector2(-21,12),true);
        for(int i=0;i<6;i++) Prop(props.transform,"Flower B0",new Vector2(-23.4f+i*.52f,-9.8f+Mathf.Sin(i)*.3f),false);
        Npc(props.transform,new Vector2(0,2),0);
        Npc(props.transform,new Vector2(-22,12),1);
        Npc(props.transform,new Vector2(14,-9),0);

        var player = new GameObject("Player"); player.layer=9; player.transform.position=Vector3.zero;
        var sr=player.AddComponent<SpriteRenderer>(); sr.sharedMaterial=material; sr.sprite=sprites["Player/Down 0"]; sr.sortingOrder=1005;
        var body=player.AddComponent<Rigidbody2D>(); body.gravityScale=0; body.constraints=RigidbodyConstraints2D.FreezeRotation; body.collisionDetectionMode=CollisionDetectionMode2D.Continuous; body.interpolation=RigidbodyInterpolation2D.Interpolate;
        var col=player.AddComponent<BoxCollider2D>(); col.size=new Vector2(.42f,.30f); col.offset=new Vector2(0,-.28f);
        var explorer=player.AddComponent<Explorer>(); explorer.spawn=Vector2.zero;
        Sprite[] Frames(string facing) => Enumerable.Range(0,4).Select(i=>sprites["Player/"+facing+" "+i]).ToArray();
        explorer.down=Frames("Down"); explorer.up=Frames("Up"); explorer.left=Frames("Left"); explorer.right=Frames("Right");
        var cameraObj=new GameObject("Main Camera"); cameraObj.tag="MainCamera";
        var cam=cameraObj.AddComponent<Camera>(); cam.orthographic=true; cam.orthographicSize=6.75f; cam.backgroundColor=new Color(.10f,.22f,.26f); cam.clearFlags=CameraClearFlags.SolidColor;
        cameraObj.AddComponent<AudioListener>(); var follow=cameraObj.AddComponent<FollowCamera>(); follow.target=player.transform; explorer.follow=follow; follow.Snap();

        var zones=new GameObject("Return stones • walk into the pale flower ring");
        Zone(zones.transform,"West meadow",new Vector2(-29,-11));
        Zone(zones.transform,"Woodland cottage",new Vector2(-23,17));
        Zone(zones.transform,"Northern lookout",new Vector2(12,21));
        Zone(zones.transform,"Lakeside pier",new Vector2(22,-1));
        var soundtrack=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sandbox/Audio/Mossmere-Meadow.wav");
        if(soundtrack==null)throw new Exception("Original music asset is missing");
        var musicObj=new GameObject("Mossmere Meadow - original background music");
        var music=musicObj.AddComponent<AudioSource>();music.clip=soundtrack;music.loop=true;music.playOnAwake=true;music.volume=.23f;music.spatialBlend=0;
        var hud=new GameObject("Game HUD and controls").AddComponent<GameHud>();hud.player=explorer;hud.music=music;
        player.AddComponent<SandboxPlayCheck>();
        ConfigureLayers();
        PlayerSettings.companyName="Tinenen-cs"; PlayerSettings.productName="Mossmere";
        PlayerSettings.bundleVersion="1.2.0";
        PlayerSettings.defaultScreenWidth=1280; PlayerSettings.defaultScreenHeight=720;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed; PlayerSettings.runInBackground=true;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Scene);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Scene,true)};
        AssetDatabase.SaveAssets();
        File.WriteAllText(Path.Combine(Root,"QA/asset-inspection.txt"),"Goldmetal Top-Down 2D RPG Assets Pack; product ID 188718; version 1.0.\nImported path: "+Pack+"\nSource demo: "+Pack+"Demo/Demo.unity\nExpanded scene: "+Scene+"\nOriginal demo retained unchanged; original assemblies retained disabled in expanded scene.\nTerrain uses source sprite tiles. Four connected branches plus southern loop, four return zones. English HUD, original music, subtle return rings without floating labels, and three villagers with proximity greetings and stories.\nTrees placed: "+treePositions.Count);
    }
    static Tilemap Map(Grid grid,string name,int order)
    {
        var obj=new GameObject(name); obj.transform.SetParent(grid.transform); var map=obj.AddComponent<Tilemap>(); var r=obj.AddComponent<TilemapRenderer>(); r.sortingOrder=order;r.sharedMaterial=material; return map;
    }
    static bool Lake(Vector2 p) => Mathf.Pow((p.x-23)/7,2)+Mathf.Pow((p.y-4)/5.5f,2)<1;
    static bool Pier(Vector2 p) => Mathf.Abs(p.x-22)<1.1f && p.y>=-7 && p.y<.1f;
    static bool Land(Vector2 p)
    {
        float edge=Mathf.Pow(p.x/36f,2)+Mathf.Pow(p.y/26f,2);
        return edge<.91f+.055f*Mathf.Sin(p.x*.46f)+.04f*Mathf.Cos(p.y*.62f) && (!Lake(p)||Pier(p));
    }
    static float Segment(Vector2 p,Vector2 a,Vector2 b) {var d=b-a;return Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude));}
    static float DistanceToRoutes(Vector2 p) => routes.Min(route=>Enumerable.Range(0,route.Length-1).Min(i=>Segment(p,route[i],route[i+1])));
    static bool PathAt(Vector2 p) => Land(p) && (DistanceToRoutes(p)<1.15f+.23f*Mathf.Sin(p.x*.8f+p.y*.6f) || clearings.Any(c=>Mathf.Pow((p.x-c.x)/3.6f,2)+Mathf.Pow((p.y-c.y)/2.5f,2)<1));
    static int Edge(Vector2 p,Func<Vector2,bool> inside)
    {
        int x=!inside(p+Vector2.left)?0:(!inside(p+Vector2.right)?2:1);
        int y=!inside(p+Vector2.up)?0:(!inside(p+Vector2.down)?2:1);
        return y*3+x;
    }
    static void Block(Transform parent,string name,Vector2 pos,Vector2 size)
    {
        var o=new GameObject(name);o.transform.SetParent(parent);o.transform.position=pos;o.layer=8;o.AddComponent<BoxCollider2D>().size=size;
    }
    static GameObject DemoCopy(GameObject originals,string name,Transform parent,Vector2 pos)
    {
        var source=originals.GetComponentsInChildren<Transform>(true).First(t=>t.name==name).gameObject;
        var o=UnityEngine.Object.Instantiate(source,parent);o.name=name;o.SetActive(true);o.transform.position=pos;
        foreach(var r in o.GetComponentsInChildren<SpriteRenderer>()) {r.sharedMaterial=material;r.sortingOrder=1000-Mathf.RoundToInt(pos.y*16);}
        return o;
    }
    static void House(GameObject originals,Transform parent,string type,Vector2 pos)
    {
        var o=DemoCopy(originals,type,parent,pos);o.layer=8;
        var c=o.AddComponent<BoxCollider2D>();c.offset=new Vector2(.5f,-1.3f);c.size=new Vector2(1.9f,1.8f);
        foreach(var r in o.GetComponentsInChildren<SpriteRenderer>())r.sortingOrder=1000-Mathf.RoundToInt((pos.y-2.3f)*16);
    }
    static void Tree(GameObject originals,Transform parent,Vector2 pos,float scale)
    {
        var o=DemoCopy(originals,"Tree Set",parent,pos+Vector2.up*(3*scale));o.transform.localScale=Vector3.one*scale;o.layer=8;
        var c=o.AddComponent<BoxCollider2D>();c.offset=new Vector2(0,-2.9f);c.size=new Vector2(.5f,.38f);
        foreach(var r in o.GetComponentsInChildren<SpriteRenderer>()) r.sortingOrder=1000-Mathf.RoundToInt(pos.y*16);
    }
    static GameObject Prop(Transform parent,string name,Vector2 p,bool collision)
    {
        var o=new GameObject(name);o.transform.SetParent(parent);o.transform.position=p;
        var r=o.AddComponent<SpriteRenderer>();r.sprite=sprites["Objects/"+name];r.sharedMaterial=material;r.sortingOrder=1000-Mathf.RoundToInt((p.y-.3f)*16);
        if(collision){o.layer=8;var c=o.AddComponent<BoxCollider2D>();c.size=new Vector2(.7f,.5f);c.offset=new Vector2(0,-.15f);}
        return o;
    }
    static void Npc(Transform parent,Vector2 p,int index)
    {
        var all=sprites.Where(kv=>kv.Key.StartsWith("NPC/")).Select(kv=>kv.Value).ToArray();
        var o=new GameObject("Villager");o.transform.SetParent(parent);o.transform.position=p;var r=o.AddComponent<SpriteRenderer>();r.sharedMaterial=material;r.sprite=all[index%all.Length];r.sortingOrder=1000-Mathf.RoundToInt((p.y-.3f)*16);
        var dialogue=o.AddComponent<VillagerDialogue>();
        if(p.x< -10)
        {
            dialogue.characterName="Fern - Woodland Keeper";
            dialogue.lines=new[]{"Good day! I'm Fern. I look after this little woodland cottage.","The forest grows close around these paths, but every trail has a quiet place worth finding.","If you lose your way, look for a glowing ring. It will bring you safely back to the village."};
        }
        else if(p.x>10)
        {
            dialogue.characterName="Rowan - Lake Watcher";
            dialogue.lines=new[]{"Hello there! I'm Rowan. The lake is wonderfully peaceful today.","I come here to listen to the water and watch travelers discover the island.","That light by the pier is a return beacon. It leads home, so you can keep exploring without worry."};
        }
        else
        {
            dialogue.characterName="Mira - Village Guide";
            dialogue.lines=new[]{"Hello, traveler! Welcome to Mossmere. Take your time and make yourself at home.","These paths connect our village to the meadow, woodland cottage and lakeside pier.","The glowing rings were left by the island's pathkeepers. Step into one whenever you want to come back home."};
        }
        o.name=dialogue.characterName;
    }
    static void Zone(Transform parent,string name,Vector2 p)
    {
        var o=new GameObject(name);o.transform.SetParent(parent);o.transform.position=p;
        o.AddComponent<BoxCollider2D>().isTrigger=true;o.GetComponent<BoxCollider2D>().size=Vector2.one*.8f;o.AddComponent<ReturnZone>();
        // In-world markers made entirely from the pack's rocks and white flowers.
        foreach(var offset in new[]{new Vector2(-.8f,0),new Vector2(.8f,0),new Vector2(0,.8f),new Vector2(0,-.8f)})Prop(o.transform,"Flower A0",p+offset,false);
        var stone=Prop(o.transform,"Rock",p,false);stone.GetComponent<SpriteRenderer>().color=new Color(.7f,1,1,1);
        string path="Assets/Sandbox/ReturnBeaconGlow.mat";
        var glow=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(glow==null){glow=new Material(Shader.Find("Mossmere/ReturnBeaconGlow"));AssetDatabase.CreateAsset(glow,path);}
        var beacon=o.AddComponent<ReturnBeacon>();beacon.glowMaterial=glow;beacon.BuildVisuals();
    }
    static void ConfigureLayers()
    {
        var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers=tags.FindProperty("layers");layers.GetArrayElementAtIndex(8).stringValue="World";layers.GetArrayElementAtIndex(9).stringValue="Player";layers.GetArrayElementAtIndex(10).stringValue="ReturnTest";tags.ApplyModifiedProperties();
    }
    [MenuItem("Mossmere/Build Windows x64")]
    public static void Build()
    {
        PlayerSettings.bundleVersion="1.2.0";
        string output=Path.GetFullPath(Path.Combine(Root,"../Deliverables/Mossmere-Windows-x64-v3/Mossmere.exe"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{Scene},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        File.WriteAllText(Path.Combine(Root,"QA/build-result.txt"),report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nBytes: "+report.summary.totalSize+"\nTime: "+report.summary.totalTime);
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Windows build failed");
    }
}
