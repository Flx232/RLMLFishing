// // stardew valley smapi mod for capturing fishing game state data
// // monitors player fishing activities, extracts detailed state information
// // from the fishing rod and minigame (bobber bar), and provides utilities
// // for automated rod charging and clicking for testing purposes
// // exposes fishing data through json serialization and console logging

// using StardewModdingAPI;
// using StardewModdingAPI.Events;
// using StardewValley;
// using StardewValley.Tools;
// using StardewValley.Menus;
// using System;
// using System.Reflection;
// using System.Collections.Generic;
// using System.Text;
// using Microsoft.Xna.Framework; 

// namespace FishingDataCapture
// {
//     // data structure containing all fishing-related state information
//     public class FishingData
//     {
//         // player state properties
//         public float PlayerStamina { get; set; }
//         public float PlayerMaxStamina { get; set; }
//         public int PlayerFishingLevel { get; set; }
//         public string PlayerLocation { get; set; }
//         public int PlayerTileX { get; set; }
//         public int PlayerTileY { get; set; }
//         public int PlayerFacingDirection { get; set; }

//         // fishing rod state properties
//         public bool HasFishingRod { get; set; }
//         public string RodName { get; set; }
//         public int RodUpgradeLevel { get; set; }
//         public bool IsCharging { get; set; }
//         public float CastingPower { get; set; }
//         public bool IsCasting { get; set; }
//         public bool IsFishing { get; set; }
//         public bool IsNibbling { get; set; }
//         public bool IsReeling { get; set; }
//         public bool HasBait { get; set; }
//         public string BaitName { get; set; }
//         public bool HasTackle { get; set; }
//         public string TackleName { get; set; }
//         public int NumAttachments { get; set; }

//         // bobber state properties
//         public bool BobberExists { get; set; }
//         public float BobberX { get; set; }
//         public float BobberY { get; set; }
//         public int BobberTileX { get; set; }
//         public int BobberTileY { get; set; }

//         // fishing minigame state properties
//         public bool MinigameActive { get; set; }
//         public float FishPosition { get; set; }
//         public float FishVelocity { get; set; }
//         public float BobberBarPosition { get; set; }
//         public float BobberBarHeight { get; set; }
//         public float BobberBarSpeed { get; set; }
//         public float DistanceFromCatching { get; set; }
//         public bool TreasureAppeared { get; set; }
//         public float TreasurePosition { get; set; }
//         public bool TreasureCaught { get; set; }
//         public int Difficulty { get; set; }
//         public int FishQuality { get; set; }
//         public int FishSize { get; set; }
//         public bool Perfect { get; set; }

//         // environmental state properties
//         public string Weather { get; set; }
//         public string Season { get; set; }
//         public int TimeOfDay { get; set; }
//         public int DayOfMonth { get; set; }
//         public bool IsRaining { get; set; }
//         public int WaterType { get; set; }

//         // catch history properties
//         public int TotalPerfectCatches { get; set; }
//         public string LastCaughtFish { get; set; }
//         public int LastCatchQuality { get; set; }

