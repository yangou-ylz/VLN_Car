#!/usr/bin/env python3
"""通过控制面板 HTTP API 驱动 Unity wheel-ground 小车，并检查手动速度行为。"""

import argparse
import json
import math
import sys
import time
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen


def get_json(url, timeout=2.0):
    with urlopen(url, timeout=timeout) as response:
        return json.loads(response.read().decode("utf-8"))


def post_json(url, payload, timeout=2.0):
    data = json.dumps(payload).encode("utf-8")
    request = Request(url, data=data, headers={"Content-Type": "application/json"}, method="POST")
    with urlopen(request, timeout=timeout) as response:
        return json.loads(response.read().decode("utf-8"))


def normalize_angle(angle):
    while angle > math.pi:
        angle -= 2.0 * math.pi
    while angle < -math.pi:
        angle += 2.0 * math.pi
    return angle


def distance_2d(a, b):
    return math.hypot(a["x"] - b["x"], a["y"] - b["y"])


def local_delta(start, end):
    dx = end["x"] - start["x"]
    dy = end["y"] - start["y"]
    cos_yaw = math.cos(start["yaw"])
    sin_yaw = math.sin(start["yaw"])
    forward = dx * cos_yaw + dy * sin_yaw
    lateral = -dx * sin_yaw + dy * cos_yaw
    yaw_delta = normalize_angle(end["yaw"] - start["yaw"])
    return forward, lateral, yaw_delta


def parse_args():
    parser = argparse.ArgumentParser(description="控制面板手动速度 Unity 联动验收")
    parser.add_argument("--base-url", default="http://127.0.0.1:8887")
    parser.add_argument("--timeout", type=float, default=45.0)
    parser.add_argument("--linear-speed", type=float, default=0.55)
    parser.add_argument("--angular-speed", type=float, default=0.42)
    parser.add_argument("--heartbeat-period", type=float, default=0.05)
    parser.add_argument("--forward-seconds", type=float, default=1.8)
    parser.add_argument("--turn-seconds", type=float, default=1.35)
    parser.add_argument("--stop-check-seconds", type=float, default=0.45)
    return parser.parse_args()


def wait_for_pose(base, timeout):
    deadline = time.monotonic() + timeout
    last_error = None
    while time.monotonic() < deadline:
        try:
            status = get_json(base + "/api/status")
            pose = status.get("pose")
            if pose is not None:
                return pose, status
        except (HTTPError, URLError, TimeoutError, json.JSONDecodeError) as exc:
            last_error = exc
        time.sleep(0.1)
    raise TimeoutError(f"未等到控制面板收到 map->base_link TF，last_error={last_error}")


def current_pose(base):
    status = get_json(base + "/api/status")
    pose = status.get("pose")
    if pose is None:
        raise RuntimeError("控制面板当前没有 TF pose")
    return pose


def stream_velocity(base, keys, seconds, linear_speed, angular_speed, period):
    end = time.monotonic() + seconds
    publish_count = 0
    last_response = None
    while time.monotonic() < end:
        last_response = post_json(base + "/api/velocity", {
            "keys": keys,
            "linear_speed": linear_speed,
            "angular_speed": angular_speed,
        })
        publish_count += 1
        sleep_time = min(period, max(0.0, end - time.monotonic()))
        if sleep_time > 0.0:
            time.sleep(sleep_time)
    return publish_count, last_response


def stop_and_measure(base, seconds):
    post_json(base + "/api/velocity_stop", {})
    before = current_pose(base)
    time.sleep(seconds)
    post_json(base + "/api/velocity_stop", {})
    after = current_pose(base)
    return distance_2d(before, after), before, after


def require(condition, message, errors):
    if not condition:
        errors.append(message)


