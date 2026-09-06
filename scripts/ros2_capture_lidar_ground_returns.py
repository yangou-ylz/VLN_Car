#!/usr/bin/env python3
"""采样 PointCloud2，统计真实 ROS 点云的近地回波，不修改 Unity 或 ROS 配置。"""

import argparse
import math
import struct
import sys
import time
import zlib

import rclpy
from rclpy.serialization import deserialize_message
from sensor_msgs.msg import PointCloud2, PointField


DATATYPE_FORMAT = {
    PointField.INT8: ("b", 1),
    PointField.UINT8: ("B", 1),
    PointField.INT16: ("h", 2),
    PointField.UINT16: ("H", 2),
    PointField.INT32: ("i", 4),
    PointField.UINT32: ("I", 4),
    PointField.FLOAT32: ("f", 4),
    PointField.FLOAT64: ("d", 8),
}


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--topic", default="/vln/lidar/points")
    parser.add_argument("--duration", type=float, default=5.0)
    parser.add_argument("--timeout", type=float, default=15.0)
    parser.add_argument("--max-horizontal-range", type=float, default=25.0)
    parser.add_argument("--near-ground-z", type=float, default=-1.5)
    parser.add_argument(
        "--vertical-axis",
        choices=("auto", "x", "y", "z"),
        default="z",
        help="点云中用于近地判断的垂直轴。UnitySensors LiDAR 的垂直分量经 ROS 字段映射后默认使用 z。",
    )
    parser.add_argument(
        "--near-ground-depth",
        type=float,
        default=1.5,
        help="垂直轴低于 -depth 且水平距离在阈值内时计为近地候选。保留 --near-ground-z 作为旧口径。",
    )
    parser.add_argument(
        "--ring-ground-depth",
        type=float,
        default=0.25,
        help="按距离环带统计地面点时使用的垂直阈值，默认只要低于传感器 0.25m 就计入地面环带。",
    )
    parser.add_argument("--topdown-png", default="", help="可选：导出真实 PointCloud2 俯视调试 PNG。")
    parser.add_argument("--topdown-range", type=float, default=45.0, help="俯视 PNG 半径范围，单位 m。")
    parser.add_argument("--topdown-size", type=int, default=900, help="俯视 PNG 边长像素。")
    parser.add_argument("--topdown-max-points", type=int, default=180000, help="俯视 PNG 最多绘制点数，避免过大。")
    return parser.parse_args()


def field_map(message):
    return {field.name: field for field in message.fields}


def read_scalar(data, offset, field, is_bigendian):
    fmt_size = DATATYPE_FORMAT.get(field.datatype)
    if fmt_size is None:
        return None
    fmt, size = fmt_size
    if field.count != 1:
        return None
    prefix = ">" if is_bigendian else "<"
    try:
        return struct.unpack_from(prefix + fmt, data, offset + field.offset)[0]
    except struct.error:
        return None


def finite(value):
    return value is not None and math.isfinite(value)


def empty_axis_counts():
    return {axis: {"0.25": 0, "0.50": 0, "1.00": 0, "1.50": 0, "2.00": 0, "3.00": 0} for axis in ("x", "y", "z")}


def ring_labels():
    return ("0-2", "2-5", "5-10", "10-20", "20-40")


def sector_labels():
    return ("front", "front_left", "left", "rear_left", "rear", "rear_right", "right", "front_right")


def empty_axis_ring_counts():
    return {axis: {label: 0 for label in ring_labels()} for axis in ("x", "y", "z")}


def empty_axis_sector_counts():
    return {axis: {label: 0 for label in sector_labels()} for axis in ("x", "y", "z")}


def ring_label(horizontal):
    if 0.0 <= horizontal < 2.0:
        return "0-2"
    if 2.0 <= horizontal < 5.0:
        return "2-5"
    if 5.0 <= horizontal < 10.0:
        return "5-10"
    if 10.0 <= horizontal < 20.0:
        return "10-20"
    if 20.0 <= horizontal <= 40.0:
        return "20-40"
    return None