//         // serializes fishing data to json format for export
//         public string ToJson()
//         {
//             var sb = new StringBuilder();
//             sb.AppendLine("{");
//             sb.AppendLine($"  \"PlayerStamina\": {PlayerStamina},");
//             sb.AppendLine($"  \"PlayerMaxStamina\": {PlayerMaxStamina},");
//             sb.AppendLine($"  \"PlayerFishingLevel\": {PlayerFishingLevel},");
//             sb.AppendLine($"  \"PlayerLocation\": \"{PlayerLocation}\",");
//             sb.AppendLine($"  \"PlayerTileX\": {PlayerTileX},");
//             sb.AppendLine($"  \"PlayerTileY\": {PlayerTileY},");
//             sb.AppendLine($"  \"PlayerFacingDirection\": {PlayerFacingDirection},");
//             sb.AppendLine($"  \"HasFishingRod\": {HasFishingRod.ToString().ToLower()},");
//             sb.AppendLine($"  \"RodName\": \"{RodName}\",");
//             sb.AppendLine($"  \"RodUpgradeLevel\": {RodUpgradeLevel},");
//             sb.AppendLine($"  \"IsCharging\": {IsCharging.ToString().ToLower()},");
//             sb.AppendLine($"  \"CastingPower\": {CastingPower},");
//             sb.AppendLine($"  \"IsCasting\": {IsCasting.ToString().ToLower()},");
//             sb.AppendLine($"  \"IsFishing\": {IsFishing.ToString().ToLower()},");
//             sb.AppendLine($"  \"IsNibbling\": {IsNibbling.ToString().ToLower()},");
//             sb.AppendLine($"  \"IsReeling\": {IsReeling.ToString().ToLower()},");
//             sb.AppendLine($"  \"HasBait\": {HasBait.ToString().ToLower()},");
//             sb.AppendLine($"  \"BaitName\": \"{BaitName}\",");
//             sb.AppendLine($"  \"HasTackle\": {HasTackle.ToString().ToLower()},");
//             sb.AppendLine($"  \"TackleName\": \"{TackleName}\",");
//             sb.AppendLine($"  \"NumAttachments\": {NumAttachments},");
//             sb.AppendLine($"  \"BobberExists\": {BobberExists.ToString().ToLower()},");
//             sb.AppendLine($"  \"BobberX\": {BobberX},");
//             sb.AppendLine($"  \"BobberY\": {BobberY},");
//             sb.AppendLine($"  \"BobberTileX\": {BobberTileX},");
//             sb.AppendLine($"  \"BobberTileY\": {BobberTileY},");
//             sb.AppendLine($"  \"MinigameActive\": {MinigameActive.ToString().ToLower()},");
//             sb.AppendLine($"  \"FishPosition\": {FishPosition},");
//             sb.AppendLine($"  \"FishVelocity\": {FishVelocity},");
//             sb.AppendLine($"  \"BobberBarPosition\": {BobberBarPosition},");
//             sb.AppendLine($"  \"BobberBarHeight\": {BobberBarHeight},");
//             sb.AppendLine($"  \"BobberBarSpeed\": {BobberBarSpeed},");
//             sb.AppendLine($"  \"DistanceFromCatching\": {DistanceFromCatching},");
//             sb.AppendLine($"  \"TreasureAppeared\": {TreasureAppeared.ToString().ToLower()},");
//             sb.AppendLine($"  \"TreasurePosition\": {TreasurePosition},");
//             sb.AppendLine($"  \"TreasureCaught\": {TreasureCaught.ToString().ToLower()},");
//             sb.AppendLine($"  \"Difficulty\": {Difficulty},");
//             sb.AppendLine($"  \"FishQuality\": {FishQuality},");
//             sb.AppendLine($"  \"FishSize\": {FishSize},");
//             sb.AppendLine($"  \"Perfect\": {Perfect.ToString().ToLower()},");
//             sb.AppendLine($"  \"Weather\": \"{Weather}\",");
//             sb.AppendLine($"  \"Season\": \"{Season}\",");
//             sb.AppendLine($"  \"TimeOfDay\": {TimeOfDay},");
//             sb.AppendLine($"  \"DayOfMonth\": {DayOfMonth},");
//             sb.AppendLine($"  \"IsRaining\": {IsRaining.ToString().ToLower()},");
//             sb.AppendLine($"  \"WaterType\": {WaterType},");
//             sb.AppendLine($"  \"TotalPerfectCatches\": {TotalPerfectCatches}");
//             sb.AppendLine("}");
//             return sb.ToString();
//         }
//     }

//     // main mod entry point that handles event registration and game state monitoring
//     public class ModEntry : Mod
//     {
//         // charging state tracking
//         private int? chargeTicks = null;
//         private bool isCharging = false;

//         // clicking state tracking
//         private DateTime? clickEndTime = null;
//         private bool isClicking = false;
//         private DateTime lastClickTime = DateTime.MinValue;
//         private double clickInterval = 0.07;

//         // current fishing data snapshot
//         private FishingData currentFishingData = new FishingData();
        
