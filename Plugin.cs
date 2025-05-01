using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    public static ConfigEntry<KeyboardShortcut> DeleteLaddersKeybind;
    public static ConfigEntry<KeyboardShortcut> DeleteAllLaddersKeybind;
    public static ConfigEntry<KeyboardShortcut> RainKeybind;
    public static ConfigEntry<KeyboardShortcut> DeleteRainToggle;
    public static bool DeleteRainState = true;
    public static ConfigEntry<bool> HoldMode;
    class Option
    {
        /// <summary>
        /// The radius to rain ladders within.
        /// </summary>
        public static Option RAIN_RADIUS = new("Rain Radius", 0, 25, 500);
        /// <summary>
        /// The number of ladders spawned at a time in rain
        /// </summary>
        public static Option RAIN_COUNT = new("Rain Count", 0, 25, null);
        /// <summary>
        /// The height the ladders spawn above the target location. If this is 0, the ladders will spawn at the target location.
        /// </summary>
        public static Option RAIN_DISTANCE = new("Rain Distance", 0, 200, null);
        /// <summary>
        /// The size of spawned ladders
        /// </summary>
        public static Option LADDER_SIZE = new("Ladder Size", 1, 1, 100);
        /// <summary>
        /// The name of this option, displayed to players
        /// </summary>
        public readonly string name;
        /// <summary>
        /// The minimum value that this option can have. If null, the value does not have a lower bound.
        /// </summary>
        public readonly int? minValue;
        // TODO allow float values
        /// <summary>
        /// The current value of this option
        /// </summary>
        public int Value { get; set; }
        /// <summary>
        /// The maximum value that this option can have. If null, the value does not have an upper bound.
        /// </summary>
        public readonly int? maxValue;
        public Option(string name, int? minValue, int defaultValue, int? maxValue)
        {
            this.name = name;
            this.Value = defaultValue;
            this.minValue = minValue;
            this.maxValue = maxValue;
        }
        /// <summary>
        /// Increment this setting's value by 1
        /// </summary>
        /// <param name="self"></param>
        /// <returns>self</returns>
        public static Option operator +(Option self)
        {
            self.Value++;
            if (self.maxValue.HasValue && self.maxValue < self.Value)
            {
                self.Value = self.maxValue.Value;
            }
            return self;
        }
        /// <summary>
        /// Decrement this setting's value by 1
        /// </summary>
        /// <param name="self"></param>
        /// <returns>self</returns>
        public static Option operator -(Option self)
        {
            self.Value--;
            if (self.minValue.HasValue && self.minValue > self.Value)
            {
                self.Value = self.minValue.Value;
            }
            return self;
        }
        /// <summary>
        /// Increment this setting's value by a specified amount
        /// </summary>
        /// <param name="self"></param>
        /// <returns>self</returns>
        public static Option operator +(Option self, int amount)
        {
            self.Value += amount;
            if (self.maxValue.HasValue && self.maxValue < self.Value)
            {
                self.Value = self.maxValue.Value;
            }
            return self;
        }
        /// <summary>
        /// Decrement this setting's value by a specified amount
        /// </summary>
        /// <param name="self"></param>
        /// <returns>self</returns>
        public static Option operator -(Option self, int amount)
        {
            self.Value -= amount;
            if (self.minValue.HasValue && self.minValue > self.Value)
            {
                self.Value = self.minValue.Value;
            }
            return self;
        }
    }

    private static readonly Option[] options = [
        Option.RAIN_RADIUS,
        Option.RAIN_COUNT,
        Option.RAIN_DISTANCE,
        Option.LADDER_SIZE
        ];
    private static int selectedIndex = 0;
    public static ConfigEntry<KeyboardShortcut> IncreaseOption;
    public static ConfigEntry<KeyboardShortcut> DecreaseOption;
    public static ConfigEntry<KeyboardShortcut> NextOption;
    public static ConfigEntry<KeyboardShortcut> PreviousOption;

    private static readonly List<GameObject> spawnedLadders = [];
    private static readonly List<GameObject> rainingLadders = [];

