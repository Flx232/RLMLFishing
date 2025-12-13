# analyzes dqn agent training logs and visualizes performance metrics
# reads structured log data from training sessions, calculates statistical measures
# of agent performance (reward, epsilon, force, error), and plots error over time
# for the first recorded minigame episode to show how well the agent learns to control

import numpy as np
import matplotlib.pyplot as plt
from collections import defaultdict
import os

LOG_FILE = 'dqn_training_log.txt'

def analyze_and_plot_dqn_data(log_filepath):
    # reads structured log data, calculates measures of spread, and plots
    # the error over the first recorded minigame episode
    
    if not os.path.exists(log_filepath):
        print(f"Error: Log file '{log_filepath}' not found. Did you run 'python3 sock.py 2> {log_filepath}'?")
        return

    data = defaultdict(list)
    
    # read and parse data from log file
    try:
        with open(log_filepath, 'r') as f:
            header = None
            for line in f:
                # capture the header row that defines data columns
                if line.startswith('[HEADER]'):
                    header = line.strip().replace('[HEADER]', '').split(',')
                    continue

                # parse data lines and store by column name
                if line.startswith('[DATA]'):
                    if not header:
                        print("Error: [DATA] found before [HEADER]. Cannot parse.")
                        return

                    values = line.strip().replace('[DATA]', '').split(',')
                    
                    # convert values to float and store them in the dictionary by header name
                    for key, val in zip(header, values):
                        try:
                            data[key].append(float(val))
                        except ValueError:
                            continue

    except Exception as e:
        print(f"An error occurred during file reading/parsing: {e}")
        return

    # convert lists to numpy arrays for efficient computation
    for key in data:
        data[key] = np.array(data[key])
        
    if not data['EPISODE'].size:
        print("No valid data points found in the log file.")
        return

    # calculate measures of spread and summarize performance metrics
    metrics_to_analyze = {
        'REWARD': data['REWARD'],
        'EPSILON': data['EPSILON'],
        'FORCE': data['FORCE'],
        'ERROR (Signed Distance)': data['ERROR'],
        'ABS_ERROR (Distance)': data['ABS_ERROR'],
    }
    
    summary = []
    
    # compute min, max, mean, and standard deviation for each metric
    for name, arr in metrics_to_analyze.items():
        if arr.size > 0:
            summary.append({
                'Metric': name,
                'Min': np.min(arr),
                'Max': np.max(arr),
                'Std Dev': np.std(arr),
                'Mean': np.mean(arr)
            })
    
    # print results table showing performance statistics
    print("\n" + "="*80)
    print("DQN AGENT PERFORMANCE AND SPREAD ANALYSIS (Across All Ticks/Episodes)")
    print("="*80)
    
    print(f"{'Metric':<25}{'Min':<15}{'Max':<15}{'Mean':<15}{'Std Dev':<15}")
    print("-" * 80)
    
    for row in summary:
        print(f"{row['Metric']:<25}{row['Min']:<15.4f}{row['Max']:<15.4f}{row['Mean']:<15.4f}{row['Std Dev']:<15.4f}")
    
    print("="*80 + "\n")

    # plot error for a single minigame to visualize agent control performance
    # find the data for the first complete episode (episode 0)
    first_episode_data_indices = np.where(data['EPISODE'] == 0)[0]

    if first_episode_data_indices.size < 10:
        print("Warning: Insufficient data to plot a single full episode (less than 10 ticks found for EPISODE 0).")
        plot_data = data
    else:
        # filter for only the selected episode
        plot_data = {key: data[key][first_episode_data_indices] for key in data}
        
    # create plot showing error over time
    plt.figure(figsize=(12, 6))
    plt.plot(plot_data['TICK'], plot_data['ERROR'], label='Error (Fish Y - Bar Center Y)', color='blue')
    
    # add zero line for reference showing target
    plt.axhline(0, color='red', linestyle='--', linewidth=0.8, label='Target (Error = 0)')
    
    plt.title(f'Agent Control Performance: Error Over Ticks (First Minigame)')
    plt.xlabel('Game Tick (Time)')
    plt.ylabel('Error (Fish Position - Bar Center Position)')
    plt.grid(True, linestyle=':', alpha=0.6)
    plt.legend()
    plt.tight_layout()
    plt.show()

# run the analysis
if __name__ == "__main__":
    analyze_and_plot_dqn_data(LOG_FILE)