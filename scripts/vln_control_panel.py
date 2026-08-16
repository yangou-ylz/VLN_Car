#!/usr/bin/env python3
"""VLN 本地控制面板：浏览器中文 UI + ROS2 目标点控制 + 传感器窗口触发。"""

import argparse
import json
import math
import os
import signal
import subprocess
import sys
import threading
import time
import webbrowser
from datetime import datetime
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlparse

import rclpy
from geometry_msgs.msg import Twist
from tf2_msgs.msg import TFMessage


VLN_ROOT = Path("/home/ubuntu22/VLN")
LOG_ROOT = VLN_ROOT / "UnityProjects" / "_SmokeTestLogs" / "control_panel"


def quaternion_yaw(q):
    siny_cosp = 2.0 * (q.w * q.z + q.x * q.y)
    cosy_cosp = 1.0 - 2.0 * (q.y * q.y + q.z * q.z)
    return math.atan2(siny_cosp, cosy_cosp)


def normalize_angle(angle):
    while angle > math.pi:
        angle -= 2.0 * math.pi
    while angle < -math.pi:
        angle += 2.0 * math.pi
    return angle


def clamp(value, low, high):
    return max(low, min(high, value))


def local_to_map(origin_xy, origin_yaw, local_xy):
    lx, ly = local_xy
    cos_yaw = math.cos(origin_yaw)
    sin_yaw = math.sin(origin_yaw)
    return (
        origin_xy[0] + lx * cos_yaw - ly * sin_yaw,
        origin_xy[1] + lx * sin_yaw + ly * cos_yaw,
    )


