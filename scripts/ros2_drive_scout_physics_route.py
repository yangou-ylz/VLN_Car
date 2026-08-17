#!/usr/bin/env python3
"""Scout wheel-ground 固定路线演示：读取 /tf，发布 /vln/cmd_vel。"""

import argparse
import math
import os
import sys
import time

import rclpy
from geometry_msgs.msg import Twist
from nav_msgs.msg import Odometry
from tf2_msgs.msg import TFMessage


DEFAULT_ROUTE = "4.0,0.0;8.0,0.0;12.0,0.0;15.0,0.0;18.0,0.0;22.0,0.0;26.0,0.0;28.0,0.0;30.0,0.0;34.0,0.0;42.0,0.0;50.0,0.0;54.0,0.0"


def parse_waypoints(text):
    waypoints = []
    for item in text.split(";"):
        item = item.strip()
        if not item:
            continue
        parts = [part.strip() for part in item.split(",")]
        if len(parts) != 2:
            raise ValueError(f"路径点格式错误：{item}，应为 forward,left")
        waypoints.append((float(parts[0]), float(parts[1])))
    if not waypoints:
        raise ValueError("至少需要一个路径点")
    return waypoints


def parse_args():
    parser = argparse.ArgumentParser(description="让 Scout wheel-ground 候选车体自动走一段越野物理测试路线")
    parser.add_argument("--cmd-topic", default="/vln/cmd_vel")
    parser.add_argument("--tf-topic", default="/tf")
    parser.add_argument("--odom-topic", default="/vln/odom")
    parser.add_argument(
        "--relative-waypoints",
        default=DEFAULT_ROUTE,
        help="以启动时 base_link 为原点的路径点，格式：forward,left;forward,left。forward 为前向米，left 为左向米。",
    )
    parser.add_argument("--timeout", type=float, default=180.0)
    parser.add_argument("--goal-tolerance", type=float, default=2.00)
    parser.add_argument("--gate-tolerance", type=float, default=2.50, help="车辆已经越过路径点时允许的横向偏差。")
    parser.add_argument(
        "--progress-only-gates",
        action="store_true",
        help="按启动坐标系的前向进度切换路径点；适合第一版 wheel-ground 物理候选，避免横向漂移后死追已越过的旧路径点。",
    )
    parser.add_argument(
        "--centerline-corridor",
        action="store_true",
        help="沿相对路径形成的中心线走廊巡航，用前视点和横向误差纠偏，减少物理车体在窄桥前后走 S 型。",
    )
    parser.add_argument("--lookahead-distance", type=float, default=4.0, help="中心线走廊模式下的前视距离，单位米。")
    parser.add_argument("--corridor-lateral-gain", type=float, default=0.32, help="中心线走廊模式的横向误差纠偏增益。")
    parser.add_argument("--corridor-max-heading-correction", type=float, default=0.42, help="中心线走廊模式允许的最大额外航向修正，单位弧度。")
    parser.add_argument("--centerline-forward-max", type=float, default=None, help="中心线走廊模式的前向截止位置；超过该位置后切回普通路径点追踪。")
    parser.add_argument("--max-lateral-offset", type=float, default=None, help="全程相对中心线最大横向偏差阈值，超过则判失败。")
    parser.add_argument("--max-final-lateral-offset", type=float, default=None, help="终点相对中心线横向偏差阈值，超过则判失败。")
    parser.add_argument("--max-bridge-lateral-offset", type=float, default=None, help="桥区相对中心线最大横向偏差阈值，超过则判失败。")
    parser.add_argument("--bridge-forward-min", type=float, default=9.5, help="桥区审计前向起点，基于启动时 base_link 坐标系。")
    parser.add_argument("--bridge-forward-max", type=float, default=22.8, help="桥区审计前向终点，基于启动时 base_link 坐标系。")
    parser.add_argument("--max-linear", type=float, default=0.95)
    parser.add_argument("--max-angular", type=float, default=0.50)
    parser.add_argument("--linear-gain", type=float, default=0.62)
    parser.add_argument("--angular-gain", type=float, default=0.75)
    parser.add_argument("--angular-bias", type=float, default=0.0, help="固定角速度偏置，负值会让当前 wheel-ground 候选略向左侧安全路廊巡航。")
    parser.add_argument("--min-linear-while-turning", type=float, default=0.40, help="朝向误差较大时仍保持的低速前进速度，避免 skid-steer 车辆原地卡住。")
    parser.add_argument("--publish-rate", type=float, default=20.0)
    parser.add_argument("--linear-accel", type=float, default=0.50)
    parser.add_argument("--angular-accel", type=float, default=0.32)
    parser.add_argument("--min-reached", type=int, default=11)
    parser.add_argument("--min-total-progress", type=float, default=44.0)
    parser.add_argument(
        "--skip-stalled-waypoints",
        action="store_true",
        help="仅用于排障观察：记录卡点并切到下一个路径点。正式验收中任何 skip 都会判失败。",
    )
    parser.add_argument("--stall-skip-seconds", type=float, default=12.0)
    parser.add_argument("--stall-skip-forward-margin", type=float, default=4.0)
    parser.add_argument("--stall-progress-threshold", type=float, default=0.35)
    parser.add_argument("--status-period", type=float, default=2.0)
    parser.add_argument("--angular-sign", type=float, default=1.0, choices=(-1.0, 1.0), help="正角速度对 ROS yaw 的影响方向。Scout wheel-ground 候选当前实测默认为 1。")
    parser.add_argument("--auto-angular-sign", action="store_true", help="先短暂原地转向，自动判断 angular.z 符号。")
    parser.add_argument("--skip-angular-calibration", action="store_true", help="跳过符号校准，直接使用 --angular-sign。")
    parser.add_argument(
        "--result-file",
        default="/home/ubuntu22/VLN/UnityProjects/VLN_Offroad/Logs/vln_scout_physics_route_demo_result.txt",
    )
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


