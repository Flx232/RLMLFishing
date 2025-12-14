"""
Statistical Comparison of Baseline vs Augmented State Experiments
Performs t-tests and generates comparison reports
"""

import pandas as pd
import numpy as np
from scipy import stats
from pathlib import Path
import json

def load_experiment_metrics(log_file):
    """Load metrics from an experiment log file"""
    from analyze_experiments import ExperimentAnalyzer
    
    analyzer = ExperimentAnalyzer(log_file)
    metrics = analyzer.compute_episode_metrics()
    
    return metrics, analyzer.metadata

def compare_two_experiments(baseline_file, augmented_file, convergence_window=0.2):
    """
    Compare baseline vs augmented experiment with statistical tests
    
    Args:
        baseline_file: Path to baseline experiment log
        augmented_file: Path to augmented experiment log
        convergence_window: Fraction of final episodes to use for convergence metrics (default: 0.2 = last 20%)
    """
    
    print("\n" + "="*80)
    print("STATISTICAL COMPARISON: BASELINE vs AUGMENTED")
    print("="*80)
    
    # Load data
    baseline_metrics, baseline_meta = load_experiment_metrics(baseline_file)
    augmented_metrics, augmented_meta = load_experiment_metrics(augmented_file)
    
    if baseline_metrics is None or augmented_metrics is None:
        print("Error: Could not load metrics from one or both files")
        return
    
    # Print experiment info
    print(f"\nBaseline Experiment: {Path(baseline_file).stem}")
    if baseline_meta:
        print(f"  Description: {baseline_meta.get('description', 'N/A')}")
        print(f"  State Type: {'Augmented' if baseline_meta.get('use_augmented_state') else 'Baseline'}")
    print(f"  Episodes: {len(baseline_metrics)}")
    
    print(f"\nAugmented Experiment: {Path(augmented_file).stem}")
    if augmented_meta:
        print(f"  Description: {augmented_meta.get('description', 'N/A')}")
        print(f"  State Type: {'Augmented' if augmented_meta.get('use_augmented_state') else 'Baseline'}")
    print(f"  Episodes: {len(augmented_metrics)}")
    
    # Get convergence window (last N% of episodes)
    n_baseline = len(baseline_metrics)
    n_augmented = len(augmented_metrics)
    
    baseline_conv = baseline_metrics.iloc[int(n_baseline * (1 - convergence_window)):]
    augmented_conv = augmented_metrics.iloc[int(n_augmented * (1 - convergence_window)):]
    
    print(f"\nConvergence Window: Last {convergence_window*100:.0f}% of episodes")
    print(f"  Baseline: {len(baseline_conv)} episodes")
    print(f"  Augmented: {len(augmented_conv)} episodes")
    
    # Statistical comparisons
    print("\n" + "-"*80)
    print("STATISTICAL TESTS (Convergence Period)")
    print("-"*80)
    
    metrics_to_compare = {
        'mean_reward': 'Mean Reward per Episode',
        'mean_absolute_error': 'Mean Absolute Error (pixels)',
        'success_rate': 'Success Rate (proportion)',
        'total_reward': 'Total Reward per Episode',
        'duration_ticks': 'Episode Duration (ticks)'
    }
    
    results = []
    
    for metric_key, metric_name in metrics_to_compare.items():
        baseline_values = baseline_conv[metric_key].values
        augmented_values = augmented_conv[metric_key].values
        
        # Compute statistics
        baseline_mean = np.mean(baseline_values)
        baseline_std = np.std(baseline_values)
        augmented_mean = np.mean(augmented_values)
        augmented_std = np.std(augmented_values)
        
        # Perform t-test
        t_stat, p_value = stats.ttest_ind(baseline_values, augmented_values)
        
        # Calculate effect size (Cohen's d)
        pooled_std = np.sqrt((baseline_std**2 + augmented_std**2) / 2)
        cohens_d = (augmented_mean - baseline_mean) / pooled_std if pooled_std > 0 else 0
        
        # Determine significance
        if p_value < 0.001:
            sig_str = "***"
        elif p_value < 0.01:
            sig_str = "**"
        elif p_value < 0.05:
            sig_str = "*"
        else:
            sig_str = "ns"
        
        # Determine improvement direction
        if metric_key in ['mean_reward', 'success_rate', 'total_reward', 'duration_ticks']:
            improvement = augmented_mean > baseline_mean
            improvement_pct = ((augmented_mean - baseline_mean) / baseline_mean) * 100 if baseline_mean != 0 else 0
        else:  # For error metrics, lower is better
            improvement = augmented_mean < baseline_mean
            improvement_pct = ((baseline_mean - augmented_mean) / baseline_mean) * 100 if baseline_mean != 0 else 0
        
        results.append({
            'metric': metric_name,
            'baseline_mean': baseline_mean,
            'baseline_std': baseline_std,
            'augmented_mean': augmented_mean,
            'augmented_std': augmented_std,
            't_statistic': t_stat,
            'p_value': p_value,
            'cohens_d': cohens_d,
            'significance': sig_str,
            'improvement': improvement,
            'improvement_pct': improvement_pct
        })
        
        print(f"\n{metric_name}:")
        print(f"  Baseline:  {baseline_mean:.4f} ± {baseline_std:.4f}")
        print(f"  Augmented: {augmented_mean:.4f} ± {augmented_std:.4f}")
        print(f"  t-statistic: {t_stat:.4f}")
        print(f"  p-value: {p_value:.6f} {sig_str}")
        print(f"  Effect size (Cohen's d): {cohens_d:.4f}")
        if improvement:
            print(f"  ✓ Augmented is {'better' if improvement else 'worse'} by {abs(improvement_pct):.2f}%")
        else:
            print(f"  ✗ Augmented is worse by {abs(improvement_pct):.2f}%")
    
    # Overall summary
    print("\n" + "="*80)
    print("SUMMARY")
    print("="*80)
    
    significant_improvements = sum(1 for r in results if r['improvement'] and r['significance'] != 'ns')
    significant_degradations = sum(1 for r in results if not r['improvement'] and r['significance'] != 'ns')
    
    print(f"\nSignificant Improvements: {significant_improvements}/{len(results)}")
    print(f"Significant Degradations: {significant_degradations}/{len(results)}")
    
    # Convergence speed analysis
    print("\n" + "-"*80)
    print("CONVERGENCE SPEED ANALYSIS")
    print("-"*80)
    
    # Find episode where baseline/augmented first reach 50% of their final performance
    baseline_final_reward = baseline_metrics['mean_reward'].iloc[-20:].mean()
    augmented_final_reward = augmented_metrics['mean_reward'].iloc[-20:].mean()
    
    baseline_target = baseline_final_reward * 0.5
    augmented_target = augmented_final_reward * 0.5
    
    # Rolling average to smooth
    baseline_smooth = baseline_metrics['mean_reward'].rolling(20).mean()
    augmented_smooth = augmented_metrics['mean_reward'].rolling(20).mean()
    
    baseline_conv_episode = baseline_smooth[baseline_smooth >= baseline_target].index[0] if any(baseline_smooth >= baseline_target) else len(baseline_metrics)
    augmented_conv_episode = augmented_smooth[augmented_smooth >= augmented_target].index[0] if any(augmented_smooth >= augmented_target) else len(augmented_metrics)
    
    print(f"\nEpisodes to reach 50% of final performance:")
    print(f"  Baseline: {baseline_conv_episode} episodes")
    print(f"  Augmented: {augmented_conv_episode} episodes")
    
    if augmented_conv_episode < baseline_conv_episode:
        speedup = ((baseline_conv_episode - augmented_conv_episode) / baseline_conv_episode) * 100
        print(f"  ✓ Augmented converges {speedup:.1f}% faster")
    else:
        slowdown = ((augmented_conv_episode - baseline_conv_episode) / baseline_conv_episode) * 100
        print(f"  ✗ Augmented converges {slowdown:.1f}% slower")
    
    # Save results to file
    results_df = pd.DataFrame(results)
    output_file = f"comparison_{Path(baseline_file).stem}_vs_{Path(augmented_file).stem}.csv"
    results_df.to_csv(output_file, index=False)
    print(f"\n✓ Detailed results saved to: {output_file}")
    
    # Interpretation guide
    print("\n" + "-"*80)
    print("SIGNIFICANCE LEVELS")
    print("-"*80)
    print("  *   : p < 0.05  (significant)")
    print("  **  : p < 0.01  (highly significant)")
    print("  *** : p < 0.001 (very highly significant)")
    print("  ns  : p ≥ 0.05  (not significant)")
    print("\nCOHEN'S D (Effect Size):")
    print("  |d| < 0.2  : Small effect")
    print("  |d| < 0.5  : Medium effect")
    print("  |d| ≥ 0.5  : Large effect")
    print("="*80 + "\n")

def main():
    import sys
    
    if len(sys.argv) != 3:
        print("Usage: python compare_experiments.py <baseline_log> <augmented_log>")
        print("\nExample:")
        print("  python compare_experiments.py \\")
        print("    data/baseline_dqn_baseline_20241213.log \\")
        print("    data/augmented_dqn_augmented_20241213.log")
        sys.exit(1)
    
    baseline_file = sys.argv[1]
    augmented_file = sys.argv[2]
    
    if not Path(baseline_file).exists():
        print(f"Error: Baseline file not found: {baseline_file}")
        sys.exit(1)
    
    if not Path(augmented_file).exists():
        print(f"Error: Augmented file not found: {augmented_file}")
        sys.exit(1)
    
    compare_two_experiments(baseline_file, augmented_file)

if __name__ == '__main__':
    main()