def horizontal_components(axis, coordinates):
    if axis == "x":
        return coordinates["y"], coordinates["z"]
    if axis == "y":
        return coordinates["x"], coordinates["z"]
    return coordinates["x"], coordinates["y"]


def sector_label(axis, coordinates):
    forward, left = horizontal_components(axis, coordinates)
    if abs(forward) < 1e-9 and abs(left) < 1e-9:
        return None
    angle = math.degrees(math.atan2(left, forward))
    normalized = angle + 360.0 if angle < 0.0 else angle
    index = int(math.floor((normalized + 22.5) / 45.0)) % len(sector_labels())
    return sector_labels()[index]


def analyze_message(message, max_horizontal_range, near_ground_z, near_ground_depth, ring_ground_depth):
    fields = field_map(message)
    if not all(name in fields for name in ("x", "y", "z")):
        return {
            "points": 0,
            "valid": 0,
            "near_ground": 0,
            "selected_near_ground": {"x": 0, "y": 0, "z": 0},
            "axis_depth_counts": empty_axis_counts(),
            "axis_ring_ground_counts": empty_axis_ring_counts(),
            "axis_sector_ground_counts": empty_axis_sector_counts(),
            "min": (float("nan"),) * 3,
            "max": (float("nan"),) * 3,
            "fields": ",".join(field.name for field in message.fields),
        }

    valid = 0
    near_ground = 0
    selected_near_ground = {"x": 0, "y": 0, "z": 0}
    axis_depth_counts = empty_axis_counts()
    axis_ring_ground_counts = empty_axis_ring_counts()
    axis_sector_ground_counts = empty_axis_sector_counts()
    min_values = [float("inf")] * 3
    max_values = [float("-inf")] * 3
    data = bytes(message.data)
    total_points = message.width * message.height
    depth_thresholds = ((0.25, "0.25"), (0.50, "0.50"), (1.00, "1.00"), (1.50, "1.50"), (2.00, "2.00"), (3.00, "3.00"))

    for index in range(total_points):
        base = index * message.point_step
        x = read_scalar(data, base, fields["x"], message.is_bigendian)
        y = read_scalar(data, base, fields["y"], message.is_bigendian)
        z = read_scalar(data, base, fields["z"], message.is_bigendian)
        if not all(finite(value) for value in (x, y, z)):
            continue

        valid += 1
        values = (x, y, z)
        for axis in range(3):
            min_values[axis] = min(min_values[axis], values[axis])
            max_values[axis] = max(max_values[axis], values[axis])

        horizontal_range = math.hypot(x, y)
        if horizontal_range <= max_horizontal_range and z <= near_ground_z:
            near_ground += 1

        coordinates = {"x": x, "y": y, "z": z}
        for axis in ("x", "y", "z"):
            horizontal_a, horizontal_b = horizontal_components(axis, coordinates)
            horizontal = math.hypot(horizontal_a, horizontal_b)

            vertical = coordinates[axis]
            if horizontal > max_horizontal_range:
                continue
            if vertical <= -near_ground_depth:
                selected_near_ground[axis] += 1
            if vertical <= -ring_ground_depth:
                label = ring_label(horizontal)
                if label is not None:
                    axis_ring_ground_counts[axis][label] += 1
                sector = sector_label(axis, coordinates)
                if sector is not None:
                    axis_sector_ground_counts[axis][sector] += 1
            for depth, label in depth_thresholds:
                if vertical <= -depth:
                    axis_depth_counts[axis][label] += 1

    return {
        "points": total_points,
        "valid": valid,
        "near_ground": near_ground,
        "selected_near_ground": selected_near_ground,
        "axis_depth_counts": axis_depth_counts,
        "axis_ring_ground_counts": axis_ring_ground_counts,
        "axis_sector_ground_counts": axis_sector_ground_counts,
        "min": tuple(min_values),
        "max": tuple(max_values),
        "fields": ",".join(field.name for field in message.fields),
    }


def format_triplet(values):
    return ",".join("nan" if not math.isfinite(value) else f"{value:.3f}" for value in values)


