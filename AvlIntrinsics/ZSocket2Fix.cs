using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
//using Jotunn.Utils;
//using Jotunn.Managers;
using UnityEngine.UI;
using TMPro;
using BepInEx.Logging;

namespace AvlIntrinsics
{
    internal class ZSocket2Fix
    {
        public static bool m_connectUsingTCP = false;

        private static GameObject ZS2Button;

        // Update "Connect with TCP" button
        [HarmonyPatch(typeof(ServerListGui))]
        class ServerListPatch
        {
            [HarmonyPostfix]
            //[HarmonyPatch(nameof(ZNet.Awake))]
            [HarmonyPatch(nameof(ServerListGui.OnEnable))]
            public static void OnEnablePostfix(ref ServerListGui __instance)
            {
                if (ZS2Button != null)
                {
                    // already initialized
                    return;
                }

                m_connectUsingTCP = false;

                var joinButton = __instance.m_joinGameButton;

                // Scale it down first and move up
                var rect = joinButton.GetComponent<RectTransform>();

                var delta = rect.sizeDelta; //.y *= 0.5;
                rect.sizeDelta = new Vector2(delta.x, delta.y * 0.55f);

                joinButton.transform.position += new Vector3(0, 17);

                Main.LOGGER.LogInfo("OnEnablePostfix_4");

                // TODO create a copy of 
                ZS2Button = UnityEngine.Object.Instantiate(joinButton.gameObject, joinButton.transform.parent);
                ZS2Button.GetComponent<Button>().onClick.AddListener(() =>
                {
                    // connect using TCP
                    ZLog.LogWarning("Connecting over TCP...");
                    m_connectUsingTCP = true;
                    FejdStartup.instance.OnJoinStart();
                });

                ZS2Button.transform.position = new Vector2(rect.position.x, rect.position.y - rect.rect.height);

                ZS2Button.SetActive(true);

                ZS2Button.name = "ZS2Button";

                ZS2Button.GetComponentInChildren<TextMeshProUGUI>().text = "Connect with TCP";
            }

            [HarmonyPostfix]
            [HarmonyPatch(nameof(ServerListGui.UpdateButtons))]
            static void UpdateButtonsPostfix(ref ServerListGui __instance)
            {
                // if this is the Awake() initial update, ignore
                if (ZS2Button == null)
                {
                    return;
                }

                int selectedServer = __instance.GetSelectedServer();
                bool isSelected = selectedServer >= 0;

                ZS2Button.GetComponent<Button>().interactable = isSelected;
            }
        }

