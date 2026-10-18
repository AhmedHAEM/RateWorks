using UnityEngine;
using UnityEngine.InputSystem;

namespace RateworksPrototype
{
    /// <summary>A nearby, visible switch controls ceiling lights without changing factory operation.</summary>
    public sealed class RoomLightSwitch : MonoBehaviour
    {
        public FactoryGame game;
        public FactoryPresentation presentation;
        public Transform interactionPoint, lever;
        public Renderer indicator;
        public float interactionDistance = 2.7f;

        MaterialPropertyBlock indicatorProperties;
        Transform cachedLever;
        Quaternion leverRestRotation;

        public bool CanInteract
        {
            get
            {
                if (!isActiveAndEnabled || game == null || presentation == null || game.engineer == null || !game.Playing ||
                    game.ResultsVisible || presentation.IsCinematic || game.engineer.CursorMode) return false;
                var engineer = game.engineer;
                Transform eye = engineer.lookTarget != null ? engineer.lookTarget :
                    engineer.view != null ? engineer.view.transform : engineer.transform;
                Vector3 point = interactionPoint != null ? interactionPoint.position : transform.position;
                Vector3 toSwitch = point - eye.position;
                float distance = toSwitch.magnitude;
                if (distance < .001f || distance > interactionDistance) return false;
                Vector3 direction = toSwitch / distance;
                if (Vector3.Dot(eye.forward, direction) < .6f) return false;
                foreach (var hit in Physics.RaycastAll(eye.position, direction, distance, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.transform.IsChildOf(engineer.transform) || hit.collider.transform.IsChildOf(transform)) continue;
                    return false;
                }
                return true;
            }
        }

        public string Prompt => CanInteract ? "E — Turn ceiling lights " + (presentation.RoomLightsOn ? "OFF" : "ON") : "";

        public bool TryInteract()
        {
            if (!CanInteract) return false;
            presentation.ToggleRoomLights();
            UpdateVisuals(false);
            return true;
        }

        void Start() { UpdateVisuals(true); }
        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame) TryInteract();
            UpdateVisuals(false);
        }

        void UpdateVisuals(bool immediate)
        {
            if (presentation == null) return;
            bool on = presentation.RoomLightsOn;
            if (lever != null && lever != transform && lever.IsChildOf(transform))
            {
                if (cachedLever != lever) { cachedLever = lever; leverRestRotation = lever.localRotation; }
                Quaternion target = leverRestRotation * Quaternion.Euler(on ? -18f : 18f, 0, 0);
                lever.localRotation = immediate ? target : Quaternion.Slerp(lever.localRotation, target, 1 - Mathf.Exp(-12 * Time.unscaledDeltaTime));
            }
            if (indicator != null)
            {
                if (indicatorProperties == null) indicatorProperties = new MaterialPropertyBlock();
                indicatorProperties.Clear(); indicator.GetPropertyBlock(indicatorProperties);
                Color color = on ? new Color(.20f, .90f, .36f) : new Color(.12f, .14f, .15f);
                indicatorProperties.SetColor("_BaseColor", color);
                indicatorProperties.SetColor("_EmissionColor", on ? color * 1.6f : Color.black);
                indicator.SetPropertyBlock(indicatorProperties);
            }
        }
    }
}