def move_towards(current, target, max_delta):
    if abs(target - current) <= max_delta:
        return target
    return current + math.copysign(max_delta, target - current)


def local_to_map(start_xy, start_yaw, local_xy):
    forward, left = local_xy
    cos_yaw = math.cos(start_yaw)
    sin_yaw = math.sin(start_yaw)
    return (
        start_xy[0] + forward * cos_yaw - left * sin_yaw,
        start_xy[1] + forward * sin_yaw + left * cos_yaw,
    )


def forward_progress_from_start(start_xy, start_yaw, point):
    cos_yaw = math.cos(start_yaw)
    sin_yaw = math.sin(start_yaw)
    rel_x = point[0] - start_xy[0]
    rel_y = point[1] - start_xy[1]
    return rel_x * cos_yaw + rel_y * sin_yaw


def lateral_offset_from_start(start_xy, start_yaw, point):
    cos_yaw = math.cos(start_yaw)
    sin_yaw = math.sin(start_yaw)
    rel_x = point[0] - start_xy[0]
    rel_y = point[1] - start_xy[1]
    return -rel_x * sin_yaw + rel_y * cos_yaw


def centerline_segment_at_forward(path_points, forward):
    if len(path_points) < 2:
        return path_points[0], path_points[0]

    if forward <= path_points[0][0]:
        return path_points[0], path_points[1]

    for index in range(len(path_points) - 1):
        a = path_points[index]
        b = path_points[index + 1]
        lo = min(a[0], b[0])
        hi = max(a[0], b[0])
        if lo <= forward <= hi:
            return a, b

    return path_points[-2], path_points[-1]


def centerline_left_at_forward(path_points, forward):
    a, b = centerline_segment_at_forward(path_points, forward)
    span = b[0] - a[0]
    if abs(span) < 1e-6:
        return b[1]
    t = clamp((forward - a[0]) / span, 0.0, 1.0)
    return a[1] + (b[1] - a[1]) * t


def centerline_heading_at_forward(path_points, forward):
    a, b = centerline_segment_at_forward(path_points, forward)
    return math.atan2(b[1] - a[1], b[0] - a[0])


def centerline_lateral_offset(path_points, forward, lateral):
    return lateral - centerline_left_at_forward(path_points, forward)


