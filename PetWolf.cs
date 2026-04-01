using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Rust.Ai.Gen2;
using UnityEngine;

namespace Carbon.Plugins;

[Info("PetWolf", "Jules", "2.0.0")]
[Description("A streamlined Gen2 AI Wolf pet plugin for Carbon.")]
public class PetWolf : CarbonPlugin
{
    private const string PermUse = "petwolf.use";
    private Harmony _harmony;
    private static FieldInfo _animLoopDurationField;

    #region Lifecycle

    private void Init()
    {
        permission.RegisterPermission(PermUse, this);
        _animLoopDurationField = typeof(State_PlayAnimLoop).GetField("duration", BindingFlags.NonPublic | BindingFlags.Instance);
        _harmony = new Harmony("com.carbon.petwolf");
        _harmony.PatchAll();
    }

    private void OnServerInitialized()
    {
        Puts($"[{Name}] {Version} initialized.");
    }

    private void Unload()
    {
        var pets = new List<WolfPet>(WolfPet.Instances);
        foreach (var pet in pets) UnityEngine.Object.Destroy(pet);
        
        _harmony?.UnpatchAll(_harmony.Id);
    }

    #endregion

    #region Commands

    [ChatCommand("pet")]
    private void CmdPet(BasePlayer player, string command, string[] args)
    {
        if (player == null) return;

        if (!permission.UserHasPermission(player.UserIDString, PermUse))
        {
            player.ChatMessage("You don't have permission to use this command.");
            return;
        }

        if (args.Length == 0)
        {
            player.ChatMessage("Usage: /pet <tame|follow|stay|sleep|attack|intimidated|release>");
            return;
        }

        string subCommand = args[0].ToLower();
        switch (subCommand)
        {
            case "tame":
                HandleTame(player);
                break;
            case "follow":
                HandleSetCommand(player, PetCommand.Follow);
                break;
            case "stay":
                HandleSetCommand(player, PetCommand.Stay);
                break;
            case "sleep":
                HandleSetCommand(player, PetCommand.Sleep);
                break;
            case "attack":
                HandleAttack(player);
                break;
            case "intimidated":
                HandleSetCommand(player, PetCommand.Intimidated);
                break;
            case "release":
                HandleRelease(player);
                break;
            default:
                player.ChatMessage("Unknown subcommand. Use: tame, follow, stay, sleep, attack, intimidated, release.");
                break;
        }
    }

    #endregion

    #region Command Handlers

    private void HandleTame(BasePlayer player)
    {
        var entity = GetLookEntity(player, 15f);
        if (entity == null || entity.IsDead() || !entity.ShortPrefabName.Contains("wolf2"))
        {
            player.ChatMessage("You are not looking at a living Gen2 wolf.");
            return;
        }

        if (entity.GetComponent<WolfPet>() != null)
        {
            player.ChatMessage("This wolf is already someone's pet.");
            return;
        }

        var pet = entity.gameObject.AddComponent<WolfPet>();
        pet.Initialize(player);
        player.ChatMessage("You tamed a pet wolf!");
    }

    private void HandleSetCommand(BasePlayer player, PetCommand cmd)
    {
        int count = 0;
        foreach (var pet in WolfPet.Instances)
        {
            if (pet.Owner == player)
            {
                pet.Command = cmd;
                count++;
            }
        }
        player.ChatMessage(count > 0 ? $"Commanded {count} wolf(s) to {cmd}." : "You don't have any pet wolves.");
    }

    private void HandleAttack(BasePlayer player)
    {
        var target = GetLookEntity(player, 50f);
        if (target == null || target.IsDead() || target == player)
        {
            player.ChatMessage("Invalid attack target.");
            return;
        }

        int count = 0;
        foreach (var pet in WolfPet.Instances)
        {
            if (pet.Owner == player)
            {
                if (pet.BaseEntity == target) continue;
                pet.Attack(target);
                count++;
            }
        }
        player.ChatMessage(count > 0 ? $"Commanded {count} wolf(s) to attack {target.ShortPrefabName}!" : "You don't have any pet wolves.");
    }

