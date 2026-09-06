#!/usr/bin/env python3
"""Local Tk keyboard controller for /vln/cmd_vel.

This intentionally bypasses the browser control panel.  It uses Tk key
press/release events and publishes Twist directly through rclpy.
"""

from __future__ import annotations

import argparse
import csv
import os
import signal
import sys
import time
import tkinter as tk
from tkinter import ttk

import rclpy
from geometry_msgs.msg import Twist


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description='Local keyboard /vln/cmd_vel controller')
    parser.add_argument('--cmd-topic', default='/vln/cmd_vel')
    parser.add_argument('--publish-rate', type=float, default=50.0)
    parser.add_argument('--linear-speed', type=float, default=1.20)
    parser.add_argument('--angular-speed', type=float, default=0.55)
    parser.add_argument('--max-linear-speed', type=float, default=5.00)
    parser.add_argument('--max-angular-speed', type=float, default=1.20)
    parser.add_argument('--release-delay-ms', type=int, default=90, help='Delay used to filter X11 key autorepeat release events')
    parser.add_argument('--log-file', default='', help='CSV path for publish timing diagnostics')
    parser.add_argument('--no-log', action='store_true', help='Disable publish timing diagnostics')
    return parser.parse_args()


class LocalKeyboardCmdVelApp:
    KEY_CAPTURE_TAG = 'VlnKeyboardControlCapture'

    def __init__(self, root: tk.Tk, node, publisher, args: argparse.Namespace) -> None:
        self.root = root
        self.node = node
        self.publisher = publisher
        self.args = args
        self.pressed: set[str] = set()
        self.key_generation: dict[str, int] = {}
        self.publish_count = 0
        self.last_publish_monotonic: float | None = None
        self.closed = False
        self.log_fp = None
        self.log_writer: csv.writer | None = None
        self.gaps_ms: list[float] = []
        self.start_monotonic = time.monotonic()

        self.linear_var = tk.DoubleVar(value=float(args.linear_speed))
        self.angular_var = tk.DoubleVar(value=float(args.angular_speed))
        self.status_var = tk.StringVar(value='待命：点击本窗口后按键控制')
        self.command_var = tk.StringVar(value='cmd_vel: linear.x=0.00, angular.z=0.00')
        self.keys_var = tk.StringVar(value='按键：无')
        self.publish_var = tk.StringVar(value='publish: 0')

        self.build_ui()
        self.bind_keys()
        self.open_log_file()

    def open_log_file(self) -> None:
        if self.args.no_log or not self.args.log_file:
            return
        os.makedirs(os.path.dirname(os.path.abspath(self.args.log_file)), exist_ok=True)
        self.log_fp = open(self.args.log_file, 'w', newline='', encoding='utf-8')
        self.log_writer = csv.writer(self.log_fp)
        self.log_writer.writerow([
            'elapsed_s',
            'publish_count',
            'gap_ms',
            'linear_x',
            'angular_z',
            'active_keys',
            'topic',
        ])
        self.log_fp.flush()

    def build_ui(self) -> None:
        self.root.title('VLN 本地键盘速度控制')
        self.root.geometry('520x320')
        self.root.minsize(480, 300)

        frame = ttk.Frame(self.root, padding=14)
        frame.pack(fill='both', expand=True)

        title = ttk.Label(frame, text='VLN 本地键盘速度控制', font=('Sans', 15, 'bold'))
        title.pack(anchor='w')

        ttk.Label(
            frame,
            text='方向：↑ 前进，↓ 后退，←/A 左转，→/D 右转；松开即停，空格立即停车，Q 退出。方向键会优先控制车辆，不会调节速度框。',
            wraplength=470,
        ).pack(anchor='w', pady=(8, 10))

        speed_frame = ttk.LabelFrame(frame, text='速度设定')
        speed_frame.pack(fill='x')

        ttk.Label(speed_frame, text='线速度 m/s').grid(row=0, column=0, padx=8, pady=8, sticky='w')
        ttk.Scale(speed_frame, from_=0.0, to=float(self.args.max_linear_speed), variable=self.linear_var, orient='horizontal').grid(row=0, column=1, padx=8, pady=8, sticky='ew')
        ttk.Spinbox(speed_frame, from_=0.0, to=float(self.args.max_linear_speed), increment=0.10, textvariable=self.linear_var, width=7).grid(row=0, column=2, padx=8, pady=8)

        ttk.Label(speed_frame, text='角速度 rad/s').grid(row=1, column=0, padx=8, pady=8, sticky='w')
        ttk.Scale(speed_frame, from_=0.0, to=float(self.args.max_angular_speed), variable=self.angular_var, orient='horizontal').grid(row=1, column=1, padx=8, pady=8, sticky='ew')
        ttk.Spinbox(speed_frame, from_=0.0, to=float(self.args.max_angular_speed), increment=0.05, textvariable=self.angular_var, width=7).grid(row=1, column=2, padx=8, pady=8)
        speed_frame.columnconfigure(1, weight=1)

        ttk.Label(frame, textvariable=self.status_var, font=('Sans', 12, 'bold')).pack(anchor='w', pady=(12, 2))
        ttk.Label(frame, textvariable=self.command_var).pack(anchor='w', pady=2)
        ttk.Label(frame, textvariable=self.keys_var).pack(anchor='w', pady=2)
        ttk.Label(frame, textvariable=self.publish_var).pack(anchor='w', pady=2)

        button_frame = ttk.Frame(frame)
        button_frame.pack(fill='x', pady=(12, 0))
        ttk.Button(button_frame, text='立即停车', command=self.clear_keys_and_stop).pack(side='left')
        ttk.Button(button_frame, text='退出', command=self.close).pack(side='right')

        self.root.focus_force()

    def bind_keys(self) -> None:
        self.root.bind_class(self.KEY_CAPTURE_TAG, '<KeyPress>', self.on_key_press)
        self.root.bind_class(self.KEY_CAPTURE_TAG, '<KeyRelease>', self.on_key_release)
        self.install_key_capture_tag(self.root)
        self.root.protocol('WM_DELETE_WINDOW', self.close)

    def install_key_capture_tag(self, widget: tk.Widget) -> None:
        tags = list(widget.bindtags())
        if self.KEY_CAPTURE_TAG not in tags:
            tags.insert(0, self.KEY_CAPTURE_TAG)
            widget.bindtags(tuple(tags))
        for child in widget.winfo_children():
            self.install_key_capture_tag(child)

    @staticmethod
    def normalize_key(keysym: str) -> str | None:
        mapping = {
            'Up': 'up',
            'Down': 'down',
            'Left': 'left',
            'Right': 'right',
            'w': 'up',
            'W': 'up',
            's': 'down',
            'S': 'down',
            'a': 'a',
            'A': 'a',
            'd': 'd',
            'D': 'd',
        }
        return mapping.get(keysym)

    def on_key_press(self, event) -> str | None:
        if event.keysym in ('q', 'Q'):
            self.close()
            return 'break'
        if event.keysym in ('space', 'Escape'):
            self.clear_keys_and_stop()
            return 'break'

        key = self.normalize_key(event.keysym)
        if key is None:
            return None
        self.key_generation[key] = self.key_generation.get(key, 0) + 1
        self.pressed.add(key)
        return 'break'

    def on_key_release(self, event) -> str | None:
        key = self.normalize_key(event.keysym)
        if key is None:
            return None
        generation = self.key_generation.get(key, 0)
        self.root.after(max(20, int(self.args.release_delay_ms)), lambda: self.release_if_stale(key, generation))
        return 'break'

    def release_if_stale(self, key: str, generation: int) -> None:
        if self.key_generation.get(key, 0) == generation:
            self.pressed.discard(key)

    def compute_command(self) -> tuple[float, float]:
        forward = int('up' in self.pressed) - int('down' in self.pressed)
        left_active = 'left' in self.pressed or 'a' in self.pressed
        right_active = 'right' in self.pressed or 'd' in self.pressed
        turn = int(left_active) - int(right_active)
        linear = forward * max(0.0, min(float(self.linear_var.get()), float(self.args.max_linear_speed)))
        angular = turn * max(0.0, min(float(self.angular_var.get()), float(self.args.max_angular_speed)))
        return linear, angular

    def publish(self, linear: float, angular: float) -> None:
        msg = Twist()
        msg.linear.x = float(linear)
        msg.angular.z = float(angular)
        self.publisher.publish(msg)
        self.publish_count += 1
        now = time.monotonic()
        gap = 0.0 if self.last_publish_monotonic is None else now - self.last_publish_monotonic
        self.last_publish_monotonic = now
        if self.publish_count > 1:
            self.gaps_ms.append(gap * 1000.0)
        self.write_publish_log(now, gap * 1000.0, linear, angular)
        self.publish_var.set(f'publish: {self.publish_count}，last_gap={gap * 1000.0:.1f} ms，topic={self.args.cmd_topic}')

    def write_publish_log(self, now: float, gap_ms: float, linear: float, angular: float) -> None:
        if self.log_writer is None:
            return
        active_keys = '+'.join(sorted(self.pressed)) if self.pressed else ''
        self.log_writer.writerow([
            f'{now - self.start_monotonic:.6f}',
            self.publish_count,
            f'{gap_ms:.3f}',
            f'{linear:.6f}',
            f'{angular:.6f}',
            active_keys,
            self.args.cmd_topic,
        ])
        if self.publish_count % 20 == 0 and self.log_fp is not None:
            self.log_fp.flush()

    def clear_keys_and_stop(self) -> None:
        self.pressed.clear()
        self.key_generation.clear()
        for _ in range(10):
            self.publish(0.0, 0.0)
        self.status_var.set('已停车')

    def tick(self) -> None:
        if self.closed:
            return
        linear, angular = self.compute_command()
        self.publish(linear, angular)
        active_keys = ' + '.join(sorted(self.pressed)) if self.pressed else '无'
        active = abs(linear) > 1e-6 or abs(angular) > 1e-6
        self.status_var.set('控制中' if active else '待命')
        self.command_var.set(f'cmd_vel: linear.x={linear:.2f}, angular.z={angular:.2f}')
        self.keys_var.set(f'按键：{active_keys}')
        try:
            rclpy.spin_once(self.node, timeout_sec=0.0)
        except Exception:
            pass
        period_ms = max(5, int(1000.0 / max(1.0, float(self.args.publish_rate))))
        self.root.after(period_ms, self.tick)

    def close(self) -> None:
        if self.closed:
            return
        self.closed = True
        self.pressed.clear()
        for _ in range(20):
            self.publish(0.0, 0.0)
            time.sleep(0.005)
        self.write_summary()
        self.root.destroy()

    def write_summary(self) -> None:
        if self.log_fp is None:
            return
        gaps = sorted(self.gaps_ms)
        elapsed = max(1e-6, time.monotonic() - self.start_monotonic)

        def percentile(p: float) -> float:
            if not gaps:
                return 0.0
            index = min(len(gaps) - 1, max(0, int(round((len(gaps) - 1) * p))))
            return gaps[index]

        summary_path = self.args.log_file + '.summary.txt'
        with open(summary_path, 'w', encoding='utf-8') as fp:
            fp.write(f'publish_count={self.publish_count}\n')
            fp.write(f'elapsed_s={elapsed:.3f}\n')
            fp.write(f'average_publish_hz={self.publish_count / elapsed:.3f}\n')
            fp.write(f'gap_avg_ms={(sum(gaps) / len(gaps) if gaps else 0.0):.3f}\n')
            fp.write(f'gap_p95_ms={percentile(0.95):.3f}\n')
            fp.write(f'gap_p99_ms={percentile(0.99):.3f}\n')
            fp.write(f'gap_max_ms={(max(gaps) if gaps else 0.0):.3f}\n')
            fp.write(f'cmd_topic={self.args.cmd_topic}\n')
        self.log_fp.flush()
        self.log_fp.close()
        self.log_fp = None
        self.log_writer = None


def main() -> int:
    args = parse_args()
    rclpy.init(args=None)
    node = rclpy.create_node('vln_local_keyboard_cmd_vel_control')
    publisher = node.create_publisher(Twist, args.cmd_topic, 10)
    root = tk.Tk()
    app = LocalKeyboardCmdVelApp(root, node, publisher, args)

    def handle_signal(_signum, _frame):
        app.close()

    signal.signal(signal.SIGINT, handle_signal)
    signal.signal(signal.SIGTERM, handle_signal)
    root.after(max(5, int(1000.0 / max(1.0, float(args.publish_rate)))), app.tick)
    try:
        root.mainloop()
    finally:
        app.write_summary()
        try:
            node.destroy_node()
        finally:
            rclpy.shutdown()
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
