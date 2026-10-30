using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AvlIntrinsics
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [NetworkCompatibility(CompatibilityLevel.VersionCheckOnly, VersionStrictness.Minor)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    internal class Main : BaseUnityPlugin
    {
        // BepInEx' plugin metadata
        public const string PluginGUID = "com.crzi.AvlIntrinsics";
        public const string PluginName = "AvlIntrinsics";
        public const string PluginVersion = "1.0.1";

        public const string DUMPER_OUT_PATH = "dumped/";
        public const string PKG_OUT_PATH = DUMPER_OUT_PATH + "pkg/";
        public const string DOC_OUT_PATH = DUMPER_OUT_PATH + "doc/";

        public static ConfigEntry<bool> m_disableSteamSetting;
        public static ConfigEntry<string> m_serverBindAddressSetting;

        public static uint m_serverBindAddressIPv4 = 0;
        public static IPAddress m_serverBindIpAddress = null;
        
        public static bool m_isDedicated = false;

        public static BepInEx.Logging.ManualLogSource LOGGER;

        Harmony _harmony;

        public static Main Instance;

        public void RunDumperCoroutine() {
            base.StartCoroutine(this.DelayedRunDumper());
        }

        private IEnumerator DelayedRunDumper() {
            ZRoutedRpc.instance.InvokeRoutedRPC(ZDOMan.instance.m_sessionID, "ChatMessage", new object[]
            {
                    Player.m_localPlayer.GetHeadPoint(),
                    (int)Talker.Type.Normal,
                    UserInfo.GetLocalUser(),
                    "Starting dump (game will freeze momentarily)"
            });

            yield return new WaitForSeconds(1);

            try
            {
                AvlDumper.DumpDocs();
                AvlDumper.DumpPackages();

                //MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "Run with /avl_dump confirm");
                //Chat.print("Dumping success");
                ZRoutedRpc.instance.InvokeRoutedRPC(ZDOMan.instance.m_sessionID, "ChatMessage", new object[]
                {
                        Player.m_localPlayer.GetHeadPoint(),
                        (int)Talker.Type.Normal,
                        UserInfo.GetLocalUser(),
                        "Dumping success"
                });
            }
            catch (Exception ex)
            {
                //Chat.print("Dumping exception, see console");
                ZRoutedRpc.instance.InvokeRoutedRPC(ZDOMan.instance.m_sessionID, "ChatMessage", new object[]
                {
                        Player.m_localPlayer.GetHeadPoint(),
                        (int)Talker.Type.Normal,
                        UserInfo.GetLocalUser(),
                        "Dumping failed; see logs"
                });
                throw new Exception("Exception: ", ex);
            }

            yield break;
        }

        private void Awake()
        {
            Instance = this;

            ZLog.Log("Loading AvlIntrinsics...");

            LOGGER = Logger;
            Game.isModded = true;

            m_disableSteamSetting = Config.Bind(
                "Client",
                "DisableSteam",
                false,
                "Whether to disable steam integration"
            );

            if (m_disableSteamSetting.Value)
            {
                // Because the devs keep screwing things around
                LOGGER.LogWarning("Steam modification is currently NYI");
            }

            // TODO; currently broken because many game steam changes I just cant track anymore...
            m_disableSteamSetting.Value = false;

            m_serverBindAddressSetting = Config.Bind(
                "Server",
                "ServerBindAddress",
                "0.0.0.0",
                "Custom address to bind server listen socket on"
            );

            // Determine whether environment is a dedicated headless server
            string[] commandLineArgs = Environment.GetCommandLineArgs();
            for (int i = 0; i < commandLineArgs.Length; i++)
            {
                string text3 = commandLineArgs[i].ToLower();
                if (text3 == "-password")
                {
                    m_isDedicated = true;
                }
            }

            if (m_disableSteamSetting.Value && !m_isDedicated)
            {
                ZLog.LogWarning("ZSocket2Fix disabling steam integration...");
            }

            if (m_isDedicated)
            {
                m_disableSteamSetting.Value = false;
                // ensure ip is valid

                m_serverBindIpAddress = IPAddress.Parse(m_serverBindAddressSetting.Value);

                var bytes = m_serverBindIpAddress.GetAddressBytes();

                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }

                // TODO test this:
                //  m_serverBindAddressIPv4 being set to something besides 0 when bytes is not {0, 0, 0, 0}

                m_serverBindAddressIPv4 = BitConverter.ToUInt32(bytes, 0);

                ZLog.LogWarning("Bytes + ip converted:::");
                ZLog.LogWarning(m_serverBindAddressSetting.Value);
                ZLog.LogWarning(BitConverter.ToString(bytes));
                ZLog.LogWarning(m_serverBindAddressIPv4);
            }

            CommandManager.Instance.AddConsoleCommand(new AvlCommand());
            CommandManager.Instance.AddConsoleCommand(new AvlDumpCommand());

            _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGUID);

            ZLog.Log("!!! Loaded AvlIntrinsics !!!");
        }

        private void Destroy()
        {
            _harmony?.UnpatchSelf();
        }

        // Dungeon logging tools
        [HarmonyPatch(typeof(DungeonGenerator))]
        class DungeonGeneratorPatch
        {
            [HarmonyPostfix]
            [HarmonyPatch(nameof(DungeonGenerator.Generate), new Type[] { typeof(int), typeof(ZoneSystem.SpawnMode) })]
            static void GeneratePostfix(ref DungeonGenerator __instance)
            {
                ZLog.LogWarning("Dungeon '" + __instance.name
                    + "', seed: " + __instance.m_generatedSeed
                    + ", pos: " + __instance.transform.position
                    + ", rot: " + __instance.transform.rotation
                    + ", m_zoneCenter: " + __instance.m_zoneCenter
                    + ", m_originalPosition: " + __instance.m_originalPosition
                );
            }
        }

        // ZRpc infinite timeout patch
        [HarmonyPatch(typeof(ZRpc))]
        class ZRpcPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch(nameof(ZRpc.Update))]
            static void GeneratePostfix()
            {
                ZRpc.m_timeout = 1 * 1000 * 60 * 60 * 24;
            }
        }

        // ZSteamSocket timeout patch
        [HarmonyPatch(typeof(ZSteamSocket))]
        class ZSteamSocketPatch
        {
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(ZSteamSocket.RegisterGlobalCallbacks))]
            static IEnumerable<CodeInstruction> RegisterGlobalCallbacksTranspiler(IEnumerable<CodeInstruction> instructions)
            {
                return new CodeMatcher(instructions)
                    .MatchForward(useEnd: false, new CodeMatch(OpCodes.Ldc_R4, 30000f))
                    .SetAndAdvance(OpCodes.Ldc_R4, (float)(1 * 1000 * 60 * 60 * 24))
                    .InstructionEnumeration();
            }
        }

    }

}