# Kamen Rider W · Futo Desktop Pet

[简体中文](README.md) | English

A standalone Windows desktop pet featuring chibi pixel sprites in four Kamen Rider W forms. Regular dialogue uses a dark speech bubble; transformation lines use a split-color bubble matching the current skin. Double-click **WDesktopPet.exe** to run. No Python or internet connection is required. Requires Windows' built-in .NET Framework 4.x.

[Download the Windows executable](https://github.com/allsterisk/WDesktopPet/raw/refs/heads/main/WDesktopPet.exe)

![Four skins and their split-color speech bubbles](assets/skins-preview.png)

## Skins and speech bubbles

Right-click the character or system tray icon and select **切换皮肤** (Switch skin). The checked menu item indicates the current skin.

| Skin | Left side of transformation bubble | Right side of transformation bubble |
| --- | --- | --- |
| CycloneJoker | Green | Black |
| HeatMetal | Red | Silver |
| LunaTrigger | Yellow | Blue |
| FangJoker | White | Black |

Each skin includes eight animation frames. Switching skins updates the character, transformation line, menu title, and tray icon while preserving the current size, position, and resting state. Split-color bubbles appear only for the current form's transformation line, such as `Cyclone！Joker！`. Each Gaia Memory name is centered in its respective half, with space around the divider and dark or light text chosen to suit the background. Regular interaction, rest, and wake-up lines use a dark bubble without a divider. The selected skin is restored when the app is restarted.

## Controls

- **Drag the character:** Move it anywhere on the desktop. Its position is retained when released.
- **Click:** Trigger jumping, pointing, or cheering animations and display a random line.
- **Right-click the character or tray icon:** Switch between the four skins, pause or enable free roaming, rest, choose from three sizes, reset, or exit.
- **Double-click the tray icon:** Wake the character and move it to the bottom-right corner of the screen containing the mouse pointer.

The pet stays on top by default, has a transparent background, and does not appear on the taskbar. While roaming, it stays at its current height and turns around at the edges of the current monitor's work area. Negative coordinates on secondary monitors are supported, and the pet is automatically brought back into view after display changes. It remains in a crouching pose while resting. Only one instance can run at a time.

Position, size, roaming preferences, and the selected skin are saved in `%LOCALAPPDATA%\WDesktopPet\settings.txt`.

The app does not enable automatic startup or access the network. Exit through the right-click menu.

## License

The project's original program code, build scripts, and documentation are licensed under the [MIT License](LICENSE). You may use, modify, and distribute them provided that the license and copyright notice are retained.

The `assets/` directory and character sprites embedded in the executable are excluded from the MIT license. Kamen Rider W characters, names, logos, and other third-party rights belong to their respective rights holders. This project does not grant any rights to those materials.
