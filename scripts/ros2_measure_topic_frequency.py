#!/usr/bin/env python3
"""Measure ROS2 topic frequency for image / pointcloud smoke tests."""

import argparse
import sys
import time

import rclpy
from sensor_msgs.msg import Image, PointCloud2


def parse_args():
    parser = argparse.ArgumentParser(description="Measure ROS2 topic frequency over a short window.")
    parser.add_argument("--topic", required=True)
    parser.add_argument("--msg-type", choices=("image", "pointcloud2"), required=True)
    parser.add_argument("--duration", type=float, default=8.0)
    parser.add_argument("--timeout", type=float, default=60.0)
    parser.add_argument("--min-hz", type=float, default=0.0)
    parser.add_argument("--frame-id", default="")
    return parser.parse_args()


def main():
    args = parse_args()
    message_type = Image if args.msg_type == "image" else PointCloud2
    deadline = time.monotonic() + args.timeout
    state = {
        "count": 0,
        "first_time": None,
        "last_time": None,
        "first_stamp": None,
        "last_stamp": None,
        "frame_id": "",
    }

    rclpy.init()
    node = rclpy.create_node("vln_measure_topic_frequency")

    def on_msg(msg):
        now = time.monotonic()
        if state["first_time"] is None:
            state["first_time"] = now
            state["first_stamp"] = f"{msg.header.stamp.sec}.{msg.header.stamp.nanosec:09d}"
        state["last_time"] = now
        state["last_stamp"] = f"{msg.header.stamp.sec}.{msg.header.stamp.nanosec:09d}"
        state["frame_id"] = msg.header.frame_id
        state["count"] += 1

    subscription = node.create_subscription(message_type, args.topic, on_msg, 10)
    try:
        while state["first_time"] is None and time.monotonic() < deadline:
            rclpy.spin_once(node, timeout_sec=0.2)

        if state["first_time"] is None:
            print(f"topic={args.topic}")
            print("status=no_messages")
            print(f"timeout_s={args.timeout:.3f}")
            return 1

        measure_deadline = state["first_time"] + max(0.5, args.duration)
        while time.monotonic() < measure_deadline:
            rclpy.spin_once(node, timeout_sec=0.1)

        first = state["first_time"]
        last = state["last_time"] or first
        elapsed = max(0.0, last - first)
        count = state["count"]
        average_hz = (count - 1) / elapsed if count > 1 and elapsed > 0.0 else 0.0

        print(f"topic={args.topic}")
        print(f"msg_type={args.msg_type}")
        print(f"frame_id={state['frame_id']}")
        print(f"message_count={count}")
        print(f"elapsed_s={elapsed:.3f}")
        print(f"average_hz={average_hz:.3f}")
        print(f"min_required_hz={args.min_hz:.3f}")
        print(f"first_stamp={state['first_stamp']}")
        print(f"last_stamp={state['last_stamp']}")

        errors = []
        if args.frame_id and state["frame_id"] != args.frame_id:
            errors.append(f"frame_id={state['frame_id']} expected={args.frame_id}")
        if average_hz + 1e-6 < args.min_hz:
            errors.append(f"average_hz={average_hz:.3f} below_min={args.min_hz:.3f}")

        if errors:
            print("status=failed")
            for error in errors:
                print("error=" + error, file=sys.stderr)
            return 2

        print("status=ok")
        print("VLN_ROS2_TOPIC_FREQUENCY_OK")
        return 0
    finally:
        node.destroy_subscription(subscription)
        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    raise SystemExit(main())