def sum_axis_counts(samples):
    totals = {axis: 0 for axis in ("x", "y", "z")}
    for sample in samples:
        for axis in totals:
            totals[axis] += sample["selected_near_ground"].get(axis, 0)
    return totals


def sum_axis_depth_counts(samples):
    totals = empty_axis_counts()
    for sample in samples:
        for axis in totals:
            for label in totals[axis]:
                totals[axis][label] += sample["axis_depth_counts"].get(axis, {}).get(label, 0)
    return totals


def sum_axis_ring_counts(samples):
    totals = empty_axis_ring_counts()
    for sample in samples:
        for axis in totals:
            for label in totals[axis]:
                totals[axis][label] += sample["axis_ring_ground_counts"].get(axis, {}).get(label, 0)
    return totals


def sum_axis_sector_counts(samples):
    totals = empty_axis_sector_counts()
    for sample in samples:
        for axis in totals:
            for label in totals[axis]:
                totals[axis][label] += sample["axis_sector_ground_counts"].get(axis, {}).get(label, 0)
    return totals


def sector_balance_ratio(counts):
    values = [counts[label] for label in sector_labels()]
    max_value = max(values) if values else 0
    min_value = min(values) if values else 0
    if max_value <= 0:
        return 0.0
    return min_value / max_value


def choose_vertical_axis(axis_counts, requested_axis):
    if requested_axis != "auto":
        return requested_axis
    return max(("x", "y", "z"), key=lambda axis: axis_counts.get(axis, 0))


def png_chunk(chunk_type, payload):
    return (
        struct.pack(">I", len(payload))
        + chunk_type
        + payload
        + struct.pack(">I", zlib.crc32(chunk_type + payload) & 0xFFFFFFFF)
    )


def write_rgb_png(path, width, height, pixels):
    rows = bytearray()
    stride = width * 3
    for y in range(height):
        rows.append(0)
        offset = y * stride
        rows.extend(pixels[offset : offset + stride])
    data = bytearray(b"\x89PNG\r\n\x1a\n")
    data.extend(png_chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)))
    data.extend(png_chunk(b"IDAT", zlib.compress(bytes(rows), 6)))
    data.extend(png_chunk(b"IEND", b""))
    with open(path, "wb") as file:
        file.write(data)


def set_pixel(pixels, width, height, x, y, color):
    if x < 0 or y < 0 or x >= width or y >= height:
        return
    offset = (y * width + x) * 3
    pixels[offset : offset + 3] = bytes(color)


def draw_square(pixels, width, height, x, y, radius, color):
    for yy in range(y - radius, y + radius + 1):
        for xx in range(x - radius, x + radius + 1):
            set_pixel(pixels, width, height, xx, yy, color)


def draw_line(pixels, width, height, x0, y0, x1, y1, color):
    dx = abs(x1 - x0)
    sx = 1 if x0 < x1 else -1
    dy = -abs(y1 - y0)
    sy = 1 if y0 < y1 else -1
    error = dx + dy
    while True:
        set_pixel(pixels, width, height, x0, y0, color)
        if x0 == x1 and y0 == y1:
            break
        e2 = 2 * error
        if e2 >= dy:
            error += dy
            x0 += sx
        if e2 <= dx:
            error += dx
            y0 += sy


def draw_circle(pixels, width, height, cx, cy, radius, color):
    if radius <= 0:
        return
    last_x = cx + radius
    last_y = cy
    for index in range(1, 361):
        angle = index * math.tau / 360.0
        x = cx + round(math.cos(angle) * radius)
        y = cy + round(math.sin(angle) * radius)
        draw_line(pixels, width, height, last_x, last_y, x, y, color)
        last_x, last_y = x, y


def create_topdown_canvas(size, topdown_range):
    size = max(128, int(size))
    pixels = bytearray([28, 28, 28] * (size * size))
    center = size // 2
    grid = (78, 78, 78)
    draw_line(pixels, size, size, center, 0, center, size - 1, grid)
    draw_line(pixels, size, size, 0, center, size - 1, center, grid)
    for meters in (5.0, 10.0, 20.0, 40.0):
        if meters <= topdown_range:
            radius = round(meters / topdown_range * (size * 0.5))
            draw_circle(pixels, size, size, center, center, radius, (88, 88, 88))
    draw_square(pixels, size, size, center, center, 5, (80, 170, 255))
    return pixels


