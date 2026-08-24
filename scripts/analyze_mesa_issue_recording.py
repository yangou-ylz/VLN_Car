#!/usr/bin/env python3
"""Analyze a Mesa Topgear manual terrain issue recording.

This script intentionally uses only the Python standard library so it does not
touch the user's ROS2/Conda/CUDA environment.
"""

from __future__ import annotations

import argparse
import csv
import math
from collections import Counter
from pathlib import Path


VLN_ROOT = Path('/home/ubuntu22/VLN')
DEFAULT_RECORD_ROOT = VLN_ROOT / 'UnityProjects' / 'VLN_Offroad_LargeAssetSandbox' / 'Logs' / 'mesa_issue_records'


def as_float(row: dict[str, str], key: str, default: float = 0.0) -> float:
    value = row.get(key, '')
    try:
        return float(value)
    except (TypeError, ValueError):
        return default


def as_int(row: dict[str, str], key: str, default: int = 0) -> int:
    value = row.get(key, '')
    try:
        return int(float(value))
    except (TypeError, ValueError):
        return default


def sample_time(row: dict[str, str]) -> float:
    if 'recording_time_s' in row:
        return as_float(row, 'recording_time_s')
    return as_float(row, 'time_s')


def latest_recording(root: Path) -> Path:
    ready_dirs = sorted(root.glob('mesa_issue_*'), key=lambda p: p.stat().st_mtime, reverse=True)
    if ready_dirs and not (ready_dirs[0] / 'samples.csv').is_file():
        raise FileNotFoundError(
            f'最新记录目录是 {ready_dirs[0]}，但里面没有 samples.csv。'
            f'这通常说明这次 Unity Play 里没有按 F6 开始录制。'
            f'请重新进入 Play 后按 F6 开始、开过问题地形、按 F7 结束；如果你确实要分析旧记录，请把旧 mesa_issue_* 目录路径显式传给本脚本。'
        )
    candidates = [p for p in ready_dirs if (p / 'samples.csv').is_file()]
    if not candidates:
        hint = ''
        if ready_dirs:
            hint = f' 最新目录是 {ready_dirs[0]}，但里面没有 samples.csv。'
        raise FileNotFoundError(
            f'没有找到有效录制：{root}/mesa_issue_*/samples.csv。'
            f'{hint}请在 Unity Play 后按 F6 开始录制，手动开过问题地形，再按 F7 结束录制。'
        )
    return max(candidates, key=lambda p: p.stat().st_mtime)


def load_rows(recording: Path) -> list[dict[str, str]]:
    sample_path = recording / 'samples.csv'
    with sample_path.open('r', newline='', encoding='utf-8') as f:
        rows = list(csv.DictReader(f))
    if not rows:
        raise RuntimeError(f'samples.csv 没有数据：{sample_path}。请确认已经按 F6 开始录制，并在开过问题地形后按 F7 结束录制。')
    return rows


def find_stuck_windows(rows: list[dict[str, str]]) -> list[tuple[int, int]]:
    windows: list[tuple[int, int]] = []
    start: int | None = None
    for i, row in enumerate(rows):
        stuck = as_int(row, 'stuck_signal') == 1
        if stuck and start is None:
            start = i
        elif not stuck and start is not None:
            if i - start >= 5:
                windows.append((start, i - 1))
            start = None
    if start is not None and len(rows) - start >= 5:
        windows.append((start, len(rows) - 1))
    return windows


def find_active_command_segments(rows: list[dict[str, str]]) -> list[tuple[int, int]]:
    segments: list[tuple[int, int]] = []
    start: int | None = None
    for i, row in enumerate(rows):
        active = as_int(row, 'command_active') == 1
        if active and start is None:
            start = i
        elif not active and start is not None:
            segments.append((start, i - 1))
            start = None
    if start is not None:
        segments.append((start, len(rows) - 1))
    return segments


def command_health(rows: list[dict[str, str]]) -> dict[str, float | int]:
    intervals = [sample_time(b) - sample_time(a) for a, b in zip(rows, rows[1:])]
    active_samples = sum(1 for row in rows if as_int(row, 'command_active') == 1)
    nonzero_samples = sum(
        1 for row in rows
        if abs(as_float(row, 'cmd_linear_x')) > 0.01 or abs(as_float(row, 'cmd_angular_z')) > 0.01
    )
    active_segments = find_active_command_segments(rows)
    duration = max(0.0, sample_time(rows[-1]) - sample_time(rows[0])) if rows else 0.0
    max_gap = max(intervals) if intervals else 0.0
    avg_gap = sum(intervals) / len(intervals) if intervals else 0.0
    longest_active_duration = 0.0
    for start, end in active_segments:
        longest_active_duration = max(longest_active_duration, sample_time(rows[end]) - sample_time(rows[start]))
    return {
        'duration_s': duration,
        'avg_sample_gap_s': avg_gap,
        'max_sample_gap_s': max_gap,
        'active_samples': active_samples,
        'nonzero_command_samples': nonzero_samples,
        'active_ratio': active_samples / max(1, len(rows)),
        'active_segment_count': len(active_segments),
        'longest_active_segment_s': longest_active_duration,
    }


