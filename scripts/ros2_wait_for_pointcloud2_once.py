#!/usr/bin/env python3
"""等待一个 ROS2 PointCloud2 消息，并验证 LiDAR 闭环的最小字段。"""

import argparse
import math
import struct
import sys
import time

import rclpy
from sensor_msgs.msg import PointCloud2


def parse_args():
    parser = argparse.ArgumentParser(description="等待并校验 sensor_msgs/msg/PointCloud2")
    parser.add_argument("--topic", required=True)
    parser.add_argument("--width", type=int, required=True)
    parser.add_argument("--point-step", type=int, required=True)
    parser.add_argument("--frame-id", required=True)
    parser.add_argument("--timeout", type=float, default=60.0)
    parser.add_argument("--min-nonzero-points", type=int, default=20)
    return parser.parse_args()


def count_nonzero_points(msg):
    field_offsets = {field.name: field.offset for field in msg.fields}
    required = ["x", "y", "z", "intensity"]
    if any(name not in field_offsets for name in required):
        return 0

    count = 0
    data = bytes(msg.data)
    total_points = msg.width * max(msg.height, 1)
    for index in range(total_points):
        base = index * msg.point_step
        try:
            x = struct.unpack_from("<f", data, base + field_offsets["x"])[0]
            y = struct.unpack_from("<f", data, base + field_offsets["y"])[0]
            z = struct.unpack_from("<f", data, base + field_offsets["z"])[0]
            intensity = struct.unpack_from("<f", data, base + field_offsets["intensity"])[0]
        except struct.error:
            break

        if all(math.isfinite(value) for value in (x, y, z, intensity)) and (abs(x) + abs(y) + abs(z)) > 1e-4:
            count += 1
    return count


def main():
    args = parse_args()
    deadline = time.monotonic() + args.timeout
    result = {"msg": None}

    rclpy.init()
    node = rclpy.create_node("vln_wait_for_pointcloud2_once")

    def on_cloud(msg):
        result["msg"] = msg

    subscription = node.create_subscription(PointCloud2, args.topic, on_cloud, 10)
    try:
        while result["msg"] is None and time.monotonic() < deadline:
            rclpy.spin_once(node, timeout_sec=0.25)

        if result["msg"] is None:
            print(f"未在 {args.timeout:.1f}s 内收到点云 topic：{args.topic}", file=sys.stderr)
            return 1

        msg = result["msg"]
        field_names = [field.name for field in msg.fields]
        expected_data_len = msg.row_step * msg.height
        nonzero_points = count_nonzero_points(msg)

        errors = []
        if msg.header.frame_id != args.frame_id:
            errors.append(f"frame_id={msg.header.frame_id}，期望 {args.frame_id}")
        if msg.height != 1:
            errors.append(f"height={msg.height}，期望 1")
        if msg.width != args.width:
            errors.append(f"width={msg.width}，期望 {args.width}")
        if msg.point_step != args.point_step:
            errors.append(f"point_step={msg.point_step}，期望 {args.point_step}")
        if msg.row_step != msg.point_step * msg.width:
            errors.append(f"row_step={msg.row_step}，期望 {msg.point_step * msg.width}")
        if len(msg.data) != expected_data_len:
            errors.append(f"data_len={len(msg.data)}，期望 {expected_data_len}")
        for field_name in ["x", "y", "z", "intensity"]:
            if field_name not in field_names:
                errors.append(f"缺少字段 {field_name}")
        if nonzero_points < args.min_nonzero_points:
            errors.append(f"nonzero_points={nonzero_points}，期望至少 {args.min_nonzero_points}")

        print(f"topic={args.topic}")
        print(f"stamp={msg.header.stamp.sec}.{msg.header.stamp.nanosec:09d}")
        print(f"frame_id={msg.header.frame_id}")
        print(f"height={msg.height}")
        print(f"width={msg.width}")
        print(f"fields={','.join(field_names)}")
        print(f"point_step={msg.point_step}")
        print(f"row_step={msg.row_step}")
        print(f"data_len={len(msg.data)}")
        print(f"nonzero_points={nonzero_points}")

        if errors:
            print("点云消息字段校验失败：", file=sys.stderr)
            for error in errors:
                print(f"- {error}", file=sys.stderr)
            return 1

        print("VLN_UNITYSENSORS_POINTCLOUD2_MSG_OK")
        return 0
    finally:
        node.destroy_subscription(subscription)
        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    raise SystemExit(main())
