﻿using BepInEx;
using UnityEngine;
using BepInEx.Bootstrap;
using System.Reflection;
using System;
using HarmonyLib;
using SpaceCraft;
using System.Collections.Generic;
using BepInEx.Logging;
using MijuTools;
using System.Text;
using System.IO;
using BepInEx.Configuration;

namespace ExampleModLoadSaveSupportSoft
{
    [BepInPlugin(guid, "(Example) Soft Dependency on ModLoadSaveSupport", "1.0.0.1")]
    [BepInDependency(libModLoadSaveSupportGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string libModLoadSaveSupportGuid = "akarnokd.theplanetcraftermods.libmodloadsavesupport";
        const string guid = "akarnokd.theplanetcraftermods.examplemodloadsavesupportsoft";

        private IDisposable handle;

        static ManualLogSource logger;

        static ConfigEntry<bool> dumpLabels;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            dumpLabels = Config.Bind<bool>("General", "DumpLabels", false, "Dump all labels for all languages in the game?");

            // Locate the libModLoadSaveSupport plugin
            if (Chainloader.PluginInfos.TryGetValue(libModLoadSaveSupportGuid, out BepInEx.PluginInfo pi))
            {
                // locate its RegisterLoadSave method
                MethodInfo mi = pi.Instance.GetType().GetMethod("RegisterLoadSave",
                    new Type[] { typeof(string), typeof(Action<string>), typeof(Func<string>) });

                // call it with our guid and the delegates to our load and save methods
                handle = (IDisposable)mi.Invoke(pi.Instance, new object[] { guid, new Action<string>(OnLoad), new Func<string>(OnSave) });

                Logger.LogInfo("Successfully registered with " + libModLoadSaveSupportGuid);
            } else
            {
                Logger.LogInfo("Could not find " + libModLoadSaveSupportGuid);
            }

            logger = Logger;

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        void OnDestroy()
        {
            handle?.Dispose();
            handle = null;
        }

        void OnLoad(string content)
        {
            Logger.LogInfo("Executing OnLoad");
            Logger.LogInfo(content);
        }

        string OnSave()
        {
            Logger.LogInfo("Executing OnSave");
            return "ExampleModLoadSaveSupportSoft example content";
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TerraformStagesHandler), "Start")]
        static void TerraformStagesHandler_Start(List<TerraformStage> ___allGlobalTerraStage)
        {
            foreach (TerraformStage stage in ___allGlobalTerraStage)
            {
                logger.LogInfo(stage.GetTerraId() + " \"" + Readable.GetTerraformStageName(stage) + "\" @ " 
                    + string.Format("{0:##,###}", stage.GetStageStartValue()) + " " + stage.GetWorldUnitType());                
            }

            UnlockingHandler unlock = Managers.GetManager<UnlockingHandler>();

            List<List<GroupData>> tiers = new List<List<GroupData>>
            {
                unlock.tier1GroupToUnlock,
                unlock.tier2GroupToUnlock,
                unlock.tier3GroupToUnlock,
                unlock.tier4GroupToUnlock,
                unlock.tier5GroupToUnlock,
                unlock.tier6GroupToUnlock,
                unlock.tier7GroupToUnlock,
                unlock.tier8GroupToUnlock,
                unlock.tier9GroupToUnlock,
                unlock.tier10GroupToUnlock,
            };

            StringBuilder sb = new StringBuilder();
            sb.Append("\r\n");

            for (int i = 0; i < tiers.Count; i++)
            {
                List<GroupData> gd = tiers[i];
                sb.Append("Tier #").Append(i + 1).Append("\r\n");

                foreach (GroupData g in gd)
                {
                    sb.Append("- ").Append(g.id).Append("\r\n");
                }
            }

            logger.LogInfo(sb.ToString());

            ExportLocalization();
            InventoryLootStages();
            ProductionValues();
        }

        static void ExportLocalization()
        {
            if (dumpLabels.Value)
            {
                Localization.GetLocalizedString("");
                FieldInfo fi = AccessTools.Field(typeof(Localization), "localizationDictionary");
                Dictionary<string, Dictionary<string, string>> dic = (Dictionary<string, Dictionary<string, string>>)fi.GetValue(null);
                if (dic != null)
                {
                    logger.LogInfo("Found localizationDictionary");

                    foreach (KeyValuePair<string, Dictionary<string, string>> kvp in dic)
                    {
                        Dictionary<string, string> dic2 = kvp.Value;
                        if (dic2 != null)
                        {
                            logger.LogInfo("Found " + kvp.Key + " labels");
                            StringBuilder sb = new StringBuilder();
                            foreach (KeyValuePair<string, string> kv in dic2)
                            {

                                sb.Append(kv.Key).Append("=").Append(kv.Value);
                                sb.AppendLine();
                            }

                            Assembly me = Assembly.GetExecutingAssembly();
                            string dir = Path.GetDirectoryName(me.Location);
                            File.WriteAllText(dir + "\\labels." + kvp.Key + ".txt", sb.ToString());
                        }
                    }
                }
            }
        }

        static void InventoryLootStages()
        {
            var stagesLH = Managers.GetManager<InventoryLootHandler>();
            var stages = stagesLH.lootTerraStages;

            logger.LogInfo("Found " + stages.Count + " stages");
            stages.Sort((a, b) =>
            {
                float v1 = a.terraStage.GetStageStartValue();
                float v2 = b.terraStage.GetStageStartValue();
                return v1 < v2 ? -1 : (v1 > v2 ? 1 : 0);
            });
            StringBuilder sb = new StringBuilder();
            sb.AppendLine();
            foreach (InventoryLootStage ils in stages)
            {
                sb.Append(ils.terraStage.GetTerraId()).Append(" @ ").Append(ils.terraStage.GetStageStartValue()).AppendLine();

                string[] titles = { "Common", "Uncommon", "Rare", "Very Rare", "Ultra Rare" };
                List<List<GroupData>> gs = new List<List<GroupData>>()
                {
                    ils.commonItems, ils.unCommonItems, ils.rareItems, ils.veryRareItems, ils.ultraRareItems
                };

                for (int i = 0; i < titles.Length; i++)
                {
                    sb.Append("  - ").Append(titles[i]).Append(" : ");
                    bool next = false;
                    foreach (GroupData g in gs[i])
                    {
                        if (next)
                        {
                            sb.Append(",");
                        }
                        sb.Append(g.id);
                        next = true;
                    }
                    sb.AppendLine();
                }
            }
            logger.LogInfo(sb.ToString());
        }

        static void ProductionValues()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine();
            foreach(GroupConstructible gc in GroupsHandler.GetGroupsConstructible())
            {
                sb.Append(gc.GetId());
                sb.AppendLine();
                sb.Append("  Pressure: " + gc.GetGroupUnitGeneration(DataConfig.WorldUnitType.Pressure)).AppendLine();
                sb.Append("  Heat: " + gc.GetGroupUnitGeneration(DataConfig.WorldUnitType.Heat)).AppendLine();
                sb.Append("  Oxygen: " + gc.GetGroupUnitGeneration(DataConfig.WorldUnitType.Oxygen)).AppendLine();
                sb.Append("  Biomass: " + gc.GetGroupUnitGeneration(DataConfig.WorldUnitType.Biomass)).AppendLine();

            }
            logger.LogInfo(sb.ToString());
        }
    }
}