def plot_topdown_point(pixels, size, topdown_range, forward, left, color, radius=1):
    if abs(forward) > topdown_range or abs(left) > topdown_range:
        return False
    center = size // 2
    scale = (size * 0.5) / topdown_range
    px = center + round(left * scale)
    py = center - round(forward * scale)
    draw_square(pixels, size, size, px, py, radius, color)
    return True


def draw_message_topdown(message, pixels, size, topdown_range, vertical_axis, ground_depth, max_points, drawn_count):
    fields = field_map(message)
    if not all(name in fields for name in ("x", "y", "z")):
        return drawn_count

    data = bytes(message.data)
    total_points = message.width * message.height
    stride = max(1, math.ceil(total_points / 8000))
    for index in range(0, total_points, stride):
        if drawn_count >= max_points:
            break
        base = index * message.point_step
        x = read_scalar(data, base, fields["x"], message.is_bigendian)
        y = read_scalar(data, base, fields["y"], message.is_bigendian)
        z = read_scalar(data, base, fields["z"], message.is_bigendian)
        if not all(finite(value) for value in (x, y, z)):
            continue
        coordinates = {"x": x, "y": y, "z": z}
        forward, left = horizontal_components(vertical_axis, coordinates)
        horizontal = math.hypot(forward, left)
        if horizontal <= 0.0001 or horizontal > topdown_range:
            continue
        vertical = coordinates[vertical_axis]
        if vertical <= -ground_depth:
            color = (238, 205, 92)
            radius = 1
        else:
            color = (255, 116, 48)
            radius = 1
        if plot_topdown_point(pixels, size, topdown_range, forward, left, color, radius):
            drawn_count += 1
    return drawn_count


