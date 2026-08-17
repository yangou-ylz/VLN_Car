#!/usr/bin/env python3
"""回放中文控制面板导出的手动驾驶速度记录。"""

import argparse
import json
import math
import sys
import time
from pathlib import Path

import rclpy
from geometry_msgs.msg import Twist


EXPECTED_SCHEMA = "vln_manual_cmd_vel_recording_v1"


def parse_args():
    parser = argparse.ArgumentParser(description="按时间戳回放手动驾驶记录，重新发布 /vln/cmd_vel")
    parser.add_argument("--file", required=True, help="控制面板导出的 manual_drive_*.json 文件")
    parser.add_argument("--cmd-topic", default=None, help="覆盖记录文件中的 cmd topic，默认使用文件内 cmd_topic")
    parser.add_argument("--speed-scale", type=float, default=1.0, help="速度倍率，1.0 表示原速")
    parser.add_argument("--time-scale", type=float, default=1.0, help="时间倍率，2.0 表示两倍速回放")
    parser.add_argument("--start-offset", type=float, default=0.0, help="从记录的第几秒开始回放")
    parser.add_argument("--max-duration", type=float, default=None, help="最多回放多少秒，默认不限制")
    parser.add_argument("--no-stop-at-end", action="store_true", help="结束时不额外发布零速度；默认会停车")
    return parser.parse_args()


def load_recording(path):
    with open(path, "r", encoding="utf-8") as handle:
        payload = json.load(handle)
    if payload.get("schema") != EXPECTED_SCHEMA:
        raise ValueError(f"记录 schema 不匹配：{payload.get('schema')}，期望 {EXPECTED_SCHEMA}")
    samples = payload.get("samples")
    if not isinstance(samples, list) or not samples:
        raise ValueError("记录文件没有 samples")
    cleaned = []
    for index, sample in enumerate(samples):
        try:
            t = float(sample.get("t", 0.0))
            linear_x = float(sample.get("linear_x", 0.0))
            angular_z = float(sample.get("angular_z", 0.0))
        except (TypeError, ValueError) as exc:
            raise ValueError(f"第 {index} 条样本不是有效速度数据") from exc
        if not all(math.isfinite(value) for value in (t, linear_x, angular_z)):
            raise ValueError(f"第 {index} 条样本包含非有限数值")
        cleaned.append({"t": max(0.0, t), "linear_x": linear_x, "angular_z": angular_z})
    cleaned.sort(key=lambda item: item["t"])
    return payload, cleaned


def make_twist(linear_x, angular_z):
    msg = Twist()
    msg.linear.x = float(linear_x)
    msg.angular.z = float(angular_z)
    return msg


def publish_zero(node, publisher, repeats=10):
    zero = Twist()
    for _ in range(max(1, repeats)):
        publisher.publish(zero)
        rclpy.spin_once(node, timeout_sec=0.01)
        time.sleep(0.05)


def main():
    args = parse_args()
    if args.speed_scale < 0.0 or not math.isfinite(args.speed_scale):
        print("--speed-scale 必须是非负有限数", file=sys.stderr)
        return 2
    if args.time_scale <= 0.0 or not math.isfinite(args.time_scale):
        print("--time-scale 必须是正有限数", file=sys.stderr)
        return 2

    path = Path(args.file).expanduser().resolve()
    payload, samples = load_recording(path)
    cmd_topic = args.cmd_topic or payload.get("cmd_topic") or "/vln/cmd_vel"
    start_offset = max(0.0, float(args.start_offset))
    max_record_t = samples[-1]["t"]
    end_offset = max_record_t if args.max_duration is None else min(max_record_t, start_offset + max(0.0, args.max_duration))
    replay_samples = [sample for sample in samples if start_offset <= sample["t"] <= end_offset]
    if not replay_samples:
        print("指定时间范围内没有可回放样本", file=sys.stderr)
        return 1

    rclpy.init()
    node = rclpy.create_node("vln_manual_drive_replay")
    publisher = node.create_publisher(Twist, cmd_topic, 10)

    publish_count = 0
    replay_started = time.monotonic()
    first_t = replay_samples[0]["t"]
    try:
        print(f"recording_file={path}")
        print(f"cmd_topic={cmd_topic}")
        print(f"sample_count={len(replay_samples)}")
        print(f"recording_window_sec={start_offset:.3f},{end_offset:.3f}")
        print(f"speed_scale={args.speed_scale:.3f}")
        print(f"time_scale={args.time_scale:.3f}")

        for sample in replay_samples:
            target_elapsed = (sample["t"] - first_t) / args.time_scale
            wait_seconds = target_elapsed - (time.monotonic() - replay_started)
            if wait_seconds > 0.0:
                time.sleep(wait_seconds)
            msg = make_twist(sample["linear_x"] * args.speed_scale, sample["angular_z"] * args.speed_scale)
            publisher.publish(msg)
            publish_count += 1
            rclpy.spin_once(node, timeout_sec=0.01)

        if not args.no_stop_at_end:
            publish_zero(node, publisher)
        print(f"published_count={publish_count}")
        print("VLN_MANUAL_DRIVE_REPLAY_OK")
        return 0
    except KeyboardInterrupt:
        publish_zero(node, publisher)
        print("手动中断，已发布零速度")
        return 130
    finally:
        try:
            node.destroy_node()
        finally:
            if rclpy.ok():
                rclpy.shutdown()


if __name__ == "__main__":
    raise SystemExit(main())
