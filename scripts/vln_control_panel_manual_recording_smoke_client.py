#!/usr/bin/env python3
"""验证控制面板的手动速度控制、记录和导出接口。"""

import argparse
import json
import sys
import time
from pathlib import Path
from urllib.request import Request, urlopen


EXPECTED_SCHEMA = "vln_manual_cmd_vel_recording_v1"


def get_json(url, timeout=2.0):
    with urlopen(url, timeout=timeout) as response:
        return json.loads(response.read().decode("utf-8"))


def post_json(url, payload, timeout=2.0):
    data = json.dumps(payload).encode("utf-8")
    request = Request(url, data=data, headers={"Content-Type": "application/json"}, method="POST")
    with urlopen(request, timeout=timeout) as response:
        return json.loads(response.read().decode("utf-8"))


def parse_args():
    parser = argparse.ArgumentParser(description="控制面板手动驾驶记录 smoke test")
    parser.add_argument("--base-url", default="http://127.0.0.1:8765")
    parser.add_argument("--timeout", type=float, default=10.0)
    return parser.parse_args()


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def main():
    args = parse_args()
    base = args.base_url.rstrip("/")
    deadline = time.monotonic() + args.timeout
    while time.monotonic() < deadline:
        try:
            status = get_json(base + "/api/status")
            if status.get("ok"):
                break
        except Exception:
            time.sleep(0.2)
    else:
        print("控制面板没有在限定时间内响应 /api/status", file=sys.stderr)
        return 1

    print("initial_status=" + json.dumps(status, ensure_ascii=False, sort_keys=True))
    print("start_record=" + json.dumps(post_json(base + "/api/recording/start", {}), ensure_ascii=False, sort_keys=True))

    forward_left = post_json(base + "/api/velocity", {
        "keys": {"up": True, "a": True},
        "linear_speed": 0.55,
        "angular_speed": 0.35,
    })
    print("forward_left=" + json.dumps(forward_left, ensure_ascii=False, sort_keys=True))
    require(forward_left["command"]["linear_x"] > 0.0, "↑ 应该发布正 linear.x，物理层负责保证正向就是前进")
    require(forward_left["command"]["angular_z"] > 0.0, "A/← 左转应该发布正 angular.z，这是当前 Scout wheel-ground 候选的实测方向")
    time.sleep(0.35)

    backward_right = post_json(base + "/api/velocity", {
        "keys": {"down": True, "d": True},
        "linear_speed": 0.55,
        "angular_speed": 0.35,
    })
    print("backward_right=" + json.dumps(backward_right, ensure_ascii=False, sort_keys=True))
    require(backward_right["command"]["linear_x"] < 0.0, "↓ 应该发布负 linear.x")
    require(backward_right["command"]["angular_z"] < 0.0, "D/→ 右转应该发布负 angular.z，这是当前 Scout wheel-ground 候选的实测方向")
    time.sleep(0.25)

    print("velocity_stop=" + json.dumps(post_json(base + "/api/velocity_stop", {}), ensure_ascii=False, sort_keys=True))
    print("stop_record=" + json.dumps(post_json(base + "/api/recording/stop", {}), ensure_ascii=False, sort_keys=True))
    export_response = post_json(base + "/api/recording/export", {})
    print("export_record=" + json.dumps(export_response, ensure_ascii=False, sort_keys=True))

    path = Path(export_response["path"])
    require(path.exists(), f"导出文件不存在：{path}")
    with open(path, "r", encoding="utf-8") as handle:
        payload = json.load(handle)
    require(payload.get("schema") == EXPECTED_SCHEMA, "导出 schema 不正确")
    samples = payload.get("samples", [])
    require(len(samples) >= 4, "导出样本太少")
    require(any(sample.get("linear_x", 0.0) > 0.0 and sample.get("angular_z", 0.0) > 0.0 for sample in samples), "记录中缺少前进左转样本")
    require(any(sample.get("linear_x", 0.0) < 0.0 and sample.get("angular_z", 0.0) < 0.0 for sample in samples), "记录中缺少后退右转样本")
    print(f"exported_file={path}")
    print("VLN_CONTROL_PANEL_MANUAL_RECORDING_HTTP_SMOKE_OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
