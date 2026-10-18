using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

namespace RateworksPrototype
{
    /// <summary>Three Cinemachine views and three small, readable lighting behaviors.</summary>
    [DefaultExecutionOrder(-10)]
    public sealed class FactoryPresentation : MonoBehaviour
    {
        public FactoryGame game;
        public CinemachineBrain brain;
        public CinemachineCamera firstPerson, thirdPerson, completion;
        public Light flashlight, statusLight;
        public Light[] bayLights;
        public Renderer[] bayLightLenses;
        public float buildLightIntensity = 32f, runLightIntensity = 40f;
        public bool RoomLightsOn { get; private set; } = true;
        public GameObject engineerAvatar;
        public Renderer statusLens;
        public bool IsCinematic { get; private set; }
        public bool FlashlightOn => flashlight != null && flashlight.enabled;
        public bool ThirdPerson { get; private set; }
        public string CameraLabel => IsCinematic || (game != null && game.ResultsVisible) ? "Contract camera" : ThirdPerson ? "Third person" : "First person";
        public string LightingState { get; private set; } = "Build";
        float cinematicEnd;
        Material statusMaterial;
        MaterialPropertyBlock bayLensProperties;
        Color workLensColor = new Color(.84f, .92f, 1f);

        void Awake()
        {
            // Detach only the runtime rendering camera; saved scene transforms stay intact.
            if (brain != null) { brain.transform.SetParent(null, true); brain.enabled = true; }
            if (statusLens != null)
            {
                statusMaterial = statusLens.material;
                statusMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                statusMaterial.EnableKeyword("_EMISSION");
            }
            ResetPresentation();
        }
        public void Begin() { ReturnToGameplay(); }
        public void ResetPresentation()
        {
            ThirdPerson = false;
            if (flashlight != null) flashlight.enabled = false;
            SetRoomLights(true);
            ReturnToGameplay();
        }
        public void ResetCameraTracking()
        {
            if (firstPerson != null) firstPerson.PreviousStateIsValid = false;
            if (thirdPerson != null) thirdPerson.PreviousStateIsValid = false;
        }
        public void ToggleCamera()
        {
            if (!game.Playing || game.ResultsVisible || IsCinematic) return;
            ThirdPerson = !ThirdPerson; SetPriorities();
        }
        public void ToggleFlashlight()
        {
            if (game.Playing && !game.ResultsVisible && flashlight != null)
                flashlight.enabled = !flashlight.enabled;
        }
        public void SetRoomLights(bool on)
        {
            RoomLightsOn = on;
            UpdateLighting();
        }
        public void ToggleRoomLights() { SetRoomLights(!RoomLightsOn); }
        public void OnRunChanged(bool running) { UpdateLighting(); }
        public void OnCompleted()
        {
            IsCinematic = true; cinematicEnd = Time.unscaledTime + 3.5f;
            SetPriorities(); UpdateLighting();
        }
        public void ReturnToGameplay() { IsCinematic = false; SetPriorities(); UpdateLighting(); }
        void SetPriorities()
        {
            if (firstPerson != null) firstPerson.Priority = ThirdPerson ? 10 : 20;
            if (thirdPerson != null) thirdPerson.Priority = ThirdPerson ? 20 : 10;
            if (completion != null) completion.Priority = IsCinematic || game.ResultsVisible ? 100 : 0;
            if (engineerAvatar != null) engineerAvatar.SetActive(ThirdPerson || IsCinematic);
        }
        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.vKey.wasPressedThisFrame) ToggleCamera();
                if (keyboard.fKey.wasPressedThisFrame) ToggleFlashlight();
            }
            // Hold the winning shot behind the result screen; blend back on Keep Optimizing.
            if (IsCinematic && Time.unscaledTime >= cinematicEnd) IsCinematic = false;
            UpdateLighting();
        }
        void UpdateLighting()
        {
            if (game == null) return;
            bool running = game.Running;
            bool won = game.ResultsVisible;
            bool warning = running && !won && (game.Paused ||
                (game.Simulation != null && game.Simulation.Time > 6 && game.Simulation.Rate < .99f));
            LightingState = won ? "Contract complete" : warning ? "Output warning" : running ? "Run" : "Build";
            Color workColor = running ? new Color(1f, .88f, .68f) : new Color(.84f, .92f, 1f);
            float blend = 1 - Mathf.Exp(-4 * Time.unscaledDeltaTime);
            workLensColor = Color.Lerp(workLensColor, workColor, blend);
            if (bayLights != null)
                foreach (var light in bayLights)
                    if (light != null && light != flashlight && light != statusLight)
                    {
                        // Manual room lighting is independent of the production test state.
                        light.enabled = RoomLightsOn;
                        light.color = Color.Lerp(light.color, workColor, blend);
                        light.intensity = Mathf.Lerp(light.intensity, running ? runLightIntensity : buildLightIntensity, blend);
                    }
            if (bayLightLenses != null)
            {
                if (bayLensProperties == null) bayLensProperties = new MaterialPropertyBlock();
                foreach (var lens in bayLightLenses)
                {
                    if (lens == null || lens == statusLens) continue;
                    bayLensProperties.Clear(); lens.GetPropertyBlock(bayLensProperties);
                    // Keep the luminous surface below white clipping so the same
                    // cool/warm transition is visible on the fixtures themselves.
                    // Actual room-light intensity and colour are unchanged.
                    bayLensProperties.SetColor("_BaseColor", RoomLightsOn ? workLensColor * .45f : new Color(.72f, .75f, .78f));
                    bayLensProperties.SetColor("_EmissionColor", RoomLightsOn ? workLensColor * .7f : Color.black);
                    lens.SetPropertyBlock(bayLensProperties);
                }
            }
            Color signal = won ? new Color(.15f, 1, .3f) : warning ? new Color(1, .42f, .04f) : new Color(.12f, .7f, 1);
            float pulse = warning ? 1.5f + 1.5f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 5)) : won ? 4 : .8f;
            if (statusLight != null) { statusLight.color = signal; statusLight.intensity = pulse; }
            if (statusMaterial != null)
            {
                statusMaterial.color = signal;
                statusMaterial.SetColor("_EmissionColor", signal * pulse);
            }
        }
        void OnDestroy() { if (statusMaterial != null) Destroy(statusMaterial); }
    }
}
