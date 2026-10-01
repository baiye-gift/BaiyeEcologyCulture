using UnityEngine;

namespace Baiye.EcologyCulture
{
    // Native ButtonMenuSideScreen gathers every ISidescreenButtonControl on the
    // target. This supplies an explicit Config section, including direct choices,
    // without relying on the generic bottom action menu or custom UI prefab paths.
    public sealed class CultureSettingButton : KMonoBehaviour, ISidescreenButtonControl
    {
        public const int Count=12;
        [SerializeField] public int Choice;
        private CultureProcess Process=>GetComponent<CultureProcess>();
        public string SidescreenTitle=>CultureIds.Text("培养设置","Culture settings");
        public string SidescreenButtonText
        {
            get
            {
                var p=Process;if(p==null)return "";
                if(Choice<3)return (p.State.Policy==(CulturePolicy)Choice?"✓ ":"")+CultureProcess.PolicyName((CulturePolicy)Choice);
                if(Choice<6)return (p.SelectedSpecies==(CultureSpecies)(Choice-3)?"✓ ":"")+CultureIds.Species((CultureSpecies)(Choice-3));
                switch(Choice)
                {
                    case 6:return CultureIds.Text("更换培养株 → ","Change culture → ")+CultureIds.Species(p.SelectedSpecies);
                    case 7:return CultureIds.Text("营养料：","Medium: ")+CultureIds.Text(p.UsePollutedDirt?"污染土":"菌泥",p.UsePollutedDirt?"Polluted dirt":"Slime");
                    case 8:return CultureIds.Text("特殊性状 / 采样：","Trait / sample: ")+CultureIds.Trait(p.SelectedTrait);
                    case 9:return CultureIds.Text(p.KeepSample?"换种保留样本：是":"换种保留样本：否",p.KeepSample?"Keep sample on change: yes":"Keep sample on change: no");
                    case 10:return p.SampleRequested?CultureIds.Text("采样工作已安排","Sampling requested"):CultureIds.Text("安排复制人采样","Request duplicant sampling");
                    default:return CultureIds.Text(p.ShowDetails?"收起详细库存、产率与经历":"展开详细库存、产率与经历",p.ShowDetails?"Hide inventory, rates and exposure":"Show inventory, rates and exposure");
                }
            }
        }
        public string SidescreenButtonTooltip
        {
            get
            {
                if(Choice<3)return CultureIds.Text("直接切换培养策略；不改变当前藻类。","Select strategy; current species stays the same.");
                if(Choice<6)return CultureIds.Text("只选择目标种类。当前培养物不变；点击“更换培养株”执行。","Select target only. Press Change culture to execute.");
                if(Choice==6)return CultureIds.Text("旧株保样、真实回收、仓内过滤和 30 秒循环清洗，随后重新接种。无需排液管。","Save sample, recover stock, filter internally, recirculate wash for 30 s, then inoculate. No liquid outlet.");
                if(Choice==7)return CultureIds.Text("点击在菌泥和污染土配送之间切换。已有两类营养料都可使用；CO₂ 为周围自动吸收的可选增速来源。","Choose slime or polluted dirt delivery. Both stored media remain usable; nearby CO2 is an optional bonus.");
                if(Choice==8)return CultureIds.Text("循环选择基础、耐热、低耗、速生、高营养株；高级株接种需要相同种类与性状的真实样本。","Cycle base, heat, dim, fast and nutritious. Advanced inoculation requires a matching physical sample.");
                if(Choice==10)return CultureIds.Text("原版工作任务：取走 50 g 母株；新性状需对应实际培养经历达到 1800 秒。","Native work chore: removes 50 g mother culture; a new trait requires 1800 s of corresponding exposure.");
                return CultureIds.Text("详细数据在状态栏分为库存、实际产率、性状经历三组。","Detailed status is grouped into inventory, actual output and strain exposure.");
            }
        }
        public bool SidescreenEnabled()=>Process!=null;
        public bool SidescreenButtonInteractable()
        {
            var p=Process;if(p==null)return false;
            if((Choice>=3&&Choice<=6)||Choice==8||Choice==9)return !p.State.Switching;
            if(Choice==10)return !p.SampleRequested&&p.CanSample(p.SelectedTrait);
            return true;
        }
        public void OnSidescreenButtonPressed()
        {
            if(!SidescreenButtonInteractable())return;
            var p=Process;
            if(Choice<3){p.SelectPolicy((CulturePolicy)Choice);return;}
            if(Choice<6){p.SelectSpecies((CultureSpecies)(Choice-3));return;}
            switch(Choice)
            {
                case 6:p.ChangeCulture();break;
                case 7:p.ToggleMedium();break;
                case 8:p.CycleTrait();break;
                case 9:p.ToggleKeepSample();break;
                case 10:p.RequestSample();break;
                case 11:p.ToggleDetails();break;
            }
        }
        public int HorizontalGroupID()=>Choice<3?0:Choice<6?1:-1;
        public int ButtonSideScreenSortOrder()=>90;
        public void SetButtonTextOverride(ButtonMenuTextOverride textOverride){}
    }
}
