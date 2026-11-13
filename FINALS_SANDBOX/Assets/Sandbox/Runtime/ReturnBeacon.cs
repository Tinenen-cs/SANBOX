using UnityEngine;
using UnityEngine.UI;

namespace Mossmere
{
    public class ReturnBeacon : MonoBehaviour
    {
        public Material glowMaterial;
        Transform beam;
        Transform ring;
        public void BuildVisuals()
        {
            if(transform.Find("Glowing return ring")!=null)return;
            ring=Quad("Glowing return ring",Vector3.zero,new Vector3(3.3f,3.3f,1),false);
            beam=Quad("Beacon light column",new Vector3(0,1.1f,0),new Vector3(.85f,2.8f,1),true);
            var sign=new GameObject("RETURN TO SPAWN label",typeof(RectTransform),typeof(Canvas));
            sign.transform.SetParent(transform,false);sign.transform.localPosition=new Vector3(0,2.7f,0);sign.transform.localScale=Vector3.one*.012f;
            var canvas=sign.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.overrideSorting=true;canvas.sortingOrder=3300;
            sign.GetComponent<RectTransform>().sizeDelta=new Vector2(300,42);
            var background=new GameObject("Sign background",typeof(RectTransform),typeof(Image));background.transform.SetParent(sign.transform,false);
            var rect=background.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.sizeDelta=Vector2.zero;
            background.GetComponent<Image>().color=new Color(.03f,.13f,.17f,.94f);background.GetComponent<Image>().raycastTarget=false;
            var label=new GameObject("English label",typeof(RectTransform),typeof(Text));label.transform.SetParent(sign.transform,false);
            rect=label.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.sizeDelta=Vector2.zero;
            var text=label.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=26;text.fontStyle=FontStyle.Bold;text.alignment=TextAnchor.MiddleCenter;text.text="RETURN TO SPAWN";text.color=new Color(.68f,1,.94f);text.raycastTarget=false;
        }
        Transform Quad(string name,Vector3 position,Vector3 scale,bool column)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Quad);obj.name=name;obj.transform.SetParent(transform,false);obj.transform.localPosition=position;obj.transform.localScale=scale;
            if(Application.isPlaying)Destroy(obj.GetComponent<Collider>());else DestroyImmediate(obj.GetComponent<Collider>());
            var renderer=obj.GetComponent<MeshRenderer>();renderer.sharedMaterial=glowMaterial;renderer.sortingOrder=column?3101:3100;
            var properties=new MaterialPropertyBlock();properties.SetFloat("_Beam",column?1:0);renderer.SetPropertyBlock(properties);
            return obj.transform;
        }
        void Start()
        {
            BuildVisuals();ring=transform.Find("Glowing return ring");beam=transform.Find("Beacon light column");
            // Property blocks are not serialized with scene renderers.
            var block=new MaterialPropertyBlock();block.SetFloat("_Beam",1);beam.GetComponent<Renderer>().SetPropertyBlock(block);
        }
        void Update()
        {
            if(ring!=null)ring.localScale=new Vector3(3.3f,3.3f,1)*(1+.045f*Mathf.Sin(Time.time*2));
        }
    }
}
