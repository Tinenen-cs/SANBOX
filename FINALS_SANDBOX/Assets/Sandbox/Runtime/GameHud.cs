using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Mossmere
{
    public class GameHud : MonoBehaviour
    {
        public Explorer player;
        public AudioSource music;
        public Text AreaText { get; private set; }
        public Text MusicText { get; private set; }
        public Button HomeButton { get; private set; }
        public Button MusicButton { get; private set; }
        public Canvas HudCanvas { get; private set; }
        public VillagerDialogue ActiveVillager { get; private set; }
        public Text DialogueText { get; private set; }
        public bool DialogueVisible => dialoguePanel!=null && dialoguePanel.activeSelf;
        Font font;
        Text toast;
        GameObject hint;
        int lastReturns;
        float toastUntil;
        ReturnZone[] zones;
        VillagerDialogue[] villagers;
        GameObject dialoguePanel;
        Text speaker;
        int dialogueIndex;
        readonly Color ink = new(.055f,.12f,.14f,.95f);
        readonly Color cream = new(.97f,.96f,.86f,1);

        void Start()
        {
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root=new GameObject("English HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            root.transform.SetParent(transform,false);
            HudCanvas=root.GetComponent<Canvas>();HudCanvas.renderMode=RenderMode.ScreenSpaceCamera;
            HudCanvas.worldCamera=player.follow.GetComponent<Camera>();HudCanvas.planeDistance=1;HudCanvas.sortingOrder=5000;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            if(EventSystem.current==null)
            {
                var events=new GameObject("HUD input",typeof(EventSystem));
                events.transform.SetParent(transform,false);
                events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            var title=Panel(root.transform,"Location",new Vector2(0,1),new Vector2(24,-24),new Vector2(280,78));
            Label(title,"MOSSMERE",new Vector2(14,-9),new Vector2(252,30),25,FontStyle.Bold);
            AreaText=Label(title,"Village",new Vector2(14,-43),new Vector2(252,25),17);
            HomeButton=MakeButton(root.transform,"Return to Spawn [R]",new Vector2(-24,-24),new Vector2(202,44));
            HomeButton.onClick.AddListener(player.ReturnToSpawn);
            MusicButton=MakeButton(root.transform,"Music: On [M]",new Vector2(-238,-24),new Vector2(164,44));
            MusicText=MusicButton.GetComponentInChildren<Text>();MusicButton.onClick.AddListener(ToggleMusic);
            var controls=Panel(root.transform,"Controls",Vector2.zero,new Vector2(24,24),new Vector2(610,66));
            Label(controls,"WASD / Arrow keys: Move     R: Return to spawn",new Vector2(14,-8),new Vector2(582,24),16);
            Label(controls,"E: Next dialogue     M: Music on/off     Esc: Quit",new Vector2(14,-34),new Vector2(582,24),15);
            var notice=Panel(root.transform,"Return notice",new Vector2(.5f,1),new Vector2(0,-24),new Vector2(330,42));
            toast=Label(notice,"Returned to village spawn",new Vector2(10,-7),new Vector2(310,28),17,FontStyle.Bold);toast.alignment=TextAnchor.MiddleCenter;
            notice.gameObject.SetActive(false);
            var context=Panel(root.transform,"Beacon guidance",new Vector2(1,0),new Vector2(-24,24),new Vector2(382,66));
            Label(context,"RETURN BEACON",new Vector2(14,-7),new Vector2(354,24),17,FontStyle.Bold);
            Label(context,"Walk into the glowing ring to return to spawn.",new Vector2(14,-34),new Vector2(354,24),15);
            hint=context.gameObject;hint.SetActive(false);
            var conversation=Panel(root.transform,"Villager conversation",new Vector2(.5f,0),new Vector2(0,106),new Vector2(680,152));
            speaker=Label(conversation,"Villager",new Vector2(18,-10),new Vector2(644,26),20,FontStyle.Bold);
            DialogueText=Label(conversation,"",new Vector2(18,-43),new Vector2(644,62),17);
            DialogueText.alignment=TextAnchor.UpperLeft;
            Label(conversation,"Walk away to close",new Vector2(18,-119),new Vector2(470,22),14);
            var next=MakeButton(conversation,"Next [E]",new Vector2(-14,10),new Vector2(112,36),new Vector2(1,0));next.onClick.AddListener(AdvanceDialogue);
            dialoguePanel=conversation.gameObject;dialoguePanel.SetActive(false);
            zones=FindObjectsByType<ReturnZone>(FindObjectsSortMode.None);
            villagers=FindObjectsByType<VillagerDialogue>(FindObjectsSortMode.None);
            lastReturns=player.returns;
        }
        void Update()
        {
            if(AreaText==null)return;
            Vector2 p=player.transform.position;
            AreaText.text=p.y>12?(p.x< -14?"Woodland Cottage":"Northern Lookout"):(p.x< -16?"West Meadow":(p.x>14?"Lakeside Pier":(p.y< -11?"Southern Trail":"Village")));
            if(Keyboard.current!=null && Keyboard.current.mKey.wasPressedThisFrame)ToggleMusic();
            MusicText.text=music!=null && !music.mute?"Music: On [M]":"Music: Off [M]";
            if(lastReturns!=player.returns){lastReturns=player.returns;toastUntil=Time.unscaledTime+2.2f;CloseDialogue();}
            toast.transform.parent.gameObject.SetActive(Time.unscaledTime<toastUntil);
            bool near=false;foreach(var zone in zones)if(Vector2.Distance(p,zone.transform.position)<4.3f){near=true;break;}
            hint.SetActive(near);
            UpdateDialogue(p);
        }
        void UpdateDialogue(Vector2 position)
        {
            if(ActiveVillager!=null && Vector2.Distance(position,ActiveVillager.transform.position)>ActiveVillager.greetingRadius+.45f)CloseDialogue();
            if(ActiveVillager==null)
            {
                VillagerDialogue nearest=null;float best=float.MaxValue;
                foreach(var villager in villagers)
                {
                    float distance=Vector2.Distance(position,villager.transform.position);
                    if(distance<=villager.greetingRadius && distance<best && villager.lines!=null && villager.lines.Length>0){nearest=villager;best=distance;}
                }
                if(nearest!=null)
                {
                    ActiveVillager=nearest;dialogueIndex=0;speaker.text=nearest.characterName;DialogueText.text=nearest.lines[0];dialoguePanel.SetActive(true);
                }
            }
            if(ActiveVillager!=null && Keyboard.current!=null && Keyboard.current.eKey.wasPressedThisFrame)AdvanceDialogue();
        }
        public void AdvanceDialogue()
        {
            if(ActiveVillager==null || ActiveVillager.lines==null || ActiveVillager.lines.Length==0)return;
            dialogueIndex=(dialogueIndex+1)%ActiveVillager.lines.Length;DialogueText.text=ActiveVillager.lines[dialogueIndex];
        }
        void CloseDialogue(){ActiveVillager=null;dialogueIndex=0;if(dialoguePanel!=null)dialoguePanel.SetActive(false);}
        public void ToggleMusic(){if(music!=null)music.mute=!music.mute;}
        RectTransform Panel(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size)
        {
            var o=new GameObject(name,typeof(RectTransform),typeof(Image));o.transform.SetParent(parent,false);
            var rect=o.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=anchor;rect.pivot=anchor;rect.anchoredPosition=position;rect.sizeDelta=size;
            o.GetComponent<Image>().color=ink;o.GetComponent<Image>().raycastTarget=false;
            var trim=new GameObject("Gold edge",typeof(RectTransform),typeof(Image));trim.transform.SetParent(rect,false);
            var edge=trim.GetComponent<RectTransform>();edge.anchorMin=new Vector2(0,1);edge.anchorMax=Vector2.one;edge.pivot=new Vector2(.5f,1);edge.sizeDelta=new Vector2(0,3);edge.anchoredPosition=Vector2.zero;
            trim.GetComponent<Image>().color=new Color(.91f,.77f,.43f,1);trim.GetComponent<Image>().raycastTarget=false;
            return rect;
        }
        Text Label(Transform parent,string value,Vector2 position,Vector2 size,int fontSize,FontStyle style=FontStyle.Normal)
        {
            var o=new GameObject(value,typeof(RectTransform),typeof(Text));o.transform.SetParent(parent,false);
            var rect=o.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=size;
            var text=o.GetComponent<Text>();text.font=font;text.text=value;text.fontSize=fontSize;text.fontStyle=style;text.color=cream;text.raycastTarget=false;text.alignment=TextAnchor.MiddleLeft;
            return text;
        }
        Button MakeButton(Transform parent,string value,Vector2 position,Vector2 size,Vector2? anchor=null)
        {
            var rect=Panel(parent,value,anchor??Vector2.one,position,size);
            var image=rect.GetComponent<Image>();image.raycastTarget=true;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var colors=button.colors;colors.highlightedColor=new Color(.48f,.82f,.78f);colors.pressedColor=new Color(.3f,.66f,.61f);button.colors=colors;
            var label=Label(rect,value,new Vector2(8,-7),size-new Vector2(16,14),16,FontStyle.Bold);label.alignment=TextAnchor.MiddleCenter;
            return button;
        }
    }
}