#pragma warning disable IDE0051 // Remove unused private members
    /// <summary>
    /// Called on mod initialization
    /// </summary>
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

        DeleteLaddersKeybind = Config.Bind(
            "Spawn",
            "Despawn Ladder Hotkey",
            new KeyboardShortcut(KeyCode.Delete, [KeyCode.None]),
            new ConfigDescription("Delete spawned ladders")
        );

        DeleteAllLaddersKeybind = Config.Bind(
            "Spawn",
            "Despawn All Ladders Hotkey",
            new KeyboardShortcut(KeyCode.Delete, [KeyCode.LeftShift]),
            new ConfigDescription("Delete spawned ladders")
        );

        HoldMode = Config.Bind(
            "Spawn",
            "Hold Mode",
            false,
            new ConfigDescription("Spawn ladders constantly while key is down")
        );

        RainKeybind = Config.Bind(
            "Rain",
            "Rain Hotkey",
            new KeyboardShortcut(KeyCode.R, [KeyCode.None]),
            new ConfigDescription("Rain ladders from the sky")
        );

        NextOption = Config.Bind(
            "Rain",
            "Select Next Option",
            new KeyboardShortcut(KeyCode.RightArrow),
            new ConfigDescription("Select next option")
        );

        PreviousOption = Config.Bind(
            "Settings",
            "Select Previous Option",
            new KeyboardShortcut(KeyCode.LeftArrow),
            new ConfigDescription("Select previous option")
        );

        IncreaseOption = Config.Bind(
            "Rain",
            "Increase Selected Value",
            new KeyboardShortcut(KeyCode.UpArrow),
            new ConfigDescription("Increase selected value")
        );

        DecreaseOption = Config.Bind(
            "Rain",
            "Decrease Selected Value",
            new KeyboardShortcut(KeyCode.DownArrow),
            new ConfigDescription("Decrease selected value")
        );

        DeleteRainToggle = Config.Bind(
            "Rain",
            "Toggle Draining Hotkey",
            new KeyboardShortcut(KeyCode.End),
            new ConfigDescription("Toggle deleting ladders when they land")
        );
    }
    /// <summary>
    /// Called every frame
    /// </summary>
    private void Update()
    {
        ProcessKeybinds();
        if (DeleteRainState)
        {
            DeleteGroundedRainedLadders();
        }
    }
