using UnityEngine;
#if !UNITY_EDITOR
using System;
using System.IO;
using System.Collections;
using System.Linq;
#endif

namespace Mossmere
{
    public class StandaloneSmokeCheck : MonoBehaviour
    {
#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if(Environment.GetCommandLineArgs().Contains("-sandboxSmoke"))
                new GameObject("Standalone build check").AddComponent<StandaloneSmokeCheck>();
        }
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(2);
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"-smokeOutput");
            string output=index>=0 && index+1<args.Length?args[index+1]:Application.persistentDataPath;
            Directory.CreateDirectory(output);
            bool ok=false;string result;
            var player=FindFirstObjectByType<Explorer>();
            var zones=FindObjectsByType<ReturnZone>(FindObjectsSortMode.None);
            var hud=FindFirstObjectByType<GameHud>();
            if(player==null || zones.Length!=4 || player.GetComponent<SpriteRenderer>().sprite==null || hud==null || hud.HudCanvas==null || hud.music==null || hud.music.clip==null)
            {
                File.WriteAllText(Path.Combine(output,"standalone-result.txt"),"FAIL: missing scene, sprites or return zones");Application.Quit(1);yield break;
            }
            var visual=player.GetComponent<SpriteRenderer>();uint rendering=visual.renderingLayerMask;
            player.gameObject.layer=10;visual.renderingLayerMask=2;
            player.Body.position=zones[0].transform.position;
            player.transform.position=zones[0].transform.position;
            Physics2D.SyncTransforms();
            yield return new WaitForSecondsRealtime(.4f);
            ok=Vector2.Distance(player.Body.position,player.spawn)<.02f && player.gameObject.layer==9 && visual.renderingLayerMask==rendering && player.Body.linearVelocity.sqrMagnitude<.001f;
            var villagers=FindObjectsByType<VillagerDialogue>(FindObjectsSortMode.None);
            var guide=villagers.FirstOrDefault(v=>v.characterName.StartsWith("Mira"));
            if(villagers.Length!=3 || guide==null)
            {
                File.WriteAllText(Path.Combine(output,"standalone-result.txt"),"FAIL: missing villager dialogue");Application.Quit(1);yield break;
            }
            Vector2 approach=(Vector2)guide.transform.position+Vector2.down*1.2f;
            player.Body.position=approach;player.transform.position=approach;Physics2D.SyncTransforms();
            yield return new WaitForSecondsRealtime(.3f);
            ok=ok && hud.DialogueVisible && hud.ActiveVillager==guide && hud.DialogueText.text==guide.lines[0];
            try{SaveCapture(player.follow.GetComponent<Camera>(),Path.Combine(output,"standalone-dialogue.png"));}
            catch(Exception e){File.WriteAllText(Path.Combine(output,"standalone-result.txt"),"FAIL: dialogue render: "+e);Application.Quit(1);yield break;}
            player.ReturnToSpawn();yield return new WaitForSecondsRealtime(.3f);
            ok=ok && !hud.DialogueVisible;
            if(args.Contains("-verifyFullscreen"))
            {
                bool full=Screen.fullScreenMode==FullScreenMode.FullScreenWindow && Screen.width==Display.main.systemWidth && Screen.height==Display.main.systemHeight;
                ok=ok && full;
                File.WriteAllText(Path.Combine(output,"fullscreen-result.txt"),(full?"PASS":"FAIL")+": "+Screen.fullScreenMode+", game "+Screen.width+"x"+Screen.height+", monitor "+Display.main.systemWidth+"x"+Display.main.systemHeight);
            }
            try
            {
                SaveCapture(player.follow.GetComponent<Camera>(),Path.Combine(output,"standalone-spawn.png"));
                result=ok?"PASS: Windows x64 player loads the English HUD, original music, subtle return beacons and three villager greetings; proximity dialogue opens and return-to-spawn clears it while restoring movement and layers.":"FAIL: standalone return or villager dialogue state";
            }
            catch(Exception e){ok=false;result="FAIL: standalone render: "+e;}
            File.WriteAllText(Path.Combine(output,"standalone-result.txt"),result);
            Debug.Log(result);Application.Quit(ok?0:1);
        }
        static void SaveCapture(Camera camera,string path)
        {
            int width=Screen.width,height=Screen.height;
            var target=RenderTexture.GetTemporary(width,height,24);
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest {destination=target});
            var previous=RenderTexture.active;RenderTexture.active=target;
            var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG());
            RenderTexture.active=previous;Destroy(texture);RenderTexture.ReleaseTemporary(target);
        }
#endif
    }
}