//         // reflection fields (kept for potential future use but currently unused)
//         private FieldInfo castingPowerField;
//         private FieldInfo isNibblingField;
//         private FieldInfo isReelingField;
//         private FieldInfo isFishingField;
//         private FieldInfo castingChosenField;
//         private FieldInfo fishPosField;
//         private FieldInfo fishVelocityField;
//         private FieldInfo bobberBarPosField;
//         private FieldInfo bobberBarHeightField;
//         private FieldInfo bobberBarSpeedField;
//         private FieldInfo distanceFromCatchingField;
//         private FieldInfo treasureField;
//         private FieldInfo treasurePositionField;
//         private FieldInfo treasureCaughtField;
//         private FieldInfo difficultyField;
//         private FieldInfo whichFishField;
//         private FieldInfo fishQualityField;
//         private FieldInfo fishSizeField;
//         private FieldInfo perfectField;

//         // mod initialization - registers event handlers
//         public override void Entry(IModHelper helper)
//         {
//             helper.Events.Input.ButtonPressed += this.OnButtonPressed;
//             helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
//             helper.Events.Display.MenuChanged += this.OnMenuChanged;
//             helper.Events.Player.InventoryChanged += this.OnInventoryChanged;
            
//             this.InitializeReflection();
            
//             this.Monitor.Log("Fishing Data Capture Mod Loaded!", LogLevel.Info);
//         }

//         // placeholder for reflection initialization (currently unused)
//         private void InitializeReflection()
//         {
//         }

//         // handles keyboard input for testing and data capture commands
//         // k: charge rod for 2 seconds
//         // l: mash click for 3 seconds
//         // j: mash click for 1 second with faster interval
//         // h: stop all actions
//         // p: print fishing data to console
//         // o: save fishing data to json file
//         private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
//         {
//             if (!Context.IsWorldReady)
//                 return;

//             switch (e.Button)
//             {
//                 case SButton.K:
//                     this.ChargeRod(2.0f);
//                     break;
//                 case SButton.L:
//                     this.MashClick(3.0f);
//                     break;
//                 case SButton.J:
//                     this.MashClick(1.0f, 0.05);
//                     break;
//                 case SButton.H:
//                     this.StopAllActions();
//                     break;
//                 case SButton.P:
//                     this.PrintFishingData();
//                     break;
//                 case SButton.O:
//                     this.SaveFishingDataToJson();
//                     break;
//             }
//         }

//         // detects when fishing minigame starts or ends
//         private void OnMenuChanged(object sender, MenuChangedEventArgs e)
//         {
//             if (e.NewMenu is BobberBar)
//             {
//                 this.Monitor.Log("Fishing minigame started!", LogLevel.Info);
//             }
//             else if (e.OldMenu is BobberBar)
//             {
//                 this.Monitor.Log("Fishing minigame ended!", LogLevel.Info);
//             }
//         }

//         // auto-warps player to beach when fishing rod is equipped
//         private void OnInventoryChanged(object sender, InventoryChangedEventArgs e)
//         {
//             if (!Context.IsWorldReady)
//                 return;

//             // check if fishing rod is equipped and player is not already at beach
//             if (Game1.player.CurrentTool is FishingRod rod)
//             {
//                 if (Game1.player.currentLocation.Name != "Beach")
//                 {
//                     // warp player to safe fishing spot on beach
//                     Game1.warpFarmer("Beach", 18, 26, 0); 
//                     this.Monitor.Log($"Warped player to Beach after equipping {rod.Name}.", LogLevel.Info);
//                     Game1.addHUDMessage(new HUDMessage("Warped to Beach for Fishing Data!", 2));
//                 }
//             }
//         }

//         // runs every game tick to update state and handle timed actions
//         private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
//         {
//             if (!Context.IsWorldReady)
//                 return;

//             // update fishing data snapshot every tick
//             this.UpdateFishingData();

//             // handle ongoing charging action
//             if (this.isCharging)
//             {
//                 this.UpdateCharging();
//             }

//             // handle ongoing clicking action
//             if (this.isClicking)
//             {
//                 this.UpdateClicking();
//             }
//         }

//         // captures current fishing state from game and stores in fishingdata object
//         private void UpdateFishingData()
//         {
//             var data = this.currentFishingData;

