using UnityEngine;

namespace Metin2Dev.Gameplay
{
    /// <summary>
    /// Drives states generated directly from each race folder's original motlist.txt.
    /// State durations and looping are owned by the imported FBX clips/controller.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MobAnimationRuntime : MonoBehaviour
    {
        static readonly int WaitState = Animator.StringToHash("Wait");
        static readonly int WalkState = Animator.StringToHash("Walk");
        static readonly int RunState = Animator.StringToHash("Run");
        static readonly int AttackState = Animator.StringToHash("Attack");
        static readonly int HitState = Animator.StringToHash("Hit");
        static readonly int DeadState = Animator.StringToHash("Dead");

        Animator animator;
        int currentState;
        int actionSequence;
        bool dead;

        void Awake()
        {
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
            if (animator == null) return;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            Play(WaitState, 0f, true);
        }

        public void SetMoveAmount(float amount)
        {
            if (dead || animator == null) return;
            int state = WaitState;
            if (amount > 0.01f)
            {
                int preferred = amount > 1.25f ? RunState : WalkState;
                int fallback = preferred == RunState ? WalkState : RunState;
                state = animator.HasState(0, preferred) ? preferred : fallback;
            }
            Play(state, 0.12f);
        }

        public void PlayAttack()
        {
            if (!dead) Play(Variation("Attack", AttackState), 0.06f, true);
        }

        public void PlayHit()
        {
            if (!dead) Play(Variation("Hit", HitState), 0.04f, true);
        }

        public void PlayDead()
        {
            dead = true;
            Play(Variation("Dead", DeadState), 0.08f, true);
        }

        public void Revive()
        {
            dead = false;
            Play(WaitState, 0f, true);
        }

        void Play(int state, float fade, bool restart = false)
        {
            if (animator == null || animator.runtimeAnimatorController == null || !animator.HasState(0, state)) return;
            if (!restart && currentState == state) return;
            currentState = state;
            if (fade <= 0f) animator.Play(state, 0, 0f);
            else animator.CrossFade(state, fade, 0, 0f);
        }

        int Variation(string baseName, int fallback)
        {
            if (animator == null) return fallback;
            int start = actionSequence++;
            for (int offset = 0; offset < 8; offset++)
            {
                int number = (start + offset) % 8;
                string name = number == 0 ? baseName : baseName + number;
                int state = Animator.StringToHash(name);
                if (animator.HasState(0, state)) return state;
            }
            return fallback;
        }
    }
}
