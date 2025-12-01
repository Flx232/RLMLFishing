// data class for capturing and serializing fishing state for reinforcement learning agents
// extracts fishing rod state, player position, and minigame data from stardew valley
// uses reflection to access private bobberbar fields for detailed state information
// serializes all state data to json format for transmission to python rl agents

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
    public float DistanceFromCatching { get; set; } // distance to fill the catch meter (0.0 to 1.0)

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

        // capture fishing rod state if equipped
        if (Game1.player.CurrentTool is FishingRod rod)
        {
            this.HasFishingRod = true;
            this.CastingPower = rod.castingPower; 
            this.IsFishing = rod.isFishing;
            this.IsNibbling = rod.isNibbling;
            this.BobberExists = this.IsFishing && rod.bobber != null;
            
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
        
        // last property without trailing comma
        sb.Append($"\"Difficulty\": {this.Difficulty}"); 
        
        sb.AppendLine("}");

        // return json string with newlines removed for compact transmission
        return sb.ToString().Replace(Environment.NewLine, "");
    }
}