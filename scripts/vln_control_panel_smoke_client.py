#!/usr/bin/env python3
"""通过 HTTP 验证 VLN 控制面板：等待 TF、发送目标、等待到达。"""

import argparse
import json
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


def parse_args():
    parser = argparse.ArgumentParser(description="控制面板 HTTP smoke test")
    parser.add_argument("--base-url", default="http://127.0.0.1:8765")
    parser.add_argument("--target-x", type=float, default=1.2)
    parser.add_argument("--target-y", type=float, default=0.0)
    parser.add_argument("--timeout", type=float, default=35.0)
    parser.add_argument("--goal-tolerance", type=float, default=0.36)
    return parser.parse_args()


def main():
    args = parse_args()
    deadline = time.monotonic() + args.timeout
    status_url = args.base_url.rstrip("/") + "/api/status"
    target_url = args.base_url.rstrip("/") + "/api/target"
    stop_url = args.base_url.rstrip("/") + "/api/stop"

    last_error = None
    while time.monotonic() < deadline:
        try:
            status = get_json(status_url)
            if status.get("pose"):
                break
        except (HTTPError, URLError, TimeoutError, json.JSONDecodeError) as exc:
            last_error = exc
        time.sleep(0.25)
    else:
        print(f"未等到控制面板 TF 状态，last_error={last_error}", file=sys.stderr)
        return 1

    print("initial_status=" + json.dumps(status, ensure_ascii=False, sort_keys=True))
    target_response = post_json(target_url, {"x": args.target_x, "y": args.target_y})
    print("target_response=" + json.dumps(target_response, ensure_ascii=False, sort_keys=True))
    if not target_response.get("ok"):
        print("控制面板拒绝目标点", file=sys.stderr)
        return 1

    reached_status = None
    while time.monotonic() < deadline:
        status = get_json(status_url)
        distance = status.get("distance")
        if status.get("pose") and not status.get("active") and distance is None:
            reached_status = status
            break
        if distance is not None and distance <= args.goal_tolerance and not status.get("active"):
            reached_status = status
            break
        time.sleep(0.25)

    post_json(stop_url, {})

    if reached_status is None:
        final_status = get_json(status_url)
        print("final_status=" + json.dumps(final_status, ensure_ascii=False, sort_keys=True))
        print("控制面板未在限定时间内到达目标", file=sys.stderr)
        return 1

    print("reached_status=" + json.dumps(reached_status, ensure_ascii=False, sort_keys=True))
    print("VLN_CONTROL_PANEL_HTTP_SMOKE_OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
