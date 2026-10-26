/*
 * Esqueleto del protocolo serial EcoBox.
 * No se definen pines, servos, sensores ni placa hasta confirmar el hardware.
 * Las respuestas LISTO solo deben habilitarse cuando la acción física esté implementada.
 */

String lineaRecibida;

void setup() {
  Serial.begin(9600);
  lineaRecibida.reserve(64);
}

void loop() {
  while (Serial.available() > 0) {
    const char caracter = static_cast<char>(Serial.read());
    if (caracter == '\n') {
      procesarComando(lineaRecibida);
      lineaRecibida = "";
    } else if (caracter != '\r' && lineaRecibida.length() < 63) {
      lineaRecibida += caracter;
    }
  }
}

void procesarComando(const String& comando) {
  if (comando == "APROVECHABLE" || comando == "NO_APROVECHABLE") {
    // No confirmar LISTO antes de integrar el mecanismo físico correspondiente.
    Serial.println("ERROR");
    return;
  }

  Serial.println("ERROR");
}
