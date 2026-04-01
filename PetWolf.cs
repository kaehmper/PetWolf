using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Rust.Ai.Gen2;
using UnityEngine;
using Oxide.Core.Libraries.Covalence;

namespace Carbon.Plugins
{
    [Info("PetWolf", "YourName", "1.6.6")]
    [Description("Allows players to tame and command Gen2 AI Wolves (Follow, Stay, Sleep, Attack, Intimidated).")]
    public class PetWolf : CarbonPlugin
    {
        public enum PetCommand { Follow, Stay, Sleep, Attack, Intimidated, Lead }

        public class PetController : MonoBehaviour
        {
            public BaseCombatEntity Entity;
            public NetworkableId EntityId;
            public BasePlayer Owner;
            public LimitedTurnNavAgent NavAgent;
            public PetCommand Command = PetCommand.Follow;
            public BaseCombatEntity AttackTarget;
            
            public State_Follow FollowState;
            public State_Stay StayState;
            public State_Lead LeadState;

            private float _attackDuration = 10f;
            private float _attackStartedAt;

            private float _sleepHealInterval = 1800f;
            private float _lastSleepHealAt;

            private Wolf2FSM _fsm;
            private SenseComponent _sense;

            private void Awake()
            {
                _fsm = GetComponent<Wolf2FSM>();
                _sense = GetComponent<SenseComponent>();
                NavAgent = GetComponent<LimitedTurnNavAgent>();
                Entity = GetComponent<BaseCombatEntity>();
                if (Entity != null)
                {
                    EntityId = Entity.net.ID;
                    Entity._scale = 0.5f;
                    Entity.SendNetworkUpdate();
                }
            }

            private void Update()
            {
                if (Entity == null || Entity.IsDestroyed || Entity.IsDead() || Owner == null || Owner.IsDead())
                {
                    Destroy(this);
                    return;
                }

                if (_fsm == null) return;

                // Deafen and blind the AI unless it's explicitly allowed to attack
                if (Command != PetCommand.Attack)
                {
                    if (_sense != null && _sense.Target != null)
                    {
                        _sense.ClearTarget(true);
                        _sense.ForgetAllNoises();
                    }
                }

                // Force FSM States based on Command
                switch (Command)
                {
                    case PetCommand.Follow:
                        if (_fsm.CurrentState != FollowState) _fsm.SetState(FollowState);
                        break;

                    case PetCommand.Stay:
                        if (_fsm.CurrentState != StayState) _fsm.SetState(StayState);
                        break;

                    case PetCommand.Lead:
                        if (_fsm.CurrentState != LeadState) _fsm.SetState(LeadState);
                        break;

                    case PetCommand.Sleep:
                        if (_fsm.CurrentState != _fsm.sleep)
                        {
                            _fsm.SetState(_fsm.sleep);
                        }
                        
                        // Manipulate the native timer to force infinite sleep so it never wakes up on its own
                        if (_animLoopDurationField != null && _fsm.CurrentState == _fsm.sleep)
                        {
                            _animLoopDurationField.SetValue(_fsm.sleep, 9999f);
                        }

                        // Forcefully wipe the path so the wolf doesn't slide across the floor while sleeping
                        if (NavAgent != null && NavAgent.IsFollowingPath)
                        {
                            NavAgent.ResetPath();
                        }

                        // Handle Sleep Healing
                        if (Time.time - _lastSleepHealAt >= _sleepHealInterval)
                        {
                            Entity.Heal(5f);
                            _lastSleepHealAt = Time.time;
                        }
                        break;

                    case PetCommand.Intimidated:
                        if (_fsm.CurrentState != _fsm.intimidated)
                        {
                            _fsm.SetState(_fsm.intimidated);
                        }

                        // Wipe path so it doesn't slide while cowering
                        if (NavAgent != null && NavAgent.IsFollowingPath)
                        {
                            NavAgent.ResetPath();
                        }
                        break;

                    case PetCommand.Attack:
                        if (AttackTarget == null || AttackTarget.IsDead())
                        {
                            Command = PetCommand.Follow;
                            _fsm.SetState(FollowState);
                        }
                        else
                        {
                            // Handle Attack Timeout
                            if (Time.time - _attackStartedAt >= _attackDuration)
                            {
                                Command = PetCommand.Follow;
                                AttackTarget = null;
                                if (Owner != null && Owner.IsConnected)
                                    Owner.ChatMessage("Your wolf has finished its attack and is returning.");

                                _fsm.SetState(FollowState);
                                break;
                            }

                            bool isPeaceful = _fsm.CurrentState == FollowState ||
                                              _fsm.CurrentState == StayState ||
                                              _fsm.CurrentState == _fsm.sleep ||
                                              _fsm.CurrentState == _fsm.intimidated ||
                                              _fsm.CurrentState == _fsm.roam ||
                                              _fsm.CurrentState == _fsm.randomIdle;
                            
                            if (isPeaceful)
                            {
                                _fsm.SetState(_fsm.charge);
                            }
                        }
                        break;
                }
            }

