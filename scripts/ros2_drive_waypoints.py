#!/usr/bin/env python3
"""基于 /tf 的轻量路径点控制器：读取 base_link，发布 /vln/cmd_vel。"""

import argparse
import math
import sys
import time

import rclpy
from geometry_msgs.msg import Twist
from tf2_msgs.msg import TFMessage


def parse_waypoints(text):
    waypoints = []
    for item in text.split(";"):
        item = item.strip()
        if not item:
            continue
        parts = [part.strip() for part in item.split(",")]
        if len(parts) != 2:
            raise ValueError(f"路径点格式错误：{item}，应为 x,y")
        waypoints.append((float(parts[0]), float(parts[1])))
    if not waypoints:
        raise ValueError("至少需要一个路径点")
    return waypoints


def parse_args():
    parser = argparse.ArgumentParser(description="用 /tf 闭环驱动 Unity 车体通过相对路径点")
    parser.add_argument("--cmd-topic", default="/vln/cmd_vel")
    parser.add_argument("--tf-topic", default="/tf")
    parser.add_argument(
        "--relative-waypoints",
        default="1.2,0.0;2.4,0.0",
        help="以启动时 base_link 为原点的相对路径点，格式：x,y;x,y。x 为前向米，y 为左向米。",
    )
    parser.add_argument("--timeout", type=float, default=70.0)
    parser.add_argument("--goal-tolerance", type=float, default=0.35)
    parser.add_argument("--max-linear", type=float, default=0.9)
    parser.add_argument("--max-angular", type=float, default=0.9)
    parser.add_argument("--linear-gain", type=float, default=0.65)
    parser.add_argument("--angular-gain", type=float, default=1.4)
    parser.add_argument("--publish-rate", type=float, default=10.0)
    parser.add_argument("--min-total-progress", type=float, default=1.4)
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


def distance_2d(a, b):
    return math.hypot(a[0] - b[0], a[1] - b[1])


def clamp(value, low, high):
    return max(low, min(high, value))


def local_to_map(start_xy, start_yaw, local_xy):
    lx, ly = local_xy
    cos_yaw = math.cos(start_yaw)
    sin_yaw = math.sin(start_yaw)
    return (
        start_xy[0] + lx * cos_yaw - ly * sin_yaw,
        start_xy[1] + lx * sin_yaw + ly * cos_yaw,
    )