//             // capture player state
//             data.PlayerStamina = Game1.player.Stamina;
//             data.PlayerMaxStamina = Game1.player.MaxStamina;
//             data.PlayerFishingLevel = Game1.player.FishingLevel;
//             data.PlayerLocation = Game1.currentLocation?.Name ?? "Unknown";
//             data.PlayerTileX = (int)Game1.player.Tile.X; 
//             data.PlayerTileY = (int)Game1.player.Tile.Y; 
//             data.PlayerFacingDirection = Game1.player.FacingDirection;

//             // capture fishing rod state using public properties
//             if (Game1.player.CurrentTool is FishingRod rod)
//             {
//                 data.HasFishingRod = true;
//                 data.RodName = rod.Name;
//                 data.RodUpgradeLevel = rod.UpgradeLevel;
//                 data.IsCharging = this.isCharging;
                
//                 // access rod state through public properties
//                 try 
//                 {
//                     data.CastingPower = rod.castingPower; 
//                     data.IsFishing = rod.isFishing;       
//                     data.IsNibbling = rod.isNibbling;
//                     data.IsReeling = rod.isReeling;
//                 }
//                 catch (Exception ex)
//                 {
//                     this.Monitor.Log($"Error accessing FishingRod properties: {ex.Message}", LogLevel.Error);
//                 }
                
//                 data.IsCasting = Game1.player.UsingTool;

//                 // check rod attachments (bait and tackle)
//                 data.NumAttachments = rod.attachments?.Length ?? 0;
                
//                 if (rod.attachments != null && rod.attachments.Length > 0)
//                 {
//                     data.HasBait = rod.attachments[0] != null;
//                     data.BaitName = data.HasBait ? rod.attachments[0].Name : null;

//                     data.HasTackle = rod.attachments.Length > 1 && rod.attachments[1] != null;
//                     data.TackleName = data.HasTackle ? rod.attachments[1].Name : null;
//                 }

//                 // capture bobber position if rod is cast
//                 if (data.IsFishing && rod.bobber != null) 
//                 {
//                     data.BobberExists = true;
//                     data.BobberX = rod.bobber.X;
//                     data.BobberY = rod.bobber.Y;
//                     data.BobberTileX = (int)(rod.bobber.X / 64);
//                     data.BobberTileY = (int)(rod.bobber.Y / 64);
//                 }
//                 else
//                 {
//                     data.BobberExists = false;
//                     data.BobberX = 0f;
//                     data.BobberY = 0f;
//                     data.BobberTileX = 0;
//                     data.BobberTileY = 0;
//                 }
//             }
//             else
//             {
//                 data.HasFishingRod = false;
//                 data.RodName = null;
//             }

//             // capture fishing minigame state using reflection
//             if (Game1.activeClickableMenu is BobberBar bobberBar)
//             {
//                 data.MinigameActive = true;
                
//                 // use smapi reflection helper to access private bobberbar fields
//                 try
//                 {
//                     data.FishPosition = this.Helper.Reflection.GetField<float>(bobberBar, "bobberPosition").GetValue();
//                     data.FishVelocity = this.Helper.Reflection.GetField<float>(bobberBar, "bobberSpeed").GetValue();
//                     data.BobberBarPosition = this.Helper.Reflection.GetField<float>(bobberBar, "bobberBarPos").GetValue();
//                     data.BobberBarHeight = this.Helper.Reflection.GetField<int>(bobberBar, "bobberBarHeight").GetValue();
//                     data.BobberBarSpeed = this.Helper.Reflection.GetField<float>(bobberBar, "bobberBarSpeed").GetValue();
//                     data.DistanceFromCatching = this.Helper.Reflection.GetField<float>(bobberBar, "distanceFromCatching").GetValue();
//                     data.TreasureAppeared = this.Helper.Reflection.GetField<bool>(bobberBar, "treasure").GetValue();
//                     data.TreasurePosition = this.Helper.Reflection.GetField<float>(bobberBar, "treasurePosition").GetValue();
//                     data.TreasureCaught = this.Helper.Reflection.GetField<bool>(bobberBar, "treasureCaught").GetValue();
                    