            public void SetAttackTarget(BaseCombatEntity target)
            {
                AttackTarget = target;
                Command = PetCommand.Attack;
                _attackStartedAt = Time.time;
            }

            public void SetSleep()
            {
                Command = PetCommand.Sleep;
                _lastSleepHealAt = Time.time;
            }

            private void OnDestroy()
            {
                if (Entity != null && !Entity.IsDestroyed)
                {
                    Entity._scale = 1.0f;
                    Entity.SendNetworkUpdate();
                }

                if (PetWolf.Instance != null && PetWolf.Instance._activePets != null)
                {
                    PetWolf.Instance._activePets.Remove(EntityId);
                }
            }
        }

        public static PetWolf Instance;
        public Dictionary<NetworkableId, PetController> _activePets = new Dictionary<NetworkableId, PetController>();

        private Harmony _harmony;
        private static FieldInfo _animLoopDurationField;

        private void Init()
        {
            Instance = this;
            permission.RegisterPermission("petwolf.tame", this);

            // Cache the private 'duration' field of the native sleep state so we can manipulate it infinitely
            _animLoopDurationField = typeof(State_PlayAnimLoop).GetField("duration", BindingFlags.NonPublic | BindingFlags.Instance);

            _harmony = new Harmony("com.yourname.petwolves");
            _harmony.PatchAll();
        }

        private void Unload()
        {
            CleanupAllPets();
            _harmony?.UnpatchAll("com.yourname.petwolves");
            Instance = null;
        }

        private void OnServerShutdown()
        {
            CleanupAllPets();
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null) return;

            var pets = GetPlayerPets(player);
            foreach (var pet in pets)
            {
                if (pet != null) UnityEngine.Object.Destroy(pet);
            }
        }

        private void OnEntityKill(BaseCombatEntity entity)
        {
            if (entity != null && entity.TryGetComponent<PetController>(out var controller))
            {
                UnityEngine.Object.Destroy(controller);
            }
        }

        private void CleanupAllPets()
        {
            if (_activePets == null) return;

            var controllers = new List<PetController>(_activePets.Values);
            foreach (var controller in controllers)
            {
                if (controller != null) UnityEngine.Object.Destroy(controller);
            }
            _activePets.Clear();
        }

        #region Helper Methods

        private BaseCombatEntity GetLookEntity(BasePlayer player, float maxDist = 30f)
        {
            if (Physics.Raycast(player.eyes.HeadRay(), out RaycastHit hit, maxDist, Physics.DefaultRaycastLayers))
            {
                return hit.GetEntity() as BaseCombatEntity;
            }
            return null;
        }

        private List<PetController> GetPlayerPets(BasePlayer player)
        {
            List<PetController> pets = new List<PetController>();
            foreach (var pet in _activePets.Values)
            {
                if (pet.Owner == player) pets.Add(pet);
            }
            return pets;
        }

