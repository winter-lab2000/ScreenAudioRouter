# 上传 GitHub：需要你提供什么

我可以在本机完成：整理仓库、初始化 git、commit、创建 GitHub 仓库并 push、上传 Release 附件。

当前机器 **未安装 git / gh**，请先满足下面任一路径。

---

## 你需要提供的信息（发在对话里即可）

| 项目 | 示例 | 必须？ |
|------|------|--------|
| GitHub 用户名 | `winte` | 必须（建仓库用） |
| 仓库名 | `ScreenAudioRouter` | 必须 |
| 可见性 | `public` 或 `private` | 必须 |
| 仓库描述 | `按显示器切换 Windows 默认音频设备` | 建议 |
| 认证方式 | 见下 | 必须 |

### 认证方式（三选一）

**方式 1 — 安装 Git + GitHub CLI，你自己登录（推荐）**

```powershell
winget install Git.Git
winget install GitHub.cli
# 重开终端后
gh auth login
# 选 GitHub.com → HTTPS → 浏览器登录
```

之后告诉我：「已 gh auth login，仓库名 XXX，public」，我来执行  
`git init / commit / gh repo create / push`。

**方式 2 — 只装 Git，用 Personal Access Token（PAT）**

1. GitHub → Settings → Developer settings → Personal access tokens → Fine-grained 或 Classic  
2. Classic 勾选 `repo`（私有仓库也要 `repo`）  
3. 把 token 发给我（或写入环境变量后只告诉我变量名）  
4. 我用 token 做 HTTPS push  

> Token 等同密码，用完建议在 GitHub 上 Revoke，或改用方式 1。

**方式 3 — 我只打包，你自己上传**

你本机已有 git / 网页上传：

```powershell
# 我这边准备好后，你执行
cd C:\Users\winte\Desktop\mimo\ScreenAudioRouter
git init
git add .
git commit -m "feat: Screen Audio Router"
git remote add origin https://github.com/<用户名>/ScreenAudioRouter.git
git push -u origin main
```

Release：网页打开仓库 → Releases → Draft a new release → 上传  
`dist\ScreenAudioRouter-v0.1.0-win-x64.zip`。

---

## 我会帮你做的事（你提供上述信息后）

1. 确认 `.gitignore`、`LICENSE`、`README.md`、源码结构  
2. `git init` + 首次 commit（不含 bin/obj/dist）  
3. `gh repo create <user>/<name> --public --source . --push`  
4. 执行 `build\publish.ps1` + `build\pack-release.ps1` 生成 zip  
5. `gh release create v0.1.0 dist\*.zip --title "v0.1.0" --notes "..."`  
6. 告诉你仓库 URL 与 Release URL  

---

## 建议的仓库设置

- 默认分支：`main`  
- About 标签：`windows` `audio` `multi-monitor` `wpf` `tray`  
- 不要把 `settings.json` / 日志提交进仓库  
- Release 标签格式：`v0.1.0`  
- 代码只进 git 树；可执行 zip 只进 Release  

---

## 最小回复模板（你可直接复制填写）

```
用户名：
仓库名：ScreenAudioRouter
可见性：public
认证方式：gh 已登录 / 我发 PAT / 我自己 push
```
