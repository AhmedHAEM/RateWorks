using UnityEngine;

namespace RateworksPrototype
{
    /// <summary>Visual animation only; EngineerController remains the sole movement authority.</summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class EngineerAnimation : MonoBehaviour
    {
        public EngineerController engineer;
        public Animator animator;
        [Min(.1f)] public float referenceSpeed = 4.5f;
        [Min(0)] public float blendTime = .12f;
        public Vector2 Motion { get; private set; }

        static readonly int Horizontal = Animator.StringToHash("Horizontal");
        static readonly int Forward = Animator.StringToHash("Forward");
        Vector3 previousPosition;
        bool sampled;

        void OnEnable()
        {
            if (engineer == null) engineer = GetComponent<EngineerController>();
            sampled = false; Motion = Vector2.zero;
            if (animator != null) animator.applyRootMotion = false;
        }

        void Update()
        {
            if (engineer == null) return;
            Vector3 current = engineer.transform.position;
            Vector3 displacement = current - previousPosition;
            previousPosition = current;
            displacement.y = 0;
            float dt = Time.deltaTime;
            var game = engineer.game;
            bool permitted = sampled && dt > .0001f && engineer.enabled && !engineer.CursorMode &&
                game != null && game.Playing && !game.ResultsVisible &&
                (game.presentation == null || !game.presentation.IsCinematic);
            sampled = true;

            // ResetPosition and other teleports must not produce a one-frame walking burst.
            float plausibleDistance = referenceSpeed * dt * 1.8f + .05f;
            if (permitted && displacement.magnitude <= plausibleDistance)
            {
                Vector3 local = engineer.transform.InverseTransformDirection(displacement / dt);
                Motion = Vector2.ClampMagnitude(new Vector2(local.x, local.z) / referenceSpeed, 1);
                if (Motion.sqrMagnitude < .0004f) Motion = Vector2.zero;
            }
            else Motion = Vector2.zero;

            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;
            animator.SetFloat(Horizontal, Motion.x, blendTime, dt);
            animator.SetFloat(Forward, Motion.y, blendTime, dt);
        }
    }
}
