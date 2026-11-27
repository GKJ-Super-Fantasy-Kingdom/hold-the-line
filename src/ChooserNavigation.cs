using SuperFantasyKingdom;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HoldTheLine
{
    internal sealed class ChooserNavigation : MonoBehaviour
    {
        private Transform chooser;
        private Button fallback;
        private InputAction move, submit, point, click;
        private bool keyboard;

        internal void Initialize(Transform scope, Button defaultButton)
        {
            chooser = scope;
            fallback = defaultButton;
            GamepadManager.Instance.SetGameObjectToSelect(fallback.gameObject, true);
            if (EventSystem.current?.currentInputModule is InputSystemUIInputModule input)
            {
                move = input.move?.action;
                submit = input.submit?.action;
                point = input.point?.action;
                click = input.leftClick?.action;
                if (move != null) move.performed += Navigate;
                if (submit != null) submit.performed += Navigate;
                if (point != null) point.performed += Pointer;
                if (click != null) click.performed += Pointer;
            }
        }

        private void Navigate(InputAction.CallbackContext input)
        {
            if (!(input.control.device is Keyboard) ||
                (input.action == move && input.ReadValue<Vector2>() == Vector2.zero)) return;
            keyboard = true;
            RestoreFocus();
        }

        private void Pointer(InputAction.CallbackContext input)
        {
            if (input.control.device is Mouse) keyboard = false;
        }

        private void LateUpdate()
        {
            if (PauseMenuManger.Instance.InMenu()) return;
            if (keyboard || GamepadManager.Instance.IsGamepadInUse()) RestoreFocus();
            else
            {
                var events = EventSystem.current;
                if (Owns(events?.currentSelectedGameObject)) events.SetSelectedGameObject(null);
            }
        }

        private void RestoreFocus()
        {
            var events = EventSystem.current;
            if (events == null || PauseMenuManger.Instance.InMenu()) return;
            var selected = events.currentSelectedGameObject;
            if (selected == null || (Owns(selected) && selected.GetComponent<Button>()?.IsInteractable() == false))
                events.SetSelectedGameObject(fallback.gameObject);
        }

        private bool Owns(GameObject target) => target != null && target.transform.IsChildOf(chooser);

        private void OnDisable()
        {
            if (move != null) move.performed -= Navigate;
            if (submit != null) submit.performed -= Navigate;
            if (point != null) point.performed -= Pointer;
            if (click != null) click.performed -= Pointer;
        }
    }
}
