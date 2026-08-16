#!/usr/bin/env python3
"""发布 /vln/cmd_vel，并用 /tf 验证 Unity 车体响应速度指令。"""

import argparse
import math
import sys
import time

import rclpy
from geometry_msgs.msg import Twist
from nav_msgs.msg import Odometry
from tf2_msgs.msg import TFMessage


def parse_args():
    parser = argparse.ArgumentParser(description="发送 geometry_msgs/msg/Twist 并校验 base_link 响应")
    parser.add_argument("--cmd-topic", default="/vln/cmd_vel")
    parser.add_argument("--tf-topic", default="/tf")
    parser.add_argument("--linear-x", type=float, default=0.8)
    parser.add_argument("--angular-z", type=float, default=0.7)
    parser.add_argument("--duration", type=float, default=4.0)
    parser.add_argument("--publish-rate", type=float, default=10.0)
    parser.add_argument("--timeout", type=float, default=60.0)
    parser.add_argument("--min-delta", type=float, default=1.0)
    parser.add_argument("--min-forward-delta", type=float, default=None, help="可选：要求 ROS map 坐标系 x 方向前向位移至少达到该值")
    parser.add_argument("--min-yaw-delta", type=float, default=0.7)
    parser.add_argument("--odom-topic", default="", help="可选：同时订阅 nav_msgs/msg/Odometry 并验证运动")
    parser.add_argument("--min-odom-delta", type=float, default=0.0)
    parser.add_argument("--min-odom-forward-delta", type=float, default=None, help="可选：要求 odom pose x 方向前向位移至少达到该值")
    parser.add_argument("--min-odom-yaw-delta", type=float, default=0.0)
    return parser.parse_args()


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


