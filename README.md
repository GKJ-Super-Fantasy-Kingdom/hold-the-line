# Hold the Line

Before battle, a kingdom with a standing Castle Wall can take one wager for the coming night. **Stake 1 coin to win 3 coins total**, a net gain of 2. Pass or dismiss the offer to keep your coin. The card opens with Pass selected.

The wager starts when you accept. Any hit that removes wall HP forfeits the stake, even if the wall is repaired before dawn. A fully shielded hit keeps the wager alive. Survive the night with the wall unharmed and the coins go straight into your purse before the tavern checkpoint.

Use the mouse or move between the two buttons with the controller and press the displayed Select control. Back passes for free. The first damaging hit triggers a loss alert. The following morning shows the result before a fresh offer. The offer uses the game's font, buttons, controller glyphs and UI scale.

## Development

Version 0.1.2 is a local playtest build. Use a separate BepInEx 5 profile and a copied save. A mod profile alone does not isolate Unity's save directory.

Build with the .NET SDK, Python and Pillow. Set the environment variables `GameDir` to the game installation and `ProfileDir` to the local BepInEx profile. Run `python package.py` to build and verify `dist/HoldTheLine-0.1.2.zip`. Import that ZIP into the test profile, or place its `plugins/HoldTheLine.dll` under the profile's `BepInEx/plugins` directory.

Run `dotnet run --project tests/WagerChecks.csproj -c Release -- "<copied game.data>"` for the wager and checkpoint checks. With the DLL at `BepInEx/plugins/HoldTheLine.dll`, run `pwsh -File tests/VerifyRuntime.ps1 -GameDir "<game>" -ProfileDir "<profile>" -CopiedSave "<copied game.data>"` for installed-assembly and packed-plugin checks.

The live playtest should cover manual and automatic tavern entry, a same-night repair, checkpoint reload, and controller focus, glyphs and text fit at 1× and 1.5×. See [CONTRIBUTING.md](CONTRIBUTING.md) for commit conventions.

## License

Original mod code is licensed under the [MIT License](LICENSE). Super Fantasy Kingdom and its assets belong to their respective owners.