def make_html():
    return """<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>VLN 控制面板</title>
  <style>
    :root {
      color-scheme: light;
      --bg: #f4f6f8;
      --panel: #ffffff;
      --text: #17202a;
      --muted: #61707f;
      --line: #d9e1ea;
      --main: #1565c0;
      --main-dark: #0d47a1;
      --ok: #1b7f3a;
      --warn: #a15c00;
      --bad: #b3261e;
    }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      min-height: 100vh;
      background: var(--bg);
      color: var(--text);
      font-family: "Noto Sans CJK SC", "Microsoft YaHei", Arial, sans-serif;
      letter-spacing: 0;
    }
    .app {
      width: min(820px, calc(100vw - 32px));
      margin: 28px auto;
      background: var(--panel);
      border: 1px solid var(--line);
      border-radius: 8px;
      box-shadow: 0 10px 28px rgba(20, 35, 55, 0.12);
      overflow: hidden;
    }
    .topbar {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      border-bottom: 1px solid var(--line);
      background: #eef3f8;
    }
    .tab {
      height: 48px;
      border: 0;
      border-right: 1px solid var(--line);
      background: transparent;
      color: #263747;
      font-size: 16px;
      font-weight: 700;
      cursor: pointer;
    }
    .tab:last-child { border-right: 0; }
    .tab.active {
      background: var(--panel);
      color: var(--main);
      box-shadow: inset 0 3px 0 var(--main);
    }
    .content { padding: 24px; }
    .title-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      margin-bottom: 18px;
    }
    h1 {
      font-size: 22px;
      line-height: 1.25;
      margin: 0;
    }
    .badge {
      min-width: 96px;
      text-align: center;
      border-radius: 999px;
      padding: 6px 12px;
      background: #eef7f0;
      color: var(--ok);
      font-weight: 700;
      font-size: 13px;
      white-space: nowrap;
    }
    .form-grid {
      display: grid;
      grid-template-columns: 1fr 1fr 150px;
      gap: 14px;
      align-items: end;
    }
    label {
      display: block;
      font-size: 14px;
      font-weight: 700;
      margin-bottom: 8px;
      color: #2b3a49;
    }
    .number-box {
      display: grid;
      grid-template-columns: 42px minmax(0, 1fr) 42px;
      border: 1px solid var(--line);
      border-radius: 6px;
      overflow: hidden;
      background: #fff;
      height: 44px;
    }
    input {
      width: 100%;
      border: 0;
      border-left: 1px solid var(--line);
      border-right: 1px solid var(--line);
      text-align: center;
      font-size: 17px;
      color: var(--text);
      outline: none;
      padding: 0 8px;
    }
    .step-btn {
      border: 0;
      background: #f6f9fc;
      color: var(--main);
      font-size: 22px;
      font-weight: 800;
      cursor: pointer;
      line-height: 1;
    }
    .step-btn:hover { background: #e7f0fb; }
    .actions {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 12px;
      margin-top: 18px;
    }
    .primary, .secondary {
      height: 44px;
      border-radius: 6px;
      border: 1px solid transparent;
      font-size: 16px;
      font-weight: 800;
      cursor: pointer;
    }
    .primary {
      background: var(--main);
      color: #fff;
    }
    .primary:hover { background: var(--main-dark); }
    .secondary {
      background: #fff;
      color: var(--bad);
      border-color: #e3a7a2;
    }
    .secondary:hover { background: #fff4f2; }
    .status-card {
      margin-top: 20px;
      border: 1px solid var(--line);
      border-radius: 6px;
      background: #fbfcfe;
      padding: 14px;
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: 12px;
    }
    .metric { min-width: 0; }
    .metric-name {
      font-size: 12px;
      color: var(--muted);
      margin-bottom: 5px;
    }
    .metric-value {
      font-size: 15px;
      font-weight: 800;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .message {
      margin-top: 14px;
      min-height: 24px;
      font-size: 14px;
      font-weight: 700;
      color: var(--muted);
    }
    .message.ok { color: var(--ok); }
    .message.warn { color: var(--warn); }
    .message.bad { color: var(--bad); }
    @media (max-width: 700px) {
      .app { width: calc(100vw - 16px); margin: 8px auto; }
      .content { padding: 16px; }
      .form-grid { grid-template-columns: 1fr; }
      .status-card { grid-template-columns: 1fr 1fr; }
      .tab { font-size: 14px; }
    }
  </style>
</head>
<body>
  <main class="app">
    <nav class="topbar" aria-label="功能模块">
      <button class="tab active" id="tabTarget">目标位置</button>
      <button class="tab" id="tabCamera">相机视图</button>
      <button class="tab" id="tabLidar">雷达点云</button>
    </nav>
    <section class="content">
      <div class="title-row">
        <h1>目标位置控制</h1>
        <div class="badge" id="stateBadge">等待 TF</div>
      </div>
      <div class="form-grid">
        <div>
          <label for="targetX">相对 X：前进 / 后退（米）</label>
          <div class="number-box">
            <button class="step-btn" data-input="targetX" data-dir="-1">−</button>
            <input id="targetX" type="number" value="1.20" step="0.10" />
            <button class="step-btn" data-input="targetX" data-dir="1">+</button>
          </div>
        </div>
        <div>
          <label for="targetY">相对 Y：左 / 右（米）</label>
          <div class="number-box">
            <button class="step-btn" data-input="targetY" data-dir="-1">−</button>
            <input id="targetY" type="number" value="0.00" step="0.10" />
            <button class="step-btn" data-input="targetY" data-dir="1">+</button>
          </div>
        </div>
        <div>
          <label for="stepSize">步进值（米）</label>
          <input id="stepSize" type="number" value="0.10" step="0.05" style="height:44px;border:1px solid var(--line);border-radius:6px;" />
        </div>
      </div>
      <div class="actions">
        <button class="primary" id="sendTarget">发送目标</button>
        <button class="secondary" id="stopVehicle">停止小车</button>
      </div>
      <div class="status-card">
        <div class="metric"><div class="metric-name">当前位置</div><div class="metric-value" id="poseText">--</div></div>
        <div class="metric"><div class="metric-name">当前朝向</div><div class="metric-value" id="yawText">--</div></div>
        <div class="metric"><div class="metric-name">目标距离</div><div class="metric-value" id="distanceText">--</div></div>
        <div class="metric"><div class="metric-name">控制状态</div><div class="metric-value" id="controlText">未启动</div></div>
      </div>
      <div class="message" id="messageText">先启动 endpoint，并在 Unity 主场景点击 Play。</div>
    </section>
  </main>
  <script>
    const $ = (id) => document.getElementById(id);
    const setMessage = (text, type = '') => {
      const el = $('messageText');
      el.textContent = text;
      el.className = 'message ' + type;
    };
    const setActiveTab = (id) => {
      ['tabTarget', 'tabCamera', 'tabLidar'].forEach((tab) => $(tab).classList.toggle('active', tab === id));
      setTimeout(() => $('tabTarget').classList.add('active'), 450);
    };
    document.querySelectorAll('.step-btn').forEach((btn) => {
      btn.addEventListener('click', () => {
        const input = $(btn.dataset.input);
        const step = Number($('stepSize').value || '0.1');
        const next = Number(input.value || '0') + Number(btn.dataset.dir) * step;
        input.value = next.toFixed(2);
      });
    });
    async function postJson(path, body) {
      const response = await fetch(path, {method: 'POST', headers: {'Content-Type': 'application/json'}, body: JSON.stringify(body || {})});
      const data = await response.json();
      if (!response.ok || data.ok === false) throw new Error(data.message || '请求失败');
      return data;
    }
    $('sendTarget').addEventListener('click', async () => {
      try {
        const x = Number($('targetX').value || '0');
        const y = Number($('targetY').value || '0');
        const data = await postJson('/api/target', {x, y});
        setMessage(data.message, 'ok');
      } catch (err) { setMessage(err.message, 'bad'); }
    });
    $('stopVehicle').addEventListener('click', async () => {
      try { const data = await postJson('/api/stop', {}); setMessage(data.message, 'warn'); }
      catch (err) { setMessage(err.message, 'bad'); }
    });
    $('tabCamera').addEventListener('click', async () => {
      setActiveTab('tabCamera');
      try { const data = await postJson('/api/open_camera', {}); setMessage(data.message, 'ok'); }
      catch (err) { setMessage(err.message, 'bad'); }
    });
    $('tabLidar').addEventListener('click', async () => {
      setActiveTab('tabLidar');
      try { const data = await postJson('/api/open_lidar', {}); setMessage(data.message, 'ok'); }
      catch (err) { setMessage(err.message, 'bad'); }
    });
    async function refreshStatus() {
      try {
        const response = await fetch('/api/status');
        const data = await response.json();
        $('stateBadge').textContent = data.pose ? 'TF 正常' : '等待 TF';
        $('poseText').textContent = data.pose ? `${data.pose.x.toFixed(2)}, ${data.pose.y.toFixed(2)}` : '--';
        $('yawText').textContent = data.pose ? `${data.pose.yaw.toFixed(2)} rad` : '--';
        $('distanceText').textContent = data.distance === null ? '--' : `${data.distance.toFixed(2)} m`;
        $('controlText').textContent = data.active ? '正在前往目标' : (data.pose ? '待命' : '无 TF');
      } catch (err) {
        $('stateBadge').textContent = '后端断开';
        $('controlText').textContent = '后端断开';
      }
    }
    refreshStatus();
    setInterval(refreshStatus, 500);
  </script>
</body>
</html>
"""


