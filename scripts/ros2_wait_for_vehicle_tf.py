#!/usr/bin/env python3
"""等待车辆 TF，并按需要验证 base_link 静止或运动。"""

import argparse
import math
import sys
import time

import rclpy
from tf2_msgs.msg import TFMessage


def parse_args():
    parser = argparse.ArgumentParser(description="等待并校验 /tf 中的车辆 frame 树")
    parser.add_argument("--topic", default="/tf")
    parser.add_argument("--timeout", type=float, default=40.0)
    parser.add_argument("--min-base-delta", type=float, default=0.0)
    parser.add_argument("--max-base-delta", type=float, default=None)
    parser.add_argument("--stable-observe-seconds", type=float, default=5.0)
    parser.add_argument(
        "--required-edge",
        action="append",
        default=None,
        help="额外或自定义 TF 边，格式 parent:child。若提供，则替代默认三条边；可重复。",
    )
    return parser.parse_args()


def parse_required_edges(values):
    if not values:
        return {
            ("map", "base_link"),
            ("base_link", "front_camera_optical_frame"),
            ("base_link", "lidar_link"),
        }

    edges = set()
    for value in values:
        if ":" not in value:
            raise ValueError(f"required-edge 格式错误：{value}，应为 parent:child")
        parent, child = value.split(":", 1)
        parent = parent.strip()
        child = child.strip()
        if not parent or not child:
            raise ValueError(f"required-edge 格式错误：{value}，parent/child 不能为空")
        edges.add((parent, child))
    return edges


def norm_delta(a, b):
    return math.sqrt((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2)


def main():
    args = parse_args()
    deadline = time.monotonic() + args.timeout
    try:
        required_edges = parse_required_edges(args.required_edge)
    except ValueError as exc:
        print(str(exc), file=sys.stderr)
        return 2
    seen_edges = set()
    first_base_position = None
    last_base_position = None
    max_base_delta = 0.0
    message_count = 0
    complete_since = None

    rclpy.init()
    node = rclpy.create_node("vln_wait_for_vehicle_tf")

    def on_tf(msg):
        nonlocal first_base_position, last_base_position, max_base_delta, message_count
        message_count += 1
        for transform in msg.transforms:
            edge = (transform.header.frame_id, transform.child_frame_id)
            if edge in required_edges:
                seen_edges.add(edge)
            if edge == ("map", "base_link"):
                translation = transform.transform.translation
                position = (translation.x, translation.y, translation.z)
                if first_base_position is None:
                    first_base_position = position
                last_base_position = position
                max_base_delta = max(max_base_delta, norm_delta(first_base_position, position))

    subscription = node.create_subscription(TFMessage, args.topic, on_tf, 20)

    try:
        while time.monotonic() < deadline:
            rclpy.spin_once(node, timeout_sec=0.25)
            if seen_edges == required_edges and first_base_position is not None and last_base_position is not None:
                if complete_since is None:
                    complete_since = time.monotonic()
                has_enough_motion = max_base_delta >= args.min_base_delta
                observed_static_window = (
                    args.max_base_delta is None
                    or time.monotonic() - complete_since >= max(0.0, args.stable_observe_seconds)
                    or max_base_delta > args.max_base_delta
                )
                if has_enough_motion and observed_static_window:
                    break

        missing_edges = required_edges - seen_edges
        base_delta = 0.0
        if first_base_position is not None and last_base_position is not None:
            base_delta = norm_delta(first_base_position, last_base_position)

        print(f"topic={args.topic}")
        print(f"message_count={message_count}")
        print("seen_edges=" + ",".join(f"{parent}->{child}" for parent, child in sorted(seen_edges)))
        print(f"base_delta={base_delta:.3f}")
        print(f"max_base_delta={max_base_delta:.3f}")
        if first_base_position is not None:
            print("first_base_position=" + ",".join(f"{value:.3f}" for value in first_base_position))
        if last_base_position is not None:
            print("last_base_position=" + ",".join(f"{value:.3f}" for value in last_base_position))

        errors = []
        if missing_edges:
            errors.append("缺少 TF 边：" + ",".join(f"{parent}->{child}" for parent, child in sorted(missing_edges)))
        if first_base_position is None or last_base_position is None:
            errors.append("未收到 map->base_link 位姿")
        elif max_base_delta < args.min_base_delta:
            errors.append(f"base_link 最大运动距离 {max_base_delta:.3f}m，期望至少 {args.min_base_delta:.3f}m")
        if args.max_base_delta is not None and max_base_delta > args.max_base_delta:
            errors.append(f"base_link 最大运动距离 {max_base_delta:.3f}m，期望不超过 {args.max_base_delta:.3f}m")

        if errors:
            print("车辆 TF 校验失败：", file=sys.stderr)
            for error in errors:
                print(f"- {error}", file=sys.stderr)
            return 1

        print("VLN_VEHICLE_TF_MSG_OK")
        return 0
    finally:
        node.destroy_subscription(subscription)
        node.destroy_node()
        if rclpy.ok():
            rclpy.shutdown()


if __name__ == "__main__":
    raise SystemExit(main())
