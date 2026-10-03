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
            "每仓只培养一种实际培养株。底部左侧液口 (-1,0) 接水或盐水，右侧气口 (1,0) 输出氧气；电力和自动化接口在 (0,1)。菌泥或污染土由复制人/自动清扫器送入。无需 CO2 输入管或排液管；周围 3 格内 CO2 是可选增产资源。基础株来自封闭种质库，建立约 2 kg 初始培养物需 120 秒并消耗真实材料；特殊株另需对应 50 g 样本。",
            "基础有机培养与光合增产",
            "基础配方：0.8 kg 有机营养料 + 0.2 kg 水 → 1 kg 含水生物量，气体净输出为零。额外光合增产每增加 1 kg 生物量，消耗 1.437333 kg CO2、0.588 kg 水和 0.02 kg 矿物养分，并输出 1.045333 kg 氧气。无旧肥料时使用有机培养基的矿物组分，其余成为残渣。盐水按 93% 水与 7% 盐核算。内部代谢抽象进培养效率、耗电和健康；这是游戏净配方，并未逐步模拟呼吸，也不是全部生长都来自光合作用。",
            "三种培养策略",
            "保种：120 W，少量生长至 16 kg 后维持，不自动采收，光合增长上限为基础增长的 25%。食物优先：600 W，12 kg 时自动采收 4 kg，保留 8 kg，光合上限 50%。供氧优先：960 W，18 kg 时采收 4 kg，保留 14 kg，光合上限 100%。上限还受真实材料、母株密度、健康、拥挤与性状影响。低耗株修正培养功率；接种、清洗和换种为 480 W。",
            "藻类、采收和食物",
            "基础绿藻适温 10–40°C，采收原版藻类；螺旋藻 20–38°C，采收藻泥；盐生藻 15–42°C，采收藻叶，偏好盐水但也可用清水。耐热株上限 +12°C。每批扣除 4 kg 真实母株，按健康/性状生成成品及残渣并自动排到建筑底部/侧面空格。非采收盐/残渣约 5 kg 一批，阶段结束排余量；真实样本生成后排出。复制人按储物箱/冰箱/厨房需求取用，清扫器按范围搬运；无目的地时不会自动生成搬运任务。藻泥粥用食物压制器，饼/脆片用电动烤炉，卷/豆腐用燃气灶。食品及时冷藏；母株和样本不可食用。",
            "选择、换种和样本",
            "主界面三个下拉框选择培养策略、培养种类与配送营养料；策略立即生效，选不同藻类后点击开始换种。高级设置默认折叠并可滚动，接种性状与采样目标分别选择，不能采样时显示原因。换种依次保样（可关闭）、回收旧株、过滤、用至少 0.6 kg 水循环清洗 30 秒，再接种；旧株不变成新食品。基础性状内置，特殊性状需对应真实 50 g 样本，不会天然生成。高营养株来源：先培养同种基础株，有机营养料库存至少 10 kg 时累计 1800 秒实际付费培养，再安排复制人 30 秒采样，消耗 50 g 母株得到真实样本；采收、维护或停机时间不计。已安排任务可取消。缺特殊样本的仓可回收清洗并退回目标藻类基础株；已配送样本保留。失活或未完成接种的旧生物量回收为残渣。",
            "状态与堵塞排查",
            "默认三组状态显示实际株/策略/阶段、健康/进度和当前模式或处理步骤。异常有世界图标；健康下降/失活分级通知，不逐帧刷屏。新受损记录原因，旧未知原因不猜测。产物仓不足下一批 4 kg 时，水、营养料、温度和付款正常则以 120 W 保种维护，暂停增长/产氧并维持原健康，空间恢复后继续；缺水/缺料/缺电/温度异常仍损害母株。出料格实心时保留库存，腾出底部/两侧空间或点击排出暂存产物。无 CO2 可继续基础培养；氧缓存满只暂停光合增产。高级设置可展开库存/近 5 秒产率/经历；母株、水、产物计量随真实库存，读档不删除旧超量物料。"
        }:new[]{
            "Connections and startup","One actual species per chamber. Liquid inlet (-1,0): water or salt water. Gas outlet (1,0): oxygen. Power and automation (0,1). Deliver slime or polluted dirt manually or by sweeper. Optional ambient CO2 is collected within 3 cells; no CO2 pipe or liquid drain. The sealed library supplies basic strains. Initial 2 kg culture takes 120 seconds and real resources; advanced strains also need a matching 50 g sample.",
            "Organic culture and photosynthetic bonus","Baseline: 0.8 kg organic medium + 0.2 kg water yields 1 kg wet biomass with zero net gas output. Each additional 1 kg photosynthetic biomass uses 1.437333 kg CO2, 0.588 kg water and 0.02 kg mineral nutrients, yielding 1.045333 kg oxygen. Organic medium supplies minerals when legacy fertilizer is absent; its remainder becomes residue. Salt water is 93% water and 7% salt. Internal metabolism is abstracted into efficiency, power and health. This is a game net recipe, not a stepwise respiration simulation.",
            "Three strategies","Preserve: 120 W, hold at 16 kg, no auto harvest, bonus capped at 25% of baseline gain. Food: 600 W, harvest 4 kg at 12 kg, retain 8 kg, bonus cap 50%. Oxygen: 960 W, harvest 4 kg at 18 kg, retain 14 kg, bonus cap 100%. Actual growth also depends on stock, density, health, crowding and traits. Dim strains reduce cultivation power. Inoculation, cleaning and switching use 480 W.",
            "Species and food","Green algae: 10–40 C, native algae. Spirulina: 20–38 C, paste. Saline algae: 15–42 C, leaves; salt or fresh water. Heat strains add 12 C. Each 4 kg real harvest ejects products and residue into a free base/side cell. Other byproducts accumulate to 5 kg or a stage boundary; samples eject on creation. Duplicants need storage/fridge/kitchen demand to fetch; sweepers need range. Porridge: musher; cakes/chips: grill; wraps/tofu: gas range. Refrigerate food. Live culture and samples are not edible.",
            "Switching and samples","Three dropdowns select strategy, species and medium. Strategy applies immediately; Start change executes a species switch. Scrollable advanced settings separate inoculum and sampling traits. Switch preserves an optional sample, recovers biomass, filters and recirculates at least 0.6 kg water for 30 s. Basic strains are internal; advanced strains need a physical matching 50 g sample, not natural generation. For a nutritious sample, cultivate the same base species with at least 10 kg organic medium for 1800 paid growth seconds, then request 30 s duplicant sampling that removes 50 g biomass. Harvest, maintenance and pauses do not count. Requests can be cancelled. Pending advanced inoculation can recover, clean and return to the target species' base strain; delivered samples remain stored. Dead or incomplete biomass becomes residue.",
            "Status and troubleshooting","Three default groups show actual culture/strategy/stage, health/progress and mode or remedy. World icons show interruptions; staged health notifications avoid per-tick spam. New stress causes persist; old unknown history is not guessed. If a 4 kg harvest cannot fit, paid 120 W maintenance holds health without biomass or oxygen, provided water, medium and temperature remain valid. Clearing output resumes cultivation. Solid outlet cells retain stock; clear base/sides or request ejection. Without CO2 baseline growth continues. Full oxygen storage pauses only bonus growth. Advanced details expose stock, actual 5-second rates and exposure. Real inventory meters and old overfull stock are retained."
        };
    }
}
