# deep q-network (dqn) reinforcement learning agent for stardew valley fishing minigame
# implements a simulated neural network with experience replay and target network
# connects to c# game server via socket, receives game state, makes decisions using
# epsilon-greedy policy, and learns to control the fishing bar using q-learning updates
# applies dynamic force scaling based on error distance and includes reward shaping
# to encourage the agent to keep the fish within the bobber bar

import socket
import json
import sys
import math
import random
import os
from collections import deque

# dqn hyperparameters
ALPHA = 0.001     # learning rate for network updates
GAMMA = 0.95      # discount factor for future rewards
EPSILON = 0.5     # initial epsilon-greedy exploration rate
EPSILON_DECAY = 0.99995 # decay rate per step to reduce exploration over time
BATCH_SIZE = 32   # number of samples to train on from memory
MEMORY_SIZE = 5000 # max number of experiences to store in replay buffer
SYNC_RATE = 2000  # how often to copy weights from policy to target network (in steps)

# control constants for force calculations
BASE_UP_FORCE = -0.2
MAX_FORCE_RANGE = 1.0
FORCE_SCALING_FACTOR_PER_10PX = 0.4

# connection settings for socket communication
HOST = '127.0.0.1' 
PORT = 8080        
BUFFER_SIZE = 4096

class DQNAgent:
    # simulated deep q-network agent with experience replay and target network
    # uses a simple 2-layer neural network (input -> hidden -> output)
    
    def __init__(self, state_size, action_size):
        self.state_size = state_size
        self.action_size = action_size
        self.memory = deque(maxlen=MEMORY_SIZE)
        self.gamma = GAMMA
        self.epsilon = EPSILON
        self.alpha = ALPHA
        self.sync_counter = 0

        # initialize policy and target networks with random weights
        self.policy_weights = self._build_network()
        self.target_weights = self._build_network()
        self.target_weights = self.copy_weights(self.policy_weights)

    def _build_network(self):
        # initializes weights and biases for a simple 2-layer network
        # architecture: input (3) -> hidden (8) -> output (2)
        weights = {}
        
        # layer 1: input (3) -> hidden (8)
        weights['w1'] = [[random.uniform(-0.1, 0.1) for _ in range(self.state_size)] for _ in range(8)]
        weights['b1'] = [0.0] * 8
        
        # layer 2: hidden (8) -> output (2)
        weights['w2'] = [[random.uniform(-0.1, 0.1) for _ in range(8)] for _ in range(self.action_size)]
        weights['b2'] = [0.0] * self.action_size
        
        return weights

    def copy_weights(self, source_weights):
        # deep copy weights for the target network to prevent updates
        return json.loads(json.dumps(source_weights))

    def relu(self, x):
        # relu activation function for hidden layer
        return max(0, x)

    def forward_pass(self, state_vector, weights):
        # simulates feed-forward calculation to get q-values from network
        
        # hidden layer 1 (input -> 8 nodes)
        h1 = []
        for j in range(len(weights['w1'])):
            sum_val = 0
            for i in range(self.state_size):
                sum_val += state_vector[i] * weights['w1'][j][i]
            h1.append(self.relu(sum_val + weights['b1'][j]))
            
        # output layer (8 nodes -> 2 q-values)
        q_values = []
        for j in range(self.action_size):
            sum_val = 0
            for i in range(len(h1)):
                sum_val += h1[i] * weights['w2'][j][i]
            q_values.append(sum_val + weights['b2'][j])
            
        return q_values, h1

    def remember(self, state, action, reward, next_state, done):
        # store experience in replay memory buffer
        self.memory.append((state, action, reward, next_state, done))

    def act(self, state_vector):
        # epsilon-greedy action selection
        # explore: random action with probability epsilon
        if random.random() < self.epsilon:
            return random.randrange(self.action_size)
        
        # exploit: choose action with highest q-value from policy network
        q_values, _ = self.forward_pass(state_vector, self.policy_weights)
        return q_values.index(max(q_values))

    def learn(self):
        # train the policy network using a batch from the replay buffer
        if len(self.memory) < BATCH_SIZE:
            return 0.0
        
        # sample random batch from memory
        batch = random.sample(self.memory, BATCH_SIZE)
        
        td_error = 0.0
        
        # perform q-learning update for each experience in batch
        for state, action, reward, next_state, done in batch:
            # get current q-values from policy network
            target_q_values, _ = self.forward_pass(state, self.policy_weights)
            current_q = target_q_values[action]

            # calculate target q-value using bellman equation
            if done:
                target = reward
            else:
                # get max q-value for next state from target network
                next_q_values, _ = self.forward_pass(next_state, self.target_weights)
                target = reward + self.gamma * max(next_q_values)
            
            # calculate td error and update q-value towards target
            td_error = target - current_q
            target_q_values[action] = current_q + self.alpha * td_error
            
        # periodically sync target network with policy network
        self.sync_counter += 1
        if self.sync_counter % SYNC_RATE == 0:
            self.target_weights = self.copy_weights(self.policy_weights)
            
        return td_error

def get_reward(error):
    # calculates reward based on continuous error between fish and bar positions
    # uses exponential reward for accuracy and linear penalty for distance
    
    # base reward: exponentially high reward for error near 0
    reward = 1.0 * math.exp(-0.005 * error**2) 

    # penalty: -0.5 for every 10px the bobber bar strays away
    abs_error = abs(error)
    
    if abs_error > 0.0:
        penalty_units = abs_error / 10.0
        reward -= penalty_units * 0.5
    
    return reward