//                     // difficulty is stored as float but cast to int for export
//                     data.Difficulty = (int)this.Helper.Reflection.GetField<float>(bobberBar, "difficulty").GetValue();
//                 }
//                 catch (Exception ex)
//                 {
//                     this.Monitor.Log($"Minigame data reflection failed. Check BobberBar field names: {ex.Message}", LogLevel.Debug);
//                 }
//             }
//             else
//             {
//                 data.MinigameActive = false;
//             }

//             // capture environmental state
//             data.Weather = Game1.weatherForTomorrow switch
//             {
//                 Game1.weather_sunny => "Sunny",
//                 Game1.weather_rain => "Rainy",
//                 Game1.weather_lightning => "Stormy",
//                 Game1.weather_snow => "Snowy",
//                 Game1.weather_debris => "Windy",
//                 Game1.weather_festival => "Festival",
//                 Game1.weather_wedding => "Wedding",
//                 _ => "Unknown"
//             };
//             data.Season = Game1.currentSeason;
//             data.TimeOfDay = Game1.timeOfDay;
//             data.DayOfMonth = Game1.dayOfMonth;
//             data.IsRaining = Game1.isRaining;

//             // determine water type based on location
//             string locationName = Game1.currentLocation?.Name?.ToLower() ?? "";
//             if (locationName.Contains("ocean") || locationName.Contains("beach"))
//                 data.WaterType = 2; 
//             else if (locationName.Contains("mountain") || locationName.Contains("forest"))
//                 data.WaterType = 0; 
//             else
//                 data.WaterType = 1;
//         }

//         // prints simplified fishing data to console log
//         private void PrintFishingData()
//         {
//             var data = this.currentFishingData;

//             this.Monitor.Log("========== FISHING DATA (Simplified) ==========", LogLevel.Info);
            
//             // display rod and cast state
//             if (data.HasFishingRod)
//             {
//                 this.Monitor.Log($"Casting Power: {data.CastingPower:F2}", LogLevel.Info);
//                 this.Monitor.Log($"Is Fishing: {data.IsFishing}", LogLevel.Info);
//                 this.Monitor.Log($"Is Nibbling: {data.IsNibbling}", LogLevel.Info);
//             }

//             // display bobber position
//             if (data.BobberExists)
//             {
//                 this.Monitor.Log($"Bobber Position (px): ({data.BobberX:F1}, {data.BobberY:F1})", LogLevel.Info);
//             }
//             else
//             {
//                  this.Monitor.Log("No Bobber Data (Rod not cast or not equipped).", LogLevel.Info);
//             }
//             this.Monitor.Log($"Player Tile Position: ({data.PlayerTileX}, {data.PlayerTileY})", LogLevel.Info);
            
//             // display minigame state if active
//             if (data.MinigameActive)
//             {
//                 this.Monitor.Log("--- MINIGAME STATE (Active) ---", LogLevel.Info);
//                 this.Monitor.Log($"Fish Pos (Y): {data.FishPosition:F2}", LogLevel.Info);
//                 this.Monitor.Log($"Bar Pos (Y): {data.BobberBarPosition:F2}", LogLevel.Info);
//                 this.Monitor.Log($"Bar Height: {data.BobberBarHeight}", LogLevel.Info);
//                 this.Monitor.Log($"Difficulty: {data.Difficulty}", LogLevel.Info);
//                 this.Monitor.Log($"Treasure?: {data.TreasureAppeared} @ {data.TreasurePosition:F2}", LogLevel.Info);
//                 this.Monitor.Log("-------------------------------", LogLevel.Info);
//             }
            
//             this.Monitor.Log("===============================================", LogLevel.Info);
//         }

//         // saves current fishing data to timestamped json file
//         private void SaveFishingDataToJson()
//         {
//             string json = this.currentFishingData.ToJson();
//             string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
//             string filename = $"fishing_data_{timestamp}.json";
//             string path = System.IO.Path.Combine(this.Helper.DirectoryPath, filename);
            
//             System.IO.File.WriteAllText(path, json);
            
//             this.Monitor.Log($"Saved fishing data to {filename}", LogLevel.Info);
//             Game1.addHUDMessage(new HUDMessage("Data saved!", 2));
//         }