def summarize_window(rows: list[dict[str, str]], start: int, end: int) -> dict[str, float | str]:
    segment = rows[start:end + 1]
    duration = sample_time(segment[-1]) - sample_time(segment[0])
    mean_forward = sum(as_float(r, 'forward_speed_mps') for r in segment) / len(segment)
    mean_cmd = sum(as_float(r, 'cmd_linear_x') for r in segment) / len(segment)
    max_wheel_slope = max(as_float(r, 'max_wheel_slope_deg') for r in segment)
    max_terrain_slope = max(as_float(r, 'terrain_slope_under_body_deg') for r in segment)
    max_raycast_slope = max(as_float(r, 'raycast_slope_deg') for r in segment)
    max_forward_slip = max(as_float(r, 'max_abs_forward_slip') for r in segment)
    max_sideways_slip = max(as_float(r, 'max_abs_sideways_slip') for r in segment)
    max_rpm = max(as_float(r, 'max_abs_wheel_rpm') for r in segment)
    max_motor = max(as_float(r, 'max_abs_motor_torque_nm') for r in segment)
    max_brake = max(as_float(r, 'max_brake_torque_nm') for r in segment)
    min_contacts = min(as_int(r, 'wheel_contact_count') for r in segment)
    max_other_contacts = max(as_int(r, 'other_wheel_contact_count') for r in segment)
    colliders = Counter()
    for row in segment:
        for name in row.get('collider_names', '').split('|'):
            name = name.strip()
            if name:
                colliders[name] += 1
    return {
        'start_time': sample_time(segment[0]),
        'end_time': sample_time(segment[-1]),
        'duration_s': duration,
        'start_pos': f"{segment[0].get('pos_x')},{segment[0].get('pos_y')},{segment[0].get('pos_z')}",
        'end_pos': f"{segment[-1].get('pos_x')},{segment[-1].get('pos_y')},{segment[-1].get('pos_z')}",
        'mean_cmd_linear_x': mean_cmd,
        'mean_forward_speed_mps': mean_forward,
        'max_wheel_slope_deg': max_wheel_slope,
        'max_terrain_slope_deg': max_terrain_slope,
        'max_raycast_slope_deg': max_raycast_slope,
        'max_abs_forward_slip': max_forward_slip,
        'max_abs_sideways_slip': max_sideways_slip,
        'max_abs_wheel_rpm': max_rpm,
        'max_abs_motor_torque_nm': max_motor,
        'max_brake_torque_nm': max_brake,
        'min_wheel_contact_count': float(min_contacts),
        'max_other_wheel_contact_count': float(max_other_contacts),
        'top_colliders': ' | '.join(f'{name} ({count})' for name, count in colliders.most_common(5)),
    }


def diagnose(rows: list[dict[str, str]], windows: list[tuple[int, int]]) -> list[str]:
    notes: list[str] = []
    health = command_health(rows)
    if int(health['nonzero_command_samples']) > 0 and float(health['active_ratio']) < 0.35:
        notes.append(
            '控制命令明显断流：录制期间曾收到非零速度，但 command_active 样本只占 '
            f"{float(health['active_ratio']) * 100.0:.1f}% ，有效命令段 {int(health['active_segment_count'])} 段，"
            f"最长连续有效段约 {float(health['longest_active_segment_s']):.2f}s。"
            '这更像是网页/HTTP/前端按键心跳没有持续送达，而不是地形或轮胎卡住。'
        )
    if not windows:
        notes.append('未检测到明显卡滞窗口：本次更像是控制命令没有持续生效，或问题时间太短/没有开到问题坡。')
        return notes

    for idx, (start, end) in enumerate(windows[:5], start=1):
        w = summarize_window(rows, start, end)
        reasons: list[str] = []
        max_slope = max(float(w['max_wheel_slope_deg']), float(w['max_terrain_slope_deg']), float(w['max_raycast_slope_deg']))
        if max_slope >= 32:
            reasons.append('局部坡度很大，超过普通轮式车可稳定爬坡范围，需改路线/坡口过渡/轮胎抓地参数')
        if float(w['max_abs_forward_slip']) >= 0.65 or float(w['max_abs_sideways_slip']) >= 0.65:
            reasons.append('轮胎滑移明显，优先看沙地摩擦、轮胎 forward/sideways stiffness、滚阻设置')
        if float(w['max_abs_wheel_rpm']) >= 180 and abs(float(w['mean_forward_speed_mps'])) < 0.12:
            reasons.append('轮子在转但车不走，典型是打滑、碰撞体台阶卡住或底盘挂住')
        if float(w['max_abs_motor_torque_nm']) >= 150 and abs(float(w['mean_forward_speed_mps'])) < 0.12:
            reasons.append('电机扭矩接近上限但前进慢，可能需要坡面物理代理/扭矩曲线/低速爬坡控制优化')
        if float(w['max_brake_torque_nm']) > 20 and abs(float(w['mean_cmd_linear_x'])) > 0.2:
            reasons.append('前进命令期间存在刹车扭矩，需查 overspeed/超时/停止阻尼逻辑是否误触发')
        if float(w['min_wheel_contact_count']) <= 1:
            reasons.append('轮胎接触数过低，可能坡口太尖、车体弹跳、Terrain/MeshCollider 代理不连续')
        if float(w['max_other_wheel_contact_count']) > 0:
            reasons.append('轮胎碰到了非 Terrain 碰撞体，可能是岩石/台阶/路沿 collider 卡住')
        if not reasons:
            reasons.append('卡滞存在但单项指标不极端，需要看截图和 collider 名称进一步判断')
        notes.append(
            f"窗口 {idx}: {w['start_time']:.2f}s-{w['end_time']:.2f}s，持续 {w['duration_s']:.2f}s，"
            f"位置 {w['start_pos']} -> {w['end_pos']}，平均命令 {w['mean_cmd_linear_x']:.2f}m/s，"
            f"平均前向速度 {w['mean_forward_speed_mps']:.2f}m/s，最大坡度约 {max_slope:.1f}deg。"
        )
        notes.extend(f'  - {reason}' for reason in reasons)
        if w['top_colliders']:
            notes.append(f"  - 主要接触体：{w['top_colliders']}")
    return notes


