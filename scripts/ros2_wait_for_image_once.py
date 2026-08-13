#!/usr/bin/env python3
"""等待一个 ROS2 Image 消息，并验证相机闭环的最小字段。"""

import argparse
import sys
import time

import rclpy
from sensor_msgs.msg import Image


def parse_args():
    parser = argparse.ArgumentParser(description="等待并校验 sensor_msgs/msg/Image")
    parser.add_argument("--topic", required=True)
    parser.add_argument("--width", type=int, required=True)
    parser.add_argument("--height", type=int, required=True)
    parser.add_argument("--encoding", required=True)
    parser.add_argument("--frame-id", required=True)
    parser.add_argument("--timeout", type=float, default=60.0)
    return parser.parse_args()


def main():
    args = parse_args()
    deadline = time.monotonic() + args.timeout
    result = {"msg": None}

    rclpy.init()
    node = rclpy.create_node("vln_wait_for_image_once")

    def on_image(msg):
        result["msg"] = msg

    subscription = node.create_subscription(Image, args.topic, on_image, 10)
    try:
        while result["msg"] is None and time.monotonic() < deadline:
            rclpy.spin_once(node, timeout_sec=0.25)

        if result["msg"] is None:
            print(f"未在 {args.timeout:.1f}s 内收到图像 topic：{args.topic}", file=sys.stderr)
            return 1

        msg = result["msg"]
        expected_step = args.width * 3 if args.encoding == "rgb8" else msg.step
        expected_data_len = msg.height * msg.step

        errors = []
        if msg.width != args.width:
            errors.append(f"width={msg.width}，期望 {args.width}")
        if msg.height != args.height:
            errors.append(f"height={msg.height}，期望 {args.height}")
        if msg.encoding != args.encoding:
            errors.append(f"encoding={msg.encoding}，期望 {args.encoding}")
        if msg.header.frame_id != args.frame_id:
            errors.append(f"frame_id={msg.header.frame_id}，期望 {args.frame_id}")
        if msg.step != expected_step:
            errors.append(f"step={msg.step}，期望 {expected_step}")
        if len(msg.data) != expected_data_len:
            errors.append(f"data_len={len(msg.data)}，期望 {expected_data_len}")

        print(f"topic={args.topic}")
        print(f"stamp={msg.header.stamp.sec}.{msg.header.stamp.nanosec:09d}")
        print(f"frame_id={msg.header.frame_id}")
        print(f"width={msg.width}")
        print(f"height={msg.height}")
        print(f"encoding={msg.encoding}")
        print(f"step={msg.step}")
        print(f"data_len={len(msg.data)}")

        if errors:
            print("图像消息字段校验失败：", file=sys.stderr)
            for error in errors:
                print(f"- {error}", file=sys.stderr)
            return 1

        print("VLN_UNITYSENSORS_IMAGE_MSG_OK")
        return 0
    finally:
        node.destroy_subscription(subscription)
        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    raise SystemExit(main())
