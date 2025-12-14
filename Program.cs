// stardew valley smapi mod that acts as a tcp server for reinforcement learning agents
// streams fishing game state data to connected python agents via json
// receives and executes agent actions for automated fishing control
// supports both hooking phase (nibble detection) and minigame phase (bar control)
// uses reflection to directly manipulate bobber bar physics for precise control
// ENHANCED: Automatic energy replenishment + Experiment controls (season, location, rod)

using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;
using StardewValley.Locations;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json; 

namespace RL_Fishing
{
    // main mod entry point - requires rlfishingdata.cs to be in the project
    public class ModEntry : Mod
    {
        // fishing state tracking
        private RLFishingData currentFishingData;
        private int inventoryCountBeforeMinigame = 0;
        
        // networking components for tcp server
        private TcpListener listener;
        private TcpClient client;
        private NetworkStream stream;
        private Thread serverThread;
        private bool isListening = true;
        
        // thread-safe action queue for agent communication
        private class AgentAction { public int Action { get; set; } = 0; public float Interval { get; set; } = 0f; }
        private volatile AgentAction receivedAction = new AgentAction();
        
        // ENERGY MANAGEMENT
        private bool enableAutoReplenish = true;
        private float energyThreshold = 50.0f;
        private float healthThreshold = 50.0f;
        private int replenishCheckInterval = 60;
        private int tickCounter = 0;
        
        // EXPERIMENT CONTROL: Quick location and rod switching
        private List<string> fishingLocations = new List<string> { "Beach", "Mountain", "Forest", "Town" };
        private int currentLocationIndex = 0;
        private int currentRodLevel = 3; // Start with Iridium Rod (0=Training, 1=Bamboo, 2=Fiberglass, 3=Iridium)
        private int currentSeasonIndex = 0; // 0=Spring, 1=Summer, 2=Fall, 3=Winter
        private int currentWeatherIndex = 0; // 0=Sunny, 1=Rainy, 2=Stormy
        private int currentTimeIndex = 0; // 0=6am, 1=12pm, 2=6pm
        
        // mod initialization
        public override void Entry(IModHelper helper)
        {
            this.currentFishingData = new RLFishingData(helper); 

            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Display.MenuChanged += this.OnMenuChanged;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            
            // start the networking server thread
            this.serverThread = new Thread(this.StartServer);
            this.serverThread.IsBackground = true;
            this.serverThread.Start();
            
            this.Monitor.Log("RL Fishing Mod Loaded! Listening for RL Agent on port 8080...", LogLevel.Info);
            this.Monitor.Log($"Auto Energy/Health Replenishment: {(enableAutoReplenish ? "ENABLED" : "DISABLED")}", LogLevel.Info);
            this.Monitor.Log("", LogLevel.Info);
            this.Monitor.Log("=== EXPERIMENT HOTKEYS ===", LogLevel.Info);
            this.Monitor.Log("P - Print fishing data", LogLevel.Info);
            this.Monitor.Log("E - Toggle energy auto-replenish", LogLevel.Info);
            this.Monitor.Log("R - Manual energy replenish", LogLevel.Info);
            this.Monitor.Log("L - Cycle location (Beach→Mountain→Forest→Town)", LogLevel.Info);
            this.Monitor.Log("K - Cycle rod type (Training→Bamboo→Fiberglass→Iridium)", LogLevel.Info);
            this.Monitor.Log("N - Cycle season (Spring→Summer→Fall→Winter)", LogLevel.Info);
            this.Monitor.Log("M - Cycle weather (Sunny→Rainy→Stormy)", LogLevel.Info);
            this.Monitor.Log("T - Cycle time (6am→12pm→6pm)", LogLevel.Info);
            this.Monitor.Log("==========================", LogLevel.Info);
        }

        // handles button presses for experiment control
        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            // === EXISTING HOTKEYS ===
            // P key: Print fishing data
            if (e.Button == SButton.P)
            {
                this.PrintFishingData();
            }
            
            // E key: Toggle auto-replenishment
            if (e.Button == SButton.E)
            {
                this.enableAutoReplenish = !this.enableAutoReplenish;
                this.Monitor.Log($"Auto Energy/Health Replenishment: {(enableAutoReplenish ? "ENABLED" : "DISABLED")}", LogLevel.Info);
            }
            