def create_state_vector(state_raw, error):
    # generates the 3-element continuous state vector for the dqn
    # state consists of: [error, bar velocity, fish velocity]
    try:
        state_vector = [
            error,
            state_raw['BobberBarVelocity'],
            state_raw['FishVelocity']
        ]
        return state_vector
    except KeyError:
        # handle case where velocity fields might be missing in initial ticks
        return [error, 0.0, 0.0]

def run_rl_agent(host, port):
    # main agent loop that connects to game server and runs dqn agent
    global EPSILON
    s = None
    
    # initialize dqn agent with 3 state features and 2 actions
    agent = DQNAgent(state_size=3, action_size=2)
    LAST_STATE_VECTOR = None
    LAST_ACTION = 0
    last_td_error = 0.0
    episode_tick_counter = 0
    episode_counter = 0

    # print csv header for logging
    print(f"[HEADER]TICK,EPISODE,BAR_POS,FISH_POS,REWARD,FORCE,Q_HOLD,EPSILON,TD_ERROR", file=sys.stderr, flush=True)
    
    try:
        # establish socket connection to c# game server
        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        s.connect((host, port))
        print(f"Connected to C# server at {host}:{port} (DQN Agent)", file=sys.stderr, flush=True)
        
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
                    q_value_hold = 0.0
                    reward = 0.0
                    
                    is_nibbling = state_raw.get('IsNibbling', False)

                    # phase 2: hooking - detect nibble and perform hook action
                    if is_nibbling and not state_raw.get('MinigameActive', False):
                        action_out = 1
                        print("\nController: HOOK!", file=sys.stderr, flush=True)
                        LAST_STATE_VECTOR = None
                        episode_tick_counter = 0
                    
                    # phase 3: minigame control - use dqn strategy with dynamic force
                    elif state_raw.get('MinigameActive', False):
                        
                        episode_tick_counter += 1
                        
                        # calculate continuous error between fish position and bar center
                        bar_center = state_raw['BobberBarPosition'] + (state_raw['BobberBarHeight'] / 2.0)
                        error = state_raw['FishPosition'] - bar_center 
                        abs_error = abs(error)
                        
                        # create state vector and calculate reward
                        current_state_vector = create_state_vector(state_raw, error)
                        reward = get_reward(error)
                        
                        # learning phase: store experience and train network
                        if LAST_STATE_VECTOR is not None:
                            agent.remember(LAST_STATE_VECTOR, LAST_ACTION, reward, current_state_vector, False)
                            last_td_error = agent.learn()
                        
                        # decision phase: choose next action using epsilon-greedy policy
                        action_out = agent.act(current_state_vector)

                        # reward correction for directional failure
                        # penalize pushing up when bar is far above fish
                        if error < -15.0 and action_out == 1:
                            reward -= 2.5
                        # reward releasing when bar is far above fish
                        elif error < -15.0 and action_out == 0:
                            reward += 0.5

                        # dynamic force scaling based on error distance
                        boost_factor = 1.0 + (abs_error / 10.0) * FORCE_SCALING_FACTOR_PER_10PX
                        dynamic_base_force = BASE_UP_FORCE * boost_factor
                        dynamic_max_range = MAX_FORCE_RANGE * boost_factor
                        
                        # map discrete action to continuous force output
                        if action_out == 1:
                            # action 1 (hold/boost): apply dynamic upward force
                            q_values, _ = agent.forward_pass(current_state_vector, agent.policy_weights)
                            q_value_hold = q_values[1]
                            
                            # normalize q-value to range [0, 1]
                            normalized_q = max(0.0, min(1.0, q_value_hold / 1.0)) 
                            
                            # map normalized q to force boost within dynamic range
                            force_magnitude = normalized_q * dynamic_max_range
                            force_boost = dynamic_base_force - force_magnitude
                            
                        else:
                            # action 0 (release) maps to zero force
                            force_boost = 0.0

                        # store current state and action for next learning step
                        LAST_STATE_VECTOR = current_state_vector
                        LAST_ACTION = action_out
                        agent.epsilon *= EPSILON_DECAY
                        
                        # log training data for analysis
                        fish_pos = state_raw['FishPosition']
                        print(f"[DATA]{episode_tick_counter},{episode_counter},{bar_center:.4f},{fish_pos:.4f},{reward:.4f},{force_boost:.4f},{q_value_hold:.4f},{agent.epsilon:.8f},{last_td_error:.4f}", file=sys.stderr, flush=True)
                        
                    else:
                        # minigame ended: finalize episode if it was active
                        if LAST_STATE_VECTOR is not None:
                            episode_counter += 1
                            agent.remember(LAST_STATE_VECTOR, LAST_ACTION, 0.0, current_state_vector, True)
                            LAST_STATE_VECTOR = None
                        
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
    
    # print final status upon disconnection
    print(f"\n--- DQN Agent Disconnected ---", file=sys.stderr, flush=True)
    print(f"Memory size: {len(agent.memory)}", file=sys.stderr, flush=True)
    print(f"Epsilon finished at: {agent.epsilon:.4f}", file=sys.stderr, flush=True)

# run the dqn agent
if __name__ == '__main__':
    run_rl_agent(HOST, PORT)