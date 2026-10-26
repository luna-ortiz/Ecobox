# Protocolo serial EcoBox

La comunicación futura usa líneas de texto terminadas en salto de línea (`\n`). No se fija placa, puerto COM, pines, sensores ni ángulos de servo.

## Mensajes

- Arduino → aplicación: `RESIDUO_DETECTADO` cuando exista una detección física definida.
- Aplicación → Arduino: `APROVECHABLE` o `NO_APROVECHABLE` después de una clasificación válida.
- Arduino → aplicación: `LISTO` cuando complete la acción física, o `ERROR` si no puede ejecutarla.

La aplicación serial está preparada con `System.IO.Ports`. `Arduino:Port` queda vacío hasta configurar el puerto real; `Arduino:BaudRate` usa inicialmente 9600. La placa y el firmware actuales no ejecutan movimiento físico y responden `ERROR` a una clasificación.

Cuando se definan placa y componentes, el firmware podrá accionar los dos servos y la pantalla LCD. No debe responder `LISTO` antes de completar la acción correspondiente.