def segment_progress_and_cross_track(segment_start, segment_end, point):
    seg_x = segment_end[0] - segment_start[0]
    seg_y = segment_end[1] - segment_start[1]
    length = math.hypot(seg_x, seg_y)
    if length < 1e-6:
        return 0.0, distance_2d(point, segment_end), length

    rel_x = point[0] - segment_start[0]
    rel_y = point[1] - segment_start[1]
    progress = (rel_x * seg_x + rel_y * seg_y) / length
    cross_track = abs(rel_x * seg_y - rel_y * seg_x) / length
    return progress, cross_track, length


def wait_for_pose(node, pose, deadline, label):
    while time.monotonic() < deadline and (pose["xy"] is None or pose["yaw"] is None):
        rclpy.spin_once(node, timeout_sec=0.1)
    if pose["xy"] is None or pose["yaw"] is None:
        raise TimeoutError(f"未收到 {label} 位姿")


def publish_for(node, publisher, command, seconds, rate_hz):
    end = time.monotonic() + seconds
    period = 1.0 / max(1.0, rate_hz)
    count = 0
    while time.monotonic() < end:
        publisher.publish(command)
        count += 1
        rclpy.spin_once(node, timeout_sec=0.01)
        time.sleep(period)
    return count


def publish_zero(node, publisher, rate_hz, repeats=12):
    zero = Twist()
    period = 1.0 / max(1.0, rate_hz)
    for _ in range(repeats):
        publisher.publish(zero)
        rclpy.spin_once(node, timeout_sec=0.02)
        time.sleep(period)


def maybe_calibrate_angular_sign(node, publisher, pose, args):
    if args.skip_angular_calibration:
        return args.angular_sign, 0.0, 0
    if not args.auto_angular_sign:
        return args.angular_sign, 0.0, 0

    yaw_before = pose["yaw"]
    command = Twist()
    command.angular.z = 0.35
    publish_count = publish_for(node, publisher, command, 1.0, args.publish_rate)
    publish_zero(node, publisher, args.publish_rate, repeats=8)

    settle_end = time.monotonic() + 0.6
    while time.monotonic() < settle_end:
        rclpy.spin_once(node, timeout_sec=0.05)

    yaw_after = pose["yaw"]
    yaw_delta = normalize_angle(yaw_after - yaw_before)
    if abs(yaw_delta) < 0.02:
        return args.angular_sign, yaw_delta, publish_count
    return (1.0 if yaw_delta > 0.0 else -1.0), yaw_delta, publish_count


def append_result(path, lines):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "a", encoding="utf-8") as handle:
        for line in lines:
            handle.write(line + "\n")