        #endregion

        #region Chat Commands

        [ChatCommand("tame")]
        private void TameCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "petwolf.tame"))
            {
                player.ChatMessage("You don't have permission to use this command.");
                return;
            }

            var target = GetLookEntity(player, 15f);
            
            if (target != null && !target.IsDead() && target.ShortPrefabName.Contains("wolf2"))
            {
                if (target.TryGetComponent<PetController>(out var controller))
                {
                    UnityEngine.Object.Destroy(controller);
                    player.ChatMessage("You released the wolf back into the wild.");
                }
                else
                {
                    controller = target.gameObject.AddComponent<PetController>();
                    controller.Owner = player;
                    controller.FollowState = new State_Follow(target, controller.NavAgent);
                    controller.StayState = new State_Stay(controller.NavAgent);
                    controller.LeadState = new State_Lead(target, controller.NavAgent);

                    _activePets[target.net.ID] = controller;
                    target.GetComponent<BlackboardComponent>()?.Clear();
                    
                    player.ChatMessage("You tamed a pet wolf! It will now follow you peacefully.");
                }
            }
            else
            {
                player.ChatMessage("You are not looking at a living Gen2 wolf.");
            }
        }

        [ChatCommand("follow")]
        private void FollowCommand(BasePlayer player, string command, string[] args)
        {
            var pets = GetPlayerPets(player);
            if (pets.Count > 0)
            {
                foreach (var pet in pets) pet.Command = PetCommand.Follow;
                player.ChatMessage($"Commanded {pets.Count} wolf(s) to follow.");
            }
            else player.ChatMessage("You don't have any pet wolves.");
        }

        [ChatCommand("stay")]
        private void StayCommand(BasePlayer player, string command, string[] args)
        {
            var pets = GetPlayerPets(player);
            if (pets.Count > 0)
            {
                foreach (var pet in pets) pet.Command = PetCommand.Stay;
                player.ChatMessage($"Commanded {pets.Count} wolf(s) to stay.");
            }
            else player.ChatMessage("You don't have any pet wolves.");
        }

        [ChatCommand("sleep")]
        private void SleepCommand(BasePlayer player, string command, string[] args)
        {
            var pets = GetPlayerPets(player);
            if (pets.Count > 0)
            {
                foreach (var pet in pets)
                {
                    pet.SetSleep();
                }
                player.ChatMessage($"Commanded {pets.Count} wolf(s) to sleep.");
            }
            else player.ChatMessage("You don't have any pet wolves.");
        }

        [ChatCommand("intimidated")]
        private void IntimidatedCommand(BasePlayer player, string command, string[] args)
        {
            var pets = GetPlayerPets(player);
            if (pets.Count > 0)
            {
                foreach (var pet in pets)
                {
                    pet.Command = PetCommand.Intimidated;
                }
                player.ChatMessage($"Commanded {pets.Count} wolf(s) to act intimidated.");
            }
            else player.ChatMessage("You don't have any pet wolves.");
        }

        [ChatCommand("attack")]
        private void AttackCommand(BasePlayer player, string command, string[] args)
        {
            var pets = GetPlayerPets(player);
            if (pets.Count > 0)
            {
                var target = GetLookEntity(player, 50f);
                if (target != null && target != player && !target.IsDead())
                {
                    foreach (var pet in pets)
                    {
                        if (target == pet.Entity) continue; 
                        pet.SetAttackTarget(target);
                    }
                    player.ChatMessage($"Commanded {pets.Count} wolf(s) to attack {target.ShortPrefabName} for 10 seconds!");
                }
                else player.ChatMessage("Invalid attack target. Make sure you are looking directly at an entity.");
            }
            else player.ChatMessage("You don't have any pet wolves.");
        }

        [ChatCommand("lead")]
        private void LeadCommand(BasePlayer player, string command, string[] args)
        {
            var pets = GetPlayerPets(player);
            if (pets.Count > 0)
            {
                foreach (var pet in pets) pet.Command = PetCommand.Lead;
                player.ChatMessage($"Commanded {pets.Count} wolf(s) to lead.");
            }
            else player.ChatMessage("You don't have any pet wolves.");
        }

        #endregion

        #region Custom AI States

        public class State_Follow : FSMStateBase
        {
            private BaseEntity _entity;
            private LimitedTurnNavAgent _navAgent;

            public float StopDistance = 3f;
            public float SprintDistance = 15f;
            public float RunDistance = 8f;

            public State_Follow(BaseEntity entity, LimitedTurnNavAgent navAgent)
            {
                Name = "Follow Owner";
                _entity = entity;
                _navAgent = navAgent;
            }

            public override EFSMStateStatus OnStateUpdate(float deltaTime)
            {
                if (_navAgent == null || _entity == null) return EFSMStateStatus.None;

                if (!_entity.TryGetComponent<PetController>(out var controller) || controller.Owner == null || controller.Owner.IsDead())
                {
                    return EFSMStateStatus.Failure; 
                }

                float distance = Vector3.Distance(_entity.transform.position, controller.Owner.transform.position);

                if (distance > StopDistance)
                {
                    _navAgent.SetDestination(controller.Owner.transform.position, true);
                    if (distance >= SprintDistance) _navAgent.SetSpeed(LimitedTurnNavAgent.Speeds.Sprint);
                    else if (distance >= RunDistance) _navAgent.SetSpeed(LimitedTurnNavAgent.Speeds.Run);
                    else _navAgent.SetSpeed(LimitedTurnNavAgent.Speeds.Walk);
                }
                else
                {
                    if (_navAgent.IsFollowingPath) _navAgent.ResetPath();
                }

                return EFSMStateStatus.None;
            }
        }

        public class State_Stay : FSMStateBase
        {
            private LimitedTurnNavAgent _navAgent;

            public State_Stay(LimitedTurnNavAgent navAgent)
            {
                Name = "Stay Still";
                _navAgent = navAgent;
            }

            public override EFSMStateStatus OnStateUpdate(float deltaTime)
            {
                if (_navAgent != null && _navAgent.IsFollowingPath)
                {
                    _navAgent.ResetPath();
                }
                return EFSMStateStatus.None;
            }
        }

        public class State_Lead : FSMStateBase
        {
            private BaseEntity _entity;
            private LimitedTurnNavAgent _navAgent;

            public float StopDistance = 2f;
            public float AheadDistance = 5f;

            public State_Lead(BaseEntity entity, LimitedTurnNavAgent navAgent)
            {
                Name = "Lead Owner";
                _entity = entity;
                _navAgent = navAgent;
            }

            public override EFSMStateStatus OnStateUpdate(float deltaTime)
            {
                if (_navAgent == null || _entity == null) return EFSMStateStatus.None;

                if (!_entity.TryGetComponent<PetController>(out var controller) || controller.Owner == null || controller.Owner.IsDead())
                {
                    return EFSMStateStatus.Failure;
                }

                Vector3 forward = controller.Owner.eyes.HeadRay().direction;
                forward.y = 0;
                Vector3 targetPos = controller.Owner.transform.position + (forward.normalized * AheadDistance);
                float distance = Vector3.Distance(_entity.transform.position, targetPos);

                if (distance > StopDistance)
                {
                    _navAgent.SetDestination(targetPos, true);
                    _navAgent.SetSpeed(LimitedTurnNavAgent.Speeds.Run);
                }
                else
                {
                    if (_navAgent.IsFollowingPath) _navAgent.ResetPath();
                }

                return EFSMStateStatus.None;
            }
        }

        #endregion

        #region Harmony Patches

        [HarmonyPatch(typeof(SenseComponent), "FindTarget")]
        public class SenseComponent_FindTarget_Patch
        {
            public static bool Prefix(SenseComponent __instance, ref BaseEntity target, ref bool __result)
            {
                if (__instance.baseEntity != null && __instance.baseEntity.TryGetComponent<PetController>(out var controller))
                {
                    if (controller.Command == PetCommand.Attack && controller.AttackTarget != null && !controller.AttackTarget.IsDead())
                    {
                        target = controller.AttackTarget;
                        __result = true;
                        return false; 
                    }
                    
                    target = null;
                    __result = false;
                    return false; 
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(State_Attack), "OnStateEnter")]
        public class State_Attack_OnStateEnter_Patch
        {
            public static bool Prefix(State_Attack __instance, FSMPayload payload, ref EFSMStateStatus __result)
            {
                if (__instance != null && __instance.Owner != null)
                {
                    if (__instance.Owner.TryGetComponent<PetController>(out var controller))
                    {
                        if (controller.Command != PetCommand.Attack)
                        {
                            __result = EFSMStateStatus.Failure;
                            return false; 
                        }
                    }
                }
                return true;
            }
        }

        // PREVENT DEAD BODY EATING: Stops the wolf from approaching food
        [HarmonyPatch(typeof(State_ApproachFood), "OnStateEnter")]
        public class State_ApproachFood_OnStateEnter_Patch
        {
            public static bool Prefix(State_ApproachFood __instance, FSMPayload payload, ref EFSMStateStatus __result)
            {
                if (__instance != null && __instance.Owner != null && __instance.Owner.GetComponent<PetController>() != null)
                {
                    __result = EFSMStateStatus.Failure;
                    return false; 
                }
                return true;
            }
        }

        // PREVENT DEAD BODY EATING: Stops the physical eating state
        [HarmonyPatch(typeof(State_EatFood), "OnStateEnter")]
        public class State_EatFood_OnStateEnter_Patch
        {
            public static bool Prefix(State_EatFood __instance, FSMPayload payload, ref EFSMStateStatus __result)
            {
                if (__instance != null && __instance.Owner != null && __instance.Owner.GetComponent<PetController>() != null)
                {
                    __result = EFSMStateStatus.Failure;
                    return false; 
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Wolf2FSM), "Hurt")]
        public class Wolf2FSM_Hurt_Patch
        {
            public static bool Prefix(Wolf2FSM __instance, HitInfo hitInfo)
            {
                return __instance.baseEntity == null || __instance.baseEntity.GetComponent<PetController>() == null;
            }
        }

        [HarmonyPatch(typeof(Wolf2FSM), "Intimidate")]
        public class Wolf2FSM_Intimidate_Patch
        {
            public static bool Prefix(Wolf2FSM __instance, BaseEntity target)
            {
                return __instance.baseEntity == null || __instance.baseEntity.GetComponent<PetController>() == null;
            }
        }

        [HarmonyPatch(typeof(Wolf2FSM), "SetState")]
        public class Wolf2FSM_SetState_Patch
        {
            public static bool Prefix(Wolf2FSM __instance, FSMStateBase nextState)
            {
                if (__instance.baseEntity != null && __instance.baseEntity.TryGetComponent<PetController>(out var controller))
                {
                    if (controller.Command == PetCommand.Attack) return true;

                    if (nextState == controller.FollowState || nextState == controller.StayState ||
                        nextState == controller.LeadState ||
                        nextState == __instance.sleep || nextState == __instance.intimidated)
                        return true;

                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Wolf2FSM), "OnDied")]
        public class Wolf2FSM_OnDied_Patch
        {
            public static void Postfix(Wolf2FSM __instance, HitInfo hitInfo)
            {
                if (__instance.baseEntity != null && __instance.baseEntity.TryGetComponent<PetController>(out var controller))
                {
                    UnityEngine.Object.Destroy(controller);
                }
            }
        }

        #endregion
    }
}