def main():
    args = parse_args()
    rclpy.init()
    node = rclpy.create_node("vln_lidar_ground_return_capture")
    samples = []
    first_metadata = None
    topdown_pixels = create_topdown_canvas(args.topdown_size, args.topdown_range) if args.topdown_png else None
    topdown_drawn_points = 0

    def callback(message):
        nonlocal first_metadata, topdown_drawn_points
        if first_metadata is None:
            first_metadata = (
                f"frame_id={message.header.frame_id} "
                f"width={message.width} height={message.height} "
                f"point_step={message.point_step} row_step={message.row_step} "
                f"is_dense={int(message.is_dense)}"
            )
        samples.append(analyze_message(message, args.max_horizontal_range, args.near_ground_z, args.near_ground_depth, args.ring_ground_depth))
        if topdown_pixels is not None:
            axis_for_plot = args.vertical_axis if args.vertical_axis != "auto" else "z"
            topdown_drawn_points = draw_message_topdown(
                message,
                topdown_pixels,
                max(128, int(args.topdown_size)),
                args.topdown_range,
                axis_for_plot,
                args.ring_ground_depth,
                args.topdown_max_points,
                topdown_drawn_points,
            )

    subscription = node.create_subscription(PointCloud2, args.topic, callback, 2)
    deadline = time.monotonic() + args.timeout
    sample_deadline = None
    try:
        while time.monotonic() < deadline:
            rclpy.spin_once(node, timeout_sec=0.1)
            if samples and sample_deadline is None:
                sample_deadline = time.monotonic() + args.duration
            if sample_deadline is not None and time.monotonic() >= sample_deadline:
                break

        print(f"topic={args.topic}")
        print(f"sample_duration_s={args.duration:.3f}")
        print(f"legacy_near_ground_rule=horizontal_range_xy<={args.max_horizontal_range:.2f}m and z<={args.near_ground_z:.2f}m")
        print(f"axis_near_ground_rule=horizontal_range_around_axis<={args.max_horizontal_range:.2f}m and selected_axis<=-{args.near_ground_depth:.2f}m")
        print(f"ring_ground_rule=selected_axis<=-{args.ring_ground_depth:.2f}m with horizontal rings 0-2,2-5,5-10,10-20,20-40m")
        print(f"message_count={len(samples)}")
        if first_metadata:
            print(first_metadata)
        if not samples:
            print("status=timeout_no_pointcloud", file=sys.stderr)
            return 1

        valid_samples = [sample for sample in samples if sample["valid"] > 0]
        total_points = sum(sample["points"] for sample in valid_samples)
        total_valid = sum(sample["valid"] for sample in valid_samples)
        total_near_ground = sum(sample["near_ground"] for sample in valid_samples)
        axis_counts = sum_axis_counts(valid_samples)
        axis_depth_counts = sum_axis_depth_counts(valid_samples)
        axis_ring_counts = sum_axis_ring_counts(valid_samples)
        axis_sector_counts = sum_axis_sector_counts(valid_samples)
        selected_axis = choose_vertical_axis(axis_counts, args.vertical_axis)
        selected_near_ground = axis_counts[selected_axis]
        print(f"total_points={total_points}")
        print(f"valid_points={total_valid}")
        print(f"legacy_z_near_ground_points={total_near_ground}")
        if total_valid:
            print(f"legacy_z_near_ground_ratio={total_near_ground / total_valid:.6f}")
        print(f"selected_vertical_axis={selected_axis}")
        print(f"near_ground_points={selected_near_ground}")
        if total_valid:
            print(f"near_ground_ratio={selected_near_ground / total_valid:.6f}")
        for axis in ("x", "y", "z"):
            print(f"axis_{axis}_near_ground_points={axis_counts[axis]}")
            if total_valid:
                print(f"axis_{axis}_near_ground_ratio={axis_counts[axis] / total_valid:.6f}")
            print(
                "axis_" + axis + "_depth_counts=" +
                ",".join(f"below_{label}m:{axis_depth_counts[axis][label]}" for label in ("0.25", "0.50", "1.00", "1.50", "2.00", "3.00"))
            )
            print(
                "axis_" + axis + "_ring_ground_counts=" +
                ",".join(f"r{label}m:{axis_ring_counts[axis][label]}" for label in ring_labels())
            )
            print(
                "axis_" + axis + "_sector_ground_counts=" +
                ",".join(f"{label}:{axis_sector_counts[axis][label]}" for label in sector_labels())
            )

        print(
            "selected_axis_ring_ground_counts=" +
            ",".join(f"r{label}m:{axis_ring_counts[selected_axis][label]}" for label in ring_labels())
        )
        print(
            "selected_axis_sector_ground_counts=" +
            ",".join(f"{label}:{axis_sector_counts[selected_axis][label]}" for label in sector_labels())
        )
        print(f"selected_axis_sector_balance_ratio={sector_balance_ratio(axis_sector_counts[selected_axis]):.6f}")
        if args.topdown_png:
            write_rgb_png(args.topdown_png, max(128, int(args.topdown_size)), max(128, int(args.topdown_size)), topdown_pixels)
            print(f"topdown_png={args.topdown_png}")
            print(f"topdown_drawn_points={topdown_drawn_points}")
            print(f"topdown_vertical_axis={args.vertical_axis if args.vertical_axis != 'auto' else 'z'}")

        for label, sample in (("first", samples[0]), ("last", samples[-1])):
            print(
                f"{label}_points={sample['points']} "
                f"{label}_valid={sample['valid']} "
                f"{label}_legacy_z_near_ground={sample['near_ground']} "
                f"{label}_selected_near_ground={sample['selected_near_ground'].get(selected_axis, 0)} "
                f"{label}_min={format_triplet(sample['min'])} "
                f"{label}_max={format_triplet(sample['max'])}"
            )

        print("VLN_LIDAR_GROUND_RETURN_CAPTURE_OK")
        return 0
    finally:
        node.destroy_subscription(subscription)
        node.destroy_node()
        if rclpy.ok():
            rclpy.shutdown()


if __name__ == "__main__":
    raise SystemExit(main())