        [HarmonyPatch(typeof(DLCMan))]
        class DLCManPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch(nameof(DLCMan.CheckDLCsSTEAM))]
            static bool CheckDLCsSTEAMPrefix(ref DLCMan __instance)
            {
                return !Main.m_disableSteamSetting.Value;
            }
        }

        [HarmonyPatch(typeof(FejdStartup))]
        class FejdStartupPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch(nameof(FejdStartup.TransitionToMainScene))]
            static void TransitionToMainScenePrefix(ref FejdStartup __instance)
            {
                if (ZNet.m_isServer || m_connectUsingTCP)
                {
                    ZNet.m_onlineBackend = OnlineBackendType.CustomSocket;
                }
            }

            [HarmonyPrefix]
            [HarmonyPatch(nameof(FejdStartup.InitializeSteam))]
            static bool InitializeSteamPrefix(ref bool __result)
            {
                if (Main.m_disableSteamSetting.Value)
                {
                    __result = true;
                }
                return !Main.m_disableSteamSetting.Value;
            }

            [HarmonyPrefix]
            [HarmonyPatch(nameof(FejdStartup.AwakePlayFab))]
            static bool AwakePlayfabPrefix(ref bool __result)
            {
                if (Main.m_disableSteamSetting.Value)
                {
                    __result = true;
                }
                return !Main.m_disableSteamSetting.Value;
            }
        }

        // The devs decided to make everything overly convoluted with Splatform... virtuals-"everythings" make tracking difficult
        /*
        [HarmonyPatch(typeof(FileHelpers))]
        class FileHelpersPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch(nameof(FileHelpers.UpdateCloudEnabledStatus))]
            static bool UpdateCloudEnabledStatusPrefix()
            {
                return !m_disableSteamSetting.Value;
            }
        }
        */

        [HarmonyPatch(typeof(SteamManager))]
        class SteamManagerPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch(nameof(SteamManager.Initialize))]
            static bool InitializePrefix(ref bool __result)
            {
                if (Main.m_disableSteamSetting.Value)
                {
                    __result = true;
                }
                return !Main.m_disableSteamSetting.Value;
            }

            [HarmonyPrefix]
            [HarmonyPatch(nameof(SteamManager.Initialized), MethodType.Getter)]
            static bool InitializedPrefix(ref bool __result)
            {
                if (Main.m_disableSteamSetting.Value)
                {
                    __result = true;
                }
                return !Main.m_disableSteamSetting.Value;
            }

        }

        public static void dump_instructions(IEnumerable<CodeInstruction> instructions)
        {
            for (int i = 0; i < instructions.Count(); i++)
            {
                var inst = instructions.ElementAt(i);
                var opr = inst.operand;

                string result = opr?.GetType()?.ToString() ?? "";

                var info = opr as MethodInfo;
                if (info != null)
                {
                    result += ", " + info.Name + ", " + info.Module?.FullyQualifiedName;
                }

                ZLog.Log(i + ": " + inst.opcode + " | "
                    + opr + " ||| " + result);

                //ZLog.LogWarning(i + ": " + inst.opcode + " | " 
                //    + opr + ", " + opr?.GetType()?.ToString() + " | " + (opr?.GetType()?.IsSubclassOf(typeof(MethodInfo)) ? opr. : ""));
            }
        }

        //static void my_test_caller_do_not_use()
        //{
        //    bool status = GameServer.Init(m_serverBindAddressIPv4, 0, 2456, 2456 + 1, EServerMode.eServerModeNoAuthentication, "1.0.0.0");
        //    if (!status)
        //        throw new Exception("bad");
        //}
        //
        //// TODO detemine whether return is required to be passed 
        //static bool AwakeDelegate(uint unIP, ushort usSteamPort, ushort usGamePort, ushort usQueryPort, EServerMode eServerMode, string pchVersionString)
        //{
        //    return GameServer.Init(m_serverBindAddressIPv4, usSteamPort, usGamePort, usQueryPort, eServerMode, pchVersionString);
        //}

        //THANKS DEVS!!! I CANT PROPERLY MAINTAIN MY CODE SO THIS IS THE REWWRD!!
        //[HarmonyPatch(typeof(PrivilegeManager))]
        //class PrivilegeManagerPatch
        //{
        //    [HarmonyPrefix]
        //    [HarmonyPatch(nameof(PrivilegeManager.CanAccessOnlineMultiplayer), MethodType.Getter)]
        //    static bool CanAccessOnlineMultiplayerPrefix(ref bool __result)
        //    {
        //        if (m_disableSteamSetting.Value)
        //        {
        //            __result = true;
        //        }
        //        return !m_disableSteamSetting.Value;
        //    }
        //}

        [HarmonyPatch(typeof(ZNet))]
        class ZNetPatch
        {
            [HarmonyTranspiler]
            [HarmonyPatch(nameof(ZNet.Awake))]
            static IEnumerable<CodeInstruction> AwakeTranspiler(IEnumerable<CodeInstruction> instructions)
            {
                var match = new CodeMatcher(instructions);

                //ZLog.Log("Original ZNet.Awake");
                //dump_instructions(instructions);

                if (Main.m_disableSteamSetting.Value)
                {
                    // Force remove any client match for GetPersonaName
                    match = match
                        // Removes SteamFriends.GetPersonaName
                        .MatchForward(useEnd: false, new CodeMatch(ci => ci.opcode.Equals(OpCodes.Call) && ci.operand.ToString().Contains(nameof(Steamworks.SteamFriends.GetPersonaName))));

                    if (match.IsValid)
                        match = match.SetAndAdvance(OpCodes.Ldstr, "nosteam");
                    else
                        match = match.Start();

                    match = match
                        // Prevent openserver clause
                        .MatchForward(useEnd: false, new CodeMatch(OpCodes.Ldsfld, typeof(ZNet).GetField(nameof(ZNet.m_openServer))))
                        .SetInstruction(new CodeInstruction(OpCodes.Ldc_I4_0));
                }
                return match
                    // postfix
                    .End()
                    .Insert(new CodeInstruction(
                        OpCodes.Call,
                        Transpilers.EmitDelegate<Action>(ZNetAwakePostfixDelegate).operand)
                    )
                    .InstructionEnumeration();
            }

            [HarmonyTranspiler]
            [HarmonyPatch(nameof(ZNet.StopAll))]
            static IEnumerable<CodeInstruction> StopAllTranspiler(IEnumerable<CodeInstruction> instructions)
            {
                if (Main.m_disableSteamSetting.Value)
                {
                    // TODO 
                    // More precise, testing needed
                    //return new CodeMatcher(instructions)
                    //    .MatchForward(useEnd: false, 
                    //        new CodeMatch(
                    //            OpCodes.Call,   
                    //            typeof(ZSteamMatchmaking).GetMethod(nameof(ZSteamMatchmaking.instance))),
                    //        new CodeMatch(
                    //            OpCodes.Callvirt,
                    //            typeof(ZSteamMatchmaking).GetMethod(nameof(ZSteamMatchmaking.ReleaseSessionTicket))),
                    //        new CodeMatch(
                    //            OpCodes.Call,
                    //            typeof(ZSteamMatchmaking).GetMethod(nameof(ZSteamMatchmaking.instance))),
                    //        new CodeMatch(
                    //            OpCodes.Callvirt,
                    //            typeof(ZSteamMatchmaking).GetMethod(nameof(ZSteamMatchmaking.UnregisterServer))))
                    //    .RemoveInstructions(4)
                    //    .InstructionEnumeration();

                    return new CodeMatcher(instructions)
                        .MatchForward(useEnd: false, new CodeMatch(OpCodes.Callvirt, typeof(ZSteamMatchmaking).GetMethod(nameof(ZSteamMatchmaking.ReleaseSessionTicket))))
                        // TODO add a MatchBackward to remove, instead of flat removing
                        .Advance(-1)
                        .RemoveInstructions(4)
                        .InstructionEnumeration();
                }
                return instructions;
            }

            [HarmonyPrefix]
            [HarmonyPatch(nameof(ZNet.Connect), new[] { typeof(string), typeof(int) })]
            static void ConnectPrefix(ref ZNet __instance, ref string host, ref int port)
            {
                var idx = host.IndexOf(':');
                if (idx > -1)
                {
                    // host for some reason has the port attached to the end
                    host = host.Substring(0, idx);
                    ZLog.LogWarning("new trimmed host: " + host);
                }
            }

            [HarmonyTranspiler]
            [HarmonyPatch(nameof(ZNet.SendPeerInfo))]
            static IEnumerable<CodeInstruction> SendPeerInfoTranspiler(IEnumerable<CodeInstruction> instructions)
            {
                if (Main.m_disableSteamSetting.Value)
                {
                    return new CodeMatcher(instructions)
                        .MatchForward(useEnd: false, new CodeMatch(OpCodes.Callvirt, typeof(ZSteamMatchmaking).GetMethod(nameof(ZSteamMatchmaking.RequestSessionTicket))))
                        // TODO add a MatchBackward to target get_instance and remove from there, instead of removing flat instructions
                        .Advance(-2)
                        .RemoveInstructions(3)

                        // instantiate an empty array in place of RequestSessionTicket call
                        .InsertAndAdvance(
                            new CodeInstruction(OpCodes.Ldc_I4_0), // Of size 0
                            new CodeInstruction(OpCodes.Newarr, typeof(byte)) // new byte[]
                        )

                        .InstructionEnumeration();
                }
                return instructions;
            }

            /*
            [HarmonyPatch(typeof(ZConnector2))]
            class ZConnector2Patch
            {
                [HarmonyPrefix]
                [HarmonyPatch(nameof(ZConnector2.OnHostLookupDone))]
                static bool OnHostLookupDonePrefix(ref ZConnector2 __instance, ref IAsyncResult res)
                {
                    ZLog.LogWarning("ZConnector2 PRE");

                    //IPHostEntry iphostEntry = Dns.EndGetHostEntry(res);
                    //
                    //foreach (var ip in iphostEntry.AddressList)
                    //{
                    //    ZLog.LogWarning("Address: " + ip.ToString());
                    //}
                    //
                    //ZLog.LogWarning("---------");
                    //
                    //iphostEntry.AddressList = __instance.KeepInetAddrs(iphostEntry.AddressList);
                    //
                    //foreach (var ip in iphostEntry.AddressList)
                    //{
                    //    ZLog.LogWarning("Address: " + ip.ToString());
                    //}
                    //
                    //ZLog.LogError("CANCELLED PREFIX! todo remove this after");
                    //return false; //WE CANCEL for now...
                    
                    return true;// continue...
                }

                [HarmonyPostfix]
                [HarmonyPatch(nameof(ZConnector2.OnHostLookupDone))]
                static void OnHostLookupDonePostfix(ref ZConnector2 __instance, ref IAsyncResult res)
                {
                    ZLog.LogWarning("ZConnector2 POST");
                }
            }*/

            [HarmonyPrefix]
            [HarmonyPatch(nameof(ZNet.Start))]
            static void StartPrefix(ref ZNet __instance)
            {
                if (ZNet.m_isServer || m_connectUsingTCP)
                {
                    ZNet.m_onlineBackend = OnlineBackendType.CustomSocket;
                }
            }

            // Client TCP connect ONLY
            [HarmonyPostfix]
            [HarmonyPatch(nameof(ZNet.Update))]
            static void UpdatePostfix(ref ZNet __instance)
            {
                if (!ZNet.m_isServer)
                {
                    if (m_connectUsingTCP)
                    {
                        //ZLog.LogWarning("Updating TCP connector...");
                        __instance.UpdateClientConnector(Time.deltaTime);
                    }
                }
            }
        }

        // Patches TCP server ONLY
        static void ZNetAwakePostfixDelegate()
        {
            ZNet __instance = ZNet.instance;
            if (ZNet.m_isServer)
            {
                if (__instance.m_hostSocket != null)
                {
                    ZLog.LogWarning("Closing host socket: " + __instance.GetType().Name);
                    __instance.m_hostSocket.Close();
                    __instance.m_hostSocket.Dispose();
                }

                ZSocket2 socket = new ZSocket2();
                socket.StartHost(ZSteamSocket.m_steamDataPort);
                __instance.m_hostSocket = socket;
                ZLog.LogWarning("Started TCP Custom Socket");
            }
        }

        [HarmonyPatch(typeof(ZSteamMatchmaking))]
        class ZSteamMatchmakingPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch(nameof(ZSteamMatchmaking.Initialize))]
            static bool InitializePrefix()
            {
                return !Main.m_disableSteamSetting.Value;
            }
        }
    }
}