    private void HandleRelease(BasePlayer player)
    {
        int count = 0;
        var pets = new List<WolfPet>(WolfPet.Instances);
        foreach (var pet in pets)
        {
            if (pet.Owner == player)
            {
                UnityEngine.Object.Destroy(pet);
                count++;
            }
        }
        player.ChatMessage(count > 0 ? $"Released {count} wolf(s) back into the wild." : "You don't have any pet wolves.");
    }

    #endregion

    #region Helpers

    private BaseCombatEntity GetLookEntity(BasePlayer player, float maxDist)
    {
        if (Physics.Raycast(player.eyes.HeadRay(), out var hit, maxDist, Physics.DefaultRaycastLayers))
        {
            return hit.GetEntity() as BaseCombatEntity;
        }
        return null;
    }

    #endregion

    #region MonoBehaviour Component

    public enum PetCommand { Follow, Stay, Sleep, Attack, Intimidated }

    public class WolfPet : MonoBehaviour
    {
        public static readonly List<WolfPet> Instances = new();

        public BasePlayer Owner { get; private set; }
        public PetCommand Command = PetCommand.Follow;
        public BaseCombatEntity AttackTarget;
        public float AttackEndTime;

        public BaseCombatEntity BaseEntity;
        public Wolf2FSM Fsm;
        public LimitedTurnNavAgent NavAgent;
        public SenseComponent Sense;

        private bool _isSettingState;

        public void Initialize(BasePlayer owner)
        {
            Owner = owner;
            BaseEntity = GetComponent<BaseCombatEntity>();
            Fsm = GetComponent<Wolf2FSM>();
            NavAgent = GetComponent<LimitedTurnNavAgent>();
            Sense = GetComponent<SenseComponent>();

            GetComponent<BlackboardComponent>()?.Clear();
        }

        private void OnEnable() => Instances.Add(this);
        private void OnDisable() => Instances.Remove(this);

        private void Update()
        {
            if (BaseEntity == null || BaseEntity.IsDestroyed || BaseEntity.IsDead() || Owner == null || !Owner.IsConnected || Owner.IsDead())
            {
                UnityEngine.Object.Destroy(this);
                return;
            }

            if (Command != PetCommand.Attack)
            {
                if (Sense != null && Sense.Target != null)
                {
                    Sense.ClearTarget(true);
                    Sense.ForgetAllNoises();
                }
            }

            switch (Command)
            {
                case PetCommand.Follow:
                    UpdateFollow();
                    break;
                case PetCommand.Stay:
                    UpdateStay();
                    break;
                case PetCommand.Sleep:
                    UpdateSleep();
                    break;
                case PetCommand.Attack:
                    UpdateAttack();
                    break;
                case PetCommand.Intimidated:
                    UpdateIntimidated();
                    break;
            }
        }

        private void UpdateFollow()
        {
            SetState(Fsm.randomIdle);
            float dist = Vector3.Distance(transform.position, Owner.transform.position);
            if (dist > 3f)
            {
                NavAgent.SetDestination(Owner.transform.position, true);
                if (dist > 15f) NavAgent.SetSpeed(LimitedTurnNavAgent.Speeds.Sprint);
                else if (dist > 8f) NavAgent.SetSpeed(LimitedTurnNavAgent.Speeds.Run);
                else NavAgent.SetSpeed(LimitedTurnNavAgent.Speeds.Walk);
            }
            else if (NavAgent.IsFollowingPath)
            {
                NavAgent.ResetPath();
            }
        }

        private void UpdateStay()
        {
            SetState(Fsm.randomIdle);
            if (NavAgent.IsFollowingPath) NavAgent.ResetPath();
        }

