using UnityEngine;
#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#endif

namespace Mossmere
{
    // The checks run only in the Editor when explicitly requested by the workshop.
    // This component has no behaviour in the shipped player.
    public class SandboxPlayCheck : MonoBehaviour
    {
#if UNITY_EDITOR
        Explorer player;
        string qa;
        readonly List<string> checks = new();
        IEnumerator Start()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            string marker=Path.Combine(root,"playcheck.run");
            if(!File.Exists(marker)) yield break;
            File.Delete(marker);
            qa=Path.Combine(root,"QA");
            player=GetComponent<Explorer>();player.testControl=true;
            var run=Run();
            while(true)
            {
                object current=null;bool next=false;
                try{next=run.MoveNext();if(next)current=run.Current;}
                catch(Exception e)
                {
                    File.WriteAllText(Path.Combine(qa,"playcheck-result.txt"),"FAIL\n"+string.Join("\n",checks)+"\n"+e);
                    Debug.LogException(e);
                    UnityEditor.EditorApplication.isPlaying=false;
                    if(Environment.GetCommandLineArgs().Contains("-sandboxPlayTest"))UnityEditor.EditorApplication.Exit(1);
                    yield break;
                }
                if(!next)break;
                yield return current;
            }
        }
        IEnumerator Run()
        {
            Time.timeScale=2;
            yield return new WaitForSecondsRealtime(.5f);
            var visual=GetComponent<SpriteRenderer>();
            uint originalRendering=visual.renderingLayerMask;
            int originalSorting=visual.sortingLayerID;
            Check(player.gameObject.layer==9,"Player starts on Player collision layer");
            Check(visual.sprite!=null,"Imported player sprites assigned");
            var hud=FindFirstObjectByType<GameHud>();
            Check(hud!=null && hud.HudCanvas!=null && hud.AreaText.text=="Village","English HUD and location visible");
            Check(hud.music!=null && hud.music.clip!=null && hud.music.clip.length>=30 && hud.music.loop && hud.music.isPlaying,"Looping background music is playing");
            var audioData=new float[8192];hud.music.clip.GetData(audioData,4410);
            Check(audioData.Any(value=>Mathf.Abs(value)>.001f),"Music contains audible samples");
            bool wasMuted=hud.music.mute;hud.MusicButton.onClick.Invoke();Check(hud.music.mute!=wasMuted,"English music button toggles audio");hud.MusicButton.onClick.Invoke();
            Check(FindObjectsByType<ReturnBeacon>(FindObjectsSortMode.None).All(b=>b.transform.Find("Glowing return ring")!=null && b.transform.Find("Beacon light column")!=null && b.transform.Find("RETURN TO SPAWN label")==null),"Return zones retain glow rings and subtle beams without floating text");
            yield return Capture("01-village");

            var villagers=FindObjectsByType<VillagerDialogue>(FindObjectsSortMode.None).OrderBy(v=>v.characterName).ToArray();
            Check(villagers.Length==3,"Three villagers have individual English greetings and stories");
            foreach(var villager in villagers)
            {
                player.ReturnToSpawn();yield return new WaitForSecondsRealtime(.3f);
                Vector2 approach=(Vector2)villager.transform.position+Vector2.down*1.2f;
                var walk=WalkTo(approach,"Approach "+villager.characterName);
                while(walk.MoveNext())yield return walk.Current;
                yield return new WaitForSecondsRealtime(.2f);
                Check(hud.DialogueVisible && hud.ActiveVillager==villager && hud.DialogueText.text==villager.lines[0],villager.characterName+": greeting appears automatically nearby");
                yield return Capture("dialogue-"+villager.characterName.Split(' ')[0]);
                hud.AdvanceDialogue();
                Check(hud.DialogueText.text==villager.lines[1],villager.characterName+": next dialogue advances the story");
                walk=WalkTo(approach+Vector2.down*2.4f,"Leave "+villager.characterName);
                while(walk.MoveNext())yield return walk.Current;
                yield return new WaitForSecondsRealtime(.2f);
                Check(!hud.DialogueVisible,villager.characterName+": dialogue closes after walking away");
            }

            var zones=FindObjectsByType<ReturnZone>(FindObjectsSortMode.None).OrderBy(z=>z.name).ToArray();
            Check(zones.Length==4,"Four return zones present");
            foreach(var zone in zones)
            {
                player.ReturnToSpawn();
                yield return new WaitForSecondsRealtime(.3f);
                Vector2 destination=zone.transform.position;
                var path=FindPath(player.Body.position,destination);
                Check(path!=null,"Reachable path to "+zone.name);
                int before=player.returns;
                float timeout=Time.time+100;
                bool corrupted=false;
                int waypoint=0;
                while(player.returns==before)
                {
                    if(Time.time>timeout)throw new Exception("Movement timed out at "+player.Body.position+" going to "+zone.name+" waypoint "+waypoint+"/"+path.Count);
                    if(waypoint<path.Count-1 && Vector2.Distance(player.Body.position,path[waypoint])<.17f)waypoint++;
                    Vector2 delta=path[waypoint]-player.Body.position;
                    player.testInput=delta.normalized*Mathf.Min(1,delta.magnitude/.22f);
                    float distance=Vector2.Distance(player.Body.position,destination);
                    if(distance<2.4f && !corrupted)
                    {
                        player.testInput=Vector2.zero;
                        yield return Capture("region-"+zone.name.Replace(' ','-'));
                        player.gameObject.layer=10;
                        visual.renderingLayerMask=2;
                        player.follow.GetComponent<Camera>().cullingMask=~(1<<9);
                        corrupted=true;
                    }
                    yield return null;
                }
                Check(corrupted,"Reached expanded area "+zone.name);
                Check(Vector2.Distance(player.Body.position,player.spawn)<.02f,zone.name+": trigger returns to spawn");
                Check(player.Body.linearVelocity.sqrMagnitude<.001f && Mathf.Abs(player.Body.angularVelocity)<.001f,zone.name+": movement cleared");
                Check(player.Movement==Vector2.zero && player.testInput==Vector2.zero,zone.name+": input cleared");
                Check(player.gameObject.layer==9,zone.name+": collision layer restored");
                Check(visual.renderingLayerMask==originalRendering && visual.sortingLayerID==originalSorting && visual.enabled,zone.name+": rendering layers restored");
                Check(player.GetComponent<BoxCollider2D>().enabled && !player.GetComponent<BoxCollider2D>().isTrigger,zone.name+": solid player collider restored");
                Check(Vector2.Distance(player.follow.transform.position,player.spawn)<.02f && player.follow.GetComponent<Camera>().cullingMask==~0,zone.name+": camera recentered and mask restored");
            }
            // Traverse the longer southern loop through actual physics movement.
            player.ReturnToSpawn();yield return new WaitForSecondsRealtime(.3f);
            foreach(var point in new[]{new Vector2(-17,-8),new Vector2(-10,-15),new Vector2(0,-17),new Vector2(10,-14),new Vector2(17,-8)})
            {
                var path=FindPath(player.Body.position,point);Check(path!=null,"Southern loop route "+point);
                int waypoint=0;float deadline=Time.time+80;
                while(Vector2.Distance(player.Body.position,point)>.2f)
                {
                    if(Time.time>deadline)throw new Exception("Southern loop movement blocked at "+player.Body.position);
                    if(waypoint<path.Count-1 && Vector2.Distance(player.Body.position,path[waypoint])<.17f)waypoint++;
                    player.testInput=(path[waypoint]-player.Body.position).normalized;
                    yield return null;
                }
                player.testInput=Vector2.zero;
            }
            Check(true,"Southern loop traversed in Play mode");
            player.ReturnToSpawn();yield return new WaitForSecondsRealtime(.3f);
            player.testInput=Vector2.left;yield return new WaitForSeconds(.5f);player.testInput=Vector2.zero;
            player.Body.angularVelocity=13;player.gameObject.layer=10;visual.renderingLayerMask=2;
            player.GetComponent<BoxCollider2D>().isTrigger=true;
            player.follow.transform.position+=Vector3.one*5;
            player.ReturnToSpawn();
            Check(player.Body.linearVelocity==Vector2.zero && player.Body.angularVelocity==0,"Manual return clears residual motion");
            Check(player.gameObject.layer==9 && !player.GetComponent<BoxCollider2D>().isTrigger && visual.renderingLayerMask==originalRendering,"Manual return restores collision and rendering");
            player.Body.position=Vector2.right*2;player.transform.position=player.Body.position;
            hud.HomeButton.onClick.Invoke();Check(Vector2.Distance(player.Body.position,player.spawn)<.02f,"English HUD return button returns to spawn");
            yield return Capture("02-final-spawn");
            Time.timeScale=1;
            File.WriteAllText(Path.Combine(qa,"playcheck-result.txt"),"PASS\n"+string.Join("\n",checks)+"\nCompleted: "+DateTime.Now.ToString("O"));
            UnityEditor.EditorApplication.isPlaying=false;
            if(Environment.GetCommandLineArgs().Contains("-sandboxPlayTest"))UnityEditor.EditorApplication.Exit(0);
        }
        void Check(bool condition,string label)
        {
            if(!condition)throw new Exception(label);
            checks.Add("PASS "+label);File.WriteAllText(Path.Combine(qa,"playcheck-progress.txt"),string.Join("\n",checks));
        }
        IEnumerator WalkTo(Vector2 point,string label)
        {
            var path=FindPath(player.Body.position,point);Check(path!=null,label+": reachable walking path");
            int waypoint=0;float deadline=Time.time+100;
            while(Vector2.Distance(player.Body.position,point)>.15f)
            {
                if(Time.time>deadline)throw new Exception(label+": walking timed out at "+player.Body.position);
                if(waypoint<path.Count-1 && Vector2.Distance(player.Body.position,path[waypoint])<.17f)waypoint++;
                Vector2 delta=path[waypoint]-player.Body.position;
                player.testInput=delta.normalized*Mathf.Min(1,delta.magnitude/.22f);
                yield return null;
            }
            player.testInput=Vector2.zero;
        }
        IEnumerator Capture(string name)
        {
            player.testInput=Vector2.zero;
            yield return new WaitForSecondsRealtime(.2f);
            var camera=player.follow.GetComponent<Camera>();
            var target=RenderTexture.GetTemporary(1280,720,24);
            var request=new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination=target };
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,request);
            var previous=RenderTexture.active;
            RenderTexture.active=target;
            var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
            File.WriteAllBytes(Path.Combine(qa,name+".png"),texture.EncodeToPNG());
            RenderTexture.active=previous;
            Destroy(texture);RenderTexture.ReleaseTemporary(target);
            yield return new WaitForSecondsRealtime(.15f);
        }
        List<Vector2> FindPath(Vector2 start,Vector2 goal)
        {
            Physics2D.SyncTransforms();
            Vector2Int Cell(Vector2 p)=>new(Mathf.RoundToInt(p.x*2),Mathf.RoundToInt(p.y*2));
            Vector2 Point(Vector2Int c)=>new(c.x*.5f,c.y*.5f);
            var first=Cell(start);var last=Cell(goal);
            var queue=new Queue<Vector2Int>();queue.Enqueue(first);
            var previous=new Dictionary<Vector2Int,Vector2Int>{{first,first}};
            var offsets=new[]{Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
            var otherZones=FindObjectsByType<ReturnZone>(FindObjectsSortMode.None).Where(z=>Vector2.Distance(z.transform.position,goal)>1).Select(z=>(Vector2)z.transform.position).ToArray();
            while(queue.Count>0)
            {
                var cell=queue.Dequeue();
                if(cell==last)
                {
                    var path=new List<Vector2>{goal};
                    while(cell!=first){path.Add(Point(cell));cell=previous[cell];}
                    path.Reverse();return path;
                }
                foreach(var offset in offsets)
                {
                    var next=cell+offset;
                    if(previous.ContainsKey(next)||Mathf.Abs(next.x)>68||Mathf.Abs(next.y)>48)continue;
                    var point=Point(next);
                    // Reserve clearance for the feet collider and turns between grid cells.
                    if(Physics2D.OverlapBox(point+new Vector2(0,-.28f),new Vector2(.9f,.8f),0,1<<8)!=null)continue;
                    if(otherZones.Any(z=>Vector2.Distance(z,point)<1.1f))continue;
                    previous[next]=cell;queue.Enqueue(next);
                }
            }
            return null;
        }
#endif
    }
}
