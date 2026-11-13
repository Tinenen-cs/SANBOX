using UnityEngine;

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
            beam=Quad("Beacon light column",new Vector3(0,.75f,0),new Vector3(.65f,1.9f,1),true);
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
