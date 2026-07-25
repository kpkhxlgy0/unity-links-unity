[English](README.md)

# Unity Links Unity Package

## 功能

这个仅限 Editor 的 Unity 包通过项目专用的 Windows Named Pipe，接收
[Unity Asset Links Codex++ tweak](https://github.com/kpkhxlgy0/unity-links-codex) 发来的本地链接请求。

- `Assets` 下的文件通过 Unity 资源 API 打开，代码链接保留行号和列号。
- `ProjectSettings` 下的文件打开 Unity Project Settings 窗口。
- `Packages` 下的文件打开 Unity Package Manager。

## 环境要求

- Unity 2022.3 或兼容的团结引擎版本。
- 当前 Named Pipe 集成需要 Windows 10 或 11。
- 当前 Windows 用户已启用配套 Codex++ tweak。

## 从 Git 安装

在目标项目的 `Packages/manifest.json` 中添加带标签的仓库：

```json
"com.kpk.codex-unity-link": "https://github.com/kpkhxlgy0/unity-links-unity.git#v0.2.0"
```

也可以在 **Window → Package Manager → Install package from git URL** 中输入相同 URL。

## 从本地目录安装

本地开发时，将仓库根目录添加为 `file:` 依赖：

```json
"com.kpk.codex-unity-link": "file:../Tools/unity-links/unity-package"
```

总入口仓库 [unity-links](https://github.com/kpkhxlgy0/unity-links) 会将本包固定在
`unity-package/` submodule。

## 兼容性

组件版本 `0.2.0` 与 `unity-links-codex` 版本 `0.2.0` 配套验证。本包只包含 Editor 代码，不会增加运行时
Player assembly。

## 验证

打开目标 Unity 项目，等待 package 编译完成，并确认 Console 中没有 `KPK.CodexUnityLink.Editor` 编译错误。
然后分别检查三类路径：

- `Assets/Resources/UI_Splash.prefab` 等资源；
- `ProjectSettings/EditorBuildSettings.asset`；
- `Packages/manifest.json`。

## 发布流程

1. 在 `package.json` 中更新稳定版本号。
2. 在 Unity 中验证编译，并将版本修改提交到 `master`。
3. 运行本仓库的 `Release` workflow，输入不带前导 `v` 的版本号。
4. 检查并手动发布生成的 Draft Release。
5. 将总入口仓库更新到已发布的组件 commit。

不要移动或复用发布标签。

## 开源协议

本项目使用 [MIT License](LICENSE)。
