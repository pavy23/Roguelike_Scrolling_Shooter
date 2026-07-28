#!/usr/bin/env python3
"""
Visual Regression Diff Tool for QA testing.
Compares two PNG images using Python & PIL (Pillow) and computes the pixel mismatch percentage.

Usage:
    python QA/tools/visual_diff.py <img1_path> <img2_path> [--threshold 5.0] [--tolerance 0] [--diff-out diff.png]
"""

import sys
import argparse
from PIL import Image, ImageChops, ImageEnhance


def compare_images(img1_path, img2_path, threshold=5.0, tolerance=0, diff_out=None):
    """
    Compares two images and calculates the pixel mismatch rate (percentage).
    Returns (mismatch_pct, passed).
    """
    try:
        img1 = Image.open(img1_path).convert("RGB")
    except Exception as e:
        print(f"Error opening image 1 ({img1_path}): {e}", file=sys.stderr)
        sys.exit(2)
        
    try:
        img2 = Image.open(img2_path).convert("RGB")
    except Exception as e:
        print(f"Error opening image 2 ({img2_path}): {e}", file=sys.stderr)
        sys.exit(2)

    if img1.size != img2.size:
        print(f"FAILED: Image dimensions mismatch! ({img1_path}: {img1.size} vs {img2_path}: {img2.size})")
        return 100.0, False

    width, height = img1.size
    total_pixels = width * height

    # Fast diff computation using ImageChops
    diff = ImageChops.difference(img1, img2)
    
    # Evaluate per-pixel difference using raw bytes
    raw_bytes = diff.tobytes()
    mismatched_pixels = 0

    if tolerance == 0:
        for i in range(0, len(raw_bytes), 3):
            if raw_bytes[i] != 0 or raw_bytes[i + 1] != 0 or raw_bytes[i + 2] != 0:
                mismatched_pixels += 1
    else:
        for i in range(0, len(raw_bytes), 3):
            if raw_bytes[i] > tolerance or raw_bytes[i + 1] > tolerance or raw_bytes[i + 2] > tolerance:
                mismatched_pixels += 1

    mismatch_pct = (mismatched_pixels / total_pixels) * 100.0
    passed = mismatch_pct <= threshold

    print("=== Visual Regression Diff Report ===")
    print(f"Image 1    : {img1_path}")
    print(f"Image 2    : {img2_path}")
    print(f"Resolution : {width}x{height} ({total_pixels:,} pixels)")
    print(f"Mismatched : {mismatched_pixels:,} pixels")
    print(f"Mismatch % : {mismatch_pct:.2f}%")
    print(f"Threshold  : {threshold:.2f}%")
    print(f"Status     : {'PASSED' if passed else 'FAILED'}")

    if diff_out:
        # Generate diff heatmap/highlight image
        enhanced_diff = ImageEnhance.Brightness(diff).enhance(3.0)
        enhanced_diff.save(diff_out)
        print(f"Diff output saved to: {diff_out}")

    return mismatch_pct, passed


def main():
    parser = argparse.ArgumentParser(description="Compute visual pixel mismatch between two PNG images.")
    parser.add_argument("img1", help="Path to baseline or reference image")
    parser.add_argument("img2", help="Path to target or newly captured image")
    parser.add_argument("--threshold", type=float, default=5.0, help="Pixel mismatch percentage threshold (default: 5.0%%)")
    parser.add_argument("--tolerance", type=int, default=0, help="RGB channel color tolerance per pixel (default: 0)")
    parser.add_argument("--diff-out", type=str, default=None, help="Optional path to save visual diff heatmap image")

    args = parser.parse_args()

    mismatch_pct, passed = compare_images(
        args.img1,
        args.img2,
        threshold=args.threshold,
        tolerance=args.tolerance,
        diff_out=args.diff_out
    )

    sys.exit(0 if passed else 1)


if __name__ == "__main__":
    main()
