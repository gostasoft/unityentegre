using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Metin2Dev.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class Metin2PlayerController : MonoBehaviour
    {
        const float SourceUnitsPerMetre = 100f;

        Metin2RaceMotionSet motionSet;
        Animator animator;
        CharacterController characterController;
        Camera gameplayCamera;
        Vector3 destination;
        bool hasDestination;
        string combatMode;
        Metin2MotionRecord currentMotion;
        float motionStartedAt;
        bool comboQueued;
        int comboIndex;
        int nextMotionEvent;
        readonly List<Metin2MotionRecord> skills = new List<Metin2MotionRecord>();

        public string CharacterName { get; private set; }

        public void Initialize(Metin2RaceMotionSet set, Animator visualAnimator, Camera camera, string characterName)
        {
            motionSet = set;
            animator = visualAnimator;
            gameplayCamera = camera;
            CharacterName = characterName;
            characterController = GetComponent<CharacterController>();
            characterController.radius = 0.35f;
            characterController.height = 1.75f;
            characterController.center = new Vector3(0f, 0.875f, 0f);
            combatMode = DefaultCombatMode();
            skills.Clear();
            skills.AddRange(motionSet.motions
                .Where(item => item != null && item.mode == "skill" && !item.name.EndsWith("_2") &&
                               !item.name.EndsWith("_3") && !item.name.EndsWith("_4") &&
                               !item.name.StartsWith("guild_", StringComparison.OrdinalIgnoreCase) &&
                               item.name != "use_me" && item.name != "use_target")
                .GroupBy(item => item.name, StringComparer.OrdinalIgnoreCase).Select(group => group.First()));
            PlayLoop("general", "wait", 0f);
        }

        void Update()
        {
            if (motionSet == null || animator == null) return;
            ReadInput();
            UpdateAction();
            UpdateMovement();
        }

        void ReadInput()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                Ray ray = gameplayCamera.ScreenPointToRay(mouse.position.ReadValue());
                if (Physics.Raycast(ray, out RaycastHit hit, 10000f, ~0, QueryTriggerInteraction.Ignore))
                {
                    destination = hit.point;
                    hasDestination = true;
                }
            }

            if (keyboard == null) return;
            if (keyboard.spaceKey.wasPressedThisFrame) QueueAttack();
            if (keyboard.rKey.wasPressedThisFrame) CycleCombatMode();
            Key[] skillKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6 };
            for (int i = 0; i < skillKeys.Length && i < skills.Count; i++)
                if (keyboard[skillKeys[i]].wasPressedThisFrame) PlayAction(skills[i]);
        }

        void UpdateMovement()
        {
            if (!hasDestination || IsActionPlaying()) return;
            Vector3 offset = destination - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude < 0.04f)
            {
                hasDestination = false;
                PlayLoop(combatMode, "wait", 0.10f);
                return;
            }

            Metin2MotionRecord run = Find(combatMode, "run") ?? Find("general", "run");
            float speed = MotionSpeed(run, 4.5f);
            Quaternion targetRotation = Quaternion.LookRotation(offset.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 720f * Time.deltaTime);
            Vector3 velocity = transform.forward * speed;
            if (!characterController.isGrounded) velocity.y = -12f;
            characterController.Move(velocity * Time.deltaTime);
            if (currentMotion != run) PlayLoop(run, 0.10f);
        }

        void UpdateAction()
        {
            if (!IsActionPlaying()) return;
            float elapsed = Time.time - motionStartedAt;
            while (nextMotionEvent < currentMotion.events.Count && currentMotion.events[nextMotionEvent].startTime <= elapsed)
                FireMotionEvent(currentMotion.events[nextMotionEvent++]);
            if (comboQueued && currentMotion.inputLimitTime >= 0f && elapsed >= currentMotion.inputLimitTime)
                comboQueued = false;
            if (comboQueued && elapsed >= ComboLinkTime(currentMotion))
            {
                comboQueued = false;
                comboIndex = comboIndex % ComboNames().Length;
                Metin2MotionRecord next = Find(combatMode, ComboNames()[comboIndex++]);
                if (next != null) PlayAction(next);
                return;
            }
            if (elapsed >= Mathf.Max(0.01f, currentMotion.duration))
            {
                currentMotion = null;
                comboIndex = 0;
                PlayLoop(combatMode, hasDestination ? "run" : "wait", 0.08f);
            }
        }

        void QueueAttack()
        {
            if (!IsActionPlaying())
            {
                comboIndex = 1;
                Metin2MotionRecord first = Find(combatMode, ComboNames()[0]) ?? Find(combatMode, "attack") ?? Find("general", "attack");
                if (first != null) PlayAction(first);
                return;
            }
            float elapsed = Time.time - motionStartedAt;
            float start = currentMotion.preInputTime >= 0f ? currentMotion.preInputTime : currentMotion.duration * 0.2f;
            float limit = currentMotion.inputLimitTime >= 0f ? currentMotion.inputLimitTime : currentMotion.duration * 0.8f;
            if (elapsed >= start && elapsed <= limit) comboQueued = true;
        }

        void PlayAction(Metin2MotionRecord motion)
        {
            if (motion == null || motion.clip == null) return;
            hasDestination = false;
            currentMotion = motion;
            motionStartedAt = Time.time;
            nextMotionEvent = 0;
            comboQueued = false;
            animator.CrossFade(StateName(motion), 0.04f, 0, 0f);
        }

        void PlayLoop(string mode, string name, float fade)
        {
            PlayLoop(Find(mode, name) ?? Find("general", name), fade);
        }

        void PlayLoop(Metin2MotionRecord motion, float fade)
        {
            if (motion == null || motion.clip == null || currentMotion == motion) return;
            currentMotion = motion;
            motionStartedAt = Time.time;
            nextMotionEvent = 0;
            animator.CrossFade(StateName(motion), fade, 0, 0f);
        }

        bool IsActionPlaying()
        {
            return currentMotion != null && !currentMotion.IsLoop;
        }

        Metin2MotionRecord Find(string mode, string name)
        {
            return motionSet.Find(mode, name);
        }

        string DefaultCombatMode()
        {
            switch (motionSet.characterClass)
            {
                case Metin2Dev.Frontend.Metin2CharacterClass.Warrior: return "onehand_sword";
                case Metin2Dev.Frontend.Metin2CharacterClass.Assassin: return "dualhand_sword";
                case Metin2Dev.Frontend.Metin2CharacterClass.Sura: return "onehand_sword";
                case Metin2Dev.Frontend.Metin2CharacterClass.Shaman: return "bell";
                default: return "general";
            }
        }

        void CycleCombatMode()
        {
            string[] modes = motionSet.motions.Select(item => item.mode)
                .Where(item => item == "general" || item.Contains("sword") || item == "bow" || item == "bell" || item == "fan")
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (modes.Length == 0) return;
            int index = Array.FindIndex(modes, item => item == combatMode);
            combatMode = modes[(index + 1 + modes.Length) % modes.Length];
            currentMotion = null;
            PlayLoop(combatMode, "wait", 0.12f);
        }

        static float MotionSpeed(Metin2MotionRecord motion, float fallback)
        {
            if (motion == null || motion.duration <= 0f) return fallback;
            float distance = motion.accumulation.magnitude / SourceUnitsPerMetre;
            return distance > 0.01f ? distance / motion.duration : fallback;
        }

        static float ComboLinkTime(Metin2MotionRecord motion)
        {
            if (motion.directInputTime >= 0f) return motion.directInputTime;
            if (motion.linkTime >= 0f) return motion.linkTime;
            return motion.duration * 0.55f;
        }

        void FireMotionEvent(Metin2MotionEvent motionEvent)
        {
            Transform attachment = string.IsNullOrWhiteSpace(motionEvent.attachingBone)
                ? transform
                : GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == motionEvent.attachingBone) ?? transform;
            Vector3 worldPosition = attachment.TransformPoint(motionEvent.position);
            if (motionEvent.effectPrefab != null)
            {
                GameObject effect = Instantiate(motionEvent.effectPrefab, worldPosition, attachment.rotation,
                    attachment == transform ? null : attachment);
                Destroy(effect, Mathf.Max(2f, motionEvent.duration + 2f));
            }
            if (motionEvent.type == 4 && motionEvent.radius > 0f)
            {
                Collider[] hits = Physics.OverlapSphere(worldPosition, motionEvent.radius, ~0, QueryTriggerInteraction.Collide);
                int remaining = motionEvent.hitLimit > 0 ? motionEvent.hitLimit : int.MaxValue;
                foreach (Collider hit in hits)
                {
                    if (remaining <= 0) break;
                    IMetin2Damageable damageable = hit.GetComponentInParent<IMetin2Damageable>();
                    if (damageable == null || hit.transform.IsChildOf(transform)) continue;
                    damageable.ReceiveMetin2Hit(this, currentMotion, motionEvent);
                    remaining--;
                }
            }
        }

        static string[] ComboNames()
        {
            return new[] { "combo_01", "combo_02", "combo_03", "combo_04", "combo_05", "combo_06", "combo_07" };
        }

        public static string StateName(Metin2MotionRecord motion)
        {
            return motion.mode + "__" + motion.name;
        }
    }

    public interface IMetin2Damageable
    {
        void ReceiveMetin2Hit(Metin2PlayerController attacker, Metin2MotionRecord motion, Metin2MotionEvent motionEvent);
    }
}
