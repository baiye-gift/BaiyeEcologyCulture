# 美术来源与编译

2026-10-01 使用 Codex 内置 `image_gen.imagegen`，透明背景，原创缺氧风格建筑与物品。原图保持不变。

- `source/culture-housing.png`：生成结果 `exec-f3e52c61-c143-4235-9815-1cf04fd7d18c.png`。提示要求 4×4 工业生态培养仓、奶油色/青绿金属外壳、空玻璃培养窗口、机械泵、两侧计量柱、四个底部接口、落地支撑；无文字、无环境背景。
- `source/culture-items.png`：生成结果 `exec-f87ccb03-ff5b-4713-9e7b-737e8222e3cb.png`。提示要求透明 3×3 均匀布局，从左到右、从上到下依次螺旋藻泥、盐生藻叶、藻泥粥、藻麦饼、盐藻脆片、菌菇藻卷、藻酱豆腐、封装样本、不可食残渣；卡通厚轮廓、统一缺氧风格，无文字、边框或背景。
- `source/pump-rotor.png`：复用同一作者已生成的聚变模组独立机械零件画稿 `FusionPower/art/source/motion-parts/baiye_isotope_separator.png`。
- `tools/build-anims.py` 保留原画，缩放/裁切输出游戏图标及物品，生成独立玻璃层、51 档浓度/料位、机械运动和气泡轨迹，导出 SCML 后由 Kanimal 转换为原生 KAnim。物品边界按 alpha >64 确认，排除透明留白中的微弱散点。没有使用游戏原版建筑贴图。
- `preview/` 为动画源的离线组合预览，并非游戏内截图。角色采样使用原版研究站交互动画，通过游戏自身资源引用，不分发原版资源。

## 0.1.2 清洁培养仓与阶段动画

- 使用内置 `image_gen.imagegen` 的编辑模式，输入既有 `source/culture-housing.png`，要求保持透明背景。新结果保存为 `source/culture-housing-v2.png`，原图保留。生成结果标识：`exec-fe3f3381-8e0b-4213-9766-c447a7d1f398.png`。
- `tools/build-anims.py` 为本项目原生可编辑资源编译管线：透明原画 + 自绘几何机械/液流/均匀浓度层 → SCML → Kanimal KAnim。新机械为独立搅拌轴、三组旋转叶片、采收阀、滑动扣、健康指示灯、循环液流及清洗波；中央没有固定藻斑。仅重编译建筑，不改九套物品图。删去四个已不使用的旧派生机械/气泡切片，保留原始画稿。
- 运行、接种、保种、采收、回收、清洗、启动及停机都有独立动画。浓度和两侧料位仍由游戏实际库存驱动。清洗波仅是流动表现，不增加液体质量。图标 128×128，建筑图集 2048×1024。
- `preview/culture-motion.gif`、各阶段 GIF、`culture-stages.png` 和 `culture-states.png` 为固定示例库存的源图合成，未使用游戏截图；原生渲染、染色、遮挡及状态触发仍需实机检查。

完整图像编辑提示词：

> Use case: precise-object-edit. Asset type: original 2D game sprite for a 4x4 industrial algae culture chamber, destined for layered native animation. The attached image is the edit target. Preserve its front-facing composition, cream metal and dark teal housings, brass pipes, bold hand-inked cartoon outlines, feet on one baseline, proportions, side gauges and left circular pump housing. Improve the central culture viewport: remove every white cloudy/foggy artifact, speckle, mold patch, algae spot and baked content. Make the central opening a perfectly clean, smooth, empty dark blue-teal viewing cavity inside the existing rounded rectangular metal rim. No particles, no plants, no liquid level, no impeller inside this cavity: those will be separate animated layers. The cavity must be large, regular and visually clean, with only restrained edge glass highlights, not a white cloud. Keep the side level gauges empty, clean and dark. The left pump's round inner cavity should also be empty and clean for a separate rotor sprite. Reduce the four decorative bottom connections to two clear brass connections, one on each side of the center, preserving feet and chassis. Exterior must be clean maintained industrial hardware, not weathered/dirty. True transparent background outside the complete isolated machine, full machine visible with margins, no floor, no environment, no labels, no text, no watermark. No neon glow, no green film, no contamination. Match Oxygen Not Included-like thick outlines, readable at small size. A single finished housing sprite, not a montage or sprite sheet.
