using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Rust.Ai.Gen2;
using UnityEngine;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins 
{
    [Info("PetWolf", "YourName", "1.6.6")]
    [Description("Allows players to tame and command Gen2 AI Wolves (Follow, Stay, Sleep, Attack, Intimidated).")]
    public class PetWolf : RustPlugin
    {
        public enum PetCommand { Follow, Stay, Sleep, Attack, Intimidated }

        public class PetData
        {
            public BaseCombatEntity Entity;
            public BasePlayer Owner;
            public LimitedTurnNavAgent NavAgent;
            public PetCommand Command = PetCommand.Follow;
            public BaseCombatEntity AttackTarget;
            
            public State_Follow FollowState;
            public State_Stay StayState;

            public Timer ActionTimer;
            public Timer SleepHealTimer;

            public void Cleanup()
            {
                ActionTimer?.Destroy();
                SleepHealTimer?.Destroy();
            }
        }

        public static Dictionary<NetworkableId, PetData> ActivePets = new Dictionary<NetworkableId, PetData>();
        
        private Timer _passiveEnforcerTimer;
        private Harmony _harmony;
        private static FieldInfo _animLoopDurationField;

        private void Init()
        {
            // Cache the private 'duration' field of the native sleep state so we can manipulate it infinitely
            _animLoopDurationField = typeof(State_PlayAnimLoop).GetField("duration", BindingFlags.NonPublic | BindingFlags.Instance);

            _harmony = new Harmony("com.yourname.petwolves");
            _harmony.PatchAll();
        }

        private void Loaded()
        {
            _passiveEnforcerTimer = timer.Every(1f, EnforceDictatorState);
        }

        private void Unload()
        {
            _passiveEnforcerTimer?.Destroy();
            foreach (var pet in ActivePets.Values) pet.Cleanup();
            ActivePets.Clear();
            _harmony?.UnpatchAll("com.yourname.petwolves");
        }

        // The Enforcer Loop: Guarantees the wolf is doing EXACTLY what its Command dictates.
        private void EnforceDictatorState()
        {
            List<NetworkableId> keysToRemove = new List<NetworkableId>();

            foreach (var kvp in ActivePets)
            {
                var pet = kvp.Value;
                var wolf = pet.Entity;

                if (wolf == null || wolf.IsDestroyed || wolf.IsDead() || pet.Owner == null || pet.Owner.IsDead())
                {
                    pet.Cleanup();
                    keysToRemove.Add(kvp.Key);
                    continue;
                }

                var senseComp = wolf.GetComponent<SenseComponent>();
                var fsm = wolf.GetComponent<Wolf2FSM>();
                if (fsm == null) continue;

                // Deafen and blind the AI unless it's explicitly allowed to attack
                if (pet.Command != PetCommand.Attack)
                {
                    if (senseComp != null && senseComp.Target != null)
                    {
                        senseComp.ClearTarget(true);
                        senseComp.ForgetAllNoises();
                    }
                }

                // Force FSM States based on Command
                switch (pet.Command)
                {
                    case PetCommand.Follow:
                        if (fsm.CurrentState != pet.FollowState) fsm.SetState(pet.FollowState);
                        break;

                    case PetCommand.Stay:
                        if (fsm.CurrentState != pet.StayState) fsm.SetState(pet.StayState);
                        break;

                    case PetCommand.Sleep:
                        if (fsm.CurrentState != fsm.sleep) 
                        {
                            fsm.SetState(fsm.sleep);
                        }
                        
                        // Manipulate the native timer to force infinite sleep so it never wakes up on its own
                        if (_animLoopDurationField != null && fsm.CurrentState == fsm.sleep)
                        {
                            _animLoopDurationField.SetValue(fsm.sleep, 9999f); 
                        }

                        // Forcefully wipe the path so the wolf doesn't slide across the floor while sleeping
                        if (pet.NavAgent != null && pet.NavAgent.IsFollowingPath)
                        {
                            pet.NavAgent.ResetPath();
                        }
                        break;

                    case PetCommand.Intimidated:
                        if (fsm.CurrentState != fsm.intimidated)
                        {
                            fsm.SetState(fsm.intimidated);
                        }

                        // Wipe path so it doesn't slide while cowering
                        if (pet.NavAgent != null && pet.NavAgent.IsFollowingPath)
                        {
                            pet.NavAgent.ResetPath();
                        }
                        break;

                    case PetCommand.Attack:
                        if (pet.AttackTarget == null || pet.AttackTarget.IsDead())
                        {
                            pet.Command = PetCommand.Follow;
                            fsm.SetState(pet.FollowState);
                        }
                        else
                        {
                            bool isPeaceful = fsm.CurrentState == pet.FollowState || 
                                              fsm.CurrentState == pet.StayState || 
                                              fsm.CurrentState == fsm.sleep ||
                                              fsm.CurrentState == fsm.intimidated ||
                                              fsm.CurrentState == fsm.roam ||
                                              fsm.CurrentState == fsm.randomIdle;
                            
                            if (isPeaceful)
                            {
                                fsm.SetState(fsm.charge);
                            }
                        }
                        break;
                }
            }

            foreach (var key in keysToRemove) ActivePets.Remove(key);
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

        private List<PetData> GetPlayerPets(BasePlayer player)
        {
            List<PetData> pets = new List<PetData>();
            foreach (var pet in ActivePets.Values)
            {
                if (pet.Owner == player) pets.Add(pet);
            }
            return pets;
        }

        #endregion

        #region Chat Commands

        [ChatCommand("tame")]
        private void CmdTame(BasePlayer player, string command, string[] args)
        {
            var target = GetLookEntity(player, 15f);
            
            if (target != null && !target.IsDead() && target.ShortPrefabName.Contains("wolf2"))
            {
                if (ActivePets.ContainsKey(target.net.ID))
                {
                    ActivePets[target.net.ID].Cleanup();
                    ActivePets.Remove(target.net.ID);
                    player.ChatMessage("You released the wolf back into the wild.");
                }
                else
                {
                    var navAgent = target.GetComponent<LimitedTurnNavAgent>();
                    var petData = new PetData
                    {
                        Entity = target,
                        Owner = player,
                        NavAgent = navAgent,
                        Command = PetCommand.Follow,
                        FollowState = new State_Follow(target, navAgent),
                        StayState = new State_Stay(navAgent)
                    };
                    ActivePets[target.net.ID] = petData;
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
        private void CmdFollow(BasePlayer player, string command, string[] args)
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
        private void CmdStay(BasePlayer player, string command, string[] args)
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
        private void CmdSleep(BasePlayer player, string command, string[] args)
        {
            var pets = GetPlayerPets(player);
            if (pets.Count > 0)
            {
                foreach (var pet in pets)
                {
                    pet.Command = PetCommand.Sleep;
                    
                    pet.SleepHealTimer?.Destroy();
                    pet.SleepHealTimer = timer.Every(1800f, () =>
                    {
                        if (pet.Command == PetCommand.Sleep && pet.Entity != null && !pet.Entity.IsDead())
                        {
                            pet.Entity.Heal(5f);
                        }
                    });
                }
                player.ChatMessage($"Commanded {pets.Count} wolf(s) to sleep.");
            }
            else player.ChatMessage("You don't have any pet wolves.");
        }

        [ChatCommand("intimidated")]
        private void CmdIntimidated(BasePlayer player, string command, string[] args)
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
        private void CmdAttack(BasePlayer player, string command, string[] args)
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
                        
                        pet.AttackTarget = target;
                        pet.Command = PetCommand.Attack;

                        pet.ActionTimer?.Destroy();
                        pet.ActionTimer = timer.Once(10f, () =>
                        {
                            if (pet.Command == PetCommand.Attack)
                            {
                                pet.Command = PetCommand.Follow;
                                pet.AttackTarget = null;
                                if (player != null && player.IsConnected)
                                    player.ChatMessage("Your wolf has finished its attack and is returning.");
                            }
                        });
                    }
                    player.ChatMessage($"Commanded {pets.Count} wolf(s) to attack {target.ShortPrefabName} for 10 seconds!");
                }
                else player.ChatMessage("Invalid attack target. Make sure you are looking directly at an entity.");
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

                if (!ActivePets.TryGetValue(_entity.net.ID, out PetData data) || data.Owner == null || data.Owner.IsDead())
                {
                    return EFSMStateStatus.Failure; 
                }

                float distance = Vector3.Distance(_entity.transform.position, data.Owner.transform.position);

                if (distance > StopDistance)
                {
                    _navAgent.SetDestination(data.Owner.transform.position, true);
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

        #endregion

        #region Harmony Patches

        [HarmonyPatch(typeof(SenseComponent), "FindTarget")]
        public class SenseComponent_FindTarget_Patch
        {
            public static bool Prefix(SenseComponent __instance, ref BaseEntity target, ref bool __result)
            {
                if (__instance.baseEntity != null && ActivePets.TryGetValue(__instance.baseEntity.net.ID, out var pet))
                {
                    if (pet.Command == PetCommand.Attack && pet.AttackTarget != null && !pet.AttackTarget.IsDead())
                    {
                        target = pet.AttackTarget;
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
                    if (ActivePets.TryGetValue(__instance.Owner.net.ID, out var pet))
                    {
                        if (pet.Command != PetCommand.Attack)
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
                if (__instance != null && __instance.Owner != null && ActivePets.ContainsKey(__instance.Owner.net.ID))
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
                if (__instance != null && __instance.Owner != null && ActivePets.ContainsKey(__instance.Owner.net.ID))
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
                return !ActivePets.ContainsKey(__instance.baseEntity.net.ID); 
            }
        }

        [HarmonyPatch(typeof(Wolf2FSM), "Intimidate")]
        public class Wolf2FSM_Intimidate_Patch
        {
            public static bool Prefix(Wolf2FSM __instance, BaseEntity target)
            {
                return !ActivePets.ContainsKey(__instance.baseEntity.net.ID);
            }
        }

        [HarmonyPatch(typeof(Wolf2FSM), "OnDied")]
        public class Wolf2FSM_OnDied_Patch
        {
            public static void Postfix(Wolf2FSM __instance, HitInfo hitInfo)
            {
                if (__instance.baseEntity != null && ActivePets.ContainsKey(__instance.baseEntity.net.ID))
                {
                    ActivePets[__instance.baseEntity.net.ID].Cleanup();
                    ActivePets.Remove(__instance.baseEntity.net.ID);
                }
            }
        }

        #endregion
    }
}
