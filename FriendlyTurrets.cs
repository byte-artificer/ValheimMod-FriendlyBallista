using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace FriendlyTurrets
{
    [BepInPlugin("ByteArtificer.FriendlyTurrets", "Friendly Ballista", "1.0.1")]
    [BepInProcess("valheim.exe")]
    public class FriendlyTurrets : BaseUnityPlugin
    {
        private readonly Harmony harmony = new Harmony("ByteArtificer.FriendlyTurrets");
        public static ManualLogSource logger;

        public void Awake()
        {
            logger = Logger;
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(Turret), nameof(Turret.UpdateTarget))]
        public static class Turret_UpdateTarget_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                logger.LogMessage("Friendly turrets patching...");
                var foundClosestCreatureCall = false;
                
                var codes = new List<CodeInstruction>(instructions);
                for (var j = 0; j < codes.Count; j++)
                {
                    var code = codes[j];

                    if (code.opcode != OpCodes.Call)
                        continue;

                    var methodInfo = code.operand as MethodInfo;

                    if (methodInfo == null)
                        continue;

                    if (methodInfo.Name == nameof(BaseAI.FindClosestCreature))
                    {
                        logger.LogMessage($"Patching Successful! {j}");
                        foundClosestCreatureCall = true;
                        codes[j] = new CodeInstruction(OpCodes.Call, typeof(FriendlyTurrets).GetMethod(nameof(FriendlyTurrets.FindClosestPlayerUnfriendlyCreature)));
                        break;
                    }
                }

                if (!foundClosestCreatureCall)
                    logger.LogWarning("Couldn't find IL call to replace");

                return codes.AsEnumerable();
            }
        }

        [HarmonyPatch(typeof(Turret), nameof(Turret.ShootProjectile))]
        public static class Turret_ShootProjectile_Prefix
        {
            static void Prefix(ref Character ___m_target, ref ZNetView ___m_nview)
            {
                if (IsValidTarget(___m_target, true))
                    return;

                logger.LogError("Firing on illegal target, destorying turret ammo for safety.");
                ___m_nview.GetZDO().Set("ammo", 0);
            }
        }

        public static Character FindClosestPlayerUnfriendlyCreature(Transform me, Vector3 eyePoint, float hearRange, float viewRange, float viewAngle, bool alerted, bool mistVision, bool includePlayers = true, bool includeTamed = true, List<Character> onlyTargets = null)
        {
            List<Character> allCharacters = Character.GetAllCharacters();
            Character character = null;
            float num = 99999f;
            foreach (Character item in allCharacters)
            {
                if (onlyTargets != null && onlyTargets.Count > 0)
                {
                    bool flag = false;
                    foreach (Character onlyTarget in onlyTargets)
                    {
                        if (item.m_name == onlyTarget.m_name)
                        {
                            flag = true;
                            break;
                        }
                    }

                    if (!flag)
                    {
                        continue;
                    }
                }

                if (!IsValidTarget(item, false))
                {
                    continue;
                }

                BaseAI baseAI = item.GetBaseAI();
                if ((!(baseAI != null) || !baseAI.IsSleeping()) && BaseAI.CanSenseTarget(me, eyePoint, hearRange, viewRange, viewAngle, alerted, mistVision, item))
                {
                    float num2 = Vector3.Distance(item.transform.position, me.position);
                    if (num2 < num || character == null)
                    {
                        character = item;
                        num = num2;
                    }
                }
            }

            return character;
        }

        static bool IsValidTarget(Character item, bool log)
        {
            if (item.IsDead())
            {
                if(log)
                    logger.LogMessage($"{item} is dead");
                return false;
            }
            if (item is Player)
            {
                if (log)
                    logger.LogMessage($"{item} is player");
                return false;
            }
            if (item.IsTamed())
            {
                if (log)
                    logger.LogMessage($"{item} is tamed creature");
                return false;
            }

            if (item.GetComponents<Growup>().Any())
            {
                if (log)
                    logger.LogMessage($"{item} is baby creature");
                return false;
            }
            return true;
        }

    }
}