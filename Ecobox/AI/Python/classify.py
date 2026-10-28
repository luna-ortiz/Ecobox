#!/usr/bin/env python3
"""Run one EcoBox Keras image classification and emit a single JSON response."""

import argparse
import contextlib
import json
import sys
import traceback
from pathlib import Path


EXPECTED_CLASSES = ["aprovechable", "no_aprovechable"]


def classify(image_path: Path, model_path: Path, threshold: float) -> dict:
    with contextlib.redirect_stdout(sys.stderr):
        import cv2
        import numpy as np
        import tensorflow as tf

    script_dir = Path(__file__).resolve().parent
    with (script_dir / "clases.json").open("r", encoding="utf-8") as classes_file:
        classes = json.load(classes_file)
    if classes != EXPECTED_CLASSES:
        raise ValueError("clases.json debe contener las dos clases EcoBox en el orden definido.")

    # Keep TensorFlow/Keras diagnostics off stdout so stdout remains valid JSON.
    with contextlib.redirect_stdout(sys.stderr):
        model = tf.keras.models.load_model(str(model_path))

    image = cv2.imread(str(image_path))
    if image is None:
        raise ValueError("No se pudo leer la imagen proporcionada.")

    image = cv2.resize(cv2.cvtColor(image, cv2.COLOR_BGR2RGB), (224, 224))
    with contextlib.redirect_stdout(sys.stderr):
        probabilities = model.predict(image[None].astype("float32"), verbose=0)[0]

    if len(probabilities) != len(classes):
        raise ValueError("La salida del modelo no coincide con las clases configuradas.")

    index = int(np.argmax(probabilities))
    confidence = float(probabilities[index])
    if not np.isfinite(confidence) or not 0.0 <= confidence <= 1.0:
        raise ValueError("El modelo devolvió una confianza inválida.")

    if confidence < threshold:
        return {
            "exito": False,
            "categoria": None,
            "confianza": confidence,
            "mensaje": "Confianza insuficiente",
        }

    return {
        "exito": True,
        "categoria": classes[index].upper(),
        "confianza": confidence,
        "mensaje": "Clasificación realizada correctamente",
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("imagen", type=Path)
    parser.add_argument("--model", type=Path, default=Path(__file__).resolve().parent / "ecobox_modelo.keras")
    parser.add_argument("--threshold", type=float, default=0.70)

    try:
        args = parser.parse_args()
        if not 0.0 <= args.threshold <= 1.0:
            raise ValueError("El umbral de confianza debe estar entre 0 y 1.")
        result = classify(args.imagen, args.model, args.threshold)
    except (Exception, SystemExit) as error:  # Always return JSON even when Python dependencies/model/image fail.
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