        private void UpdateSleep()
        {
            SetState(Fsm.sleep);
            if (_animLoopDurationField != null) _animLoopDurationField.SetValue(Fsm.sleep, 9999f);
            if (NavAgent.IsFollowingPath) NavAgent.ResetPath();
        }

        private void UpdateAttack()
        {
            if (AttackTarget == null || AttackTarget.IsDead() || Time.time > AttackEndTime)
            {
                Command = PetCommand.Follow;
                AttackTarget = null;
                return;
            }

            if (Fsm.CurrentState != Fsm.charge && Fsm.CurrentState != Fsm.attack)
            {
                SetState(Fsm.charge);
            }
        }

        private void UpdateIntimidated()
        {
            SetState(Fsm.intimidated);
            if (NavAgent.IsFollowingPath) NavAgent.ResetPath();
        }

        public void Attack(BaseCombatEntity target)
        {
            AttackTarget = target;
            AttackEndTime = Time.time + 15f;
            Command = PetCommand.Attack;
        }

        public void SetState(FSMStateBase state)
        {
            if (Fsm.CurrentState == state) return;
            _isSettingState = true;
            Fsm.SetState(state);
            _isSettingState = false;
        }

        public bool CanSetState() => _isSettingState;
    }

    #endregion

    #region Harmony Patches

    [HarmonyPatch(typeof(Wolf2FSM), "SetState")]
    public static class Wolf2FSM_SetState_Patch
    {
        public static bool Prefix(Wolf2FSM __instance, FSMStateBase state)
        {
            if (__instance.TryGetComponent<WolfPet>(out var pet))
            {
                // Always allow commanded states, death, or any state during the Attack command
                return pet.CanSetState() || state == __instance.dead || pet.Command == PetCommand.Attack;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SenseComponent), "FindTarget")]
    public static class SenseComponent_FindTarget_Patch
    {
        public static bool Prefix(SenseComponent __instance, ref BaseEntity target, ref bool __result)
        {
            if (__instance.TryGetComponent<WolfPet>(out var pet))
            {
                if (pet.Command == PetCommand.Attack && pet.AttackTarget != null && !pet.AttackTarget.IsDead())
                {
                    target = pet.AttackTarget;
                    __result = true;
                }
                else
                {
                    target = null;
                    __result = false;
                }
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Wolf2FSM), "Hurt")]
    public static class Wolf2FSM_Hurt_Patch
    {
        public static bool Prefix(Wolf2FSM __instance) => !__instance.TryGetComponent<WolfPet>(out _);
    }

    [HarmonyPatch(typeof(Wolf2FSM), "Intimidate")]
    public static class Wolf2FSM_Intimidate_Patch
    {
        public static bool Prefix(Wolf2FSM __instance) => !__instance.TryGetComponent<WolfPet>(out _);
    }

    [HarmonyPatch(typeof(Wolf2FSM), "OnDied")]
    public static class Wolf2FSM_OnDied_Patch
    {
        public static void Postfix(Wolf2FSM __instance)
        {
            if (__instance.TryGetComponent<WolfPet>(out var pet))
            {
                UnityEngine.Object.Destroy(pet);
            }
        }
    }

    [HarmonyPatch(typeof(State_ApproachFood), "OnStateEnter")]
    public static class State_ApproachFood_Patch
    {
        public static bool Prefix(State_ApproachFood __instance, ref EFSMStateStatus __result)
        {
            if (__instance.Owner != null && __instance.Owner.TryGetComponent<WolfPet>(out _))
            {
                __result = EFSMStateStatus.Failure;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(State_EatFood), "OnStateEnter")]
    public static class State_EatFood_Patch
    {
        public static bool Prefix(State_EatFood __instance, ref EFSMStateStatus __result)
        {
            if (__instance.Owner != null && __instance.Owner.TryGetComponent<WolfPet>(out _))
            {
                __result = EFSMStateStatus.Failure;
                return false;
            }
            return true;
        }
    }

    #endregion
}
