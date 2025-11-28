using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Xml.XPath;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;
using HarmonyLib;

namespace RLMLFishing
{
    internal sealed class ModEntry : Mod
    {
        private int? oldXP = null;
        private string bobberInfo = "";
        private string catchInfo = "";
        private static bool _shouldPressFishingButton = false;
        private static bool _isInBobberBarUpdate = false;

        public override void Entry(IModHelper helper)
        {
            var harmony = new Harmony(ModManifest.UniqueID);
            harmony.Patch(
                original: AccessTools.Method(typeof(Game1), "isOneOfTheseKeysDown", new Type[] { typeof(KeyboardState), typeof(InputButton[]) }),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(Pre_Game1_IsOneOfTheseKeysDown))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(BobberBar), "update"),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(Pre_BobberBar_Update)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Post_BobberBar_Update))
            );
            helper.Events.GameLoop.UpdateTicked += this.OnUpdate;
        }
        public static void Pre_BobberBar_Update(BobberBar __instance) => _isInBobberBarUpdate = true;
        public static void Post_BobberBar_Update() => _isInBobberBarUpdate = false;


        public static bool Pre_Game1_IsOneOfTheseKeysDown(KeyboardState state, InputButton[] keys, ref bool __result)
        {
            if (_isInBobberBarUpdate && keys != null && keys.Contains(Game1.options.useToolButton[0]))
            {
                __result = _shouldPressFishingButton; 
                return false;
            }
            return true;
        }
        private enum FishingState { Idle, Playing, FishCaught, FishEscaped }
        private FishingState GetFishingState(FishingRod rod) => true switch
        {
            _ when Game1.activeClickableMenu is BobberBar bobberBar => bobberBar.distanceFromCatching > 0f ? FishingState.Playing : FishingState.FishEscaped,
            _ when rod.fishCaught => FishingState.FishCaught,
            _ => FishingState.Idle
        };

        private void readFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string content = File.ReadAllText(filePath).Trim();
                    _shouldPressFishingButton = bool.Parse(content);
                    this.Monitor.Log($"{_shouldPressFishingButton}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading boolean from file: {ex.Message}");
            }
        }

        private async void OnUpdate(object? sender, UpdateTickedEventArgs e)
        {
            if (!e.IsMultipleOf(15) || !Context.IsWorldReady || Game1.player.CurrentTool is not FishingRod rod)
                return;
            if (oldXP is null)
                oldXP = Game1.player.experiencePoints[1];
            int? newXP = oldXP;
            string docPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            Action fish = GetFishingState(rod) switch
            {
                FishingState.Playing => async () =>
                {
                    readFile(Path.Combine(docPath, "Input.txt"));
                    bobberInfo = castBobber((BobberBar)Game1.activeClickableMenu);
                    using (StreamWriter outputFile = new StreamWriter(Path.Combine(docPath, "Output.txt")))
                    {
                        await outputFile.WriteLineAsync(bobberInfo);
                    }
                }
                ,
                FishingState.FishCaught => async () =>
                {
                    catchInfo = reward(rod);
                    newXP = Game1.player.experiencePoints[1];
                    if (newXP - oldXP != 0)
                    {
                        oldXP = newXP;
                        catchInfo += $"{newXP - oldXP}";
                    }
                    using (StreamWriter outputFile = new StreamWriter(Path.Combine(docPath, "Output.txt")))
                    {
                        await outputFile.WriteLineAsync(catchInfo);
                    }
                }
                ,
                _ => () => { }
                ,
            };
            fish();
        }
        private string reward(FishingRod rod)
        {
            bool fishIsObject = rod.whichFish.TypeIdentifier == "(O)";
            string name = (fishIsObject ? rod.whichFish.GetParsedOrErrorData().DisplayName : "???");
            // this.Monitor.Log($"{rod.fishCaught}, {rod.fishQuality}, {name}", LogLevel.Debug);
            return $"{rod.fishCaught}, {rod.fishQuality}, {name}";
        }

        private string castBobber(BobberBar bobberBar)
        {
            bool treasureInBar = bobberBar.treasurePosition + 12f <= bobberBar.bobberBarPos - 32f + (float)bobberBar.bobberBarHeight && bobberBar.treasurePosition - 16f >= bobberBar.bobberBarPos - 32f;
            // this.Monitor.Log($"{bobberBar.bobberInBar}, {treasureInBar}, {bobberBar.bobberPosition}, {bobberBar.bobberTargetPosition}, {bobberBar.treasurePosition}, {bobberBar.distanceFromCatching}, {bobberBar.treasureCaught}, {bobberBar.fishShake.X}, {bobberBar.fishShake.Y}", LogLevel.Debug);
            return $"{bobberBar.bobberInBar}, {treasureInBar}, {bobberBar.bobberPosition}, {bobberBar.bobberTargetPosition}, {bobberBar.treasurePosition}, {bobberBar.distanceFromCatching}, {bobberBar.treasureCaught}, {bobberBar.fishShake.X}, {bobberBar.fishShake.Y}";
        }
    }
}