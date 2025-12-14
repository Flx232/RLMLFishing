# Stardew Valley RL Fishing Agent

Reinforcement Learning agents (Q-Learning and Deep Q-Networks) for mastering the Stardew Valley fishing minigame. This project uses a custom C# mod to interface with the game and Python-based RL agents for real-time control.

## Prerequisites

### Game Requirements
- **Stardew Valley Version:** `1.6.15` (exact version required)(We do not encourage piracy, please support ConcernedApe)
- **SMAPI Version:** `4.3.2` (Stardew Modding API)

### Installation Steps

#### 1. Install SMAPI (Stardew Modding API)

SMAPI is required to load the RL Fishing mod into Stardew Valley.

**Installation Guide:** https://stardewvalleywiki.com/Modding:Player_Guide/Getting_Started

**Quick Installation:**
1. Download SMAPI 4.3.2 from https://smapi.io/
2. Extract the downloaded file
3. Run the installer for your operating system:
   - Windows: `install on Windows.bat`
   - macOS: `install on Mac.command`
   - Linux: `install on Linux.sh`
4. Follow the on-screen instructions
5. Verify installation by launching the game through SMAPI (not Steam directly)

#### 2. Install the RL Fishing Mod

1. Locate your Stardew Valley `Mods` folder:
   - Windows: `%appdata%\StardewValley\Mods`
   - macOS: `~/.config/StardewValley/Mods`
   - Linux: `~/.config/StardewValley/Mods`
   
2. Extract `rl_fishing.zip` into the `Mods` folder
   - You should have: `Mods/RLFishing/` containing the mod files

3. Launch Stardew Valley through SMAPI to verify the mod loads correctly

#### 3. Install Python Dependencies

```bash
# Python 3.8+ required
pip install numpy
```

No additional dependencies needed - the agents use only standard library and NumPy!

## Running the RL Agents

### Step-by-Step Instructions

**Important:** The agents run in real-time alongside the game. Follow these steps in order:

#### 1. Launch Stardew Valley with SMAPI
```bash
# Launch through SMAPI (not Steam directly)
# The game will start with mods loaded
```

#### 2. Load Your Save and Start Fishing
- Load your save file
- Equip a fishing rod (Bamboo, Fiberglass, or Iridium)
- Go to a fishing location (e.g., Pelican Town river)
- Cast your line (the mod will take over once a fish bites)

#### 3. Run the RL Agent (in a separate terminal)

**Option A: Q-Learning Agent**
```bash
# Run with output to console
python q_learning.py

# RECOMMENDED: Write output to file for analysis
python q_learning.py 2> q_learning_output.txt
```

**Option B: Deep Q-Network (DQN) Agent**
```bash
# Run with output to console
python DQN.py

# RECOMMENDED: Write output to file for analysis
python DQN.py 2> dqn_output.txt
```

## Understanding the Output

### Data Format

Both agents output structured training data in the following format:

```
[HEADER]MODE,TICK,EPISODE,BAR_POS,FISH_POS,REWARD,FORCE,Q_HOLD,EPSILON,TD_ERROR
[DATA]AUGMENTED-3D,1,0,320.5,315.2,0.8234,-0.25,0.4532,0.49997500,0.1234
[DATA]AUGMENTED-3D,2,0,318.3,312.8,0.8567,0.00,0.4589,0.49995000,0.0876
...
```

**Columns:**
- `MODE`: State representation used (AUGMENTED-3D or AUGMENTED-7D)
- `TICK`: Timestep within current episode
- `EPISODE`: Episode number (fish catch attempt)
- `BAR_POS`: Bobber bar center position
- `FISH_POS`: Fish position
- `REWARD`: Reward received this timestep
- `FORCE`: Control force applied
- `Q_HOLD`: Q-value for holding action
- `EPSILON`: Current exploration rate
- `TD_ERROR`: Temporal difference error

### Agent Hyperparameters

Both agents can be configured by editing the Python files:

**Q-Learning (`q_learning.py`):**
```python
ALPHA = 0.001          # Learning rate
GAMMA = 0.95           # Discount factor
EPSILON = 0.5          # Initial exploration rate
EPSILON_DECAY = 0.99995  # Exploration decay
```

**DQN (`DQN.py`):**
```python
ALPHA = 0.001          # Learning rate
GAMMA = 0.95           # Discount factor
EPSILON = 0.5          # Initial exploration rate
EPSILON_DECAY = 0.99995  # Exploration decay
BATCH_SIZE = 32        # Training batch size
MEMORY_SIZE = 5000     # Replay buffer capacity
SYNC_RATE = 2000       # Target network update frequency
```

### State Augmentation

Both agents support state augmentation flags:

**Q-Learning:**
```python
USE_AUGMENTED_STATE = True  # 3D state (error + rod + difficulty)
                            # False: 1D state (error only)
```

**DQN:**
```python
USE_AUGMENTED_STATE = True  # 7D state (error + velocities + context)
                            # False: 3D state (error + velocities)
```

## References

- **FishBot mod** inspired by: https://git.ulra.eu/adro/sdv-fishbot

## Contact

For questions or issues:
- Akash Basu: ab3334@columbia.edu
- Frank Xu: fx65@columbia.edu
