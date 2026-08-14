[简体中文](README.zh-CN.md)

# Unity Links Unity Package

## What It Does

This Editor-only Unity package receives local link requests from the
[Unity Asset Links Codex++ tweak](https://github.com/kpkhxlgy0/unity-links-codex) over a project-specific Windows named
pipe.

- Files under `Assets` open through Unity's asset APIs, including line and column information for code.
- Files under `ProjectSettings` open Unity's Project Settings window.
- Files under `Packages` open Unity's Package Manager.

## Requirements

- Unity 2022.3 or a compatible Tuanjie Editor version.
- Windows 10 or 11 for the current named-pipe integration.
- The matching Codex++ tweak enabled for the current Windows user.

## Install from Git

Add the tagged repository to the target project's `Packages/manifest.json`:

```json
"com.kpk.codex-unity-link": "https://github.com/kpkhxlgy0/unity-links-unity.git#v0.2.4"
```

You can also use **Window → Package Manager → Install package from git URL** with the same URL.

## Install from Disk

For local development, add the repository root as a `file:` dependency:

```json
"com.kpk.codex-unity-link": "file:../Tools/unity-links/unity-package"
```

The umbrella [unity-links](https://github.com/kpkhxlgy0/unity-links) repository keeps this package at
`unity-package/` as a pinned submodule.

## Compatibility

Component version `0.2.4` is tested with `unity-links-codex` version `0.2.2`. The package contains only Editor code and
does not add runtime player assemblies.

## Validation

Open the target Unity project, wait for package compilation, and confirm that the Console contains no
`KPK.CodexUnityLink.Editor` compilation errors. Then smoke one path from each supported root:

- an asset such as `Assets/Resources/UI_Splash.prefab`;
- `ProjectSettings/EditorBuildSettings.asset`;
- `Packages/manifest.json`.

## Release Process

1. Update the stable version in `package.json`.
2. Validate compilation in Unity and commit the version change to `master`.
3. Run the repository's `Release` workflow with the version without a leading `v`.
4. Review and manually publish the generated Draft Release.
5. Update the umbrella repository to the released component commit.

Never move or reuse a release tag.

## License

This project is licensed under the [MIT License](LICENSE).
