#!/usr/bin/env python3
"""等待阶段 7 车辆 TF，并验证 frame 关系与 base_link 运动。"""

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
    parser.add_argument("--min-base-delta", type=float, default=0.25)
    return parser.parse_args()


def norm_delta(a, b):
    return math.sqrt((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2)


def main():
    args = parse_args()
    deadline = time.monotonic() + args.timeout
    required_edges = {
        ("map", "base_link"),
        ("base_link", "front_camera_optical_frame"),
        ("base_link", "lidar_link"),
    }
    seen_edges = set()
    first_base_position = None
    last_base_position = None
    message_count = 0

    rclpy.init()
    node = rclpy.create_node("vln_wait_for_vehicle_tf")

    def on_tf(msg):
        nonlocal first_base_position, last_base_position, message_count
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

    subscription = node.create_subscription(TFMessage, args.topic, on_tf, 20)

    try:
        while time.monotonic() < deadline:
            rclpy.spin_once(node, timeout_sec=0.25)
            if seen_edges == required_edges and first_base_position is not None and last_base_position is not None:
                if norm_delta(first_base_position, last_base_position) >= args.min_base_delta:
                    break

        missing_edges = required_edges - seen_edges
        base_delta = 0.0
        if first_base_position is not None and last_base_position is not None:
            base_delta = norm_delta(first_base_position, last_base_position)

        print(f"topic={args.topic}")
        print(f"message_count={message_count}")
        print("seen_edges=" + ",".join(f"{parent}->{child}" for parent, child in sorted(seen_edges)))
        print(f"base_delta={base_delta:.3f}")
        if first_base_position is not None:
            print("first_base_position=" + ",".join(f"{value:.3f}" for value in first_base_position))
        if last_base_position is not None:
            print("last_base_position=" + ",".join(f"{value:.3f}" for value in last_base_position))

        errors = []
        if missing_edges:
            errors.append("缺少 TF 边：" + ",".join(f"{parent}->{child}" for parent, child in sorted(missing_edges)))
        if first_base_position is None or last_base_position is None:
            errors.append("未收到 map->base_link 位姿")
        elif base_delta < args.min_base_delta:
            errors.append(f"base_link 运动距离 {base_delta:.3f}m，期望至少 {args.min_base_delta:.3f}m")

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