class ControlPanel:
    def __init__(self, args):
        self.args = args
        self.lock = threading.Lock()
        self.pose_xy = None
        self.pose_yaw = None
        self.tf_count = 0
        self.active = False
        self.target_map_xy = None
        self.target_relative_xy = None
        self.distance = None
        self.last_message = "等待 TF"
        self.last_publish_time = 0.0
        self.shutdown_requested = False
        self.camera_process = None
        self.lidar_process = None
        self.run_id = datetime.now().strftime("vln_control_panel_%Y%m%d_%H%M%S")
        self.log_dir = LOG_ROOT / self.run_id
        self.log_dir.mkdir(parents=True, exist_ok=True)

        rclpy.init()
        self.node = rclpy.create_node("vln_control_panel")
        self.publisher = self.node.create_publisher(Twist, args.cmd_topic, 10)
        self.subscription = self.node.create_subscription(TFMessage, args.tf_topic, self.on_tf, 20)

    def on_tf(self, msg):
        with self.lock:
            self.tf_count += 1
            for transform in msg.transforms:
                if transform.header.frame_id == "map" and transform.child_frame_id == "base_link":
                    t = transform.transform.translation
                    self.pose_xy = (float(t.x), float(t.y))
                    self.pose_yaw = quaternion_yaw(transform.transform.rotation)

    def set_target(self, relative_x, relative_y):
        with self.lock:
            if self.pose_xy is None or self.pose_yaw is None:
                raise RuntimeError("还没有收到 map->base_link TF。请先启动 endpoint，并在 Unity 主场景点击 Play。")
            self.target_relative_xy = (float(relative_x), float(relative_y))
            self.target_map_xy = local_to_map(self.pose_xy, self.pose_yaw, self.target_relative_xy)
            self.distance = math.hypot(self.target_map_xy[0] - self.pose_xy[0], self.target_map_xy[1] - self.pose_xy[1])
            self.active = True
            self.last_message = f"已发送相对目标 X={relative_x:.2f}m, Y={relative_y:.2f}m"
            return self.last_message

    def stop(self, message="已发送停止指令"):
        with self.lock:
            self.active = False
            self.target_map_xy = None
            self.target_relative_xy = None
            self.distance = None
            self.last_message = message
        self.publish_zero(repeat=6)

    def publish_zero(self, repeat=1):
        if not rclpy.ok():
            return
        zero = Twist()
        for _ in range(max(1, repeat)):
            try:
                self.publisher.publish(zero)
            except Exception:
                break
            time.sleep(0.025)

    def update_control(self):
        now = time.monotonic()
        if now - self.last_publish_time < 1.0 / max(1.0, self.args.publish_rate):
            return
        self.last_publish_time = now

        with self.lock:
            active = self.active
            pose_xy = self.pose_xy
            pose_yaw = self.pose_yaw
            target = self.target_map_xy

        if not active or pose_xy is None or pose_yaw is None or target is None:
            return

        dx = target[0] - pose_xy[0]
        dy = target[1] - pose_xy[1]
        distance = math.hypot(dx, dy)
        target_heading = math.atan2(dy, dx)
        heading_error = normalize_angle(target_heading - pose_yaw)

        if distance <= self.args.goal_tolerance:
            self.stop(message=f"已到达目标，剩余距离 {distance:.2f}m")
            return

        command = Twist()
        command.linear.x = clamp(self.args.linear_gain * distance, 0.12, self.args.max_linear)
        if abs(heading_error) > 1.1:
            command.linear.x = 0.0

        command.angular.z = clamp(-self.args.angular_gain * heading_error, -self.args.max_angular, self.args.max_angular)
        self.publisher.publish(command)
        with self.lock:
            self.distance = distance
            self.last_message = f"正在前往目标，剩余 {distance:.2f}m"

    def status(self):
        with self.lock:
            pose = None
            if self.pose_xy is not None and self.pose_yaw is not None:
                pose = {"x": self.pose_xy[0], "y": self.pose_xy[1], "yaw": self.pose_yaw}
            return {
                "ok": True,
                "pose": pose,
                "tf_count": self.tf_count,
                "active": self.active,
                "target_relative": self.target_relative_xy,
                "target_map": self.target_map_xy,
                "distance": self.distance,
                "message": self.last_message,
                "run_id": self.run_id,
            }

    def launch_viewer(self, kind):
        if kind == "camera":
            script = VLN_ROOT / "scripts" / "view_front_image.sh"
            proc_attr = "camera_process"
            label = "相机视图"
        elif kind == "lidar":
            script = VLN_ROOT / "scripts" / "view_vln_vehicle_rviz.sh"
            proc_attr = "lidar_process"
            label = "雷达点云"
        else:
            raise RuntimeError("未知视图类型")

        process = getattr(self, proc_attr)
        if process is not None and process.poll() is None:
            return f"{label}窗口已经在运行"

        log_path = self.log_dir / f"{kind}_viewer.log"
        log_file = open(log_path, "a", encoding="utf-8")
        process = subprocess.Popen(
            [str(script)],
            stdout=log_file,
            stderr=subprocess.STDOUT,
            cwd=str(VLN_ROOT),
            start_new_session=True,
        )
        setattr(self, proc_attr, process)
        return f"已打开{label}窗口，日志：{log_path}"

    def cleanup(self):
        self.publish_zero(repeat=4)
        for process in (self.camera_process, self.lidar_process):
            if process is not None and process.poll() is None:
                try:
                    os.killpg(process.pid, signal.SIGTERM)
                except ProcessLookupError:
                    pass
        try:
            self.node.destroy_subscription(self.subscription)
            self.node.destroy_publisher(self.publisher)
            self.node.destroy_node()
        except Exception:
            pass
        if rclpy.ok():
            rclpy.shutdown()


