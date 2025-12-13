# q-learning reinforcement learning agent for stardew valley fishing minigame
# implements tabular q-learning with discrete state space (error binning)
# connects to c# game server via socket, receives game state, and learns optimal
# fishing bar control policy through epsilon-greedy exploration and q-value updates
# applies dynamic force scaling based on error distance to improve control precision

import socket
import json
import sys
import math
import random
import os
from collections import defaultdict

# q-learning hyperparameters
ALPHA = 0.001     # learning rate (how much new information overrides old)
GAMMA = 0.95      # discount factor (how much to value future rewards)
EPSILON = 0.5     # initial epsilon-greedy exploration rate
EPSILON_DECAY = 0.99995 # decay rate per step to reduce exploration over time

# control constants for q-learned force range
BASE_UP_FORCE = -0.2  # minimum upward force applied when action 1 is chosen
MAX_FORCE_RANGE = 1.0 # initial range magnitude for force calculation
FORCE_SCALING_FACTOR_PER_10PX = 0.2

# state discretization bins based on pixel error (fish pos - bar center)
# creates 5 final bins: (-inf, -50], (-50, -20], (-20, 20], (20, 50], (50, inf)
ERROR_BINS = [
    -50.0,  # far above
    -20.0,  # above
    20.0,   # near center
    50.0    # below
]

# connection settings for socket communication
HOST = '127.0.0.1' 
PORT = 8080        
BUFFER_SIZE = 4096

# q-table and agent state tracking
Q_TABLE = defaultdict(lambda: {0: 0.0, 1: 0.0}) # automatic state initialization
LAST_STATE = None
LAST_ACTION = 0

def get_state_key(error):
    # discretizes the continuous error into a string key for the q-table
    
    # discretize error into bins
    error_state = 0
    if error < ERROR_BINS[0]:
        error_state = 0 # far above
    elif error < ERROR_BINS[1]:
        error_state = 1 # above
    elif error < ERROR_BINS[2]:
        error_state = 2 # near center
    elif error < ERROR_BINS[3]:
        error_state = 3 # below
    else:
        error_state = 4 # far below
        
    # return state key as string
    return f"{error_state}"

def get_reward(error):
    # calculates reward based on continuous error with exponential base and linear penalty
    
    # base reward: exponentially high reward for error near 0
    reward = 1.0 * math.exp(-0.005 * error**2) 

    # custom penalty: -0.5 for every 10px the bobber bar strays away
    abs_error = abs(error)
    
    if abs_error > 0.0:
        penalty_units = abs_error / 10.0
        reward -= penalty_units * 0.5
    
    return reward

def choose_action(state_key):
    # epsilon-greedy strategy to select the next action
    global EPSILON
    
    # epsilon-greedy: choose random action with probability epsilon
    if random.random() < EPSILON:
        action = random.choice([0, 1])
    # exploit: choose action with the highest q-value
    else:
        q_values = Q_TABLE[state_key]
        action = max(q_values, key=q_values.get)

    # decay epsilon for more exploitation over time
    EPSILON *= EPSILON_DECAY
    
    return action

def update_q_table(old_state, action, reward, new_state, is_terminal=False):
    # core q-learning update equation
    # returns the temporal difference (td) error for logging
    
    old_q = Q_TABLE[old_state][action]
    
    # calculate target q-value
    if is_terminal:
        target = reward # target is just the final reward
    else:
        # get max q-value for next state
        max_q_new = max(Q_TABLE[new_state].values())
        target = reward + GAMMA * max_q_new
    
    # calculate td error
    td_error = target - old_q
    
    # q-learning update formula
    new_q = old_q + ALPHA * td_error
    Q_TABLE[old_state][action] = new_q
    
    return td_error

