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
            if(player==null || zones.Length!=4 || player.GetComponent<SpriteRenderer>().sprite==null)
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
            try
            {
                var target=RenderTexture.GetTemporary(1280,720,24);
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(player.follow.GetComponent<Camera>(),new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest {destination=target});
                var previous=RenderTexture.active;RenderTexture.active=target;
                var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
                File.WriteAllBytes(Path.Combine(output,"standalone-spawn.png"),texture.EncodeToPNG());
                RenderTexture.active=previous;Destroy(texture);RenderTexture.ReleaseTemporary(target);
                result=ok?"PASS: Windows x64 player loads expanded scene, source sprites, four return zones; trigger restores spawn, movement and layers.":"FAIL: standalone return state";
            }
            catch(Exception e){ok=false;result="FAIL: standalone render: "+e;}
            File.WriteAllText(Path.Combine(output,"standalone-result.txt"),result);
            Debug.Log(result);Application.Quit(ok?0:1);
        }
#endif
    }
}
