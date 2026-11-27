using System.Linq;
using SuperFantasyKingdom;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.UI;

namespace HoldTheLine
{
    internal sealed class StakeShortcutHint
    {
        private readonly GameObject hint;
        private readonly TMP_Text label;
        private readonly Image glyph;
        private InputControl displayedControl;
        private int displayedIndex = -1;
        private string displayedPath;

        internal StakeShortcutHint(GameObject hint, TMP_Text label, Image glyph)
        {
            this.hint = hint;
            this.label = label;
            this.glyph = glyph;
            hint.SetActive(false);
        }

        internal bool Refresh(InputAction action, PlayerInput player, GamepadManager pad, bool available)
        {
            string scheme = player?.currentControlScheme;
            if (action != null && action.enabled && !string.IsNullOrEmpty(scheme))
            {
                var schemeMask = InputBinding.MaskByGroup(scheme);
                foreach (var control in action.controls)
                {
                    if (!player.devices.Contains(control.device)) continue;
                    int index = action.GetBindingIndexForControl(control);
                    if (index < 0) continue;
                    var binding = action.bindings[index];
                    if (!string.IsNullOrEmpty(binding.groups) && !schemeMask.Matches(binding)) continue;
                    string path = binding.effectivePath;
                    if (displayedControl != control || displayedIndex != index || displayedPath != path)
                    {
                        displayedControl = control;
                        displayedIndex = index;
                        displayedPath = path;
                        bool nativeGamepad = control.device is DualShockGamepad ||
                            (control.device is Gamepad && (control.device.layout == "Gamepad" ||
                                InputSystem.IsFirstLayoutBasedOnSecond(control.device.layout, "XInputController")));
                        glyph.sprite = nativeGamepad ? pad.GetButtonPrompt(path) : null;
                        glyph.enabled = glyph.sprite != null;
                        label.text = glyph.enabled ? "" : $"[{action.GetBindingDisplayString(index)}]";
                    }
                    Color tint = available ? Color.white : new Color(.62f, .59f, .55f);
                    glyph.color = tint;
                    label.color = tint;
                    hint.SetActive(true);
                    return true;
                }
            }
            displayedControl = null;
            hint.SetActive(false);
            return false;
        }
    }
}
