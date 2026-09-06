#!/usr/bin/env python3
"""Capture VLN pinhole RGB Image/CameraInfo topics and validate the model."""

import argparse
import math
import time
from pathlib import Path

import numpy as np
from PIL import Image as PilImage

import rclpy
from sensor_msgs.msg import CameraInfo, Image


VIEWS = {
    "front": ("/vln/front/image_raw", "/vln/front/camera_info", "front_camera_optical_frame"),
    "rear": ("/vln/rear/image_raw", "/vln/rear/camera_info", "rear_camera_optical_frame"),
    "left": ("/vln/left/image_raw", "/vln/left/camera_info", "left_camera_optical_frame"),
    "right": ("/vln/right/image_raw", "/vln/right/camera_info", "right_camera_optical_frame"),
}


def parse_args():
    parser = argparse.ArgumentParser(description="保存四路普通 RGB/pinhole ROS2 图像并校验 CameraInfo。")
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--timeout", type=float, default=60.0)
    parser.add_argument("--width", type=int, default=960)
    parser.add_argument("--height", type=int, default=540)
    parser.add_argument("--encoding", default="rgb8")
    parser.add_argument("--fov-deg", type=float, default=120.0)
    parser.add_argument("--max-corner-black-fraction", type=float, default=0.40)
    return parser.parse_args()


def image_msg_to_rgb(msg):
    if msg.encoding == "rgb8":
        arr = np.frombuffer(msg.data, dtype=np.uint8).reshape((msg.height, msg.width, 3))
        return arr.copy()
    if msg.encoding == "bgr8":
        arr = np.frombuffer(msg.data, dtype=np.uint8).reshape((msg.height, msg.width, 3))
        return arr[:, :, ::-1].copy()
    if msg.encoding == "rgba8":
        arr = np.frombuffer(msg.data, dtype=np.uint8).reshape((msg.height, msg.width, 4))
        return arr[:, :, :3].copy()
    raise ValueError(f"unsupported image encoding: {msg.encoding}")


def save_rgb(path, rgb):
    PilImage.fromarray(rgb, mode="RGB").save(path)


