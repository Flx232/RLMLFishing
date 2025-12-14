"""
Data Analysis Script for RL Fishing Experiments
Parses log files and generates performance metrics
"""

import re
import json
import pandas as pd
import numpy as np
from pathlib import Path
import matplotlib.pyplot as plt
import seaborn as sns

class ExperimentAnalyzer:
    """Analyzes experiment log files and generates metrics"""
    
    def __init__(self, log_file):
        self.log_file = log_file
        self.data = None
        self.metadata = None
        self.load_data()
    
    def load_data(self):
        """Load and parse log file"""
        print(f"Loading data from {self.log_file}...")
        
        # Try to load metadata
        metadata_file = self.log_file.replace('.log', '_metadata.json')
        if Path(metadata_file).exists():
            with open(metadata_file, 'r') as f:
                self.metadata = json.load(f)
                print(f"  ✓ Loaded metadata")
        
        # Parse log file
        data_rows = []
        with open(self.log_file, 'r') as f:
            for line in f:
                # Look for [DATA] lines
                if '[DATA]' in line:
                    # Parse data line
                    # Format: [DATA]MODE,TICK,EPISODE,BAR_POS,FISH_POS,REWARD,FORCE,Q_HOLD,EPSILON,TD_ERROR
                    match = re.search(r'\[DATA\](.+)', line)
                    if match:
                        parts = match.group(1).split(',')
                        if len(parts) >= 10:
                            try:
                                row = {
                                    'mode': parts[0],
                                    'tick': int(parts[1]),
                                    'episode': int(parts[2]),
                                    'bar_pos': float(parts[3]),
                                    'fish_pos': float(parts[4]),
                                    'reward': float(parts[5]),
                                    'force': float(parts[6]),
                                    'q_hold': float(parts[7]),
                                    'epsilon': float(parts[8]),
                                    'td_error': float(parts[9])
                                }
                                data_rows.append(row)
                            except (ValueError, IndexError):
                                continue
        
        self.data = pd.DataFrame(data_rows)
        print(f"  ✓ Loaded {len(self.data)} data points across {self.data['episode'].max() + 1} episodes")
    
    def compute_episode_metrics(self):
        """Compute per-episode metrics"""
        if self.data is None or len(self.data) == 0:
            print("No data to analyze")
            return None
        
        episode_metrics = []
        
        for episode_num in self.data['episode'].unique():
            episode_data = self.data[self.data['episode'] == episode_num]
            
            # Calculate error (distance between fish and bar center)
            episode_data['error'] = episode_data['fish_pos'] - episode_data['bar_pos']
            
            metrics = {
                'episode': episode_num,
                'duration_ticks': len(episode_data),
                'mean_reward': episode_data['reward'].mean(),
                'total_reward': episode_data['reward'].sum(),
                'mean_absolute_error': episode_data['error'].abs().mean(),
                'max_absolute_error': episode_data['error'].abs().max(),
                'mean_td_error': episode_data['td_error'].abs().mean(),
                'final_epsilon': episode_data['epsilon'].iloc[-1],
                'success_rate': (episode_data['error'].abs() < 20).mean(),  # % time fish in bar zone
            }
            
            episode_metrics.append(metrics)
        
        return pd.DataFrame(episode_metrics)
    
    def print_summary(self):
        """Print summary statistics"""
        episode_metrics = self.compute_episode_metrics()
        if episode_metrics is None:
            return
        
        print("\n" + "="*70)
        print("EXPERIMENT SUMMARY")
        print("="*70)
        
        if self.metadata:
            print(f"Experiment: {self.metadata.get('experiment_name', 'Unknown')}")
            print(f"Description: {self.metadata.get('description', 'N/A')}")
            print(f"Agent Type: {self.metadata.get('agent_type', 'Unknown')}")
            print(f"State Type: {'Augmented' if self.metadata.get('use_augmented_state') else 'Baseline'}")
        
        print(f"\nTotal Episodes: {len(episode_metrics)}")
        print(f"Total Ticks: {episode_metrics['duration_ticks'].sum()}")
        
        print("\nPerformance Metrics (All Episodes):")
        print(f"  Mean Reward per Episode: {episode_metrics['mean_reward'].mean():.4f} ± {episode_metrics['mean_reward'].std():.4f}")
        print(f"  Mean Absolute Error: {episode_metrics['mean_absolute_error'].mean():.2f} ± {episode_metrics['mean_absolute_error'].std():.2f}")
        print(f"  Mean Success Rate: {episode_metrics['success_rate'].mean():.2%}")
        
        # Performance in last 20% of episodes (convergence)
        n_episodes = len(episode_metrics)
        last_20pct = episode_metrics.iloc[int(n_episodes * 0.8):]
        
        print("\nPerformance Metrics (Last 20% of Episodes):")
        print(f"  Mean Reward per Episode: {last_20pct['mean_reward'].mean():.4f} ± {last_20pct['mean_reward'].std():.4f}")
        print(f"  Mean Absolute Error: {last_20pct['mean_absolute_error'].mean():.2f} ± {last_20pct['mean_absolute_error'].std():.2f}")
        print(f"  Mean Success Rate: {last_20pct['success_rate'].mean():.2%}")
        
        print("\nLearning Progression:")
        print(f"  Initial Epsilon: {episode_metrics['final_epsilon'].iloc[0]:.6f}")
        print(f"  Final Epsilon: {episode_metrics['final_epsilon'].iloc[-1]:.6f}")
        
        print("="*70 + "\n")
        
        return episode_metrics
    
    def plot_learning_curves(self, output_file=None):
        """Generate learning curve visualizations"""
        episode_metrics = self.compute_episode_metrics()
        if episode_metrics is None:
            return
        
        # Create figure with subplots
        fig, axes = plt.subplots(2, 2, figsize=(14, 10))
        fig.suptitle(f'Learning Curves: {Path(self.log_file).stem}', fontsize=14, fontweight='bold')
        
        # Smooth the curves using rolling average
        window = max(10, len(episode_metrics) // 50)
        
        # Plot 1: Mean Reward over Episodes
        ax = axes[0, 0]
        ax.plot(episode_metrics['episode'], episode_metrics['mean_reward'].rolling(window).mean(), 
                linewidth=2, label='Mean Reward (smoothed)')
        ax.fill_between(episode_metrics['episode'], 
                        episode_metrics['mean_reward'].rolling(window).mean() - episode_metrics['mean_reward'].rolling(window).std(),
                        episode_metrics['mean_reward'].rolling(window).mean() + episode_metrics['mean_reward'].rolling(window).std(),
                        alpha=0.3)
        ax.set_xlabel('Episode')
        ax.set_ylabel('Mean Reward')
        ax.set_title('Reward Over Time')
        ax.legend()
        ax.grid(True, alpha=0.3)
        
        # Plot 2: Success Rate over Episodes
        ax = axes[0, 1]
        ax.plot(episode_metrics['episode'], episode_metrics['success_rate'].rolling(window).mean() * 100,
                linewidth=2, color='green', label='Success Rate (smoothed)')
        ax.set_xlabel('Episode')
        ax.set_ylabel('Success Rate (%)')
        ax.set_title('Success Rate Over Time')
        ax.legend()
        ax.grid(True, alpha=0.3)
        
        # Plot 3: Mean Absolute Error over Episodes
        ax = axes[1, 0]
        ax.plot(episode_metrics['episode'], episode_metrics['mean_absolute_error'].rolling(window).mean(),
                linewidth=2, color='red', label='MAE (smoothed)')
        ax.set_xlabel('Episode')
        ax.set_ylabel('Mean Absolute Error (pixels)')
        ax.set_title('Tracking Error Over Time')
        ax.legend()
        ax.grid(True, alpha=0.3)
        
        # Plot 4: Epsilon Decay
        ax = axes[1, 1]
        ax.plot(episode_metrics['episode'], episode_metrics['final_epsilon'],
                linewidth=2, color='purple', label='Epsilon')
        ax.set_xlabel('Episode')
        ax.set_ylabel('Epsilon')
        ax.set_title('Exploration Rate Decay')
        ax.set_yscale('log')
        ax.legend()
        ax.grid(True, alpha=0.3)
        
        plt.tight_layout()
        
        if output_file:
            plt.savefig(output_file, dpi=150, bbox_inches='tight')
            print(f"  ✓ Saved plot to {output_file}")
        else:
            plt.show()
        
        plt.close()

def compare_experiments(log_files, labels=None, output_file=None):
    """Compare multiple experiments side by side"""
    if labels is None:
        labels = [Path(f).stem for f in log_files]
    
    fig, axes = plt.subplots(1, 3, figsize=(18, 5))
    fig.suptitle('Experiment Comparison', fontsize=14, fontweight='bold')
    
    colors = plt.cm.tab10(range(len(log_files)))
    
    for i, (log_file, label, color) in enumerate(zip(log_files, labels, colors)):
        analyzer = ExperimentAnalyzer(log_file)
        metrics = analyzer.compute_episode_metrics()
        
        if metrics is None:
            continue
        
        window = max(10, len(metrics) // 50)
        
        # Plot mean reward
        axes[0].plot(metrics['episode'], metrics['mean_reward'].rolling(window).mean(),
                    linewidth=2, label=label, color=color)
        
        # Plot success rate
        axes[1].plot(metrics['episode'], metrics['success_rate'].rolling(window).mean() * 100,
                    linewidth=2, label=label, color=color)
        
        # Plot MAE
        axes[2].plot(metrics['episode'], metrics['mean_absolute_error'].rolling(window).mean(),
                    linewidth=2, label=label, color=color)
    
    axes[0].set_xlabel('Episode')
    axes[0].set_ylabel('Mean Reward')
    axes[0].set_title('Reward Comparison')
    axes[0].legend()
    axes[0].grid(True, alpha=0.3)
    
    axes[1].set_xlabel('Episode')
    axes[1].set_ylabel('Success Rate (%)')
    axes[1].set_title('Success Rate Comparison')
    axes[1].legend()
    axes[1].grid(True, alpha=0.3)
    
    axes[2].set_xlabel('Episode')
    axes[2].set_ylabel('Mean Absolute Error (pixels)')
    axes[2].set_title('Tracking Error Comparison')
    axes[2].legend()
    axes[2].grid(True, alpha=0.3)
    
    plt.tight_layout()
    
    if output_file:
        plt.savefig(output_file, dpi=150, bbox_inches='tight')
        print(f"  ✓ Saved comparison plot to {output_file}")
    else:
        plt.show()
    
    plt.close()

def main():
    import sys
    
    if len(sys.argv) < 2:
        print("Usage: python analyze_experiments.py <log_file> [log_file2 ...]")
        print("   or: python analyze_experiments.py data/*.log  (analyze all)")
        sys.exit(1)
    
    log_files = sys.argv[1:]
    
    if len(log_files) == 1:
        # Single experiment analysis
        analyzer = ExperimentAnalyzer(log_files[0])
        metrics = analyzer.print_summary()
        
        # Generate plots
        output_plot = log_files[0].replace('.log', '_plots.png')
        analyzer.plot_learning_curves(output_plot)
        
        # Save metrics to CSV
        if metrics is not None:
            output_csv = log_files[0].replace('.log', '_metrics.csv')
            metrics.to_csv(output_csv, index=False)
            print(f"  ✓ Saved metrics to {output_csv}")
    
    else:
        # Multiple experiment comparison
        print(f"\nComparing {len(log_files)} experiments...")
        
        for log_file in log_files:
            print(f"\n{'='*70}")
            analyzer = ExperimentAnalyzer(log_file)
            analyzer.print_summary()
        
        # Generate comparison plot
        output_plot = 'data/experiment_comparison.png'
        compare_experiments(log_files, output_file=output_plot)

if __name__ == '__main__':
    main()