import re
import pandas as pd
import sys
import os

def load_and_analyze_log(log_path):
    if not os.path.exists(log_path):
        print(f"error: log file not found at '{log_path}'", file=sys.stderr)
        return

    data_lines = []
    try:
        with open(log_path, 'r') as f:
            for line in f:
                if line.startswith('[DATA]'):
                    csv_data = line[len('[DATA]'):].strip()
                    data_lines.append(csv_data.split(','))
    except Exception as e:
        print(f"Error reading log file: {e}", file=sys.stderr)
        return

    if not data_lines:
        print("no [DATA] entries found in the log file", file=sys.stderr)
        return

    column_names = [
        'TICK', 'EPISODE', 'BAR_POS', 'FISH_POS', 'REWARD',
        'FORCE', 'Q_HOLD', 'EPSILON', 'TD_ERROR'
    ]

    try:
        df = pd.DataFrame(data_lines, columns=column_names).astype(float)
    except ValueError as e:
        print(f"error converting data {e}", file=sys.stderr)
        return

    df['ABS_DISPLACEMENT'] = (df['FISH_POS'] - df['BAR_POS']).abs()

    metrics_to_analyze = [
        'REWARD',
        'EPSILON',
        'FORCE',
        'BAR_POS',
        'FISH_POS',
        'ABS_DISPLACEMENT'
    ]

    summary = df[metrics_to_analyze].describe().T[['min', 'max', 'mean', 'std']]
    summary.columns = ['min', 'max', 'mean', 'std dev']
    summary.index.name = 'metric'

    summary = summary.rename(index={
        'ABS_DISPLACEMENT': 'DISPLACEMENT (Abs Error)',
        'BAR_POS': 'BAR_POS (Center)',
        'FISH_POS': 'FISH_POS'
    })

    print("q learning performance")

    header_format = "{:<30}{:>12s}{:>12s}{:>12s}{:>12s}"
    row_format = "{:<30}{:>12.4f}{:>12.4f}{:>12.4f}{:>12.4f}"

    print(header_format.format("Metric", "Min", "Max", "Mean", "Std Dev"))
    print("-" * 80)

    for metric, row in summary.iterrows():
        print(row_format.format(
            metric,
            row['Min'],
            row['Max'],
            row['Mean'],
            row['Std Dev']
        ))

    print("=" * 80)

if __name__ == '__main__':
    if len(sys.argv) != 2:
        print("python analyze_fishing_log.py <file>", file=sys.stderr)
        sys.exit(1)

    log_file_path = sys.argv[1]
    load_and_analyze_log(log_file_path)
