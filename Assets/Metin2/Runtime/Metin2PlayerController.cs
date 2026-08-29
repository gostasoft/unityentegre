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
        // The original clips are authored in client world scale; generated maps use a larger visual scale.
        // The imported map is much smaller than the original client coordinate space.
        // Keep source walk/run timing but use one tenth of the previous map displacement.
        const float GeneratedMapLocomotionScale = 0.01f;
        const int LocalPlayerLayer = 8;
        const float GroundOffset = 0.02f;
        const float GroundProbeHeight = 1f;
        const float GroundProbeDistance = 2f;

        [Header("Movement Speed (persisted defaults are in Metin2PlayerMovementSettings)")]
        [Min(0f)] public float walkSpeedMultiplier = 2f;
        [Min(0f)] public float runSpeedMultiplier = 3f;

        Metin2RaceMotionSet motionSet;
        Animator animator;
        CharacterController characterController;
        Camera gameplayCamera;
        Vector2 moveInput;
        bool runInput;
        float verticalVelocity;
        string combatMode;
        Metin2MotionRecord currentMotion;
        float motionStartedAt;
        bool comboQueued;
        int comboIndex;
        int nextMotionEvent;
        bool clickMoveActive;
        Vector3 clickMoveDestination;
        float nextHeldAttackAt;
        float nextAutoAttackAt;
        bool actionHitApplied;
        Metin2MobCombatant combatTarget;
        readonly List<Metin2MotionRecord> skills = new List<Metin2MotionRecord>();

        public string CharacterName { get; private set; }
        public Metin2MobCombatant CombatTarget => combatTarget;

        public void Initialize(Metin2RaceMotionSet set, Animator visualAnimator, Camera camera, string characterName)
        {
            motionSet = set;
            animator = visualAnimator;
            gameplayCamera = camera;
            CharacterName = characterName;
            characterController = GetComponent<CharacterController>();
            Metin2PlayerMovementSettings settings = Resources.Load<Metin2PlayerMovementSettings>("Metin2PlayerMovementSettings");
            if (settings != null)
            {
                walkSpeedMultiplier = settings.walkSpeedMultiplier;
                runSpeedMultiplier = settings.runSpeedMultiplier;
            }
            characterController.radius = 0.35f;
            characterController.height = 1.75f;
            characterController.center = new Vector3(0f, 0.875f, 0f);
            combatMode = DefaultCombatMode();
            skills.Clear();
            foreach (string name in SourceSkillOrder())
            {
                Metin2MotionRecord skill = Find("skill", name);
                if (skill != null) skills.Add(skill);
            }
            if (skills.Count == 0)
                skills.AddRange(motionSet.motions.Where(item => item != null && item.mode == "skill")
                    .GroupBy(item => item.name, StringComparer.OrdinalIgnoreCase).Select(group => group.First()));
            animator.Rebind();
            animator.Update(0f);
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
            Keyboard keyboard = Keyboard.current;
            moveInput = Vector2.zero;
            runInput = false;
            if (keyboard == null) return;
            if (Metin2GameplayOverlay.IsTyping) return;

            // Locomotion is driven by the original walk/run clips. A normal WASD press uses walk;
            // holding Shift selects the source run clip, whose Accumulation / MotionDuration sets the speed.
            if (keyboard.wKey.isPressed) moveInput.y += 1f;
            if (keyboard.sKey.isPressed) moveInput.y -= 1f;
            if (keyboard.dKey.isPressed) moveInput.x += 1f;
            if (keyboard.aKey.isPressed) moveInput.x -= 1f;
            if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();
            runInput = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            if (moveInput.sqrMagnitude > 0.0001f) clickMoveActive = false;

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
                SetClickMoveDestination(mouse.position.ReadValue());
            // Point-and-click is a run command; keyboard movement remains walk unless Shift is held.
            if (clickMoveActive) runInput = true;

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                QueueAttack();
                nextHeldAttackAt = Time.time + 0.08f;
            }
            else if (keyboard.spaceKey.isPressed && Time.time >= nextHeldAttackAt)
            {
                // Holding Space continuously feeds the source combo input window; releasing is optional.
                QueueAttack();
                nextHeldAttackAt = Time.time + 0.08f;
            }
            else if (!keyboard.spaceKey.isPressed)
            {
                nextHeldAttackAt = 0f;
            }
            if (keyboard.rKey.wasPressedThisFrame) CycleCombatMode();
            if (keyboard.vKey.wasPressedThisFrame)
            {
                Metin2GameplayCamera cameraController = gameplayCamera != null
                    ? gameplayCamera.GetComponent<Metin2GameplayCamera>()
                    : null;
                if (cameraController != null) cameraController.ToggleView();
            }
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) ActivateQuickSlot(0);
            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) ActivateQuickSlot(1);
            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) ActivateQuickSlot(2);
            if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) ActivateQuickSlot(3);
            if (keyboard.f1Key.wasPressedThisFrame) ActivateQuickSlot(4);
            if (keyboard.f2Key.wasPressedThisFrame) ActivateQuickSlot(5);
            if (keyboard.f3Key.wasPressedThisFrame) ActivateQuickSlot(6);
            if (keyboard.f4Key.wasPressedThisFrame) ActivateQuickSlot(7);
        }

        public void ActivateQuickSlot(int index)
        {
            if (index < 0 || index > 7) return;
            Metin2QuickSlotSystem.Activate(index, this);
        }

        public void ActivateSkill(int skillIndex)
        {
            if (skillIndex < 0) return;
            TryPlaySkill(skillIndex);
        }

        void TryPlaySkill(int index)
        {
            string[] sourceOrder = SourceSkillOrder();
            Metin2MotionRecord skill = index < sourceOrder.Length ? Find("skill", sourceOrder[index]) : null;
            skill ??= index < skills.Count ? skills[index] : null;
            if (skill == null)
            {
                Debug.LogWarning("Selected character has no source skill mapped to key " + (index + 1) + ".");
                return;
            }
            Metin2PlayerState state = GetComponent<Metin2PlayerState>();
            int spCost = 12 + index * 4;
            if (state != null && !state.SpendSp(spCost))
            {
                Metin2ChatService.Append(Metin2ChatChannel.Info, "Yeterli SP yok.");
                return;
            }
            PlayAction(skill);
        }

        void SetClickMoveDestination(Vector2 screenPosition)
        {
            if (gameplayCamera == null) return;
            int groundMask = ~(1 << LocalPlayerLayer);
            Ray ray = gameplayCamera.ScreenPointToRay(screenPosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 10000f, groundMask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit candidate in hits)
            {
                Metin2MobCombatant mob = candidate.collider.GetComponentInParent<Metin2MobCombatant>();
                if (mob == null || !mob.IsAttackable) continue;
                SetCombatTarget(mob);
                clickMoveActive = true;
                clickMoveDestination = mob.transform.position;
                return;
            }
            if (!Physics.Raycast(ray, out RaycastHit hit, 10000f, groundMask, QueryTriggerInteraction.Ignore)) return;
            ClearCombatTarget();
            clickMoveDestination = hit.point;
            clickMoveActive = true;
        }

        void SetCombatTarget(Metin2MobCombatant target)
        {
            if (combatTarget == target) return;
            if (combatTarget != null) combatTarget.Select(false);
            combatTarget = target;
            if (combatTarget != null) combatTarget.Select(true);
        }

        void ClearCombatTarget()
        {
            if (combatTarget != null) combatTarget.Select(false);
            combatTarget = null;
        }

        void UpdateMovement()
        {
            if (IsActionPlaying())
            {
                ApplyGravity();
                return;
            }

            Vector3 direction = Vector3.zero;
            if (moveInput.sqrMagnitude > 0.0001f)
            {
                if (combatTarget != null) ClearCombatTarget();
                Vector3 forward = Vector3.ProjectOnPlane(gameplayCamera.transform.forward, Vector3.up).normalized;
                Vector3 right = Vector3.ProjectOnPlane(gameplayCamera.transform.right, Vector3.up).normalized;
                direction = (forward * moveInput.y) + (right * moveInput.x);
            }
            else if (combatTarget != null)
            {
                if (combatTarget.IsDead)
                {
                    ClearCombatTarget();
                    clickMoveActive = false;
                }
                else
                {
                    Vector3 toTarget = Vector3.ProjectOnPlane(combatTarget.transform.position - transform.position, Vector3.up);
                    float stopRange = 2.8f;
                    if (toTarget.magnitude > stopRange)
                    {
                        direction = toTarget;
                        clickMoveDestination = combatTarget.transform.position;
                        clickMoveActive = true;
                    }
                    else
                    {
                        clickMoveActive = false;
                        if (toTarget.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                        if (Time.time >= nextAutoAttackAt)
                        {
                            nextAutoAttackAt = Time.time + 0.25f;
                            QueueAttack();
                        }
                    }
                }
            }
            else if (clickMoveActive)
            {
                direction = Vector3.ProjectOnPlane(clickMoveDestination - transform.position, Vector3.up);
                if (direction.sqrMagnitude <= 0.0064f) clickMoveActive = false;
            }

            if (direction.sqrMagnitude < 0.0001f)
            {
                PlayLoop(combatMode, "wait", 0.10f);
                ApplyGravity();
                return;
            }

            Metin2MotionRecord locomotion = LocomotionMotion();
            if (locomotion == null) return;
            direction.Normalize();

            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            // Run distance is deliberately derived from walk distance so Shift is always exactly 3x walking,
            // even when a source run MSA has different authored accumulation.
            float walkSpeed = MotionSpeed(Find(combatMode, "walk") ?? Find("general", "walk")) * walkSpeedMultiplier;
            float speed = runInput && walkSpeed > 0f ? walkSpeed * runSpeedMultiplier : MotionSpeed(locomotion) * walkSpeedMultiplier;
            ApplyGravity(direction * speed);
            if (currentMotion != locomotion) PlayLoop(locomotion, 0.10f);
        }

        void ApplyGravity(Vector3 horizontalVelocity = default)
        {
            if (StickToGround())
            {
                verticalVelocity = 0f;
                characterController.Move(horizontalVelocity * Time.deltaTime);
                StickToGround();
                return;
            }

            verticalVelocity += Physics.gravity.y * Time.deltaTime;
            characterController.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        bool StickToGround()
        {
            int groundMask = ~(1 << LocalPlayerLayer);
            Vector3 origin = transform.position + Vector3.up * GroundProbeHeight;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, GroundProbeDistance, groundMask,
                    QueryTriggerInteraction.Ignore)) return false;
            Vector3 position = transform.position;
            position.y = hit.point.y + GroundOffset;
            transform.position = position;
            return true;
        }

        void UpdateAction()
        {
            if (!IsActionPlaying()) return;
            float elapsed = Time.time - motionStartedAt;
            while (nextMotionEvent < currentMotion.events.Count && currentMotion.events[nextMotionEvent].startTime <= elapsed)
                FireMotionEvent(currentMotion.events[nextMotionEvent++]);
            float impactTime = currentMotion.attackStartTime >= 0f ? currentMotion.attackStartTime : currentMotion.duration * 0.38f;
            if (!actionHitApplied && combatTarget != null && elapsed >= impactTime)
                ApplyTargetHit();
            if (comboQueued && currentMotion.inputLimitTime >= 0f && elapsed >= currentMotion.inputLimitTime)
                comboQueued = false;
            if (comboQueued && elapsed >= ComboLinkTime(currentMotion))
            {
                comboQueued = false;
                comboIndex = comboIndex % ComboNames().Length;
                Metin2MotionRecord next = Find(combatMode, ComboNames()[comboIndex++]);
                if (next != null) PlayAction(next, 0.12f);
                return;
            }
            if (elapsed >= Mathf.Max(0.01f, currentMotion.duration))
            {
                currentMotion = null;
                comboIndex = 0;
                PlayLoop(moveInput.sqrMagnitude > 0.0001f ? LocomotionMotion() : Find(combatMode, "wait"), 0.08f);
            }
        }

        void QueueAttack()
        {
            if (!IsActionPlaying())
            {
                comboIndex = 1;
                Metin2MotionRecord first = Find(combatMode, ComboNames()[0]) ?? Find(combatMode, "attack") ?? Find("general", "attack");
                if (first != null) PlayAction(first, 0.06f);
                return;
            }
            float elapsed = Time.time - motionStartedAt;
            float start = currentMotion.preInputTime >= 0f ? currentMotion.preInputTime : currentMotion.duration * 0.2f;
            float limit = currentMotion.inputLimitTime >= 0f ? currentMotion.inputLimitTime : currentMotion.duration * 0.8f;
            if (elapsed >= start && elapsed <= limit) comboQueued = true;
        }

        void PlayAction(Metin2MotionRecord motion, float fade = 0.08f)
        {
            if (motion == null || motion.clip == null) return;
            currentMotion = motion;
            motionStartedAt = Time.time;
            nextMotionEvent = 0;
            actionHitApplied = false;
            comboQueued = false;
            PlayAnimatorState(motion, fade);
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
            PlayAnimatorState(motion, fade);
        }

        void PlayAnimatorState(Metin2MotionRecord motion, float fade)
        {
            int state = Animator.StringToHash(StateName(motion));
            if (!animator.HasState(0, state))
            {
                Debug.LogError("Metin2 motion state is missing from the selected character controller: " + StateName(motion));
                return;
            }

            // Do not rebind here: rebind resets the outgoing combo pose and causes a visible restart.
            // The animator is bound once in Initialize; CrossFade now blends from the live source pose.
            if (fade <= 0f) animator.Play(state, 0, 0f);
            else animator.CrossFade(state, fade, 0, 0f);
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

        Metin2MotionRecord LocomotionMotion()
        {
            string motionName = runInput ? "run" : "walk";
            return Find(combatMode, motionName) ?? Find("general", motionName) ??
                   Find(combatMode, runInput ? "walk" : "run") ?? Find("general", runInput ? "walk" : "run");
        }

        string[] SourceSkillOrder()
        {
            switch (motionSet.characterClass)
            {
                // These orders mirror root/playersettingmodule.py in the extracted client.
                case Metin2Dev.Frontend.Metin2CharacterClass.Warrior:
                    return new[] { "samyeon", "palbang", "jeongwi", "geomgyeong", "tanhwan", "geompung" };
                case Metin2Dev.Frontend.Metin2CharacterClass.Assassin:
                    return new[] { "amseup", "gungsin", "charyun", "eunhyeong", "sangong", "dokgigung" };
                case Metin2Dev.Frontend.Metin2CharacterClass.Sura:
                    return new[] { "swaeryeong", "yonggwon", "gwigeom", "gongpo", "jumagap", "pabeop" };
                case Metin2Dev.Frontend.Metin2CharacterClass.Shaman:
                    return new[] { "bipabu", "yongpa", "paeryong", "hosin", "boho", "gicheon" };
                default:
                    return Array.Empty<string>();
            }
        }

        static float MotionSpeed(Metin2MotionRecord motion)
        {
            if (motion == null || motion.duration <= 0f) return 0f;
            // ParseMotion already converts the source accumulation (centimetres) into Unity metres.
            float distance = motion.accumulation.magnitude;
            return distance > 0.01f ? (distance / motion.duration) * GeneratedMapLocomotionScale : 0f;
        }

        static float ComboLinkTime(Metin2MotionRecord motion)
        {
            if (motion.directInputTime >= 0f) return motion.directInputTime;
            if (motion.linkTime >= 0f) return motion.linkTime;
            return motion.duration * 0.55f;
        }

        void FireMotionEvent(Metin2MotionEvent motionEvent)
        {
            // EffectLib only evaluates AttachingBoneName when AttachingEnable is set.
            // A number of source skills (for example samyeon) retain a bone name while
            // explicitly disabling attachment; those effects must originate at the actor root.
            Transform attachment = motionEvent.attachToBone
                ? ResolveEffectAttachment(motionEvent.attachingBone)
                : transform;
            Vector3 worldPosition = attachment.TransformPoint(motionEvent.position);
            if (motionEvent.effectPrefab != null)
            {
                GameObject effect = Instantiate(motionEvent.effectPrefab, worldPosition, attachment.rotation,
                    motionEvent.attachToBone && motionEvent.followAttachment && attachment != transform ? attachment : null);
                // MSA effect events do not always provide DuringTime. Keep the spawned
                // EffectLib conversion alive for the actual authored particle lifetime.
                Destroy(effect, Mathf.Max(2f, motionEvent.duration + 2f, EffectLifetime(effect) + 0.1f));
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
                    if (damageable is Metin2MobCombatant mob && mob == combatTarget) actionHitApplied = true;
                    remaining--;
                }
            }
        }

        void ApplyTargetHit()
        {
            if (combatTarget == null || combatTarget.IsDead) return;
            Vector3 delta = Vector3.ProjectOnPlane(combatTarget.transform.position - transform.position, Vector3.up);
            float range = currentMotion != null && currentMotion.weaponLength > 0f
                ? Mathf.Clamp(currentMotion.weaponLength * 0.01f + 1.5f, 2.5f, 5f)
                : 3.25f;
            if (delta.magnitude > range) return;
            actionHitApplied = true;
            combatTarget.ReceiveMetin2Hit(this, currentMotion, null);
            if (combatTarget.IsDead) ClearCombatTarget();
        }

        static float EffectLifetime(GameObject effect)
        {
            float lifetime = 0f;
            foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                lifetime = Mathf.Max(lifetime, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
            }
            return lifetime;
        }

        Transform ResolveEffectAttachment(string sourceBone)
        {
            if (string.IsNullOrWhiteSpace(sourceBone)) return transform;

            Transform[] bones = GetComponentsInChildren<Transform>(true);
            // The original client attaches a weapon effect requested through this logical socket
            // to the currently equipped weapon.  In the converted FBX the socket can be a
            // separate dummy, while the test sword is correctly animated below Bip01 R Hand.
            if (sourceBone.Equals("equip_right_hand", StringComparison.OrdinalIgnoreCase))
            {
                Transform weapon = bones.FirstOrDefault(item => item.name.StartsWith("Weapon -", StringComparison.Ordinal));
                if (weapon != null) return weapon;
            }
            Transform exact = bones.FirstOrDefault(item => item.name == sourceBone);
            if (exact != null) return exact;

            // Metin2 MSA files name logical equipment sockets while the converted model
            // retains the original Bip01 bone names.
            string modelBone = sourceBone.Equals("equip_right_hand", StringComparison.OrdinalIgnoreCase) ? "Bip01 R Hand" :
                sourceBone.Equals("equip_left_hand", StringComparison.OrdinalIgnoreCase) ? "Bip01 L Hand" :
                sourceBone;
            return bones.FirstOrDefault(item => item.name.Equals(modelBone, StringComparison.OrdinalIgnoreCase)) ?? transform;
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