            // R key: Manual replenish now
            if (e.Button == SButton.R)
            {
                this.ReplenishPlayerStats(force: true);
                this.Monitor.Log("Manual energy/health replenishment triggered!", LogLevel.Info);
            }
            
            // === NEW EXPERIMENT CONTROL HOTKEYS ===
            
            // L key: Cycle fishing location
            if (e.Button == SButton.L)
            {
                this.CycleLocation();
            }
            
            // K key: Cycle rod type
            if (e.Button == SButton.K)
            {
                this.CycleRodType();
            }
            
            // N key: Cycle season (N for seasoN)
            if (e.Button == SButton.N)
            {
                this.CycleSeason();
            }
            
            // M key: Cycle weather (M for cliMate)
            if (e.Button == SButton.M)
            {
                this.CycleWeather();
            }
            
            // T key: Cycle time (morning/noon/evening)
            if (e.Button == SButton.T)
            {
                this.CycleTime();
            }
        }

        // Event handler for when save is loaded - add all fishing rods to inventory
        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            this.Monitor.Log("Save loaded - adding fishing rods to inventory...", LogLevel.Info);
            
            // Add all 4 fishing rod types to player inventory
            this.AddFishingRodToInventory(0); // Training Rod
            this.AddFishingRodToInventory(1); // Bamboo Pole
            this.AddFishingRodToInventory(2); // Fiberglass Rod
            this.AddFishingRodToInventory(3); // Iridium Rod
            
