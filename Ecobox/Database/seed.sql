INSERT INTO tipo_residuo (clasificacion)
VALUES ('APROVECHABLE'), ('NO_APROVECHABLE')
ON CONFLICT (clasificacion) DO NOTHING;

INSERT INTO contenedor (nombre, capacidad_maxima, nivel_actual, estado)
VALUES
    ('Aprovechables', 50, 0, 'VACÍA'),
    ('No aprovechables', 50, 0, 'VACÍA')
ON CONFLICT (nombre) DO NOTHING;