def main():
    args = parse_args()
    relative_waypoints = parse_waypoints(args.relative_waypoints)
    deadline = time.monotonic() + args.timeout
    pose = {"xy": None, "z": None, "yaw": None, "message_count": 0}
    odom = {"xy": None, "yaw": None, "message_count": 0}

    os.makedirs(os.path.dirname(args.result_file), exist_ok=True)
    with open(args.result_file, "w", encoding="utf-8") as handle:
        handle.write(f"started={time.strftime('%Y-%m-%dT%H:%M:%S%z')}\n")
        handle.write("route_type=scout_wheel_ground_fixed_physics_demo\n")
        handle.write(f"cmd_topic={args.cmd_topic}\n")
        handle.write(f"tf_topic={args.tf_topic}\n")
        handle.write(f"odom_topic={args.odom_topic}\n")
        handle.write(f"relative_waypoints={args.relative_waypoints}\n")

    rclpy.init()
    node = rclpy.create_node("vln_scout_physics_route_demo")
    publisher = node.create_publisher(Twist, args.cmd_topic, 10)

    def on_tf(msg):
        pose["message_count"] += 1
        for transform in msg.transforms:
            if transform.header.frame_id == "map" and transform.child_frame_id == "base_link":
                translation = transform.transform.translation
                pose["xy"] = (translation.x, translation.y)
                pose["z"] = translation.z
                pose["yaw"] = quaternion_yaw(transform.transform.rotation)

    def on_odom(msg):
        odom["message_count"] += 1
        if msg.header.frame_id == "map" and msg.child_frame_id == "base_link":
            position = msg.pose.pose.position
            odom["xy"] = (position.x, position.y)
            odom["yaw"] = quaternion_yaw(msg.pose.pose.orientation)

    tf_subscription = node.create_subscription(TFMessage, args.tf_topic, on_tf, 20)
    odom_subscription = node.create_subscription(Odometry, args.odom_topic, on_odom, 20) if args.odom_topic else None

    try:
        wait_for_pose(node, pose, deadline, "map->base_link TF")
        start_xy = pose["xy"]
        start_yaw = pose["yaw"]
        centerline_path = [(0.0, 0.0)] + relative_waypoints
        map_waypoints = [local_to_map(start_xy, start_yaw, waypoint) for waypoint in relative_waypoints]

        angular_sign, calibration_yaw_delta, calibration_publish_count = maybe_calibrate_angular_sign(node, publisher, pose, args)
        straight_cruise_mode = abs(args.max_angular) < 1e-6 and abs(args.angular_gain) < 1e-6

        print("relative_waypoints=" + ";".join(f"{x:.2f},{y:.2f}" for x, y in relative_waypoints))
        print("map_waypoints=" + ";".join(f"{x:.2f},{y:.2f}" for x, y in map_waypoints))
        print(f"start_xy={start_xy[0]:.3f},{start_xy[1]:.3f}")
        print(f"start_yaw={start_yaw:.3f}")
        print(f"angular_sign={angular_sign:.0f}")
        print(f"calibration_yaw_delta={calibration_yaw_delta:.3f}")
        print(f"straight_cruise_mode={straight_cruise_mode}")
        print(f"progress_only_gates={args.progress_only_gates}")
        print(f"centerline_corridor={args.centerline_corridor}")

        append_result(args.result_file, [
            "map_waypoints=" + ";".join(f"{x:.3f},{y:.3f}" for x, y in map_waypoints),
            "centerline_path=" + ";".join(f"{x:.3f},{y:.3f}" for x, y in centerline_path),
            f"start_xy={start_xy[0]:.3f},{start_xy[1]:.3f}",
            f"start_yaw={start_yaw:.3f}",
            f"angular_sign={angular_sign:.0f}",
            f"calibration_yaw_delta={calibration_yaw_delta:.3f}",
            f"straight_cruise_mode={straight_cruise_mode}",
            f"progress_only_gates={args.progress_only_gates}",
            f"centerline_corridor={args.centerline_corridor}",
            f"lookahead_distance={args.lookahead_distance:.3f}",
            f"corridor_lateral_gain={args.corridor_lateral_gain:.3f}",
            f"centerline_forward_max={args.centerline_forward_max if args.centerline_forward_max is not None else 'none'}",
            f"bridge_forward_min={args.bridge_forward_min:.3f}",
            f"bridge_forward_max={args.bridge_forward_max:.3f}",
        ])

        current_index = 0
        reached = []
        publish_count = calibration_publish_count
        status_next = time.monotonic()
        period = 1.0 / max(1.0, args.publish_rate)
        current_linear = 0.0
        current_angular = 0.0
        last_xy = pose["xy"]
        last_forward_progress = 0.0
        last_progress_time = time.monotonic()
        stall_count = 0
        skipped_count = 0
        max_reached_cross_track = 0.0
        max_abs_lateral_offset = 0.0
        max_bridge_abs_lateral_offset = 0.0

        while time.monotonic() < deadline and current_index < len(map_waypoints):
            loop_start = time.monotonic()
            rclpy.spin_once(node, timeout_sec=0.02)
            if pose["xy"] is None or pose["yaw"] is None:
                time.sleep(period)
                continue

            if straight_cruise_mode:
                target_forward = relative_waypoints[current_index][0]
                progress = forward_progress_from_start(start_xy, start_yaw, pose["xy"])
                remaining_forward = target_forward - progress
                target = map_waypoints[current_index]
                dx = target[0] - pose["xy"][0]
                dy = target[1] - pose["xy"][1]
                dist = math.hypot(dx, dy)
                cross_track = abs(relative_waypoints[current_index][1] - (
                    -(pose["xy"][0] - start_xy[0]) * math.sin(start_yaw) +
                    (pose["xy"][1] - start_xy[1]) * math.cos(start_yaw)
                ))

                if remaining_forward <= args.goal_tolerance:
                    max_reached_cross_track = max(max_reached_cross_track, cross_track)
                    reached.append((current_index + 1, pose["xy"], dist, time.monotonic()))
                    append_result(args.result_file, [
                        f"reached_waypoint_{current_index + 1}=xy:{pose['xy'][0]:.3f},{pose['xy'][1]:.3f};remaining:{dist:.3f};forward_progress:{progress:.3f};remaining_forward:{remaining_forward:.3f};cross_track:{cross_track:.3f};straight_cruise:True",
                    ])
                    print(
                        f"到达路径点 {current_index + 1}/{len(map_waypoints)}，"
                        f"前向进度 {progress:.2f} m，横向偏差 {cross_track:.2f} m"
                    )
                    current_index += 1
                    continue

                desired_linear = args.max_linear
                if current_index == len(map_waypoints) - 1 and remaining_forward < 2.5:
                    desired_linear = clamp(args.linear_gain * max(remaining_forward, 0.0), 0.12, args.max_linear)

                current_linear = move_towards(current_linear, desired_linear, args.linear_accel * period)
                current_angular = move_towards(current_angular, 0.0, args.angular_accel * period)

                command = Twist()
                command.linear.x = current_linear
                command.angular.z = 0.0
                publisher.publish(command)
                publish_count += 1

                if progress - last_forward_progress > args.stall_progress_threshold or distance_2d(last_xy, pose["xy"]) > args.stall_progress_threshold:
                    last_xy = pose["xy"]
                    last_forward_progress = progress
                    last_progress_time = time.monotonic()
                elif time.monotonic() - last_progress_time > args.stall_skip_seconds:
                    stall_count += 1
                    last_progress_time = time.monotonic()
                    print(f"警告：最近 {args.stall_skip_seconds:.1f} 秒进展较小，可能正在打滑、顶住障碍或控制饱和。stall_count={stall_count}")
                    if args.skip_stalled_waypoints and remaining_forward <= args.stall_skip_forward_margin:
                        skipped_count += 1
                        append_result(args.result_file, [
                            f"skipped_waypoint_{current_index + 1}_due_to_stall=xy:{pose['xy'][0]:.3f},{pose['xy'][1]:.3f};forward_progress:{progress:.3f};remaining_forward:{remaining_forward:.3f};cross_track:{cross_track:.3f};straight_cruise:True",
                        ])
                        print(f"跳过路径点 {current_index + 1}/{len(map_waypoints)}：已接近但物理上进展停滞，继续下一个目标。")
                        current_index += 1
                        last_xy = pose["xy"]
                        last_forward_progress = progress
                        current_angular = 0.0
                        continue

                if time.monotonic() >= status_next:
                    print(
                        f"状态: wp={current_index + 1}/{len(map_waypoints)} forward={progress:.2f}m "
                        f"remain_forward={remaining_forward:.2f}m cross_track={cross_track:.2f}m "
                        f"lin={current_linear:.2f} ang=0.00 xy={pose['xy'][0]:.2f},{pose['xy'][1]:.2f}"
                    )
                    status_next = time.monotonic() + max(0.5, args.status_period)

                elapsed = time.monotonic() - loop_start
                if elapsed < period:
                    time.sleep(period - elapsed)
                continue

            global_forward_progress = forward_progress_from_start(start_xy, start_yaw, pose["xy"])
            raw_lateral_offset = lateral_offset_from_start(start_xy, start_yaw, pose["xy"])
            target_forward = relative_waypoints[current_index][0]
            remaining_forward = target_forward - global_forward_progress
            use_centerline_corridor = args.centerline_corridor and (
                args.centerline_forward_max is None or global_forward_progress <= args.centerline_forward_max
            )

            if use_centerline_corridor:
                global_lateral_offset = centerline_lateral_offset(centerline_path, global_forward_progress, raw_lateral_offset)
                route_end_forward = relative_waypoints[-1][0]
                lookahead_forward = clamp(
                    global_forward_progress + max(0.2, args.lookahead_distance),
                    0.0,
                    route_end_forward,
                )
                lookahead_left = centerline_left_at_forward(centerline_path, lookahead_forward)
                target = local_to_map(start_xy, start_yaw, (lookahead_forward, lookahead_left))
            else:
                global_lateral_offset = raw_lateral_offset
                target = map_waypoints[current_index]

            dx = target[0] - pose["xy"][0]
            dy = target[1] - pose["xy"][1]
            dist = math.hypot(dx, dy)

            if use_centerline_corridor:
                previous_forward = 0.0 if current_index == 0 else relative_waypoints[current_index - 1][0]
                segment_length = max(1e-6, target_forward - previous_forward)
                progress = global_forward_progress - previous_forward
                cross_track = abs(global_lateral_offset)
            else:
                segment_start = start_xy if current_index == 0 else map_waypoints[current_index - 1]
                progress, cross_track, segment_length = segment_progress_and_cross_track(segment_start, target, pose["xy"])

            max_abs_lateral_offset = max(max_abs_lateral_offset, abs(global_lateral_offset))
            if args.bridge_forward_min <= global_forward_progress <= args.bridge_forward_max:
                max_bridge_abs_lateral_offset = max(max_bridge_abs_lateral_offset, abs(global_lateral_offset))

            passed_gate = progress >= segment_length - 0.15 and cross_track <= args.gate_tolerance
            passed_progress_gate = args.progress_only_gates and remaining_forward <= args.goal_tolerance and cross_track <= args.gate_tolerance

            if dist <= args.goal_tolerance or passed_gate or passed_progress_gate:
                max_reached_cross_track = max(max_reached_cross_track, cross_track)
                reached.append((current_index + 1, pose["xy"], dist, time.monotonic()))
                append_result(args.result_file, [
                    f"reached_waypoint_{current_index + 1}=xy:{pose['xy'][0]:.3f},{pose['xy'][1]:.3f};remaining:{dist:.3f};progress:{progress:.3f};cross_track:{cross_track:.3f};forward_progress:{global_forward_progress:.3f};remaining_forward:{remaining_forward:.3f};lateral_offset:{global_lateral_offset:.3f};passed_gate:{passed_gate};passed_progress_gate:{passed_progress_gate}",
                ])
                print(
                    f"到达路径点 {current_index + 1}/{len(map_waypoints)}，剩余 {dist:.2f} m，"
                    f"前向进度 {global_forward_progress:.2f} m，横向偏差 {abs(global_lateral_offset):.2f} m"
                )
                current_index += 1
                continue

            if use_centerline_corridor:
                path_heading = start_yaw + centerline_heading_at_forward(centerline_path, lookahead_forward)
                heading_correction = clamp(
                    math.atan(args.corridor_lateral_gain * global_lateral_offset),
                    -abs(args.corridor_max_heading_correction),
                    abs(args.corridor_max_heading_correction),
                )
                target_heading = path_heading - heading_correction
            else:
                target_heading = math.atan2(dy, dx)
            heading_error = normalize_angle(target_heading - pose["yaw"])
            abs_heading_error = abs(heading_error)
            heading_scale = clamp(math.cos(min(abs_heading_error, math.pi * 0.5)), 0.0, 1.0)
            if use_centerline_corridor:
                desired_linear = clamp(args.linear_gain * max(remaining_forward, 0.0), 0.18, args.max_linear) * (0.72 + 0.28 * heading_scale)
            else:
                desired_linear = clamp(args.linear_gain * dist, 0.12, args.max_linear) * (0.20 + 0.80 * heading_scale)
            if abs_heading_error > 1.15:
                desired_linear = max(desired_linear, clamp(args.min_linear_while_turning, 0.05, args.max_linear))
            if current_index == len(map_waypoints) - 1 and (dist < 2.5 or remaining_forward < 2.5):
                desired_linear = min(desired_linear, 0.32)

            desired_angular = clamp(
                angular_sign * clamp(args.angular_gain * heading_error, -args.max_angular, args.max_angular) + args.angular_bias,
                -args.max_angular,
                args.max_angular,
            )
            current_linear = move_towards(current_linear, desired_linear, args.linear_accel * period)
            current_angular = move_towards(current_angular, desired_angular, args.angular_accel * period)

            command = Twist()
            command.linear.x = current_linear
            command.angular.z = current_angular
            publisher.publish(command)
            publish_count += 1

            if global_forward_progress - last_forward_progress > args.stall_progress_threshold or distance_2d(last_xy, pose["xy"]) > args.stall_progress_threshold:
                last_xy = pose["xy"]
                last_forward_progress = global_forward_progress
                last_progress_time = time.monotonic()
            elif time.monotonic() - last_progress_time > args.stall_skip_seconds:
                stall_count += 1
                last_progress_time = time.monotonic()
                print(f"警告：最近 {args.stall_skip_seconds:.1f} 秒进展较小，可能正在打滑、顶住障碍或控制饱和。stall_count={stall_count}")
                if args.skip_stalled_waypoints and remaining_forward <= args.stall_skip_forward_margin:
                    skipped_count += 1
                    append_result(args.result_file, [
                        f"skipped_waypoint_{current_index + 1}_due_to_stall=xy:{pose['xy'][0]:.3f},{pose['xy'][1]:.3f};remaining:{dist:.3f};progress:{progress:.3f};cross_track:{cross_track:.3f};forward_progress:{global_forward_progress:.3f};remaining_forward:{remaining_forward:.3f};lateral_offset:{global_lateral_offset:.3f}",
                    ])
                    print(f"跳过路径点 {current_index + 1}/{len(map_waypoints)}：已接近但物理上进展停滞，继续下一个目标。")
                    current_index += 1
                    last_xy = pose["xy"]
                    last_forward_progress = global_forward_progress
                    current_angular = 0.0
                    continue

            if time.monotonic() >= status_next:
                print(
                    f"状态: wp={current_index + 1}/{len(map_waypoints)} dist={dist:.2f}m "
                    f"forward={global_forward_progress:.2f}m remain_forward={remaining_forward:.2f}m "
                    f"lateral={global_lateral_offset:.2f}m max_lateral={max_abs_lateral_offset:.2f}m heading_error={heading_error:.2f} corridor={use_centerline_corridor} "
                    f"lin={current_linear:.2f} ang={current_angular:.2f} "
                    f"xy={pose['xy'][0]:.2f},{pose['xy'][1]:.2f}"
                )
                status_next = time.monotonic() + max(0.5, args.status_period)

            elapsed = time.monotonic() - loop_start
            if elapsed < period:
                time.sleep(period - elapsed)

        current_linear = move_towards(current_linear, 0.0, args.linear_accel * 2.0)
        current_angular = move_towards(current_angular, 0.0, args.angular_accel * 2.0)
        publish_zero(node, publisher, args.publish_rate, repeats=16)

        settle_end = time.monotonic() + 1.0
        while time.monotonic() < settle_end:
            rclpy.spin_once(node, timeout_sec=0.1)

        final_xy = pose["xy"]
        final_yaw = pose["yaw"]
        final_target = map_waypoints[-1]
        final_error = distance_2d(final_xy, final_target)
        total_progress = distance_2d(start_xy, final_xy)
        total_forward_progress = forward_progress_from_start(start_xy, start_yaw, final_xy)
        final_raw_lateral_offset = lateral_offset_from_start(start_xy, start_yaw, final_xy)
        final_lateral_offset = centerline_lateral_offset(centerline_path, total_forward_progress, final_raw_lateral_offset) if args.centerline_corridor else final_raw_lateral_offset
        reached_count = len(reached)

        summary = [
            f"publish_count={publish_count}",
            f"tf_message_count={pose['message_count']}",
            f"odom_message_count={odom['message_count']}",
            f"reached_count={reached_count}",
            f"route_waypoint_count={len(map_waypoints)}",
            f"final_xy={final_xy[0]:.3f},{final_xy[1]:.3f}",
            f"final_yaw={final_yaw:.3f}",
            f"final_error={final_error:.3f}",
            f"total_progress={total_progress:.3f}",
            f"total_forward_progress={total_forward_progress:.3f}",
            f"final_lateral_offset={final_lateral_offset:.3f}",
            f"max_reached_cross_track={max_reached_cross_track:.3f}",
            f"max_abs_lateral_offset={max_abs_lateral_offset:.3f}",
            f"max_bridge_abs_lateral_offset={max_bridge_abs_lateral_offset:.3f}",
            f"stall_count={stall_count}",
            f"skipped_count={skipped_count}",
        ]
        append_result(args.result_file, summary)
        for line in summary:
            print(line)

        errors = []
        if reached_count < min(args.min_reached, len(map_waypoints)):
            errors.append(f"只到达 {reached_count}/{len(map_waypoints)} 个路径点，低于要求 {args.min_reached}")
        if total_forward_progress < args.min_total_progress:
            errors.append(f"前向进度 {total_forward_progress:.3f}m，小于要求 {args.min_total_progress:.3f}m")
        if max_reached_cross_track > args.gate_tolerance:
            errors.append(f"路径点最大横向偏差 {max_reached_cross_track:.3f}m，超过 gate_tolerance {args.gate_tolerance:.3f}m")
        if abs(final_lateral_offset) > args.gate_tolerance:
            errors.append(f"终点横向偏差 {final_lateral_offset:.3f}m，超过 gate_tolerance {args.gate_tolerance:.3f}m")
        if args.max_lateral_offset is not None and max_abs_lateral_offset > args.max_lateral_offset:
            errors.append(f"全程最大横向偏差 {max_abs_lateral_offset:.3f}m，超过 max_lateral_offset {args.max_lateral_offset:.3f}m")
        if args.max_final_lateral_offset is not None and abs(final_lateral_offset) > args.max_final_lateral_offset:
            errors.append(f"终点横向偏差 {final_lateral_offset:.3f}m，超过 max_final_lateral_offset {args.max_final_lateral_offset:.3f}m")
        if args.max_bridge_lateral_offset is not None and max_bridge_abs_lateral_offset > args.max_bridge_lateral_offset:
            errors.append(f"桥区最大横向偏差 {max_bridge_abs_lateral_offset:.3f}m，超过 max_bridge_lateral_offset {args.max_bridge_lateral_offset:.3f}m")
        if publish_count <= 0:
            errors.append("没有发布任何 cmd_vel")
        if stall_count > 0:
            errors.append(f"出现 {stall_count} 次停滞，说明真实物理路线仍有卡点，不能作为通过结果")
        if skipped_count > 0:
            errors.append(f"出现 {skipped_count} 次跳过路径点，属于演示容错，不能作为真实物理通过结果")

        if errors:
            append_result(args.result_file, ["status=failed"] + ["error=" + item for item in errors])
            print("Scout 固定路线演示失败：", file=sys.stderr)
            for error in errors:
                print(f"- {error}", file=sys.stderr)
            return 1

        append_result(args.result_file, ["status=pass", "VLN_SCOUT_PHYSICS_ROUTE_MSG_OK"])
        print("VLN_SCOUT_PHYSICS_ROUTE_MSG_OK")
        return 0
    except Exception as exc:
        append_result(args.result_file, ["status=failed", f"error={exc}"])
        print(f"Scout 固定路线演示异常：{exc}", file=sys.stderr)
        return 1
    finally:
        publish_zero(node, publisher, args.publish_rate, repeats=8)
        if odom_subscription is not None:
            node.destroy_subscription(odom_subscription)
        node.destroy_subscription(tf_subscription)
        node.destroy_publisher(publisher)
        node.destroy_node()
        if rclpy.ok():
            rclpy.shutdown()


if __name__ == "__main__":
    raise SystemExit(main())
