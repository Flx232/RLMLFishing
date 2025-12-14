import socket
import json
import sys
import math
import random
import os
from collections import deque

ALPHA = 0.001
GAMMA = 0.95
EPSILON = 0.5
EPSILON_DECAY = 0.99995
BATCH_SIZE = 32
MEMORY_SIZE = 5000
SYNC_RATE = 2000

BASE_UP_FORCE = -0.25
MAX_FORCE_RANGE = 0.15
FORCE_SCALING_FACTOR_PER_10PX = 0.15

HOST = '127.0.0.1'
PORT = 8080
BUFFER_SIZE = 4096

class DQNAgent:
    def __init__(self, state_size, action_size):
        self.state_size = state_size
        self.action_size = action_size
        self.memory = deque(maxlen=MEMORY_SIZE)
        self.gamma = GAMMA
        self.epsilon = EPSILON
        self.alpha = ALPHA
        self.sync_counter = 0
        self.policy_weights = self._build_network()
        self.target_weights = self._build_network()
        self.target_weights = self.copy_weights(self.policy_weights)

    def _build_network(self):
        weights = {}
        weights['w1'] = [[random.uniform(-0.1, 0.1) for _ in range(self.state_size)] for _ in range(8)]
        weights['b1'] = [0.0] * 8
        weights['w2'] = [[random.uniform(-0.1, 0.1) for _ in range(8)] for _ in range(self.action_size)]
        weights['b2'] = [0.0] * self.action_size
        return weights

    def copy_weights(self, source_weights):
        return json.loads(json.dumps(source_weights))

    def relu(self, x):
        return max(0, x)

    def forward_pass(self, state_vector, weights):
        h1 = []
        for j in range(len(weights['w1'])):
            sum_val = 0
            for i in range(self.state_size):
                sum_val += state_vector[i] * weights['w1'][j][i]
            h1.append(self.relu(sum_val + weights['b1'][j]))

        q_values = []
        for j in range(self.action_size):
            sum_val = 0
            for i in range(len(h1)):
                sum_val += h1[i] * weights['w2'][j][i]
            q_values.append(sum_val + weights['b2'][j])

        return q_values, h1

    def remember(self, state, action, reward, next_state, done):
        self.memory.append((state, action, reward, next_state, done))

    def act(self, state_vector):
        if random.random() < self.epsilon:
            return random.randrange(self.action_size)
        q_values, _ = self.forward_pass(state_vector, self.policy_weights)
        return q_values.index(max(q_values))

    def learn(self):
        if len(self.memory) < BATCH_SIZE:
            return 0.0

        batch = random.sample(self.memory, BATCH_SIZE)
        td_error = 0.0

        for state, action, reward, next_state, done in batch:
            target_q_values, _ = self.forward_pass(state, self.policy_weights)
            current_q = target_q_values[action]

            if done:
                target = reward
            else:
                next_q_values, _ = self.forward_pass(next_state, self.target_weights)
                target = reward + self.gamma * max(next_q_values)

            td_error = target - current_q
            target_q_values[action] = current_q + self.alpha * td_error

        self.sync_counter += 1
        if self.sync_counter % SYNC_RATE == 0:
            self.target_weights = self.copy_weights(self.policy_weights)

        return td_error

def get_reward(error):
    abs_error = abs(error)
    reward = math.exp(-0.01 * abs_error)
    if abs_error > 30.0:
        reward -= 0.3
    return reward

def create_state_vector(state_raw, error):
    try:
        return [
            error,
            state_raw['BobberBarVelocity'],
            state_raw['FishVelocity']
        ]
    except KeyError:
        return [error, 0.0, 0.0]

