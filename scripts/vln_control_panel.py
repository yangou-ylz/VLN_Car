#!/usr/bin/env python3
"""VLN 本地控制面板：浏览器中文 UI + ROS2 控制、传感器窗口触发和手工速度记录。"""

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
RECORDING_ROOT = VLN_ROOT / "VLN_RECORDINGS" / "manual_drives"
RECORDING_SCHEMA = "vln_manual_cmd_vel_recording_v1"
MANUAL_KEY_NAMES = ("up", "down", "left", "right", "a", "d")


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


def utc_now_iso():
    return datetime.utcnow().replace(microsecond=0).isoformat() + "Z"


def normalize_keys(raw_keys):
    raw_keys = raw_keys or {}
    return {name: bool(raw_keys.get(name, False)) for name in MANUAL_KEY_NAMES}


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
      grid-template-columns: repeat(4, minmax(0, 1fr));
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
    .panel.hidden { display: none; }
    h2 {
      font-size: 17px;
      margin: 22px 0 12px;
    }
    .velocity-grid {
      display: grid;
      grid-template-columns: 1fr 1fr 1fr;
      gap: 14px;
      align-items: end;
    }
    .key-help {
      margin-top: 16px;
      border: 1px solid var(--line);
      border-radius: 6px;
      background: #fbfcfe;
      padding: 14px;
    }
    .key-row {
      display: flex;
      gap: 8px;
      align-items: center;
      flex-wrap: wrap;
      margin: 8px 0;
    }
    .key {
      min-width: 42px;
      height: 34px;
      padding: 0 10px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      border: 1px solid #b8c7d6;
      border-bottom-width: 3px;
      border-radius: 6px;
      background: #fff;
      color: #22313f;
      font-weight: 900;
      font-size: 14px;
    }
    .key.active { background: #e3f0ff; color: var(--main); border-color: var(--main); }
    .record-actions {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: 10px;
      margin-top: 16px;
    }
    .file-line {
      margin-top: 12px;
      border: 1px dashed #b8c7d6;
      border-radius: 6px;
      padding: 10px 12px;
      color: var(--muted);
      font-size: 13px;
      word-break: break-all;
      background: #fff;
    }
    @media (max-width: 700px) {
      .app { width: calc(100vw - 16px); margin: 8px auto; }
      .content { padding: 16px; }
      .form-grid, .velocity-grid, .record-actions { grid-template-columns: 1fr; }
      .status-card { grid-template-columns: 1fr 1fr; }
      .tab { font-size: 14px; }
    }
  </style>
</head>
<body>
  <main class="app">
    <nav class="topbar" aria-label="功能模块">
      <button class="tab active" id="tabTarget">目标位置</button>
      <button class="tab" id="tabVelocity">速度控制</button>
      <button class="tab" id="tabCamera">相机视图</button>
      <button class="tab" id="tabLidar">雷达点云</button>
    </nav>
    <section class="content">
      <div id="targetPanel" class="panel">
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
      </div>

      <div id="velocityPanel" class="panel hidden">
        <div class="title-row">
          <h1>速度控制与路线记录</h1>
          <div class="badge" id="recordBadge">未记录</div>
        </div>
        <div class="velocity-grid">
          <div>
            <label for="manualLinearSpeed">线速度（m/s）</label>
            <input id="manualLinearSpeed" type="number" value="0.55" step="0.05" min="0" max="0.55" style="height:44px;border:1px solid var(--line);border-radius:6px;" />
          </div>
          <div>
            <label for="manualAngularSpeed">角速度（rad/s）</label>
            <input id="manualAngularSpeed" type="number" value="0.42" step="0.05" min="0" max="1.00" style="height:44px;border:1px solid var(--line);border-radius:6px;" />
          </div>
          <div>
            <label>实时速度</label>
            <div class="metric-value" id="velocityText" style="height:44px;display:flex;align-items:center;border:1px solid var(--line);border-radius:6px;padding:0 12px;background:#fbfcfe;">0.00 / 0.00</div>
          </div>
        </div>
        <div class="key-help">
          <div class="key-row"><span class="key" id="keyUp">↑</span><span>前进</span><span class="key" id="keyDown">↓</span><span>后退</span></div>
          <div class="key-row"><span class="key" id="keyLeft">←</span><span>左转</span><span class="key" id="keyRight">→</span><span>右转</span><span class="key" id="keyA">A</span><span>左转</span><span class="key" id="keyD">D</span><span>右转</span></div>
          <div class="metric-name">当前 Scout 是差速轮式底盘，不发布横向平移速度；←/A 为左转，→/D 为右转，可与前进/后退同时按。</div>
        </div>
        <div class="record-actions">
          <button class="primary" id="startRecord">开始记录</button>
          <button class="secondary" id="stopRecord">停止记录</button>
          <button class="primary" id="exportRecord">导出记录</button>
          <button class="secondary" id="stopVelocity">速度归零</button>
        </div>
        <div class="status-card">
          <div class="metric"><div class="metric-name">记录样本</div><div class="metric-value" id="sampleText">0</div></div>
          <div class="metric"><div class="metric-name">记录时长</div><div class="metric-value" id="recordDurationText">0.0 s</div></div>
          <div class="metric"><div class="metric-name">导出状态</div><div class="metric-value" id="exportText">未导出</div></div>
          <div class="metric"><div class="metric-name">键盘状态</div><div class="metric-value" id="keyStateText">待命</div></div>
        </div>
        <div class="file-line" id="recordFileText">记录目录：/home/ubuntu22/VLN/VLN_RECORDINGS/manual_drives</div>
        <div class="message" id="velocityMessage">点击“开始记录”后，用键盘开车；满意后点击“导出记录”。</div>
      </div>
    </section>
  </main>
  <script>
    const $ = (id) => document.getElementById(id);
    const setTargetMessage = (text, type = '') => {
      const el = $('messageText');
      el.textContent = text;
      el.className = 'message ' + type;
    };
    const setVelocityMessage = (text, type = '') => {
      const el = $('velocityMessage');
      el.textContent = text;
      el.className = 'message ' + type;
    };
    const showPanel = (panel) => {
      $('targetPanel').classList.toggle('hidden', panel !== 'target');
      $('velocityPanel').classList.toggle('hidden', panel !== 'velocity');
      $('tabTarget').classList.toggle('active', panel === 'target');
      $('tabVelocity').classList.toggle('active', panel === 'velocity');
      $('tabCamera').classList.remove('active');
      $('tabLidar').classList.remove('active');
    };
    const flashTriggerTab = (id) => {
      ['tabTarget', 'tabVelocity', 'tabCamera', 'tabLidar'].forEach((tab) => $(tab).classList.toggle('active', tab === id));
      setTimeout(() => showPanel($('velocityPanel').classList.contains('hidden') ? 'target' : 'velocity'), 450);
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
        setTargetMessage(data.message, 'ok');
      } catch (err) { setTargetMessage(err.message, 'bad'); }
    });
    $('stopVehicle').addEventListener('click', async () => {
      try { const data = await postJson('/api/stop', {}); setTargetMessage(data.message, 'warn'); }
      catch (err) { setTargetMessage(err.message, 'bad'); }
    });
    $('tabTarget').addEventListener('click', () => showPanel('target'));
    $('tabVelocity').addEventListener('click', () => showPanel('velocity'));
    $('tabCamera').addEventListener('click', async () => {
      flashTriggerTab('tabCamera');
      try { const data = await postJson('/api/open_camera', {}); setTargetMessage(data.message, 'ok'); setVelocityMessage(data.message, 'ok'); }
      catch (err) { setTargetMessage(err.message, 'bad'); setVelocityMessage(err.message, 'bad'); }
    });
    $('tabLidar').addEventListener('click', async () => {
      flashTriggerTab('tabLidar');
      try { const data = await postJson('/api/open_lidar', {}); setTargetMessage(data.message, 'ok'); setVelocityMessage(data.message, 'ok'); }
      catch (err) { setTargetMessage(err.message, 'bad'); setVelocityMessage(err.message, 'bad'); }
    });
    const keyState = {up: false, down: false, left: false, right: false, a: false, d: false};
    const keyMap = {ArrowUp: 'up', ArrowDown: 'down', ArrowLeft: 'left', ArrowRight: 'right', KeyA: 'a', KeyD: 'd'};
    const anyKeyActive = () => Object.values(keyState).some(Boolean);
    const updateKeyClasses = () => {
      $('keyUp').classList.toggle('active', keyState.up);
      $('keyDown').classList.toggle('active', keyState.down);
      $('keyLeft').classList.toggle('active', keyState.left);
      $('keyRight').classList.toggle('active', keyState.right);
      $('keyA').classList.toggle('active', keyState.a);
      $('keyD').classList.toggle('active', keyState.d);
    };
    const sendVelocity = async () => {
      const linearSpeed = Number($('manualLinearSpeed').value || '0.55');
      const angularSpeed = Number($('manualAngularSpeed').value || '0.42');
      try {
        const data = await postJson('/api/velocity', {keys: keyState, linear_speed: linearSpeed, angular_speed: angularSpeed});
        $('velocityText').textContent = `${data.command.linear_x.toFixed(2)} / ${data.command.angular_z.toFixed(2)}`;
        const activeKeys = Object.entries(keyState).filter(([, v]) => v).map(([k]) => k.toUpperCase()).join(' + ');
        $('keyStateText').textContent = activeKeys || '待命';
      } catch (err) { setVelocityMessage(err.message, 'bad'); }
    };
    const stopVelocityNow = async () => {
      Object.keys(keyState).forEach((key) => { keyState[key] = false; });
      updateKeyClasses();
      try {
        const data = await postJson('/api/velocity_stop', {});
        $('velocityText').textContent = '0.00 / 0.00';
        $('keyStateText').textContent = '待命';
        setVelocityMessage(data.message, 'warn');
      } catch (err) { setVelocityMessage(err.message, 'bad'); }
    };
    document.addEventListener('keydown', (event) => {
      if ($('velocityPanel').classList.contains('hidden')) return;
      if (event.target && ['INPUT', 'TEXTAREA'].includes(event.target.tagName)) return;
      const key = keyMap[event.code];
      if (!key) return;
      event.preventDefault();
      if (!keyState[key]) {
        keyState[key] = true;
        updateKeyClasses();
        sendVelocity();
      }
    });
    document.addEventListener('keyup', (event) => {
      const key = keyMap[event.code];
      if (!key) return;
      event.preventDefault();
      if (keyState[key]) {
        keyState[key] = false;
        updateKeyClasses();
        if (anyKeyActive()) sendVelocity();
        else stopVelocityNow();
      }
    });
    ['manualLinearSpeed', 'manualAngularSpeed'].forEach((inputId) => {
      $(inputId).addEventListener('change', () => { if (anyKeyActive()) sendVelocity(); });
    });
    window.addEventListener('blur', async () => {
      let changed = false;
      Object.keys(keyState).forEach((key) => { if (keyState[key]) { keyState[key] = false; changed = true; } });
      if (changed) await stopVelocityNow();
    });
    document.addEventListener('visibilitychange', async () => {
      if (document.hidden && anyKeyActive()) await stopVelocityNow();
    });
    $('stopVelocity').addEventListener('click', async () => {
      await stopVelocityNow();
    });
    $('startRecord').addEventListener('click', async () => {
      try { const data = await postJson('/api/recording/start', {}); setVelocityMessage(data.message, 'ok'); }
      catch (err) { setVelocityMessage(err.message, 'bad'); }
    });
    $('stopRecord').addEventListener('click', async () => {
      try { const data = await postJson('/api/recording/stop', {}); setVelocityMessage(data.message, 'warn'); }
      catch (err) { setVelocityMessage(err.message, 'bad'); }
    });
    $('exportRecord').addEventListener('click', async () => {
      try {
        const data = await postJson('/api/recording/export', {});
        $('recordFileText').textContent = data.path;
        $('exportText').textContent = '已导出';
        setVelocityMessage(data.message, 'ok');
      } catch (err) { setVelocityMessage(err.message, 'bad'); }
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
        if (data.recording) {
          $('recordBadge').textContent = data.recording.active ? '记录中' : '未记录';
          $('sampleText').textContent = String(data.recording.samples);
          $('recordDurationText').textContent = `${data.recording.duration.toFixed(1)} s`;
          if (data.recording.last_export_path) $('recordFileText').textContent = data.recording.last_export_path;
        }
        if (data.manual) {
          $('velocityText').textContent = `${data.manual.linear_x.toFixed(2)} / ${data.manual.angular_z.toFixed(2)}`;
        }
      } catch (err) {
        $('stateBadge').textContent = '后端断开';
        $('controlText').textContent = '后端断开';
      }
    }
    refreshStatus();
    setInterval(refreshStatus, 250);
    setInterval(() => {
      if (!$('velocityPanel').classList.contains('hidden') && anyKeyActive()) sendVelocity();
    }, 50);
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
        self.manual_linear_x = 0.0
        self.manual_angular_z = 0.0
        self.manual_keys = normalize_keys({})
        self.manual_requested_linear_speed = float(args.manual_default_linear)
        self.manual_requested_angular_speed = float(args.manual_default_angular)
        self.manual_control_active = False
        self.manual_last_update_monotonic = 0.0
        self.manual_heading_hold_yaw = None
        self.manual_heading_hold_active = False
        self.manual_last_heading_error = 0.0
        self.recording_active = False
        self.recording_started_at_monotonic = None
        self.recording_started_at_utc = None
        self.recording_samples = []
        self.recording_default_linear_speed = float(args.manual_default_linear)
        self.recording_default_angular_speed = float(args.manual_default_angular)
        self.last_export_path = None
        self.shutdown_requested = False
        self.camera_process = None
        self.lidar_process = None
        self.pose_stamp_monotonic = None
        self.pose_yaw_rate = 0.0
        self.run_id = datetime.now().strftime("vln_control_panel_%Y%m%d_%H%M%S")
        self.log_dir = LOG_ROOT / self.run_id
        self.log_dir.mkdir(parents=True, exist_ok=True)
        RECORDING_ROOT.mkdir(parents=True, exist_ok=True)

        rclpy.init()
        self.node = rclpy.create_node("vln_control_panel")
        self.publisher = self.node.create_publisher(Twist, args.cmd_topic, 10)
        self.subscription = self.node.create_subscription(TFMessage, args.tf_topic, self.on_tf, 20)

    def on_tf(self, msg):
        now = time.monotonic()
        with self.lock:
            self.tf_count += 1
            for transform in msg.transforms:
                if transform.header.frame_id == "map" and transform.child_frame_id == "base_link":
                    t = transform.transform.translation
                    yaw = quaternion_yaw(transform.transform.rotation)
                    if self.pose_yaw is not None and self.pose_stamp_monotonic is not None:
                        dt = now - self.pose_stamp_monotonic
                        if dt > 1e-4:
                            self.pose_yaw_rate = normalize_angle(yaw - self.pose_yaw) / dt
                    self.pose_xy = (float(t.x), float(t.y))
                    self.pose_yaw = yaw
                    self.pose_stamp_monotonic = now

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
            self.manual_linear_x = 0.0
            self.manual_angular_z = 0.0
            self.manual_keys = normalize_keys({})
            self.manual_heading_hold_yaw = None
            self.manual_heading_hold_active = False
            self.manual_control_active = False
            self.last_message = message
        self.publish_zero(repeat=6)
        self.record_manual_sample(0.0, 0.0, normalize_keys({}), "stop")

    def make_twist(self, linear_x, angular_z):
        command = Twist()
        command.linear.x = float(linear_x)
        command.angular.z = float(angular_z)
        return command

    def publish_command(self, linear_x, angular_z):
        if not rclpy.ok():
            return False
        try:
            self.publisher.publish(self.make_twist(linear_x, angular_z))
            self.last_publish_time = time.monotonic()
            return True
        except Exception:
            return False

    def compute_manual_command(self, keys, linear_speed, angular_speed, update_heading_hold=False):
        keys = normalize_keys(keys)
        linear_speed = clamp(abs(float(linear_speed)), 0.0, self.args.manual_max_linear)
        angular_speed = clamp(abs(float(angular_speed)), 0.0, self.args.manual_max_angular)
        forward_sign = -1.0 if self.args.manual_forward_linear_sign < 0.0 else 1.0

        if keys["up"] and not keys["down"]:
            linear_x = forward_sign * linear_speed
        elif keys["down"] and not keys["up"]:
            linear_x = -forward_sign * linear_speed
        else:
            linear_x = 0.0

        left_turn = keys["left"] or keys["a"]
        right_turn = keys["right"] or keys["d"]
        left_sign = -1.0 if self.args.manual_left_angular_sign < 0.0 else 1.0
        if left_turn and not right_turn:
            angular_z = left_sign * angular_speed
        elif right_turn and not left_turn:
            angular_z = -left_sign * angular_speed
        else:
            angular_z = 0.0

        pure_straight_drive = abs(linear_x) > 1e-6 and abs(angular_z) < 1e-6
        if pure_straight_drive:
            with self.lock:
                pose_yaw = self.pose_yaw
                yaw_rate = self.pose_yaw_rate
                if update_heading_hold and self.manual_heading_hold_yaw is None and pose_yaw is not None:
                    self.manual_heading_hold_yaw = pose_yaw
                hold_yaw = self.manual_heading_hold_yaw

            if pose_yaw is not None and hold_yaw is not None:
                heading_error = normalize_angle(hold_yaw - pose_yaw)
                correction = (
                    self.args.manual_heading_hold_kp * heading_error
                    - self.args.manual_heading_hold_kd * yaw_rate
                )
                yaw_sign = -1.0 if self.args.manual_yaw_correction_sign < 0.0 else 1.0
                angular_z = clamp(
                    yaw_sign * correction,
                    -self.args.manual_heading_hold_max_angular,
                    self.args.manual_heading_hold_max_angular,
                )
                if abs(heading_error) < self.args.manual_heading_hold_deadband and abs(yaw_rate) < 0.02:
                    angular_z = 0.0
                with self.lock:
                    self.manual_heading_hold_active = True
                    self.manual_last_heading_error = heading_error
        else:
            with self.lock:
                self.manual_heading_hold_yaw = None
                self.manual_heading_hold_active = False
                self.manual_last_heading_error = 0.0

        return keys, linear_x, angular_z

    def set_manual_velocity(self, keys, linear_speed, angular_speed):
        keys, linear_x, angular_z = self.compute_manual_command(keys, linear_speed, angular_speed, update_heading_hold=True)
        active = abs(linear_x) > 1e-6 or abs(angular_z) > 1e-6
        with self.lock:
            self.active = False
            self.target_map_xy = None
            self.target_relative_xy = None
            self.distance = None
            self.manual_keys = keys
            self.manual_requested_linear_speed = clamp(abs(float(linear_speed)), 0.0, self.args.manual_max_linear)
            self.manual_requested_angular_speed = clamp(abs(float(angular_speed)), 0.0, self.args.manual_max_angular)
            self.manual_linear_x = linear_x
            self.manual_angular_z = angular_z
            self.manual_control_active = active
            self.manual_last_update_monotonic = time.monotonic()
            self.recording_default_linear_speed = clamp(abs(float(linear_speed)), 0.0, self.args.manual_max_linear)
            self.recording_default_angular_speed = clamp(abs(float(angular_speed)), 0.0, self.args.manual_max_angular)
            self.last_message = "手动速度控制中" if active else "手动速度已归零"

        self.publish_command(linear_x, angular_z)
        self.record_manual_sample(linear_x, angular_z, keys, "manual_update")
        message = f"手动速度：线速度 {linear_x:.2f} m/s，角速度 {angular_z:.2f} rad/s"
        if not active:
            message = "手动速度已归零"
        return {
            "ok": True,
            "message": message,
            "command": {"linear_x": linear_x, "angular_z": angular_z},
            "keys": keys,
        }

    def stop_manual_velocity(self):
        with self.lock:
            self.active = False
            self.target_map_xy = None
            self.target_relative_xy = None
            self.distance = None
            self.manual_linear_x = 0.0
            self.manual_angular_z = 0.0
            self.manual_keys = normalize_keys({})
            self.manual_control_active = False
            self.manual_heading_hold_yaw = None
            self.manual_heading_hold_active = False
            self.manual_last_heading_error = 0.0
            self.last_message = "手动速度已归零"
        self.publish_zero(repeat=10)
        self.record_manual_sample(0.0, 0.0, normalize_keys({}), "manual_stop")
        return {"ok": True, "message": "速度已归零", "command": {"linear_x": 0.0, "angular_z": 0.0}}

    def _pose_payload_locked(self):
        if self.pose_xy is None or self.pose_yaw is None:
            return None
        return {"x": self.pose_xy[0], "y": self.pose_xy[1], "yaw": self.pose_yaw}

    def _append_record_sample_locked(self, now, linear_x, angular_z, keys, source):
        if not self.recording_active or self.recording_started_at_monotonic is None:
            return
        self.recording_samples.append({
            "t": round(now - self.recording_started_at_monotonic, 4),
            "wall_time": utc_now_iso(),
            "linear_x": round(float(linear_x), 6),
            "angular_z": round(float(angular_z), 6),
            "keys": normalize_keys(keys),
            "pose": self._pose_payload_locked(),
            "source": source,
        })

    def record_manual_sample(self, linear_x, angular_z, keys, source):
        now = time.monotonic()
        with self.lock:
            self._append_record_sample_locked(now, linear_x, angular_z, keys, source)

    def start_recording(self):
        now = time.monotonic()
        with self.lock:
            self.recording_active = True
            self.recording_started_at_monotonic = now
            self.recording_started_at_utc = utc_now_iso()
            self.recording_samples = []
            self.last_export_path = None
            self._append_record_sample_locked(now, self.manual_linear_x, self.manual_angular_z, self.manual_keys, "recording_start")
        return {"ok": True, "message": "已开始记录手动速度数据"}

    def stop_recording(self):
        now = time.monotonic()
        with self.lock:
            if not self.recording_active:
                return {"ok": True, "message": "当前没有正在记录的路线"}
            self._append_record_sample_locked(now, self.manual_linear_x, self.manual_angular_z, self.manual_keys, "recording_stop")
            self.recording_active = False
        return {"ok": True, "message": "已停止记录，满意后可点击导出"}

    def export_recording(self):
        with self.lock:
            if not self.recording_samples:
                raise RuntimeError("还没有可导出的速度记录。请先点击“开始记录”，再用键盘开车。")
            if self.recording_active:
                self._append_record_sample_locked(time.monotonic(), self.manual_linear_x, self.manual_angular_z, self.manual_keys, "export_snapshot")
            samples = list(self.recording_samples)
            started_at = self.recording_started_at_utc or utc_now_iso()
            default_linear = self.recording_default_linear_speed
            default_angular = self.recording_default_angular_speed

        duration = max((sample.get("t", 0.0) for sample in samples), default=0.0)
        file_name = "manual_drive_" + datetime.now().strftime("%Y%m%d_%H%M%S") + ".json"
        path = RECORDING_ROOT / file_name
        payload = {
            "schema": RECORDING_SCHEMA,
            "created_at": utc_now_iso(),
            "recording_started_at": started_at,
            "cmd_topic": self.args.cmd_topic,
            "tf_topic": self.args.tf_topic,
            "publish_rate_hz": float(self.args.publish_rate),
        "manual_left_angular_sign": -1.0 if self.args.manual_left_angular_sign < 0.0 else 1.0,
            "manual_forward_linear_sign": -1.0 if self.args.manual_forward_linear_sign < 0.0 else 1.0,
            "manual_yaw_correction_sign": -1.0 if self.args.manual_yaw_correction_sign < 0.0 else 1.0,
            "manual_heading_hold_kp": float(self.args.manual_heading_hold_kp),
            "manual_heading_hold_kd": float(self.args.manual_heading_hold_kd),
            "manual_heading_hold_max_angular": float(self.args.manual_heading_hold_max_angular),
            "default_linear_speed_mps": default_linear,
            "default_angular_speed_radps": default_angular,
            "duration_sec": round(duration, 4),
            "sample_count": len(samples),
            "samples": samples,
        }
        with open(path, "w", encoding="utf-8") as handle:
            json.dump(payload, handle, ensure_ascii=False, indent=2)
            handle.write("\n")
        with self.lock:
            self.last_export_path = str(path)
        return {"ok": True, "message": f"已导出 {len(samples)} 条速度样本", "path": str(path), "sample_count": len(samples)}

    def publish_zero(self, repeat=1):
        if not rclpy.ok():
            return
        zero = Twist()
        period = min(0.01, 1.0 / max(1.0, self.args.publish_rate))
        for _ in range(max(1, repeat)):
            try:
                self.publisher.publish(zero)
            except Exception:
                break
            time.sleep(period)

    def update_control(self):
        now = time.monotonic()
        if now - self.last_publish_time < 1.0 / max(1.0, self.args.publish_rate):
            return

        with self.lock:
            manual_active = self.manual_control_active
            manual_keys = dict(self.manual_keys)
            manual_requested_linear_speed = self.manual_requested_linear_speed
            manual_requested_angular_speed = self.manual_requested_angular_speed
            manual_age = now - self.manual_last_update_monotonic

        if manual_active:
            if manual_age > max(0.05, self.args.manual_command_timeout):
                with self.lock:
                    self.manual_linear_x = 0.0
                    self.manual_angular_z = 0.0
                    self.manual_keys = normalize_keys({})
                    self.manual_control_active = False
                    self.manual_heading_hold_yaw = None
                    self.manual_heading_hold_active = False
                    self.last_message = "手动速度心跳超时，已自动停车"
                self.publish_zero(repeat=10)
                self.record_manual_sample(0.0, 0.0, normalize_keys({}), "manual_timeout_stop")
                return
            manual_keys, manual_linear_x, manual_angular_z = self.compute_manual_command(
                manual_keys,
                manual_requested_linear_speed,
                manual_requested_angular_speed,
                update_heading_hold=True,
            )
            with self.lock:
                self.manual_linear_x = manual_linear_x
                self.manual_angular_z = manual_angular_z
            if self.publish_command(manual_linear_x, manual_angular_z):
                self.record_manual_sample(manual_linear_x, manual_angular_z, manual_keys, "manual_publish")
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
        now = time.monotonic()
        with self.lock:
            pose = None
            if self.pose_xy is not None and self.pose_yaw is not None:
                pose = {"x": self.pose_xy[0], "y": self.pose_xy[1], "yaw": self.pose_yaw}
            if self.recording_active and self.recording_started_at_monotonic is not None:
                recording_duration = now - self.recording_started_at_monotonic
            elif self.recording_samples:
                recording_duration = float(self.recording_samples[-1].get("t", 0.0))
            else:
                recording_duration = 0.0
            return {
                "ok": True,
                "pose": pose,
                "tf_count": self.tf_count,
                "active": self.active,
                "manual": {
                    "active": self.manual_control_active,
                    "linear_x": self.manual_linear_x,
                    "angular_z": self.manual_angular_z,
                    "keys": self.manual_keys,
                    "heading_hold_active": self.manual_heading_hold_active,
                    "heading_hold_yaw": self.manual_heading_hold_yaw,
                    "heading_error": self.manual_last_heading_error,
                    "yaw_rate": self.pose_yaw_rate,
                },
                "recording": {
                    "active": self.recording_active,
                    "samples": len(self.recording_samples),
                    "duration": recording_duration,
                    "last_export_path": self.last_export_path,
                    "directory": str(RECORDING_ROOT),
                },
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
                if path == "/api/velocity":
                    self.send_json(panel.set_manual_velocity(
                        data.get("keys", {}),
                        data.get("linear_speed", panel.args.manual_default_linear),
                        data.get("angular_speed", panel.args.manual_default_angular),
                    ))
                    return
                if path == "/api/velocity_stop":
                    self.send_json(panel.stop_manual_velocity())
                    return
                if path == "/api/recording/start":
                    self.send_json(panel.start_recording())
                    return
                if path == "/api/recording/stop":
                    self.send_json(panel.stop_recording())
                    return
                if path == "/api/recording/export":
                    self.send_json(panel.export_recording())
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
    parser.add_argument("--publish-rate", type=float, default=100.0)
    parser.add_argument("--manual-default-linear", type=float, default=0.55, help="速度控制面板默认线速度，单位 m/s。")
    parser.add_argument("--manual-default-angular", type=float, default=0.42, help="速度控制面板默认角速度，单位 rad/s。")
    parser.add_argument("--manual-max-linear", type=float, default=0.55, help="速度控制面板允许的最大线速度绝对值；更高速度需要单独做轮胎/电机标定。")
    parser.add_argument("--manual-max-angular", type=float, default=1.00, help="速度控制面板允许的最大角速度绝对值。")
    parser.add_argument("--manual-command-timeout", type=float, default=0.18, help="速度控制心跳超时时间；浏览器停止刷新按键状态后自动停车。")
    parser.add_argument(
        "--manual-forward-linear-sign",
        type=float,
        default=1.0,
        choices=(-1.0, 1.0),
        help="↑ 前进按键对应 linear.x 的符号；默认保持 ROS2 标准：前进为正 linear.x。",
    )
    parser.add_argument(
        "--manual-left-angular-sign",
        type=float,
        default=1.0,
        choices=(-1.0, 1.0),
        help="左转按键对应的 angular.z 符号；当前 Scout wheel-ground 候选使用 ROS2 常规约定：左转为正 angular.z。",
    )
    parser.add_argument(
        "--manual-yaw-correction-sign",
        type=float,
        default=1.0,
        choices=(-1.0, 1.0),
        help="手动直行航向保持的 angular.z 修正符号；当前 Scout wheel-ground 候选使用正 angular.z 左转的闭环约定。",
    )
    parser.add_argument("--manual-heading-hold-kp", type=float, default=0.0, help="手动直行航向保持 P 增益；默认关闭，避免 UI 层过度纠偏。")
    parser.add_argument("--manual-heading-hold-kd", type=float, default=0.0, help="手动直行航向保持 D 增益；默认关闭，Unity 物理层仍保留 yaw-rate PID。")
    parser.add_argument("--manual-heading-hold-max-angular", type=float, default=0.0, help="直行航向保持允许叠加的最大角速度；默认 0 表示不额外改写用户直行指令。")
    parser.add_argument("--manual-heading-hold-deadband", type=float, default=0.015, help="直行航向误差死区，单位 rad。")
    parser.add_argument("--no-browser", action="store_true")
    return parser.parse_args()


def main():
    args = parse_args()
    panel = ControlPanel(args)
    server = ThreadingHTTPServer((args.host, args.port), build_handler(panel))
    server.timeout = 0.005
    url = f"http://{args.host}:{args.port}/"
    print(f"VLN 控制面板已启动：{url}")
    print(f"日志目录：{panel.log_dir}")
    print("前提：endpoint 已启动，Unity 主场景已点击 Play。按 Ctrl+C 退出。")

    if not args.no_browser:
        threading.Timer(0.5, lambda: webbrowser.open(url)).start()

    server_thread = threading.Thread(target=server.serve_forever, kwargs={"poll_interval": 0.005}, daemon=True)
    server_thread.start()

    try:
        loop_period = 1.0 / max(100.0, float(args.publish_rate) * 2.0)
        while rclpy.ok() and not panel.shutdown_requested:
            loop_start = time.monotonic()
            rclpy.spin_once(panel.node, timeout_sec=0.001)
            panel.update_control()
            elapsed = time.monotonic() - loop_start
            if elapsed < loop_period:
                time.sleep(loop_period - elapsed)
    except (KeyboardInterrupt, rclpy.executors.ExternalShutdownException):
        pass
    finally:
        server.shutdown()
        server.server_close()
        server_thread.join(timeout=1.0)
        panel.cleanup()
        print("VLN 控制面板已退出")


if __name__ == "__main__":
    raise SystemExit(main())