def build_handler(panel):
    html = make_html().encode("utf-8")

    class Handler(BaseHTTPRequestHandler):
        def log_message(self, fmt, *args):
            return

        def send_json(self, payload, status=200):
            body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
            self.send_response(status)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(body)

        def do_GET(self):
            path = urlparse(self.path).path
            if path in ("/", "/index.html"):
                self.send_response(200)
                self.send_header("Content-Type", "text/html; charset=utf-8")
                self.send_header("Content-Length", str(len(html)))
                self.send_header("Cache-Control", "no-store")
                self.end_headers()
                self.wfile.write(html)
                return
            if path == "/api/status":
                self.send_json(panel.status())
                return
            self.send_json({"ok": False, "message": "路径不存在"}, 404)

        def read_json(self):
            length = int(self.headers.get("Content-Length", "0") or "0")
            if length <= 0:
                return {}
            return json.loads(self.rfile.read(length).decode("utf-8"))

        def do_POST(self):
            path = urlparse(self.path).path
            try:
                data = self.read_json()
                if path == "/api/target":
                    message = panel.set_target(float(data.get("x", 0.0)), float(data.get("y", 0.0)))
                    self.send_json({"ok": True, "message": message})
                    return
                if path == "/api/stop":
                    panel.stop()
                    self.send_json({"ok": True, "message": "已停止小车"})
                    return
                if path == "/api/open_camera":
                    self.send_json({"ok": True, "message": panel.launch_viewer("camera")})
                    return
                if path == "/api/open_lidar":
                    self.send_json({"ok": True, "message": panel.launch_viewer("lidar")})
                    return
                self.send_json({"ok": False, "message": "路径不存在"}, 404)
            except Exception as exc:
                self.send_json({"ok": False, "message": str(exc)}, 400)

    return Handler