def run_rl_agent(host, port):
    s = None
    agent = DQNAgent(state_size=3, action_size=2)
    LAST_STATE_VECTOR = None
    LAST_ACTION = 0
    last_td_error = 0.0
    episode_tick_counter = 0
    episode_counter = 0

    print(f"[HEADER]TICK,EPISODE,BAR_POS,FISH_POS,REWARD,FORCE,Q_HOLD,EPSILON,TD_ERROR", file=sys.stderr, flush=True)

    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        s.connect((host, port))
        print(f"Connected to C# server at {host}:{port} (DQN Agent)", file=sys.stderr, flush=True)

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
                    q_value_hold = 0.0
                    reward = 0.0

                    is_nibbling = state_raw.get('IsNibbling', False)

                    if is_nibbling and not state_raw.get('MinigameActive', False):
                        action_out = 1
                        print("\nController: HOOK!", file=sys.stderr, flush=True)
                        LAST_STATE_VECTOR = None
                        episode_tick_counter = 0

                    elif state_raw.get('MinigameActive', False):
                        episode_tick_counter += 1

                        bar_center = state_raw['BobberBarPosition'] + (state_raw['BobberBarHeight'] / 2.0)
                        error = state_raw['FishPosition'] - bar_center
                        abs_error = abs(error)

                        current_state_vector = create_state_vector(state_raw, error)
                        reward = get_reward(error)

                        if LAST_STATE_VECTOR is not None:
                            agent.remember(LAST_STATE_VECTOR, LAST_ACTION, reward, current_state_vector, False)
                            last_td_error = agent.learn()

                        action_out = agent.act(current_state_vector)

                        if error < -20.0 and action_out == 1:
                            reward -= 0.5
                        elif error < -20.0 and action_out == 0:
                            reward += 0.2

                        if action_out == 1:
                            q_values, _ = agent.forward_pass(current_state_vector, agent.policy_weights)
                            q_value_hold = q_values[1]
                            boost_factor = 1.0 + (abs_error / 10.0) * FORCE_SCALING_FACTOR_PER_10PX
                            dynamic_base_force = BASE_UP_FORCE * boost_factor
                            q_clamped = max(0.0, min(10.0, q_value_hold))
                            reduction_factor = q_clamped / 10.0
                            force_boost = dynamic_base_force + (reduction_factor * MAX_FORCE_RANGE)
                        else:
                            force_boost = 0.0

                        LAST_STATE_VECTOR = current_state_vector
                        LAST_ACTION = action_out
                        agent.epsilon *= EPSILON_DECAY

                        fish_pos = state_raw['FishPosition']
                        print(
                            f"[DATA]{episode_tick_counter},{episode_counter},"
                            f"{bar_center:.4f},{fish_pos:.4f},{reward:.4f},"
                            f"{force_boost:.4f},{q_value_hold:.4f},"
                            f"{agent.epsilon:.8f},{last_td_error:.4f}",
                            file=sys.stderr,
                            flush=True
                        )

                    else:
                        if LAST_STATE_VECTOR is not None:
                            episode_counter += 1
                            agent.remember(LAST_STATE_VECTOR, LAST_ACTION, 0.0, [0.0, 0.0, 0.0], True)
                            LAST_STATE_VECTOR = None

                    action_payload = json.dumps({"action": action_out, "interval": force_boost}) + "\n"
                    s.sendall(action_payload.encode('utf-8'))

                except json.JSONDecodeError as e:
                    print(f"\nJSON Decode Error on message: '{message.strip()}'\nError: {e}", file=sys.stderr, flush=True)
                except Exception as e:
                    print(f"\nController runtime error: {e}", file=sys.stderr, flush=True)

    except Exception as e:
        print(f"Connection or main loop error: {e}", file=sys.stderr, flush=True)
    finally:
        if s:
            s.close()

    print(f"\n--- DQN Agent Disconnected ---", file=sys.stderr, flush=True)
    print(f"Memory size: {len(agent.memory)}", file=sys.stderr, flush=True)
    print(f"Epsilon finished at: {agent.epsilon:.4f}", file=sys.stderr, flush=True)

if __name__ == '__main__':
    run_rl_agent(HOST, PORT)
