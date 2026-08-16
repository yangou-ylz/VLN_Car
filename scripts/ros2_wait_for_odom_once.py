#!/usr/bin/env python3
"""等待一个 ROS2 Odometry 消息，并验证最小字段。"""

import argparse
import math
import sys
import time

import rclpy
from nav_msgs.msg import Odometry


def parse_args():
    parser = argparse.ArgumentParser(description="等待并校验 nav_msgs/msg/Odometry")
    parser.add_argument("--topic", default="/vln/odom")
    parser.add_argument("--frame-id", default="map")
    parser.add_argument("--child-frame-id", default="base_link")
    parser.add_argument("--timeout", type=float, default=60.0)
    return parser.parse_args()


def quaternion_norm(q):
    return math.sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w)


def finite_values(values):
    return all(math.isfinite(value) for value in values)


def main():
    args = parse_args()
    deadline = time.monotonic() + args.timeout
    result = {"msg": None}

    rclpy.init()
    node = rclpy.create_node("vln_wait_for_odom_once")

    def on_odom(msg):
        result["msg"] = msg

    subscription = node.create_subscription(Odometry, args.topic, on_odom, 10)
    try:
        while result["msg"] is None and time.monotonic() < deadline:
            rclpy.spin_once(node, timeout_sec=0.25)

        if result["msg"] is None:
            print(f"未在 {args.timeout:.1f}s 内收到 odom topic：{args.topic}", file=sys.stderr)
            return 1

        msg = result["msg"]
        position = msg.pose.pose.position
        orientation = msg.pose.pose.orientation
        linear = msg.twist.twist.linear
        angular = msg.twist.twist.angular
        q_norm = quaternion_norm(orientation)

        errors = []
        if msg.header.frame_id != args.frame_id:
            errors.append(f"frame_id={msg.header.frame_id}，期望 {args.frame_id}")
        if msg.child_frame_id != args.child_frame_id:
            errors.append(f"child_frame_id={msg.child_frame_id}，期望 {args.child_frame_id}")
        if len(msg.pose.covariance) != 36:
            errors.append(f"pose_covariance_len={len(msg.pose.covariance)}，期望 36")
        if len(msg.twist.covariance) != 36:
            errors.append(f"twist_covariance_len={len(msg.twist.covariance)}，期望 36")
        if not finite_values((position.x, position.y, position.z, orientation.x, orientation.y, orientation.z, orientation.w)):
            errors.append("pose 中存在非有限数值")
        if not finite_values((linear.x, linear.y, linear.z, angular.x, angular.y, angular.z)):
            errors.append("twist 中存在非有限数值")
        if not (0.95 <= q_norm <= 1.05):
            errors.append(f"orientation quaternion norm={q_norm:.6f}，不接近 1")

        print(f"topic={args.topic}")
        print(f"stamp={msg.header.stamp.sec}.{msg.header.stamp.nanosec:09d}")
        print(f"frame_id={msg.header.frame_id}")
        print(f"child_frame_id={msg.child_frame_id}")
        print(f"position={position.x:.3f},{position.y:.3f},{position.z:.3f}")
        print(f"orientation={orientation.x:.6f},{orientation.y:.6f},{orientation.z:.6f},{orientation.w:.6f}")
        print(f"quaternion_norm={q_norm:.6f}")
        print(f"linear={linear.x:.3f},{linear.y:.3f},{linear.z:.3f}")
        print(f"angular={angular.x:.3f},{angular.y:.3f},{angular.z:.3f}")

        if errors:
            print("odom 消息字段校验失败：", file=sys.stderr)
            for error in errors:
                print(f"- {error}", file=sys.stderr)
            return 1

        print("VLN_ODOM_MSG_OK")
        return 0
    finally:
        node.destroy_subscription(subscription)
        node.destroy_node()
        if rclpy.ok():
            rclpy.shutdown()


if __name__ == "__main__":
    raise SystemExit(main())