//         // returns current fishing data snapshot for external access
//         public FishingData GetCurrentFishingData()
//         {
//             return this.currentFishingData;
//         }

//         // initiates rod charging for specified duration in seconds
//         public void ChargeRod(float seconds)
//         {
//             int ticks = (int)(seconds * 60);
//             this.StartCharging(ticks);
//         }

//         // initiates rapid clicking for specified duration
//         public void MashClick(float seconds, double intervalSeconds = 0.07)
//         {
//             this.StartClicking(seconds, intervalSeconds);
//         }

//         // stops all automated actions (charging and clicking)
//         public void StopAllActions()
//         {
//             this.StopCharging();
//             this.StopClicking();
//             this.Monitor.Log("All actions stopped", LogLevel.Info);
//         }

//         // begins charging the fishing rod for specified number of ticks
//         private void StartCharging(int ticks)
//         {
//             if (!(Game1.player.CurrentTool is FishingRod rod))
//             {
//                 this.Monitor.Log("No fishing rod equipped!", LogLevel.Warn);
//                 return;
//             }

//             this.StopClicking();
//             this.chargeTicks = ticks;
//             this.isCharging = true;
//             Game1.player.BeginUsingTool();
            
//             float seconds = ticks / 60f;
//             this.Monitor.Log($"Started charging for {seconds:F2} seconds", LogLevel.Info);
//         }

//         // begins automated clicking for specified duration and interval
//         private void StartClicking(float seconds, double interval)
//         {
//             if (this.isCharging)
//             {
//                 this.StopCharging();
//             }

//             this.clickEndTime = DateTime.Now.AddSeconds(seconds);
//             this.isClicking = true;
//             this.lastClickTime = DateTime.MinValue;
//             this.clickInterval = interval;
            
//             this.Monitor.Log($"Started clicking for {seconds}s (interval: {interval}s)", LogLevel.Info);
//         }

//         // updates charging state each tick and releases when time expires
//         private void UpdateCharging()
//         {
//             if (this.chargeTicks == null)
//                 return;

//             this.chargeTicks--;

//             if (this.chargeTicks <= 0)
//             {
//                 this.ReleaseCast();
//                 return;
//             }

//             if (Game1.player.CurrentTool is FishingRod rod)
//             {
//                 rod.tickUpdate(Game1.currentGameTime, Game1.player);
//             }
//         }

//         // updates clicking state and performs clicks at specified intervals
//         private void UpdateClicking()
//         {
//             if (this.clickEndTime != null && DateTime.Now >= this.clickEndTime)
//             {
//                 this.StopClicking();
//                 return;
//             }

//             TimeSpan timeSinceLastClick = DateTime.Now - this.lastClickTime;
//             if (timeSinceLastClick.TotalSeconds >= this.clickInterval)
//             {
//                 this.PerformClick();
//                 this.lastClickTime = DateTime.Now;
//             }
//         }

//         // executes a single tool use click
//         private void PerformClick()
//         {
//             var tool = Game1.player.CurrentTool;
//             if (tool == null)
//                 return;

//             tool.DoFunction(
//                 Game1.currentLocation,
//                 (int)Game1.player.GetToolLocation().X,
//                 (int)Game1.player.GetToolLocation().Y,
//                 1,
//                 Game1.player
//             );
//         }

//         // releases the charged cast
//         private void ReleaseCast()
//         {
//             this.isCharging = false;
//             this.chargeTicks = null;
//             Game1.player.EndUsingTool();
//             this.Monitor.Log("Cast released!", LogLevel.Info);
//         }

//         // stops the charging action
//         private void StopCharging()
//         {
//             if (!this.isCharging)
//                 return;

//             this.isCharging = false;
//             this.chargeTicks = null;
//             Game1.player.UsingTool = false;
//             Game1.player.CanMove = true;
//         }

//         // stops the clicking action
//         private void StopClicking()
//         {
//             if (!this.isClicking)
//                 return;

//             this.isClicking = false;
//             this.clickEndTime = null;
            
//             if (Game1.player.CurrentTool != null)
//             {
//                 Game1.player.CurrentTool.endUsing(Game1.currentLocation, Game1.player);
//             }
//         }
//     }
// }