def write_report(recording: Path, rows: list[dict[str, str]], notes: list[str]) -> Path:
    report = recording / 'analysis_report.txt'
    events_path = recording / 'events.txt'
    metadata_path = recording / 'metadata.txt'
    screenshots = sorted(recording.glob('*.png'))
    overall_max_slope = max(
        max(as_float(r, 'max_wheel_slope_deg'), as_float(r, 'terrain_slope_under_body_deg'), as_float(r, 'raycast_slope_deg'))
        for r in rows
    )
    overall_max_slip = max(max(as_float(r, 'max_abs_forward_slip'), as_float(r, 'max_abs_sideways_slip')) for r in rows)
    stuck_samples = sum(as_int(r, 'stuck_signal') for r in rows)
    command_samples = sum(1 for r in rows if as_int(r, 'command_active') == 1)
    duration = sample_time(rows[-1]) - sample_time(rows[0])
    health = command_health(rows)

    lines = [
        'Mesa Topgear 问题地形记录分析',
        f'记录目录: {recording}',
        f'样本数: {len(rows)}',
        f'时长: {duration:.2f}s',
        f'有命令样本: {command_samples}',
        f"命令活跃率: {float(health['active_ratio']) * 100.0:.1f}%",
        f"非零命令样本: {int(health['nonzero_command_samples'])}",
        f"有效命令段数: {int(health['active_segment_count'])}",
        f"最长连续有效命令段: {float(health['longest_active_segment_s']):.2f}s",
        f"平均采样间隔: {float(health['avg_sample_gap_s']):.3f}s",
        f"最大采样间隔: {float(health['max_sample_gap_s']):.3f}s",
        f'卡滞样本: {stuck_samples}',
        f'最大局部坡度: {overall_max_slope:.2f}deg',
        f'最大滑移指标: {overall_max_slip:.3f}',
        f'截图数量: {len(screenshots)}',
        '',
        '诊断:',
        *notes,
        '',
        f'样本CSV: {recording / "samples.csv"}',
        f'事件记录: {events_path if events_path.exists() else "missing"}',
        f'元数据: {metadata_path if metadata_path.exists() else "missing"}',
    ]
    if screenshots:
        lines.append('截图:')
        lines.extend(f'- {p}' for p in screenshots)
    report.write_text('\n'.join(lines) + '\n', encoding='utf-8')
    return report


def main() -> int:
    parser = argparse.ArgumentParser(description='Analyze Mesa Topgear manual terrain issue recording.')
    parser.add_argument('recording', nargs='?', help='记录目录；省略则分析最新 mesa_issue_*')
    parser.add_argument('--root', default=str(DEFAULT_RECORD_ROOT), help='记录根目录')
    args = parser.parse_args()

    try:
        recording = Path(args.recording).expanduser() if args.recording else latest_recording(Path(args.root))
        recording = recording.resolve()
        rows = load_rows(recording)
    except (FileNotFoundError, RuntimeError) as exc:
        print(f'分析失败: {exc}')
        return 1
    windows = find_stuck_windows(rows)
    notes = diagnose(rows, windows)
    report = write_report(recording, rows, notes)
    print(f'记录目录: {recording}')
    print(f'分析报告: {report}')
    print('\n'.join(notes[:12]))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