def black_fraction(rgb, patch=48):
    h, w = rgb.shape[:2]
    patch = max(4, min(patch, h // 3, w // 3))
    corners = np.concatenate([
        rgb[:patch, :patch].reshape(-1, 3),
        rgb[:patch, w - patch:].reshape(-1, 3),
        rgb[h - patch:, :patch].reshape(-1, 3),
        rgb[h - patch:, w - patch:].reshape(-1, 3),
    ], axis=0)
    return float(np.mean(np.sum(corners, axis=1) < 18))


def center_mean(rgb, patch=64):
    h, w = rgb.shape[:2]
    y0 = max(0, h // 2 - patch // 2)
    x0 = max(0, w // 2 - patch // 2)
    return float(np.mean(rgb[y0:y0 + patch, x0:x0 + patch]))


def center_std(rgb, patch=160):
    h, w = rgb.shape[:2]
    y0 = max(0, h // 2 - patch // 2)
    x0 = max(0, w // 2 - patch // 2)
    return float(np.std(rgb[y0:y0 + patch, x0:x0 + patch].astype(np.float32)))


def expected_focal_pixels(height, fov_deg):
    return (height * 0.5) / math.tan(math.radians(fov_deg * 0.5))


def validate_info(view, msg, args):
    errors = []
    expected_frame = VIEWS[view][2]
    if msg.header.frame_id != expected_frame:
        errors.append(f"camera_info frame_id={msg.header.frame_id}, expected {expected_frame}")
    if msg.width != args.width:
        errors.append(f"camera_info width={msg.width}, expected {args.width}")
    if msg.height != args.height:
        errors.append(f"camera_info height={msg.height}, expected {args.height}")
    if msg.distortion_model != "plumb_bob":
        errors.append(f"distortion_model={msg.distortion_model}, expected plumb_bob")
    if len(msg.d) != 5:
        errors.append(f"D length={len(msg.d)}, expected 5")
    elif any(abs(value) > 1e-9 for value in msg.d):
        errors.append("D contains non-zero distortion coefficients")
    expected_f = expected_focal_pixels(args.height, args.fov_deg)
    if len(msg.k) >= 5 and abs(msg.k[0] - expected_f) > 6.0:
        errors.append(f"fx={msg.k[0]:.3f}, expected about {expected_f:.3f}")
    if len(msg.k) >= 5 and abs(msg.k[4] - expected_f) > 6.0:
        errors.append(f"fy={msg.k[4]:.3f}, expected about {expected_f:.3f}")
    if len(msg.k) >= 6 and (abs(msg.k[2] - args.width * 0.5) > 1.0 or abs(msg.k[5] - args.height * 0.5) > 1.0):
        errors.append(f"principal_point=({msg.k[2]:.3f},{msg.k[5]:.3f}), expected center")
    return errors


def image_has_content(msg):
    try:
        rgb = image_msg_to_rgb(msg)
    except Exception:
        return False
    return center_mean(rgb) >= 2.0 and center_std(rgb) >= 6.0


def main():
    args = parse_args()
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    images = {}
    infos = {}
    image_counts = {view: 0 for view in VIEWS}
    start_time = time.monotonic()

    rclpy.init()
    node = rclpy.create_node("vln_capture_pinhole_rgb_images")

    subscriptions = []
    for view, (image_topic, info_topic, _) in VIEWS.items():
        def on_image(msg, v=view):
            image_counts[v] += 1
            images[v] = msg

        subscriptions.append(node.create_subscription(Image, image_topic, on_image, 10))
        subscriptions.append(node.create_subscription(CameraInfo, info_topic, lambda msg, v=view: infos.__setitem__(v, msg), 10))

    deadline = time.monotonic() + args.timeout
    try:
        while time.monotonic() < deadline:
            enough_images = all(image_counts[view] >= 5 for view in VIEWS)
            warmed_up = (time.monotonic() - start_time) >= 2.0
            valid_latest_images = len(images) == len(VIEWS) and all(image_has_content(images[view]) for view in VIEWS)
            if warmed_up and enough_images and valid_latest_images and len(infos) == len(VIEWS):
                break
            rclpy.spin_once(node, timeout_sec=0.2)

        report = []
        failures = []
        for view in VIEWS:
            if view not in images:
                failures.append(f"{view}: missing image")
                continue
            if view not in infos:
                failures.append(f"{view}: missing camera_info")
                continue

            msg = images[view]
            info = infos[view]
            rgb = image_msg_to_rgb(msg)
            output_path = output_dir / f"{view}_pinhole_rgb.png"
            save_rgb(output_path, rgb)

            corner_black = black_fraction(rgb)
            center = center_mean(rgb)
            variation = center_std(rgb)
            view_errors = []
            if msg.width != args.width:
                view_errors.append(f"image width={msg.width}, expected {args.width}")
            if msg.height != args.height:
                view_errors.append(f"image height={msg.height}, expected {args.height}")
            if msg.encoding != args.encoding:
                view_errors.append(f"image encoding={msg.encoding}, expected {args.encoding}")
            if msg.header.frame_id != VIEWS[view][2]:
                view_errors.append(f"image frame_id={msg.header.frame_id}, expected {VIEWS[view][2]}")
            if corner_black > args.max_corner_black_fraction:
                view_errors.append(
                    f"corner_black_fraction={corner_black:.3f}, expected <= {args.max_corner_black_fraction:.3f} for rectangular pinhole image")
            if center < 2.0:
                view_errors.append(f"center_mean={center:.3f}, expected visible image content")
            if variation < 6.0:
                view_errors.append(f"center_std={variation:.3f}, expected non-uniform rendered scene content")
            view_errors.extend(validate_info(view, info, args))

            report.extend([
                f"{view}_image_topic={VIEWS[view][0]}",
                f"{view}_camera_info_topic={VIEWS[view][1]}",
                f"{view}_image_count={image_counts[view]}",
                f"{view}_width={msg.width}",
                f"{view}_height={msg.height}",
                f"{view}_encoding={msg.encoding}",
                f"{view}_frame_id={msg.header.frame_id}",
                f"{view}_distortion_model={info.distortion_model}",
                f"{view}_camera_info_fx={info.k[0] if len(info.k) else float('nan'):.3f}",
                f"{view}_camera_info_fy={info.k[4] if len(info.k) > 4 else float('nan'):.3f}",
                f"{view}_camera_info_d_len={len(info.d)}",
                f"{view}_corner_black_fraction={corner_black:.3f}",
                f"{view}_center_mean={center:.3f}",
                f"{view}_center_std={variation:.3f}",
                f"{view}_saved_png={output_path}",
            ])
            if view_errors:
                failures.extend(f"{view}: {error}" for error in view_errors)

        summary = [
            "camera_model=pinhole",
            "camera_distortion_model=plumb_bob_zero_coefficients",
            f"expected_fov_deg={args.fov_deg:.1f}",
            f"expected_width={args.width}",
            f"expected_height={args.height}",
            f"view_count={len(images)}",
            f"camera_info_count={len(infos)}",
        ]
        if failures:
            summary.append("success=0")
            output = "\n".join(summary + report + ["failure=" + item for item in failures]) + "\n"
            (output_dir / "pinhole_rgb_capture_report.txt").write_text(output, encoding="utf-8")
            print(output, end="")
            return 1

        summary.append("success=1")
        summary.append("VLN_PINHOLE_RGB_CAPTURE_OK")
        output = "\n".join(summary + report) + "\n"
        (output_dir / "pinhole_rgb_capture_report.txt").write_text(output, encoding="utf-8")
        print(output, end="")
        return 0
    finally:
        for subscription in subscriptions:
            node.destroy_subscription(subscription)
        node.destroy_node()
        if rclpy.ok():
            rclpy.shutdown()


if __name__ == "__main__":
    raise SystemExit(main())
