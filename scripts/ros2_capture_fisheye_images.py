#!/usr/bin/env python3
"""Capture VLN fisheye Image/CameraInfo topics and generate rectified previews."""

import argparse
import math
import time
from pathlib import Path

import cv2
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
    parser = argparse.ArgumentParser(description="保存四路真实鱼眼 ROS2 图像，并生成等距鱼眼反校正图。")
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--timeout", type=float, default=60.0)
    parser.add_argument("--width", type=int, default=640)
    parser.add_argument("--height", type=int, default=640)
    parser.add_argument("--encoding", default="rgb8")
    parser.add_argument("--view-angle-deg", type=float, default=190.0)
    parser.add_argument("--rectified-fov-deg", type=float, default=90.0)
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


def rectify_equidistant(rgb, view_angle_deg, rectified_fov_deg):
    height, width = rgb.shape[:2]
    out_w = width
    out_h = int(round(width * 3.0 / 4.0))
    cx_fish = width * 0.5
    cy_fish = height * 0.5
    radius = min(width, height) * 0.5
    half_angle = math.radians(view_angle_deg * 0.5)
    f_fish = radius / max(half_angle, 1e-9)

    f_rect = (out_w * 0.5) / math.tan(math.radians(rectified_fov_deg * 0.5))
    cx_rect = out_w * 0.5
    cy_rect = out_h * 0.5

    yy, xx = np.indices((out_h, out_w), dtype=np.float32)
    x = (xx - cx_rect) / f_rect
    y = (yy - cy_rect) / f_rect
    z = np.ones_like(x)
    norm = np.sqrt(x * x + y * y + z * z)
    x /= norm
    y /= norm
    z /= norm

    theta = np.arccos(np.clip(z, -1.0, 1.0))
    phi = np.arctan2(y, x)
    r = f_fish * theta
    map_x = (cx_fish + r * np.cos(phi)).astype(np.float32)
    map_y = (cy_fish + r * np.sin(phi)).astype(np.float32)
    valid = theta <= half_angle
    rectified = cv2.remap(rgb, map_x, map_y, interpolation=cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT, borderValue=(0, 0, 0))
    rectified[~valid] = 0
    return rectified


def black_fraction(rgb, patch=48):
    h, w = rgb.shape[:2]
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


def validate_info(view, msg, args):
    errors = []
    expected_frame = VIEWS[view][2]
    if msg.header.frame_id != expected_frame:
        errors.append(f"camera_info frame_id={msg.header.frame_id}, expected {expected_frame}")
    if msg.width != args.width:
        errors.append(f"camera_info width={msg.width}, expected {args.width}")
    if msg.height != args.height:
        errors.append(f"camera_info height={msg.height}, expected {args.height}")
    if msg.distortion_model != "equidistant":
        errors.append(f"distortion_model={msg.distortion_model}, expected equidistant")
    if len(msg.d) != 4:
        errors.append(f"D length={len(msg.d)}, expected 4")
    expected_f = (min(args.width, args.height) * 0.5) / math.radians(args.view_angle_deg * 0.5)
    if len(msg.k) >= 5 and abs(msg.k[0] - expected_f) > 1.5:
        errors.append(f"fx={msg.k[0]:.3f}, expected about {expected_f:.3f}")
    if len(msg.k) >= 5 and abs(msg.k[4] - expected_f) > 1.5:
        errors.append(f"fy={msg.k[4]:.3f}, expected about {expected_f:.3f}")
    return errors


def image_has_valid_fisheye_content(msg):
    try:
        rgb = image_msg_to_rgb(msg)
    except Exception:
        return False
    return black_fraction(rgb) >= 0.75 and center_mean(rgb) >= 2.0 and center_std(rgb) >= 6.0


def main():
    args = parse_args()
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    images = {}
    infos = {}
    image_counts = {view: 0 for view in VIEWS}
    start_time = time.monotonic()

    rclpy.init()
    node = rclpy.create_node("vln_capture_fisheye_images")

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
            valid_latest_images = len(images) == len(VIEWS) and all(image_has_valid_fisheye_content(images[view]) for view in VIEWS)
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
            raw_path = output_dir / f"{view}_fisheye_raw.png"
            rectified_path = output_dir / f"{view}_fisheye_rectified_90deg.png"
            save_rgb(raw_path, rgb)
            save_rgb(rectified_path, rectify_equidistant(rgb, args.view_angle_deg, args.rectified_fov_deg))

            view_errors = []
            if msg.width != args.width:
                view_errors.append(f"image width={msg.width}, expected {args.width}")
            if msg.height != args.height:
                view_errors.append(f"image height={msg.height}, expected {args.height}")
            if msg.encoding != args.encoding:
                view_errors.append(f"image encoding={msg.encoding}, expected {args.encoding}")
            if msg.header.frame_id != VIEWS[view][2]:
                view_errors.append(f"image frame_id={msg.header.frame_id}, expected {VIEWS[view][2]}")
            corner_black = black_fraction(rgb)
            center = center_mean(rgb)
            center_variation = center_std(rgb)
            if corner_black < 0.75:
                view_errors.append(f"corner_black_fraction={corner_black:.3f}, expected >= 0.75 for circular fisheye mask")
            if center < 2.0:
                view_errors.append(f"center_mean={center:.3f}, expected visible image content")
            if center_variation < 6.0:
                view_errors.append(f"center_std={center_variation:.3f}, expected non-uniform rendered scene content")
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
                f"{view}_corner_black_fraction={corner_black:.3f}",
                f"{view}_center_mean={center:.3f}",
                f"{view}_center_std={center_variation:.3f}",
                f"{view}_raw_png={raw_path}",
                f"{view}_rectified_png={rectified_path}",
            ])
            if view_errors:
                failures.extend(f"{view}: {error}" for error in view_errors)

        report.append(f"output_dir={output_dir}")
        report.append(f"failure_count={len(failures)}")
        for failure in failures:
            report.append(f"failure={failure}")

        report_path = output_dir / "fisheye_capture_report.txt"
        report_path.write_text("\n".join(report) + "\n", encoding="utf-8")
        print("\n".join(report))
        if failures:
            return 1
        print("VLN_FISHEYE_CAPTURE_AND_RECTIFY_OK")
        return 0
    finally:
        for sub in subscriptions:
            node.destroy_subscription(sub)
        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    raise SystemExit(main())
