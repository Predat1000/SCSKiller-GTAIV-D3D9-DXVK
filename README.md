# SCSKiller GTA IV D3D9 / DXVK support

Experimental GTA IV support for **SCSKiller 1.3.0**.

This patch adds a dedicated **RAGE D3D9 / GTA IV FXC** reader and an x86 D3D9 warm-up helper so SCSKiller can warm the shaders used by **Grand Theft Auto IV: The Complete Edition** when the game runs through **DXVK**.

## Tested configuration

- SCSKiller 1.3.0
- Grand Theft Auto IV: The Complete Edition (Steam, app 12210)
- DXVK 2.6.2 x86 exposed as `GTAIV\vulkan.dll`
- NVIDIA GeForce RTX 3080
- NVIDIA driver 616.64
- Windows build 26100

On the test installation SCSKiller detected:

- 715 `.fxc` files
- 1,170 unique vertex shaders
- 1,505 unique pixel shaders
- 2,675 unique D3D9 Shader Model 3.0 shaders total
- 2,675 / 2,675 warmed through DXVK
- 0 failures

After a successful run SCSKiller reports GTA IV as **Warmed** for the current driver.

## What the patch changes

- Adds `Rage4Reader` for GTA IV FXC files.
- Detects GTA IV as `RAGE D3D9 / GTA IV FXC` instead of the generic unsupported carved path.
- Marks the game Ready on the currently tested NVIDIA path when DXVK is present.
- Adds an x86 helper named `GTAIV.exe` that loads the game's DXVK DLL by absolute path.
- Creates the D3D9 vertex/pixel shader objects through DXVK so Vulkan/NVIDIA shader cache entries can be populated under the GTA IV executable name.
- Integrates the warm-up into the normal SCSKiller `compile` queue.
- Stores the warm state, driver, game build, FXC stamp and shader count so the UI can show `Warmed` and invalidate the result after relevant changes.

## Important limitation

This version warms the **individual D3D9 shaders**. It does **not yet replay every GTA IV FXC Technique/Pass combination as a complete graphics pipeline**. This can reduce shader-related first-use work, but some first-time pipeline/scene stutter may remain.

That is the next logical improvement.

## Requirements

- GTA IV must already be configured to use the DXVK path expected by this patch (`GTAIV\vulkan.dll`).
- SCSKiller 1.3.0 must already be installed for the automatic install script.
- Git.
- .NET 10 SDK for SCSKiller Core.
- .NET 9 SDK for the x86 helper.

## Install

Open PowerShell in this repository and run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\install.ps1
```

The installer:

1. verifies SCSKiller 1.3.0;
2. clones the upstream SCSKiller v1.3.0 source into a local build folder;
3. applies `SCSKiller-GTAIV-D3D9-DXVK.patch`;
4. builds `SCSKiller.Core.dll` as version 1.3.0.0;
5. publishes the GTA IV helper as self-contained x86;
6. backs up the installed SCSKiller Core DLLs;
7. installs the patched Core into the GUI and CLI folders;
8. installs the helper at `%LOCALAPPDATA%\SCSKiller\tools\GTAIV.exe`;
9. starts SCSKiller.

Then refresh SCSKiller. GTA IV should be shown as Ready. Compile it normally from SCSKiller.

## Manual source patch

The patch is based on upstream SCSKiller **v1.3.0 / commit `f8c8b43`**.

```powershell
git clone https://github.com/BlueHeisenberg/SCSKiller.git
cd SCSKiller
git checkout v1.3.0
git apply ..\SCSKiller-GTAIV-D3D9-DXVK.patch
```

The helper source is included in `helper-source/`.

## Safety / scope

- The patch does not modify GTA IV game files.
- It does not install SCSKiller's recording/injection proxy into GTA IV.
- The helper reads the loose `.fxc` files and loads the DXVK DLL already present in the game directory.
- Backups of the original SCSKiller Core DLLs are created before replacement.
- This is experimental third-party support and is not an official SCSKiller feature unless accepted upstream.

## Compatibility

The installer intentionally refuses to patch a SCSKiller installation whose file version is not **1.3.0.0**. A future SCSKiller update may require rebasing this patch.

## License and upstream

SCSKiller is licensed under the **GNU GPL v3**. This repository preserves the upstream license and license exception files. The patch is distributed under the same applicable GPLv3 terms.

Upstream project: BlueHeisenberg/SCSKiller

## Credits

- SCSKiller: BlueHeisenberg and contributors.
- GTA IV support patch/testing: Predat1000.
- DXVK: DXVK project and contributors.
