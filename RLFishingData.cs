// data class for capturing and serializing fishing state for reinforcement learning agents
// extracts fishing rod state, player position, and minigame data from stardew valley
// uses reflection to access private bobberbar fields for detailed state information
// serializes all state data to json format for transmission to python rl agents
// AUGMENTED VERSION: includes contextual variables (rod type, location, weather, time of day)

using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;
using System.Text;
using System;
using System.Reflection;

public class RLFishingData
{
    private readonly IModHelper Helper;
    
    // fishing rod and player state properties
    public bool HasFishingRod { get; set; }
    public int PlayerTileX { get; set; }
    public int PlayerTileY { get; set; }
    
    public float CastingPower { get; set; }
    public bool IsFishing { get; set; }
    public bool IsNibbling { get; set; }
    public bool BobberExists { get; set; }
    public float BobberX { get; set; }
    public float BobberY { get; set; }

    // fishing minigame state properties
    public bool MinigameActive { get; set; }
    public float FishPosition { get; set; }
    public float BobberBarPosition { get; set; }
    public int BobberBarHeight { get; set; }
    public int Difficulty { get; set; }
    public bool TreasureAppeared { get; set; }
    public float TreasurePosition { get; set; }
    public float FishTargetPosition { get; set; } 
    public float BobberBarVelocity { get; set; }
    public float FishVelocity { get; set; }
    public float DistanceFromCatching { get; set; } 

    public string RodType { get; set; }          // Training Rod, Bamboo Pole, Fiberglass Rod, Iridium Rod
    public string Location { get; set; }         // Beach, River, Lake, Ocean, Mountain, Forest
    public string Weather { get; set; }          // Sunny, Rainy, Snowy, etc.
    public string Season { get; set; }           // Spring, Summer, Fall, Winter
    public int TimeOfDay { get; set; }           // Game time (600-2600)

    // constructor that stores reference to smapi helper for reflection
    public RLFishingData(IModHelper helper)
    {
        this.Helper = helper;
    }

    // updates all fishing state properties from current game state
    public void Update()
    {
        // capture player tile position
        this.PlayerTileX = (int)Game1.player.Tile.X;
        this.PlayerTileY = (int)Game1.player.Tile.Y;

        this.TimeOfDay = Game1.timeOfDay;
        this.Weather = Game1.isRaining ? "Rainy" : (Game1.isSnowing ? "Snowy" : "Sunny");
        this.Season = Game1.season.ToString(); // Convert Season enum to string
        this.Location = GetLocationName();

        // capture fishing rod state if equipped
        if (Game1.player.CurrentTool is FishingRod rod)
        {
            this.HasFishingRod = true;
            this.CastingPower = rod.castingPower; 
            this.IsFishing = rod.isFishing;
            this.IsNibbling = rod.isNibbling;
            this.BobberExists = this.IsFishing && rod.bobber != null;
            
            this.RodType = GetRodTypeName(rod);
            
            // capture bobber position if it exists
            if (this.BobberExists)
            {
                this.BobberX = rod.bobber.X;
                this.BobberY = rod.bobber.Y;
            }
        }
        else
        {
            this.HasFishingRod = false;
            this.BobberExists = false;
            this.RodType = "None";
        }

        // capture minigame state using reflection to access private fields
        if (Game1.activeClickableMenu is BobberBar bobberBar)
        {
            this.MinigameActive = true;
            try 
            {
                // extract core positions and difficulty
                this.FishPosition = this.Helper.Reflection.GetField<float>(bobberBar, "bobberPosition").GetValue();
                this.BobberBarPosition = this.Helper.Reflection.GetField<float>(bobberBar, "bobberBarPos").GetValue();
                this.BobberBarHeight = this.Helper.Reflection.GetField<int>(bobberBar, "bobberBarHeight").GetValue();
                this.Difficulty = (int)this.Helper.Reflection.GetField<float>(bobberBar, "difficulty").GetValue();
                
                // extract catching progress status
                this.DistanceFromCatching = this.Helper.Reflection.GetField<float>(bobberBar, "distanceFromCatching").GetValue();
                
                // extract treasure state
                this.TreasureAppeared = this.Helper.Reflection.GetField<bool>(bobberBar, "treasure").GetValue();
                this.TreasurePosition = this.Helper.Reflection.GetField<float>(bobberBar, "treasurePosition").GetValue();
                
                // extract velocity and target position fields
                this.BobberBarVelocity = this.Helper.Reflection.GetField<float>(bobberBar, "bobberBarSpeed").GetValue(); 
                this.FishVelocity = this.Helper.Reflection.GetField<float>(bobberBar, "bobberSpeed").GetValue();        
                this.FishTargetPosition = this.Helper.Reflection.GetField<float>(bobberBar, "bobberTargetPosition").GetValue(); 

            }
            catch (Exception ex) 
            {
                // handle reflection errors gracefully to prevent game crashes
                Console.WriteLine($"Reflection Error in RLFishingData: {ex.Message}");
            } 
        }
        else
        {
            this.MinigameActive = false;
        }
    }

