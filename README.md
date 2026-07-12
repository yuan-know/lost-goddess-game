# 《失落的女神》Lost Goddess

> 一个老人追寻失落女神的旅程,最终发现自己寻找的不是神明,而是被遗忘的勇气。

类绣湖(Rusty Lake)2D 点击式叙事解谜游戏。

## 环境要求(所有程序必须一致)
- **Unity 版本:2022.3 LTS**(具体子版本以 `ProjectSettings/ProjectVersion.txt` 为准,不要私自升级)
- Git + Git LFS
- 插件:DOTween(补间动画)

## 首次拉取步骤
```bash
git lfs install          # 先装 LFS,否则拉下来的图片是文本指针
git clone <仓库地址>
# 用 Unity Hub 添加该工程,以指定 2022.3 版本打开
```

## 目录约定
- 我们自己的资源全部放在 `Assets/_Project/` 下
- 系统层代码 `Assets/_Project/Scripts/Core/` —— 程序员 A 维护
- 内容层代码 `Assets/_Project/Scripts/Rooms|Puzzles/` —— 程序员 B 维护
- 每个房间 = 一个独立 `.unity` 场景,放 `Scenes/<章节>/`

## 团队铁律(防止 Unity 冲突)
1. **一个场景/预制体同一时间只有一个人改**,开工前在看板认领。
2. 系统层公共文件只有程序员 A 改;需要新全局功能提给 A。
3. 每天开工 `git pull`,收工 `git push`,不要攒着。
4. 提交信息带前缀:`[core]` `[room]` `[puzzle]` `[art]` `[fix]`。
5. Flag / 物品 key 集中写在 `Data/Keys.cs`,禁止散落魔法字符串。

## 分工
| 程序员 | 负责 |
|--------|------|
| A(系统程序) | 框架、存档、场景切换、时间循环、交互/谜题基类、各 Manager |
| B(内容程序) | 房间、可交互物件、具体谜题、剧情触发、过场 |

详见 `docs/程序开发计划方案.md`。

## 章节进度
- [ ] 序章(进行中,10 天周期)
- [ ] 老年章
- [ ] 中年章
- [ ] 青年章
