#!/usr/bin/env python3
"""Persistent NDJSON inference worker. Loads Keras once and serves frames on stdin."""

import argparse
import base64
import json
import sys
import traceback
from pathlib import Path

import cv2
import numpy as np

from classify import classify_bgr, load_model


def emit(payload: dict) -> None:
    sys.stdout.write(json.dumps(payload, ensure_ascii=False, allow_nan=False, separators=(",", ":")) + "\n")
    sys.stdout.flush()


class DebugFrameRing:
    def __init__(self, directory: Path, limit: int):
        self.directory = directory
        self.limit = min(10, max(1, limit))
        self.directory.mkdir(parents=True, exist_ok=True)
        self.count = 0

    def save(self, image_rgb, roi_info: dict) -> None:
        index = self.count % self.limit
        output_path = self.directory / f"roi-model-input-{index + 1:02d}.jpg"
        image_bgr = cv2.cvtColor(image_rgb, cv2.COLOR_RGB2BGR)
        if not cv2.imwrite(str(output_path), image_bgr):
            raise OSError(f"No se pudo guardar {output_path}")
        self.count += 1
        print(
            "Frame de diagnóstico guardado: "
            f"{output_path}; ROI={roi_info['width']}x{roi_info['height']}; entrada={image_rgb.shape[1]}x{image_rgb.shape[0]}",
            file=sys.stderr,
            flush=True,
        )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", type=Path, required=True)
    parser.add_argument("--classes", type=Path, required=True)
    parser.add_argument("--threshold", type=float, default=0.70)
    parser.add_argument("--roi-size-ratio", type=float, default=0.85)
    parser.add_argument("--save-debug-frames", action="store_true")
    parser.add_argument("--debug-dir", type=Path, default=Path("."))
    parser.add_argument("--debug-frame-limit", type=int, default=8)
    args = parser.parse_args()

    try:
        if not 0.0 <= args.threshold <= 1.0:
            raise ValueError("El umbral de confianza debe estar entre 0 y 1.")
        if not 0.0 < args.roi_size_ratio <= 1.0:
            raise ValueError("El tamaño de ROI debe estar entre 0 y 1.")
        model, classes = load_model(args.model, args.classes)
        debug_ring = DebugFrameRing(args.debug_dir, args.debug_frame_limit) if args.save_debug_frames else None
    except Exception as error:
        traceback.print_exc(file=sys.stderr)
        emit({"tipo": "ready", "exito": False, "mensaje": str(error)})
        return 1

    emit({"tipo": "ready", "exito": True, "mensaje": "Modelo de visión listo."})

    for line in sys.stdin:
        request = None
        try:
            request = json.loads(line)
            image_bytes = base64.b64decode(request["imagen_base64"], validate=True)
            frame = cv2.imdecode(np.frombuffer(image_bytes, dtype=np.uint8), cv2.IMREAD_COLOR)
            if frame is None:
                raise ValueError("No se pudo decodificar el frame JPEG.")

            result = classify_bgr(
                frame,
                model,
                classes,
                args.threshold,
                args.roi_size_ratio,
                debug_ring.save if debug_ring else None,
            )
            roi = result["roi"]
            print(
                f"ROI detectada: x={roi['x']}, y={roi['y']}, "
                f"{roi['width']}x{roi['height']} sobre {roi['source_width']}x{roi['source_height']}; "
                "frame enviado a TensorFlow: 224x224 RGB float32; "
                f"predicción={result['prediccion']}; confianza={result['confianza']:.4f}; "
                f"tiempos_ms={json.dumps(result['timings_ms'], separators=(',', ':'))}",
                file=sys.stderr,
                flush=True,
            )
            emit({"tipo": "resultado", "id": request.get("id"), **result})
        except Exception as error:
            traceback.print_exc(file=sys.stderr)
            emit({
                "tipo": "resultado",
                "id": request.get("id") if isinstance(request, dict) else None,
                "exito": False,
                "categoria": None,
                "prediccion": None,
                "confianza": 0,
                "mensaje": str(error) or "Error durante la clasificación.",
            })

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