    // AUGMENTED STATE HELPER: Get readable rod type name
    private string GetRodTypeName(FishingRod rod)
    {
        // Rod upgrade level: 0 = Training Rod, 1 = Bamboo Pole, 2 = Fiberglass Rod, 3 = Iridium Rod
        int upgradeLevel = rod.UpgradeLevel;
        
        switch (upgradeLevel)
        {
            case 0:
                return "Training Rod";
            case 1:
                return "Bamboo Pole";
            case 2:
                return "Fiberglass Rod";
            case 3:
                return "Iridium Rod";
            default:
                return "Unknown Rod";
        }
    }

    // AUGMENTED STATE HELPER: Get location name for categorization
    private string GetLocationName()
    {
        if (Game1.currentLocation == null)
            return "Unknown";
        
        string locationName = Game1.currentLocation.Name;
        
        // Map location names to categories
        if (locationName.Contains("Beach"))
            return "Beach";
        else if (locationName.Contains("Mountain") || locationName.Contains("UndergroundMine"))
            return "Mountain";
        else if (locationName.Contains("Forest") || locationName.Contains("Woods"))
            return "Forest";
        else if (locationName.Contains("Town") || locationName.Contains("River"))
            return "River";
        else if (locationName == "Ocean" || locationName.Contains("Submarine"))
            return "Ocean";
        else
            return "Lake";  // Default category for ponds, lakes, etc.
    }

    // serializes all fishing state data to json format for transmission
    public string ToJson()
    {
        var sb = new StringBuilder();
        
        sb.Append("{"); 
        
        // base fishing state
        sb.AppendLine($"\"HasFishingRod\": {this.HasFishingRod.ToString().ToLower()},");
        sb.AppendLine($"\"CastingPower\": {this.CastingPower},");
        sb.AppendLine($"\"IsFishing\": {this.IsFishing.ToString().ToLower()},");
        sb.AppendLine($"\"IsNibbling\": {this.IsNibbling.ToString().ToLower()},");
        sb.AppendLine($"\"PlayerTileX\": {this.PlayerTileX},");
        sb.AppendLine($"\"PlayerTileY\": {this.PlayerTileY},");
        
        // minigame state
        sb.AppendLine($"\"MinigameActive\": {this.MinigameActive.ToString().ToLower()},");
        sb.AppendLine($"\"FishPosition\": {this.FishPosition},");
        sb.AppendLine($"\"BobberBarPosition\": {this.BobberBarPosition},");
        sb.AppendLine($"\"BobberBarHeight\": {this.BobberBarHeight},");
        sb.AppendLine($"\"FishTargetPosition\": {this.FishTargetPosition},"); 
        sb.AppendLine($"\"DistanceFromCatching\": {this.DistanceFromCatching},"); 
        
        // treasure state
        sb.AppendLine($"\"TreasureAppeared\": {this.TreasureAppeared.ToString().ToLower()},");
        sb.AppendLine($"\"TreasurePosition\": {this.TreasurePosition},");
        
        // velocity data
        sb.AppendLine($"\"BobberBarVelocity\": {this.BobberBarVelocity},");
        sb.AppendLine($"\"FishVelocity\": {this.FishVelocity},");
        sb.AppendLine($"\"Difficulty\": {this.Difficulty},");
        
        // AUGMENTED STATE: Contextual variables
        sb.AppendLine($"\"RodType\": \"{this.RodType}\",");
        sb.AppendLine($"\"Location\": \"{this.Location}\",");
        sb.AppendLine($"\"Weather\": \"{this.Weather}\",");
        sb.AppendLine($"\"Season\": \"{this.Season}\",");
        
        // last property without trailing comma
        sb.Append($"\"TimeOfDay\": {this.TimeOfDay}"); 
        
        sb.AppendLine("}");

        // return json string with newlines removed for compact transmission
        return sb.ToString().Replace(Environment.NewLine, "");
    }
}