def distance(a, b):
    return math.sqrt((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2)


def odom_position_and_yaw(msg):
    position = msg.pose.pose.position
    return (position.x, position.y, position.z), quaternion_yaw(msg.pose.pose.orientation)


def main():
    args = parse_args()
    deadline = time.monotonic() + args.timeout
    pose = {"position": None, "yaw": None, "message_count": 0}
    odom = {"position": None, "yaw": None, "message_count": 0}

    rclpy.init()
    node = rclpy.create_node("vln_send_cmd_vel_and_wait_tf")
    publisher = node.create_publisher(Twist, args.cmd_topic, 10)

    def on_tf(msg):
        pose["message_count"] += 1
        for transform in msg.transforms:
            if transform.header.frame_id == "map" and transform.child_frame_id == "base_link":
                translation = transform.transform.translation
                pose["position"] = (translation.x, translation.y, translation.z)
                pose["yaw"] = quaternion_yaw(transform.transform.rotation)

    subscription = node.create_subscription(TFMessage, args.tf_topic, on_tf, 20)
    odom_subscription = None

    if args.odom_topic:
        def on_odom(msg):
            odom["message_count"] += 1
            if msg.header.frame_id == "map" and msg.child_frame_id == "base_link":
                odom["position"], odom["yaw"] = odom_position_and_yaw(msg)

        odom_subscription = node.create_subscription(Odometry, args.odom_topic, on_odom, 20)

    try:
        while time.monotonic() < deadline and (pose["position"] is None or pose["yaw"] is None):
            rclpy.spin_once(node, timeout_sec=0.1)

        if pose["position"] is None or pose["yaw"] is None:
            print(f"未在 {args.timeout:.1f}s 内收到 map->base_link TF", file=sys.stderr)
            return 1

        if args.odom_topic:
            while time.monotonic() < deadline and (odom["position"] is None or odom["yaw"] is None):
                rclpy.spin_once(node, timeout_sec=0.1)

            if odom["position"] is None or odom["yaw"] is None:
                print(f"未在 {args.timeout:.1f}s 内收到 map/base_link odom：{args.odom_topic}", file=sys.stderr)
                return 1

        start_position = pose["position"]
        start_yaw = pose["yaw"]
        start_odom_position = odom["position"]
        start_odom_yaw = odom["yaw"]
        command = Twist()
        command.linear.x = float(args.linear_x)
        command.angular.z = float(args.angular_z)
        zero = Twist()
        publish_period = 1.0 / max(1.0, args.publish_rate)
        publish_count = 0
        command_end = time.monotonic() + args.duration

        while time.monotonic() < command_end and time.monotonic() < deadline:
            publisher.publish(command)
            publish_count += 1
            rclpy.spin_once(node, timeout_sec=0.02)
            time.sleep(publish_period)

        for _ in range(8):
            publisher.publish(zero)
            publish_count += 1
            rclpy.spin_once(node, timeout_sec=0.05)
            time.sleep(0.05)

        settle_end = time.monotonic() + 1.0
        while time.monotonic() < settle_end:
            rclpy.spin_once(node, timeout_sec=0.1)

        end_position = pose["position"]
        end_yaw = pose["yaw"]
        base_delta = distance(start_position, end_position)
        forward_delta = end_position[0] - start_position[0]
        yaw_delta = abs(normalize_angle(end_yaw - start_yaw))
        odom_delta = 0.0
        odom_forward_delta = 0.0
        odom_yaw_delta = 0.0
        if args.odom_topic and odom["position"] is not None and odom["yaw"] is not None:
            odom_delta = distance(start_odom_position, odom["position"])
            odom_forward_delta = odom["position"][0] - start_odom_position[0]
            odom_yaw_delta = abs(normalize_angle(odom["yaw"] - start_odom_yaw))

        print(f"cmd_topic={args.cmd_topic}")
        print(f"tf_topic={args.tf_topic}")
        print(f"linear_x={args.linear_x:.3f}")
        print(f"angular_z={args.angular_z:.3f}")
        print(f"duration={args.duration:.3f}")
        print(f"publish_count={publish_count}")
        print(f"tf_message_count={pose['message_count']}")
        print("start_position=" + ",".join(f"{value:.3f}" for value in start_position))
        print("end_position=" + ",".join(f"{value:.3f}" for value in end_position))
        print(f"base_delta={base_delta:.3f}")
        print(f"forward_delta={forward_delta:.3f}")
        print(f"start_yaw={start_yaw:.3f}")
        print(f"end_yaw={end_yaw:.3f}")
        print(f"yaw_delta={yaw_delta:.3f}")
        if args.odom_topic:
            print(f"odom_topic={args.odom_topic}")
            print(f"odom_message_count={odom['message_count']}")
            print("start_odom_position=" + ",".join(f"{value:.3f}" for value in start_odom_position))
            print("end_odom_position=" + ",".join(f"{value:.3f}" for value in odom["position"]))
            print(f"odom_delta={odom_delta:.3f}")
            print(f"odom_forward_delta={odom_forward_delta:.3f}")
            print(f"start_odom_yaw={start_odom_yaw:.3f}")
            print(f"end_odom_yaw={odom['yaw']:.3f}")
            print(f"odom_yaw_delta={odom_yaw_delta:.3f}")

        errors = []
        if publish_count <= 0:
            errors.append("没有发布任何 cmd_vel")
        if base_delta < args.min_delta:
            errors.append(f"base_link 位移 {base_delta:.3f}m，小于期望 {args.min_delta:.3f}m")
        if args.min_forward_delta is not None and forward_delta < args.min_forward_delta:
            errors.append(f"base_link 前向位移 {forward_delta:.3f}m，小于期望 {args.min_forward_delta:.3f}m")
        if yaw_delta < args.min_yaw_delta:
            errors.append(f"base_link yaw 变化 {yaw_delta:.3f}rad，小于期望 {args.min_yaw_delta:.3f}rad")
        if args.odom_topic and odom_delta < args.min_odom_delta:
            errors.append(f"odom 位移 {odom_delta:.3f}m，小于期望 {args.min_odom_delta:.3f}m")
        if args.odom_topic and args.min_odom_forward_delta is not None and odom_forward_delta < args.min_odom_forward_delta:
            errors.append(f"odom 前向位移 {odom_forward_delta:.3f}m，小于期望 {args.min_odom_forward_delta:.3f}m")
        if args.odom_topic and odom_yaw_delta < args.min_odom_yaw_delta:
            errors.append(f"odom yaw 变化 {odom_yaw_delta:.3f}rad，小于期望 {args.min_odom_yaw_delta:.3f}rad")

        if errors:
            print("cmd_vel 控制校验失败：", file=sys.stderr)
            for error in errors:
                print(f"- {error}", file=sys.stderr)
            return 1

        print("VLN_CMD_VEL_CONTROL_MSG_OK")
        if args.odom_topic:
            print("VLN_ODOM_MOTION_MSG_OK")
        return 0
    finally:
        if odom_subscription is not None:
            node.destroy_subscription(odom_subscription)
        node.destroy_subscription(subscription)
        node.destroy_publisher(publisher)
        node.destroy_node()
        if rclpy.ok():
            rclpy.shutdown()


if __name__ == "__main__":
    raise SystemExit(main())