def parse_args():
    parser = argparse.ArgumentParser(description="启动 VLN 本地控制面板")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--cmd-topic", default="/vln/cmd_vel")
    parser.add_argument("--tf-topic", default="/tf")
    parser.add_argument("--goal-tolerance", type=float, default=0.28)
    parser.add_argument("--max-linear", type=float, default=0.9)
    parser.add_argument("--max-angular", type=float, default=0.9)
    parser.add_argument("--linear-gain", type=float, default=0.65)
    parser.add_argument("--angular-gain", type=float, default=1.4)
    parser.add_argument("--publish-rate", type=float, default=10.0)
    parser.add_argument("--no-browser", action="store_true")
    return parser.parse_args()


def main():
    args = parse_args()
    panel = ControlPanel(args)
    server = ThreadingHTTPServer((args.host, args.port), build_handler(panel))
    server.timeout = 0.1
    url = f"http://{args.host}:{args.port}/"
    print(f"VLN 控制面板已启动：{url}")
    print(f"日志目录：{panel.log_dir}")
    print("前提：endpoint 已启动，Unity 主场景已点击 Play。按 Ctrl+C 退出。")

    if not args.no_browser:
        threading.Timer(0.5, lambda: webbrowser.open(url)).start()

    try:
        while rclpy.ok() and not panel.shutdown_requested:
            rclpy.spin_once(panel.node, timeout_sec=0.02)
            panel.update_control()
            server.handle_request()
    except (KeyboardInterrupt, rclpy.executors.ExternalShutdownException):
        pass
    finally:
        server.server_close()
        panel.cleanup()
        print("VLN 控制面板已退出")


if __name__ == "__main__":
    raise SystemExit(main())
