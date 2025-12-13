// stardew valley smapi mod that acts as a tcp server for reinforcement learning agents
// streams fishing game state data to connected python agents via json
// receives and executes agent actions for automated fishing control
// supports both hooking phase (nibble detection) and minigame phase (bar control)
// uses reflection to directly manipulate bobber bar physics for precise control

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
using System.Collections.Generic;
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
        // interval field is repurposed to carry the calculated force/boost value
        private class AgentAction { public int Action { get; set; } = 0; public float Interval { get; set; } = 0f; }
        private volatile AgentAction receivedAction = new AgentAction(); 
        
        // mod initialization - registers event handlers and starts tcp server
        public override void Entry(IModHelper helper)
        {
            this.currentFishingData = new RLFishingData(helper); 

            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Display.MenuChanged += this.OnMenuChanged;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            
            // start the networking server thread
            this.serverThread = new Thread(this.StartServer);
            this.serverThread.IsBackground = true;
            this.serverThread.Start();
            
            this.Monitor.Log("RL Fishing Mod Loaded! Listening for RL Agent on port 8080...", LogLevel.Info);
        }

        // handles button presses for debug logging (press p to print fishing data)
        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || e.Button != SButton.P)
                return;

            this.PrintFishingData();
        }

        // debug function that lists all private fields in bobberbar for reflection debugging
        // helps identify correct field names for velocity manipulation
        private void ListBobberBarFields(BobberBar bobberBar)
        {
            this.Monitor.Log("--- DEBUG: BOBBER BAR PRIVATE FIELDS ---", LogLevel.Info);
            this.Monitor.Log("Look for a field name that looks like 'motion', 'yVel', or '_yVel' with Type: Single.", LogLevel.Info);
            try
            {
                // use reflection to get all non-public instance fields
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
        
        // tracks minigame start and end to detect successful catches
        private void OnMenuChanged(object sender, MenuChangedEventArgs e)
        {
            // fishing minigame (bobberbar) has just opened
            if (e.NewMenu is BobberBar)
            {
                // store current inventory count to check for successful catch later
                this.inventoryCountBeforeMinigame = Game1.player.Items.Count;
                this.Monitor.Log($"Minigame started. Inventory count before: {this.inventoryCountBeforeMinigame}", LogLevel.Debug);
            }
            // fishing minigame has just closed
            else if (e.OldMenu is BobberBar && e.NewMenu is null)
            {
                // check if a fish/junk was caught by comparing inventory size
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

        // core game loop handler that updates state and executes agent actions
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            // update the environment state every tick
            this.currentFishingData.Update(); 
            
            // safely grab the latest action instruction from agent
            int action;
            float forceInjection;
            lock (receivedAction)
            {
                action = receivedAction.Action;
                // interval is interpreted as the calculated force/boost value
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
                // action 1 performs a single click to hook the fish
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
                // reflection targets for state and direct control
                var buttonPressedField = this.Helper.Reflection.GetField<bool>(bobberBar, "buttonPressed", false);
                // use bobberbarspeed field for reading and modifying velocity
                var motionField = this.Helper.Reflection.GetField<float>(bobberBar, "bobberBarSpeed", false); 

                if (action == 1) // action 1: apply force/boost
                {
                    // simulate continuous hold (acceleration) flag
                    try 
                    { 
                        buttonPressedField?.SetValue(true); 
                        this.Monitor.Log("RL Action: Continuous Hold Flag Set (TRUE)", LogLevel.Trace);
                    }
                    catch (Exception ex) 
                    { 
                        this.Monitor.Log($"Warning: Failed to set 'buttonPressed' field: {ex.Message}", LogLevel.Trace); 
                    }

                    // direct velocity injection using calculated force from python agent
                    if (Math.Abs(forceInjection) > 0.001f)
                    {
                        try
                        {
                            if (motionField != null)
                            {
                                float currentMotion = motionField.GetValue();
                                float newMotion = currentMotion + forceInjection;
                                
                                // inject the calculated force directly into the velocity field
                                motionField.SetValue(newMotion); 
                                
                                this.Monitor.Log($"RL Action: FORCE APPLIED! Force: {forceInjection:F2}, New Motion: {newMotion:F2}", LogLevel.Debug);
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
                else if (action == 0) // action 0: release/do nothing
                {
                    // simulate release by setting the private field to false
                    try 
                    { 
                        buttonPressedField?.SetValue(false); 
                        this.Monitor.Log("RL Action: Continuous Hold Flag Set (FALSE)", LogLevel.Trace);
                    }
                    catch (Exception ex) 
                    { 
                        // ignored
                    } 
                    
                    // explicitly call release method to clear game's internal input state
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
            this.Monitor.Log("========== FISHING DATA (Simplified) ==========", LogLevel.Info);
            
            if (data.HasFishingRod)
            {
                this.Monitor.Log($"Casting Power: {data.CastingPower:P}", LogLevel.Info);
                this.Monitor.Log($"Is Fishing: {data.IsFishing}", LogLevel.Info);
                this.Monitor.Log($"Is Nibbling: {data.IsNibbling}", LogLevel.Info);
            }

            if (data.BobberExists)
            {
                this.Monitor.Log($"Bobber Position (px): ({data.BobberX:F1}, {data.BobberY:F1})", LogLevel.Info);
            }
            else
            {
                 this.Monitor.Log("No Bobber Data (Rod not cast or not equipped).", LogLevel.Info);
            }
            this.Monitor.Log($"Player Tile Position: ({data.PlayerTileX}, {data.PlayerTileY})", LogLevel.Info);
            
            if (data.MinigameActive)
            {
                this.Monitor.Log("--- MINIGAME STATE (Active) ---", LogLevel.Info);
                this.Monitor.Log($"Fish Pos (Y): {data.FishPosition:P}", LogLevel.Info);
                this.Monitor.Log($"Bar Pos (Y): {data.BobberBarPosition:P}", LogLevel.Info);
                this.Monitor.Log($"Bar Height: {data.BobberBarHeight}", LogLevel.Info);
                this.Monitor.Log($"Difficulty: {data.Difficulty}", LogLevel.Info);

                // debug call to list all bobberbar fields for reflection debugging
                if (Game1.activeClickableMenu is BobberBar bobberBar)
                {
                    this.ListBobberBarFields(bobberBar);
                }
            }
            
            this.Monitor.Log("===============================================", LogLevel.Info);
        }

        // starts tcp server listening for rl agent connections on port 8080
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

        // main communication loop that sends state and receives actions
        private void CommunicationLoop()
        {
            byte[] bytes = new byte[256];
            
            try
            {
                while (this.client.Connected)
                {
                    // send current state (observation) to agent as json
                    string stateJson = this.currentFishingData.ToJson();
                    
                    byte[] msg = Encoding.UTF8.GetBytes(stateJson + Environment.NewLine); 
                    this.stream.Write(msg, 0, msg.Length);
                    
                    // wait for action from agent (expecting json: {"action": 1, "interval": [force_value]})
                    int i = this.stream.Read(bytes, 0, bytes.Length);
                    if (i > 0)
                    {
                        string data = Encoding.UTF8.GetString(bytes, 0, i).Trim();
                        try
                        {
                            var actionData = JsonConvert.DeserializeObject<AgentAction>(data);

                            // place the action in the thread-safe queue
                            lock (receivedAction)
                            {
                                receivedAction.Action = actionData.Action;
                                // interval is repurposed as the force/boost magnitude
                                receivedAction.Interval = actionData.Interval;
                            }
                        }
                        catch (JsonException ex)
                        {
                             this.Monitor.Log($"JSON parsing failed for received action: {ex.Message} Data: {data}", LogLevel.Error);
                        }
                    }
                    Thread.Sleep(16); // ~60 fps communication rate
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

        // cleanup method to stop server and close connections
        public new void Dispose()
        {
            this.isListening = false;
            try { this.listener?.Stop(); } catch { }
            this.serverThread?.Join();
        }
    }
}