def main():
    args = parse_args()
    base = args.base_url.rstrip("/")
    errors = []

    start_pose, initial_status = wait_for_pose(base, args.timeout)
    print("initial_status=" + json.dumps(initial_status, ensure_ascii=False, sort_keys=True))

    forward_count, forward_response = stream_velocity(
        base,
        {"up": True},
        args.forward_seconds,
        args.linear_speed,
        args.angular_speed,
        args.heartbeat_period,
    )
    forward_pose = current_pose(base)
    forward_progress, forward_lateral, forward_yaw = local_delta(start_pose, forward_pose)
    print(f"forward_publish_count={forward_count}")
    print("forward_response=" + json.dumps(forward_response, ensure_ascii=False, sort_keys=True))
    print(f"forward_progress_m={forward_progress:.3f}")
    print(f"forward_lateral_m={forward_lateral:.3f}")
    print(f"forward_yaw_delta_rad={forward_yaw:.3f}")
    require(forward_count >= 20, "前进阶段 HTTP 心跳次数过低，说明没有形成稳定手动控制流", errors)
    require(forward_response["command"]["linear_x"] > 0.0, "↑ 前进没有发布正 linear.x", errors)
    require(forward_progress > 0.35, f"↑ 前进后的前向进度太小：{forward_progress:.3f}m", errors)
    require(abs(forward_lateral) < 0.35, f"↑ 直行横向漂移过大：{forward_lateral:.3f}m", errors)
    require(abs(forward_yaw) < 0.35, f"↑ 直行偏航过大：{forward_yaw:.3f}rad", errors)

    stop_drift, stop_before, stop_after = stop_and_measure(base, args.stop_check_seconds)
    print(f"stop_drift_m={stop_drift:.3f}")
    print("stop_before=" + json.dumps(stop_before, ensure_ascii=False, sort_keys=True))
    print("stop_after=" + json.dumps(stop_after, ensure_ascii=False, sort_keys=True))
    require(stop_drift < 0.18, f"松键/停车后 {args.stop_check_seconds:.2f}s 仍漂移过大：{stop_drift:.3f}m", errors)

    left_start = current_pose(base)
    left_count, left_response = stream_velocity(
        base,
        {"a": True},
        args.turn_seconds,
        args.linear_speed,
        args.angular_speed,
        args.heartbeat_period,
    )
    left_end = current_pose(base)
    left_translation = distance_2d(left_start, left_end)
    left_yaw = normalize_angle(left_end["yaw"] - left_start["yaw"])
    print(f"left_publish_count={left_count}")
    print("left_response=" + json.dumps(left_response, ensure_ascii=False, sort_keys=True))
    print(f"left_translation_m={left_translation:.3f}")
    print(f"left_yaw_delta_rad={left_yaw:.3f}")
    require(left_response["command"]["linear_x"] == 0.0, "纯左转时 linear.x 应为 0", errors)
    require(left_response["command"]["angular_z"] > 0.0, "A/← 左转应发布正 angular.z", errors)
    require(left_yaw > 0.12, f"A/← 左转 yaw 增量太小或方向反了：{left_yaw:.3f}rad", errors)
    require(left_translation < 0.55, f"A/← 纯转向平移过大，不像原地转：{left_translation:.3f}m", errors)

    stop_and_measure(base, args.stop_check_seconds)

    right_start = current_pose(base)
    right_count, right_response = stream_velocity(
        base,
        {"d": True},
        args.turn_seconds,
        args.linear_speed,
        args.angular_speed,
        args.heartbeat_period,
    )
    right_end = current_pose(base)
    right_translation = distance_2d(right_start, right_end)
    right_yaw = normalize_angle(right_end["yaw"] - right_start["yaw"])
    print(f"right_publish_count={right_count}")
    print("right_response=" + json.dumps(right_response, ensure_ascii=False, sort_keys=True))
    print(f"right_translation_m={right_translation:.3f}")
    print(f"right_yaw_delta_rad={right_yaw:.3f}")
    require(right_response["command"]["linear_x"] == 0.0, "纯右转时 linear.x 应为 0", errors)
    require(right_response["command"]["angular_z"] < 0.0, "D/→ 右转应发布负 angular.z", errors)
    require(right_yaw < -0.12, f"D/→ 右转 yaw 增量太小或方向反了：{right_yaw:.3f}rad", errors)
    require(right_translation < 0.55, f"D/→ 纯转向平移过大，不像原地转：{right_translation:.3f}m", errors)

    stop_and_measure(base, args.stop_check_seconds)

    arrow_left_start = current_pose(base)
    arrow_left_count, arrow_left_response = stream_velocity(
        base,
        {"left": True},
        args.turn_seconds,
        args.linear_speed,
        args.angular_speed,
        args.heartbeat_period,
    )
    arrow_left_end = current_pose(base)
    arrow_left_translation = distance_2d(arrow_left_start, arrow_left_end)
    arrow_left_yaw = normalize_angle(arrow_left_end["yaw"] - arrow_left_start["yaw"])
    print(f"arrow_left_publish_count={arrow_left_count}")
    print("arrow_left_response=" + json.dumps(arrow_left_response, ensure_ascii=False, sort_keys=True))
    print(f"arrow_left_translation_m={arrow_left_translation:.3f}")
    print(f"arrow_left_yaw_delta_rad={arrow_left_yaw:.3f}")
    require(arrow_left_response["command"]["linear_x"] == 0.0, "← 纯左转时 linear.x 应为 0", errors)
    require(arrow_left_response["command"]["angular_z"] > 0.0, "← 左转应发布正 angular.z", errors)
    require(arrow_left_yaw > 0.12, f"← 左转 yaw 增量太小或方向反了：{arrow_left_yaw:.3f}rad", errors)
    require(arrow_left_translation < 0.55, f"← 纯转向平移过大，不像原地转：{arrow_left_translation:.3f}m", errors)

    stop_and_measure(base, args.stop_check_seconds)

    arrow_right_start = current_pose(base)
    arrow_right_count, arrow_right_response = stream_velocity(
        base,
        {"right": True},
        args.turn_seconds,
        args.linear_speed,
        args.angular_speed,
        args.heartbeat_period,
    )
    arrow_right_end = current_pose(base)
    arrow_right_translation = distance_2d(arrow_right_start, arrow_right_end)
    arrow_right_yaw = normalize_angle(arrow_right_end["yaw"] - arrow_right_start["yaw"])
    print(f"arrow_right_publish_count={arrow_right_count}")
    print("arrow_right_response=" + json.dumps(arrow_right_response, ensure_ascii=False, sort_keys=True))
    print(f"arrow_right_translation_m={arrow_right_translation:.3f}")
    print(f"arrow_right_yaw_delta_rad={arrow_right_yaw:.3f}")
    require(arrow_right_response["command"]["linear_x"] == 0.0, "→ 纯右转时 linear.x 应为 0", errors)
    require(arrow_right_response["command"]["angular_z"] < 0.0, "→ 右转应发布负 angular.z", errors)
    require(arrow_right_yaw < -0.12, f"→ 右转 yaw 增量太小或方向反了：{arrow_right_yaw:.3f}rad", errors)
    require(arrow_right_translation < 0.55, f"→ 纯转向平移过大，不像原地转：{arrow_right_translation:.3f}m", errors)

    final_drift, _, _ = stop_and_measure(base, args.stop_check_seconds)
    print(f"final_stop_drift_m={final_drift:.3f}")
    require(final_drift < 0.18, f"最终停车后仍漂移过大：{final_drift:.3f}m", errors)

    if errors:
        post_json(base + "/api/velocity_stop", {})
        print("控制面板手动速度 Unity 联动验收失败：", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    print("VLN_CONTROL_PANEL_MANUAL_VELOCITY_UNITY_HTTP_OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
