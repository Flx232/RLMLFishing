import numpy as np
import matplotlib.pyplot as plt
from collections import defaultdict
import os

LOG_FILE = 'dqn_training_log.txt'

def analyze_and_plot_dqn_data(log_filepath):
    if not os.path.exists(log_filepath):
        print(f"error: Log file '{log_filepath}'> {log_filepath}'?")
        return

    data = defaultdict(list)

    try:
        with open(log_filepath, 'r') as f:
            header = None
            for line in f:
                if line.startswith('[HEADER]'):
                    header = line.strip().replace('[HEADER]', '').split(',')
                    continue

                if line.startswith('[DATA]'):
                    if not header:
                        print("[DATA] found before [HEADER] cant parse")
                        return

                    values = line.strip().replace('[DATA]', '').split(',')

                    for key, val in zip(header, values):
                        try:
                            data[key].append(float(val))
                        except ValueError:
                            continue

    except Exception as e:
        print(f"error occurred during file reading/parsing: {e}")
        return

    for key in data:
        data[key] = np.array(data[key])

    if not data['EPISODE'].size:
        print("no valid data points")
        return

    metrics_to_analyze = {
        'REWARD': data['REWARD'],
        'EPSILON': data['EPSILON'],
        'FORCE': data['FORCE'],
        'ERROR (Signed Distance)': data['ERROR'],
        'ABS_ERROR (Distance)': data['ABS_ERROR'],
    }

    summary = []

    for name, arr in metrics_to_analyze.items():
        if arr.size > 0:
            summary.append({
                'metric': name,
                'min': np.min(arr),
                'max': np.max(arr),
                'std dev': np.std(arr),
                'mean': np.mean(arr)
            })

    print("\n" + "=" * 80)
    print("DQN AGENT PERFORMANCE AND SPREAD ANALYSIS (Across All Ticks/Episodes)")
    print("=" * 80)
    print(f"{'metric':<25}{'min':<15}{'max':<15}{'mean':<15}{'std dev':<15}")
    print("-" * 80)

    for row in summary:
        print(
            f"{row['metric']:<25}"
            f"{row['min']:<15.4f}"
            f"{row['max']:<15.4f}"
            f"{row['mean']:<15.4f}"
            f"{row['std dev']:<15.4f}"
        )

    print("=" * 80 + "\n")

    first_episode_data_indices = np.where(data['EPISODE'] == 0)[0]

    if first_episode_data_indices.size < 10:
        print("insufficient data")
        plot_data = data
    else:
        plot_data = {key: data[key][first_episode_data_indices] for key in data}

    plt.figure(figsize=(12, 6))
    plt.plot(plot_data['TICK'], plot_data['ERROR'], label='Error (Fish Y - Bar Center Y)', color='blue')
    plt.axhline(0, color='red', linestyle='--', linewidth=0.8, label='Target (Error = 0)')
    plt.title('agent control performance: error over ticks (first minigame)')
    plt.xlabel('game tick (time)')
    plt.ylabel('error (fish position - bar center position)')
    plt.grid(True, linestyle=':', alpha=0.6)
    plt.legend()
    plt.tight_layout()
    plt.show()

if __name__ == "__main__":
    analyze_and_plot_dqn_data(LOG_FILE)
