"""
Experiment Runner for Stardew Valley RL Fishing
Manages different experimental configurations and data collection
"""

import subprocess
import sys
import time
import datetime
import os
import json

class ExperimentConfig:
    """Configuration for a single experiment run"""
    def __init__(self, name, agent_type, use_augmented, episodes, description):
        self.name = name
        self.agent_type = agent_type  # 'dqn' or 'qlearning'
        self.use_augmented = use_augmented
        self.episodes = episodes
        self.description = description
        self.output_file = None
    
    def get_script_name(self):
        if self.agent_type == 'dqn':
            return 'DQN_augmented.py'
        else:
            return 'q_learning_augmented.py'
    
    def get_output_filename(self):
        timestamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
        state_type = "augmented" if self.use_augmented else "baseline"
        return f"data/{self.name}_{self.agent_type}_{state_type}_{timestamp}.log"

# Define all experiment sets from the plan
EXPERIMENTS = {
    # Baseline experiments (Week 1)
    'baseline_dqn_3d': ExperimentConfig(
        name='baseline_dqn',
        agent_type='dqn',
        use_augmented=False,
        episodes=500,
        description='Baseline DQN with 3D state (error, bobber_vel, fish_vel)'
    ),
    
    'baseline_qlearn_1d': ExperimentConfig(
        name='baseline_qlearn',
        agent_type='qlearning',
        use_augmented=False,
        episodes=500,
        description='Baseline Q-Learning with 1D state (error only)'
    ),
    
    # Augmented state experiments (Week 2)
    'augmented_dqn_7d': ExperimentConfig(
        name='augmented_dqn',
        agent_type='dqn',
        use_augmented=True,
        episodes=1000,
        description='Augmented DQN with 7D state (+ rod, difficulty, time, weather)'
    ),
    
    'augmented_qlearn_3d': ExperimentConfig(
        name='augmented_qlearn',
        agent_type='qlearning',
        use_augmented=True,
        episodes=1000,
        description='Augmented Q-Learning with 3D state (error + rod + difficulty)'
    ),
    
    # Transfer learning experiments
    'transfer_single_location': ExperimentConfig(
        name='transfer_single',
        agent_type='dqn',
        use_augmented=True,
        episodes=1000,
        description='Transfer Learning: Train on Beach only'
    ),
    
    'transfer_multi_location': ExperimentConfig(
        name='transfer_multi',
        agent_type='dqn',
        use_augmented=True,
        episodes=1000,
        description='Transfer Learning: Train on 4 locations (250 each)'
    ),
}

def create_data_directory():
    """Ensure data directory exists"""
    if not os.path.exists('data'):
        os.makedirs('data')
        print("Created data directory")

def modify_script_config(script_name, use_augmented):
    """
    Modify the USE_AUGMENTED_STATE flag in the agent script
    """
    with open(script_name, 'r') as f:
        content = f.read()
    
    # Replace the configuration line
    if use_augmented:
        content = content.replace(
            'USE_AUGMENTED_STATE = False',
            'USE_AUGMENTED_STATE = True'
        )
    else:
        content = content.replace(
            'USE_AUGMENTED_STATE = True',
            'USE_AUGMENTED_STATE = False'
        )
    
    with open(script_name, 'w') as f:
        f.write(content)
    
    print(f"  ✓ Set USE_AUGMENTED_STATE = {use_augmented} in {script_name}")

def run_experiment(config):
    """Run a single experiment configuration"""
    print(f"\n{'='*70}")
    print(f"EXPERIMENT: {config.name}")
    print(f"{'='*70}")
    print(f"Description: {config.description}")
    print(f"Agent Type: {config.agent_type}")
    print(f"State Type: {'Augmented' if config.use_augmented else 'Baseline'}")
    print(f"Episodes: {config.episodes}")
    print(f"{'='*70}\n")
    
    # Create data directory
    create_data_directory()
    
    # Get script and output file
    script_name = config.get_script_name()
    output_file = config.get_output_filename()
    
    # Modify script configuration
    modify_script_config(script_name, config.use_augmented)
    
    # Save experiment metadata
    metadata = {
        'experiment_name': config.name,
        'agent_type': config.agent_type,
        'use_augmented_state': config.use_augmented,
        'target_episodes': config.episodes,
        'description': config.description,
        'start_time': datetime.datetime.now().isoformat(),
        'script': script_name,
        'output_file': output_file
    }
    
    metadata_file = output_file.replace('.log', '_metadata.json')
    with open(metadata_file, 'w') as f:
        json.dump(metadata, f, indent=2)
    
    print(f"Starting agent... (output: {output_file})")
    print(f"Metadata saved to: {metadata_file}")
    print("\nIMPORTANT:")
    print("1. Make sure Stardew Valley is running with the RL Fishing mod")
    print("2. Go to a fishing location and start fishing")
    print(f"3. Let the agent run for approximately {config.episodes} episodes")
    print("4. Press Ctrl+C to stop when done\n")
    
    try:
        # Run the agent and redirect stderr to file (where data is logged)
        with open(output_file, 'w') as f:
            process = subprocess.Popen(
                [sys.executable, script_name],
                stderr=f,
                stdout=subprocess.PIPE
            )
            
            # Monitor the process
            while process.poll() is None:
                time.sleep(1)
                
    except KeyboardInterrupt:
        print("\n\nExperiment interrupted by user")
        process.terminate()
    except Exception as e:
        print(f"\nError running experiment: {e}")
    finally:
        # Update metadata with end time
        metadata['end_time'] = datetime.datetime.now().isoformat()
        with open(metadata_file, 'w') as f:
            json.dump(metadata, f, indent=2)
        
        print(f"\n✓ Experiment data saved to: {output_file}")
        print(f"✓ Metadata saved to: {metadata_file}")

def list_experiments():
    """List all available experiments"""
    print("\nAvailable Experiments:")
    print("=" * 70)
    for key, config in EXPERIMENTS.items():
        print(f"\n{key}:")
        print(f"  {config.description}")
        print(f"  Agent: {config.agent_type}, Augmented: {config.use_augmented}, Episodes: {config.episodes}")
    print("=" * 70)

def main():
    if len(sys.argv) < 2:
        print("Usage: python experiment_runner.py <experiment_key>")
        print("   or: python experiment_runner.py list")
        list_experiments()
        sys.exit(1)
    
    command = sys.argv[1]
    
    if command == 'list':
        list_experiments()
        return
    
    if command not in EXPERIMENTS:
        print(f"Error: Unknown experiment '{command}'")
        list_experiments()
        sys.exit(1)
    
    config = EXPERIMENTS[command]
    run_experiment(config)

if __name__ == '__main__':
    main()