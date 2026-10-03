using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Baiye.EcologyCulture
{
    // Native button/LocText styling and DropDown popup handling. DetailsScreen
    // owns each instance and its target; no global selected culture reference.
    public sealed class CultureSettingsSideScreen : SideScreenContent
    {
        public const int MainSelectorCount=3;
        [SerializeField] public LayoutElement ButtonTemplate;
        [SerializeField] public LocText LabelTemplate;
        private CultureProcess process;
        private bool built;
        private float nextRefresh;
        private LocText current,samplingReason,inoculumSource;
        private DropDown policy,species,medium,inoculum,sampling;
        private KButton change,restart,eject,advanced,keep,sample,details;
        private GameObject advancedBody,contentBody;
        private RectTransform contentRect;
        private LayoutElement panelSize;
        private readonly List<DropDown> dropdowns=new List<DropDown>();
        // Bind while cloning, including inactive children. Refresh must not
        // depend on a button, advanced group or native host being visible.
        private readonly Dictionary<KButton,ButtonView> buttons=new Dictionary<KButton,ButtonView>();
        private sealed class ButtonView
        {
            public GameObject Root;
            public LocText Label;
            public ToolTip Tooltip;
        }
        public override bool IsValidForTarget(GameObject target)=>target!=null&&target.GetComponent<CultureProcess>()!=null;
        public override int GetSideScreenSortOrder()=>90;
        public override string GetTitle()=>CultureIds.Text("培养设置","Culture settings");
        public override void SetTarget(GameObject target)
        {
            var next=target==null?null:target.GetComponent<CultureProcess>();
            if(next!=process)CloseDropdowns();process=next;if(process==null)return;
            Build();RefreshView();
            Debug.Log("[BaiyeEcologyCulture] Settings target: "+target.name+", stage="+process.State.Stage+", switching="+process.State.Switching);
        }
        public override void ClearTarget(){CloseDropdowns();process=null;}
        private void Update(){if(process!=null&&isActiveAndEnabled&&Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.5f;RefreshView();}}
        private void CloseDropdowns(){foreach(var d in dropdowns)if(d!=null)d.Close();}
        protected override void OnShow(bool show){if(!show)CloseDropdowns();base.OnShow(show);if(show&&process!=null)RefreshView();}
        private void Build()
        {
            if(built)return;built=true;
            panelSize=gameObject.AddOrGet<LayoutElement>();panelSize.ignoreLayout=false;panelSize.minWidth=280;panelSize.preferredWidth=280;panelSize.flexibleWidth=0;panelSize.flexibleHeight=0;panelSize.minHeight=240;panelSize.preferredHeight=240;
            var scroll=gameObject.AddOrGet<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;
            var viewport=new GameObject("Viewport",typeof(RectTransform),typeof(Image),typeof(RectMask2D));viewport.transform.SetParent(transform,false);viewport.layer=gameObject.layer;Stretch(viewport.GetComponent<RectTransform>());viewport.GetComponent<Image>().color=Color.clear;
            contentBody=new GameObject("Content",typeof(RectTransform),typeof(VerticalLayoutGroup),typeof(ContentSizeFitter));contentBody.layer=gameObject.layer;contentBody.transform.SetParent(viewport.transform,false);ContentContainer=contentBody;
            contentRect=contentBody.GetComponent<RectTransform>();contentRect.anchorMin=new Vector2(0,1);contentRect.anchorMax=Vector2.one;contentRect.pivot=new Vector2(.5f,1);contentRect.sizeDelta=Vector2.zero;
            contentBody.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=viewport.GetComponent<RectTransform>();scroll.content=contentRect;
            var layout=contentBody.GetComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(8,8,8,8);layout.spacing=5;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            current=Label(contentBody,42);
            policy=Selector(contentBody,Enum.GetValues(typeof(CulturePolicy)).Cast<CulturePolicy>().Select(v=>new Option((int)v,CultureProcess.PolicyName(v))),v=>process.SelectPolicy((CulturePolicy)v));
            species=Selector(contentBody,Enum.GetValues(typeof(CultureSpecies)).Cast<CultureSpecies>().Select(v=>new Option((int)v,CultureIds.Species(v))),v=>process.SelectSpecies((CultureSpecies)v));
            medium=Selector(contentBody,new[]{new Option(0,CultureIds.Text("菌泥","Slime")),new Option(1,CultureIds.Text("污染土","Polluted dirt"))},v=>process.SelectMedium(v==1));
            change=Button(contentBody,()=>{if(process.Inactive)process.RestartBase();else process.ChangeCulture();});
            restart=Button(contentBody,()=>process.RestartBase());
            eject=Button(contentBody,()=>process.Output.Request());advanced=Button(contentBody,()=>process.ToggleAdvanced());
            advancedBody=new GameObject("Advanced",typeof(RectTransform),typeof(VerticalLayoutGroup));advancedBody.layer=gameObject.layer;advancedBody.transform.SetParent(contentBody.transform,false);
            var al=advancedBody.GetComponent<VerticalLayoutGroup>();al.spacing=5;al.childControlWidth=true;al.childControlHeight=true;al.childForceExpandWidth=true;al.childForceExpandHeight=false;
            inoculum=Selector(advancedBody,Traits(),v=>process.SelectTrait((CultureTrait)v));inoculumSource=Label(advancedBody,96);keep=Button(advancedBody,()=>process.ToggleKeepSample());
            sampling=Selector(advancedBody,Traits(),v=>process.SelectSampleTrait((CultureTrait)v));samplingReason=Label(advancedBody,96);
            sample=Button(advancedBody,()=>{if(process.SampleRequested)process.CancelSample();else process.RequestSample();});details=Button(advancedBody,()=>process.ToggleDetails());
        }
        private static IEnumerable<Option> Traits()=>Enum.GetValues(typeof(CultureTrait)).Cast<CultureTrait>().Select(v=>new Option((int)v,CultureIds.Trait(v)));
        private LocText Label(GameObject parent,float height)
        {
            var go=Util.KInstantiateUI(LabelTemplate.gameObject,parent,true);var text=go.GetComponent<LocText>();text.key="";text.raycastTarget=false;text.textWrappingMode=TMPro.TextWrappingModes.Normal;text.fontSize=14;
            var l=go.AddOrGet<LayoutElement>();l.ignoreLayout=false;l.minHeight=height;l.preferredHeight=-1;l.flexibleHeight=0;l.minWidth=0;l.preferredWidth=-1;l.flexibleWidth=1;return text;
        }
        private KButton Button(GameObject parent,System.Action action)
        {
            var go=Util.KInstantiateUI(ButtonTemplate.gameObject,parent,true);var button=go.GetComponentInChildren<KButton>(true);var l=go.GetComponent<LayoutElement>();l.ignoreLayout=false;l.minHeight=34;l.preferredHeight=34;l.flexibleWidth=1;l.flexibleHeight=0;
            var view=new ButtonView{Root=go,Label=go.GetComponentInChildren<LocText>(true),Tooltip=go.GetComponentInChildren<ToolTip>(true)};
            buttons.Add(button,view);
            button.ClearOnClick();button.onClick+=()=>
            {
                if(process==null)return;
                string caption=view.Label.text;
                Debug.Log("[BaiyeEcologyCulture] Settings action begin: "+caption+", stage="+process.State.Stage);
                action();RefreshView();
                Debug.Log("[BaiyeEcologyCulture] Settings action end: "+caption+", stage="+process.State.Stage+", switching="+process.State.Switching+", settingsActive="+gameObject.activeInHierarchy+", detailsActive="+(DetailsScreen.Instance!=null&&DetailsScreen.Instance.gameObject.activeInHierarchy));
            };return button;
        }
        private DropDown Selector(GameObject parent,IEnumerable<Option> options,System.Action<int> selected)
        {
            var button=Button(parent,()=>{});var d=button.gameObject.AddComponent<DropDown>();dropdowns.Add(d);
            d.openButton=button;d.selectedLabel=buttons[button].Label;d.dropdownAlignmentTarget=button.GetComponent<RectTransform>();d.addEmptyRow=false;
            d.targetDropDownContainer=GameScreenManager.Instance.ssOverlayCanvas.gameObject;
            var popup=new GameObject("CultureOptions",typeof(RectTransform),typeof(Image),typeof(ScrollRect));popup.layer=gameObject.layer;popup.SetActive(false);popup.transform.SetParent(d.targetDropDownContainer.transform,false);
            var rect=popup.GetComponent<RectTransform>();rect.pivot=new Vector2(0,1);rect.sizeDelta=new Vector2(300,160);popup.GetComponent<Image>().color=new Color(.16f,.17f,.22f,1);
            var viewport=new GameObject("Viewport",typeof(RectTransform),typeof(RectMask2D));viewport.layer=gameObject.layer;viewport.transform.SetParent(popup.transform,false);Stretch(viewport.GetComponent<RectTransform>());
            var content=new GameObject("Content",typeof(RectTransform),typeof(VerticalLayoutGroup),typeof(ContentSizeFitter));content.layer=gameObject.layer;content.transform.SetParent(viewport.transform,false);
            var cr=content.GetComponent<RectTransform>();cr.anchorMin=new Vector2(0,1);cr.anchorMax=new Vector2(1,1);cr.pivot=new Vector2(.5f,1);cr.sizeDelta=Vector2.zero;
            var cl=content.GetComponent<VerticalLayoutGroup>();cl.childControlWidth=true;cl.childControlHeight=true;cl.childForceExpandWidth=true;cl.childForceExpandHeight=false;
            content.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var sr=popup.GetComponent<ScrollRect>();sr.viewport=viewport.GetComponent<RectTransform>();sr.content=cr;sr.horizontal=false;sr.vertical=true;
            var entry=Util.KInstantiateUI(ButtonTemplate.GetComponentInChildren<KButton>(true).gameObject,popup,false);entry.name="OptionTemplate";entry.SetActive(false);var el=entry.AddOrGet<LayoutElement>();el.minHeight=34;el.preferredHeight=34;el.minWidth=300;
            entry.GetComponent<KButton>().ClearOnClick();
            var row=entry.AddComponent<DropDownEntry>();row.label=entry.GetComponentInChildren<LocText>(true);row.button=entry.GetComponent<KButton>();row.tooltip=entry.GetComponentInChildren<ToolTip>(true);
            d.rowEntryPrefab=entry;d.contentContainer=cr;d.scrollRect=popup;
            d.Initialize(options,(option,data)=>{if(process==null||option==null)return;selected(((Option)option).Value);RefreshView();},refreshAction:(r,data)=>{if(r.tooltip!=null)r.tooltip.SetSimpleTooltip(r.entryData is Option o?o.GetProperName():"");},displaySelectedValueWhenClosed:false);
            return d;
        }
        private static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;}
        private void Caption(KButton b,string caption,string tip,bool enabled=true)
        {var view=buttons[b];view.Label.SetText(caption);b.isInteractable=enabled;view.Tooltip?.SetSimpleTooltip(tip);}
        private static void SelectorCaption(DropDown d,string name,string value,bool enabled=true)
        {d.selectedLabel.SetText(name+"："+value+CultureIds.Text(" [选择]"," [select]"));d.openButton.isInteractable=enabled;if(!enabled)d.Close();}
        private void ResizePanel()
        {
            if(!gameObject.activeInHierarchy||contentRect==null)return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            var scaler=GameScreenManager.Instance.ssOverlayCanvas.GetComponent<KCanvasScaler>();
            float scale=scaler==null?1:Mathf.Max(.01f,scaler.GetCanvasScale());
            float budget=Mathf.Max(180,Screen.height/scale-150);
            float height=Mathf.Clamp(LayoutUtility.GetPreferredHeight(contentRect),60,budget);
            if(Mathf.Abs(panelSize.preferredHeight-height)>.5f){panelSize.minHeight=height;panelSize.preferredHeight=height;LayoutRebuilder.MarkLayoutForRebuild(GetComponent<RectTransform>());}
        }
        private void RefreshView()
        {
            if(!built||process==null)return;var p=process;
            current.SetText(CultureIds.Text("当前培养：","Current culture: ")+CultureIds.Species(p.State.Species)+" · "+CultureIds.Trait(p.State.Trait));
            SelectorCaption(policy,CultureIds.Text("培养策略","Strategy"),CultureProcess.PolicyName(p.State.Policy));
            SelectorCaption(species,CultureIds.Text("培养种类","Target species"),CultureIds.Species(p.SelectedSpecies),!p.State.Switching);
            SelectorCaption(medium,CultureIds.Text("营养料","Medium"),CultureIds.Text(p.UsePollutedDirt?"污染土":"菌泥",p.UsePollutedDirt?"Polluted dirt":"Slime"));
            buttons[change].Root.SetActive(p.Inactive||p.NeedsChange||p.State.Switching);
            string changeText=p.State.Switching?CultureIds.Text("回收与清洗进行中","Recovery/cleaning in progress"):p.Inactive?CultureIds.Text("清洗并重新接种基础株","Clean and restart base culture"):CultureIds.Text("开始换种：","Start change: ")+CultureIds.Species(p.SelectedSpecies)+" · "+CultureIds.Trait(p.SelectedTrait);
            Caption(change,changeText,CultureIds.Text("回收旧株、过滤和 30 秒循环清洗，再接种。特殊株需实际配送对应样本。","Recover old biomass, filter and wash for 30 s, then inoculate. Advanced traits need a physical matching sample."),!p.State.Switching);
            buttons[restart].Root.SetActive(p.CanReturnToBase);
            Caption(restart,CultureIds.Text("回收清洗并退回基础株","Recover, clean and return to base"),CultureIds.Text("撤回特殊株接种。真实母株先回收，再清洗并用水和营养料接种基础株；已配送样本保留。","Cancel advanced inoculation. Recover real biomass, wash, then grow base culture with water and medium; delivered samples remain stored."));
            Caption(eject,CultureIds.Text("排出暂存产物","Eject stored output"),p.Output.HasStock?CultureIds.Text("排出成品、副产物和样本；母株、水与营养料留仓。","Eject products, byproducts and samples; retain mother culture, water and medium."):CultureIds.Text("暂存仓为空，产物已自动排出。","Buffer empty; output is automatically ejected."),p.Output.HasStock);
            Caption(advanced,CultureIds.Text(p.ShowAdvanced?"收起高级设置":"展开高级设置",p.ShowAdvanced?"Hide advanced settings":"Show advanced settings"),CultureIds.Text("接种性状、保样、采样及详细数据。","Inoculum trait, preservation, sampling and details."));
            advancedBody.SetActive(p.ShowAdvanced);
            if(!p.ShowAdvanced){inoculum.Close();sampling.Close();ResizePanel();return;}
            SelectorCaption(inoculum,CultureIds.Text("接种性状","Inoculum trait"),CultureIds.Trait(p.SelectedTrait),!p.State.Switching);
            inoculumSource.SetText(p.InoculumHelp());
            Caption(keep,CultureIds.Text(p.KeepSample?"换种保留旧样本：是":"换种保留旧样本：否",p.KeepSample?"Preserve old sample: yes":"Preserve old sample: no"),CultureIds.Text("取走 50 g 真实母株，作为样本排出。","Remove 50 g real biomass and eject as sample."),!p.State.Switching);
            SelectorCaption(sampling,CultureIds.Text("采样目标","Sampling target"),CultureIds.Trait(p.SampleTrait),!p.SampleRequested);
            samplingReason.SetText(p.SampleReason());
            Caption(sample,CultureIds.Text(p.SampleRequested?"取消采样任务":"安排复制人采样",p.SampleRequested?"Cancel sampling":"Request duplicant sampling"),p.SampleReason(),p.SampleRequested||p.CanSample(p.SampleTrait));
            Caption(details,CultureIds.Text(p.ShowDetails?"收起详细数据":"展开详细数据",p.ShowDetails?"Hide detailed data":"Show detailed data"),CultureIds.Text("库存、实际产率与性状经历。","Stock, actual rates and strain exposure."));
            ResizePanel();
        }
        private sealed class Option : IListableOption
        {public int Value { get; }private readonly string name;public Option(int value,string name){Value=value;this.name=name;}public string GetProperName()=>name;}
    }
    [HarmonyPatch(typeof(DetailsScreen),"OnPrefabInit")]
    internal static class CultureSettingsRegistration
    {
        internal static bool AddScreenReference(List<DetailsScreen.SideScreenRef> screens,CultureSettingsSideScreen screen)
        {
            if(screens.Any(s=>s.screenPrefab is CultureSettingsSideScreen))return false;
            screens.Add(new DetailsScreen.SideScreenRef{name="BaiyeCultureSettings",screenPrefab=screen,screenInstance=screen,tab=DetailsScreen.SidescreenTabTypes.Config});
            return true;
        }
        private static void Postfix(DetailsScreen __instance)
        {
            var screens=(List<DetailsScreen.SideScreenRef>)AccessTools.Field(typeof(DetailsScreen),"sideScreens").GetValue(__instance);
            if(screens.Any(s=>s.screenPrefab is CultureSettingsSideScreen))return;
            var source=screens.Select(s=>s.screenPrefab).OfType<ButtonMenuSideScreen>().FirstOrDefault();
            var config=__instance.GetTabOfType(DetailsScreen.SidescreenTabTypes.Config);
            if(source==null||config?.bodyInstance==null){Debug.LogWarning("[BaiyeEcologyCulture] Native config body/button template unavailable; no global UI changes made.");return;}
            // A scene instance belongs to Config's content body. Never insert a
            // naked prefab beneath DetailsScreen's global VerticalLayoutGroup.
            var go=new GameObject("BaiyeCultureSettings",typeof(RectTransform),typeof(LayoutElement));go.SetActive(false);go.layer=source.gameObject.layer;go.transform.SetParent(config.bodyInstance.transform,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.sizeDelta=new Vector2(280,240);
            var screen=go.AddComponent<CultureSettingsSideScreen>();screen.ContentContainer=go;screen.ButtonTemplate=source.buttonPrefab;screen.LabelTemplate=source.buttonPrefab.GetComponentInChildren<LocText>(true);
            AddScreenReference(screens,screen);
            Debug.Log("[BaiyeEcologyCulture] Registered culture settings instance under Config body; native panels retained.");
        }
    }
}
