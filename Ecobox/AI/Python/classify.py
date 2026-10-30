#!/usr/bin/env python3
"""Single-image EcoBox inference and the shared preprocessing used by vision_server.py."""

import argparse
import contextlib
import json
import sys
import time
import traceback
from pathlib import Path


EXPECTED_CLASSES = ["aprovechable", "no_aprovechable"]
MODEL_INPUT_SIZE = 224


def load_model(model_path: Path, classes_path: Path):
    with contextlib.redirect_stdout(sys.stderr):
        import tensorflow as tf

    with classes_path.open("r", encoding="utf-8") as classes_file:
        classes = json.load(classes_file)
    if classes != EXPECTED_CLASSES:
        raise ValueError("clases.json debe contener las dos clases EcoBox en el orden definido.")

    with contextlib.redirect_stdout(sys.stderr):
        model = tf.keras.models.load_model(str(model_path))
    return model, classes


def crop_center_square(frame, roi_size_ratio: float):
    height, width = frame.shape[:2]
    if not 0.0 < roi_size_ratio <= 1.0:
        raise ValueError("RoiSizeRatio debe estar entre 0 y 1.")

    side = max(1, min(min(width, height), round(min(width, height) * roi_size_ratio)))
    x = (width - side) // 2
    y = (height - side) // 2
    roi = frame[y:y + side, x:x + side]
    return roi, {
        "x": x,
        "y": y,
        "width": side,
        "height": side,
        "source_width": width,
        "source_height": height,
    }


def perceptual_hash(roi, cv2) -> str:
    gray = cv2.cvtColor(roi, cv2.COLOR_BGR2GRAY)
    sample = cv2.resize(gray, (9, 8), interpolation=cv2.INTER_AREA)
    differences = sample[:, 1:] > sample[:, :-1]
    value = 0
    for bit in differences.flatten():
        value = (value << 1) | int(bit)
    return f"{value:016x}"


def classify_bgr(frame, model, classes, threshold: float, roi_size_ratio: float = 0.85, debug_frame_sink=None) -> dict:
    import cv2
    import numpy as np

    total_start = time.perf_counter()
    roi_start = total_start
    roi, roi_info = crop_center_square(frame, roi_size_ratio)
    roi_ms = (time.perf_counter() - roi_start) * 1000

    preprocess_start = time.perf_counter()
    roi_hash = perceptual_hash(roi, cv2)
    # Keep the validated model preprocessing: BGR -> RGB, 224x224, float32, batch dimension.
    image_rgb = cv2.resize(
        cv2.cvtColor(roi, cv2.COLOR_BGR2RGB),
        (MODEL_INPUT_SIZE, MODEL_INPUT_SIZE),
        interpolation=cv2.INTER_LINEAR,
    )
    model_input = image_rgb.astype("float32")[None]
    preprocess_ms = (time.perf_counter() - preprocess_start) * 1000

    debug_ms = 0.0
    if debug_frame_sink is not None:
        debug_start = time.perf_counter()
        try:
            debug_frame_sink(image_rgb, roi_info)
        except Exception as error:
            print(f"No se pudo guardar el frame de diagnóstico: {error}", file=sys.stderr)
        debug_ms = (time.perf_counter() - debug_start) * 1000

    inference_start = time.perf_counter()
    with contextlib.redirect_stdout(sys.stderr):
        probabilities = model.predict(model_input, verbose=0)[0]
    inference_ms = (time.perf_counter() - inference_start) * 1000

    if len(probabilities) != len(classes):
        raise ValueError("La salida del modelo no coincide con las clases configuradas.")

    index = int(np.argmax(probabilities))
    confidence = float(probabilities[index])
    if not np.isfinite(confidence) or not 0.0 <= confidence <= 1.0:
        raise ValueError("El modelo devolvió una confianza inválida.")

    result = {
        "categoria": classes[index].upper() if confidence >= threshold else None,
        "prediccion": classes[index].upper(),
        "confianza": confidence,
        "exito": confidence >= threshold,
        "mensaje": "Clasificación realizada correctamente" if confidence >= threshold else "Confianza insuficiente",
        "roi_hash": roi_hash,
        "roi": {**roi_info, "input_width": MODEL_INPUT_SIZE, "input_height": MODEL_INPUT_SIZE},
        "timings_ms": {
            "roi_crop": round(roi_ms, 2),
            "preprocess": round(preprocess_ms, 2),
            "debug_save": round(debug_ms, 2),
            "inference": round(inference_ms, 2),
            "total_python": round((time.perf_counter() - total_start) * 1000, 2),
        },
    }
    return result


def classify(
    image_path: Path,
    model_path: Path,
    threshold: float,
    classes_path: Path | None = None,
    roi_size_ratio: float = 0.85,
) -> dict:
    import cv2

    if classes_path is None:
        classes_path = Path(__file__).resolve().parent / "clases.json"
    model, classes = load_model(model_path, classes_path)
    frame = cv2.imread(str(image_path))
    if frame is None:
        raise ValueError("No se pudo leer la imagen proporcionada.")
    return classify_bgr(frame, model, classes, threshold, roi_size_ratio)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("imagen", type=Path)
    parser.add_argument("--model", type=Path, default=Path(__file__).resolve().parent / "ecobox_modelo.keras")
    parser.add_argument("--classes", type=Path, default=Path(__file__).resolve().parent / "clases.json")
    parser.add_argument("--threshold", type=float, default=0.70)
    parser.add_argument("--roi-size-ratio", type=float, default=0.85)

    try:
        args = parser.parse_args()
        if not 0.0 <= args.threshold <= 1.0:
            raise ValueError("El umbral de confianza debe estar entre 0 y 1.")
        result = classify(args.imagen, args.model, args.threshold, args.classes, args.roi_size_ratio)
    except (Exception, SystemExit) as error:
        traceback.print_exc(file=sys.stderr)
        result = {
            "exito": False,
            "categoria": None,
            "confianza": 0,
            "mensaje": str(error) or "Error durante la clasificación.",
        }

    print(json.dumps(result, ensure_ascii=False, allow_nan=False))


if __name__ == "__main__":
    main()