def run_rl_agent(host, port):
    # main agent loop that connects to game server and runs q-learning agent
    global LAST_STATE, LAST_ACTION, EPSILON
    s = None
    
    # counters and trackers for logging
    episode_tick_counter = 0
    episode_counter = 0
    last_td_error = 0.0
    
    # print csv header for logging
    print(f"[HEADER]TICK,EPISODE,BAR_POS,FISH_POS,REWARD,FORCE,Q_HOLD,EPSILON,TD_ERROR", file=sys.stderr, flush=True)

    try:
        # establish socket connection to c# game server
        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        s.connect((host, port))
        print(f"Connected to C# server at {host}:{port} (Q-Learning Agent)", file=sys.stderr, flush=True)
        
        buffer = ""
        
        while True:
            # receive state data from server
            new_data = s.recv(BUFFER_SIZE).decode('utf-8')
            if not new_data:
                break
            
            buffer += new_data
            
            # process complete messages (terminated by newline)
            if '\n' in buffer:
                message, buffer = buffer.split('\n', 1)
                
                try:
                    state_raw = json.loads(message.strip())
                    action_out = 0 
                    force_boost = 0.0
                    
                    is_nibbling = state_raw.get('IsNibbling', False)

                    # phase 2: hooking - detect nibble and perform hook action
                    if is_nibbling and not state_raw.get('MinigameActive', False):
                        action_out = 1
                        print("\nController: HOOK!", file=sys.stderr, flush=True)
                        LAST_STATE = None
                    
                    # phase 3: minigame control - use q-learning strategy with dynamic force
                    elif state_raw.get('MinigameActive', False):
                        
                        episode_tick_counter += 1
                        
                        # extract state variables
                        fish_pos = state_raw['FishPosition']    
                        bar_pos = state_raw['BobberBarPosition']
                        bar_height = state_raw['BobberBarHeight']
                        
                        # calculate continuous error between fish position and bar center
                        bar_center = bar_pos + (bar_height / 2.0)
                        error = fish_pos - bar_center 
                        abs_error = abs(error)
                        
                        # dynamic force scaling based on error distance
                        boost_factor = 1.0 + (abs_error / 10.0) * FORCE_SCALING_FACTOR_PER_10PX
                        dynamic_base_force = BASE_UP_FORCE * boost_factor
                        dynamic_max_range = MAX_FORCE_RANGE * boost_factor
                        
                        # get current discrete state key from continuous error
                        current_state = get_state_key(error)
                        
                        # calculate reward for current state
                        reward = get_reward(error)
                        
                        # q-table update (learning phase)
                        if LAST_STATE is not None:
                            # update q-table and get td error for logging
                            last_td_error = update_q_table(LAST_STATE, LAST_ACTION, reward, current_state)
                        
                        # choose next action using epsilon-greedy policy (decision phase)
                        action_out = choose_action(current_state)

                        # map discrete action to continuous force output
                        q_value_hold = Q_TABLE[current_state].get(1, 0.0) # get q for action 1 for logging
                        
                        if action_out == 1:
                            # action 1 (hold/boost): apply dynamic upward force
                            
                            # normalize q-value to range [0, 1]
                            normalized_q = max(0.0, min(1.0, q_value_hold / 1.0)) 
                            
                            # map normalized q to force boost within dynamic range
                            force_magnitude = normalized_q * dynamic_max_range
                            force_boost = dynamic_base_force - force_magnitude
                            
                        else:
                            # action 0 (release) maps to zero force
                            force_boost = 0.0

                        # store current state and action for next learning step
                        LAST_STATE = current_state
                        LAST_ACTION = action_out
                        
                        # log training data for analysis
                        print(f"[DATA]{episode_tick_counter},{episode_counter},{bar_center:.4f},{fish_pos:.4f},{reward:.4f},{force_boost:.4f},{q_value_hold:.4f},{EPSILON:.8f},{last_td_error:.4f}", file=sys.stderr, flush=True)
                        
                    else:
                        # minigame ended: finalize episode if it was active
                        if LAST_STATE is not None:
                             episode_counter += 1
                             # final update for terminal state with zero reward
                             update_q_table(LAST_STATE, LAST_ACTION, 0.0, None, is_terminal=True) 
                             LAST_STATE = None
                        
                        # reset tick counter for next minigame
                        episode_tick_counter = 0
                        
                    # send action to server as json
                    action_payload = json.dumps({"action": action_out, "interval": force_boost}) + "\n"
                    s.sendall(action_payload.encode('utf-8'))

                except json.JSONDecodeError as e:
                    print(f"\nJSON Decode Error on message: '{message.strip()}'\nError: {e}", file=sys.stderr, flush=True)
                    continue
                except Exception as e:
                    print(f"\nController runtime error: {e}", file=sys.stderr, flush=True)
                    
    except Exception as e:
        print(f"Connection or main loop error: {e}", file=sys.stderr, flush=True)
    finally:
        if s:
            s.close()
    
    # print final q-table upon disconnection for debugging
    print("\n--- Final Q-Table (Partial View) ---", file=sys.stderr, flush=True)
    for state, q_values in list(dict(Q_TABLE).items()):
        print(f"State {state}: Q(0): {q_values.get(0, 0.0):.2f}, Q(1): {q_values.get(1, 0.0):.2f}", file=sys.stderr, flush=True)
    print("------------------------------------", file=sys.stderr, flush=True)

# run the q-learning agent
if __name__ == '__main__':
    run_rl_agent(HOST, PORT)