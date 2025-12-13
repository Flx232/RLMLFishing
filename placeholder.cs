// // stardew valley smapi mod for testing and simulating fishing actions
// // provides utilities for automated rod charging and casting simulation
// // includes debugging tools for inventory inspection and player state management
// // designed for reinforcement learning experiments with fishing mechanics

// using System;
// using System.Collections.Generic;
// using StardewModdingAPI;
// using StardewModdingAPI.Events;
// using StardewValley;
// using StardewValley.Tools;
// using Microsoft.Xna.Framework;
// using xTile.Dimensions;
// using StardewObject = StardewValley.Object;

// namespace RL_Fishing
// {
//     // main mod entry point for rl fishing testing utilities
//     public class ModEntry : Mod
//     {
//         // tracking state for button presses and timing
//         private Dictionary<SButton, DateTime> buttonPressStartTime = new Dictionary<SButton, DateTime>();
//         private DateTime? moveEndTime = null;
//         private int moveDirection = -1;
//         private DateTime? clickEndTime = null;
//         private bool isClicking = false;
//         private DateTime? holdEndTime = null;
//         private bool isHolding = false;
//         private bool hasInitiatedAction = false;
//         private bool isCharging = false;

//         // mod initialization - registers event handlers
//         public override void Entry(IModHelper helper)
//         {
//             this.Monitor.Log("My First Mod has loaded!", LogLevel.Info);

//             // subscribe to game events
//             helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
//             helper.Events.GameLoop.DayStarted += this.OnDayStarted;
//             helper.Events.Input.ButtonPressed += this.OnButtonPressed;
//             helper.Events.Input.ButtonReleased += this.OnButtonReleased;        
//             helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
//         }

//         // called when the game launches
//         private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
//         {
//             this.Monitor.Log("The game has launched!", LogLevel.Info);
//         }

//         // called at the start of each new day
//         private void OnDayStarted(object sender, DayStartedEventArgs e)
//         {
//             this.Monitor.Log($"Good morning! Today is {Game1.currentSeason} {Game1.dayOfMonth}", LogLevel.Info);
            
//             // give the player bonus gold each day
//             Game1.player.Money += 100;
//             Game1.addHUDMessage(new HUDMessage("You received 100g!", 2));

//             Game1.addHUDMessage(new HUDMessage($"Good morning! Today is {Game1.currentSeason} {Game1.dayOfMonth}", 2));
//         }

//         // handles keyboard button press events
//         // q: add pufferfish to inventory
//         // i: display inventory contents
//         // k: restore energy and health
//         // p: simulate casting click
//         private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
//         {
//             // only respond in-game (not in menus)
//             if (!Context.IsWorldReady)
//                 return;

//             this.Monitor.Log($"Player pressed {e.Button}", LogLevel.Debug);

//             // add pufferfish to inventory
//             if (e.Button == SButton.Q)
//             {
//                 Item puffer = ItemRegistry.Create("(O)128", 1);                 
//                 Game1.player.addItemToInventory(puffer);
                
//                 Game1.addHUDMessage(new HUDMessage("Added Puffer Fish!", 2));
//             }
            
//             // display inventory contents in console
//             if (e.Button == SButton.I)
//             {
//                 this.check_inventory();                
//             }
            
//             // restore player energy and health to maximum
//             if (e.Button == SButton.K)
//             {
//                 Game1.player.Stamina = Game1.player.MaxStamina;
//                 Game1.player.health = Game1.player.maxHealth;
//                 Game1.addHUDMessage(new HUDMessage("Energy and health restored!", 1));
//             }

//             // record when the button was first pressed for duration tracking
//             if (!this.buttonPressStartTime.ContainsKey(e.Button))
//             {
//                 this.buttonPressStartTime[e.Button] = DateTime.Now;
//                 this.Monitor.Log($"Started pressing {e.Button}", LogLevel.Debug);
//             }

//             // simulate a quick click for casting
//             switch (e.Button)
//             {
//                 case SButton.P:
//                     this.SimulateClickFor(0.1f);                    
//                     break;
//             }
//         }

//         // handles button release events and calculates hold duration
//         private void OnButtonReleased(object sender, ButtonReleasedEventArgs e)
//         {
//             if (!Context.IsWorldReady)
//                 return;