def main():
    args = parse_args()
    relative_waypoints = parse_waypoints(args.relative_waypoints)
    deadline = time.monotonic() + args.timeout
    pose = {"xy": None, "z": None, "yaw": None, "message_count": 0}

    rclpy.init()
    node = rclpy.create_node("vln_drive_waypoints")
    publisher = node.create_publisher(Twist, args.cmd_topic, 10)

    def on_tf(msg):
        pose["message_count"] += 1
        for transform in msg.transforms:
            if transform.header.frame_id == "map" and transform.child_frame_id == "base_link":
                translation = transform.transform.translation
                pose["xy"] = (translation.x, translation.y)
                pose["z"] = translation.z
                pose["yaw"] = quaternion_yaw(transform.transform.rotation)

    subscription = node.create_subscription(TFMessage, args.tf_topic, on_tf, 20)

    try:
        while time.monotonic() < deadline and (pose["xy"] is None or pose["yaw"] is None):
            rclpy.spin_once(node, timeout_sec=0.1)

        if pose["xy"] is None or pose["yaw"] is None:
            print(f"未在 {args.timeout:.1f}s 内收到 map->base_link TF", file=sys.stderr)
            return 1

        start_xy = pose["xy"]
        start_yaw = pose["yaw"]
        map_waypoints = [local_to_map(start_xy, start_yaw, waypoint) for waypoint in relative_waypoints]
        reached = []
        current_index = 0
        publish_count = 0
        rate_period = 1.0 / max(1.0, args.publish_rate)

        print("relative_waypoints=" + ";".join(f"{x:.3f},{y:.3f}" for x, y in relative_waypoints))
        print("map_waypoints=" + ";".join(f"{x:.3f},{y:.3f}" for x, y in map_waypoints))
        print(f"start_xy={start_xy[0]:.3f},{start_xy[1]:.3f}")
        print(f"start_yaw={start_yaw:.3f}")

        while time.monotonic() < deadline and current_index < len(map_waypoints):
            rclpy.spin_once(node, timeout_sec=0.02)
            if pose["xy"] is None or pose["yaw"] is None:
                time.sleep(rate_period)
                continue

            target = map_waypoints[current_index]
            dx = target[0] - pose["xy"][0]
            dy = target[1] - pose["xy"][1]
            dist = math.hypot(dx, dy)

            if dist <= args.goal_tolerance:
                reached.append((current_index + 1, pose["xy"], dist))
                current_index += 1
                continue

            target_heading = math.atan2(dy, dx)
            heading_error = normalize_angle(target_heading - pose["yaw"])
            command = Twist()
            command.linear.x = clamp(args.linear_gain * dist, 0.15, args.max_linear)
            if abs(heading_error) > 1.1:
                command.linear.x = 0.0

            # 当前 Unity 轻量运动学中，正 angular.z 会让发布到 TF 的 yaw 变小，因此这里取反。
            command.angular.z = clamp(-args.angular_gain * heading_error, -args.max_angular, args.max_angular)
            publisher.publish(command)
            publish_count += 1
            time.sleep(rate_period)

        zero = Twist()
        for _ in range(8):
            publisher.publish(zero)
            publish_count += 1
            rclpy.spin_once(node, timeout_sec=0.05)
            time.sleep(0.05)

        settle_end = time.monotonic() + 0.8
        while time.monotonic() < settle_end:
            rclpy.spin_once(node, timeout_sec=0.1)

        final_xy = pose["xy"]
        final_yaw = pose["yaw"]
        final_target = map_waypoints[-1]
        final_error = distance_2d(final_xy, final_target)
        total_progress = distance_2d(start_xy, final_xy)

        print(f"cmd_topic={args.cmd_topic}")
        print(f"tf_topic={args.tf_topic}")
        print(f"publish_count={publish_count}")
        print(f"tf_message_count={pose['message_count']}")
        print(f"reached_count={len(reached)}")
        for index, xy, dist in reached:
            print(f"reached_waypoint_{index}=xy:{xy[0]:.3f},{xy[1]:.3f};remaining:{dist:.3f}")
        print(f"final_xy={final_xy[0]:.3f},{final_xy[1]:.3f}")
        print(f"final_yaw={final_yaw:.3f}")
        print(f"final_error={final_error:.3f}")
        print(f"total_progress={total_progress:.3f}")

        errors = []
        if current_index < len(map_waypoints):
            errors.append(f"只到达 {current_index}/{len(map_waypoints)} 个路径点")
        if final_error > args.goal_tolerance * 1.6:
            errors.append(f"最终距离最后路径点 {final_error:.3f}m，超过阈值 {args.goal_tolerance * 1.6:.3f}m")
        if total_progress < args.min_total_progress:
            errors.append(f"总位移 {total_progress:.3f}m，小于期望 {args.min_total_progress:.3f}m")
        if publish_count <= 0:
            errors.append("没有发布任何 cmd_vel")

        if errors:
            print("路径点控制校验失败：", file=sys.stderr)
            for error in errors:
                print(f"- {error}", file=sys.stderr)
            return 1

        print("VLN_WAYPOINT_CONTROL_MSG_OK")
        return 0
    finally:
        node.destroy_subscription(subscription)
        node.destroy_publisher(publisher)
        node.destroy_node()
        if rclpy.ok():
            rclpy.shutdown()


if __name__ == "__main__":
    raise SystemExit(main())
