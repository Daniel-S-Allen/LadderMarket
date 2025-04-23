using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HutongGames.PlayMaker;
using UnityEngine;

namespace LadderMarket;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class LadderPlugin : BaseUnityPlugin
{
    public static LadderPlugin instance;
    internal static new ManualLogSource Logger;
    public static ConfigEntry<KeyboardShortcut> LadderKeybind;
    public static ConfigEntry<bool> HoldMode;
    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        instance = this;
        
        LadderKeybind = Config.Bind(
            "Spawn",
            "Ladder Hotkey",
            new KeyboardShortcut(KeyCode.L),
            new ConfigDescription("Key to spawn a ladder in front of you."));

        HoldMode = Config.Bind(
            "Spawn",
            "Hold Mode",
            false,
            new ConfigDescription("Spawn ladders constantly while key is down")
        );
    }

    private void Update() {
        if (FsmVariables.GlobalVariables.GetFsmBool("InChat").Value) {
            return;
        } else {
            if(HoldMode.Value == false){
                if (Input.GetKeyDown(LadderKeybind.Value.MainKey)) {
                    SpawnLadder();
                }
            } else {
                if (Input.GetKey(LadderKeybind.Value.MainKey)) {
                    SpawnLadder();
                }
            }
        }
    }

    public static void SpawnLadder(){
        Vector3 spawnLocation = Vector3.zero;
        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out var hitInfo, 4f))
        {
            spawnLocation = hitInfo.point + hitInfo.normal.normalized * 0.5f;
        }
        else
        {
            spawnLocation = Camera.main.transform.position + Camera.main.transform.forward * 3.5f;
        }
        GameData.Instance.GetComponent<NetworkSpawner>().CmdSpawnProp(8, spawnLocation, new Vector3(9f, 0f, 0f));
        Logger.LogInfo($"Spawned ladder!");
    }
}