//             // calculate how long the button was held
//             if (this.buttonPressStartTime.TryGetValue(e.Button, out DateTime startTime))
//             {
//                 TimeSpan duration = DateTime.Now - startTime;
//                 this.Monitor.Log($"Released {e.Button} after {duration.TotalSeconds:F2} seconds", LogLevel.Info);

//                 // clean up tracking dictionary
//                 this.buttonPressStartTime.Remove(e.Button);
//             }
//         }

//         // displays complete inventory contents to console
//         private void check_inventory(){
//             IList<Item> inventory = Game1.player.Items;
//             this.Monitor.Log("====================", LogLevel.Info);
//             this.Monitor.Log($"Total slots: {inventory.Count}", LogLevel.Info);
//             this.Monitor.Log($"Max items: {Game1.player.MaxItems}", LogLevel.Info);

//             // iterate through all inventory slots
//             for (int i = 0; i < inventory.Count; i++)
//             {
//                 Item item = inventory[i];
                
//                 if (item != null)
//                 {
//                     this.Monitor.Log($"Slot {i}: {item.Name} (x{item.Stack})", LogLevel.Info);
//                 }
//                 else
//                 {
//                     this.Monitor.Log($"Slot {i}: Empty", LogLevel.Debug);
//                 }
//             }
//             this.Monitor.Log("====================", LogLevel.Info);
//         }

//         // initiates automated rod charging for specified duration
//         private void SimulateClickFor(float seconds)
//         {
//             if (!(Game1.player.CurrentTool is FishingRod rod))
//             {
//                 this.Monitor.Log("No fishing rod equipped!", LogLevel.Warn);
//                 return;
//             }

//             this.holdEndTime = DateTime.Now.AddSeconds(seconds);
//             this.isCharging = true;
            
//             // set player to using tool state
//             Game1.player.UsingTool = true;
//             Game1.player.CanMove = false;
            
//             this.Monitor.Log($"Started charging for {seconds} seconds", LogLevel.Info);
//             Game1.addHUDMessage(new HUDMessage("Charging cast!", 2));
//         }

//         // runs every game tick to update charging state
//         private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
//         {
//             if (!Context.IsWorldReady || !this.isCharging)
//                 return;

//             // check if charging duration has elapsed
//             if (this.holdEndTime != null && DateTime.Now >= this.holdEndTime)
//             {
//                 this.StopClicking();
//                 return;
//             }

//             // update fishing rod state while charging
//             if (Game1.player.CurrentTool is FishingRod rod)
//             {
//                 // call tick method to update power bar and animation
//                 rod.tickUpdate(Game1.currentGameTime, Game1.player);
                
//                 // keep player in using tool state
//                 Game1.player.UsingTool = true;
//             }
//         }

//         // alternative method for initiating click action (similar to simulateclickfor)
//         private void PerformClickAction(float seconds)
//         {
//             if (!(Game1.player.CurrentTool is FishingRod rod))
//             {
//                 this.Monitor.Log("No fishing rod equipped!", LogLevel.Warn);
//                 return;
//             }

//             this.holdEndTime = DateTime.Now.AddSeconds(seconds);
//             this.isCharging = true;
            
//             // set player to using tool state
//             Game1.player.UsingTool = true;
//             Game1.player.CanMove = false;
            
//             this.Monitor.Log($"Started charging for {seconds} seconds", LogLevel.Info);
//             Game1.addHUDMessage(new HUDMessage("Charging cast!", 2));
//         }

//         // releases the charged cast and performs the fishing action
//         private void StopClicking()
//         {
//             this.isCharging = false;
//             this.holdEndTime = null;
            
//             // execute the cast by calling dofunction on the rod
//             if (Game1.player.CurrentTool is FishingRod rod)
//             {
//                 rod.DoFunction(
//                     Game1.currentLocation,
//                     (int)Game1.player.GetToolLocation().X,
//                     (int)Game1.player.GetToolLocation().Y,
//                     1,
//                     Game1.player
//                 );
                
//                 this.Monitor.Log("Cast released!", LogLevel.Info);
//             }
            
//             // restore player control
//             Game1.player.UsingTool = false;
//             Game1.player.CanMove = true;
//             Game1.addHUDMessage(new HUDMessage("Cast!", 2));
//         }
//     }
// }