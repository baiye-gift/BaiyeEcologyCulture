using System.Collections.Generic;
using System.Linq;

namespace Baiye.EcologyCulture
{
    public static class CultureCodex
    {
        public static void Attach(BuildingDef def,System.Func<bool> language=null)
        {
            var previous=def.ExtendCodexEntry;
            def.ExtendCodexEntry=entry=>Append(previous!=null?previous(entry):entry,def.PrefabID,language!=null?language():CultureIds.Chinese);
        }
        public static CodexEntry Append(CodexEntry entry,string id,bool chinese)
        {
            if(entry==null||id!=CultureIds.Building)return entry;
            string marker="BAIYE_ECOLOGY_GUIDE_"+id;
            if(entry.contentContainers.Any(c=>c!=null&&c.content!=null&&c.content.OfType<CodexText>().Any(t=>t.messageID==marker)))return entry;
            var sections=Sections(chinese);
            for(int n=0;n<sections.Length;n+=2)
                entry.AddContentContainer(new ContentContainer(new List<ICodexWidget>{new CodexText(sections[n],CodexTextStyle.Subtitle,n==0?marker:null),new CodexText(sections[n+1])},ContentContainer.ContentLayout.Vertical));
            return entry;
        }
        public static string[] Sections(bool zh)=>zh?new[]{
            "连接与启动",
            "每仓只培养一种实际培养株。底部左侧液口 (-1,0) 接水或盐水，右侧气口 (1,0) 输出氧气；电力和自动化接口在 (0,1)。菌泥或污染土由复制人/自动清扫器送入。无需 CO₂ 输入管或排液管；周围 3 格内 CO₂ 是可选增产资源。基础株来自封闭种质库，建立约 2 kg 初始培养物需 120 秒并消耗真实材料；特殊株另需对应 50 g 样本。",
            "基础有机培养与光合增产",
            "基础配方：0.8 kg 有机营养料 + 0.2 kg 水 → 1 kg 含水生物量，气体净输出为零。额外光合增产每增加 1 kg 生物量，消耗 1.437333 kg CO₂、0.588 kg 水和 0.02 kg 矿物养分，并输出 1.045333 kg 氧气。无旧肥料时使用有机培养基的矿物组分，其余成为残渣。盐水按 93% 水与 7% 盐核算。内部代谢抽象进培养效率、耗电和健康；这是游戏净配方，并未逐步模拟呼吸，也不是全部生长都来自光合作用。",
            "三种培养策略",
            "保种：120 W，少量生长至 16 kg 后维持，不自动采收，光合增长上限为基础增长的 25%。食物优先：600 W，12 kg 时自动采收 4 kg，保留 8 kg，光合上限 50%。供氧优先：960 W，18 kg 时采收 4 kg，保留 14 kg，光合上限 100%。上限还受真实材料、母株密度、健康、拥挤与性状影响。低耗株修正培养功率；接种、清洗和换种为 480 W。",
            "藻类、采收和食物",
            "基础绿藻适温 10–40°C，采收原版藻类，供氧设备可继续使用。螺旋藻适温 20–38°C，采收螺旋藻泥；盐生藻适温 15–42°C，采收盐生藻叶，偏好盐水但也能使用清水。耐热株提高上限 12°C。食物按扣除的真实生物量、健康和性状计算，未成为食品的部分是残渣。培养仓自动批量采收，无需人工摘取；复制人/清扫器应及时清空产物仓。藻泥粥用食物压制器，藻麦饼和盐藻脆片用电动烤炉，菌菇藻卷和藻酱豆腐用燃气灶。食品应冷藏，活体母株和样本不可直接食用。",
            "选择、换种和样本",
            "在配置页“培养设置”直接选择策略及目标藻类；目标选择不会立即改变实际培养株，必须点击执行换种。换种依次保留旧样本（可关闭）、回收旧株、仓内过滤、循环清洗 30 秒，再接种新株。旧库存按旧藻类处理，不能直接变成另一种食品。基础性状可内置接种，特殊性状需实际配送同种同型样本。人工采样是复制人工作，消耗 50 g 母株；新性状需对应经历累计 1800 秒。失活或未完成接种的回收物是残渣。",
            "状态与堵塞排查",
            "默认面板分别显示实际株/策略/阶段、健康/采收进度和当前生产模式。“光合增产中”只代表近期实际完成的增产事务，CO₂ 缓存非空不等于产氧。无 CO₂ 可继续基础培养；氧缓存满只暂停光合增产。缺水、营养料、电力、温度不适或产物仓满会暂停相关工作并保留库存。详细信息可展开查看实际库存、近 5 秒产率与性状经历。中央浓度表示真实 0–20 kg 母株，两侧料位表示水与产物库存；断电、读档和旧超量库存不丢失物料。"
        }:new[]{
            "Connections and startup","One actual species per chamber. Liquid inlet (-1,0): water or salt water. Gas outlet (1,0): oxygen. Power and automation (0,1). Deliver slime or polluted dirt manually or by sweeper. Optional ambient CO2 is collected within 3 cells; no CO2 pipe or liquid drain. The sealed library supplies basic strains. Initial 2 kg culture takes 120 seconds and real resources; advanced strains also need a matching 50 g sample.",
            "Organic culture and photosynthetic bonus","Baseline: 0.8 kg organic medium + 0.2 kg water yields 1 kg wet biomass with zero net gas output. Each additional 1 kg photosynthetic biomass uses 1.437333 kg CO2, 0.588 kg water and 0.02 kg mineral nutrients, yielding 1.045333 kg oxygen. Organic medium supplies minerals when legacy fertilizer is absent; its remainder becomes residue. Salt water is 93% water and 7% salt. Internal metabolism is abstracted into efficiency, power and health. This is a game net recipe, not a stepwise respiration simulation.",
            "Three strategies","Preserve: 120 W, hold at 16 kg, no auto harvest, bonus capped at 25% of baseline gain. Food: 600 W, harvest 4 kg at 12 kg, retain 8 kg, bonus cap 50%. Oxygen: 960 W, harvest 4 kg at 18 kg, retain 14 kg, bonus cap 100%. Actual growth also depends on stock, density, health, crowding and traits. Dim strains reduce cultivation power. Inoculation, cleaning and switching use 480 W.",
            "Species and food","Green algae: 10–40 C, produces native algae. Spirulina: 20–38 C, produces paste. Saline algae: 15–42 C, produces leaves; prefers salt water but accepts fresh water. Heat strains add 12 C to the maximum. Harvest consumes actual biomass; health and traits determine food yield, remainder becomes residue. Automatic batches need prompt removal by duplicants or sweepers. Porridge: musher; cakes/chips: grill; wraps/tofu: gas range. Refrigerate food. Live culture and samples are not edible.",
            "Switching and samples","Select policy and target species in Culture settings; execute the switch separately. The actual species changes only after optional old-sample preservation, recovery, internal filtration and 30 seconds of washing. Old biomass keeps its old identity. Advanced traits require a matching physical sample. Manual sampling is duplicant work and consumes 50 g live culture; new traits require 1800 seconds of matching exposure. Dead or incomplete inoculation yields residue on recovery.",
            "Status and troubleshooting","Default status separates actual culture/policy/phase, health/progress and current production mode. Photosynthetic bonus means a recent committed transaction, not merely stored CO2. Without CO2 baseline growth continues. Full oxygen storage pauses only bonus growth. Water, medium, power, unsuitable temperature and full products can block work; inventories persist. Details expose stock, actual 5-second rates and trait exposure. Central concentration reflects 0–20 kg live biomass; side gauges reflect water and products, including clamped old overfull stock without deletion."
        };
    }
}