            this.Monitor.Log("✓ All fishing rods added to inventory!", LogLevel.Info);
        }

        // Helper method to add a fishing rod to player inventory
        private void AddFishingRodToInventory(int upgradeLevel)
        {
            // Create fishing rod with specified upgrade level
            FishingRod rod = new FishingRod(upgradeLevel);
            
            // Add to player inventory
            bool added = Game1.player.addItemToInventoryBool(rod);
            
            if (added)
            {
                this.Monitor.Log($"  Added: {GetRodName(upgradeLevel)}", LogLevel.Debug);
            }
            else
            {
                this.Monitor.Log($"  Inventory full - couldn't add {GetRodName(upgradeLevel)}", LogLevel.Warn);
            }
        }

        // === EXPERIMENT CONTROL METHODS ===
        
        private void CycleLocation()
        {
            currentLocationIndex = (currentLocationIndex + 1) % fishingLocations.Count;
            string targetLocation = fishingLocations[currentLocationIndex];
            
            // Teleport to the location
            switch (targetLocation)
            {
                case "Beach":
                    Game1.warpFarmer("Beach", 30, 30, false);
                    break;
                case "Mountain":
                    Game1.warpFarmer("Mountain", 20, 20, false);
                    break;
                case "Forest":
                    Game1.warpFarmer("Forest", 50, 50, false);
                    break;
                case "Town":
                    Game1.warpFarmer("Town", 50, 85, false);
                    break;
            }
            
            this.Monitor.Log($"✓ Teleported to: {targetLocation}", LogLevel.Info);
        }
        
        private void CycleRodType()
        {
            currentRodLevel = (currentRodLevel + 1) % 4; // 0-3
            
            // Try to find the rod with this upgrade level in inventory
            FishingRod targetRod = null;
            int targetIndex = -1;
            
            for (int i = 0; i < Game1.player.Items.Count; i++)
            {
                if (Game1.player.Items[i] is FishingRod fishingRod && fishingRod.UpgradeLevel == currentRodLevel)
                {
                    targetRod = fishingRod;
                    targetIndex = i;
                    break;
                }
            }
            
            if (targetRod != null && targetIndex >= 0)
            {
                // Equip the found rod
                Game1.player.CurrentToolIndex = targetIndex;
                this.Monitor.Log($"✓ Equipped: {GetRodName(currentRodLevel)}", LogLevel.Info);
            }
            else
            {
                // Rod not found in inventory - add it
                this.Monitor.Log($"  {GetRodName(currentRodLevel)} not in inventory, adding...", LogLevel.Warn);
                this.AddFishingRodToInventory(currentRodLevel);
                
                // Try to equip it
                for (int i = 0; i < Game1.player.Items.Count; i++)
                {
                    if (Game1.player.Items[i] is FishingRod fishingRod && fishingRod.UpgradeLevel == currentRodLevel)
                    {
                        Game1.player.CurrentToolIndex = i;
                        this.Monitor.Log($"✓ Added and equipped: {GetRodName(currentRodLevel)}", LogLevel.Info);
                        break;
                    }
                }
            }
        }
        
        private string GetRodName(int level)
        {
            switch (level)
            {
                case 0: return "Training Rod";
                case 1: return "Bamboo Pole";
                case 2: return "Fiberglass Rod";
                case 3: return "Iridium Rod";
                default: return "Unknown Rod";
            }
        }
        
        private void CycleSeason()
        {
            currentSeasonIndex = (currentSeasonIndex + 1) % 4;
            
            Season season;
            string seasonName;
            switch (currentSeasonIndex)
            {
                case 0:
                    season = Season.Spring;
                    seasonName = "Spring";
                    break;
                case 1:
                    season = Season.Summer;
                    seasonName = "Summer";
                    break;
                case 2:
                    season = Season.Fall;
                    seasonName = "Fall";
                    break;
                case 3:
                    season = Season.Winter;
                    seasonName = "Winter";
                    break;
                default:
                    season = Season.Spring;
                    seasonName = "Spring";
                    break;
            }
            
            Game1.season = season;
            this.Monitor.Log($"✓ Season: {seasonName}", LogLevel.Info);
        }
        
        private void CycleWeather()
        {
            currentWeatherIndex = (currentWeatherIndex + 1) % 3;
            
            string weatherName;
            switch (currentWeatherIndex)
            {
                case 0: // Sunny
                    Game1.isRaining = false;
                    Game1.isSnowing = false;
                    Game1.isLightning = false;
                    weatherName = "Sunny";
                    break;
                case 1: // Rainy
                    Game1.isRaining = true;
                    Game1.isSnowing = false;
                    Game1.isLightning = false;
                    weatherName = "Rainy";
                    break;
                case 2: // Stormy
                    Game1.isRaining = true;
                    Game1.isLightning = true;
                    Game1.isSnowing = false;
                    weatherName = "Stormy";
                    break;
                default:
                    Game1.isRaining = false;
                    Game1.isSnowing = false;
                    Game1.isLightning = false;
                    weatherName = "Sunny";
                    break;
            }
            
            this.Monitor.Log($"✓ Weather: {weatherName}", LogLevel.Info);
        }
        
        private void CycleTime()
        {
            currentTimeIndex = (currentTimeIndex + 1) % 3;
            
            int time;
            string timeName;
            switch (currentTimeIndex)
            {
                case 0:
                    time = 600; // 6:00 AM
                    timeName = "6:00 AM (Morning)";
                    break;
                case 1:
                    time = 1200; // 12:00 PM
                    timeName = "12:00 PM (Noon)";
                    break;
                case 2:
                    time = 1800; // 6:00 PM
                    timeName = "6:00 PM (Evening)";
                    break;
                default:
                    time = 600;
                    timeName = "6:00 AM (Morning)";
                    break;
            }
            
            Game1.timeOfDay = time;
            this.Monitor.Log($"✓ Time: {timeName}", LogLevel.Info);
        }
        
        private void SetSeason(string seasonName)
        {
            Season season;
            switch (seasonName.ToLower())
            {
                case "spring":
                    season = Season.Spring;
                    currentSeasonIndex = 0;
                    break;
                case "summer":
                    season = Season.Summer;
                    currentSeasonIndex = 1;
                    break;
                case "fall":
                    season = Season.Fall;
                    currentSeasonIndex = 2;
                    break;
                case "winter":
                    season = Season.Winter;
                    currentSeasonIndex = 3;
                    break;
                default:
                    season = Season.Spring;
                    currentSeasonIndex = 0;
                    break;
            }
            
            Game1.season = season;
            this.Monitor.Log($"✓ Season set to: {seasonName.ToUpper()}", LogLevel.Info);
        }
        
        private void SetWeather(string weather)
        {
            switch (weather.ToLower())
            {
                case "sunny":
                    Game1.isRaining = false;
                    Game1.isSnowing = false;
                    Game1.isLightning = false;
                    currentWeatherIndex = 0;
                    this.Monitor.Log("✓ Weather set to: SUNNY", LogLevel.Info);
                    break;
                case "rainy":
                    Game1.isRaining = true;
                    Game1.isSnowing = false;
                    Game1.isLightning = false;
                    currentWeatherIndex = 1;
                    this.Monitor.Log("✓ Weather set to: RAINY", LogLevel.Info);
                    break;
                case "stormy":
                    Game1.isRaining = true;
                    Game1.isLightning = true;
                    Game1.isSnowing = false;
                    currentWeatherIndex = 2;
                    this.Monitor.Log("✓ Weather set to: STORMY", LogLevel.Info);
                    break;
                case "snowy":
                    Game1.isSnowing = true;
                    Game1.isRaining = false;
                    Game1.isLightning = false;
                    this.Monitor.Log("✓ Weather set to: SNOWY", LogLevel.Info);
                    break;
            }
        }
        
        private void SetTime(int time)
        {
            Game1.timeOfDay = time;
            string timeStr = (time / 100).ToString() + ":" + (time % 100).ToString("00");
            
            // Update index to match
            if (time == 600)
                currentTimeIndex = 0;
            else if (time == 1200)
                currentTimeIndex = 1;
            else if (time == 1800)
                currentTimeIndex = 2;
            
            this.Monitor.Log($"✓ Time set to: {timeStr}", LogLevel.Info);
        }

        // debug function that lists all private fields in bobberbar
        private void ListBobberBarFields(BobberBar bobberBar)
        {
            this.Monitor.Log("--- DEBUG: BOBBER BAR PRIVATE FIELDS ---", LogLevel.Info);
            try
            {
                var fields = bobberBar.GetType().GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                foreach (var field in fields)
                {
                    this.Monitor.Log($"FIELD NAME: {field.Name} (Type: {field.FieldType.Name})", LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Failed to list fields: {ex.Message}", LogLevel.Error);
            }
            this.Monitor.Log("-----------------------------------------", LogLevel.Info);
        }
        
        // tracks minigame start and end
        private void OnMenuChanged(object sender, MenuChangedEventArgs e)
        {
            if (e.NewMenu is BobberBar)
            {
                this.inventoryCountBeforeMinigame = Game1.player.Items.Count;
                this.Monitor.Log($"Minigame started. Inventory count before: {this.inventoryCountBeforeMinigame}", LogLevel.Debug);
            }
            else if (e.OldMenu is BobberBar && e.NewMenu is null)
            {
                int currentInventoryCount = Game1.player.Items.Count;
                if (currentInventoryCount > this.inventoryCountBeforeMinigame)
                {
                    this.Monitor.Log("Minigame ended: Item caught (Inventory increase detected).", LogLevel.Info);
                }
                else
                {
                    this.Monitor.Log("Minigame ended: Fish lost or trash caught without increasing item count.", LogLevel.Info);
                }
                this.inventoryCountBeforeMinigame = 0;
            }
        }

        // ENERGY MANAGEMENT: Automatically replenish player stamina and health
        private void ReplenishPlayerStats(bool force = false)
        {
            if (!Context.IsWorldReady || Game1.player == null)
                return;
            
            bool needsReplenish = force;
            
            if (Game1.player.Stamina < energyThreshold)
            {
                needsReplenish = true;
            }
            
            if (Game1.player.health < healthThreshold)
            {
                needsReplenish = true;
            }
            
            if (needsReplenish)
            {
                Game1.player.Stamina = Game1.player.MaxStamina;
                Game1.player.health = Game1.player.maxHealth;
                
                if (force)
                {
                    this.Monitor.Log($"Replenished: Stamina={Game1.player.Stamina}/{Game1.player.MaxStamina}, Health={Game1.player.health}/{Game1.player.maxHealth}", LogLevel.Debug);
                }
            }
        }

        // core game loop handler
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            // ENERGY MANAGEMENT: Periodic stamina/health check
            if (enableAutoReplenish)
            {
                tickCounter++;
                if (tickCounter >= replenishCheckInterval)
                {
                    this.ReplenishPlayerStats();
                    tickCounter = 0;
                }
            }

            // update the environment state every tick
            this.currentFishingData.Update(); 
            
            // safely grab the latest action instruction from agent
            int action;
            float forceInjection;
            lock (receivedAction)
            {
                action = receivedAction.Action;
                forceInjection = receivedAction.Interval;
            }

            // execute the agent's chosen action
            this.ExecuteAction(action, forceInjection);
        }
        
        // action execution logic using force injection from agent
        private void ExecuteAction(int action, float forceInjection)
        {
            // phase 2: hooking the fish when nibble is detected
            if (Game1.activeClickableMenu is null && Game1.player.CurrentTool is FishingRod rod)
            {
                if (rod.isNibbling && action == 1) 
                {
                     this.PerformSingleClick();
                     this.Monitor.Log("RL Action: Hooking Fish (Single Click)", LogLevel.Debug);
                     return;
                }
            }

            // phase 3: minigame control when bobberbar is active
            if (Game1.activeClickableMenu is BobberBar bobberBar)
            {
                var buttonPressedField = this.Helper.Reflection.GetField<bool>(bobberBar, "buttonPressed", false);
                var motionField = this.Helper.Reflection.GetField<float>(bobberBar, "bobberBarSpeed", false); 

                if (action == 1)
                {
                    try 
                    { 
                        buttonPressedField?.SetValue(true); 
                        this.Monitor.Log("RL Action: Continuous Hold Flag Set (TRUE)", LogLevel.Trace);
                    }
                    catch (Exception ex) 
                    { 
                        this.Monitor.Log($"Warning: Failed to set 'buttonPressed' field: {ex.Message}", LogLevel.Trace); 
                    }

                    if (Math.Abs(forceInjection) > 0.001f)
                    {
                        try
                        {
                            if (motionField != null)
                            {
                                float currentMotion = motionField.GetValue();
                                float newMotion = currentMotion + forceInjection;
                                motionField.SetValue(newMotion);
                                this.Monitor.Log($"RL Action: Motion Injection/Boost - Current: {currentMotion:F4}, Delta: {forceInjection:F4}, New: {newMotion:F4}", LogLevel.Trace);
                            }
                            else
                            {
                                this.Monitor.Log("CRITICAL ERROR: Failed to find 'bobberBarSpeed' field for injection. Force not applied.", LogLevel.Error);
                            }
                        }
                        catch (Exception ex)
                        {
                            this.Monitor.Log($"Error during Motion Injection/Boost: {ex.Message}", LogLevel.Error);
                        }
                    }
                }
                else if (action == 0)
                {
                    try 
                    { 
                        buttonPressedField?.SetValue(false); 
                        this.Monitor.Log("RL Action: Continuous Hold Flag Set (FALSE)", LogLevel.Trace);
                    }
                    catch
                    {
                        // ignored
                    } 
                    
                    try
                    {
                        this.Helper.Reflection.GetMethod(bobberBar, "releaseLeftClick").Invoke(0, 0); 
                    }
                    catch (Exception ex)
                    {
                        this.Monitor.Log($"Warning: Failed to call releaseLeftClick: {ex.Message}", LogLevel.Trace);
                    }

                    this.Monitor.Log("RL Action: Bar RELEASE (Hold Off)", LogLevel.Debug);
                }
            }
        }
        
        // performs a single click action for hooking fish
        private void PerformSingleClick()
        {
            if (Game1.player.CurrentTool is FishingRod rod)
            {
                rod.DoFunction(
                    Game1.currentLocation,
                    (int)Game1.player.GetToolLocation().X,
                    (int)Game1.player.GetToolLocation().Y,
                    1,
                    Game1.player
                );
            }
        }
        
        // prints current fishing data to console for debugging
        public void PrintFishingData()
        {
            var data = this.currentFishingData;
            this.Monitor.Log("========== CURRENT EXPERIMENT SETUP ==========", LogLevel.Info);
            
            // Experiment conditions
            this.Monitor.Log($"Season: {Game1.season.ToString().ToUpper()}", LogLevel.Info);
            string weather = Game1.isRaining ? "RAINY" : (Game1.isSnowing ? "SNOWY" : "SUNNY");
            this.Monitor.Log($"Weather: {weather}", LogLevel.Info);
            string timeStr = (Game1.timeOfDay / 100).ToString() + ":" + (Game1.timeOfDay % 100).ToString("00");
            this.Monitor.Log($"Time: {timeStr}", LogLevel.Info);
            this.Monitor.Log($"Location: {Game1.currentLocation.Name}", LogLevel.Info);
            
            if (data.HasFishingRod)
            {
                this.Monitor.Log($"Rod Type: {GetRodName(currentRodLevel)}", LogLevel.Info);
                this.Monitor.Log($"Casting Power: {data.CastingPower:P}", LogLevel.Info);
                this.Monitor.Log($"Is Fishing: {data.IsFishing}", LogLevel.Info);
                this.Monitor.Log($"Is Nibbling: {data.IsNibbling}", LogLevel.Info);
            }
            else
            {
                this.Monitor.Log("Rod: NOT EQUIPPED", LogLevel.Info);
            }

            if (data.BobberExists)
            {
                this.Monitor.Log($"Bobber Position (px): ({data.BobberX:F1}, {data.BobberY:F1})", LogLevel.Info);
            }
            
            this.Monitor.Log($"Player Position: ({data.PlayerTileX}, {data.PlayerTileY})", LogLevel.Info);
            this.Monitor.Log($"Player Stamina: {Game1.player.Stamina:F0}/{Game1.player.MaxStamina:F0}", LogLevel.Info);
            this.Monitor.Log($"Player Health: {Game1.player.health}/{Game1.player.maxHealth}", LogLevel.Info);
            
            if (data.MinigameActive)
            {
                this.Monitor.Log("--- MINIGAME STATE (Active) ---", LogLevel.Info);
                this.Monitor.Log($"Fish Pos (Y): {data.FishPosition:P}", LogLevel.Info);
                this.Monitor.Log($"Bar Pos (Y): {data.BobberBarPosition:P}", LogLevel.Info);
                this.Monitor.Log($"Bar Height: {data.BobberBarHeight}", LogLevel.Info);
                this.Monitor.Log($"Difficulty: {data.Difficulty}", LogLevel.Info);
            }
            
            this.Monitor.Log("==============================================", LogLevel.Info);
        }

        // starts tcp server
        private void StartServer()
        {
            try
            {
                IPAddress localAddr = IPAddress.Parse("127.0.0.1");
                int port = 8080; 

                this.listener = new TcpListener(localAddr, port);
                this.listener.Start();
                this.Monitor.Log($"Server listening on {localAddr}:{port}...", LogLevel.Info);

                while (this.isListening)
                {
                    this.client = this.listener.AcceptTcpClient();
                    this.Monitor.Log("RL Agent connected!", LogLevel.Info);
                    this.stream = this.client.GetStream();
                    
                    Task.Run(() => this.CommunicationLoop());
                }
            }
            catch (SocketException ex)
            {
                this.Monitor.Log($"Socket error: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                this.listener?.Stop();
            }
        }

        // main communication loop
        private void CommunicationLoop()
        {
            byte[] bytes = new byte[256];
            
            try
            {
                while (this.client.Connected)
                {
                    string stateJson = this.currentFishingData.ToJson();
                    
                    byte[] msg = Encoding.UTF8.GetBytes(stateJson + Environment.NewLine); 
                    this.stream.Write(msg, 0, msg.Length);
                    
                    int i = this.stream.Read(bytes, 0, bytes.Length);
                    if (i > 0)
                    {
                        string data = Encoding.UTF8.GetString(bytes, 0, i).Trim();
                        try
                        {
                            var actionData = JsonConvert.DeserializeObject<AgentAction>(data);

                            lock (receivedAction)
                            {
                                receivedAction.Action = actionData.Action;
                                receivedAction.Interval = actionData.Interval;
                            }
                        }
                        catch (JsonException ex)
                        {
                             this.Monitor.Log($"JSON parsing failed for received action: {ex.Message} Data: {data}", LogLevel.Error);
                        }
                    }
                    Thread.Sleep(16);
                }
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"RL Agent disconnected or communication error: {ex.Message}", LogLevel.Info);
            }
            finally
            {
                this.stream?.Close();
                this.client?.Close();
                this.Monitor.Log("Client cleanup complete.", LogLevel.Info);
            }
        }

        // cleanup method
        public new void Dispose()
        {
            this.isListening = false;
            try { this.listener?.Stop(); } catch { }
            this.serverThread?.Join();
        }
    }
}