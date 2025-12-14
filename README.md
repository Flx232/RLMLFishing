# RLMLFishing - How to Run RLMLFishing in Stardew Valley
## Requirements
UPDATE: USE 'akash-model(s)-and-output' branch instead!!!!!
A valid copy of Stardew Valley (We do not encourage piracy, please support ConcernedApe)
An external modding client called SMAPI
The ability to run python on your console.

## Instructions
Step 1. Buy Stardew Valley. Recommended to buy on steam for easy connection to SMAPI down the line.
Step 2. Go to https://stardewvalleywiki.com/Modding:Player_Guide/Getting_Started. Under Getting started tab, click the OS you are using right now to learn how to install SMAPI.
Step 3. Once SMAPI is configured, Unzip the RLMLFishing and move the contents to the Mods folder in your Stardew Valley folder. SMAPI will automatically link the mod to Stardew Valley.
Step 4. Open up Stardew Running SMAPI. If you are running windows, the shortcut is in the same Stardew Valley folder as the mods folder. Though you can also configure Steam to always star up the modded version.
Step 4.1 If you are starting a new game, it is recommended after you get the fishing rod to fish and catch it yourself once. This will finish the fishing tutorial and allow the Q_learning model to start learning real scenarios.
Step 5. Finally, open up the terminal, go to the directory containing q_learning.py and run the script.
Step 5.1 Or you can run the DQN.py file, which should be located in the same directory.
Step 6. Find the nearest body of water with a fishing pole and start running the experiment
