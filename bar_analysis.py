# analyzes q-learning agent training logs and calculates performance statistics
# reads log files containing agent state data (positions, rewards, q-values),
# computes displacement error between fish and bar positions, and prints
# summary statistics showing agent performance across all episodes

import re
import pandas as pd
import sys
import os

def load_and_analyze_log(log_path):
    # loads the q-learning log, calculates performance spread, and prints a summary table
    
    if not os.path.exists(log_path):
        print(f"Error: Log file not found at '{log_path}'", file=sys.stderr)
        return
    
    # load data from log file
    data_lines = []
    try:
        with open(log_path, 'r') as f:
            # extract csv data from lines marked with [DATA] tag
            for line in f:
                if line.startswith('[DATA]'):
                    csv_data = line[len('[DATA]'):].strip()
                    data_lines.append(csv_data.split(','))
    except Exception as e:
        print(f"Error reading log file: {e}", file=sys.stderr)
        return
    
    if not data_lines:
        print("No [DATA] entries found in the log file. Make sure the agent is running and logging correctly.", file=sys.stderr)
        return
    
    # define column names matching the order in the agent's log output
    column_names = [
        'TICK', 'EPISODE', 'BAR_POS', 'FISH_POS', 'REWARD', 
        'FORCE', 'Q_HOLD', 'EPSILON', 'TD_ERROR'
    ]
    
    # convert data to pandas dataframe with float values
    try:
        df = pd.DataFrame(data_lines, columns=column_names).astype(float)
    except ValueError as e:
        print(f"Error converting data to float. Check log integrity. Details: {e}", file=sys.stderr)
        return
    
    # calculate absolute displacement between fish position and bar center position
    # this represents the agent's control error
    df['ABS_DISPLACEMENT'] = (df['FISH_POS'] - df['BAR_POS']).abs()
    
    # select metrics to analyze and calculate descriptive statistics
    metrics_to_analyze = [
        'REWARD', 
        'EPSILON', 
        'FORCE', 
        'BAR_POS', 
        'FISH_POS', 
        'ABS_DISPLACEMENT'
    ]
    
    # compute min, max, mean, and standard deviation for each metric
    summary = df[metrics_to_analyze].describe().T[['min', 'max', 'mean', 'std']]
    
    # rename columns and index for display formatting
    summary.columns = ['Min', 'Max', 'Mean', 'Std Dev']
    summary.index.name = 'Metric'
    
    # customize metric names for better readability in output
    summary = summary.rename(index={
        'ABS_DISPLACEMENT': 'DISPLACEMENT (Abs Error)',
        'BAR_POS': 'BAR_POS (Center)',
        'FISH_POS': 'FISH_POS'
    })
    
    # format and print the summary table
    print("\n" + "=" * 80)
    print("Q-LEARNING AGENT PERFORMANCE AND SPREAD ANALYSIS (Across All Ticks/Episodes)")
    print("=" * 80)
    
    # define format strings for header and data rows
    header_format = "{:<30}{:>12s}{:>12s}{:>12s}{:>12s}"
    row_format = "{:<30}{:>12.4f}{:>12.4f}{:>12.4f}{:>12.4f}"
    
    # print formatted header
    print(header_format.format("Metric", "Min", "Max", "Mean", "Std Dev"))
    print("-" * 80)
    
    # print formatted data rows for each metric
    for metric, row in summary.iterrows():
        print(row_format.format(
            metric, 
            row['Min'], 
            row['Max'], 
            row['Mean'], 
            row['Std Dev']
        ))
    
    print("=" * 80)

# entry point for command line execution
if __name__ == '__main__':
    # check for correct command line arguments
    if len(sys.argv) != 2:
        print("Usage: python analyze_fishing_log.py <path_to_log_file>", file=sys.stderr)
        sys.exit(1)
    
    # run analysis on provided log file
    log_file_path = sys.argv[1]
    load_and_analyze_log(log_file_path)