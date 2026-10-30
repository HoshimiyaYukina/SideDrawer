# 上传到 GitHub 的操作步骤

仓库已经在本地初始化并完成首次提交：

```
C:\Users\Hoshimiya\SideDrawer\oss
分支：main
提交：bc7cad2  Initial commit: 侧边抽屉 SideDrawer — WPF 桌面侧边抽屉（液态玻璃）
文件：19 个（源码 + README + LICENSE + .gitignore + docs）
```

Git 已安装：`C:\Program Files\Git\cmd\git.exe`（2.56.0）

---

## 方式 A：命令行推送（推荐，2 条命令）

### 1. 在 GitHub 网页上新建仓库

打开 https://github.com/new

- **Repository name**：`SideDrawer`（或 `side-drawer`）
- **Description**：`Windows 桌面侧边抽屉 · WPF 液态玻璃 · 单文件源码构建`
- **Public**（开源就是要公开）
- ⚠️ **不要**勾选 "Add a README file"、"Add .gitignore"、"Choose a license"（本地已经有了，勾了会冲突）

点 **Create repository**。

### 2. 推送

把 `<你的用户名>` 换成你的 GitHub 用户名，然后在 PowerShell 里执行：

```powershell
$env:Path += ';C:\Program Files\Git\cmd'
cd C:\Users\Hoshimiya\SideDrawer\oss

git remote add origin https://github.com/<你的用户名>/SideDrawer.git
git push -u origin main
```

第一次推送会弹出登录窗口（Git Credential Manager），浏览器里授权一次即可。

---

## 方式 B：网页上传（不用命令行）

1. 新建仓库（同上，不勾任何初始化选项）
2. 进入仓库 → 点 **uploading an existing file**
3. 把 `SideDrawer-opensource.zip` 解压后，**把这些文件拖进去**：
   - 所有 `.cs` 文件
   - `README.md`、`LICENSE`、`.gitignore`、`app.ico`、`app.manifest`、`build.ps1`
   - `docs/` 整个文件夹
4. 提交

> 注意：`.gitignore` 是以点开头的隐藏文件，网页拖拽可能漏掉它，可以在仓库里点 "Add file → Create new file"，文件名叫 `.gitignore` 再把内容粘进去。

---

## 推送后建议做的事

1. **确认没有敏感文件**：仓库页面上不应该出现 `drawer.json`、`log.txt`、`*.bak`、`idle.jpg`
2. **加 Topics**（仓库右上角齿轮）：`wpf` `csharp` `windows` `desktop` `launcher` `glassmorphism`
3. **仓库描述**填上：`Windows 桌面侧边抽屉 · WPF 液态玻璃 · 无第三方依赖，单文件 csc 构建`
4. **可选**：在 README 里把 `docs/screenshot.png` 换成你自己满意的截图

---

## 如果后续想更新

```powershell
$env:Path += ';C:\Program Files\Git\cmd'
cd C:\Users\Hoshimiya\SideDrawer\oss
git add -A
git commit -m "feat: 描述这次改动"
git push
```

---

## 说明：为什么我没直接帮你 push

推送需要 GitHub 账号授权（Token 或浏览器登录）。我没有你的凭据，也不应该持有，所以：

- 仓库、提交、`.gitignore`、敏感信息清理 **都已在这台机器上准备好**
- 你只需要做「建仓库 + `git push`」这两步

如果你愿意用 Personal Access Token 让我代推，也可以（Token 建议用完立刻在 GitHub 设置里删除），但**更推荐你自己执行上面两条命令**，凭据不经过我。