#pragma warning restore IDE0051 // Remove unused private members


    /// <summary>
    /// Delete all rained ladders that are out of the map or not moving.
    /// </summary>
    public static void DeleteGroundedRainedLadders()
    {
        for (int i = rainingLadders.Count - 1; i >= 0; i--)
        {
            GameObject current = rainingLadders[i];
            if (current == null)
            {

                Logger.LogInfo(MethodBase.GetCurrentMethod().Name + ": Ladder was null???");
                rainingLadders.RemoveAt(i);
            }
            else
            {
                Vector3 position = current.GetComponent<Rigidbody>().position;
                Vector3 velocity = current.GetComponent<Rigidbody>().velocity;
                float vertical_velocity = velocity.y;
                if (System.Math.Abs(vertical_velocity) < 0.1)
                {
                    Destroy(rainingLadders[i]);
                    rainingLadders.RemoveAt(i);
                }
                else if (position.y < -100)
                {
                    Destroy(rainingLadders[i]);
                    rainingLadders.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>
    /// Handle keybindings/hotkeys.
    /// </summary>
    public void ProcessKeybinds()
    {
        if (FsmVariables.GlobalVariables.GetFsmBool("InChat").Value)
        {
            return;
        }

        if (HoldMode.Value == false)
        {
            if (Input.GetKeyDown(LadderKeybind.Value.MainKey))
            {
                SpawnLadderAtTarget();
            }
        }
        else
        {
            if (Input.GetKey(LadderKeybind.Value.MainKey))
            {
                SpawnLadderAtTarget();
            }
        }

        if (Input.GetKey(RainKeybind.Value.MainKey))
        {
            RainLadder();
        }

        //Rain settings
        if (Input.GetKeyDown(DecreaseOption.Value.MainKey))
        {
            DecrementOption();
        }
        if (Input.GetKeyDown(IncreaseOption.Value.MainKey))
        {
            IncrementOption();
        }
        if (Input.GetKeyDown(PreviousOption.Value.MainKey))
        {
            SelectPreviousOption();

        }
        if (Input.GetKeyDown(NextOption.Value.MainKey))
        {
            SelectNextOption();
        }

        //Deletion keybinds
        if (DeleteRainToggle.Value.IsDown())
        {
            DeleteRainState = !DeleteRainState;
            GameCanvas.Instance.CreateCanvasNotification("`Ladder draining: " + (DeleteRainState ? "ON" : "OFF"));
        }
        else if (DeleteAllLaddersKeybind.Value.IsDown())
        {
            DeleteAllLadders();
        }
        else if (Input.GetKeyDown(DeleteLaddersKeybind.Value.MainKey))
        {
            DeleteSpawnedLadders();
        }
    }
    /// <summary>
    /// Select the next option. If this is called at the end of the list (selectedIndex = options.Count() -1), it will wrap around and select the first element of the list.
    /// </summary>
    /// <param name="notify">Whether to display a notification on the hud</param>
    public static void SelectNextOption(bool notify = true)
    {
        selectedIndex++;
        if (selectedIndex >= options.Count())
        {
            selectedIndex = 0;
        }
        if (notify)
        {
            GameCanvas.Instance.CreateCanvasNotification(System.String.Format($"`Selected {options[selectedIndex].name}"));
        }
    }
    /// <summary>
    /// Select the previous option. If this is called at the start of the list (selectedIndex = 0), it will wrap around and select the last element of the list.
    /// </summary>
    /// <param name="notify">Whether to display a notification on the hud</param>
    public static void SelectPreviousOption(bool notify = true)
    {
        selectedIndex--;
        if (selectedIndex < 0)
        {
            selectedIndex = options.Count() - 1;
        }
        if (notify)
        {
            GameCanvas.Instance.CreateCanvasNotification(System.String.Format($"`Selected {options[selectedIndex].name}"));
        }
    }
    /// <summary>
    /// Increase the currently selected value
    /// </summary>
    /// <param name="notify">Whether to display a notification on the hud</param>
    public static void IncrementOption(bool notify = true)
    {
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.LeftControl))
        {
            options[selectedIndex] += 100;
        }
        else if (Input.GetKey(KeyCode.LeftShift))
        {
            options[selectedIndex] += 10;
        }
        else
        {
            options[selectedIndex] += 1;
        }
        if (notify)
        {
            GameCanvas.Instance.CreateCanvasNotification(System.String.Format($"`{options[selectedIndex].name}: {options[selectedIndex].Value}"));
        }
    }
    /// <summary>
    /// Decrease the currently selected value
    /// </summary>
    /// <param name="notify">Whether to display a notification on the hud</param>
    public static void DecrementOption(bool notify = true)
    {
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.LeftControl))
        {
            options[selectedIndex] -= 100;
        }
        else if (Input.GetKey(KeyCode.LeftShift))
        {
            options[selectedIndex] -= 10;
        }
        else
        {
            options[selectedIndex] -= 1;
        }
        if (notify)
        {
            GameCanvas.Instance.CreateCanvasNotification(System.String.Format($"`{options[selectedIndex].name}: {options[selectedIndex].Value}"));
        }
    }

    /// <summary>
    /// Delete all ladders. This is a backup option if some ladders aren't tracked, filtering through all GameObjects to delete any ladders.
    /// </summary>
    public static void DeleteAllLadders()
    {
        Object[] objects = FindObjectsByType(GameData.Instance.GetComponent<NetworkSpawner>().props[8].GetType(), FindObjectsSortMode.None);

        foreach (Object obj in objects)
        {
            if (obj.name.Equals("8_Ladder(Clone)"))
            {
                Destroy(obj);
            }
        }
        rainingLadders.Clear();
        spawnedLadders.Clear();
    }

    /// <summary>
    /// Delete ladders spawned using the "Spawn Ladder" keybind.
    /// </summary>
    public static void DeleteSpawnedLadders()
    {
        for (int i = spawnedLadders.Count - 1; i >= 0; i--)
        {
            Destroy(spawnedLadders[i]);
            spawnedLadders.RemoveAt(spawnedLadders.Count - 1);
        }
        for (int i = rainingLadders.Count - 1; i >= 0; i--)
        {
            Destroy(rainingLadders[i]);
            rainingLadders.RemoveAt(rainingLadders.Count - 1);
        }
    }

    /// <summary>
    /// Spawn a ladder at the player's target location.
    /// </summary>
    public static void SpawnLadderAtTarget()
    {
        Vector3 targetLocation;
        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out var hitInfo, 4f))
        {
            targetLocation = hitInfo.point + hitInfo.normal.normalized * 0.5f;
        }
        else
        {
            targetLocation = Camera.main.transform.position + Camera.main.transform.forward * 3.5f;
        }
        GameObject obj = Object.Instantiate(GameData.Instance.GetComponent<NetworkSpawner>().props[8], targetLocation, Quaternion.Euler(new Vector3(9f, 0f, 0f)));

        spawnedLadders.Add(obj);
        obj.transform.SetParent(GameData.Instance.GetComponent<NetworkSpawner>().levelPropsOBJ.transform.GetChild(5));
        obj.transform.localScale = new(Option.LADDER_SIZE.Value, Option.LADDER_SIZE.Value, Option.LADDER_SIZE.Value);
        Mirror.NetworkServer.Spawn(obj);
    }
    public static void RainLadder()
    {
        Vector3 targetLocation;
        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out var hitInfo, 1000f))
        {
            targetLocation = hitInfo.point + hitInfo.normal.normalized * 0.5f;
        }
        else
        {
            targetLocation = Camera.main.transform.position + Camera.main.transform.forward * 100f;
        }
        for (int i = 0; i < Option.RAIN_COUNT.Value; i++)
        {
            Vector3 tempSpawn = targetLocation;
            float spawnDistance = Random.Range(-Option.RAIN_RADIUS.Value, Option.RAIN_RADIUS.Value);
            float spawnAngle = Random.Range(0f, (float)System.Math.PI);
            tempSpawn.x += (float)(spawnDistance * System.Math.Cos(spawnAngle));
            tempSpawn.z += (float)(spawnDistance * System.Math.Sin(spawnAngle));
            tempSpawn.y += Option.RAIN_DISTANCE.Value;
            GameObject obj = Object.Instantiate(GameData.Instance.GetComponent<NetworkSpawner>().props[8], tempSpawn, Quaternion.Euler(new Vector3(9f, 0f, 0f)));
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            Vector3 velocity = rb.velocity;
            velocity.y = -9.8f;
            rb.velocity = velocity;
            obj.transform.localScale = new(Option.LADDER_SIZE.Value, Option.LADDER_SIZE.Value, Option.LADDER_SIZE.Value);
            rainingLadders.Add(obj);
            obj.transform.SetParent(GameData.Instance.GetComponent<NetworkSpawner>().levelPropsOBJ.transform.GetChild(5));
            Mirror.NetworkServer.Spawn(obj);

        }
    }
}
