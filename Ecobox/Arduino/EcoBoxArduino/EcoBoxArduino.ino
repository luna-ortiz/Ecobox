/*
  EcoBox - Arduino + servo
  Recibe por serial (9600):  'A' = aprovechable   'N' = no aprovechable
  Un servo inclina la bandeja/compuerta hacia un lado u otro y luego vuelve al centro.

  Conexión: servo señal (naranja/amarillo) -> pin 9, rojo -> 5V, café/negro -> GND
  (si el servo se reinicia el Arduino, alimentarlo con fuente externa de 5V y unir GND)
*/
#include <Servo.h>

const int PIN_SERVO = 9;
const int CENTRO = 90;
const int ANGULO_APROVECHABLE = 150;     // ajustar según tu estructura
const int ANGULO_NO_APROVECHABLE = 30;   // ajustar según tu estructura
const unsigned long TIEMPO_ABIERTO = 2500; // ms

Servo compuerta;

void setup() {
  Serial.begin(9600);
  compuerta.attach(PIN_SERVO);
  compuerta.write(CENTRO);
  pinMode(LED_BUILTIN, OUTPUT);
}

void mover(int angulo) {
  digitalWrite(LED_BUILTIN, HIGH);
  compuerta.write(angulo);
  delay(TIEMPO_ABIERTO);      // el residuo cae
  compuerta.write(CENTRO);
  delay(500);
  digitalWrite(LED_BUILTIN, LOW);
}

void loop() {
  if (Serial.available() > 0) {
    char c = Serial.read();
    if (c == 'A')      mover(ANGULO_APROVECHABLE);
    else if (c == 'N') mover(ANGULO_NO_APROVECHABLE);
  }
}
