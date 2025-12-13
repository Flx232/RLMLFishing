import socket
import json
import sys
import math
import random
import os
from collections import defaultdict

ALPHA = 0.001
GAMMA = 0.95
EPSILON = 0.5
EPSILON_DECAY = 0.99995

BASE_UP_FORCE = -0.25
MAX_FORCE_RANGE = 0.15
FORCE_SCALING_FACTOR_PER_10PX = 0.15

ERROR_BINS = [
    -50.0,
    -20.0,
    20.0,
    50.0
]

HOST = '127.0.0.1'
PORT = 8080
BUFFER_SIZE = 4096

Q_TABLE = defaultdict(lambda: {0: 0.0, 1: 0.0})
LAST_STATE = None
LAST_ACTION = 0

def get_state_key(error):
    error_state = 0
    if error < ERROR_BINS[0]:
        error_state = 0
    elif error < ERROR_BINS[1]:
        error_state = 1
    elif error < ERROR_BINS[2]:
        error_state = 2
    elif error < ERROR_BINS[3]:
        error_state = 3
    else:
        error_state = 4
    return f"{error_state}"

def get_reward(error):
    abs_error = abs(error)
    reward = math.exp(-0.01 * abs_error)
    if abs_error > 30.0:
        reward -= 0.3
    return reward

def choose_action(state_key):
    global EPSILON
    if random.random() < EPSILON:
        action = random.choice([0, 1])
    else:
        q_values = Q_TABLE[state_key]
        action = max(q_values, key=q_values.get)
    EPSILON *= EPSILON_DECAY
    return action

def update_q_table(old_state, action, reward, new_state, is_terminal=False):
    old_q = Q_TABLE[old_state][action]
    if is_terminal:
        target = reward
    else:
        max_q_new = max(Q_TABLE[new_state].values())
        target = reward + GAMMA * max_q_new
    td_error = target - old_q
    new_q = old_q + ALPHA * td_error
    Q_TABLE[old_state][action] = new_q
    return td_error

def run_rl_agent(host, port):
    global LAST_STATE, LAST_ACTION, EPSILON
    s = None
    episode_tick_counter = 0
    episode_counter = 0
    last_td_error = 0.0

    print(f"[HEADER]TICK,EPISODE,BAR_POS,FISH_POS,REWARD,FORCE,Q_HOLD,EPSILON,TD_ERROR", file=sys.stderr, flush=True)

    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        s.connect((host, port))
        print(f"Connected to C# server at {host}:{port} (Q-Learning Agent)", file=sys.stderr, flush=True)

        buffer = ""

        while True:
            new_data = s.recv(BUFFER_SIZE).decode('utf-8')
            if not new_data:
                break

            buffer += new_data

            if '\n' in buffer:
                message, buffer = buffer.split('\n', 1)

                try:
                    state_raw = json.loads(message.strip())
                    action_out = 0
                    force_boost = 0.0

                    is_nibbling = state_raw.get('IsNibbling', False)

                    if is_nibbling and not state_raw.get('MinigameActive', False):
                        action_out = 1
                        print("\nController: HOOK!", file=sys.stderr, flush=True)
                        LAST_STATE = None

                    elif state_raw.get('MinigameActive', False):
                        episode_tick_counter += 1

                        fish_pos = state_raw['FishPosition']
                        bar_pos = state_raw['BobberBarPosition']
                        bar_height = state_raw['BobberBarHeight']

                        bar_center = bar_pos + (bar_height / 2.0)
                        error = fish_pos - bar_center
                        abs_error = abs(error)

                        current_state = get_state_key(error)
                        reward = get_reward(error)

                        if LAST_STATE is not None:
                            last_td_error = update_q_table(
                                LAST_STATE,
                                LAST_ACTION,
                                reward,
                                current_state
                            )

                        action_out = choose_action(current_state)
                        q_value_hold = Q_TABLE[current_state].get(1, 0.0)

                        if action_out == 1:
                            boost_factor = 1.0 + (abs_error / 10.0) * FORCE_SCALING_FACTOR_PER_10PX
                            dynamic_base_force = BASE_UP_FORCE * boost_factor
                            q_clamped = max(0.0, min(10.0, q_value_hold))
                            reduction_factor = q_clamped / 10.0
                            force_boost = dynamic_base_force + (reduction_factor * MAX_FORCE_RANGE)
                        else:
                            force_boost = 0.0

                        LAST_STATE = current_state
                        LAST_ACTION = action_out

                        print(
                            f"[DATA]{episode_tick_counter},{episode_counter},"
                            f"{bar_center:.4f},{fish_pos:.4f},{reward:.4f},"
                            f"{force_boost:.4f},{q_value_hold:.4f},"
                            f"{EPSILON:.8f},{last_td_error:.4f}",
                            file=sys.stderr,
                            flush=True
                        )

                    else:
                        if LAST_STATE is not None:
                            episode_counter += 1
                            update_q_table(
                                LAST_STATE,
                                LAST_ACTION,
                                0.0,
                                None,
                                is_terminal=True
                            )
                            LAST_STATE = None

                        episode_tick_counter = 0

                    action_payload = json.dumps(
                        {"action": action_out, "interval": force_boost}
                    ) + "\n"
                    s.sendall(action_payload.encode('utf-8'))

                except json.JSONDecodeError as e:
                    print(
                        f"\nJSON Decode Error on message: '{message.strip()}'\nError: {e}",
                        file=sys.stderr,
                        flush=True
                    )
                    continue
                except Exception as e:
                    print(
                        f"\nController runtime error: {e}",
                        file=sys.stderr,
                        flush=True
                    )

    except Exception as e:
        print(
            f"Connection or main loop error: {e}",
            file=sys.stderr,
            flush=True
        )
    finally:
        if s:
            s.close()

    print("\n--- Final Q-Table (Partial View) ---", file=sys.stderr, flush=True)
    for state, q_values in list(dict(Q_TABLE).items())[:10]:
        print(
            f"State {state}: Q(0): {q_values.get(0, 0.0):.2f}, "
            f"Q(1): {q_values.get(1, 0.0):.2f}",
            file=sys.stderr,
            flush=True
        )
    print("------------------------------------", file=sys.stderr, flush=True)

if __name__ == '__main__':
    run_rl_agent(HOST, PORT)
